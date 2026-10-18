using SlayTheSpireOverlay.Core.Options;
using SlayTheSpireOverlay.Core.Services;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class RelicProcRulesTests
{
    [TestMethod]
    public void PenNib_AtNine_IsPreProcAndFormatsAsAttackCount()
    {
        var info = RelicProcRules.TryGetProcInfo("PEN_NIB", 9);
        Assert.IsNotNull(info);
        Assert.AreEqual(10, info.MaxCount);
        Assert.IsTrue(info.IsPreProc);
        Assert.AreEqual("[9/10]", info.BadgeText);
    }

    [TestMethod]
    public void IncenseBurner_FormatsAsTurnCycle()
    {
        var info = RelicProcRules.TryGetProcInfo("INCENSE_BURNER", 5);
        Assert.IsNotNull(info);
        Assert.AreEqual("[T:5/6]", info.BadgeText);
        Assert.IsTrue(info.IsPreProc);
    }

    [TestMethod]
    public void MidCycle_IsNotPreProc()
    {
        Assert.IsFalse(RelicProcRules.TryGetProcInfo("HAPPY_FLOWER", 1)!.IsPreProc);
        Assert.IsFalse(RelicProcRules.TryGetProcInfo("KUNAI", 0)!.IsPreProc);
    }

    [TestMethod]
    [DataRow("PEN_NIB", 10)]
    [DataRow("INCENSE_BURNER", 6)]
    [DataRow("HAPPY_FLOWER", 3)]
    [DataRow("KUNAI", 3)]
    [DataRow("SHURIKEN", 3)]
    [DataRow("STONE_CALENDAR", 7)]
    public void AllSupportedRelics_HaveExpectedMax(string id, int max) =>
        Assert.AreEqual(max, RelicProcRules.TryGetProcInfo(id, 0)!.MaxCount);

    [TestMethod]
    public void IdNormalization_AcceptsCategoryAndCamelCase()
    {
        Assert.IsNotNull(RelicProcRules.TryGetProcInfo("RELIC:PEN_NIB", 1));
        Assert.IsNotNull(RelicProcRules.TryGetProcInfo("PenNib", 1));
        Assert.IsTrue(RelicProcRules.IsSupported("RELIC:SHURIKEN"));
    }

    [TestMethod]
    public void MaxOverride_FromGameThreshold_Wins()
    {
        var info = RelicProcRules.TryGetProcInfo("PEN_NIB", 7, maxOverride: 8);
        Assert.AreEqual(8, info!.MaxCount);
        Assert.IsTrue(info.IsPreProc);
        Assert.AreEqual(10, RelicProcRules.TryGetProcInfo("PEN_NIB", 7, maxOverride: 0)!.MaxCount);
    }

    [TestMethod]
    public void UnsupportedOrMissingData_ReturnsNull()
    {
        Assert.IsNull(RelicProcRules.TryGetProcInfo("BURNING_BLOOD", 3));
        Assert.IsNull(RelicProcRules.TryGetProcInfo(null, 3));
        Assert.IsNull(RelicProcRules.TryGetProcInfo("", 3));
        Assert.IsNull(RelicProcRules.TryGetProcInfo("PEN_NIB", null));
        Assert.IsNull(RelicProcRules.TryGetProcInfo("PEN_NIB", -1));
        Assert.IsFalse(RelicProcRules.IsSupported("BURNING_BLOOD"));
    }

    [TestMethod]
    public void HiddenCounter_ReturnsNull_AndGameMinusOneMeansNoCounter()
    {
        Assert.IsNull(RelicProcRules.TryGetProcInfo("KUNAI", 0, showCounter: false));
        Assert.IsNull(RelicProcRules.TryGetProcInfo("STONE_CALENDAR", -1));
    }

    [TestMethod]
    public void CountAtOrBeyondMax_IsNotPreProc()
    {
        Assert.IsFalse(RelicProcRules.TryGetProcInfo("KUNAI", 3)!.IsPreProc);
    }

    [TestMethod]
    public void PreProcColor_IsGoldHex() => StringAssert.Matches(RelicProcRules.PreProcColorHex, new System.Text.RegularExpressions.Regex("^#ffdd44$"));

    [TestMethod]
    public void Config_ProcCountersDefaultOn_AndSurviveJsonRoundTrip()
    {
        Assert.IsTrue(new OverlayConfig().EnableRelicProcCounters);
        var off = System.Text.Json.JsonSerializer.Deserialize<OverlayConfig>("{\"EnableRelicProcCounters\":false}")!;
        Assert.IsFalse(off.EnableRelicProcCounters);
    }
}
