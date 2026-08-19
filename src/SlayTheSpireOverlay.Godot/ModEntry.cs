using Godot;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using SlayTheSpireOverlay.Core.Interfaces;
using SlayTheSpireOverlay.Core.Services;
using SlayTheSpireOverlay.Core.Options;

namespace SlayTheSpireOverlay.Godot;

public partial class ModEntry : Node
{
    private IServiceProvider _serviceProvider = null!;

    public override void _Ready()
    {
        var services = new ServiceCollection();

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

        services.AddSingleton(config);
        services.AddSingleton(cacheOptions);

        // 3. Register core services
        services.AddSingleton<LocalCacheManager>();
        services.AddSingleton<System.Net.Http.HttpClient>();
        services.AddSingleton<ITierListProvider>(sp => new HttpTierListProvider(
            sp.GetRequiredService<System.Net.Http.HttpClient>(),
            sp.GetRequiredService<LocalCacheManager>(),
            sp.GetRequiredService<OverlayConfig>()
        ));

        _serviceProvider = services.BuildServiceProvider();

        // Trigger cache load and background fetch immediately on start
        var provider = _serviceProvider.GetRequiredService<ITierListProvider>();
        _ = provider.GetTierListAsync();

        // Subscribe to game signals
        SubscribeToGameSignals();
    }

    private void SubscribeToGameSignals()
    {
        // Example integration point for game signals:
        // Here you would hook into the Slay the Spire 2 Modding API.
        // e.g. CardLoader.Connect("CardGenerated", new Callable(this, nameof(OnCardGenerated)));
    }

    // This method is called by the mod hook when a card UI node is created
    public void OnCardGenerated(Node cardNode, string cardId)
    {
        var provider = _serviceProvider.GetRequiredService<ITierListProvider>();
        
        // Dynamically instantiate the UI overlay badge
        var badge = new UI.TierBadge(provider, cardId);
        cardNode.AddChild(badge);
    }
}
