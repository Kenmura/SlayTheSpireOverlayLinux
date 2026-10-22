namespace SlayTheSpireTestSubjectVote.Core.Voting;

/// <summary>Remembers which peers announced they run the mod. A vote is only held when every other peer did.</summary>
public sealed class PeerRoster
{
    private readonly HashSet<ulong> _withMod = [];
    private readonly HashSet<ulong> _left = [];

    public void MarkHasMod(ulong playerId) => _withMod.Add(playerId);

    /// <summary>A disconnected player must stop counting as a voter, or the (timeout-free) vote would wait on them forever.</summary>
    public void MarkLeft(ulong playerId) => _left.Add(playerId);

    public bool HasLeft(ulong playerId) => _left.Contains(playerId);

    public bool AllHaveMod(IEnumerable<ulong> playerIds) => playerIds.All(_withMod.Contains);
}
