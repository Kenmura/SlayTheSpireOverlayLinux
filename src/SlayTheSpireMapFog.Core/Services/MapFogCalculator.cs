using System;
using System.Collections.Generic;
using SlayTheSpireMapFog.Core.Options;

namespace SlayTheSpireMapFog.Core.Services;

public readonly record struct FogCoord(int Col, int Row);

public sealed class MapFogVisibility
{
    private readonly HashSet<FogCoord> _revealedCoords;
    private readonly HashSet<FogCoord> _bossCoords;
    private readonly MapFogConfig _config;

    public MapFogVisibility(HashSet<FogCoord> revealedCoords, HashSet<FogCoord> bossCoords, MapFogConfig config)
    {
        _revealedCoords = revealedCoords ?? throw new ArgumentNullException(nameof(revealedCoords));
        _bossCoords = bossCoords ?? throw new ArgumentNullException(nameof(bossCoords));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public bool IsRevealed(FogCoord coord) =>
        !_config.Enabled ||
        _revealedCoords.Contains(coord) ||
        (_config.ShowBoss && _bossCoords.Contains(coord));

    public bool IsNodeVisible(FogCoord coord) =>
        !_config.Enabled ||
        _config.Mode == FogMode.HiddenIcons ||
        IsRevealed(coord);

    public bool ShouldMaskAsUnknown(FogCoord coord) =>
        _config.Enabled &&
        _config.Mode == FogMode.HiddenIcons &&
        !IsRevealed(coord) &&
        !_bossCoords.Contains(coord);

    public bool IsEdgeVisible(FogCoord from, FogCoord to) =>
        !_config.Enabled ||
        _config.Mode == FogMode.HiddenIcons ||
        (_revealedCoords.Contains(from) && IsRevealed(to));
}

public static class MapFogCalculator
{
    public static MapFogVisibility CalculateVisibility(
        IEnumerable<FogCoord> visitedCoords,
        IEnumerable<FogCoord> travelableCoords,
        IEnumerable<FogCoord> bossCoords,
        IEnumerable<(FogCoord From, FogCoord To)> directedEdges,
        MapFogConfig config)
    {
        ArgumentNullException.ThrowIfNull(visitedCoords);
        ArgumentNullException.ThrowIfNull(travelableCoords);
        ArgumentNullException.ThrowIfNull(bossCoords);
        ArgumentNullException.ThrowIfNull(directedEdges);
        ArgumentNullException.ThrowIfNull(config);

        var revealed = new HashSet<FogCoord>();
        foreach (var visited in visitedCoords)
        {
            revealed.Add(visited);
        }

        var bosses = new HashSet<FogCoord>();
        foreach (var boss in bossCoords)
        {
            bosses.Add(boss);
        }

        int depth = Math.Max(1, config.VisionDepth);
        var adjacency = new Dictionary<FogCoord, List<FogCoord>>();
        foreach (var (from, to) in directedEdges)
        {
            if (!adjacency.TryGetValue(from, out var children))
            {
                children = new List<FogCoord>();
                adjacency[from] = children;
            }
            children.Add(to);
        }

        var queue = new Queue<(FogCoord Coord, int Step)>();
        foreach (var travelable in travelableCoords)
        {
            if (revealed.Add(travelable))
            {
                queue.Enqueue((travelable, 1));
            }
            else
            {
                queue.Enqueue((travelable, 1));
            }
        }

        while (queue.Count > 0)
        {
            var (current, step) = queue.Dequeue();
            if (step >= depth) continue;

            if (adjacency.TryGetValue(current, out var nextNodes))
            {
                foreach (var next in nextNodes)
                {
                    if (revealed.Add(next))
                    {
                        queue.Enqueue((next, step + 1));
                    }
                }
            }
        }

        return new MapFogVisibility(revealed, bosses, config);
    }
}
