namespace SlayTheSpireTestSubjectVote.Core.Voting;

public static class VoteEligibility
{
    /// <summary>A play is held for a vote only for a manual Skill in a multiplayer Test Subject first form, when every peer runs the mod.</summary>
    public static bool NeedsVote(bool isSkill, bool isManualPlay, bool isMultiplayer, bool isFirstForm, bool allPeersHaveMod) =>
        isSkill && isManualPlay && isMultiplayer && isFirstForm && allPeersHaveMod;

    /// <summary>Everyone except the card player, in the given order.</summary>
    public static IReadOnlyList<ulong> VoterIds(IEnumerable<ulong> allPlayerIds, ulong cardPlayerId) =>
        allPlayerIds.Where(id => id != cardPlayerId).ToList();
}
