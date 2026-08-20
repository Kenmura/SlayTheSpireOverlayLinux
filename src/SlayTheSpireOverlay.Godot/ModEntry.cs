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

        // Subscribe to game signals
        SubscribeToGameSignals();
    }

    private void SubscribeToGameSignals()
    {
        // Example integration point for game signals:
        // e.g. CardLoader.Connect("CardGenerated", new Callable(this, nameof(OnCardGenerated)));
    }

    // This method is called by the mod hook when a card UI node is created
    public void OnCardGenerated(Node cardNode, string cardId)
    {
        // Dynamically instantiate the UI overlay badge
        var badge = new UI.TierBadge(_tierProvider, cardId);
        cardNode.AddChild(badge);
    }
}
