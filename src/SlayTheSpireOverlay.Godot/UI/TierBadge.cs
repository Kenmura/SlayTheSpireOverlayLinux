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

    private async System.Threading.Tasks.Task LoadDataAndApplyStyles()
    {
        var tierList = await _tierProvider.GetTierListAsync();
        if (tierList.TryGetValue(_cardId, out var cardData))
        {
            _tierLabel.Text = cardData.Tier;
            _scoreLabel.Text = cardData.Score.ToString("0.0");
            
            // Apply HSL-curated coloring depending on the tier score
            _tierLabel.SelfModulate = GetColorForTier(cardData.Tier);
            
            // Setup tooltip for commentary
            TooltipText = cardData.Commentary;
        }
        else
        {
            // Fallback for unrated / new cards
            _tierLabel.Text = "?";
            _scoreLabel.Text = "N/A";
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
