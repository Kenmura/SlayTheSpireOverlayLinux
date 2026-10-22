using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace SlayTheSpireTestSubjectVote.Godot.Game;

/// <summary>Reads what the vote needs to know about the Test Subject fight. All members used here are public in sts2.dll.</summary>
public static class TestSubjectState
{
    public static Creature? Find(ICombatState? combat) =>
        combat?.Enemies.FirstOrDefault(c => c.Monster is TestSubject);

    /// <summary>First form: it still has Adaptable and has not yet gained Painful Stabs or Nemesis (those arrive on revive).</summary>
    public static bool IsFirstForm(ICombatState? combat)
    {
        var subject = Find(combat);
        return subject != null
            && subject.HasPower<AdaptablePower>()
            && !subject.HasPower<PainfulStabsPower>()
            && !subject.HasPower<NemesisPower>();
    }

    /// <summary>The Strength the Test Subject gains per Skill, read from its own Enrage power (2 or 3 depending on ascension).</summary>
    public static int EnrageAmount(ICombatState? combat) =>
        Find(combat)?.GetPower<EnragePower>() is { } power ? Convert.ToInt32(power.Amount) : 0;

    public static bool IsMultiplayer() =>
        RunManager.Instance.NetService is { } net && net.Type != NetGameType.Singleplayer;
}
