namespace SlayTheSpireTestSubjectVote.Core.Voting;

/// <summary>Identifies one held play. Attempt counts replays of the same card by the same player.</summary>
public readonly record struct PlayKey(ulong PlayerId, uint CombatCardIndex, int Attempt);

/// <summary>
/// Tracks every open vote on this client. Votes can arrive before the local client reaches the held play
/// (messages race the synchronized action queue), so those are buffered until <see cref="Open"/>.
/// Votes for closed plays are ignored.
/// </summary>
public sealed class VoteBook
{
    private readonly Dictionary<(ulong, uint), int> _attempts = [];
    private readonly Dictionary<PlayKey, VoteTally> _open = [];
    private readonly Dictionary<PlayKey, List<(ulong Voter, bool Allow)>> _early = [];
    private readonly HashSet<PlayKey> _closed = [];

    /// <summary>Allocates the key for the next attempt; identical on every client because plays are synchronized.</summary>
    public PlayKey NextKey(ulong playerId, uint combatCardIndex)
    {
        var slot = (playerId, combatCardIndex);
        int attempt = _attempts.GetValueOrDefault(slot);
        _attempts[slot] = attempt + 1;
        return new PlayKey(playerId, combatCardIndex, attempt);
    }

    public VoteTally Open(PlayKey key, IEnumerable<ulong> voterIds)
    {
        var tally = new VoteTally(voterIds);
        _open[key] = tally;
        if (_early.Remove(key, out var buffered))
        {
            foreach (var (voter, allow) in buffered) tally.Cast(voter, allow);
        }
        return tally;
    }

    /// <summary>Buffers a vote for a play this client has not opened yet. Returns false if the play is already closed.</summary>
    public bool RecordEarlyVote(PlayKey key, ulong voterId, bool allow)
    {
        if (_closed.Contains(key)) return false;
        if (_open.TryGetValue(key, out var tally)) return tally.Cast(voterId, allow);
        if (!_early.TryGetValue(key, out var list)) _early[key] = list = [];
        list.Add((voterId, allow));
        return true;
    }

    public bool Cast(PlayKey key, ulong voterId, bool allow) => RecordEarlyVote(key, voterId, allow);

    public void Close(PlayKey key)
    {
        _open.Remove(key);
        _early.Remove(key);
        _closed.Add(key);
    }
}
