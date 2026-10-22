using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Pooling;
using MegaCrit.Sts2.Core.Runs;
using SlayTheSpireTestSubjectVote.Core.Voting;
using SlayTheSpireTestSubjectVote.Godot.Game;
using SlayTheSpireTestSubjectVote.Godot.Net;
using SlayTheSpireTestSubjectVote.Godot.UI;

namespace SlayTheSpireTestSubjectVote.Godot.Patches;

/// <summary>
/// Holds a Skill play before PlayCardAction.ExecuteAction spends energy, runs the vote, then either lets the
/// original ExecuteAction run (allowed) or puts the card back (rejected).
///
/// Why not GameAction.Cancel(): it flips the action to Canceled, after which GameAction.Execute() logs an
/// "unexpected state" error. We mirror the game's own "card is no longer in hand" early return instead:
/// remove the card from the play queue and, for the local player, let the hand take it back.
///
/// The game runs every action through one serial executor, so a held play blocks everyone until it resolves.
/// Nothing here may leave it unresolved: UI failures are contained, and the host can skip a stuck vote.
/// </summary>
internal static class VoteGate
{
    private const int PollMs = 100;
    private const int ResultPauseMs = 900;
    private const string LogPrefix = "[STS2 TestSubjectVote]";

    /// <summary>The host's hotkey for the escape hatch (the pop-up also has a Skip button).</summary>
    internal static readonly Key SkipKey = Key.F9;

    private static readonly HashSet<PlayCardAction> Approved = [];

    internal static MethodBase? OriginalExecuteAction { get; set; }

    /// <summary>True once for an action that already won its vote, so the prefix lets the original body run.</summary>
    public static bool ConsumeApproval(PlayCardAction action) => Approved.Remove(action);

    public static bool TryBuildHold(PlayCardAction action, out HeldPlay held)
    {
        held = default!;
        try
        {
            var card = action.NetCombatCard.ToCardModelOrNull();
            var combat = action.Player.Creature.CombatState;
            if (card == null || combat == null || action.Id is not { } actionId || !TestSubjectState.IsMultiplayer()) return false;

            var livePlayers = combat.Players.Where(p => p.Creature.IsAlive).Select(p => p.NetId).ToList();
            int strength = TestSubjectState.EnrageAmount(combat);
            var context = new PlayContext
            {
                IsSkill = card.Type == CardType.Skill,
                IsManualPlay = true, // auto-played cards never go through PlayCardAction
                IsMultiplayer = true,
                IsFirstForm = TestSubjectState.IsFirstForm(combat),
                AllPeersHaveMod = VoteNetwork.Instance.Roster.AllHaveMod(combat.Players.Select(p => p.NetId)),
                StrengthGain = strength,
            };
            var voters = VoteEligibility.VoterIds(livePlayers, action.Player.NetId);
            if (!VoteEligibility.NeedsVote(context) || voters.Count == 0) return false;

            held = new HeldPlay(card, combat, voters, strength, new PlayKey(action.Player.NetId, actionId));
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} could not evaluate play, letting it through: {ex.Message}");
            return false;
        }
    }

    public static async Task RunAsync(PlayCardAction action, HeldPlay held)
    {
        var network = VoteNetwork.Instance;
        var key = held.Key;
        ulong localId = RunManager.Instance.NetService.NetId;
        var tally = network.Book.Open(key, held.Voters);
        VotePopupView? view = null;
        bool abandoned = false;
        bool allowed = true;

        try
        {
            view = OpenView(action, held, localId);
            while (tally.Outcome == VoteOutcome.Pending)
            {
                if (IsAbandoned(action, held))
                {
                    abandoned = true;
                    break;
                }
                PruneVoters(held, tally);
                if (WantsSkip(view)) network.SendSkip(key);
                Ui(() => view?.Update(tally));
                await Task.Delay(PollMs);
            }

            if (!abandoned)
            {
                allowed = tally.Outcome == VoteOutcome.Allowed;
                Ui(() =>
                {
                    if (allowed) view?.ShowApproved(VoteCopy.ApprovedQuip((int)key.ActionId));
                    else view?.ShowDenied();
                });
                if (view != null) await Task.Delay(ResultPauseMs);
                abandoned = IsAbandoned(action, held);
            }
        }
        catch (Exception ex)
        {
            // Fail open: a bug in the vote must never strand the card or freeze the shared executor.
            GD.PrintErr($"{LogPrefix} vote failed, letting the play through: {ex}");
            allowed = true;
            abandoned = IsAbandoned(action, held);
        }
        finally
        {
            Ui(() => view?.Close());
            network.Book.Close(key);
        }

        if (abandoned) return;
        if (allowed) await RunOriginal(action);
        else Reject(action, held.Card);
    }

    private static async Task RunOriginal(PlayCardAction action)
    {
        Approved.Add(action);
        try
        {
            await (Task)OriginalExecuteAction!.Invoke(action, null)!;
        }
        finally
        {
            Approved.Remove(action);
        }
    }

    /// <summary>The game cancelled the action, or the fight ended: the play is no longer ours to resolve.</summary>
    private static bool IsAbandoned(PlayCardAction action, HeldPlay held) =>
        action.State == GameActionState.Canceled || action.Player.Creature.CombatState != held.Combat;

    /// <summary>Drops voters who left or died and have not voted yet. Voters who already voted stay counted.</summary>
    private static void PruneVoters(HeldPlay held, VoteTally tally)
    {
        foreach (ulong voter in held.Voters)
        {
            bool gone = VoteNetwork.Instance.Roster.HasLeft(voter)
                || !held.Combat.Players.Any(p => p.NetId == voter && p.Creature.IsAlive);
            if (gone) tally.Remove(voter);
        }
    }

    private static bool WantsSkip(VotePopupView? view) =>
        VoteNetwork.Instance.IsHost && (Input.IsKeyPressed(SkipKey) || view?.ConsumeSkipRequest() == true);

    private static VotePopupView? OpenView(PlayCardAction action, HeldPlay held, ulong localId)
    {
        var options = new PopupOptions(VoteNetwork.Instance.IsHost, SkipKey.ToString(), ReleaseCard);
        string cardName = held.Card.Title;
        bool isVoter = held.Voters.Contains(localId);
        try
        {
            if (isVoter)
            {
                return VotePopupFactory.CreateForVoter(action.Player.Creature.Name, cardName, CardPreview(held.Card), held.Strength,
                    allow => VoteNetwork.Instance.SendVote(held.Key, localId, allow), options);
            }
            return action.Player.NetId == localId ? VotePopupFactory.CreateForWaiting(cardName, CardPreview(held.Card), options) : null;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} could not open the pop-up: {ex.Message}");
            // A voter who cannot see the pop-up must not block everyone else: answer Allow for them.
            if (isVoter) VoteNetwork.Instance.SendVote(held.Key, localId, allow: true);
            return null;
        }
    }

    /// <summary>The game's own card node, so the preview looks exactly like the card in hand. Null if the game cannot make one.</summary>
    private static Control? CardPreview(CardModel card)
    {
        try
        {
            return NCard.Create(card);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} no card preview: {ex.Message}");
            return null;
        }
    }

    private static void ReleaseCard(Control card)
    {
        if (card is NCard pooled) NodePool.Free(pooled);
        else card.QueueFree();
    }

    private static void Reject(PlayCardAction action, CardModel card)
    {
        NCardPlayQueue.Instance?.RemoveCardFromQueueForCancellation(action);
        if (MegaCrit.Sts2.Core.Context.LocalContext.IsMe(action.Player)) NPlayerHand.Instance?.TryCancelCardPlay(card);
    }

    /// <summary>UI is cosmetic: an exception there must never change how the vote resolves.</summary>
    private static void Ui(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogPrefix} pop-up update failed: {ex.Message}");
        }
    }

    internal readonly record struct HeldPlay(CardModel Card, ICombatState Combat, IReadOnlyList<ulong> Voters, int Strength, PlayKey Key);
}
