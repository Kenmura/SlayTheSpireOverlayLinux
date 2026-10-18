using Godot;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using SlayTheSpireMapFog.Core.Options;
using SlayTheSpireMapFog.Core.Services;

namespace SlayTheSpireMapFog.Godot;

public static class Sts2MapInspector
{
    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<(Type, string), MethodInfo?> MethodCache = new();
    private static readonly ConcurrentDictionary<(Type, string), MemberInfo?> MemberCache = new();
    private static Texture2D? _cachedUnknownIcon;
    private static Texture2D? _cachedUnknownOutline;

    public static void ApplyMapFog(object? mapScreen, MapFogConfig config)
    {
        if (mapScreen is not Node screenNode || !GodotObject.IsInstanceValid(screenNode) || config == null) return;

        try
        {
            var visitedCoords = new List<FogCoord>();
            var travelableCoords = new List<FogCoord>();
            var bossCoords = new List<FogCoord>();
            var directedEdges = new List<(FogCoord From, FogCoord To)>();
            var pointNodes = new List<(FogCoord Coord, Control Node)>();

            var runState = GetMember(mapScreen, "_runState", "RunState");
            if (runState != null && GetMember(runState, "VisitedMapCoords", "_visitedMapCoords") is IEnumerable visitedList)
            {
                foreach (object? coordObj in visitedList)
                {
                    if (TryExtractCoord(coordObj, out var fc))
                    {
                        visitedCoords.Add(fc);
                    }
                }
            }

            if (GetMember(mapScreen, "_mapPointDictionary", "MapPointDictionary") is IDictionary pointDict)
            {
                foreach (DictionaryEntry entry in pointDict)
                {
                    if (entry.Value is Control ptNode &&
                        GodotObject.IsInstanceValid(ptNode) &&
                        TryExtractCoord(entry.Key, out var coord))
                    {
                        RegisterMapPointNode(ptNode, coord, pointNodes, visitedCoords, travelableCoords, directedEdges);
                    }
                }
            }

            RegisterSpecialPointNode(GetMember(mapScreen, "_startingPointNode", "StartingPointNode"), isBoss: false, pointNodes, visitedCoords, travelableCoords, bossCoords, directedEdges);
            RegisterSpecialPointNode(GetMember(mapScreen, "_bossPointNode", "BossPointNode"), isBoss: true, pointNodes, visitedCoords, travelableCoords, bossCoords, directedEdges);
            RegisterSpecialPointNode(GetMember(mapScreen, "_secondBossPointNode", "SecondBossPointNode"), isBoss: true, pointNodes, visitedCoords, travelableCoords, bossCoords, directedEdges);

            var pathEntries = new List<(FogCoord From, FogCoord To, IEnumerable Ticks)>();
            if (GetMember(mapScreen, "_paths", "Paths") is IDictionary pathsDict)
            {
                foreach (DictionaryEntry entry in pathsDict)
                {
                    if (entry.Key != null &&
                        entry.Value is IEnumerable ticks &&
                        TryExtractCoord(GetMember(entry.Key, "Item1"), out var fromCoord) &&
                        TryExtractCoord(GetMember(entry.Key, "Item2"), out var toCoord))
                    {
                        directedEdges.Add((fromCoord, toCoord));
                        pathEntries.Add((fromCoord, toCoord, ticks));
                    }
                }
            }

            var visibility = MapFogCalculator.CalculateVisibility(visitedCoords, travelableCoords, bossCoords, directedEdges, config);

            foreach (var (coord, ptNode) in pointNodes)
            {
                bool isRevealed = visibility.IsRevealed(coord);
                bool shouldMask = visibility.ShouldMaskAsUnknown(coord);
                ptNode.SetMeta("FogHidden", !isRevealed);
                ptNode.SetMeta("FogMasked", shouldMask);
                ptNode.Visible = visibility.IsNodeVisible(coord);

                if (ptNode.GetType().Name == "NNormalMapPoint")
                {
                    if (shouldMask)
                    {
                        ApplyMaskToNormalPoint(ptNode);
                    }
                    else
                    {
                        GetCachedMethod(ptNode.GetType(), "UpdateIcon")?.Invoke(ptNode, null);
                        GetCachedMethod(ptNode.GetType(), "RefreshMarkedIconVisibility")?.Invoke(ptNode, null);
                    }
                }
            }

            ApplyDarkFogOverlay(pointNodes, bossCoords, visibility, config);

            foreach (var (fromCoord, toCoord, ticks) in pathEntries)
            {
                bool edgeVisible = visibility.IsEdgeVisible(fromCoord, toCoord);
                foreach (object? tickObj in ticks)
                {
                    if (tickObj is CanvasItem tick && GodotObject.IsInstanceValid(tick))
                    {
                        tick.Visible = edgeVisible;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 MapFog] Error applying map fog: {ex.Message}");
        }
    }

    private const string DarkFogOverlayName = "MapFogDarkOverlay";
    private const float FogFadeLength = 260f;
    private const float FogFrontierPadding = 70f;
    private const float FogExtent = 6000f;
    private static readonly Color DarkFogColor = new(0.03f, 0.04f, 0.07f, 0.9f);

    private static void ApplyDarkFogOverlay(
        List<(FogCoord Coord, Control Node)> pointNodes,
        List<FogCoord> bossCoords,
        MapFogVisibility visibility,
        MapFogConfig config)
    {
        Node? parent = pointNodes.Count > 0 ? pointNodes[0].Node.GetParent() : null;
        if (parent == null || !GodotObject.IsInstanceValid(parent)) return;

        var existing = parent.GetNodeOrNull(DarkFogOverlayName);
        if (existing != null && GodotObject.IsInstanceValid(existing))
        {
            parent.RemoveChild(existing);
            existing.QueueFree();
        }

        if (!config.Enabled || !config.DarkFog || config.Mode != FogMode.FullFog) return;

        var bosses = new HashSet<FogCoord>(bossCoords);
        float minX = float.MaxValue, maxX = float.MinValue;
        float revealedMinY = float.MaxValue, revealedMaxY = float.MinValue;
        float bossY = float.NaN;
        foreach (var (coord, node) in pointNodes)
        {
            if (node.GetParent() != parent) continue;
            minX = Math.Min(minX, node.Position.X);
            maxX = Math.Max(maxX, node.Position.X);
            if (bosses.Contains(coord))
            {
                bossY = node.Position.Y;
            }
            else if (visibility.IsRevealed(coord))
            {
                revealedMinY = Math.Min(revealedMinY, node.Position.Y);
                revealedMaxY = Math.Max(revealedMaxY, node.Position.Y);
            }
        }

        if (revealedMinY > revealedMaxY) return;

        // Fog spreads from the explored frontier toward the boss (the map normally climbs upward).
        bool fogAbove = float.IsNaN(bossY) || bossY <= (revealedMinY + revealedMaxY) / 2f;
        float frontier = fogAbove ? revealedMinY - FogFrontierPadding : revealedMaxY + FogFrontierPadding;
        float left = minX - FogExtent / 2f;
        float width = maxX - minX + FogExtent;

        var overlay = new Control { Name = DarkFogOverlayName, MouseFilter = Control.MouseFilterEnum.Ignore };

        var gradient = new Gradient();
        gradient.SetColor(0, new Color(DarkFogColor.R, DarkFogColor.G, DarkFogColor.B, 0f));
        gradient.SetColor(1, DarkFogColor);
        var fadeTexture = new GradientTexture2D
        {
            Gradient = gradient,
            Width = 4,
            Height = 256,
            FillFrom = new Vector2(0f, fogAbove ? 1f : 0f),
            FillTo = new Vector2(0f, fogAbove ? 0f : 1f)
        };

        float fadeTop = fogAbove ? frontier - FogFadeLength : frontier;
        overlay.AddChild(new TextureRect
        {
            Name = "FogFade",
            Texture = fadeTexture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Position = new Vector2(left, fadeTop),
            Size = new Vector2(width, FogFadeLength),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        float solidTop = fogAbove ? fadeTop - FogExtent : fadeTop + FogFadeLength;
        overlay.AddChild(new ColorRect
        {
            Name = "FogSolid",
            Color = DarkFogColor,
            Position = new Vector2(left, solidTop),
            Size = new Vector2(width, FogExtent),
            MouseFilter = Control.MouseFilterEnum.Ignore
        });

        parent.AddChild(overlay);
        parent.MoveChild(overlay, 0);
    }

    public static void ApplyMaskToNormalPoint(object? normalMapPoint)
    {
        if (normalMapPoint is not Control ptNode || !GodotObject.IsInstanceValid(ptNode)) return;
        if (!ptNode.HasMeta("FogMasked") || !ptNode.GetMeta("FogMasked").AsBool()) return;

        try
        {
            EnsureUnknownTexturesLoaded(ptNode.GetType());
            if (_cachedUnknownIcon != null &&
                GetMember(ptNode, "_icon", "Icon") is TextureRect iconRect &&
                GodotObject.IsInstanceValid(iconRect))
            {
                iconRect.Texture = _cachedUnknownIcon;
            }

            if (_cachedUnknownOutline != null &&
                GetMember(ptNode, "_outline", "Outline") is TextureRect outlineRect &&
                GodotObject.IsInstanceValid(outlineRect))
            {
                outlineRect.Texture = _cachedUnknownOutline;
            }

            if (GetMember(ptNode, "_questIcon", "QuestIcon") is CanvasItem questIcon &&
                GodotObject.IsInstanceValid(questIcon))
            {
                questIcon.Visible = false;
            }
        }
        catch
        {
        }
    }

    public static bool IsPointFogHidden(object? mapPoint) =>
        mapPoint is Node ptNode &&
        GodotObject.IsInstanceValid(ptNode) &&
        ptNode.HasMeta("FogHidden") &&
        ptNode.GetMeta("FogHidden").AsBool();

    public static void RefreshActiveMapInScene(Node? contextNode, MapFogConfig config)
    {
        var root = contextNode != null && GodotObject.IsInstanceValid(contextNode)
            ? contextNode.GetTree()?.CurrentScene
            : null;
        if (root != null && GodotObject.IsInstanceValid(root))
        {
            RefreshMapScreenRecursive(root, config);
        }
    }

    private static void RefreshMapScreenRecursive(Node node, MapFogConfig config)
    {
        if (!GodotObject.IsInstanceValid(node)) return;
        if (node.GetType().Name == "NMapScreen")
        {
            ApplyMapFog(node, config);
            return;
        }

        foreach (Node child in node.GetChildren())
        {
            RefreshMapScreenRecursive(child, config);
        }
    }

    public static bool IsOurMod(object? mod)
    {
        if (mod == null) return false;
        try
        {
            var manifest = GetMember(mod, "manifest", "Manifest");
            string? id = manifest != null ? GetMember(manifest, "id", "Id") as string : null;
            if (!string.IsNullOrEmpty(id) && id.Contains("SlayTheSpireMapFog", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string? pck = GetMember(mod, "pckName", "PckName") as string;
            return !string.IsNullOrEmpty(pck) && pck.Contains("SlayTheSpireMapFog", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void EnableModdingButtonInRun(object? settingsScreen)
    {
        if (settingsScreen is not Node screenNode || !GodotObject.IsInstanceValid(screenNode)) return;
        try
        {
            var moddingRow = screenNode.GetNodeOrNull<CanvasItem>("%Modding");
            if (moddingRow != null && GodotObject.IsInstanceValid(moddingRow)) moddingRow.Visible = true;

            var moddingDivider = screenNode.GetNodeOrNull<CanvasItem>("%ModdingDivider");
            if (moddingDivider != null && GodotObject.IsInstanceValid(moddingDivider)) moddingDivider.Visible = true;

            var btn = GetMember(settingsScreen, "_moddingScreenButton", "ModdingScreenButton") ?? screenNode.GetNodeOrNull("%ModdingButton");
            if (btn != null)
            {
                GetCachedMethod(btn.GetType(), "Enable")?.Invoke(btn, null);
            }
        }
        catch
        {
        }
    }

    public static object? GetOrCreateRunModdingScreen(object? submenuStack)
    {
        if (submenuStack is not Node stackNode || !GodotObject.IsInstanceValid(stackNode)) return null;
        try
        {
            var existing = stackNode.GetNodeOrNull("OverlayRunModdingScreen");
            if (existing != null && GodotObject.IsInstanceValid(existing))
            {
                return existing;
            }

            var moddingScreenType = GetCachedType("MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModdingScreen");
            var createMethod = moddingScreenType != null ? GetCachedMethod(moddingScreenType, "Create") : null;
            if (createMethod?.Invoke(null, null) is Control moddingScreen && GodotObject.IsInstanceValid(moddingScreen))
            {
                moddingScreen.Name = "OverlayRunModdingScreen";
                moddingScreen.Visible = false;
                stackNode.AddChild(moddingScreen);
                return moddingScreen;
            }
        }
        catch
        {
        }
        return null;
    }

    private static void RegisterSpecialPointNode(
        object? nodeObj,
        bool isBoss,
        List<(FogCoord Coord, Control Node)> pointNodes,
        List<FogCoord> visitedCoords,
        List<FogCoord> travelableCoords,
        List<FogCoord> bossCoords,
        List<(FogCoord From, FogCoord To)> directedEdges)
    {
        if (nodeObj is not Control ptNode || !GodotObject.IsInstanceValid(ptNode)) return;
        var pointModel = GetMember(ptNode, "Point", "_point");
        if (!TryExtractCoord(GetMember(pointModel, "coord", "Coord"), out var coord)) return;

        if (isBoss) bossCoords.Add(coord);
        RegisterMapPointNode(ptNode, coord, pointNodes, visitedCoords, travelableCoords, directedEdges);
    }

    private static void RegisterMapPointNode(
        Control ptNode,
        FogCoord coord,
        List<(FogCoord Coord, Control Node)> pointNodes,
        List<FogCoord> visitedCoords,
        List<FogCoord> travelableCoords,
        List<(FogCoord From, FogCoord To)> directedEdges)
    {
        pointNodes.Add((coord, ptNode));

        string? stateStr = GetMember(ptNode, "State", "_state")?.ToString();
        if (string.Equals(stateStr, "Traveled", StringComparison.OrdinalIgnoreCase))
        {
            visitedCoords.Add(coord);
        }
        else if (string.Equals(stateStr, "Travelable", StringComparison.OrdinalIgnoreCase))
        {
            travelableCoords.Add(coord);
        }

        var pointModel = GetMember(ptNode, "Point", "_point");
        if (pointModel != null && GetMember(pointModel, "Children", "_children") is IEnumerable children)
        {
            foreach (object? childPoint in children)
            {
                if (TryExtractCoord(GetMember(childPoint, "coord", "Coord"), out var childCoord))
                {
                    directedEdges.Add((coord, childCoord));
                }
            }
        }
    }

    private static bool TryExtractCoord(object? coordObj, out FogCoord coord)
    {
        if (coordObj != null &&
            GetMember(coordObj, "col", "Col") is int col &&
            GetMember(coordObj, "row", "Row") is int row)
        {
            coord = new FogCoord(col, row);
            return true;
        }
        coord = default;
        return false;
    }

    private static void EnsureUnknownTexturesLoaded(Type normalMapPointType)
    {
        if (_cachedUnknownIcon == null)
        {
            string? iconPath = GetCachedMethod(normalMapPointType, "IconPath")?.Invoke(null, new object[] { "map_unknown" }) as string;
            if (!string.IsNullOrEmpty(iconPath))
            {
                _cachedUnknownIcon = ResourceLoader.Load<Texture2D>(iconPath);
            }
        }

        if (_cachedUnknownOutline == null)
        {
            string? outlinePath = GetCachedMethod(normalMapPointType, "OutlinePath")?.Invoke(null, new object[] { "map_unknown" }) as string;
            if (!string.IsNullOrEmpty(outlinePath))
            {
                _cachedUnknownOutline = ResourceLoader.Load<Texture2D>(outlinePath);
            }
        }
    }

    private static Type? GetCachedType(string fullName) =>
        TypeCache.GetOrAdd(fullName, static name => AccessTools.TypeByName(name));

    private static MethodInfo? GetCachedMethod(Type type, string methodName) =>
        MethodCache.GetOrAdd((type, methodName), static key => AccessTools.Method(key.Item1, key.Item2));

    private static object? GetMember(object? target, params string[] names)
    {
        if (target == null) return null;
        var type = target.GetType();
        foreach (string name in names)
        {
            var member = MemberCache.GetOrAdd((type, name), static key =>
            {
                var prop = AccessTools.Property(key.Item1, key.Item2);
                if (prop != null && prop.CanRead) return prop;
                return AccessTools.Field(key.Item1, key.Item2);
            });

            if (member is PropertyInfo p)
            {
                var val = p.GetValue(target);
                if (val != null) return val;
            }
            else if (member is FieldInfo f)
            {
                var val = f.GetValue(target);
                if (val != null) return val;
            }
        }
        return null;
    }
}
