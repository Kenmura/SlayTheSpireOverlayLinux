namespace SlayTheSpireTestSubjectVote.Core.Voting;

public enum VoteOutcome
{
    Pending,
    Allowed,
    Rejected,
}

/// <summary>
/// Counts one vote on one held Skill play. Resolves only once every remaining voter has answered;
/// there is deliberately no timeout. A tie allows the play (the card player is the tiebreaker).
/// </summary>
public sealed class VoteTally
{
    private readonly Dictionary<ulong, bool?> _votes;

    public VoteTally(IEnumerable<ulong> voterIds)
    {
        _votes = voterIds.Distinct().ToDictionary(id => id, _ => (bool?)null);
    }

    public int Allow => _votes.Values.Count(v => v == true);

    public int Reject => _votes.Values.Count(v => v == false);

    public int Total => _votes.Count;

    public int Waiting => _votes.Values.Count(v => v == null);

    public VoteOutcome Outcome =>
        Waiting > 0 ? VoteOutcome.Pending
        : Reject > Allow ? VoteOutcome.Rejected
        : VoteOutcome.Allowed;

    /// <summary>Returns false (and changes nothing) for an unknown voter or one who already voted.</summary>
    public bool Cast(ulong voterId, bool allow)
    {
        if (!_votes.TryGetValue(voterId, out bool? existing) || existing != null) return false;
        _votes[voterId] = allow;
        return true;
    }

    /// <summary>Drops a voter who left or died mid-vote so they no longer block the result.</summary>
    public void Remove(ulong voterId) => _votes.Remove(voterId);
}
