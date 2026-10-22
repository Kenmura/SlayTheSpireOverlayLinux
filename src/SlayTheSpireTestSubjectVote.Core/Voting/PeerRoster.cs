namespace SlayTheSpireTestSubjectVote.Core.Voting;

/// <summary>Remembers which peers announced they run the mod. A vote is only held when every other peer did.</summary>
public sealed class PeerRoster
{
    private readonly HashSet<ulong> _withMod = [];

    public void MarkHasMod(ulong playerId) => _withMod.Add(playerId);

    public bool AllHaveMod(IEnumerable<ulong> playerIds) => playerIds.All(_withMod.Contains);
}
