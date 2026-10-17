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

        try
        {
            var harmony = new Harmony("SlayTheSpireMapFog.Godot");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            PatchPostfixIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen", new[] { "SetMap", "RecalculateTravelability", "RefreshAllPointVisuals" }, nameof(OnMapScreenUpdated));
            PatchPostfixIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.Map.NNormalMapPoint", new[] { "UpdateIcon", "RefreshMarkedIconVisibility" }, nameof(OnNormalMapPointIconUpdated));
            PatchPrefixIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.Map.NNormalMapPoint", "OnHighlightPointType", nameof(OnNormalMapPointHighlightPrefix));
            PatchPostfixIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModInfoContainer", new[] { "Fill" }, nameof(OnModInfoFill));
            PatchPostfixIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModInfoContainer", new[] { "Clear" }, nameof(OnModInfoClear));
            PatchPostfixIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.Settings.NSettingsScreen", new[] { "_Ready" }, nameof(OnSettingsScreenReady));
            PatchRunSubmenuStackIfPresent(harmony);
            GD.Print("[STS2 MapFog] Harmony patches applied successfully!");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 MapFog] Error applying Harmony patches: {ex.Message}");
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

    private static void PatchPostfixIfPresent(Harmony harmony, string typeName, string[] methods, string postfixName)
    {
        var type = AccessTools.TypeByName(typeName);
        var postfix = AccessTools.Method(typeof(ModEntry), postfixName);
        if (type == null || postfix == null) return;

        foreach (string name in methods)
        {
            var method = AccessTools.Method(type, name);
            if (method != null) harmony.Patch(method, postfix: new HarmonyMethod(postfix));
        }
    }

    private static void PatchPrefixIfPresent(Harmony harmony, string typeName, string methodName, string prefixName)
    {
        var type = AccessTools.TypeByName(typeName);
        var method = type != null ? AccessTools.Method(type, methodName) : null;
        var prefix = AccessTools.Method(typeof(ModEntry), prefixName);
        if (method != null && prefix != null)
        {
            harmony.Patch(method, prefix: new HarmonyMethod(prefix));
        }
    }

    private static void PatchRunSubmenuStackIfPresent(Harmony harmony)
    {
        var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.NRunSubmenuStack");
        var method = type != null ? AccessTools.Method(type, "GetSubmenuType", new[] { typeof(Type) }) : null;
        var prefix = AccessTools.Method(typeof(ModEntry), nameof(OnRunSubmenuStackGetSubmenuType));
        if (method != null && prefix != null)
        {
            harmony.Patch(method, prefix: new HarmonyMethod(prefix));
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

    public static void OnModInfoClear(object __instance)
    {
        if (__instance is Node container && GodotObject.IsInstanceValid(container))
        {
            UI.MapFogSettingsFactory.HideModSettingsPanel(container);
        }
    }
}
