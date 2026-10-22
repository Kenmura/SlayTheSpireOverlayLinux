using SlayTheSpireTestSubjectVote.Core.Voting;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class VoteCopyTests
{
    [TestMethod]
    public void CostLine_ShowsRuntimeAmount()
    {
        StringAssert.Contains(VoteCopy.CostLine(2), "2 Strength");
        StringAssert.Contains(VoteCopy.CostLine(3), "3 Strength");
        Assert.DoesNotContain("3", VoteCopy.CostLine(2));
    }

    [TestMethod]
    public void Headline_NamesPlayerAndCard()
    {
        string line = VoteCopy.Headline("Alice", "Defend");
        StringAssert.Contains(line, "Alice");
        StringAssert.Contains(line, "Defend");
    }

    [TestMethod]
    public void WaitingLine_CyclesAndIsNeverEmpty()
    {
        var seen = new HashSet<string>();
        for (int tick = 0; tick <= 20; tick++)
        {
            string line = VoteCopy.WaitingLine(tick);
            Assert.IsFalse(string.IsNullOrWhiteSpace(line));
            seen.Add(line);
        }
        Assert.IsGreaterThanOrEqualTo(3, seen.Count);
        StringAssert.StartsWith(VoteCopy.WaitingLine(0), "Your teammates are deciding your fate");
    }

    [TestMethod]
    public void ApprovedQuip_CyclesAndIsNeverEmpty()
    {
        var seen = new HashSet<string>();
        for (int pick = -3; pick <= 20; pick++)
        {
            string line = VoteCopy.ApprovedQuip(pick);
            Assert.IsFalse(string.IsNullOrWhiteSpace(line));
            seen.Add(line);
        }
        Assert.IsGreaterThanOrEqualTo(3, seen.Count);
        StringAssert.StartsWith(VoteCopy.ApprovedQuip(0), "Bold choice");
    }

    [TestMethod]
    public void Progress_Format()
    {
        Assert.AreEqual("1 of 2 votes in", VoteCopy.Progress(1, 2));
    }

    [TestMethod]
    public void Labels_MatchSpecCopy()
    {
        Assert.AreEqual("Let it happen", VoteCopy.AllowLabel);
        Assert.AreEqual("Absolutely not", VoteCopy.RejectLabel);
        Assert.AreEqual("DENIED", VoteCopy.DeniedStamp);
    }

    [TestMethod]
    public void NegativeIndexes_WrapFromTheEnd()
    {
        Assert.AreEqual(VoteCopy.WaitingLine(2), VoteCopy.WaitingLine(-1));
        Assert.AreEqual(VoteCopy.ApprovedQuip(2), VoteCopy.ApprovedQuip(-1));
    }

    [TestMethod]
    public void SkipCopy_NamesTheHostAndTheKey()
    {
        Assert.AreEqual("Skip vote (host)", VoteCopy.SkipLabel);
        StringAssert.Contains(VoteCopy.SkipHint("F9"), "F9");
        StringAssert.Contains(VoteCopy.SkipHint("F9"), "host");
    }
}
