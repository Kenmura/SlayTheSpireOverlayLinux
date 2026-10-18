using Microsoft.VisualStudio.TestTools.UnitTesting;
using SlayTheSpireOverlay.Core.Models;
using SlayTheSpireOverlay.Core.Options;
using SlayTheSpireOverlay.Core.Services;
using System.Collections.Generic;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class MapPathTests
{
    [TestMethod]
    public void OverlayConfig_HasEnableMapPathSummary_DefaultingToTrue()
    {
        var config = new OverlayConfig();
        Assert.IsTrue(config.EnableMapPathSummary);
    }

    [TestMethod]
    public void MapPathAnalyzer_CalculatesMinMaxEncounters_OnSimpleBranchingDag()
    {
        // Path 1: (0,0)[Monster] -> (0,1)[Elite] -> (0,2)[RestSite] -> (0,3)[Boss]
        // Path 2: (0,0)[Monster] -> (1,1)[Shop]  -> (1,2)[Elite]    -> (0,3)[Boss]
        var start = new MapPathCoord(0, 0);
        var p1_1 = new MapPathCoord(0, 1);
        var p1_2 = new MapPathCoord(0, 2);
        var p2_1 = new MapPathCoord(1, 1);
        var p2_2 = new MapPathCoord(1, 2);
        var boss = new MapPathCoord(0, 3);

        var graph = new MapGraph();
        graph.AddNode(new MapPathNode(start, MapNodeType.Monster));
        graph.AddNode(new MapPathNode(p1_1, MapNodeType.Elite));
        graph.AddNode(new MapPathNode(p1_2, MapNodeType.RestSite));
        graph.AddNode(new MapPathNode(p2_1, MapNodeType.Shop));
        graph.AddNode(new MapPathNode(p2_2, MapNodeType.Elite));
        graph.AddNode(new MapPathNode(boss, MapNodeType.Boss));

        graph.AddEdge(start, p1_1);
        graph.AddEdge(p1_1, p1_2);
        graph.AddEdge(p1_2, boss);

        graph.AddEdge(start, p2_1);
        graph.AddEdge(p2_1, p2_2);
        graph.AddEdge(p2_2, boss);

        var summary = MapPathAnalyzer.CalculateReachableSummary(graph, start);

        Assert.AreEqual(2, summary.DistinctPathsCount);
        Assert.AreEqual(6, summary.TotalReachableNodes);
        Assert.AreEqual(new RangeStat(1, 1), summary.Elites);
        Assert.AreEqual(new RangeStat(0, 1), summary.RestSites);
        Assert.AreEqual(new RangeStat(0, 1), summary.Shops);
        Assert.AreEqual(new RangeStat(1, 1), summary.Monsters);
        Assert.AreEqual(new RangeStat(1, 1), summary.Bosses);
        Assert.AreEqual(new RangeStat(0, 0), summary.FoggedNodes);
    }

    [TestMethod]
    public void MapPathAnalyzer_CalculatesVaryingCounts_AcrossMultipleDivergentBranches()
    {
        // Branch A: 3 Elites, 1 Rest Site
        // Branch B: 0 Elites, 3 Rest Sites
        var root = new MapPathCoord(0, 0);
        var a1 = new MapPathCoord(0, 1);
        var a2 = new MapPathCoord(0, 2);
        var a3 = new MapPathCoord(0, 3);
        var aRest = new MapPathCoord(0, 4);

        var b1 = new MapPathCoord(1, 1);
        var b2 = new MapPathCoord(1, 2);
        var b3 = new MapPathCoord(1, 3);

        var boss = new MapPathCoord(0, 5);

        var graph = new MapGraph();
        graph.AddNode(new MapPathNode(root, MapNodeType.Monster));
        graph.AddNode(new MapPathNode(a1, MapNodeType.Elite));
        graph.AddNode(new MapPathNode(a2, MapNodeType.Elite));
        graph.AddNode(new MapPathNode(a3, MapNodeType.Elite));
        graph.AddNode(new MapPathNode(aRest, MapNodeType.RestSite));

        graph.AddNode(new MapPathNode(b1, MapNodeType.RestSite));
        graph.AddNode(new MapPathNode(b2, MapNodeType.RestSite));
        graph.AddNode(new MapPathNode(b3, MapNodeType.RestSite));

        graph.AddNode(new MapPathNode(boss, MapNodeType.Boss));

        graph.AddEdge(root, a1);
        graph.AddEdge(a1, a2);
        graph.AddEdge(a2, a3);
        graph.AddEdge(a3, aRest);
        graph.AddEdge(aRest, boss);

        graph.AddEdge(root, b1);
        graph.AddEdge(b1, b2);
        graph.AddEdge(b2, b3);
        graph.AddEdge(b3, boss);

        var summary = MapPathAnalyzer.CalculateReachableSummary(graph, root);

        Assert.AreEqual(new RangeStat(0, 3), summary.Elites);
        Assert.AreEqual(new RangeStat(1, 3), summary.RestSites);
        Assert.AreEqual(2, summary.DistinctPathsCount);
    }

    [TestMethod]
    public void MapPathAnalyzer_FogHorizonRespect_MasksUnrevealedNodesAsFoggedWithoutLeaking()
    {
        // Node layout:
        // (0,0) Monster (Revealed)
        // (0,1) Elite   (Revealed)
        // (0,2) Elite   (Fogged - unrevealed)
        // (0,3) RestSite(Fogged - unrevealed)
        // (0,4) Boss    (Boss shown via ShowBoss = true)
        var n0 = new MapPathCoord(0, 0);
        var n1 = new MapPathCoord(0, 1);
        var n2 = new MapPathCoord(0, 2);
        var n3 = new MapPathCoord(0, 3);
        var boss = new MapPathCoord(0, 4);

        var graph = new MapGraph();
        graph.AddNode(new MapPathNode(n0, MapNodeType.Monster));
        graph.AddNode(new MapPathNode(n1, MapNodeType.Elite));
        graph.AddNode(new MapPathNode(n2, MapNodeType.Elite));
        graph.AddNode(new MapPathNode(n3, MapNodeType.RestSite));
        graph.AddNode(new MapPathNode(boss, MapNodeType.Boss));

        graph.AddEdge(n0, n1);
        graph.AddEdge(n1, n2);
        graph.AddEdge(n2, n3);
        graph.AddEdge(n3, boss);

        // Revealed coordinates: only n0, n1, and boss
        var revealed = new HashSet<MapPathCoord> { n0, n1, boss };

        var summary = MapPathAnalyzer.CalculateReachableSummary(graph, n0, revealedCoords: revealed, bossRevealed: true);

        // n2 was Elite, n3 was RestSite. Because they are not revealed, they MUST be counted as Fogged!
        // Leaked elites should NOT appear in the summary.
        Assert.AreEqual(new RangeStat(1, 1), summary.Elites, "Unrevealed elite at n2 must not be leaked!");
        Assert.AreEqual(new RangeStat(0, 0), summary.RestSites, "Unrevealed rest site at n3 must not be leaked!");
        Assert.AreEqual(new RangeStat(2, 2), summary.FoggedNodes, "Two unrevealed nodes must be counted as Fogged!");
        Assert.AreEqual(new RangeStat(1, 1), summary.Bosses);
    }

    [TestMethod]
    public void MapPathAnalyzer_FogHorizonRespect_MasksBossWhenBossNotRevealed()
    {
        var n0 = new MapPathCoord(0, 0);
        var boss = new MapPathCoord(0, 1);

        var graph = new MapGraph();
        graph.AddNode(new MapPathNode(n0, MapNodeType.Monster));
        graph.AddNode(new MapPathNode(boss, MapNodeType.Boss));
        graph.AddEdge(n0, boss);

        var revealed = new HashSet<MapPathCoord> { n0 };
        var summary = MapPathAnalyzer.CalculateReachableSummary(graph, n0, revealedCoords: revealed, bossRevealed: false);

        Assert.AreEqual(new RangeStat(0, 0), summary.Bosses, "Boss must not be counted if unrevealed!");
        Assert.AreEqual(new RangeStat(1, 1), summary.FoggedNodes);
    }

    [TestMethod]
    public void MapPathAnalyzer_CombinedFrontierSummary_AggregatesAcrossAllTravelableNodes()
    {
        // Two travelable start nodes on floor 1
        var t1 = new MapPathCoord(0, 1); // 2 Elites path
        var t2 = new MapPathCoord(2, 1); // 1 Elite, 2 Shops path
        var e1 = new MapPathCoord(0, 2);
        var e2 = new MapPathCoord(0, 3);
        var s1 = new MapPathCoord(2, 2);
        var s2 = new MapPathCoord(2, 3);
        var boss = new MapPathCoord(1, 4);

        var graph = new MapGraph();
        graph.AddNode(new MapPathNode(t1, MapNodeType.Monster));
        graph.AddNode(new MapPathNode(e1, MapNodeType.Elite));
        graph.AddNode(new MapPathNode(e2, MapNodeType.Elite));

        graph.AddNode(new MapPathNode(t2, MapNodeType.Monster));
        graph.AddNode(new MapPathNode(s1, MapNodeType.Shop));
        graph.AddNode(new MapPathNode(s2, MapNodeType.Shop));

        graph.AddNode(new MapPathNode(boss, MapNodeType.Boss));

        graph.AddEdge(t1, e1);
        graph.AddEdge(e1, e2);
        graph.AddEdge(e2, boss);

        graph.AddEdge(t2, s1);
        graph.AddEdge(s1, s2);
        graph.AddEdge(s2, boss);

        var combined = MapPathAnalyzer.CalculateCombinedSummary(graph, new[] { t1, t2 });

        Assert.AreEqual(new RangeStat(0, 2), combined.Elites);
        Assert.AreEqual(new RangeStat(0, 2), combined.Shops);
        Assert.AreEqual(2, combined.DistinctPathsCount);
    }

    [TestMethod]
    public void MapPathSummary_FormatDisplayString_ProducesClearHudText()
    {
        var summary = new MapPathSummary
        {
            Elites = new RangeStat(1, 2),
            RestSites = new RangeStat(2, 3),
            Shops = new RangeStat(0, 1),
            Unknowns = new RangeStat(3, 4),
            FoggedNodes = new RangeStat(0, 0),
            DistinctPathsCount = 4
        };

        string text = summary.ToDisplayString();
        StringAssert.Contains(text, "Elites: 1-2");
        StringAssert.Contains(text, "Rest: 2-3");
        StringAssert.Contains(text, "Shops: 0-1");

        var foggedSummary = new MapPathSummary
        {
            Elites = new RangeStat(1, 1),
            RestSites = new RangeStat(1, 2),
            Shops = new RangeStat(0, 0),
            Unknowns = new RangeStat(0, 1),
            FoggedNodes = new RangeStat(2, 4),
            DistinctPathsCount = 2
        };

        string foggedText = foggedSummary.ToDisplayString();
        StringAssert.Contains(foggedText, "Elites: 1");
        StringAssert.Contains(foggedText, "Fogged: 2-4");
    }

    [TestMethod]
    public void MapPathAnalyzer_ConvertRawPointType_MapsAllGameValues()
    {
        Assert.AreEqual(MapNodeType.Unknown, MapPathAnalyzer.ConvertRawPointType(0));
        Assert.AreEqual(MapNodeType.Unknown, MapPathAnalyzer.ConvertRawPointType(1));
        Assert.AreEqual(MapNodeType.Shop, MapPathAnalyzer.ConvertRawPointType(2));
        Assert.AreEqual(MapNodeType.Treasure, MapPathAnalyzer.ConvertRawPointType(3));
        Assert.AreEqual(MapNodeType.RestSite, MapPathAnalyzer.ConvertRawPointType(4));
        Assert.AreEqual(MapNodeType.Monster, MapPathAnalyzer.ConvertRawPointType(5));
        Assert.AreEqual(MapNodeType.Elite, MapPathAnalyzer.ConvertRawPointType(6));
        Assert.AreEqual(MapNodeType.Boss, MapPathAnalyzer.ConvertRawPointType(7));
        Assert.AreEqual(MapNodeType.Ancient, MapPathAnalyzer.ConvertRawPointType(8));
        Assert.AreEqual(MapNodeType.Unknown, MapPathAnalyzer.ConvertRawPointType(999));
    }
}
