using Godot;
using System;
using System.Reflection;
using HarmonyLib;
using SlayTheSpireOverlay.Core.Interfaces;
using SlayTheSpireOverlay.Core.Services;
using SlayTheSpireOverlay.Core.Options;

namespace SlayTheSpireOverlay.Godot;

[MegaCrit.Sts2.Core.Modding.ModInitializer("Initialize")]
public static class ModEntry
{
    private static readonly System.Text.Json.JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    public static ITierListProvider TierProvider { get; private set; } = null!;
    public static OverlayConfig Config { get; private set; } = new();
    private static string _configPath = string.Empty;

    public static void Initialize()
    {
        GD.Print("[STS2 Overlay] ModEntry.Initialize called!");

        string godotUserDir = ProjectSettings.GlobalizePath("user://");
        _configPath = System.IO.Path.Combine(godotUserDir, "overlay_config.json");

        if (System.IO.File.Exists(_configPath))
        {
            try
            {
                var json = System.IO.File.ReadAllText(_configPath);
                Config = System.Text.Json.JsonSerializer.Deserialize<OverlayConfig>(json) ?? new OverlayConfig();
            }
            catch
            {
                Config = new OverlayConfig();
            }
        }
        else
        {
            SaveConfig();
        }
        UI.RoomOverlayFactory.IsOverlayHidden = !Config.EnableOverlay;

        string cachePath = System.IO.Path.Combine(godotUserDir, "tier_list_cache.json");
        try
        {
            using var stream = typeof(ModEntry).Assembly.GetManifestResourceStream("SlayTheSpireOverlay.Godot.baalorlord_tiers.json");
            if (stream != null)
            {
                bool shouldSeed = !System.IO.File.Exists(cachePath);
                if (!shouldSeed)
                {
                    try
                    {
                        shouldSeed = new System.IO.FileInfo(cachePath).Length < 64;
                    }
                    catch
                    {
                        shouldSeed = true;
                    }
                }

                if (shouldSeed)
                {
                    using var reader = new System.IO.StreamReader(stream);
                    string embeddedJson = reader.ReadToEnd();
                    System.IO.Directory.CreateDirectory(godotUserDir);
                    string tmpCache = cachePath + ".tmp";
                    System.IO.File.WriteAllText(tmpCache, embeddedJson);
                    System.IO.File.Move(tmpCache, cachePath, overwrite: true);
                }
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Overlay] Error seeding cache: {ex.Message}");
        }

        var cacheManager = new LocalCacheManager(new CacheOptions
        {
            CacheDirectory = godotUserDir,
            CacheExpiryHours = Config.CacheExpiryHours
        });
        var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        TierProvider = new HttpTierListProvider(httpClient, cacheManager, Config);

        System.Threading.Tasks.Task.Run(async () =>
        {
            try { await TierProvider.GetTierListAsync().ConfigureAwait(false); }
            catch { }
        });

        try
        {
            var harmony = new Harmony("SlayTheSpireOverlay.Godot");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Cards.NCard", new[] { "UpdateVisuals" }, nameof(OnCardVisualsUpdated));
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Relics.NRelic", new[] { "Reload" }, nameof(OnRelicReloaded));
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Potions.NPotion", new[] { "_Ready", "set_Model", "Reload" }, nameof(OnPotionReady));
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Rewards.NRewardButton", new[] { "_Ready", "Reload", "SetReward" }, nameof(OnRewardButtonReady));
            PatchHolderFocusIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Cards.Holders.NCardHolder");
            PatchHolderFocusIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantSlot");
            PatchHolderFocusIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton");
            PatchHolderFocusIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Relics.NRelicBasicHolder");
            PatchHolderFocusIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic.NTreasureRoomRelicHolder");
            PatchHolderFocusIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Rewards.NRewardButton");
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom", new[] { "_Ready" }, nameof(OnCombatRoomReady));
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom", new[] { "_Ready" }, nameof(OnMerchantRoomReady));
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom", new[] { "_Ready" }, nameof(OnEventRoomReady));
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Rooms.NTreasureRoom", new[] { "_Ready" }, nameof(OnGenericRoomReady));
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Rooms.NRestSiteRoom", new[] { "_Ready" }, nameof(OnGenericRoomReady));
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Rooms.NMapRoom", new[] { "_Ready" }, nameof(OnGenericRoomReady));
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.NRun", new[] { "_Ready" }, nameof(OnGenericRoomReady));
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.NGame", new[] { "_Ready" }, nameof(OnGenericRoomReady));
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModInfoContainer", new[] { "Fill" }, nameof(OnModInfoFill));
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModInfoContainer", new[] { "Clear" }, nameof(OnModInfoClear));
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.Settings.NSettingsScreen", new[] { "_Ready" }, nameof(OnSettingsScreenReady));
            PatchRunSubmenuStackIfPresent(harmony);
            GD.Print("[STS2 Overlay] Harmony patches applied successfully!");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Overlay] Error applying Harmony patches: {ex.Message}");
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
            GD.PrintErr($"[STS2 Overlay] Error saving overlay_config.json: {ex.Message}");
        }
    }

    public static void ToggleAuthor(Node? contextNode)
    {
        Config.Author = string.Equals(Config.Author?.Trim(), "Jorbs", StringComparison.OrdinalIgnoreCase)
            ? "Baalorlord"
            : "Jorbs";
        SaveConfig();

        var root = contextNode != null && GodotObject.IsInstanceValid(contextNode)
            ? contextNode.GetTree()?.CurrentScene
            : null;
        UI.RoomOverlayFactory.ShowAuthorToast(root, Config.Author);

        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                await TierProvider.GetTierListAsync().ConfigureAwait(false);
                Callable.From(() => RefreshSceneFromContext(contextNode)).CallDeferred();
            }
            catch
            {
            }
        });
    }

    public static void ForceRefreshTiers(Node? contextNode)
    {
        string author = Config.Author ?? "Baalorlord";
        var root = contextNode != null && GodotObject.IsInstanceValid(contextNode)
            ? contextNode.GetTree()?.CurrentScene
            : null;
        UI.RoomOverlayFactory.ShowAuthorToast(root, $"Refreshing {author}... [F9]");

        System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                await TierProvider.GetTierListAsync(forceRefresh: true).ConfigureAwait(false);
                Callable.From(() =>
                {
                    RefreshSceneFromContext(contextNode);
                    var currentRoot = contextNode != null && GodotObject.IsInstanceValid(contextNode)
                        ? contextNode.GetTree()?.CurrentScene
                        : null;
                    UI.RoomOverlayFactory.ShowAuthorToast(currentRoot, $"Refreshed {author} [F9]");
                }).CallDeferred();
            }
            catch
            {
            }
        });
    }

    private static void ToggleVisibility(Node? contextNode)
    {
        Config.EnableOverlay = !UI.RoomOverlayFactory.IsOverlayHidden;
        SaveConfig();
        RefreshSceneFromContext(contextNode);
        var root = contextNode != null && GodotObject.IsInstanceValid(contextNode)
            ? contextNode.GetTree()?.CurrentScene
            : null;
        string status = UI.RoomOverlayFactory.IsOverlayHidden ? "Overlay Hidden [F8]" : "Overlay Visible [F8]";
        UI.RoomOverlayFactory.ShowAuthorToast(root, status, forceVisible: true);
    }

    private static void RefreshSceneFromContext(Node? contextNode)
    {
        var root = contextNode != null && GodotObject.IsInstanceValid(contextNode)
            ? contextNode.GetTree()?.CurrentScene
            : null;
        if (root != null && GodotObject.IsInstanceValid(root))
        {
            RefreshSceneBadges(root);
        }
    }

    private static void RefreshSceneBadges(Node node)
    {
        if (!GodotObject.IsInstanceValid(node)) return;
        if (node is MegaCrit.Sts2.Core.Nodes.Cards.NCard card)
        {
            NCardReadyPatch.UpdateCardBadge(card);
        }
        else if (node is MegaCrit.Sts2.Core.Nodes.Relics.NRelic relic)
        {
            NRelicReadyPatch.UpdateRelicBadge(relic);
        }
        else if (node is MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton optBtn)
        {
            NEventOptionButtonReadyPatch.UpdateOptionBadge(optBtn);
        }
        else if (node.GetType().Name == "NPotion")
        {
            OnPotionReady(node);
        }
        else if (node.GetType().Name == "NRewardButton")
        {
            OnRewardButtonReady(node);
        }
        else if (node.GetType().Name == "NEventRoom")
        {
            var evPanel = node.GetNodeOrNull<Control>("EventCommentaryPanel");
            if (evPanel != null && GodotObject.IsInstanceValid(evPanel))
            {
                evPanel.Visible = !UI.RoomOverlayFactory.IsOverlayHidden;
            }
            string? eventId = Sts2RuntimeInspector.InspectEventId(node);
            UI.RoomOverlayFactory.AttachEventCommentary(node, TierProvider, eventId, Config.EnableEventCommentary, CurrentAuthor);
        }

        foreach (Node child in node.GetChildren())
        {
            RefreshSceneBadges(child);
        }
    }

    public static string CurrentAuthor => Config.Author ?? "Baalorlord";

    public static string FormatModelId(MegaCrit.Sts2.Core.Models.ModelId? id) =>
        id == null
            ? string.Empty
            : (string.IsNullOrEmpty(id.Category) ? id.Entry : $"{id.Category}:{id.Entry}");

    private static void PatchIfPresent(Harmony harmony, string typeName, string[] methods, string postfixName)
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

    private static void PatchHolderFocusIfPresent(Harmony harmony, string typeName)
    {
        PatchIfPresent(harmony, typeName, new[] { "OnFocus" }, nameof(OnItemHolderFocused));
        PatchIfPresent(harmony, typeName, new[] { "OnUnfocus" }, nameof(OnItemHolderUnfocused));
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

    public static void OnSettingsScreenReady(object __instance)
    {
        Sts2RuntimeInspector.EnableModdingButtonInRun(__instance);
    }

    public static bool OnRunSubmenuStackGetSubmenuType(object __instance, Type type, ref object? __result)
    {
        if (type?.FullName == "MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModdingScreen")
        {
            var screen = Sts2RuntimeInspector.GetOrCreateRunModdingScreen(__instance);
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
        if (Sts2RuntimeInspector.IsOurMod(mod))
        {
            UI.RoomOverlayFactory.AttachOrUpdateModSettingsPanel(
                container,
                Config,
                () =>
                {
                    SaveConfig();
                    RefreshSceneFromContext(container);
                },
                () => ToggleAuthor(container),
                () => ForceRefreshTiers(container));
        }
        else
        {
            UI.RoomOverlayFactory.HideModSettingsPanel(container);
        }
    }

    public static void OnModInfoClear(object __instance)
    {
        if (__instance is Node container && GodotObject.IsInstanceValid(container))
        {
            UI.RoomOverlayFactory.HideModSettingsPanel(container);
        }
    }

    public static void OnCardVisualsUpdated(object __instance)
    {
        if (__instance is MegaCrit.Sts2.Core.Nodes.Cards.NCard card)
        {
            NCardReadyPatch.UpdateCardBadge(card);
        }
    }

    public static void OnRelicReloaded(object __instance)
    {
        if (__instance is MegaCrit.Sts2.Core.Nodes.Relics.NRelic relic)
        {
            NRelicReadyPatch.UpdateRelicBadge(relic);
        }
    }

    public static void OnRewardButtonReady(object __instance)
    {
        if (__instance is not Node node || !GodotObject.IsInstanceValid(node)) return;
        string? itemId = Sts2RuntimeInspector.ExtractRewardItemId(__instance, out bool isCard, out bool isRelic, out bool isPotion);
        bool allowed = (isCard && Config.EnableCardBadges) ||
                       (isRelic && Config.EnableRelicBadges) ||
                       (isPotion && Config.EnablePotionBadges);
        if (!allowed || string.IsNullOrEmpty(itemId))
        {
            UI.TierBadgeFactory.HideBadge(node, "RelicOptionBadgePanel");
            return;
        }

        UI.RelicBadgeFactory.CreateOrUpdateOptionBadge(node, TierProvider, itemId, CurrentAuthor, Config.EnableHoverCommentary);
    }

    public static void OnItemHolderFocused(object __instance)
    {
        if (__instance is Node node && GodotObject.IsInstanceValid(node))
        {
            UI.RoomOverlayFactory.SetHoverCommentaryVisibleRecursive(node, true);
        }
    }

    public static void OnItemHolderUnfocused(object __instance)
    {
        if (__instance is Node node && GodotObject.IsInstanceValid(node))
        {
            UI.RoomOverlayFactory.SetHoverCommentaryVisibleRecursive(node, false);
        }
    }

    public static void OnPotionReady(object __instance)
    {
        if (__instance is not Node node || !GodotObject.IsInstanceValid(node)) return;
        if (!Config.EnablePotionBadges || Sts2RuntimeInspector.IsItemInTopBar(node))
        {
            UI.TierBadgeFactory.HideBadge(node, "PotionBadgePanel");
            return;
        }
        string? id = Sts2RuntimeInspector.ExtractNodeModelEntryId(__instance);
        if (!string.IsNullOrEmpty(id))
        {
            UI.RelicBadgeFactory.CreateOrUpdatePotionBadge(node, TierProvider, id, CurrentAuthor, Sts2RuntimeInspector.IsItemInTopBar, Config.EnableHoverCommentary);
        }
        else
        {
            UI.TierBadgeFactory.HideBadge(node, "PotionBadgePanel");
        }
    }

    public static void OnCombatRoomReady(object __instance)
    {
        if (__instance is not Node room || !GodotObject.IsInstanceValid(room)) return;
        AttachRoomPollTimer(room, "OverlayCombatTimer", 0.15, () =>
        {
            UI.RoomOverlayFactory.PollHotkeys(() => ToggleAuthor(room), () => ToggleVisibility(room), () => ForceRefreshTiers(room));
            bool visible = !UI.RoomOverlayFactory.IsOverlayHidden && !Sts2RuntimeInspector.IsCombatHudObscured(room);
            bool showAttack = visible && Config.EnableAttackSummary;
            bool showTracker = visible && Config.EnableDeckTracker;
            UI.RoomOverlayFactory.UpdateAttackSummary(room, showAttack ? Sts2RuntimeInspector.InspectCombatAttacks(room) : new AttackSummary(0, 0, 0), showAttack);
            UI.RoomOverlayFactory.UpdateDeckTracker(room, showTracker ? Sts2RuntimeInspector.InspectDeckTracker() : null, showTracker, CurrentAuthor);
        });
    }

    public static void OnMerchantRoomReady(object __instance)
    {
        if (__instance is not Node room || !GodotObject.IsInstanceValid(room)) return;
        AttachRoomPollTimer(room, "OverlayMerchantTimer", 0.10, () =>
        {
            UI.RoomOverlayFactory.PollHotkeys(() => ToggleAuthor(room), () => ToggleVisibility(room), () => ForceRefreshTiers(room));
            bool showGold = !UI.RoomOverlayFactory.IsOverlayHidden && Config.EnableMerchantGoldLeft;
            UI.RoomOverlayFactory.UpdateMerchantGoldLeft(room, showGold ? Sts2RuntimeInspector.InspectMerchantGoldLeft(room) : null, showGold);
        });
    }

    public static void OnEventRoomReady(object __instance)
    {
        if (__instance is not Node room || !GodotObject.IsInstanceValid(room)) return;

        string? eventId = Sts2RuntimeInspector.InspectEventId(room);
        UI.RoomOverlayFactory.AttachEventCommentary(room, TierProvider, eventId, Config.EnableEventCommentary, CurrentAuthor);

        // Also support F6/F8/F9 hotkeys & events that spawn an EmbeddedCombatRoom
        AttachRoomPollTimer(room, "OverlayEventCombatTimer", 0.15, () =>
        {
            UI.RoomOverlayFactory.PollHotkeys(() => ToggleAuthor(room), () => ToggleVisibility(room), () => ForceRefreshTiers(room));
            var embeddedCombat = Sts2RuntimeInspector.GetEmbeddedCombatRoom(room);
            bool hasEmbeddedCombat = embeddedCombat != null && GodotObject.IsInstanceValid(embeddedCombat);
            if (hasEmbeddedCombat)
            {
                OnCombatRoomReady(embeddedCombat!);
            }

            var evPanel = room.GetNodeOrNull<Control>("EventCommentaryPanel");
            if (evPanel != null && GodotObject.IsInstanceValid(evPanel))
            {
                evPanel.Visible = Config.EnableEventCommentary &&
                                  !UI.RoomOverlayFactory.IsOverlayHidden &&
                                  !hasEmbeddedCombat;
            }
        });
    }

    public static void OnGenericRoomReady(object __instance)
    {
        if (__instance is not Node room || !GodotObject.IsInstanceValid(room)) return;
        AttachRoomPollTimer(room, "OverlayGenericRoomTimer", 0.15, () =>
        {
            UI.RoomOverlayFactory.PollHotkeys(() => ToggleAuthor(room), () => ToggleVisibility(room), () => ForceRefreshTiers(room));
        });
    }

    private static void AttachRoomPollTimer(Node room, string name, double intervalSeconds, Action tick)
    {
        if (room.GetNodeOrNull<global::Godot.Timer>(name) != null) return;
        var timer = new global::Godot.Timer { Name = name, WaitTime = intervalSeconds, Autostart = true };
        timer.Timeout += tick;
        room.AddChild(timer);
        tick();
    }
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Cards.NCard), "_Ready")]
public static class NCardReadyPatch
{
    public static void Postfix(MegaCrit.Sts2.Core.Nodes.Cards.NCard __instance) => UpdateCardBadge(__instance);

    public static void UpdateCardBadge(MegaCrit.Sts2.Core.Nodes.Cards.NCard cardNode)
    {
        try
        {
            if (cardNode == null || !GodotObject.IsInstanceValid(cardNode)) return;

            string cardId = ModEntry.FormatModelId(cardNode.Model?.Id);
            if (!ModEntry.Config.EnableCardBadges || string.IsNullOrEmpty(cardId) || Sts2RuntimeInspector.IsCardInCombatHand(cardNode))
            {
                UI.TierBadgeFactory.HideBadge(cardNode, "TierBadgePanel");
                return;
            }

            UI.TierBadgeFactory.CreateOrUpdateBadge(cardNode, ModEntry.TierProvider, cardId, ModEntry.CurrentAuthor, Sts2RuntimeInspector.IsCardInCombatHand, ModEntry.Config.EnableHoverCommentary);
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
    public static void Postfix(MegaCrit.Sts2.Core.Nodes.Cards.NCard __instance) => NCardReadyPatch.UpdateCardBadge(__instance);
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Relics.NRelic), "_Ready")]
public static class NRelicReadyPatch
{
    public static void Postfix(MegaCrit.Sts2.Core.Nodes.Relics.NRelic __instance) => UpdateRelicBadge(__instance);

    public static void UpdateRelicBadge(MegaCrit.Sts2.Core.Nodes.Relics.NRelic relicNode)
    {
        try
        {
            if (relicNode == null || !GodotObject.IsInstanceValid(relicNode)) return;

            string relicId = ModEntry.FormatModelId(relicNode.Model?.Id);
            if (!ModEntry.Config.EnableRelicBadges || string.IsNullOrEmpty(relicId) || Sts2RuntimeInspector.IsItemInTopBar(relicNode))
            {
                UI.TierBadgeFactory.HideBadge(relicNode, "RelicBadgePanel");
                return;
            }

            UI.RelicBadgeFactory.CreateOrUpdateBadge(relicNode, ModEntry.TierProvider, relicId, ModEntry.CurrentAuthor, Sts2RuntimeInspector.IsItemInTopBar, ModEntry.Config.EnableHoverCommentary);
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
    public static void Postfix(MegaCrit.Sts2.Core.Nodes.Relics.NRelic __instance) => NRelicReadyPatch.UpdateRelicBadge(__instance);
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton), "_Ready")]
public static class NEventOptionButtonReadyPatch
{
    public static void Postfix(MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton __instance) => UpdateOptionBadge(__instance);

    public static void UpdateOptionBadge(MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton buttonNode)
    {
        try
        {
            var option = buttonNode?.Option;
            if (buttonNode == null || !GodotObject.IsInstanceValid(buttonNode) || option == null || (!ModEntry.Config.EnableRelicBadges && !ModEntry.Config.EnableCardBadges))
            {
                UI.TierBadgeFactory.HideBadge(buttonNode, "RelicOptionBadgePanel");
                return;
            }

            string? relicId = option.Relic?.Id?.Entry;
            string? tipId = null;

            if (string.IsNullOrEmpty(relicId) && option.HoverTips != null)
            {
                foreach (var tip in option.HoverTips)
                {
                    if (tip is MegaCrit.Sts2.Core.HoverTips.CardHoverTip cardTip && cardTip.Card?.Id != null)
                    {
                        tipId = cardTip.Card.Id.Entry;
                        break;
                    }
                    if (tip is MegaCrit.Sts2.Core.HoverTips.HoverTip hoverTip && hoverTip.CanonicalModel?.Id != null)
                    {
                        tipId = hoverTip.CanonicalModel.Id.Entry;
                        break;
                    }
                }
            }

            string targetId = OverlayCalculators.ExtractEventOptionId(relicId, tipId, option.TextKey);
            if (!string.IsNullOrEmpty(targetId))
            {
                UI.RelicBadgeFactory.CreateOrUpdateOptionBadge(buttonNode, ModEntry.TierProvider, targetId, ModEntry.CurrentAuthor, ModEntry.Config.EnableHoverCommentary);
            }
            else
            {
                UI.TierBadgeFactory.HideBadge(buttonNode, "RelicOptionBadgePanel");
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
    public static void Postfix(MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton __instance) => NEventOptionButtonReadyPatch.UpdateOptionBadge(__instance);
}
