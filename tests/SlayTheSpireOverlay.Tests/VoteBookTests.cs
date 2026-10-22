using SlayTheSpireTestSubjectVote.Core.Voting;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class VoteBookTests
{
    [TestMethod]
    public void EarlyVote_IsAppliedWhenVoteOpens()
    {
        var book = new VoteBook();
        var key = new PlayKey(1, 7, 0);
        Assert.IsTrue(book.RecordEarlyVote(key, 2, allow: false));

        var tally = book.Open(key, [2UL]);

        Assert.AreEqual(VoteOutcome.Rejected, tally.Outcome);
    }

    [TestMethod]
    public void ReplayOfSameCard_GetsNewKey_AndOldVotesDoNotLeak()
    {
        var book = new VoteBook();
        var first = book.NextKey(1, 7);
        var second = book.NextKey(1, 7);
        Assert.AreEqual(0, first.Attempt);
        Assert.AreEqual(1, second.Attempt);

        var oldTally = book.Open(first, [2UL]);
        book.Cast(first, 2, allow: false);
        book.Close(first);
        var newTally = book.Open(second, [2UL]);

        Assert.AreEqual(VoteOutcome.Rejected, oldTally.Outcome);
        Assert.AreEqual(VoteOutcome.Pending, newTally.Outcome);
    }

    [TestMethod]
    public void VoteAfterClose_IsIgnored()
    {
        var book = new VoteBook();
        var key = new PlayKey(1, 7, 0);
        var tally = book.Open(key, [2UL, 3UL]);
        book.Close(key);

        Assert.IsFalse(book.Cast(key, 2, allow: true));
        Assert.IsFalse(book.RecordEarlyVote(key, 3, allow: true));
        Assert.AreEqual(0, tally.Allow);
    }

    [TestMethod]
    public void TwoPlayersAtOnce_AreIndependent()
    {
        var book = new VoteBook();
        var a = book.NextKey(1, 7);
        var b = book.NextKey(2, 7);
        var tallyA = book.Open(a, [2UL]);
        var tallyB = book.Open(b, [1UL]);

        book.Cast(a, 2, allow: false);

        Assert.AreEqual(VoteOutcome.Rejected, tallyA.Outcome);
        Assert.AreEqual(VoteOutcome.Pending, tallyB.Outcome);
    }

    [TestMethod]
    public void Cast_BeforeOpen_IsBufferedLikeAnEarlyVote()
    {
        var book = new VoteBook();
        var key = new PlayKey(1, 7, 0);
        Assert.IsTrue(book.Cast(key, 2, allow: true));

        Assert.AreEqual(VoteOutcome.Allowed, book.Open(key, [2UL]).Outcome);
    }

    [TestMethod]
    public void TwoDifferentCardsFromOnePlayer_AreIndependent()
    {
        var book = new VoteBook();
        var first = book.NextKey(1, 7);
        var second = book.NextKey(1, 8);
        var tallyFirst = book.Open(first, [2UL]);
        var tallySecond = book.Open(second, [2UL]);

        book.Cast(second, 2, allow: false);

        Assert.AreEqual(VoteOutcome.Pending, tallyFirst.Outcome);
        Assert.AreEqual(VoteOutcome.Rejected, tallySecond.Outcome);
    }
}
