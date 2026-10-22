using SlayTheSpireTestSubjectVote.Core.Voting;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class VoteEligibilityTests
{
    [TestMethod]
    public void NeedsVote_TrueWhenEveryConditionHolds()
    {
        Assert.IsTrue(VoteEligibility.NeedsVote(true, true, true, true, true));
    }

    [TestMethod]
    [DataRow(false, true, true, true, true)]
    [DataRow(true, false, true, true, true)]
    [DataRow(true, true, false, true, true)]
    [DataRow(true, true, true, false, true)]
    [DataRow(true, true, true, true, false)]
    public void NeedsVote_FalseWhenAnyConditionMissing(bool isSkill, bool manual, bool multiplayer, bool firstForm, bool allHaveMod)
    {
        Assert.IsFalse(VoteEligibility.NeedsVote(isSkill, manual, multiplayer, firstForm, allHaveMod));
    }

    [TestMethod]
    public void VoterIds_ExcludesCardPlayerAndKeepsOrder()
    {
        var voters = VoteEligibility.VoterIds([5UL, 1UL, 9UL], cardPlayerId: 1);
        CollectionAssert.AreEqual(new ulong[] { 5, 9 }, voters.ToArray());
    }
}
