namespace SlayTheSpireTestSubjectVote.Core.Voting;

/// <summary>
/// Identifies one held play: the player and the game's synchronized action id, which the host assigns and
/// every client shares, so keys match on all clients without any local counting.
/// </summary>
public readonly record struct PlayKey(ulong PlayerId, uint ActionId);

/// <summary>
/// Tracks every open vote on this client. Votes can arrive before the local client reaches the held play
/// (messages race the synchronized action queue), so those are buffered until <see cref="Open"/>.
/// Votes for closed plays are ignored; only the most recent closed keys are remembered.
/// </summary>
public sealed class VoteBook
{
    private const int ClosedMemory = 128;

    private readonly Dictionary<PlayKey, VoteTally> _open = [];
    private readonly Dictionary<PlayKey, List<(ulong Voter, bool Allow)>> _early = [];
    private readonly HashSet<PlayKey> _forcedEarly = [];
    private readonly HashSet<PlayKey> _closed = [];
    private readonly Queue<PlayKey> _closedOrder = new();

    public VoteTally Open(PlayKey key, IEnumerable<ulong> voterIds)
    {
        var tally = new VoteTally(voterIds);
        _open[key] = tally;
        if (_early.Remove(key, out var buffered))
        {
            foreach (var (voter, allow) in buffered) tally.Cast(voter, allow);
        }
        if (_forcedEarly.Remove(key)) tally.ForceAllow();
        return tally;
    }

    /// <summary>Buffers a vote for a play this client has not opened yet. Returns false if the play is already closed or the vote was not accepted.</summary>
    public bool RecordEarlyVote(PlayKey key, ulong voterId, bool allow)
    {
        if (_closed.Contains(key)) return false;
        if (_open.TryGetValue(key, out var tally)) return tally.Cast(voterId, allow);
        if (!_early.TryGetValue(key, out var list)) _early[key] = list = [];
        list.Add((voterId, allow));
        return true;
    }

    public bool Cast(PlayKey key, ulong voterId, bool allow) => RecordEarlyVote(key, voterId, allow);

    /// <summary>Applies the host's skip to an open vote, or remembers it for a vote this client has not opened yet.</summary>
    public bool ForceAllow(PlayKey key)
    {
        if (_closed.Contains(key)) return false;
        if (_open.TryGetValue(key, out var tally)) tally.ForceAllow();
        else _forcedEarly.Add(key);
        return true;
    }

    public void Close(PlayKey key)
    {
        _open.Remove(key);
        _early.Remove(key);
        _forcedEarly.Remove(key);
        if (_closed.Add(key)) _closedOrder.Enqueue(key);
        while (_closedOrder.Count > ClosedMemory) _closed.Remove(_closedOrder.Dequeue());
    }
}
