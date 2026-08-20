using Godot;
using System;
using SlayTheSpireOverlay.Core.Interfaces;
using SlayTheSpireOverlay.Core.Models;

namespace SlayTheSpireOverlay.Godot.UI;

public static class TierBadgeFactory
{
    public static void CreateAndAttach(Node cardNode, ITierListProvider tierProvider, string cardId)
    {
        // Use built-in Godot PanelContainer directly (no custom C# script class inheritance)
        var panel = new PanelContainer();
        panel.Name = "TierBadgePanel";

        var layout = new VBoxContainer();
        var tierLabel = new Label();
        var scoreLabel = new Label();

        tierLabel.HorizontalAlignment = HorizontalAlignment.Center;
        scoreLabel.HorizontalAlignment = HorizontalAlignment.Center;

        layout.AddChild(tierLabel);
        layout.AddChild(scoreLabel);
        panel.AddChild(layout);

        // Styling badge with dark mode glassmorphism
        panel.Size = new Vector2(60, 40);
        panel.Position = new Vector2(10, 10);
        panel.SelfModulate = new Color(0.12f, 0.12f, 0.16f, 0.9f);

        // Set initial placeholder text
        tierLabel.Text = "...";
        scoreLabel.Text = "";

        // Attach panel directly to card node
        cardNode.AddChild(panel);

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
                            tierLabel.Text = cardData.Tier;
                            scoreLabel.Text = cardData.Score.ToString("0.0");
                            tierLabel.SelfModulate = GetColorForTier(cardData.Tier);
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
                Callable.From(() =>
                {
                    if (GodotObject.IsInstanceValid(panel) && GodotObject.IsInstanceValid(tierLabel) && GodotObject.IsInstanceValid(scoreLabel))
                    {
                        tierLabel.Text = "N/A";
                        scoreLabel.Text = "";
                        tierLabel.SelfModulate = new Color(0.6f, 0.6f, 0.6f);
                        panel.TooltipText = "Error loading evaluation data.";
                    }
                }).CallDeferred();
            }
        });
    }

    private static string NormalizeCardId(string rawId)
    {
        if (string.IsNullOrWhiteSpace(rawId)) return string.Empty;

        string id = rawId.Trim();

        // Remove namespacing prefixes like "base:Bash" -> "Bash"
        int colonIndex = id.IndexOf(':');
        if (colonIndex >= 0 && colonIndex < id.Length - 1)
        {
            id = id.Substring(colonIndex + 1);
        }

        return id.ToUpperInvariant();
    }

    private static Color GetColorForTier(string tier) => tier switch
    {
        "S" => new Color(1.0f, 0.3f, 0.3f), // Soft Red
        "A" => new Color(1.0f, 0.6f, 0.2f), // Orange
        "B" => new Color(0.9f, 0.9f, 0.2f), // Golden Yellow
        "C" => new Color(0.4f, 0.8f, 1.0f), // Sky Blue
        "D" => new Color(0.6f, 0.6f, 0.6f), // Grey
        _ => new Color(0.8f, 0.8f, 0.8f)
    };
}
