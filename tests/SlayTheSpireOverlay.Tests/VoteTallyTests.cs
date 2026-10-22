using SlayTheSpireTestSubjectVote.Core.Voting;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class VoteTallyTests
{
    [TestMethod]
    public void Tie_Allows()
    {
        var tally = new VoteTally([2UL, 3UL]);
        tally.Cast(2, allow: true);
        tally.Cast(3, allow: false);
        Assert.AreEqual(VoteOutcome.Allowed, tally.Outcome);
    }

    [TestMethod]
    public void SingleVoter_Reject_Rejects()
    {
        var tally = new VoteTally([2UL]);
        tally.Cast(2, allow: false);
        Assert.AreEqual(VoteOutcome.Rejected, tally.Outcome);
    }

    [TestMethod]
    public void SingleVoter_Allow_Allows()
    {
        var tally = new VoteTally([2UL]);
        tally.Cast(2, allow: true);
        Assert.AreEqual(VoteOutcome.Allowed, tally.Outcome);
    }

    [TestMethod]
    public void ThreePlayers_BlockNeedsBothRejects()
    {
        var split = new VoteTally([2UL, 3UL]);
        split.Cast(2, allow: false);
        split.Cast(3, allow: true);
        Assert.AreEqual(VoteOutcome.Allowed, split.Outcome);

        var both = new VoteTally([2UL, 3UL]);
        both.Cast(2, allow: false);
        both.Cast(3, allow: false);
        Assert.AreEqual(VoteOutcome.Rejected, both.Outcome);
    }

    [TestMethod]
    public void Pending_UntilEveryVoterHasVoted()
    {
        var tally = new VoteTally([2UL, 3UL, 4UL]);
        tally.Cast(2, allow: true);
        tally.Cast(3, allow: true);
        Assert.AreEqual(VoteOutcome.Pending, tally.Outcome);
        Assert.AreEqual(1, tally.Waiting);
    }

    [TestMethod]
    public void RejectLead_StaysPending_UntilLastVoterCasts()
    {
        var tally = new VoteTally([2UL, 3UL, 4UL]);
        tally.Cast(2, allow: false);
        tally.Cast(3, allow: false);
        Assert.AreEqual(VoteOutcome.Pending, tally.Outcome);
        tally.Cast(4, allow: true);
        Assert.AreEqual(VoteOutcome.Rejected, tally.Outcome);
    }

    [TestMethod]
    public void NonVoterAndDuplicateVotes_AreIgnored()
    {
        var tally = new VoteTally([2UL, 3UL]);
        Assert.IsTrue(tally.Cast(2, allow: true));
        Assert.IsFalse(tally.Cast(2, allow: false));
        Assert.IsFalse(tally.Cast(99, allow: false));
        Assert.AreEqual(1, tally.Allow);
        Assert.AreEqual(0, tally.Reject);
        Assert.AreEqual(2, tally.Total);
    }

    [TestMethod]
    public void RemovedVoter_StopsCounting()
    {
        var tally = new VoteTally([2UL, 3UL]);
        tally.Cast(2, allow: false);
        tally.Remove(3);
        Assert.AreEqual(1, tally.Total);
        Assert.AreEqual(VoteOutcome.Rejected, tally.Outcome);
    }

    [TestMethod]
    public void RemovingLastRemainingVoter_Allows()
    {
        var tally = new VoteTally([2UL]);
        tally.Remove(2);
        Assert.AreEqual(VoteOutcome.Allowed, tally.Outcome);
    }

    [TestMethod]
    public void DuplicateVoterIds_AreCountedOnce()
    {
        var tally = new VoteTally([2UL, 2UL, 3UL]);
        Assert.AreEqual(2, tally.Total);
    }

    [TestMethod]
    public void ZeroVoters_Allows()
    {
        Assert.AreEqual(VoteOutcome.Allowed, new VoteTally([]).Outcome);
    }

    [TestMethod]
    public void Remove_KeepsAVoteThatWasAlreadyCast()
    {
        var tally = new VoteTally([2UL, 3UL, 4UL]);
        tally.Cast(2, allow: false);
        tally.Cast(3, allow: false);
        tally.Cast(4, allow: true);
        tally.Remove(2);
        Assert.AreEqual(3, tally.Total);
        Assert.AreEqual(2, tally.Reject);
        Assert.AreEqual(VoteOutcome.Rejected, tally.Outcome);
    }

    [TestMethod]
    public void Cast_AfterRemove_IsIgnored()
    {
        var tally = new VoteTally([2UL, 3UL]);
        tally.Remove(3);
        Assert.IsFalse(tally.Cast(3, allow: false));
        Assert.AreEqual(1, tally.Total);
    }

    [TestMethod]
    public void RemovingAPendingVoter_CanLeaveATieThatAllows()
    {
        var tally = new VoteTally([2UL, 3UL, 4UL]);
        tally.Cast(2, allow: true);
        tally.Cast(3, allow: false);
        tally.Remove(4);
        Assert.AreEqual(VoteOutcome.Allowed, tally.Outcome);
    }

    [TestMethod]
    public void ForceAllow_ResolvesAPendingVote()
    {
        var tally = new VoteTally([2UL, 3UL]);
        tally.Cast(2, allow: false);
        tally.ForceAllow();
        Assert.AreEqual(VoteOutcome.Allowed, tally.Outcome);
    }

    [TestMethod]
    public void ForceAllow_CannotOverturnAnOutcomeThatAlreadyResolved()
    {
        var tally = new VoteTally([2UL]);
        tally.Cast(2, allow: false);
        tally.ForceAllow();
        Assert.AreEqual(VoteOutcome.Rejected, tally.Outcome);
    }
}
