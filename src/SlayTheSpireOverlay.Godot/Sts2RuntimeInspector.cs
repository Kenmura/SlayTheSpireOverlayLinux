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
            var enemyWillDie = new List<bool>();
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
                enemyWillDie.Add(OverlayCalculators.WillEnemyDieBeforeAttack(hp, InspectCreaturePowers(entity)));
            }

            object? player = GetLocalPlayer();
            var (playerHp, playerBlock) = GetLocalPlayerHpAndBlock(combatRoom, player);
            var (passiveBlock, selfDamage) = InspectPlayerEndOfTurnEffects(player, playerBlock);

            var allPlayers = GetAllPlayers();
            List<AllyAttackStatus>? allyStatuses = null;
            if (allPlayers.Count > 1)
            {
                var allyList = new List<AllyAttackStatus>();
                int allyIdx = 1;
                foreach (object p in allPlayers)
                {
                    if (ReferenceEquals(p, player)) continue;
                    string name = GetMember(p, "Name", "_name") as string
                        ?? ExtractModelEntryId(GetMember(p, "Character", "_character", "Hero"))
                        ?? $"Ally {allyIdx++}";
                    object? creature = GetMember(p, "Creature", "_creature") ?? p;
                    int allyHp = creature != null && GetMember(creature, "CurrentHp", "_currentHp") is int ah ? ah : 0;
                    int allyBlock = creature != null && GetMember(creature, "Block", "_block") is int ab ? ab : 0;
                    if (allyHp <= 0) continue;

                    int allyDmg = 0;
                    int allyHits = 0;
                    foreach (Node child in container.GetChildren())
                    {
                        if (child == null || !GodotObject.IsInstanceValid(child)) continue;
                        var entity = GetMember(child, "Entity", "_entity");
                        if (entity == null) continue;
                        int eHp = GetMember(entity, "CurrentHp", "_currentHp") is int i ? i : 0;
                        if (eHp <= 0) continue;
                        if (OverlayCalculators.WillEnemyDieBeforeAttack(eHp, InspectCreaturePowers(entity))) continue;

                        if (GetMember(child, "IntentContainer", "_intentContainer") is Node intentContainer &&
                            GodotObject.IsInstanceValid(intentContainer))
                        {
                            foreach (Node intentNode in intentContainer.GetChildren())
                            {
                                if (intentNode is CanvasItem ci && !ci.Visible) continue;
                                var intentModel = GetMember(intentNode, "_intent", "Intent");
                                string? type = intentModel?.GetType().Name;
                                if (string.IsNullOrEmpty(type) || (!type.Contains("AttackIntent", StringComparison.OrdinalIgnoreCase) &&
                                    !type.EndsWith("DeathBlowIntent", StringComparison.OrdinalIgnoreCase)))
                                {
                                    continue;
                                }

                                var target = (intentModel != null ? GetMember(intentModel, "Target", "_target", "TargetCreature", "_targetCreature") : null)
                                    ?? GetMember(intentNode, "Target", "_target");

                                bool targetsThisAlly = target != null && (ReferenceEquals(target, creature) || ReferenceEquals(target, p));
                                if (targetsThisAlly)
                                {
                                    var labelNode = GetMember(intentNode, "_valueLabel", "ValueLabel");
                                    string? text = labelNode is RichTextLabel rtl ? rtl.Text : (labelNode is Label lbl ? lbl.Text : null);
                                    var (dmg, hits) = OverlayCalculators.ParseAttackIntentValues(text);
                                    allyDmg += dmg;
                                    allyHits += hits;
                                }
                            }
                        }
                    }

                    allyList.Add(OverlayCalculators.CalculateAllyAttackStatus(name, allyHp, allyBlock, allyDmg, allyHits));
                }

                if (allyList.Count > 0)
                {
                    allyStatuses = allyList;
                }
            }

            OutgoingDamageSummary? outgoingDamage = null;
            if (ModEntry.Config.EnableOutgoingDamageSummary)
            {
                try
                {
                    object? combatState = player != null ? GetMember(player, "PlayerCombatState", "_playerCombatState") : null;
                    var handPile = combatState != null ? GetMember(combatState, "Hand", "_hand") : null;
                    object? energyObj = combatState != null ? GetMember(combatState, "Energy", "_energy") : null;
                    int? availableEnergy = energyObj is int e ? e
                        : (energyObj != null && GetMember(energyObj, "Energy", "_energy", "Current", "Amount") is int ce ? ce : null);
                    if (handPile != null && availableEnergy.HasValue && GetMember(handPile, "Cards", "_cards") is IEnumerable handCards)
                    {

                        var attackCards = new List<PlayableAttackCard>();
                        foreach (object? card in handCards)
                        {
                            if (card == null) continue;
                            string type = GetMember(card, "Type", "CardType", "_type")?.ToString() ?? "";
                            if (!string.Equals(type, "Attack", StringComparison.OrdinalIgnoreCase)) continue;

                            string title = GetMember(card, "Title", "_title") as string ?? ExtractModelEntryId(card) ?? "Attack";
                            if (!CanPlayCard(card)) continue;
                            int dmg = EvaluateCardDamage(card);
                            if (dmg <= 0) continue;

                            var energyCostObj = GetMember(card, "EnergyCost", "_energyCost");
                            bool costsX = energyCostObj != null && GetMember(energyCostObj, "CostsX", "_costsX") is bool x && x;
                            int? resolvedCost = costsX ? 0 : EvaluateEffectiveCost(energyCostObj);
                            if (!resolvedCost.HasValue) continue; // unknown cost: never assume playable-for-free
                            int cost = resolvedCost.Value;

                            string targetStr = GetMember(card, "TargetType", "_targetType", "TargetingType", "Target")?.ToString() ?? "";
                            bool isAoe = targetStr.Contains("All", StringComparison.OrdinalIgnoreCase);
                            int hits = EvaluateCardHits(card);

                            attackCards.Add(new PlayableAttackCard(title, cost, dmg, hits, costsX, isAoe));
                        }

                        var enemyTargets = new List<EnemyTarget>();
                        foreach (Node child in container.GetChildren())
                        {
                            if (child == null || !GodotObject.IsInstanceValid(child)) continue;
                            var entity = GetMember(child, "Entity", "_entity");
                            if (entity == null) continue;
                            int eHp = GetMember(entity, "CurrentHp", "_currentHp") is int i ? i : 0;
                            int eBlock = GetMember(entity, "Block", "_block") is int b ? b : 0;
                            if (eHp <= 0) continue;
                            string eName = GetMember(entity, "Name", "_name") as string ?? ExtractModelEntryId(entity) ?? "Enemy";
                            enemyTargets.Add(new EnemyTarget(eName, eHp, eBlock));
                        }

                        if (attackCards.Count > 0)
                        {
                            outgoingDamage = OverlayCalculators.CalculateMaxOutgoingHandDamage(availableEnergy.Value, attackCards, enemyTargets);
                        }
                    }
                }
                catch
                {
                }
            }

            return OverlayCalculators.CalculateIncomingAttacks(
                enemies,
                playerBlock,
                playerHp,
                passiveBlock,
                selfDamage,
                enemyWillDie,
                allyStatuses,
                outgoingDamage);
        }
        catch
        {
            return new AttackSummary(0, 0, 0);
        }
    }

    private static int ReadDynamicVarInt(object? dynVars, string name)
    {
        try
        {
            object? v = dynVars != null ? GetMember(dynVars, name) : null;
            if (v == null) return 0;
            foreach (string member in new[] { "PreviewValue", "EnchantedValue", "BaseValue" })
            {
                object? raw = GetMember(v, member);
                int n = raw switch { decimal m => (int)decimal.Truncate(m), int i => i, _ => 0 };
                if (n > 0) return n;
            }
            return GetMember(v, "IntValue") is int iv ? iv : 0;
        }
        catch
        {
            return 0;
        }
    }

    // CardModel exposes damage through DynamicVars.Damage (decimal PreviewValue includes Strength/Vulnerable/Weak).
    private static int EvaluateCardDamage(object? card)
    {
        if (card == null) return 0;
        var vars = GetMember(card, "DynamicVars", "_dynamicVars");
        foreach (string name in new[] { "Damage", "CalculatedDamage", "OstyDamage", "ExtraDamage" })
        {
            int dmg = ReadDynamicVarInt(vars, name);
            if (dmg > 0) return dmg;
        }
        return 0;
    }

    private static int EvaluateCardHits(object? card)
    {
        int repeat = card == null ? 0 : ReadDynamicVarInt(GetMember(card, "DynamicVars", "_dynamicVars"), "Repeat");
        return Math.Max(1, repeat);
    }

    private static bool CanPlayCard(object card)
    {
        try
        {
            var m = card.GetType().GetMethod("CanPlay", Type.EmptyTypes);
            return m == null || m.Invoke(card, null) is not bool playable || playable;
        }
        catch
        {
            return false;
        }
    }

    private static List<(string? PowerId, int Amount)> InspectCreaturePowers(object? creature)
    {
        var powers = new List<(string? PowerId, int Amount)>();
        if (creature == null) return powers;
        try
        {
            if (GetMember(creature, "Powers", "_powers") is IEnumerable powerList)
            {
                foreach (object? power in powerList)
                {
                    if (power == null) continue;
                    string? id = ExtractModelEntryId(power) ?? power.GetType().Name;
                    int amount = GetMember(power, "Amount", "_amount", "Stacks") is int a ? a : 0;
                    powers.Add((id, amount));
                }
            }
        }
        catch
        {
        }
        return powers;
    }

    private static (int PassiveBlock, int SelfDamage) InspectPlayerEndOfTurnEffects(object? player, int currentPlayerBlock)
    {
        if (player == null) return (0, 0);
        try
        {
            object? creature = GetMember(player, "Creature", "_creature") ?? player;
            var effects = InspectCreaturePowers(creature);

            if (GetMember(player, "Relics", "_relics") is IEnumerable relics)
            {
                foreach (object? relic in relics)
                {
                    if (relic == null) continue;
                    string? id = ExtractModelEntryId(relic) ?? relic.GetType().Name;
                    int amount = GetMember(relic, "Counter", "_counter", "Amount") is int c ? c : 0;
                    effects.Add((id, amount));
                }
            }

            object? combatState = GetMember(player, "PlayerCombatState", "_playerCombatState");
            if (combatState != null)
            {
                object? orbContainer = GetMember(combatState, "OrbQueue", "_orbQueue") ?? combatState;
                if (GetMember(orbContainer, "Orbs", "_orbs") is IEnumerable orbs)
                {
                    foreach (object? orb in orbs)
                    {
                        if (orb == null) continue;
                        string? id = ExtractModelEntryId(orb) ?? orb.GetType().Name;
                        int passive = GetMember(orb, "PassiveVal", "PassiveAmount", "_passiveAmount", "Amount") is int p ? p : 0;
                        effects.Add((id, passive));
                    }
                }
            }

            var handCards = new List<(string? CardId, int UpgradeLevel)>();
            var handPile = combatState != null ? GetMember(combatState, "Hand", "_hand") : null;
            if (handPile != null && GetMember(handPile, "Cards", "_cards") is IEnumerable cards)
            {
                foreach (object? card in cards)
                {
                    if (card == null) continue;
                    string? id = ExtractModelEntryId(card);
                    int upgrade = GetMember(card, "CurrentUpgradeLevel", "_currentUpgradeLevel") is int u ? u : 0;
                    handCards.Add((id, upgrade));
                }
            }

            return OverlayCalculators.CalculatePlayerEndOfTurnEffects(currentPlayerBlock, effects, handCards);
        }
        catch
        {
            return (0, 0);
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
            int nextHandSampleSize = InspectNextTurnDrawCount(player, combatState);
            return OverlayCalculators.CalculateDeckTracker(
                ReadPile(GetMember(combatState, "DrawPile", "_drawPile")),
                ReadPile(GetMember(combatState, "DiscardPile", "_discardPile")),
                ReadPile(GetMember(combatState, "Hand", "_hand")),
                exhaustCount,
                nextHandSampleSize);
        }
        catch
        {
            return null;
        }
    }

    public static DeckSynergySummary? InspectMasterDeckSynergy()
    {
        try
        {
            object? player = GetLocalPlayer();
            if (player == null) return null;
            object? deck = GetMember(player, "Deck", "_deck", "MasterDeck");
            if (deck == null) return null;

            var cards = new List<DeckCardInfo>();
            if (GetMember(deck, "Cards", "_cards") is IEnumerable cardList)
            {
                foreach (object? card in cardList)
                {
                    if (card == null) continue;
                    string? id = ExtractModelEntryId(card);
                    if (string.IsNullOrEmpty(id)) continue;

                    string title = GetMember(card, "Title", "_title") as string ?? id;
                    string type = GetMember(card, "Type", "CardType", "_type")?.ToString() ?? "Other";
                    int upgrade = GetMember(card, "CurrentUpgradeLevel", "_currentUpgradeLevel") is int u ? u : 0;
                    var energy = GetMember(card, "EnergyCost", "_energyCost");
                    bool costsX = energy != null && GetMember(energy, "CostsX", "_costsX") is bool x && x;
                    int? cost = costsX ? null : EvaluateEffectiveCost(energy);

                    string? desc = GetMember(card, "Description", "_description", "RawDescription") as string;
                    var keywords = new List<string>();
                    if (GetMember(card, "Keywords", "_keywords") is IEnumerable kwList)
                    {
                        foreach (object? kw in kwList)
                        {
                            string? kwId = ExtractModelEntryId(kw) ?? kw?.ToString();
                            if (!string.IsNullOrEmpty(kwId)) keywords.Add(kwId);
                        }
                    }

                    cards.Add(new DeckCardInfo(id, title, type, cost, costsX, upgrade, keywords, desc));
                }
            }

            return DeckSynergyAnalyzer.AnalyzeDeck(cards);
        }
        catch
        {
            return null;
        }
    }

    public static bool IsCardRewardScreenActive(Node? contextNode)
    {
        if (contextNode == null || !GodotObject.IsInstanceValid(contextNode)) return false;
        try
        {
            var root = contextNode.GetTree()?.CurrentScene ?? contextNode;
            return CheckNodeForCardRewardRecursive(root, 0, 7);
        }
        catch
        {
            return false;
        }
    }

    private static bool CheckNodeForCardRewardRecursive(Node node, int depth, int maxDepth)
    {
        if (node == null || !GodotObject.IsInstanceValid(node) || depth > maxDepth) return false;
        if (node is CanvasItem ci && !ci.Visible) return false;

        string type = node.GetType().Name;
        string name = node.Name.ToString();
        if (type.Contains("CardReward", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("CardReward", StringComparison.OrdinalIgnoreCase) ||
            type.Contains("RewardSelection", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("RewardSelection", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (Node child in node.GetChildren())
        {
            if (CheckNodeForCardRewardRecursive(child, depth + 1, maxDepth)) return true;
        }
        return false;
    }

    public static BossPrepInfo? InspectCurrentBoss()
    {
        try
        {
            var runMgrType = GetCachedType("MegaCrit.Sts2.Core.Runs.RunManager");
            var runMgr = runMgrType != null ? GetStaticMember(runMgrType, "Instance", "_instance") : null;
            var state = runMgr != null ? GetMember(runMgr, "State", "_state") : null;
            if (state == null) return null;

            object? act = GetMember(state, "CurrentAct", "Act", "_act");
            if (act == null && GetMember(state, "Acts", "_acts") is IList actList)
            {
                int actIdx = GetMember(state, "CurrentActIndex", "_currentActIndex") is int idx ? idx : 0;
                if (actIdx >= 0 && actIdx < actList.Count)
                {
                    act = actList[actIdx];
                }
            }
            if (act == null) return null;

            object? boss = GetMember(act, "BossEncounter", "_bossEncounter", "Boss", "_boss", "BossId", "_bossId", "Encounter");
            string? rawId = ExtractModelEntryId(boss) ?? boss?.ToString();
            string? title = boss != null ? (GetMember(boss, "Title", "_title", "Name", "_name") as string) : null;
            bool isFogged = IsBossFogged();

            if (string.IsNullOrEmpty(rawId) && !isFogged) return null;

            return OverlayCalculators.ResolveBossPrep(rawId, title, isFogged);
        }
        catch
        {
            return null;
        }
    }

    public static bool IsBossFogged()
    {
        try
        {
            var mapFogModType = AccessTools.TypeByName("SlayTheSpireMapFog.Godot.ModEntry");
            if (mapFogModType != null)
            {
                var cfg = AccessTools.Property(mapFogModType, "Config")?.GetValue(null);
                if (cfg != null)
                {
                    bool enabled = AccessTools.Property(cfg.GetType(), "Enabled")?.GetValue(cfg) is bool e && e;
                    bool showBoss = AccessTools.Property(cfg.GetType(), "ShowBoss")?.GetValue(cfg) is bool sb && sb;
                    if (enabled && !showBoss)
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
        }
        return false;
    }

    private static int InspectNextTurnDrawCount(object? player, object? combatState)
    {
        try
        {
            int baseDraw = 5;
            if (combatState != null && GetMember(combatState, "BaseCardDraw", "_baseCardDraw", "CardsPerTurn") is int csDraw && csDraw > 0)
            {
                baseDraw = csDraw;
            }
            else if (player != null && GetMember(player, "MaxHandDraw", "_maxHandDraw", "BaseCardDraw") is int pDraw && pDraw > 0)
            {
                baseDraw = pDraw;
            }

            object? creature = player != null ? (GetMember(player, "Creature", "_creature") ?? player) : null;
            return OverlayCalculators.CalculateEffectiveDrawCount(baseDraw, InspectCreaturePowers(creature));
        }
        catch
        {
            return 5;
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

    public static ShopBudgetSummary? InspectMerchantBudget(Node merchantRoom, bool toggleHoveredPin = false)
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

            var slotNodes = new List<Node>();
            string[] containers = { "_characterCardContainer", "_colorlessCardContainer", "_relicContainer", "_potionContainer" };
            foreach (string name in containers)
            {
                if (GetMember(inventory, name) is Node c && GodotObject.IsInstanceValid(c))
                {
                    foreach (Node slot in c.GetChildren())
                    {
                        if (slot != null && GodotObject.IsInstanceValid(slot))
                        {
                            slotNodes.Add(slot);
                        }
                    }
                }
            }
            if (GetMember(inventory, "_cardRemovalNode", "CardRemovalNode") is Node rem && GodotObject.IsInstanceValid(rem))
            {
                slotNodes.Add(rem);
            }

            bool pinToggled = false;
            var slots = new List<(int Cost, bool IsStocked, bool IsHovered, bool IsPinned)>();

            foreach (var slot in slotNodes)
            {
                bool hovered = GetMember(slot, "_isHovered", "IsHovered") is bool h && h;
                var entry = GetMember(slot, "Entry", "_entry") ?? slot;
                int cost = GetMember(entry, "Cost", "_cost") is int c ? c : 0;
                bool stocked = GetMember(entry, "IsStocked", "_isStocked") is bool s ? s : true;

                bool isPinned = slot.HasMeta("IsPinned") && slot.GetMeta("IsPinned").AsBool();

                // AC3: Auto-clear on purchase if slot is no longer stocked
                if (!stocked && isPinned)
                {
                    isPinned = false;
                    slot.SetMeta("IsPinned", false);
                    UI.RoomOverlayFactory.UpdateShopSlotPinBadge(slot, false);
                }

                // AC2: Toggle hovered slot
                if (toggleHoveredPin && !pinToggled && hovered && stocked)
                {
                    isPinned = !isPinned;
                    slot.SetMeta("IsPinned", isPinned);
                    pinToggled = true;
                }

                // Ensure visual pin badge matches current state
                UI.RoomOverlayFactory.UpdateShopSlotPinBadge(slot, isPinned && stocked);

                slots.Add((cost, stocked, hovered, isPinned));
            }

            return OverlayCalculators.CalculateShopBudget(gold, slots);
        }
        catch
        {
            return null;
        }
    }

    public static (int RemainingGold, int Cost)? InspectMerchantGoldLeft(Node merchantRoom)
    {
        var budget = InspectMerchantBudget(merchantRoom, toggleHoveredPin: false);
        if (budget != null && budget.HoveredCost.HasValue && budget.HoveredRemainingGold.HasValue)
        {
            return (budget.HoveredRemainingGold.Value, budget.HoveredCost.Value);
        }
        return null;
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

    public static void InspectCardUpgradeAndEnchantment(object? cardNode, out int upgradeLevel, out string? enchantmentId)
    {
        upgradeLevel = 0;
        enchantmentId = null;
        if (cardNode == null) return;

        try
        {
            var model = GetMember(cardNode, "Model", "_model") ?? cardNode;
            if (GetMember(model, "CurrentUpgradeLevel", "_currentUpgradeLevel", "UpgradeLevel") is int lvl && lvl > 0)
            {
                upgradeLevel = lvl;
            }
            else if (GetMember(model, "IsUpgraded", "_isUpgraded") is bool upg && upg)
            {
                upgradeLevel = 1;
            }

            var enchantment = GetMember(model, "Enchantment", "_enchantment");
            if (enchantment != null)
            {
                enchantmentId = ExtractModelEntryId(enchantment);
            }
        }
        catch
        {
        }
    }

    public static string? ExtractRewardItemId(
        object? rewardButton,
        out bool isCard,
        out bool isRelic,
        out bool isPotion,
        out int upgradeLevel,
        out string? enchantmentId)
    {
        isCard = false;
        isRelic = false;
        isPotion = false;
        upgradeLevel = 0;
        enchantmentId = null;
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
                var card = GetMember(reward, "Card", "_card");
                InspectCardUpgradeAndEnchantment(card, out upgradeLevel, out enchantmentId);
                return ExtractModelEntryId(card);
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

            list.Add(new CombatCard(id, title, type, cost, costsX, upgrade, permanent, ReadCardDescription(card, id, upgrade)));
        }
        return list;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> DescriptionCache = new();

    // CardModel.Description is only the raw LocString template; GetDescriptionForPile injects dynamic vars,
    // upgrade/keyword/enchantment text and returns the final formatted string.
    private static string ReadCardDescription(object card, string id, int upgrade)
    {
        string key = $"{id}:{upgrade}";
        if (DescriptionCache.TryGetValue(key, out string? cached)) return cached;

        string result = "";
        try
        {
            var method = card.GetType().GetMethods()
                .Where(m => m.Name == "GetDescriptionForPile" && m.GetParameters().Length == 2)
                .FirstOrDefault();
            if (method != null)
            {
                var ps = method.GetParameters();
                object? pile = ps[0].ParameterType.IsEnum
                    ? (Enum.TryParse(ps[0].ParameterType, "Draw", true, out object? parsed) ? parsed : Activator.CreateInstance(ps[0].ParameterType))
                    : null;
                object? owner = GetMember(card, "Owner", "_owner");
                object? creature = owner != null ? GetMember(owner, "Creature", "_creature") : null;
                result = method.Invoke(card, new[] { pile, creature }) as string ?? "";
            }
        }
        catch
        {
            result = "";
        }

        result = OverlayCalculators.StripBbCode(result);
        if (DescriptionCache.Count > 1024) DescriptionCache.Clear();
        if (result.Length > 0) DescriptionCache[key] = result; // don't cache failures so a later poll can retry
        return result;
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

    private static List<object> GetAllPlayers()
    {
        var list = new List<object>();
        var runMgrType = GetCachedType("MegaCrit.Sts2.Core.Runs.RunManager");
        var runMgr = runMgrType != null ? GetStaticMember(runMgrType, "Instance", "_instance") : null;
        var state = runMgr != null ? GetMember(runMgr, "State", "_state") : null;
        if (state != null && GetMember(state, "Players", "_players") is IEnumerable players)
        {
            foreach (object? p in players)
            {
                if (p != null) list.Add(p);
            }
        }
        return list;
    }

    internal static object? GetLocalPlayer()
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
                try
                {
                    var val = p.GetValue(target);
                    if (val != null) return val;
                }
                catch
                {
                    // Property threw before value was assigned (e.g. Model was accessed before it was set)
                }
            }
            else if (member is FieldInfo f)
            {
                try
                {
                    var val = f.GetValue(target);
                    if (val != null) return val;
                }
                catch
                {
                }
            }
        }
        return null;
    }
}
