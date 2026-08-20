using Godot;
using System;
using System.Net.Http;
using HarmonyLib;
using SlayTheSpireOverlay.Core.Interfaces;
using SlayTheSpireOverlay.Core.Services;
using SlayTheSpireOverlay.Core.Options;

namespace SlayTheSpireOverlay.Godot;

[MegaCrit.Sts2.Core.Modding.ModInitializer("Initialize")]
public partial class ModEntry : Node
{
    public static ITierListProvider TierProvider { get; private set; } = null!;

    public static void Initialize()
    {
        GD.Print("[STS2 Overlay] ModInitializer.Initialize called!");

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
        TierProvider = new HttpTierListProvider(httpClient, cacheManager, config);

        // Trigger cache load and background fetch immediately on start
        _ = TierProvider.GetTierListAsync();

        // 4. Apply Harmony Patches
        try
        {
            var harmony = new Harmony("SlayTheSpireOverlay.Godot");
            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            GD.Print("[STS2 Overlay] Harmony patches applied successfully!");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Overlay] Error applying Harmony patches: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Cards.NCard), "_Ready")]
public static class NCardReadyPatch
{
    public static void Postfix(MegaCrit.Sts2.Core.Nodes.Cards.NCard __instance)
    {
        try
        {
            if (__instance == null || !global::Godot.GodotObject.IsInstanceValid(__instance)) return;
            
            var model = __instance.Model;
            if (model == null || model.Id == null) return;
            
            string category = model.Id.Category;
            string entry = model.Id.Entry;
            string cardId = string.IsNullOrEmpty(category) ? entry : $"{category}:{entry}";

            // Prevent duplicate badges on the same card node
            foreach (var child in __instance.GetChildren())
            {
                if (child is UI.TierBadge)
                {
                    return;
                }
            }

            GD.Print($"[STS2 Overlay] Adding tier badge to card node '{__instance.Name}' with ID: {cardId}");
            var badge = new UI.TierBadge(ModEntry.TierProvider, cardId);
            __instance.AddChild(badge);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Overlay] Error in NCard._Ready postfix patch: {ex.Message}");
        }
    }
}
