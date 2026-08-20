using Godot;
using System;
using System.Net.Http;
using SlayTheSpireOverlay.Core.Interfaces;
using SlayTheSpireOverlay.Core.Services;
using SlayTheSpireOverlay.Core.Options;

namespace SlayTheSpireOverlay.Godot;

public partial class ModEntry : Node
{
    private ITierListProvider _tierProvider = null!;

    public override void _Ready()
    {
        // 1. Resolve Godot's user path
        string godotUserDir = ProjectSettings.GlobalizePath("user://");

        // 2. Load or create user configuration file
        string configPath = System.IO.Path.Combine(godotUserDir, "overlay_config.json");
        OverlayConfig config;
        
        if (System.IO.File.Exists(configPath))
        {
            try
            {
                var json = System.IO.File.ReadAllText(configPath);
                config = System.Text.Json.JsonSerializer.Deserialize<OverlayConfig>(json) ?? new OverlayConfig();
            }
            catch
            {
                config = new OverlayConfig();
            }
        }
        else
        {
            config = new OverlayConfig();
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.Directory.CreateDirectory(godotUserDir);
                System.IO.File.WriteAllText(configPath, json);
            }
            catch { }
        }

        var cacheOptions = new CacheOptions
        {
            CacheDirectory = godotUserDir,
            CacheExpiryHours = config.CacheExpiryHours
        };

        // 3. Instantiate core services directly (zero external DI library dependency)
        var cacheManager = new LocalCacheManager(cacheOptions);
        var httpClient = new System.Net.Http.HttpClient();
        _tierProvider = new HttpTierListProvider(httpClient, cacheManager, config);

        // Trigger cache load and background fetch immediately on start
        _ = _tierProvider.GetTierListAsync();

        // Subscribe to game signals and scene tree modifications
        SubscribeToGameSignals();
    }

    private void SubscribeToGameSignals()
    {
        GetTree().NodeAdded += OnNodeAdded;
        GD.Print("[STS2 Overlay] Subscribed to Godot SceneTree.NodeAdded signal.");
    }

    private void OnNodeAdded(Node node)
    {
        string? cardId = GetCardIdFromNode(node);
        if (cardId != null)
        {
            CallDeferred(nameof(OnCardGenerated), node, cardId);
        }
    }

    private string? GetCardIdFromNode(Node node)
    {
        if (node == null || !IsInstanceValid(node)) return null;
        
        if (node.GetType().FullName != "MegaCrit.Sts2.Core.Nodes.Cards.NCard")
        {
            return null;
        }

        try
        {
            var modelProp = node.GetType().GetProperty("Model", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var model = modelProp?.GetValue(node);
            if (model == null) return null;

            var idProp = model.GetType().GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var id = idProp?.GetValue(model);
            if (id == null) return null;

            var categoryProp = id.GetType().GetProperty("Category", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var entryProp = id.GetType().GetProperty("Entry", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            string? category = categoryProp?.GetValue(id) as string;
            string? entry = entryProp?.GetValue(id) as string;

            if (!string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(entry))
            {
                return $"{category}:{entry}";
            }
            return entry;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Overlay] Error reflecting card ID: {ex.Message}");
            return null;
        }
    }

    // This method is called by the mod hook when a card UI node is created
    public void OnCardGenerated(Node cardNode, string cardId)
    {
        if (cardNode == null || !IsInstanceValid(cardNode)) return;

        // Prevent duplicate badges on the same card node
        foreach (var child in cardNode.GetChildren())
        {
            if (child is UI.TierBadge)
            {
                return;
            }
        }

        GD.Print($"[STS2 Overlay] Adding tier badge to card node '{cardNode.Name}' with ID: {cardId}");
        
        // Dynamically instantiate the UI overlay badge
        var badge = new UI.TierBadge(_tierProvider, cardId);
        cardNode.AddChild(badge);
    }
}
