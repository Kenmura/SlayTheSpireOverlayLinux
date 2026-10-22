using SlayTheSpireTestSubjectVote.Core.Voting;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class VoteBookTests
{
    private static readonly PlayKey Key = new(1, 40);

    [TestMethod]
    public void EarlyVotes_AreAllAppliedWhenVoteOpens()
    {
        var book = new VoteBook();
        Assert.IsTrue(book.RecordEarlyVote(Key, 2, allow: false));
        Assert.IsTrue(book.RecordEarlyVote(Key, 3, allow: false));

        var tally = book.Open(Key, [2UL, 3UL, 4UL]);

        Assert.AreEqual(2, tally.Reject);
        Assert.AreEqual(1, tally.Waiting);
    }

    [TestMethod]
    public void Cast_BeforeOpen_IsBufferedLikeAnEarlyVote()
    {
        var book = new VoteBook();
        Assert.IsTrue(book.Cast(Key, 2, allow: true));

        Assert.AreEqual(VoteOutcome.Allowed, book.Open(Key, [2UL]).Outcome);
    }

    [TestMethod]
    public void Close_DiscardsBufferedVotes()
    {
        var book = new VoteBook();
        book.RecordEarlyVote(Key, 2, allow: false);
        book.Close(Key);

        Assert.AreEqual(VoteOutcome.Pending, book.Open(Key, [2UL]).Outcome);
    }

    [TestMethod]
    public void VoteAfterClose_IsIgnored()
    {
        var book = new VoteBook();
        var tally = book.Open(Key, [2UL, 3UL]);
        book.Close(Key);

        Assert.IsFalse(book.Cast(Key, 2, allow: true));
        Assert.IsFalse(book.RecordEarlyVote(Key, 3, allow: true));
        Assert.AreEqual(0, tally.Allow);
    }

    [TestMethod]
    public void DuplicateAndNonVoterVotes_AreRejectedAfterOpen()
    {
        var book = new VoteBook();
        book.Open(Key, [2UL]);

        Assert.IsTrue(book.Cast(Key, 2, allow: true));
        Assert.IsFalse(book.Cast(Key, 2, allow: false));
        Assert.IsFalse(book.Cast(Key, 99, allow: false));
    }

    [TestMethod]
    public void SamePlayerDifferentActions_AreIndependent()
    {
        var book = new VoteBook();
        var first = new PlayKey(1, 40);
        var second = new PlayKey(1, 41);
        var tallyFirst = book.Open(first, [2UL]);
        var tallySecond = book.Open(second, [2UL]);

        book.Cast(second, 2, allow: false);

        Assert.AreEqual(VoteOutcome.Pending, tallyFirst.Outcome);
        Assert.AreEqual(VoteOutcome.Rejected, tallySecond.Outcome);
    }

    [TestMethod]
    public void TwoPlayersWithTheSameActionId_AreIndependent()
    {
        var book = new VoteBook();
        var a = new PlayKey(1, 40);
        var b = new PlayKey(2, 40);
        var tallyA = book.Open(a, [2UL]);
        var tallyB = book.Open(b, [1UL]);

        book.Cast(a, 2, allow: false);

        Assert.AreEqual(VoteOutcome.Rejected, tallyA.Outcome);
        Assert.AreEqual(VoteOutcome.Pending, tallyB.Outcome);
    }

    [TestMethod]
    public void ForceAllow_ResolvesAnOpenVote()
    {
        var book = new VoteBook();
        var tally = book.Open(Key, [2UL, 3UL]);

        Assert.IsTrue(book.ForceAllow(Key));

        Assert.AreEqual(VoteOutcome.Allowed, tally.Outcome);
    }

    [TestMethod]
    public void ForceAllow_BeforeOpen_AppliesWhenVoteOpens()
    {
        var book = new VoteBook();
        Assert.IsTrue(book.ForceAllow(Key));

        Assert.AreEqual(VoteOutcome.Allowed, book.Open(Key, [2UL, 3UL]).Outcome);
    }

    [TestMethod]
    public void ForceAllow_AfterClose_IsIgnored()
    {
        var book = new VoteBook();
        book.Open(Key, [2UL]);
        book.Close(Key);

        Assert.IsFalse(book.ForceAllow(Key));
    }

    [TestMethod]
    public void ClosedKeys_AreForgottenOnceTheBoundIsExceeded_SoMemoryStaysFlat()
    {
        var book = new VoteBook();
        for (uint i = 0; i < 300; i++)
        {
            book.Open(new PlayKey(1, i), [2UL]);
            book.Close(new PlayKey(1, i));
        }

        Assert.IsTrue(book.Cast(new PlayKey(1, 0), 2, allow: true), "oldest closed key is no longer remembered");
        Assert.IsFalse(book.Cast(new PlayKey(1, 299), 2, allow: true), "recent closed key is still remembered");
    }
}
