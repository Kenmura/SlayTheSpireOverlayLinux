using Godot;
using System;
using SlayTheSpireOverlay.Core.Interfaces;
using SlayTheSpireOverlay.Core.Models;

namespace SlayTheSpireOverlay.Godot.UI;

public static class RelicBadgeFactory
{
    public static void CreateOrUpdateBadge(Node relicNode, ITierListProvider tierProvider, string relicId)
    {
        if (relicNode == null || !GodotObject.IsInstanceValid(relicNode)) return;

        PanelContainer panel = relicNode.GetNodeOrNull<PanelContainer>("RelicBadgePanel");
        Label tierLabel;
        Label dividerLabel;
        Label scoreLabel;

        if (panel == null || !GodotObject.IsInstanceValid(panel))
        {
            panel = new PanelContainer();
            panel.Name = "RelicBadgePanel";

            var styleBox = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.09f, 0.12f, 0.92f),
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(1.0f, 1.0f, 1.0f, 0.2f),
                ContentMarginLeft = 5,
                ContentMarginTop = 2,
                ContentMarginRight = 5,
                ContentMarginBottom = 2
            };
            panel.AddThemeStyleboxOverride("panel", styleBox);

            var layout = new HBoxContainer();
            layout.Name = "BadgeLayout";
            layout.AddThemeConstantOverride("separation", 4);

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

            // Position pill at top-right corner of the relic icon
            panel.Position = new Vector2(22, -8);

            tierLabel.Text = "...";
            scoreLabel.Text = "";

            relicNode.AddChild(panel);
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
                string lookupKey = NormalizeRelicId(relicId);
                string strippedKey = lookupKey.Replace("_", "");

                if (tierList.TryGetValue(lookupKey, out var relicData) ||
                    (strippedKey != lookupKey && tierList.TryGetValue(strippedKey, out relicData)))
                {
                    Callable.From(() =>
                    {
                        if (GodotObject.IsInstanceValid(panel) && GodotObject.IsInstanceValid(tierLabel) && GodotObject.IsInstanceValid(scoreLabel))
                        {
                            tierLabel.Text = relicData.Tier;
                            tierLabel.SelfModulate = GetColorForTier(relicData.Tier);
                            panel.TooltipText = relicData.Commentary ?? "";

                            if (relicData.Score > 0)
                            {
                                dividerLabel.Visible = true;
                                scoreLabel.Text = relicData.Score.ToString("0.0");
                                scoreLabel.SelfModulate = new Color(0.95f, 0.95f, 0.98f);
                            }
                            else
                            {
                                dividerLabel.Visible = false;
                                scoreLabel.Text = "";
                            }
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
                            panel.TooltipText = "No evaluation data found for this relic.";
                        }
                    }).CallDeferred();
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[STS2 Overlay] Error loading relic rating data: {ex.Message}");
            }
        });
    }

    public static void CreateOrUpdateOptionBadge(Node buttonNode, ITierListProvider tierProvider, string relicId)
    {
        if (buttonNode == null || !GodotObject.IsInstanceValid(buttonNode)) return;

        PanelContainer panel = buttonNode.GetNodeOrNull<PanelContainer>("RelicOptionBadgePanel");
        Label tierLabel;
        Label dividerLabel;
        Label scoreLabel;

        if (panel == null || !GodotObject.IsInstanceValid(panel))
        {
            panel = new PanelContainer();
            panel.Name = "RelicOptionBadgePanel";

            var styleBox = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.09f, 0.12f, 0.92f),
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(1.0f, 1.0f, 1.0f, 0.2f),
                ContentMarginLeft = 6,
                ContentMarginTop = 2,
                ContentMarginRight = 6,
                ContentMarginBottom = 2
            };
            panel.AddThemeStyleboxOverride("panel", styleBox);

            var layout = new HBoxContainer();
            layout.Name = "BadgeLayout";
            layout.AddThemeConstantOverride("separation", 4);

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

            // Position pill at right side of the option button
            panel.Position = new Vector2(350, 6);

            tierLabel.Text = "...";
            scoreLabel.Text = "";

            buttonNode.AddChild(panel);
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
                string lookupKey = NormalizeRelicId(relicId);
                string strippedKey = lookupKey.Replace("_", "");

                if (tierList.TryGetValue(lookupKey, out var relicData) ||
                    (strippedKey != lookupKey && tierList.TryGetValue(strippedKey, out relicData)))
                {
                    Callable.From(() =>
                    {
                        if (GodotObject.IsInstanceValid(panel) && GodotObject.IsInstanceValid(tierLabel) && GodotObject.IsInstanceValid(scoreLabel))
                        {
                            tierLabel.Text = relicData.Tier;
                            tierLabel.SelfModulate = GetColorForTier(relicData.Tier);
                            panel.TooltipText = relicData.Commentary ?? "";

                            if (relicData.Score > 0)
                            {
                                dividerLabel.Visible = true;
                                scoreLabel.Text = relicData.Score.ToString("0.0");
                                scoreLabel.SelfModulate = new Color(0.95f, 0.95f, 0.98f);
                            }
                            else
                            {
                                dividerLabel.Visible = false;
                                scoreLabel.Text = "";
                            }
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
                            panel.TooltipText = "No evaluation data found for this relic.";
                        }
                    }).CallDeferred();
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[STS2 Overlay] Error loading relic rating data: {ex.Message}");
            }
        });
    }

    private static string NormalizeRelicId(string rawId)
    {
        if (string.IsNullOrWhiteSpace(rawId)) return string.Empty;

        string id = rawId.Trim();

        // Remove namespacing prefixes like "RELIC:Akabeko" -> "Akabeko"
        int colonIndex = id.IndexOf(':');
        if (colonIndex >= 0 && colonIndex < id.Length - 1)
        {
            id = id.Substring(colonIndex + 1);
        }

        // Convert CamelCase (e.g. LavaRockOption -> LAVA_ROCK_OPTION)
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

        string result = sb.ToString();

        // Strip _OPTION suffix if option button node passes Option variant name
        if (result.EndsWith("_OPTION"))
        {
            result = result.Substring(0, result.Length - 7);
        }

        return result;
    }

    private static Color GetColorForTier(string tier) => tier switch
    {
        "S" => new Color(1.0f, 0.35f, 0.35f), // Crimson Red
        "A" => new Color(1.0f, 0.65f, 0.25f), // Bright Amber
        "B" => new Color(0.95f, 0.95f, 0.3f), // Golden Yellow
        "C" => new Color(0.45f, 0.85f, 1.0f), // Sky Blue
        "D" => new Color(0.65f, 0.65f, 0.65f), // Grey
        "Map Dependent" => new Color(1.0f, 0.7f, 0.3f), // Warm Amber
        "Inconsistent" => new Color(0.85f, 0.5f, 1.0f), // Light Purple
        _ => new Color(0.85f, 0.85f, 0.85f)
    };
}
