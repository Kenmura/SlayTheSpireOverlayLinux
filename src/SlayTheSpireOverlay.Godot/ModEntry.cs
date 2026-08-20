using Godot;
using System;
using System.Net.Http;
using HarmonyLib;
using SlayTheSpireOverlay.Core.Interfaces;
using SlayTheSpireOverlay.Core.Services;
using SlayTheSpireOverlay.Core.Options;

namespace SlayTheSpireOverlay.Godot;

[MegaCrit.Sts2.Core.Modding.ModInitializer("Initialize")]
public static class ModEntry
{
    public static ITierListProvider TierProvider { get; private set; } = null!;

    public static void Initialize()
    {
        GD.Print("[STS2 Overlay] ModEntry.Initialize called!");

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
        }

        // 3. Always ensure cache on disk is seeded or updated with latest embedded resource
        string cachePath = System.IO.Path.Combine(godotUserDir, "tier_list_cache.json");
        try
        {
            var assembly = typeof(ModEntry).Assembly;
            using var stream = assembly.GetManifestResourceStream("SlayTheSpireOverlay.Godot.baalorlord_tiers.json");
            if (stream != null)
            {
                using var reader = new System.IO.StreamReader(stream);
                string embeddedJson = reader.ReadToEnd();
                System.IO.Directory.CreateDirectory(godotUserDir);

                bool shouldOverwrite = !System.IO.File.Exists(cachePath);
                if (!shouldOverwrite)
                {
                    try
                    {
                        var diskJson = System.IO.File.ReadAllText(cachePath);
                        shouldOverwrite = embeddedJson.Length > diskJson.Length;
                    }
                    catch
                    {
                        shouldOverwrite = true;
                    }
                }

                if (shouldOverwrite)
                {
                    System.IO.File.WriteAllText(cachePath, embeddedJson);
                    GD.Print("[STS2 Overlay] Updated tier_list_cache.json from embedded resource (802 items).");
                }
            }
            else
            {
                GD.PrintErr("[STS2 Overlay] Embedded resource stream for baalorlord_tiers.json was null!");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Overlay] Error seeding cache from embedded resource: {ex.Message}");
        }

        var cacheOptions = new CacheOptions
        {
            CacheDirectory = godotUserDir,
            CacheExpiryHours = config.CacheExpiryHours
        };

        // 4. Instantiate core services directly
        var cacheManager = new LocalCacheManager(cacheOptions);
        var httpClient = new System.Net.Http.HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(3);
        TierProvider = new HttpTierListProvider(httpClient, cacheManager, config);

        // Trigger cache load off-thread
        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                await TierProvider.GetTierListAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[STS2 Overlay] Error prefetching tierlist on start: {ex.Message}");
            }
        });

        // 5. Apply Harmony Patches
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
        UpdateCardBadge(__instance);
    }

    public static void UpdateCardBadge(MegaCrit.Sts2.Core.Nodes.Cards.NCard cardNode)
    {
        try
        {
            if (cardNode == null || !GodotObject.IsInstanceValid(cardNode)) return;
            var model = cardNode.Model;
            if (model == null || model.Id == null) return;

            string category = model.Id.Category;
            string entry = model.Id.Entry;
            string cardId = string.IsNullOrEmpty(category) ? entry : $"{category}:{entry}";

            UI.TierBadgeFactory.CreateOrUpdateBadge(cardNode, ModEntry.TierProvider, cardId);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Overlay] Error in NCard patch: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Cards.NCard), "set_Model")]
public static class NCardSetModelPatch
{
    public static void Postfix(MegaCrit.Sts2.Core.Nodes.Cards.NCard __instance)
    {
        NCardReadyPatch.UpdateCardBadge(__instance);
    }
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Relics.NRelic), "_Ready")]
public static class NRelicReadyPatch
{
    public static void Postfix(MegaCrit.Sts2.Core.Nodes.Relics.NRelic __instance)
    {
        UpdateRelicBadge(__instance);
    }

    public static void UpdateRelicBadge(MegaCrit.Sts2.Core.Nodes.Relics.NRelic relicNode)
    {
        try
        {
            if (relicNode == null || !GodotObject.IsInstanceValid(relicNode)) return;
            var model = relicNode.Model;
            if (model == null || model.Id == null) return;

            string category = model.Id.Category;
            string entry = model.Id.Entry;
            string relicId = string.IsNullOrEmpty(category) ? entry : $"{category}:{entry}";

            UI.RelicBadgeFactory.CreateOrUpdateBadge(relicNode, ModEntry.TierProvider, relicId);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Overlay] Error in NRelic patch: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Relics.NRelic), "set_Model")]
public static class NRelicSetModelPatch
{
    public static void Postfix(MegaCrit.Sts2.Core.Nodes.Relics.NRelic __instance)
    {
        NRelicReadyPatch.UpdateRelicBadge(__instance);
    }
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton), "_Ready")]
public static class NEventOptionButtonReadyPatch
{
    public static void Postfix(MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton __instance)
    {
        UpdateOptionBadge(__instance);
    }

    public static void UpdateOptionBadge(MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton buttonNode)
    {
        try
        {
            if (buttonNode == null || !GodotObject.IsInstanceValid(buttonNode)) return;
            var option = buttonNode.Option;
            if (option == null) return;

            string? targetId = null;

            // 1. Check direct Relic property on EventOption
            if (option.Relic != null && option.Relic.Id != null)
            {
                targetId = option.Relic.Id.Entry;
            }

            // 2. Check HoverTips on EventOption
            if (string.IsNullOrEmpty(targetId) && option.HoverTips != null)
            {
                foreach (var tip in option.HoverTips)
                {
                    if (tip is MegaCrit.Sts2.Core.HoverTips.CardHoverTip cardTip && cardTip.Card != null && cardTip.Card.Id != null)
                    {
                        targetId = cardTip.Card.Id.Entry;
                        break;
                    }
                    else if (tip is MegaCrit.Sts2.Core.HoverTips.HoverTip hoverTip && hoverTip.CanonicalModel != null && hoverTip.CanonicalModel.Id != null)
                    {
                        targetId = hoverTip.CanonicalModel.Id.Entry;
                        break;
                    }
                }
            }

            // 3. Fallback to option.TextKey
            if (string.IsNullOrEmpty(targetId) && !string.IsNullOrEmpty(option.TextKey))
            {
                targetId = option.TextKey;
            }

            if (!string.IsNullOrEmpty(targetId))
            {
                UI.RelicBadgeFactory.CreateOrUpdateOptionBadge(buttonNode, ModEntry.TierProvider, targetId);
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Overlay] Error in NEventOptionButton patch: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton), "set_Option")]
public static class NEventOptionButtonSetOptionPatch
{
    public static void Postfix(MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton __instance)
    {
        NEventOptionButtonReadyPatch.UpdateOptionBadge(__instance);
    }
}
