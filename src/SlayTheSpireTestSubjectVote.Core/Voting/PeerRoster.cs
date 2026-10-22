namespace SlayTheSpireTestSubjectVote.Core.Voting;

/// <summary>
/// Remembers which peers announced they run the mod, who the host is, and who has left.
/// A vote is only held when every player still in the run announced the mod.
/// </summary>
public sealed class PeerRoster
{
    private readonly HashSet<ulong> _withMod = [];
    private readonly HashSet<ulong> _left = [];
    private ulong? _hostId;

    public void MarkHasMod(ulong playerId) => _withMod.Add(playerId);

    /// <summary>A disconnected player must stop counting as a voter, or the (timeout-free) vote would wait on them forever.</summary>
    public void MarkLeft(ulong playerId) => _left.Add(playerId);

    /// <summary>A player who reconnects counts again.</summary>
    public void MarkRejoined(ulong playerId) => _left.Remove(playerId);

    public bool HasLeft(ulong playerId) => _left.Contains(playerId);

    /// <summary>Records who the host is (from the host's own hello); only the host may skip a stuck vote.</summary>
    public void MarkHost(ulong playerId) => _hostId = playerId;

    public bool IsHost(ulong playerId) => _hostId == playerId;

    public bool AllHaveMod(IEnumerable<ulong> playerIds) => playerIds.All(id => _left.Contains(id) || _withMod.Contains(id));
}
