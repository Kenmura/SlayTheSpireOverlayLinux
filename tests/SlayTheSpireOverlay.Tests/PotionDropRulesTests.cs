using SlayTheSpireOverlay.Core.Options;
using SlayTheSpireOverlay.Core.Services;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class PotionDropRulesTests
{
    [TestMethod]
    public void ToPercent_ConvertsOddsToWholePercent()
    {
        Assert.AreEqual(40, PotionDropRules.ToPercent(0.4f));
        Assert.AreEqual(60, PotionDropRules.ToPercent(0.6f));
        Assert.AreEqual(50, PotionDropRules.ToPercent(0.5f));
    }

    [TestMethod]
    public void ToPercent_ClampsBetweenZeroAndHundred()
    {
        Assert.AreEqual(0, PotionDropRules.ToPercent(-0.1f));
        Assert.AreEqual(0, PotionDropRules.ToPercent(0f));
        Assert.AreEqual(100, PotionDropRules.ToPercent(1.2f));
        Assert.AreEqual(100, PotionDropRules.ToPercent(1f));
    }

    [TestMethod]
    public void ToPercent_AbsorbsFloatDrift()
    {
        // 0.4 + 0.1 + 0.1 + 0.1 in float is 0.70000005; must still read 70%.
        float drift = 0.4f + 0.1f + 0.1f + 0.1f;
        Assert.AreEqual(70, PotionDropRules.ToPercent(drift));
    }

    [TestMethod]
    public void ToPercent_UnknownOrInvalid_ReturnsNull()
    {
        Assert.IsNull(PotionDropRules.ToPercent(null));
        Assert.IsNull(PotionDropRules.ToPercent(float.NaN));
        Assert.IsNull(PotionDropRules.ToPercent(float.PositiveInfinity));
    }

    [TestMethod]
    public void FormatLabel_ShowsPercent_OrNullWhenUnavailable()
    {
        Assert.AreEqual("🧪 Potion Drop Chance: 60%", PotionDropRules.FormatLabel(0.6f));
        Assert.IsNull(PotionDropRules.FormatLabel(null));
    }

    [TestMethod]
    public void Config_PotionDropChanceDefaultsOn_AndCanBeDisabledViaJson()
    {
        Assert.IsTrue(new OverlayConfig().EnablePotionDropChance);
        var off = System.Text.Json.JsonSerializer.Deserialize<OverlayConfig>("{\"EnablePotionDropChance\":false}")!;
        Assert.IsFalse(off.EnablePotionDropChance);
    }
}
