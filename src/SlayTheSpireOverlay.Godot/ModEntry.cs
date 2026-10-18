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
        UI.RoomOverlayFactory.SyncLayoutFromConfig(Config);

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

        int appliedCount = 0;
        int skippedCount = 0;
        try
        {
            var harmony = new Harmony("SlayTheSpireOverlay.Godot");

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
                        GD.Print($"[STS2 Overlay] patch class {patchClass.Name} skipped: no matching methods found");
                    }
                }
                catch (Exception ex)
                {
                    skippedCount++;
                    GD.PrintErr($"[STS2 Overlay] patch class {patchClass.Name} skipped: {ex.Message}");
                }
            }

            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Cards.NCard", new[] { "UpdateVisuals" }, nameof(OnCardVisualsUpdated), ref appliedCount, ref skippedCount);
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Relics.NRelic", new[] { "Reload" }, nameof(OnRelicReloaded), ref appliedCount, ref skippedCount);
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Potions.NPotion", new[] { "_Ready", "set_Model", "Reload" }, nameof(OnPotionReady), ref appliedCount, ref skippedCount);
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Rewards.NRewardButton", new[] { "_Ready", "Reload", "SetReward" }, nameof(OnRewardButtonReady), ref appliedCount, ref skippedCount);
            PatchHolderFocusIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Cards.Holders.NCardHolder", ref appliedCount, ref skippedCount);
            PatchHolderFocusIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantSlot", ref appliedCount, ref skippedCount);
            PatchHolderFocusIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton", ref appliedCount, ref skippedCount);
            PatchHolderFocusIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Relics.NRelicBasicHolder", ref appliedCount, ref skippedCount);
            PatchHolderFocusIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic.NTreasureRoomRelicHolder", ref appliedCount, ref skippedCount);
            PatchHolderFocusIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Rewards.NRewardButton", ref appliedCount, ref skippedCount);
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom", new[] { "_Ready" }, nameof(OnCombatRoomReady), ref appliedCount, ref skippedCount);
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom", new[] { "_Ready" }, nameof(OnMerchantRoomReady), ref appliedCount, ref skippedCount);
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom", new[] { "_Ready" }, nameof(OnEventRoomReady), ref appliedCount, ref skippedCount);
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Rooms.NTreasureRoom", new[] { "_Ready" }, nameof(OnGenericRoomReady), ref appliedCount, ref skippedCount);
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Rooms.NRestSiteRoom", new[] { "_Ready" }, nameof(OnGenericRoomReady), ref appliedCount, ref skippedCount);
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Rooms.NMapRoom", new[] { "_Ready" }, nameof(OnGenericRoomReady), ref appliedCount, ref skippedCount);
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.NRun", new[] { "_Ready" }, nameof(OnGenericRoomReady), ref appliedCount, ref skippedCount);
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.NGame", new[] { "_Ready" }, nameof(OnGenericRoomReady), ref appliedCount, ref skippedCount);
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModInfoContainer", new[] { "Fill" }, nameof(OnModInfoFill), ref appliedCount, ref skippedCount);
            PatchIfPresent(harmony, "MegaCrit.Sts2.Core.Nodes.Screens.Settings.NSettingsScreen", new[] { "_Ready" }, nameof(OnSettingsScreenReady), ref appliedCount, ref skippedCount);
            PatchRunSubmenuStackIfPresent(harmony, ref appliedCount, ref skippedCount);

            GD.Print($"[STS2 Overlay] Harmony initialization complete: {appliedCount} applied, {skippedCount} skipped");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[STS2 Overlay] Unexpected error initializing Harmony: {ex.Message}");
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
        Config.Author = (Config.Author?.Trim().ToLowerInvariant()) switch
        {
            "baalorlord" => "Jorbs",
            "jorbs" => "Both",
            _ => "Baalorlord"
        };
        SaveConfig();

        string f6Label = OverlayCalculators.NormalizeKeybindName(Config.ToggleAuthorKey, "F6");
        var root = contextNode != null && GodotObject.IsInstanceValid(contextNode)
            ? contextNode.GetTree()?.CurrentScene
            : null;
        UI.RoomOverlayFactory.ShowAuthorToast(root, $"Tier List: {Config.Author} [{f6Label}]");
        RefreshSceneFromContext(contextNode);

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
        string f9Label = OverlayCalculators.NormalizeKeybindName(Config.RefreshTiersKey, "F9");
        var root = contextNode != null && GodotObject.IsInstanceValid(contextNode)
            ? contextNode.GetTree()?.CurrentScene
            : null;
        UI.RoomOverlayFactory.ShowAuthorToast(root, $"Refreshing {author}... [{f9Label}]");

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
                    UI.RoomOverlayFactory.ShowAuthorToast(currentRoot, $"Refreshed {author} [{f9Label}]");
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
        string f8Label = OverlayCalculators.NormalizeKeybindName(Config.ToggleOverlayKey, "F8");
        var root = contextNode != null && GodotObject.IsInstanceValid(contextNode)
            ? contextNode.GetTree()?.CurrentScene
            : null;
        string status = UI.RoomOverlayFactory.IsOverlayHidden ? $"Overlay Hidden [{f8Label}]" : $"Overlay Visible [{f8Label}]";
        UI.RoomOverlayFactory.ShowAuthorToast(root, status, forceVisible: true);
    }

    private static void OnCollapseToggled(Node? contextNode)
    {
        string f7Label = OverlayCalculators.NormalizeKeybindName(Config.CollapseDeckTrackerKey, "F7");
        var root = contextNode != null && GodotObject.IsInstanceValid(contextNode)
            ? contextNode.GetTree()?.CurrentScene
            : null;
        string status = UI.RoomOverlayFactory.IsDeckTrackerCollapsed
            ? $"Deck View: Compact [{f7Label}]"
            : $"Deck View: Expanded [{f7Label}]";
        UI.RoomOverlayFactory.ShowAuthorToast(root, status);
    }

    private static void PollRoomInput(Node room)
    {
        UI.RoomOverlayFactory.PollHotkeys(
            () => ToggleAuthor(room),
            () => ToggleVisibility(room),
            () => ForceRefreshTiers(room),
            Config,
            () => OnCollapseToggled(room));

        if (room.GetType().Name.EndsWith("Room", StringComparison.Ordinal))
        {
            UI.RoomOverlayFactory.UpdateQuickToggleBar(
                room,
                Config,
                () => ToggleAuthor(room),
                () => ToggleVisibility(room),
                () => ForceRefreshTiers(room));
        }
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
        string typeName = node.GetType().Name;
        if (typeName.EndsWith("Room", StringComparison.Ordinal))
        {
            UI.RoomOverlayFactory.UpdateQuickToggleBar(
                node,
                Config,
                () => ToggleAuthor(node),
                () => ToggleVisibility(node),
                () => ForceRefreshTiers(node));
        }

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
        else if (typeName == "NPotion")
        {
            OnPotionReady(node);
        }
        else if (typeName == "NRewardButton")
        {
            OnRewardButtonReady(node);
        }
        else if (typeName == "NEventRoom")
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

    private static void PatchIfPresent(Harmony harmony, string typeName, string[] methods, string postfixName, ref int applied, ref int skipped)
    {
        var type = AccessTools.TypeByName(typeName);
        if (type == null)
        {
            skipped += methods.Length;
            GD.Print($"[STS2 Overlay] patch {typeName} skipped: target type not found");
            return;
        }

        var postfix = AccessTools.Method(typeof(ModEntry), postfixName);
        if (postfix == null)
        {
            skipped += methods.Length;
            GD.PrintErr($"[STS2 Overlay] patch handler {postfixName} skipped: handler method not found on ModEntry");
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
                    GD.Print($"[STS2 Overlay] patch {typeName}.{name} skipped: method not found on {typeName}");
                }
            }
            catch (Exception ex)
            {
                skipped++;
                GD.PrintErr($"[STS2 Overlay] patch {typeName}.{name} skipped: {ex.Message}");
            }
        }
    }

    private static void PatchHolderFocusIfPresent(Harmony harmony, string typeName, ref int applied, ref int skipped)
    {
        PatchIfPresent(harmony, typeName, new[] { "OnFocus" }, nameof(OnItemHolderFocused), ref applied, ref skipped);
        PatchIfPresent(harmony, typeName, new[] { "OnUnfocus" }, nameof(OnItemHolderUnfocused), ref applied, ref skipped);
    }

    private static void PatchRunSubmenuStackIfPresent(Harmony harmony, ref int applied, ref int skipped)
    {
        try
        {
            var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Screens.NRunSubmenuStack");
            if (type == null)
            {
                skipped++;
                GD.Print("[STS2 Overlay] patch NRunSubmenuStack skipped: target type not found");
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
                GD.Print("[STS2 Overlay] patch NRunSubmenuStack.GetSubmenuType skipped: method or prefix not found");
            }
        }
        catch (Exception ex)
        {
            skipped++;
            GD.PrintErr($"[STS2 Overlay] patch NRunSubmenuStack.GetSubmenuType skipped: {ex.Message}");
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
        string? itemId = Sts2RuntimeInspector.ExtractRewardItemId(
            __instance,
            out bool isCard,
            out bool isRelic,
            out bool isPotion,
            out int upgradeLevel,
            out string? enchantmentId);
        bool allowed = (isCard && Config.EnableCardBadges) ||
                       (isRelic && Config.EnableRelicBadges) ||
                       (isPotion && Config.EnablePotionBadges);
        if (!allowed || string.IsNullOrEmpty(itemId))
        {
            UI.TierBadgeFactory.HideBadge(node, "RelicOptionBadgePanel");
            return;
        }

        UI.RelicBadgeFactory.CreateOrUpdateOptionBadge(
            node,
            TierProvider,
            itemId,
            CurrentAuthor,
            Config.EnableHoverCommentary,
            upgradeLevel,
            enchantmentId);
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
            PollRoomInput(room);
            bool visible = !UI.RoomOverlayFactory.IsOverlayHidden && !Sts2RuntimeInspector.IsCombatHudObscured(room);
            bool showIncoming = visible && Config.EnableAttackSummary;
            bool showAttack = visible && (Config.EnableAttackSummary || Config.EnableOutgoingDamageSummary);
            bool showTracker = visible && Config.EnableDeckTracker;
            UI.RoomOverlayFactory.UpdateAttackSummary(room, showAttack ? Sts2RuntimeInspector.InspectCombatAttacks(room) : new AttackSummary(0, 0, 0), showAttack, showIncoming);
            UI.RoomOverlayFactory.UpdateDeckTracker(room, showTracker ? Sts2RuntimeInspector.InspectDeckTracker() : null, showTracker, CurrentAuthor, Config.CollapseDeckTrackerKey);

            bool cardRewardActive = Sts2RuntimeInspector.IsCardRewardScreenActive(room);
            bool showSynergy = !UI.RoomOverlayFactory.IsOverlayHidden && Config.EnableDeckSynergyBar && cardRewardActive;
            UI.RoomOverlayFactory.UpdateDeckSynergyPanel(room, showSynergy ? Sts2RuntimeInspector.InspectMasterDeckSynergy() : null, showSynergy);

            bool showBossPrep = !UI.RoomOverlayFactory.IsOverlayHidden && Config.EnableBossPrepBanner && cardRewardActive;
            UI.RoomOverlayFactory.UpdateBossPrepBanner(room, showBossPrep ? Sts2RuntimeInspector.InspectCurrentBoss() : null, showBossPrep);
        });
    }

    public static void OnMerchantRoomReady(object __instance)
    {
        if (__instance is not Node room || !GodotObject.IsInstanceValid(room)) return;
        AttachRoomPollTimer(room, "OverlayMerchantTimer", 0.10, () =>
        {
            bool pinTriggered = UI.RoomOverlayFactory.PollPinHotkey(Config);
            PollRoomInput(room);
            bool showGold = !UI.RoomOverlayFactory.IsOverlayHidden && Config.EnableMerchantGoldLeft;
            var budget = showGold ? Sts2RuntimeInspector.InspectMerchantBudget(room, pinTriggered) : null;
            UI.RoomOverlayFactory.UpdateMerchantGoldLeft(room, budget, showGold, Config.PinShopItemKey);

            bool showSynergy = !UI.RoomOverlayFactory.IsOverlayHidden && Config.EnableDeckSynergyBar;
            UI.RoomOverlayFactory.UpdateDeckSynergyPanel(room, showSynergy ? Sts2RuntimeInspector.InspectMasterDeckSynergy() : null, showSynergy);
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
            PollRoomInput(room);
            var embeddedCombat = Sts2RuntimeInspector.GetEmbeddedCombatRoom(room);
            bool hasEmbeddedCombat = embeddedCombat != null && GodotObject.IsInstanceValid(embeddedCombat);
            if (hasEmbeddedCombat)
            {
                OnCombatRoomReady(embeddedCombat!);
                var evQuickBar = room.GetNodeOrNull<Control>("QuickToggleBarPanel");
                if (evQuickBar != null && GodotObject.IsInstanceValid(evQuickBar))
                {
                    evQuickBar.Visible = false;
                }
            }

            var evPanel = room.GetNodeOrNull<Control>("EventCommentaryPanel");
            if (evPanel != null && GodotObject.IsInstanceValid(evPanel))
            {
                evPanel.Visible = Config.EnableEventCommentary &&
                                  !UI.RoomOverlayFactory.IsOverlayHidden &&
                                  !hasEmbeddedCombat;
            }

            bool cardRewardActive = Sts2RuntimeInspector.IsCardRewardScreenActive(room);
            bool showSynergy = !UI.RoomOverlayFactory.IsOverlayHidden && Config.EnableDeckSynergyBar && cardRewardActive;
            UI.RoomOverlayFactory.UpdateDeckSynergyPanel(room, showSynergy ? Sts2RuntimeInspector.InspectMasterDeckSynergy() : null, showSynergy);

            bool showBossPrep = !UI.RoomOverlayFactory.IsOverlayHidden && Config.EnableBossPrepBanner && cardRewardActive;
            UI.RoomOverlayFactory.UpdateBossPrepBanner(room, showBossPrep ? Sts2RuntimeInspector.InspectCurrentBoss() : null, showBossPrep);
        });
    }

    public static void OnGenericRoomReady(object __instance)
    {
        if (__instance is not Node room || !GodotObject.IsInstanceValid(room)) return;
        AttachRoomPollTimer(room, "OverlayGenericRoomTimer", 0.15, () =>
        {
            PollRoomInput(room);
            bool isRestSite = room.GetType().Name.Contains("RestSite", StringComparison.OrdinalIgnoreCase);
            bool cardRewardActive = Sts2RuntimeInspector.IsCardRewardScreenActive(room);
            bool showSynergy = !UI.RoomOverlayFactory.IsOverlayHidden && Config.EnableDeckSynergyBar && cardRewardActive;
            UI.RoomOverlayFactory.UpdateDeckSynergyPanel(room, showSynergy ? Sts2RuntimeInspector.InspectMasterDeckSynergy() : null, showSynergy);

            bool showBossPrep = !UI.RoomOverlayFactory.IsOverlayHidden && Config.EnableBossPrepBanner && (cardRewardActive || isRestSite);
            UI.RoomOverlayFactory.UpdateBossPrepBanner(room, showBossPrep ? Sts2RuntimeInspector.InspectCurrentBoss() : null, showBossPrep);
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

            string cardId;
            try
            {
                cardId = ModEntry.FormatModelId(cardNode.Model?.Id);
            }
            catch (Exception ex) when (ex.Message.Contains("accessed before it was set", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!ModEntry.Config.EnableCardBadges || string.IsNullOrEmpty(cardId) || Sts2RuntimeInspector.IsCardInCombatHand(cardNode))
            {
                UI.TierBadgeFactory.HideBadge(cardNode, "TierBadgePanel");
                return;
            }

            Sts2RuntimeInspector.InspectCardUpgradeAndEnchantment(cardNode, out int upgradeLevel, out string? enchantmentId);
            UI.TierBadgeFactory.CreateOrUpdateBadge(
                cardNode,
                ModEntry.TierProvider,
                cardId,
                ModEntry.CurrentAuthor,
                Sts2RuntimeInspector.IsCardInCombatHand,
                ModEntry.Config.EnableHoverCommentary,
                upgradeLevel,
                enchantmentId);
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("accessed before it was set", StringComparison.OrdinalIgnoreCase)) return;
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

            string relicId;
            try
            {
                relicId = ModEntry.FormatModelId(relicNode.Model?.Id);
            }
            catch (Exception ex) when (ex.Message.Contains("accessed before it was set", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!ModEntry.Config.EnableRelicBadges || string.IsNullOrEmpty(relicId) || Sts2RuntimeInspector.IsItemInTopBar(relicNode))
            {
                UI.TierBadgeFactory.HideBadge(relicNode, "RelicBadgePanel");
                return;
            }

            UI.RelicBadgeFactory.CreateOrUpdateBadge(relicNode, ModEntry.TierProvider, relicId, ModEntry.CurrentAuthor, Sts2RuntimeInspector.IsItemInTopBar, ModEntry.Config.EnableHoverCommentary);
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("accessed before it was set", StringComparison.OrdinalIgnoreCase)) return;
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
            if (buttonNode == null || !GodotObject.IsInstanceValid(buttonNode)) return;

            var option = buttonNode.Option;
            if (option == null || (!ModEntry.Config.EnableRelicBadges && !ModEntry.Config.EnableCardBadges))
            {
                UI.TierBadgeFactory.HideBadge(buttonNode, "RelicOptionBadgePanel");
                return;
            }

            string? relicId = option.Relic?.Id?.Entry;
            string? tipId = null;
            int upgradeLevel = 0;
            string? enchantmentId = null;

            if (string.IsNullOrEmpty(relicId) && option.HoverTips != null)
            {
                foreach (var tip in option.HoverTips)
                {
                    if (tip is MegaCrit.Sts2.Core.HoverTips.CardHoverTip cardTip && cardTip.Card?.Id != null)
                    {
                        tipId = cardTip.Card.Id.Entry;
                        Sts2RuntimeInspector.InspectCardUpgradeAndEnchantment(cardTip.Card, out upgradeLevel, out enchantmentId);
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
                UI.RelicBadgeFactory.CreateOrUpdateOptionBadge(
                    buttonNode,
                    ModEntry.TierProvider,
                    targetId,
                    ModEntry.CurrentAuthor,
                    ModEntry.Config.EnableHoverCommentary,
                    upgradeLevel,
                    enchantmentId);
            }
            else
            {
                UI.TierBadgeFactory.HideBadge(buttonNode, "RelicOptionBadgePanel");
            }
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("accessed before it was set", StringComparison.OrdinalIgnoreCase)) return;
            GD.PrintErr($"[STS2 Overlay] Error in NEventOptionButton patch: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton), "set_Option")]
public static class NEventOptionButtonSetOptionPatch
{
    public static void Postfix(MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton __instance) => NEventOptionButtonReadyPatch.UpdateOptionBadge(__instance);
}
