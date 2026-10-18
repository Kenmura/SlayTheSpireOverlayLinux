using Godot;
using HarmonyLib;
using System;
using System.Reflection;
using SlayTheSpireMapFog.Core.Options;

namespace SlayTheSpireMapFog.Godot;

[MegaCrit.Sts2.Core.Modding.ModInitializer("Initialize")]
public static class ModEntry
{
    private static readonly System.Text.Json.JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    public static MapFogConfig Config { get; private set; } = new();
    private static string _configPath = string.Empty;

    public static void Initialize()
    {
        GD.Print("[STS2 MapFog] ModEntry.Initialize called!");

        string godotUserDir = ProjectSettings.GlobalizePath("user://");
        _configPath = System.IO.Path.Combine(godotUserDir, "map_fog_config.json");

        if (System.IO.File.Exists(_configPath))
        {
            try
            {
                string json = System.IO.File.ReadAllText(_configPath);
                Config = System.Text.Json.JsonSerializer.Deserialize<MapFogConfig>(json) ?? new MapFogConfig();
            }
            catch
            {
                Config = new MapFogConfig();
            }
        }
        else
        {
            SaveConfig();
        }

        int appliedCount = 0;
        int skippedCount = 0;
        try
        {
            var harmony = new Harmony("SlayTheSpireMapFog.Godot");

            foreach (var patchClass in Assembly.GetExecutingAssembly().GetTypes())
            {
                var patchAttrs = patchClass.GetCustomAttributes(typeof(HarmonyPatch), inherit: true);
                if (patchAttrs.Length == 0) continue;
                try
                {
                    var processor = harmony.CreateClassProcessor(patchClass);
                    var patchedMethods = processor.Patch();
                    if (patchedMethods != null && patchedMethods.Count > 0)
                    {
                        appliedCount += patchedMethods.Count;
                    }
                    else
                    {
                        skippedCount++;
                        GD.Print($"[STS2 MapFog] patch class {patchClass.Name} skipped: no matching methods found");
                    }
                }
                catch (Exception ex)
                {
                    skippedCount++;
                    GD.PrintErr($"[STS2 MapFog] patch class {patchClass.Name} skipped: {ex.Message}");
                }
            }

            PatchPostfixIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen", new[] { "SetMap", "RecalculateTravelability", "RefreshAllPointVisuals" }, nameof(OnMapScreenUpdated), ref appliedCount, ref skippedCount);
            PatchPostfixIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.Map.NNormalMapPoint", new[] { "UpdateIcon", "RefreshMarkedIconVisibility" }, nameof(OnNormalMapPointIconUpdated), ref appliedCount, ref skippedCount);
            PatchPrefixIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.Map.NNormalMapPoint", "OnHighlightPointType", nameof(OnNormalMapPointHighlightPrefix), ref appliedCount, ref skippedCount);
            PatchPostfixIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModInfoContainer", new[] { "Fill" }, nameof(OnModInfoFill), ref appliedCount, ref skippedCount);
            PatchPostfixIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.Settings.NSettingsScreen", new[] { "_Ready" }, nameof(OnSettingsScreenReady), ref appliedCount, ref skippedCount);
            PatchRunSubmenuStackIfPresent(harmony, ref appliedCount, ref skippedCount);

            GD.Print($"[STS2 MapFog] Harmony initialization complete: {appliedCount} applied, {skippedCount} skipped");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 MapFog] Unexpected error initializing Harmony: {ex.Message}");
        }
    }

    private static void SaveConfig()
    {
        try
        {
            if (string.IsNullOrEmpty(_configPath)) return;
            string? dir = System.IO.Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
            string tmpPath = _configPath + ".tmp";
            System.IO.File.WriteAllText(tmpPath, System.Text.Json.JsonSerializer.Serialize(Config, IndentedJsonOptions));
            System.IO.File.Move(tmpPath, _configPath, overwrite: true);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 MapFog] Error saving map_fog_config.json: {ex.Message}");
        }
    }

    private static void PatchPostfixIfPresent(Harmony harmony, string typeName, string[] methods, string postfixName, ref int applied, ref int skipped)
    {
        var type = AccessTools.TypeByName(typeName);
        if (type == null)
        {
            skipped += methods.Length;
            GD.Print($"[STS2 MapFog] patch {typeName} skipped: target type not found");
            return;
        }

        var postfix = AccessTools.Method(typeof(ModEntry), postfixName);
        if (postfix == null)
        {
            skipped += methods.Length;
            GD.PrintErr($"[STS2 MapFog] patch handler {postfixName} skipped: handler method not found on ModEntry");
            return;
        }

        foreach (string name in methods)
        {
            try
            {
                var method = AccessTools.Method(type, name);
                if (method != null)
                {
                    harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                    applied++;
                }
                else
                {
                    skipped++;
                    GD.Print($"[STS2 MapFog] patch {typeName}.{name} skipped: method not found on {typeName}");
                }
            }
            catch (Exception ex)
            {
                skipped++;
                GD.PrintErr($"[STS2 MapFog] patch {typeName}.{name} skipped: {ex.Message}");
            }
        }
    }

    private static void PatchPrefixIfPresent(Harmony harmony, string typeName, string methodName, string prefixName, ref int applied, ref int skipped)
    {
        try
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                skipped++;
                GD.Print($"[STS2 MapFog] patch {typeName} skipped: target type not found");
                return;
            }

            var method = AccessTools.Method(type, methodName);
            var prefix = AccessTools.Method(typeof(ModEntry), prefixName);
            if (method != null && prefix != null)
            {
                harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                applied++;
            }
            else
            {
                skipped++;
                GD.Print($"[STS2 MapFog] patch {typeName}.{methodName} skipped: method or prefix not found");
            }
        }
        catch (Exception ex)
        {
            skipped++;
            GD.PrintErr($"[STS2 MapFog] patch {typeName}.{methodName} skipped: {ex.Message}");
        }
    }

    private static void PatchRunSubmenuStackIfPresent(Harmony harmony, ref int applied, ref int skipped)
    {
        try
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.NRunSubmenuStack");
            if (type == null)
            {
                skipped++;
                GD.Print("[STS2 MapFog] patch NRunSubmenuStack skipped: target type not found");
                return;
            }

            var method = AccessTools.Method(type, "GetSubmenuType", new[] { typeof(Type) });
            var prefix = AccessTools.Method(typeof(ModEntry), nameof(OnRunSubmenuStackGetSubmenuType));
            if (method != null && prefix != null)
            {
                harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                applied++;
            }
            else
            {
                skipped++;
                GD.Print("[STS2 MapFog] patch NRunSubmenuStack.GetSubmenuType skipped: method or prefix not found");
            }
        }
        catch (Exception ex)
        {
            skipped++;
            GD.PrintErr($"[STS2 MapFog] patch NRunSubmenuStack.GetSubmenuType skipped: {ex.Message}");
        }
    }

    public static void OnMapScreenUpdated(object __instance)
    {
        Sts2MapInspector.ApplyMapFog(__instance, Config);
    }

    public static void OnNormalMapPointIconUpdated(object __instance)
    {
        Sts2MapInspector.ApplyMaskToNormalPoint(__instance);
    }

    public static bool OnNormalMapPointHighlightPrefix(object __instance)
    {
        return !Sts2MapInspector.IsPointFogHidden(__instance);
    }

    public static void OnSettingsScreenReady(object __instance)
    {
        Sts2MapInspector.EnableModdingButtonInRun(__instance);
    }

    public static bool OnRunSubmenuStackGetSubmenuType(object __instance, Type type, ref object? __result)
    {
        if (type?.FullName == "MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModdingScreen")
        {
            var screen = Sts2MapInspector.GetOrCreateRunModdingScreen(__instance);
            if (screen != null)
            {
                __result = screen;
                return false;
            }
        }
        return true;
    }

    public static void OnModInfoFill(object __instance, object? mod)
    {
        if (__instance is not Node container || !GodotObject.IsInstanceValid(container)) return;
        if (Sts2MapInspector.IsOurMod(mod))
        {
            UI.MapFogSettingsFactory.AttachOrUpdateModSettingsPanel(
                container,
                Config,
                () =>
                {
                    SaveConfig();
                    Sts2MapInspector.RefreshActiveMapInScene(container, Config);
                });
        }
        else
        {
            UI.MapFogSettingsFactory.HideModSettingsPanel(container);
        }
    }
}
