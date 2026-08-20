using Godot;
using System;
using SlayTheSpireOverlay.Core.Interfaces;
using SlayTheSpireOverlay.Core.Models;

namespace SlayTheSpireOverlay.Godot.UI;

public partial class TierBadge : Control
{
    private readonly ITierListProvider _tierProvider;
    private readonly string _cardId;

    private Label _tierLabel = null!;
    private Label _scoreLabel = null!;
    private PanelContainer _backgroundPanel = null!;

    public TierBadge(ITierListProvider tierProvider, string cardId)
    {
        _tierProvider = tierProvider;
        _cardId = cardId;
    }

    public override async void _Ready()
    {
        SetupUIComponents();
        await LoadDataAndApplyStyles();
    }

    private void SetupUIComponents()
    {
        _backgroundPanel = new PanelContainer();
        
        var layout = new VBoxContainer();
        _tierLabel = new Label();
        _scoreLabel = new Label();

        layout.AddChild(_tierLabel);
        layout.AddChild(_scoreLabel);
        _backgroundPanel.AddChild(layout);
        AddChild(_backgroundPanel);

        // Styling badge with premium HSL-curated dark mode and glassmorphism styling
        Size = new Vector2(60, 40);
        _backgroundPanel.SelfModulate = new Color(0.12f, 0.12f, 0.16f, 0.9f);
    }

    private string NormalizeCardId(string rawId)
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

    private async System.Threading.Tasks.Task LoadDataAndApplyStyles()
    {
        var tierList = await _tierProvider.GetTierListAsync();
        string lookupKey = NormalizeCardId(_cardId);
        string strippedKey = lookupKey.Replace("_", "");
        
        GD.Print($"[STS2 Overlay] Card ID: {_cardId} (Normalized: {lookupKey}, Stripped: {strippedKey}). Total tierlist cards: {tierList.Count}");

        if (tierList.TryGetValue(lookupKey, out var cardData) || 
            (strippedKey != lookupKey && tierList.TryGetValue(strippedKey, out cardData)))
        {
            _tierLabel.Text = cardData.Tier;
            _scoreLabel.Text = cardData.Score.ToString("0.0");
            _tierLabel.SelfModulate = GetColorForTier(cardData.Tier);
            TooltipText = cardData.Commentary;
        }
        else
        {
            // Fallback: Show N/A for unrated / new cards as requested
            _tierLabel.Text = "N/A";
            _scoreLabel.Text = "";
            _tierLabel.SelfModulate = new Color(0.6f, 0.6f, 0.6f); // Grey color for N/A
            TooltipText = "No evaluation data found for this card.";
        }
    }

    private Color GetColorForTier(string tier) => tier switch
    {
        "S" => new Color(1.0f, 0.3f, 0.3f), // Vibrant Soft Red
        "A" => new Color(1.0f, 0.6f, 0.2f), // Bright Orange
        "B" => new Color(0.9f, 0.9f, 0.2f), // Golden Yellow
        "C" => new Color(0.4f, 0.8f, 1.0f), // Sky Blue
        "D" => new Color(0.6f, 0.6f, 0.6f), // Muted Grey
        _ => new Color(0.8f, 0.8f, 0.8f)
    };
}
