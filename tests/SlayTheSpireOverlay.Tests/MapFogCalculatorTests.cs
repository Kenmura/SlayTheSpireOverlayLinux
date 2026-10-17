using SlayTheSpireMapFog.Core.Options;
using SlayTheSpireMapFog.Core.Services;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class MapFogCalculatorTests
{
    private static readonly FogCoord StartNode = new(0, -1);
    private static readonly FogCoord Row0Left = new(0, 0);
    private static readonly FogCoord Row0Right = new(1, 0);
    private static readonly FogCoord Row1Left = new(0, 1);
    private static readonly FogCoord Row1Right = new(1, 1);
    private static readonly FogCoord Row2Mid = new(0, 2);
    private static readonly FogCoord BossNode = new(0, 3);

    private static readonly (FogCoord From, FogCoord To)[] SampleEdges =
    [
        (StartNode, Row0Left),
        (StartNode, Row0Right),
        (Row0Left, Row1Left),
        (Row0Right, Row1Right),
        (Row1Left, Row2Mid),
        (Row1Right, Row2Mid),
        (Row2Mid, BossNode)
    ];

    [TestMethod]
    public void DefaultConfig_FullFogShowBoss_RevealsOnlyVisitedOneStepAheadAndBoss()
    {
        var config = new MapFogConfig();
        Assert.IsTrue(config.Enabled);
        Assert.AreEqual(FogMode.FullFog, config.Mode);
        Assert.IsTrue(config.ShowBoss);
        Assert.AreEqual(1, config.VisionDepth);

        var vis = MapFogCalculator.CalculateVisibility(
            visitedCoords: [StartNode, Row0Left],
            travelableCoords: [Row1Left],
            bossCoords: [BossNode],
            directedEdges: SampleEdges,
            config: config);

        Assert.IsTrue(vis.IsNodeVisible(StartNode));
        Assert.IsTrue(vis.IsNodeVisible(Row0Left));
        Assert.IsTrue(vis.IsNodeVisible(Row1Left));
        Assert.IsTrue(vis.IsNodeVisible(BossNode));

        Assert.IsFalse(vis.IsNodeVisible(Row0Right));
        Assert.IsFalse(vis.IsNodeVisible(Row1Right));
        Assert.IsFalse(vis.IsNodeVisible(Row2Mid));

        Assert.IsTrue(vis.IsEdgeVisible(StartNode, Row0Left));
        Assert.IsTrue(vis.IsEdgeVisible(Row0Left, Row1Left));
        Assert.IsFalse(vis.IsEdgeVisible(Row1Left, Row2Mid));
        Assert.IsFalse(vis.IsEdgeVisible(Row2Mid, BossNode));
    }

    [TestMethod]
    public void HideBossConfig_HidesBossUntilTravelable()
    {
        var config = new MapFogConfig { ShowBoss = false };
        var vis = MapFogCalculator.CalculateVisibility(
            visitedCoords: [StartNode, Row0Left],
            travelableCoords: [Row1Left],
            bossCoords: [BossNode],
            directedEdges: SampleEdges,
            config: config);

        Assert.IsFalse(vis.IsNodeVisible(BossNode));

        var visAtBossApproach = MapFogCalculator.CalculateVisibility(
            visitedCoords: [StartNode, Row0Left, Row1Left, Row2Mid],
            travelableCoords: [BossNode],
            bossCoords: [BossNode],
            directedEdges: SampleEdges,
            config: config);

        Assert.IsTrue(visAtBossApproach.IsNodeVisible(BossNode));
        Assert.IsTrue(visAtBossApproach.IsEdgeVisible(Row2Mid, BossNode));
    }

    [TestMethod]
    public void HiddenIconsMode_KeepsGraphVisibleAndMasksFutureNodesAsUnknown()
    {
        var config = new MapFogConfig { Mode = FogMode.HiddenIcons, ShowBoss = true };
        var vis = MapFogCalculator.CalculateVisibility(
            visitedCoords: [StartNode, Row0Left],
            travelableCoords: [Row1Left],
            bossCoords: [BossNode],
            directedEdges: SampleEdges,
            config: config);

        Assert.IsTrue(vis.IsNodeVisible(Row2Mid));
        Assert.IsTrue(vis.IsEdgeVisible(Row1Left, Row2Mid));

        Assert.IsFalse(vis.ShouldMaskAsUnknown(StartNode));
        Assert.IsFalse(vis.ShouldMaskAsUnknown(Row0Left));
        Assert.IsFalse(vis.ShouldMaskAsUnknown(Row1Left));
        Assert.IsFalse(vis.ShouldMaskAsUnknown(BossNode));

        Assert.IsTrue(vis.ShouldMaskAsUnknown(Row2Mid));
        Assert.IsTrue(vis.ShouldMaskAsUnknown(Row1Right));
    }

    [TestMethod]
    public void VisionDepthTwo_RevealsTwoStepsAheadAlongDirectedEdges()
    {
        var config = new MapFogConfig { VisionDepth = 2 };
        var vis = MapFogCalculator.CalculateVisibility(
            visitedCoords: [StartNode, Row0Left],
            travelableCoords: [Row1Left],
            bossCoords: [BossNode],
            directedEdges: SampleEdges,
            config: config);

        Assert.IsTrue(vis.IsNodeVisible(Row1Left));
        Assert.IsTrue(vis.IsNodeVisible(Row2Mid));
        Assert.IsTrue(vis.IsEdgeVisible(Row1Left, Row2Mid));
        Assert.IsFalse(vis.IsNodeVisible(Row1Right));
    }
}
