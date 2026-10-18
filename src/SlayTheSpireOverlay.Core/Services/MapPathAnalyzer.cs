using System;
using System.Collections.Generic;
using System.Linq;
using SlayTheSpireOverlay.Core.Models;

namespace SlayTheSpireOverlay.Core.Services;

public static class MapPathAnalyzer
{
    public static MapNodeType ConvertRawPointType(int raw) => raw switch
    {
        1 => MapNodeType.Unknown,
        2 => MapNodeType.Shop,
        3 => MapNodeType.Treasure,
        4 => MapNodeType.RestSite,
        5 => MapNodeType.Monster,
        6 => MapNodeType.Elite,
        7 => MapNodeType.Boss,
        8 => MapNodeType.Ancient,
        _ => MapNodeType.Unknown
    };

    private sealed class PathCounts
    {
        public int Elites;
        public int RestSites;
        public int Shops;
        public int Unknowns;
        public int Monsters;
        public int Treasures;
        public int Bosses;
        public int Ancients;
        public int Fogged;

        public void AddType(MapNodeType type)
        {
            switch (type)
            {
                case MapNodeType.Elite: Elites++; break;
                case MapNodeType.RestSite: RestSites++; break;
                case MapNodeType.Shop: Shops++; break;
                case MapNodeType.Unknown: Unknowns++; break;
                case MapNodeType.Monster: Monsters++; break;
                case MapNodeType.Treasure: Treasures++; break;
                case MapNodeType.Boss: Bosses++; break;
                case MapNodeType.Ancient: Ancients++; break;
                case MapNodeType.Fogged: Fogged++; break;
            }
        }
    }

    public static MapPathSummary CalculateReachableSummary(
        MapGraph graph,
        MapPathCoord startCoord,
        IReadOnlySet<MapPathCoord>? revealedCoords = null,
        bool bossRevealed = true,
        int maxDepth = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return CalculateCombinedSummary(graph, new[] { startCoord }, revealedCoords, bossRevealed, maxDepth);
    }

    public static MapPathSummary CalculateCombinedSummary(
        MapGraph graph,
        IEnumerable<MapPathCoord> startCoords,
        IReadOnlySet<MapPathCoord>? revealedCoords = null,
        bool bossRevealed = true,
        int maxDepth = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(startCoords);

        var allPaths = new List<PathCounts>();
        var totalReachable = new HashSet<MapPathCoord>();

        foreach (var start in startCoords)
        {
            if (!graph.ContainsCoord(start)) continue;

            var visitedInCurrentPath = new HashSet<MapPathCoord>();
            TraverseDfs(
                graph,
                start,
                depth: 1,
                maxDepth,
                revealedCoords,
                bossRevealed,
                visitedInCurrentPath,
                currentCounts: new PathCounts(),
                allPaths,
                totalReachable);
        }

        if (allPaths.Count == 0)
        {
            return new MapPathSummary();
        }

        return new MapPathSummary
        {
            Elites = new RangeStat(allPaths.Min(p => p.Elites), allPaths.Max(p => p.Elites)),
            RestSites = new RangeStat(allPaths.Min(p => p.RestSites), allPaths.Max(p => p.RestSites)),
            Shops = new RangeStat(allPaths.Min(p => p.Shops), allPaths.Max(p => p.Shops)),
            Unknowns = new RangeStat(allPaths.Min(p => p.Unknowns), allPaths.Max(p => p.Unknowns)),
            Monsters = new RangeStat(allPaths.Min(p => p.Monsters), allPaths.Max(p => p.Monsters)),
            Treasures = new RangeStat(allPaths.Min(p => p.Treasures), allPaths.Max(p => p.Treasures)),
            Bosses = new RangeStat(allPaths.Min(p => p.Bosses), allPaths.Max(p => p.Bosses)),
            Ancients = new RangeStat(allPaths.Min(p => p.Ancients), allPaths.Max(p => p.Ancients)),
            FoggedNodes = new RangeStat(allPaths.Min(p => p.Fogged), allPaths.Max(p => p.Fogged)),
            DistinctPathsCount = allPaths.Count,
            TotalReachableNodes = totalReachable.Count
        };
    }

    public static IReadOnlyDictionary<MapPathCoord, MapPathSummary> CalculateBranchSummaries(
        MapGraph graph,
        IEnumerable<MapPathCoord> startCoords,
        IReadOnlySet<MapPathCoord>? revealedCoords = null,
        bool bossRevealed = true,
        int maxDepth = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(startCoords);

        var result = new Dictionary<MapPathCoord, MapPathSummary>();
        foreach (var start in startCoords)
        {
            result[start] = CalculateReachableSummary(graph, start, revealedCoords, bossRevealed, maxDepth);
        }
        return result;
    }

    private static void TraverseDfs(
        MapGraph graph,
        MapPathCoord currentCoord,
        int depth,
        int maxDepth,
        IReadOnlySet<MapPathCoord>? revealedCoords,
        bool bossRevealed,
        HashSet<MapPathCoord> visitedInPath,
        PathCounts currentCounts,
        List<PathCounts> allPaths,
        HashSet<MapPathCoord> totalReachable)
    {
        var node = graph.GetNode(currentCoord);
        if (node == null) return;

        if (!visitedInPath.Add(currentCoord))
        {
            // Cycle detected: record current path and terminate branch
            allPaths.Add(CloneCounts(currentCounts));
            return;
        }

        totalReachable.Add(currentCoord);

        var effectiveType = ResolveEffectiveType(node, revealedCoords, bossRevealed);
        currentCounts.AddType(effectiveType);

        var validChildren = node.Children
            .Where(childCoord => !visitedInPath.Contains(childCoord) && graph.ContainsCoord(childCoord))
            .ToList();

        if (validChildren.Count == 0 || depth >= maxDepth || node.Type == MapNodeType.Boss)
        {
            allPaths.Add(CloneCounts(currentCounts));
        }
        else
        {
            foreach (var childCoord in validChildren)
            {
                TraverseDfs(
                    graph,
                    childCoord,
                    depth + 1,
                    maxDepth,
                    revealedCoords,
                    bossRevealed,
                    visitedInPath,
                    CloneCounts(currentCounts),
                    allPaths,
                    totalReachable);
            }
        }

        visitedInPath.Remove(currentCoord);
    }

    private static MapNodeType ResolveEffectiveType(
        MapPathNode node,
        IReadOnlySet<MapPathCoord>? revealedCoords,
        bool bossRevealed)
    {
        if (revealedCoords == null)
        {
            return node.Type;
        }

        if (revealedCoords.Contains(node.Coord))
        {
            return node.Type;
        }

        if (node.Type == MapNodeType.Boss)
        {
            return bossRevealed ? MapNodeType.Boss : MapNodeType.Fogged;
        }

        return MapNodeType.Fogged;
    }

    private static PathCounts CloneCounts(PathCounts source) => new()
    {
        Elites = source.Elites,
        RestSites = source.RestSites,
        Shops = source.Shops,
        Unknowns = source.Unknowns,
        Monsters = source.Monsters,
        Treasures = source.Treasures,
        Bosses = source.Bosses,
        Ancients = source.Ancients,
        Fogged = source.Fogged
    };
}
