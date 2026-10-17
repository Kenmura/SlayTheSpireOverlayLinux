using Godot;
using System;
using SlayTheSpireOverlay.Core.Interfaces;
using SlayTheSpireOverlay.Core.Services;

namespace SlayTheSpireOverlay.Godot.UI;

public static class TierBadgeFactory
{
    public static void CreateOrUpdateBadge(
        Node cardNode,
        ITierListProvider tierProvider,
        string cardId,
        string author = "Baalorlord",
        Func<Node, bool>? isSuppressed = null,
        bool showHoverCommentary = true) =>
        AttachBadge(
            cardNode,
            "TierBadgePanel",
            new Vector2(70, -145),
            tierProvider,
            cardId,
            author,
            stripOptionSuffix: false,
            alignRightOnControl: false,
            cornerRadius: 6,
            marginH: 8,
            marginV: 3,
            separation: 5,
            isSuppressed: isSuppressed,
            showHoverCommentary: showHoverCommentary);

    public static void HideBadge(Node? targetNode, string panelName)
    {
        if (targetNode == null || !GodotObject.IsInstanceValid(targetNode)) return;
        var existing = targetNode.GetNodeOrNull<Control>(panelName);
        if (existing != null && GodotObject.IsInstanceValid(existing))
        {
            existing.SetMeta("TargetId", string.Empty);
            existing.Visible = false;
        }
    }

    internal static void AttachBadge(
        Node? targetNode,
        string panelName,
        Vector2 position,
        ITierListProvider tierProvider,
        string rawId,
        string author,
        bool stripOptionSuffix,
        bool alignRightOnControl,
        int cornerRadius,
        int marginH,
        int marginV,
        int separation,
        Func<Node, bool>? isSuppressed = null,
        bool showHoverCommentary = true)
    {
        if (targetNode == null || !GodotObject.IsInstanceValid(targetNode)) return;

        string lookupKey = OverlayCalculators.NormalizeId(rawId, stripOptionSuffix);
        if (string.IsNullOrEmpty(lookupKey))
        {
            HideBadge(targetNode, panelName);
            return;
        }

        PanelContainer? panel = targetNode.GetNodeOrNull<PanelContainer>(panelName);
        Label? tierLabel;
        Label? dividerLabel;
        Label? scoreLabel;

        if (panel == null || !GodotObject.IsInstanceValid(panel))
        {
            // MouseFilter.Pass allows hover commentary popups to detect mouse enter/exit
            // while propagating all clicks and hovers through to the underlying game control.
            panel = new PanelContainer { Name = panelName, MouseFilter = Control.MouseFilterEnum.Pass };

            var styleBox = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.09f, 0.12f, 0.92f),
                CornerRadiusTopLeft = cornerRadius,
                CornerRadiusTopRight = cornerRadius,
                CornerRadiusBottomLeft = cornerRadius,
                CornerRadiusBottomRight = cornerRadius,
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(1.0f, 1.0f, 1.0f, 0.2f),
                ContentMarginLeft = marginH,
                ContentMarginTop = marginV,
                ContentMarginRight = marginH,
                ContentMarginBottom = marginV
            };
            panel.AddThemeStyleboxOverride("panel", styleBox);

            var layout = new HBoxContainer { Name = "BadgeLayout", MouseFilter = Control.MouseFilterEnum.Ignore };
            layout.AddThemeConstantOverride("separation", separation);

            tierLabel = new Label { Name = "TierLabel", VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
            dividerLabel = new Label { Name = "DividerLabel", VerticalAlignment = VerticalAlignment.Center, Text = "|", SelfModulate = new Color(0.5f, 0.5f, 0.6f, 0.6f), MouseFilter = Control.MouseFilterEnum.Ignore };
            scoreLabel = new Label { Name = "ScoreLabel", VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };

            layout.AddChild(tierLabel);
            layout.AddChild(dividerLabel);
            layout.AddChild(scoreLabel);
            panel.AddChild(layout);

            panel.Position = position;
            panel.Visible = false;
            tierLabel.Text = "";
            scoreLabel.Text = "";
            RoomOverlayFactory.AttachHoverCommentaryPopup(panel);

            targetNode.AddChild(panel);
        }
        else
        {
            var layout = panel.GetNodeOrNull<HBoxContainer>("BadgeLayout");
            if (layout == null) return;
            tierLabel = layout.GetNodeOrNull<Label>("TierLabel");
            dividerLabel = layout.GetNodeOrNull<Label>("DividerLabel");
            scoreLabel = layout.GetNodeOrNull<Label>("ScoreLabel");
            if (tierLabel == null || dividerLabel == null || scoreLabel == null) return;
        }

        string expectedToken = $"{author}:{lookupKey}";
        panel.SetMeta("TargetId", expectedToken);

        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var tierList = await tierProvider.GetTierListAsync().ConfigureAwait(false);
                string strippedKey = lookupKey.Replace("_", "", StringComparison.Ordinal);

                if (tierList.TryGetValue(lookupKey, out var data) ||
                    (strippedKey != lookupKey && tierList.TryGetValue(strippedKey, out data)))
                {
                    Callable.From(() =>
                    {
                        if (!GodotObject.IsInstanceValid(targetNode) ||
                            !GodotObject.IsInstanceValid(panel) ||
                            !GodotObject.IsInstanceValid(tierLabel) ||
                            !GodotObject.IsInstanceValid(dividerLabel) ||
                            !GodotObject.IsInstanceValid(scoreLabel))
                        {
                            return;
                        }

                        if (!panel.HasMeta("TargetId") || panel.GetMeta("TargetId").AsString() != expectedToken)
                        {
                            return;
                        }

                        if (alignRightOnControl && targetNode is Control ctrl && ctrl.Size.X > 220f)
                        {
                            panel.Position = new Vector2(Math.Max(position.X, ctrl.Size.X - 95f), position.Y);
                        }

                        Color tierColor = GetColorForTier(data.Tier);
                        if (panel.GetThemeStylebox("panel") is StyleBoxFlat sb)
                        {
                            sb.BorderColor = new Color(tierColor.R, tierColor.G, tierColor.B, 0.65f);
                        }

                        bool suppressed = isSuppressed != null && isSuppressed(targetNode);
                        panel.Visible = !RoomOverlayFactory.IsOverlayHidden && !suppressed;

                        bool hasScore = data.Score > 0;
                        dividerLabel.Visible = hasScore;
                        tierLabel.Text = data.Tier;
                        tierLabel.SelfModulate = tierColor;
                        scoreLabel.Text = hasScore ? data.Score.ToString("0.0") : "";
                        scoreLabel.SelfModulate = new Color(0.95f, 0.95f, 0.98f);
                        panel.TooltipText = string.Empty;
                        panel.SetMeta("HoverText", showHoverCommentary ? RoomOverlayFactory.FormatHoverCommentary(data.Tier, data.Commentary, author) : string.Empty);
                    }).CallDeferred();
                }
                else
                {
                    Callable.From(() =>
                    {
                        if (GodotObject.IsInstanceValid(panel) &&
                            panel.HasMeta("TargetId") &&
                            panel.GetMeta("TargetId").AsString() == expectedToken)
                        {
                            panel.SetMeta("HoverText", string.Empty);
                            panel.Visible = false;
                        }
                    }).CallDeferred();
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[STS2 Overlay] Error loading badge rating data: {ex.Message}");
            }
        });
    }

    public static Color GetColorForTier(string? tier) => (tier?.Trim()) switch
    {
        "S" or "S Tier" or "Always Amazing" => new Color(1.0f, 0.35f, 0.35f),
        "A" or "A Tier" or "Always Good" => new Color(1.0f, 0.65f, 0.25f),
        "B" or "B Tier" or "Great" or "Good" => new Color(0.95f, 0.95f, 0.3f),
        "C" or "C Tier" or "Needs Synergy" => new Color(0.45f, 0.85f, 1.0f),
        "D" or "D Tier" or "Inconsistent" => new Color(0.85f, 0.5f, 1.0f),
        "F" or "F Tier" => new Color(0.78f, 0.42f, 0.38f),
        "Map Dependent" or "Situational" => new Color(1.0f, 0.7f, 0.3f),
        "Almost Never" or "Never Take" or "Skip" => new Color(0.65f, 0.65f, 0.65f),
        _ => new Color(0.85f, 0.85f, 0.85f)
    };
}
