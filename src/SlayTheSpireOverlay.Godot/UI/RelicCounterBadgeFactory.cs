using Godot;
using System;
using System.Collections.Generic;
using SlayTheSpireMod.Common;
using SlayTheSpireOverlay.Core.Services;

namespace SlayTheSpireOverlay.Godot.UI;

/// <summary>Thin Godot adapter: draws the proc counter pill ("[9/10]") on top-bar relics. All decisions live in RelicProcRules.</summary>
public static class RelicCounterBadgeFactory
{
    public const string PanelName = "RelicCounterBadge";
    private static readonly Color NormalColor = new(0.85f, 0.88f, 0.95f);
    private static readonly Color PreProcColor = Color.FromHtml(RelicProcRules.PreProcColorHex);

    // Counter-bearing top-bar relic nodes, so a counter change can refresh them without re-scanning the tree.
    private static readonly List<WeakReference<Node>> Tracked = new();

    /// <summary>Reads the counter state from a relic model; null for unsupported relics or missing members.</summary>
    public static RelicProcInfo? ReadInfo(object? relicModel)
    {
        if (relicModel == null) return null;
        try
        {
            string? id = ReflectionAccess.GetMember(ReflectionAccess.GetMember(relicModel, "Id"), "Entry") as string;
            if (!RelicProcRules.IsSupported(id)) return null;
            int? current = ReflectionAccess.GetMember(relicModel, "DisplayAmount") as int?;
            int? max = ReflectionAccess.GetMember(relicModel, "_attacksThreshold", "turnsThreshold", "_turnsThreshold") as int?;
            return RelicProcRules.TryGetProcInfo(id, current, max);
        }
        catch
        {
            return null;
        }
    }

    public static void Refresh(Node? relicNode, object? relicModel, bool enabled, bool isInTopBar)
    {
        if (relicNode == null || !GodotObject.IsInstanceValid(relicNode)) return;
        var info = enabled && isInTopBar ? ReadInfo(relicModel) : null;
        var existing = relicNode.GetNodeOrNull<PanelContainer>(PanelName);
        if (info == null)
        {
            if (existing != null && GodotObject.IsInstanceValid(existing)) existing.Visible = false;
            return;
        }

        Track(relicNode);
        Label label;
        if (existing == null || !GodotObject.IsInstanceValid(existing))
        {
            existing = new PanelContainer { Name = PanelName, MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(0, 30) };
            existing.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.09f, 0.12f, 0.92f),
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
                ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 1, ContentMarginBottom = 1,
            });
            label = new Label { Name = "CounterLabel", MouseFilter = Control.MouseFilterEnum.Ignore };
            label.AddThemeFontSizeOverride("font_size", 12);
            existing.AddChild(label);
            relicNode.AddChild(existing);
        }
        else
        {
            label = existing.GetNodeOrNull<Label>("CounterLabel") ?? new Label();
        }

        label.Text = info.BadgeText;
        label.SelfModulate = info.IsPreProc ? PreProcColor : NormalColor;
        existing.SetMeta("PreProc", info.IsPreProc);
        existing.Visible = true;
    }

    /// <summary>Re-reads every tracked relic whose model is <paramref name="model"/> (called when its DisplayAmount changes).</summary>
    public static void RefreshForModel(object model, bool enabled)
    {
        Callable.From(() =>
        {
            Tracked.RemoveAll(w => !w.TryGetTarget(out var n) || !GodotObject.IsInstanceValid(n));
            foreach (var w in Tracked.ToArray())
            {
                if (!w.TryGetTarget(out var node)) continue;
                if (ReferenceEquals(ReflectionAccess.GetMember(node, "Model"), model)) Refresh(node, model, enabled, isInTopBar: true);
            }
        }).CallDeferred();
    }

    private static void Track(Node node)
    {
        foreach (var w in Tracked) if (w.TryGetTarget(out var n) && ReferenceEquals(n, node)) return;
        Tracked.Add(new WeakReference<Node>(node));
    }
}
