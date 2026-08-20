using Godot;
using System;
using SlayTheSpireOverlay.Core.Interfaces;
using SlayTheSpireOverlay.Core.Models;

namespace SlayTheSpireOverlay.Godot.UI;

public static class TierBadgeFactory
{
    public static void CreateOrUpdateBadge(Node cardNode, ITierListProvider tierProvider, string cardId)
    {
        if (cardNode == null || !GodotObject.IsInstanceValid(cardNode)) return;

        // Check if badge panel already exists on this card node
        PanelContainer panel = cardNode.GetNodeOrNull<PanelContainer>("TierBadgePanel");
        Label tierLabel;
        Label dividerLabel;
        Label scoreLabel;

        if (panel == null || !GodotObject.IsInstanceValid(panel))
        {
            panel = new PanelContainer();
            panel.Name = "TierBadgePanel";

            var styleBox = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.09f, 0.12f, 0.92f),
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(1.0f, 1.0f, 1.0f, 0.2f),
                ContentMarginLeft = 8,
                ContentMarginTop = 3,
                ContentMarginRight = 8,
                ContentMarginBottom = 3
            };
            panel.AddThemeStyleboxOverride("panel", styleBox);

            var layout = new HBoxContainer();
            layout.Name = "BadgeLayout";
            layout.AddThemeConstantOverride("separation", 5);

            tierLabel = new Label();
            tierLabel.Name = "TierLabel";
            dividerLabel = new Label();
            dividerLabel.Name = "DividerLabel";
            scoreLabel = new Label();
            scoreLabel.Name = "ScoreLabel";

            tierLabel.VerticalAlignment = VerticalAlignment.Center;
            dividerLabel.VerticalAlignment = VerticalAlignment.Center;
            scoreLabel.VerticalAlignment = VerticalAlignment.Center;

            dividerLabel.Text = "|";
            dividerLabel.SelfModulate = new Color(0.5f, 0.5f, 0.6f, 0.6f);

            layout.AddChild(tierLabel);
            layout.AddChild(dividerLabel);
            layout.AddChild(scoreLabel);
            panel.AddChild(layout);

            // Position pill at top-right of the card header frame
            panel.Position = new Vector2(70, -145);

            tierLabel.Text = "...";
            scoreLabel.Text = "";

            cardNode.AddChild(panel);
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

        // Load tier data asynchronously off-thread and update labels thread-safely
        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var tierList = await tierProvider.GetTierListAsync().ConfigureAwait(false);
                string lookupKey = NormalizeCardId(cardId);
                string strippedKey = lookupKey.Replace("_", "");

                if (tierList.TryGetValue(lookupKey, out var cardData) ||
                    (strippedKey != lookupKey && tierList.TryGetValue(strippedKey, out cardData)))
                {
                    Callable.From(() =>
                    {
                        if (GodotObject.IsInstanceValid(panel) && GodotObject.IsInstanceValid(tierLabel) && GodotObject.IsInstanceValid(scoreLabel))
                        {
                            dividerLabel.Visible = true;
                            tierLabel.Text = cardData.Tier;
                            scoreLabel.Text = cardData.Score.ToString("0.0");
                            tierLabel.SelfModulate = GetColorForTier(cardData.Tier);
                            scoreLabel.SelfModulate = new Color(0.95f, 0.95f, 0.98f);
                            panel.TooltipText = cardData.Commentary ?? "";
                        }
                    }).CallDeferred();
                }
                else
                {
                    Callable.From(() =>
                    {
                        if (GodotObject.IsInstanceValid(panel) && GodotObject.IsInstanceValid(tierLabel) && GodotObject.IsInstanceValid(scoreLabel))
                        {
                            tierLabel.Text = "N/A";
                            dividerLabel.Visible = false;
                            scoreLabel.Text = "";
                            tierLabel.SelfModulate = new Color(0.6f, 0.6f, 0.6f);
                            panel.TooltipText = "No evaluation data found for this card.";
                        }
                    }).CallDeferred();
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[STS2 Overlay] Error loading card rating data: {ex.Message}");
            }
        });
    }

    private static string NormalizeCardId(string rawId)
    {
        if (string.IsNullOrWhiteSpace(rawId)) return string.Empty;

        string id = rawId.Trim();

        // Remove namespacing prefixes like "CARD:GoForTheEyes" -> "GoForTheEyes"
        int colonIndex = id.IndexOf(':');
        if (colonIndex >= 0 && colonIndex < id.Length - 1)
        {
            id = id.Substring(colonIndex + 1);
        }

        // Convert CamelCase (e.g. GoForTheEyes -> GO_FOR_THE_EYES, DefendNecrobinder -> DEFEND_NECROBINDER)
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < id.Length; i++)
        {
            char c = id[i];
            if (char.IsUpper(c) && i > 0 && (char.IsLower(id[i - 1]) || (i + 1 < id.Length && char.IsLower(id[i + 1]))))
            {
                sb.Append('_');
            }
            sb.Append(char.ToUpperInvariant(c));
        }

        return sb.ToString();
    }

    private static Color GetColorForTier(string tier) => tier switch
    {
        "S" => new Color(1.0f, 0.35f, 0.35f), // Crimson Red
        "A" => new Color(1.0f, 0.65f, 0.25f), // Bright Amber
        "B" => new Color(0.95f, 0.95f, 0.3f), // Golden Yellow
        "C" => new Color(0.45f, 0.85f, 1.0f), // Sky Blue
        "D" => new Color(0.65f, 0.65f, 0.65f), // Grey
        _ => new Color(0.85f, 0.85f, 0.85f)
    };
}
