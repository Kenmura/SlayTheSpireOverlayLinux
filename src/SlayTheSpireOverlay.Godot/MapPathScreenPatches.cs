using Godot;
using HarmonyLib;
using SlayTheSpireMod.Common;
using SlayTheSpireOverlay.Core.Models;
using SlayTheSpireOverlay.Core.Services;
using SlayTheSpireOverlay.Godot.UI;
using System;
using System.Collections;
using System.Collections.Generic;

namespace SlayTheSpireOverlay.Godot;

public static class MapPathCoordinator
{
    public static void UpdateMapPathSummary(Node? mapScreen)
    {
        if (mapScreen == null || !GodotObject.IsInstanceValid(mapScreen)) return;

        try
        {
            bool isOverlayEnabled = ModEntry.Config?.EnableOverlay ?? true;
            bool isPathSummaryEnabled = ModEntry.Config?.EnableMapPathSummary ?? true;
            bool isHidden = RoomOverlayFactory.IsOverlayHidden;

            if (!MapPathAnalyzer.IsSummaryVisible(isOverlayEnabled, isPathSummaryEnabled, isHidden))
            {
                MapPathOverlayFactory.HideSummary(mapScreen);
                return;
            }

            if (!TryExtractMapGraph(mapScreen, out var graph, out var travelable, out var revealed, out bool bossRevealed))
            {
                MapPathOverlayFactory.HideSummary(mapScreen);
                return;
            }

            var summary = MapPathAnalyzer.CalculateCombinedSummary(graph, travelable, revealed, bossRevealed);
            if (summary.DistinctPathsCount == 0)
            {
                MapPathOverlayFactory.HideSummary(mapScreen);
                return;
            }

            MapPathOverlayFactory.ShowSummary(mapScreen, summary.ToDisplayString());
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Overlay] Error updating map path summary: {ex.Message}");
        }
    }

    private static bool TryExtractMapGraph(
        Node mapScreen,
        out MapGraph graph,
        out List<MapPathCoord> travelableCoords,
        out HashSet<MapPathCoord>? revealedCoords,
        out bool bossRevealed)
    {
        graph = new MapGraph();
        travelableCoords = new List<MapPathCoord>();
        revealedCoords = null;
        bossRevealed = true;

        var pointDict = ReflectionAccess.GetMember(mapScreen, "_mapPointDictionary", "MapPointDictionary") as IDictionary;
        if (pointDict == null) return false;

        var directedEdges = new List<(MapPathCoord From, MapPathCoord To)>();
        bool hasAnyFogMeta = false;
        var fogRevealed = new HashSet<MapPathCoord>();

        foreach (DictionaryEntry entry in pointDict)
        {
            if (entry.Value is not Control ptNode || !GodotObject.IsInstanceValid(ptNode)) continue;

            var pointModel = ReflectionAccess.GetMember(ptNode, "Point", "_point");
            if (pointModel == null) continue;

            if (!TryExtractCoord(ReflectionAccess.GetMember(pointModel, "coord", "Coord"), out var coord)) continue;

            int rawType = 0;
            var ptTypeObj = ReflectionAccess.GetMember(pointModel, "PointType", "_pointType");
            if (ptTypeObj != null)
            {
                rawType = Convert.ToInt32(ptTypeObj);
            }
            var nodeType = MapPathAnalyzer.ConvertRawPointType(rawType);
            graph.AddNode(new MapPathNode(coord, nodeType));

            string? stateStr = ReflectionAccess.GetMember(ptNode, "State", "_state")?.ToString();
            if (string.Equals(stateStr, "Travelable", StringComparison.OrdinalIgnoreCase))
            {
                travelableCoords.Add(coord);
            }

            if (ptNode.HasMeta("FogHidden"))
            {
                hasAnyFogMeta = true;
                bool isHidden = ptNode.GetMeta("FogHidden").AsBool();
                bool isMasked = ptNode.HasMeta("FogMasked") && ptNode.GetMeta("FogMasked").AsBool();
                if (!isHidden && !isMasked)
                {
                    fogRevealed.Add(coord);
                }
            }

            if (ReflectionAccess.GetMember(pointModel, "Children", "_children") is IEnumerable children)
            {
                foreach (object? child in children)
                {
                    if (child != null && TryExtractCoord(ReflectionAccess.GetMember(child, "coord", "Coord"), out var childCoord))
                    {
                        directedEdges.Add((coord, childCoord));
                    }
                }
            }
        }

        RegisterBossPointNode(ReflectionAccess.GetMember(mapScreen, "_bossPointNode", "BossPointNode"), graph, fogRevealed, ref hasAnyFogMeta);
        RegisterBossPointNode(ReflectionAccess.GetMember(mapScreen, "_secondBossPointNode", "SecondBossPointNode"), graph, fogRevealed, ref hasAnyFogMeta);

        foreach (var (from, to) in directedEdges)
        {
            graph.AddEdge(from, to);
        }

        // If MapFog is present or has marked nodes, enforce fog horizon
        if (hasAnyFogMeta || IsMapFogModActive())
        {
            revealedCoords = fogRevealed;
            bossRevealed = !Sts2RuntimeInspector.IsBossFogged();
        }

        if (travelableCoords.Count == 0 && graph.Nodes.Count > 0)
        {
            int minRow = int.MaxValue;
            foreach (var n in graph.Nodes)
            {
                if (n.Coord.Row < minRow) minRow = n.Coord.Row;
            }
            foreach (var n in graph.Nodes)
            {
                if (n.Coord.Row == minRow) travelableCoords.Add(n.Coord);
            }
        }

        return graph.Nodes.Count > 0 && travelableCoords.Count > 0;
    }

    private static bool IsMapFogModActive()
    {
        try
        {
            var mapFogModType = AccessTools.TypeByName("SlayTheSpireMapFog.Godot.ModEntry");
            if (mapFogModType != null)
            {
                var cfg = AccessTools.Property(mapFogModType, "Config")?.GetValue(null);
                if (cfg != null && AccessTools.Property(cfg.GetType(), "Enabled")?.GetValue(cfg) is bool e && e)
                {
                    return true;
                }
            }
        }
        catch
        {
        }
        return false;
    }

    private static void RegisterBossPointNode(
        object? bossNodeObj,
        MapGraph graph,
        HashSet<MapPathCoord> fogRevealed,
        ref bool hasAnyFogMeta)
    {
        if (bossNodeObj is not Control bossPt || !GodotObject.IsInstanceValid(bossPt)) return;

        var pointModel = ReflectionAccess.GetMember(bossPt, "Point", "_point");
        if (pointModel == null) return;

        if (!TryExtractCoord(ReflectionAccess.GetMember(pointModel, "coord", "Coord"), out var coord)) return;

        graph.AddNode(new MapPathNode(coord, MapNodeType.Boss));

        if (bossPt.HasMeta("FogHidden"))
        {
            hasAnyFogMeta = true;
            if (!bossPt.GetMeta("FogHidden").AsBool())
            {
                fogRevealed.Add(coord);
            }
        }
    }

    private static bool TryExtractCoord(object? coordObj, out MapPathCoord coord)
    {
        if (coordObj != null &&
            ReflectionAccess.GetMember(coordObj, "col", "Col") is int col &&
            ReflectionAccess.GetMember(coordObj, "row", "Row") is int row)
        {
            coord = new MapPathCoord(col, row);
            return true;
        }
        coord = default;
        return false;
    }
}

[HarmonyPatch("MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen", "SetMap")]
public static class NMapScreenSetMapPatch
{
    public static void Postfix(Node __instance)
    {
        if (__instance != null && GodotObject.IsInstanceValid(__instance))
        {
            Callable.From(() => MapPathCoordinator.UpdateMapPathSummary(__instance)).CallDeferred();
        }
    }
}

[HarmonyPatch("MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen", "RecalculateTravelability")]
public static class NMapScreenRecalculateTravelabilityPatch
{
    public static void Postfix(Node __instance)
    {
        if (__instance != null && GodotObject.IsInstanceValid(__instance))
        {
            Callable.From(() => MapPathCoordinator.UpdateMapPathSummary(__instance)).CallDeferred();
        }
    }
}

[HarmonyPatch("MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen", "RefreshAllPointVisuals")]
public static class NMapScreenRefreshAllPointVisualsPatch
{
    public static void Postfix(Node __instance)
    {
        if (__instance != null && GodotObject.IsInstanceValid(__instance))
        {
            Callable.From(() => MapPathCoordinator.UpdateMapPathSummary(__instance)).CallDeferred();
        }
    }
}
