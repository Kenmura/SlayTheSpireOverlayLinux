using SlayTheSpireTestSubjectVote.Core.Voting;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class PeerRosterTests
{
    [TestMethod]
    public void AllHaveMod_FalseWhenAnyPeerMissing()
    {
        var roster = new PeerRoster();
        roster.MarkHasMod(2);
        Assert.IsFalse(roster.AllHaveMod([2UL, 3UL]));
    }

    [TestMethod]
    public void AllHaveMod_TrueWhenEveryPeerAnnounced()
    {
        var roster = new PeerRoster();
        roster.MarkHasMod(2);
        roster.MarkHasMod(3);
        Assert.IsTrue(roster.AllHaveMod([2UL, 3UL]));
    }

    [TestMethod]
    public void AllHaveMod_TrueForEmptyList()
    {
        Assert.IsTrue(new PeerRoster().AllHaveMod([]));
    }
}
