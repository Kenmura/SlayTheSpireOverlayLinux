using System;
using System.Collections.Generic;
using System.Text;

namespace SlayTheSpireOverlay.Core.Models;

public enum MapNodeType
{
    Unknown = 0,
    Monster = 1,
    Elite = 2,
    RestSite = 3,
    Shop = 4,
    Treasure = 5,
    Boss = 6,
    Ancient = 7,
    Fogged = 8
}

public readonly record struct MapPathCoord(int Col, int Row)
{
    public override string ToString() => $"({Col},{Row})";
}

public sealed class MapPathNode
{
    public MapPathCoord Coord { get; }
    public MapNodeType Type { get; set; }
    public HashSet<MapPathCoord> Children { get; } = new();
    public HashSet<MapPathCoord> Parents { get; } = new();

    public MapPathNode(MapPathCoord coord, MapNodeType type)
    {
        Coord = coord;
        Type = type;
    }
}

public sealed class MapGraph
{
    private readonly Dictionary<MapPathCoord, MapPathNode> _nodes = new();

    public IReadOnlyCollection<MapPathNode> Nodes => _nodes.Values;

    public void AddNode(MapPathNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        _nodes[node.Coord] = node;
    }

    public void AddEdge(MapPathCoord from, MapPathCoord to)
    {
        if (_nodes.TryGetValue(from, out var fromNode) && _nodes.TryGetValue(to, out var toNode))
        {
            fromNode.Children.Add(to);
            toNode.Parents.Add(from);
        }
    }

    public MapPathNode? GetNode(MapPathCoord coord) => _nodes.TryGetValue(coord, out var node) ? node : null;

    public bool ContainsCoord(MapPathCoord coord) => _nodes.ContainsKey(coord);
}

public readonly record struct RangeStat(int Min, int Max)
{
    public override string ToString() => Min == Max ? $"{Min}" : $"{Min}-{Max}";

    public static RangeStat Zero => new(0, 0);

    public static RangeStat Combine(RangeStat a, RangeStat b) =>
        new(Math.Min(a.Min, b.Min), Math.Max(a.Max, b.Max));
}

public sealed class MapPathSummary
{
    public RangeStat Elites { get; init; } = RangeStat.Zero;
    public RangeStat RestSites { get; init; } = RangeStat.Zero;
    public RangeStat Shops { get; init; } = RangeStat.Zero;
    public RangeStat Unknowns { get; init; } = RangeStat.Zero;
    public RangeStat Monsters { get; init; } = RangeStat.Zero;
    public RangeStat Treasures { get; init; } = RangeStat.Zero;
    public RangeStat Bosses { get; init; } = RangeStat.Zero;
    public RangeStat Ancients { get; init; } = RangeStat.Zero;
    public RangeStat FoggedNodes { get; init; } = RangeStat.Zero;

    public int TotalReachableNodes { get; init; }
    public int DistinctPathsCount { get; init; }

    public string ToDisplayString()
    {
        var sb = new StringBuilder();
        sb.Append($"Paths: 💀 Elites: {Elites} | ⛺ Rest: {RestSites} | 💰 Shops: {Shops}");
        if (Unknowns.Max > 0)
        {
            sb.Append($" | ? Unknown: {Unknowns}");
        }
        if (FoggedNodes.Max > 0)
        {
            sb.Append($" | 🌫️ Fogged: {FoggedNodes}");
        }
        return sb.ToString();
    }
}
