namespace SlayTheSpireTestSubjectVote.Core.Voting;

/// <summary>Everything that decides whether a play is put to a vote. Named members, so arguments cannot be mixed up.</summary>
public readonly record struct PlayContext
{
    public bool IsSkill { get; init; }

    public bool IsManualPlay { get; init; }

    public bool IsMultiplayer { get; init; }

    public bool IsFirstForm { get; init; }

    public bool AllPeersHaveMod { get; init; }

    /// <summary>The Strength the Test Subject would gain; a vote is pointless when the Skill costs nothing.</summary>
    public int StrengthGain { get; init; }
}

public static class VoteEligibility
{
    /// <summary>A play is held for a vote only for a manual Skill in a multiplayer Test Subject first form, when every peer runs the mod and the Skill would actually cost Strength.</summary>
    public static bool NeedsVote(PlayContext context) =>
        context.IsSkill && context.IsManualPlay && context.IsMultiplayer && context.IsFirstForm
        && context.AllPeersHaveMod && context.StrengthGain > 0;

    /// <summary>Everyone except the card player, in the given order.</summary>
    public static IReadOnlyList<ulong> VoterIds(IEnumerable<ulong> allPlayerIds, ulong cardPlayerId) =>
        allPlayerIds.Where(id => id != cardPlayerId).ToList();
}
