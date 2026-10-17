using Godot;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using SlayTheSpireOverlay.Core.Services;

namespace SlayTheSpireOverlay.Godot;

/// <summary>
/// Minimal in-process reader for MegaCrit.Sts2.Core combat, merchant, and event state.
/// </summary>
public static class Sts2RuntimeInspector
{
    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<(Type, string), MethodInfo?> MethodCache = new();
    private static readonly ConcurrentDictionary<Type, object?> CostModifierEnumCache = new();
    private static readonly ConcurrentDictionary<(Type, string), MemberInfo?> MemberCache = new();
    private static readonly ConcurrentDictionary<(Type, string), MemberInfo?> StaticMemberCache = new();

    public static bool IsCombatHudObscured(Node combatRoom)
    {
        try
        {
            // Hide when combat has ended (e.g. post-combat reward screen)
            var combatMgrType = GetCachedType("MegaCrit.Sts2.Core.Combat.CombatManager");
            var combatMgr = combatMgrType != null ? GetStaticMember(combatMgrType, "Instance", "_instance") : null;
            if (combatMgr != null && combatMgrType != null)
            {
                var inProgressMethod = GetCachedMethod(combatMgrType, "IsInProgress");
                if (inProgressMethod != null && inProgressMethod.Invoke(combatMgr, null) is bool inProg && !inProg)
                {
                    return true;
                }
                if (GetMember(combatMgr, "IsInProgress", "_isInProgress") is bool inProgProp && !inProgProp)
                {
                    return true;
                }
            }

            // If the player is actively holding the Peek button, keep the combat HUD visible
            var ui = GetMember(combatRoom, "Ui", "_ui");
            var peekBtn = ui != null ? GetMember(ui, "PeekButton", "_peekButton") : null;
            bool isPeeking = peekBtn != null && GetMember(peekBtn, "IsPeeking", "_isPeeking") is bool pk && pk;
            if (isPeeking) return false;

            // Hide when Map screen is open
            var runNode = combatRoom.GetTree()?.CurrentScene;
            if (runNode != null)
            {
                var globalUi = GetMember(runNode, "GlobalUi", "_globalUi");
                var mapScreen = globalUi != null ? GetMember(globalUi, "MapScreen", "_mapScreen") : null;
                if (mapScreen != null && GetMember(mapScreen, "IsOpen", "_isOpen") is bool mapOpen && mapOpen)
                {
                    return true;
                }

                // Hide when an overlay screen (Deck view, Card reward, Grid select) is open on top of combat
                var overlays = globalUi != null ? GetMember(globalUi, "Overlays", "_overlays") : null;
                if (overlays != null && GetMember(overlays, "_overlays", "Overlays") is ICollection overlayList && overlayList.Count > 0)
                {
                    return true;
                }
            }
        }
        catch
        {
            // Default to visible on reflection fallback
        }
        return false;
    }

    public static bool IsCardInCombatHand(Node? cardNode)
    {
        if (cardNode == null) return false;
        try
        {
            string? pile = GetMember(cardNode, "DisplayingPile", "_displayingPile")?.ToString();
            if (string.Equals(pile, "Hand", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(pile, "Play", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Node? cur = cardNode.GetParent();
            for (int depth = 0; depth < 6 && cur != null; depth++, cur = cur.GetParent())
            {
                string typeName = cur.GetType().Name;
                string nodeName = cur.Name.ToString();
                if (typeName.Contains("PlayerHand", StringComparison.OrdinalIgnoreCase) ||
                    typeName.Contains("HandCard", StringComparison.OrdinalIgnoreCase) ||
                    typeName.Contains("CardPlay", StringComparison.OrdinalIgnoreCase) ||
                    typeName.Contains("PlayQueue", StringComparison.OrdinalIgnoreCase) ||
                    nodeName.Contains("PlayerHand", StringComparison.OrdinalIgnoreCase) ||
                    nodeName.Contains("HandCard", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
        }
        return false;
    }

    public static bool IsItemInTopBar(Node? itemNode)
    {
        try
        {
            Node? cur = itemNode?.GetParent();
            for (int depth = 0; depth < 6 && cur != null; depth++, cur = cur.GetParent())
            {
                string typeName = cur.GetType().Name;
                string nodeName = cur.Name.ToString();
                if (typeName.Contains("TopBar", StringComparison.OrdinalIgnoreCase) ||
                    typeName.Contains("RelicInventory", StringComparison.OrdinalIgnoreCase) ||
                    typeName.Contains("PotionContainer", StringComparison.OrdinalIgnoreCase) ||
                    typeName.Contains("TopPanel", StringComparison.OrdinalIgnoreCase) ||
                    typeName.Contains("RewardButton", StringComparison.OrdinalIgnoreCase) ||
                    nodeName.Contains("TopBar", StringComparison.OrdinalIgnoreCase) ||
                    nodeName.Contains("RelicInventory", StringComparison.OrdinalIgnoreCase) ||
                    nodeName.Contains("PotionContainer", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
        }
        return false;
    }

    public static AttackSummary InspectCombatAttacks(Node combatRoom)
    {
        try
        {
            var container = GetMember(combatRoom, "_enemyContainer", "EnemyContainer") as Node;
            if (container == null || !GodotObject.IsInstanceValid(container))
            {
                return new AttackSummary(0, 0, 0);
            }

            var enemies = new List<(int CurrentHp, IEnumerable<(string? IntentType, string? LabelText)> Intents)>();
            foreach (Node child in container.GetChildren())
            {
                if (child == null || !GodotObject.IsInstanceValid(child)) continue;

                var entity = GetMember(child, "Entity", "_entity");
                if (entity == null) continue;
                int hp = GetMember(entity, "CurrentHp", "_currentHp") is int i ? i : 0;

                var intents = new List<(string? IntentType, string? LabelText)>();
                if (GetMember(child, "IntentContainer", "_intentContainer") is Node intentContainer &&
                    GodotObject.IsInstanceValid(intentContainer))
                {
                    foreach (Node intentNode in intentContainer.GetChildren())
                    {
                        if (intentNode is CanvasItem ci && !ci.Visible) continue;
                        var intentModel = GetMember(intentNode, "_intent", "Intent");
                        var labelNode = GetMember(intentNode, "_valueLabel", "ValueLabel");
                        string? text = labelNode is RichTextLabel rtl ? rtl.Text : (labelNode is Label lbl ? lbl.Text : null);
                        intents.Add((intentModel?.GetType().Name, text));
                    }
                }
                enemies.Add((hp, intents));
            }

            object? player = GetLocalPlayer();
            var (playerHp, playerBlock) = GetLocalPlayerHpAndBlock(combatRoom, player);
            return OverlayCalculators.CalculateIncomingAttacks(enemies, playerBlock, playerHp);
        }
        catch
        {
            return new AttackSummary(0, 0, 0);
        }
    }

    public static DeckTrackerState? InspectDeckTracker()
    {
        try
        {
            object? player = GetLocalPlayer();
            object? combatState = player != null ? GetMember(player, "PlayerCombatState", "_playerCombatState") : null;
            if (combatState == null) return null;

            int exhaustCount = CountPile(GetMember(combatState, "ExhaustPile", "_exhaustPile"));
            return OverlayCalculators.CalculateDeckTracker(
                ReadPile(GetMember(combatState, "DrawPile", "_drawPile")),
                ReadPile(GetMember(combatState, "DiscardPile", "_discardPile")),
                ReadPile(GetMember(combatState, "Hand", "_hand")),
                exhaustCount);
        }
        catch
        {
            return null;
        }
    }

    private static (int Hp, int Block) GetLocalPlayerHpAndBlock(Node combatRoom, object? player)
    {
        object? creature = player != null ? (GetMember(player, "Creature", "_creature") ?? player) : null;
        int hp = creature != null && GetMember(creature, "CurrentHp", "_currentHp") is int h ? h : 0;
        int block = creature != null && GetMember(creature, "Block", "_block") is int b ? b : 0;

        if (hp <= 0 && GetMember(combatRoom, "_allyContainer", "AllyContainer") is Node allyContainer &&
            GodotObject.IsInstanceValid(allyContainer))
        {
            foreach (Node child in allyContainer.GetChildren())
            {
                var entity = GetMember(child, "Entity", "_entity");
                if (entity == null) continue;
                hp = GetMember(entity, "CurrentHp", "_currentHp") is int ah ? ah : hp;
                block = GetMember(entity, "Block", "_block") is int ab ? ab : block;
                if (hp > 0) break;
            }
        }
        return (hp, block);
    }

    public static (int RemainingGold, int Cost)? InspectMerchantGoldLeft(Node merchantRoom)
    {
        try
        {
            object? player = GetLocalPlayer();
            if (player == null) return null;
            int gold = GetMember(player, "Gold", "_gold") is int g ? g : 0;

            if (GetMember(merchantRoom, "Inventory", "_inventory") is not Node inventory ||
                !GodotObject.IsInstanceValid(inventory))
            {
                return null;
            }

            var slots = new List<(int Cost, bool IsStocked, bool IsHovered)>();
            string[] containers = { "_characterCardContainer", "_colorlessCardContainer", "_relicContainer", "_potionContainer" };
            foreach (string name in containers)
            {
                if (GetMember(inventory, name) is Node c && GodotObject.IsInstanceValid(c))
                {
                    foreach (Node slot in c.GetChildren()) AddSlot(slot, slots);
                }
            }
            if (GetMember(inventory, "_cardRemovalNode", "CardRemovalNode") is Node rem) AddSlot(rem, slots);

            return OverlayCalculators.CalculateGoldLeft(gold, slots);
        }
        catch
        {
            return null;
        }
    }

    public static string? InspectEventId(Node eventRoom)
    {
        try
        {
            var ev = GetMember(eventRoom, "_event", "Event");
            return ExtractModelEntryId(ev);
        }
        catch
        {
            return null;
        }
    }

    public static Node? GetEmbeddedCombatRoom(Node eventRoom)
    {
        try
        {
            return GetMember(eventRoom, "EmbeddedCombatRoom", "_embeddedCombatRoom") as Node;
        }
        catch
        {
            return null;
        }
    }

    public static string? ExtractNodeModelEntryId(object? node) =>
        node != null ? ExtractModelEntryId(GetMember(node, "Model", "_model")) : null;

    public static string? ExtractModelEntryId(object? model)
    {
        var idObj = model != null ? GetMember(model, "Id", "_id") : null;
        return idObj != null ? GetMember(idObj, "Entry", "_entry") as string : null;
    }

    public static string? ExtractRewardItemId(object? rewardButton, out bool isCard, out bool isRelic, out bool isPotion)
    {
        isCard = false;
        isRelic = false;
        isPotion = false;
        if (rewardButton == null) return null;

        try
        {
            var reward = GetMember(rewardButton, "Reward", "_reward");
            if (reward == null) return null;

            string rewardType = reward.GetType().Name;
            if (rewardType.Contains("Relic", StringComparison.OrdinalIgnoreCase))
            {
                isRelic = true;
                return ExtractModelEntryId(GetMember(reward, "Relic", "_relic", "_predeterminedRelic"));
            }
            if (rewardType.Contains("Potion", StringComparison.OrdinalIgnoreCase))
            {
                isPotion = true;
                return ExtractModelEntryId(GetMember(reward, "Potion", "_potion"));
            }
            if (rewardType.Contains("SpecialCard", StringComparison.OrdinalIgnoreCase))
            {
                isCard = true;
                return ExtractModelEntryId(GetMember(reward, "Card", "_card"));
            }
        }
        catch
        {
        }
        return null;
    }

    public static bool IsOurMod(object? mod)
    {
        if (mod == null) return false;
        try
        {
            var manifest = GetMember(mod, "manifest", "Manifest");
            string? id = manifest != null ? GetMember(manifest, "id", "Id") as string : null;
            if (!string.IsNullOrEmpty(id) && id.Contains("SlayTheSpireOverlay", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string? pck = GetMember(mod, "pckName", "PckName") as string;
            return !string.IsNullOrEmpty(pck) && pck.Contains("SlayTheSpireOverlay", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void EnableModdingButtonInRun(object? settingsScreen)
    {
        if (settingsScreen is not Node screenNode || !GodotObject.IsInstanceValid(screenNode)) return;
        try
        {
            var moddingRow = screenNode.GetNodeOrNull<CanvasItem>("%Modding");
            if (moddingRow != null && GodotObject.IsInstanceValid(moddingRow)) moddingRow.Visible = true;

            var moddingDivider = screenNode.GetNodeOrNull<CanvasItem>("%ModdingDivider");
            if (moddingDivider != null && GodotObject.IsInstanceValid(moddingDivider)) moddingDivider.Visible = true;

            var btn = GetMember(settingsScreen, "_moddingScreenButton", "ModdingScreenButton") ?? screenNode.GetNodeOrNull("%ModdingButton");
            if (btn != null)
            {
                GetCachedMethod(btn.GetType(), "Enable")?.Invoke(btn, null);
            }
        }
        catch
        {
        }
    }

    public static object? GetOrCreateRunModdingScreen(object? submenuStack)
    {
        if (submenuStack is not Node stackNode || !GodotObject.IsInstanceValid(stackNode)) return null;
        try
        {
            var existing = stackNode.GetNodeOrNull("OverlayRunModdingScreen");
            if (existing != null && GodotObject.IsInstanceValid(existing))
            {
                return existing;
            }

            var moddingScreenType = GetCachedType("MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen.NModdingScreen");
            var createMethod = moddingScreenType != null ? GetCachedMethod(moddingScreenType, "Create") : null;
            if (createMethod?.Invoke(null, null) is Control moddingScreen && GodotObject.IsInstanceValid(moddingScreen))
            {
                moddingScreen.Name = "OverlayRunModdingScreen";
                moddingScreen.Visible = false;
                stackNode.AddChild(moddingScreen);
                return moddingScreen;
            }
        }
        catch
        {
        }
        return null;
    }

    private static void AddSlot(Node? slot, List<(int Cost, bool IsStocked, bool IsHovered)> slots)
    {
        if (slot == null || !GodotObject.IsInstanceValid(slot)) return;
        bool hovered = GetMember(slot, "_isHovered", "IsHovered") is bool h && h;
        var entry = GetMember(slot, "Entry", "_entry");
        if (entry == null) return;
        int cost = GetMember(entry, "Cost", "_cost") is int c ? c : 0;
        bool stocked = GetMember(entry, "IsStocked", "_isStocked") is bool s && s;
        slots.Add((cost, stocked, hovered));
    }

    private static int CountPile(object? pile)
    {
        if (pile == null) return 0;
        var cards = GetMember(pile, "Cards", "_cards");
        if (cards is ICollection col) return col.Count;
        if (cards is IEnumerable enumerable)
        {
            int count = 0;
            foreach (object? _ in enumerable) count++;
            return count;
        }
        return 0;
    }

    private static List<CombatCard> ReadPile(object? pile)
    {
        var list = new List<CombatCard>();
        if (pile == null || GetMember(pile, "Cards", "_cards") is not IEnumerable cards) return list;

        foreach (object? card in cards)
        {
            if (card == null) continue;
            string? id = ExtractModelEntryId(card);
            if (string.IsNullOrEmpty(id)) continue;

            string title = GetMember(card, "Title", "_title") as string ?? id;
            string type = GetMember(card, "Type", "CardType", "_type")?.ToString() ?? "Other";
            int upgrade = GetMember(card, "CurrentUpgradeLevel", "_currentUpgradeLevel") is int u ? u : 0;
            bool permanent = GetMember(card, "_deckVersion", "DeckVersion") != null;

            var energy = GetMember(card, "EnergyCost", "_energyCost");
            bool costsX = energy != null && GetMember(energy, "CostsX", "_costsX") is bool x && x;
            int? cost = costsX ? null : EvaluateEffectiveCost(energy);

            list.Add(new CombatCard(id, title, type, cost, costsX, upgrade, permanent));
        }
        return list;
    }

    private static int? EvaluateEffectiveCost(object? energyCost)
    {
        if (energyCost == null) return null;
        int? baseCost = GetMember(energyCost, "_base", "Canonical") is int b ? b : null;
        if (baseCost.HasValue && baseCost.Value < 0) return null;

        try
        {
            // Try CardEnergyCost.GetWithModifiers(CostModifiers.All = 3) first for live turn/combat cost reductions
            var energyType = energyCost.GetType();
            var method = GetCachedMethod(energyType, "GetWithModifiers");
            if (method != null)
            {
                var enumVal = CostModifierEnumCache.GetOrAdd(energyType, static (_, m) =>
                {
                    var parms = m.GetParameters();
                    return parms.Length == 1 && parms[0].ParameterType.IsEnum
                        ? Enum.ToObject(parms[0].ParameterType, 3)
                        : null;
                }, method);

                if (enumVal != null && method.Invoke(energyCost, new[] { enumVal }) is int modified)
                {
                    return modified < 0 ? null : modified;
                }
            }
        }
        catch
        {
            // Fallback to baseCost below
        }

        return baseCost;
    }

    private static object? GetLocalPlayer()
    {
        var runMgrType = GetCachedType("MegaCrit.Sts2.Core.Runs.RunManager");
        var runMgr = runMgrType != null ? GetStaticMember(runMgrType, "Instance", "_instance") : null;
        var state = runMgr != null ? GetMember(runMgr, "State", "_state") : null;
        if (state == null || GetMember(state, "Players", "_players") is not IEnumerable players) return null;

        var ctxType = GetCachedType("MegaCrit.Sts2.Core.Context.LocalContext");
        var localNetId = ctxType != null ? GetStaticMember(ctxType, "NetId", "_netId") : null;

        object? first = null;
        foreach (object? p in players)
        {
            if (p == null) continue;
            first ??= p;
            if (localNetId != null && Equals(GetMember(p, "NetId", "_netId"), localNetId)) return p;
        }
        return first;
    }

    private static Type? GetCachedType(string fullName) =>
        TypeCache.GetOrAdd(fullName, static name => AccessTools.TypeByName(name));

    private static MethodInfo? GetCachedMethod(Type type, string methodName) =>
        MethodCache.GetOrAdd((type, methodName), static key => AccessTools.Method(key.Item1, key.Item2));

    private static object? GetStaticMember(Type type, params string[] names)
    {
        foreach (string name in names)
        {
            var member = StaticMemberCache.GetOrAdd((type, name), static key =>
            {
                var prop = AccessTools.Property(key.Item1, key.Item2);
                if (prop?.GetMethod != null && prop.GetMethod.IsStatic) return prop;
                var field = AccessTools.Field(key.Item1, key.Item2);
                if (field != null && field.IsStatic) return field;
                return null;
            });

            if (member is PropertyInfo p)
            {
                var val = p.GetValue(null);
                if (val != null) return val;
            }
            else if (member is FieldInfo f)
            {
                var val = f.GetValue(null);
                if (val != null) return val;
            }
        }
        return null;
    }

    private static object? GetMember(object target, params string[] names)
    {
        var type = target.GetType();
        foreach (string name in names)
        {
            var member = MemberCache.GetOrAdd((type, name), static key =>
            {
                var prop = AccessTools.Property(key.Item1, key.Item2);
                if (prop != null && prop.CanRead) return prop;
                return AccessTools.Field(key.Item1, key.Item2);
            });

            if (member is PropertyInfo p)
            {
                var val = p.GetValue(target);
                if (val != null) return val;
            }
            else if (member is FieldInfo f)
            {
                var val = f.GetValue(target);
                if (val != null) return val;
            }
        }
        return null;
    }
}
