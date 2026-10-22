using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Actions;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
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
/// </summary>
internal static class VoteGate
{
    private const int PollMs = 100;
    private const int ResultPauseMs = 900;
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
            if (card == null || combat == null || !TestSubjectState.IsMultiplayer()) return false;

            var network = VoteNetwork.Instance;
            var livePlayers = combat.Players.Where(p => p.Creature.IsAlive).Select(p => p.NetId).ToList();
            bool needsVote = VoteEligibility.NeedsVote(
                isSkill: card.Type == CardType.Skill,
                isManualPlay: true,
                isMultiplayer: true,
                isFirstForm: TestSubjectState.IsFirstForm(combat),
                allPeersHaveMod: network.Roster.AllHaveMod(combat.Players.Select(p => p.NetId)));
            var voters = VoteEligibility.VoterIds(livePlayers, action.Player.NetId);
            if (!needsVote || voters.Count == 0) return false;

            held = new HeldPlay(card, combat, voters, TestSubjectState.EnrageAmount(combat));
            return true;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 TestSubjectVote] could not evaluate play, letting it through: {ex.Message}");
            return false;
        }
    }

    public static async Task RunAsync(PlayCardAction action, HeldPlay held)
    {
        var network = VoteNetwork.Instance;
        ulong localId = RunManager.Instance.NetService.NetId;
        var key = network.Book.NextKey(action.Player.NetId, action.NetCombatCard.CombatCardIndex);
        var tally = network.Book.Open(key, held.Voters);
        var view = OpenView(action, held, key, localId);
        bool abandoned = false;

        try
        {
            while (tally.Outcome == VoteOutcome.Pending)
            {
                if (action.State == GameActionState.Canceled || action.Player.Creature.CombatState != held.Combat)
                {
                    abandoned = true;
                    break;
                }
                foreach (ulong voter in held.Voters)
                {
                    if (!held.Combat.Players.Any(p => p.NetId == voter && p.Creature.IsAlive)) tally.Remove(voter);
                }
                view?.Update(tally);
                await Task.Delay(PollMs);
            }

            if (!abandoned)
            {
                bool allowed = tally.Outcome == VoteOutcome.Allowed;
                if (allowed) view?.ShowApproved(VoteCopy.ApprovedQuip(key.Attempt));
                else view?.ShowDenied();
                if (view != null) await Task.Delay(ResultPauseMs);
                Resolve(action, held.Card, allowed);
            }
        }
        finally
        {
            view?.Close();
            network.Book.Close(key);
        }

        if (!abandoned && tally.Outcome == VoteOutcome.Allowed && Approved.Contains(action))
        {
            await (Task)OriginalExecuteAction!.Invoke(action, null)!;
        }
    }

    private static VotePopupView? OpenView(PlayCardAction action, HeldPlay held, PlayKey key, ulong localId)
    {
        string cardName = held.Card.Title;
        if (held.Voters.Contains(localId))
        {
            return VotePopupFactory.CreateForVoter(action.Player.Creature.Name, cardName, CardPreview(held.Card), held.Strength,
                allow => VoteNetwork.Instance.SendVote(key, localId, allow));
        }
        return action.Player.NetId == localId ? VotePopupFactory.CreateForWaiting(cardName, CardPreview(held.Card)) : null;
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
            GD.PrintErr($"[STS2 TestSubjectVote] no card preview: {ex.Message}");
            return null;
        }
    }

    private static void Resolve(PlayCardAction action, CardModel card, bool allowed)
    {
        if (allowed)
        {
            Approved.Add(action);
            return;
        }
        NCardPlayQueue.Instance?.RemoveCardFromQueueForCancellation(action);
        if (MegaCrit.Sts2.Core.Context.LocalContext.IsMe(action.Player)) NPlayerHand.Instance?.TryCancelCardPlay(card);
    }

    internal readonly record struct HeldPlay(CardModel Card, ICombatState Combat, IReadOnlyList<ulong> Voters, int Strength);
}
