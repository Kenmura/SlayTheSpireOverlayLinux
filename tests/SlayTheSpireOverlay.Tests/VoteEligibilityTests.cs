using SlayTheSpireTestSubjectVote.Core.Voting;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class VoteEligibilityTests
{
    private static readonly PlayContext Eligible = new()
    {
        IsSkill = true,
        IsManualPlay = true,
        IsMultiplayer = true,
        IsFirstForm = true,
        AllPeersHaveMod = true,
        StrengthGain = 3,
    };

    [TestMethod]
    public void NeedsVote_TrueWhenEveryConditionHolds()
    {
        Assert.IsTrue(VoteEligibility.NeedsVote(Eligible));
    }

    [TestMethod]
    public void NeedsVote_FalseWhenNotASkill() => Assert.IsFalse(VoteEligibility.NeedsVote(Eligible with { IsSkill = false }));

    [TestMethod]
    public void NeedsVote_FalseWhenNotAManualPlay() => Assert.IsFalse(VoteEligibility.NeedsVote(Eligible with { IsManualPlay = false }));

    [TestMethod]
    public void NeedsVote_FalseInSinglePlayer() => Assert.IsFalse(VoteEligibility.NeedsVote(Eligible with { IsMultiplayer = false }));

    [TestMethod]
    public void NeedsVote_FalseOutsideFirstForm() => Assert.IsFalse(VoteEligibility.NeedsVote(Eligible with { IsFirstForm = false }));

    [TestMethod]
    public void NeedsVote_FalseWhenAPeerLacksTheMod() => Assert.IsFalse(VoteEligibility.NeedsVote(Eligible with { AllPeersHaveMod = false }));

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void NeedsVote_FalseWhenTheSkillCostsNothing(int strength) =>
        Assert.IsFalse(VoteEligibility.NeedsVote(Eligible with { StrengthGain = strength }));

    [TestMethod]
    public void VoterIds_ExcludesCardPlayerAndKeepsOrder()
    {
        var voters = VoteEligibility.VoterIds([5UL, 1UL, 9UL], cardPlayerId: 1);
        CollectionAssert.AreEqual(new ulong[] { 5, 9 }, voters.ToArray());
    }
}
