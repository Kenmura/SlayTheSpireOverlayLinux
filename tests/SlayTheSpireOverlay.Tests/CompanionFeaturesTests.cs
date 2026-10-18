using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SlayTheSpireOverlay.Core.Services;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class CompanionFeaturesTests
{
    [TestMethod]
    public void NormalizeId_And_ExtractEventOptionId_ShouldResolveCorrectKeys()
    {
        Assert.AreEqual("GO_FOR_THE_EYES", OverlayCalculators.NormalizeId("CARD:GoForTheEyes"));
        Assert.AreEqual("AKABEKO", OverlayCalculators.NormalizeId("RELIC:Akabeko"));
        Assert.AreEqual("LAVA_ROCK", OverlayCalculators.NormalizeId("LavaRockOption", stripOptionSuffix: true));
        Assert.AreEqual("SCROLL_BOXES", OverlayCalculators.ExtractEventOptionId(null, null, "ANCIENT.CHOICES.SCROLL_BOXES_OPTION"));
    }

    [TestMethod]
    public void CalculateIncomingAttacks_ShouldStripBbCodeAndSumLivingEnemyAttacks()
    {
        var enemies = new List<(int CurrentHp, IEnumerable<(string? IntentType, string? LabelText)> Intents)>
        {
            (45, new (string?, string?)[] { ("MultiAttackIntent", "[center][color=#ff4444]6x3[/color][/center]") }),
            (30, new (string?, string?)[] { ("SingleAttackIntent", "[b]14[/b]"), ("BuffIntent", "99") }),
            (0,  new (string?, string?)[] { ("DeathBlowIntent", "50") }) // Dead enemy ignored
        };

        var summary = OverlayCalculators.CalculateIncomingAttacks(enemies, playerBlock: 12, playerHp: 20);

        Assert.AreEqual(32, summary.TotalDamage);
        Assert.AreEqual(4, summary.TotalHits);
        Assert.AreEqual(2, summary.AttackingEnemies);
        Assert.AreEqual(20, summary.UnblockedDamage);
        Assert.IsTrue(summary.IsLethal);

        var blockedSummary = OverlayCalculators.CalculateIncomingAttacks(enemies, playerBlock: 40, playerHp: 20);
        Assert.AreEqual(0, blockedSummary.UnblockedDamage);
        Assert.IsFalse(blockedSummary.IsLethal);
    }

    [TestMethod]
    public void CalculateDeckTracker_ShouldComputeDrawPileProbabilities()
    {
        var draw = new List<CombatCard>
        {
            new("STRIKE", "[color=#ffcc00]Strike[/color]", "Attack", 1, false, 0),
            new("STRIKE", "[color=#ffcc00]Strike[/color]", "Attack", 1, false, 0),
            new("BASH", "Bash+", "Attack", 2, false, 1),
            new("DEFEND", "Defend", "Skill", 1, false, 0)
        };
        var discard = new List<CombatCard>
        {
            new("ANGER", "Anger", "Attack", 0, false, 0),
            new("DEFEND", "Defend", "Skill", 1, false, 0)
        };
        var hand = new List<CombatCard>
        {
            new("SHIV", "Shiv", "Attack", 0, false, 0, IsPermanent: false),
            new("WOUND", "Wound", "Status", null, false, 0, IsPermanent: false)
        };

        var state = OverlayCalculators.CalculateDeckTracker(draw, discard, hand, exhaustCount: 2);

        Assert.AreEqual(4, state.DrawCount);
        Assert.AreEqual(2, state.HandCount);
        Assert.AreEqual(2, state.DiscardCount);
        Assert.AreEqual(2, state.ExhaustCount);
        var attacks = state.Categories.First(c => c.Category == "Attack");
        Assert.AreEqual(3, attacks.InDraw);
        Assert.AreEqual(4, attacks.Total);
        Assert.AreEqual(75, attacks.ProbabilityPercent);
        Assert.AreEqual("Strike", attacks.Cards[0].Title);
        Assert.AreEqual("Bash+", attacks.Cards[1].Title);
        Assert.AreEqual("Anger", attacks.Cards[^1].Title);
        Assert.AreEqual(0, attacks.Cards[^1].InDraw);
        Assert.AreEqual(50, attacks.Cards[^1].NextHandPercent); // 4 in draw + 1 post-reshuffle draw from 2 discard cards = 50%
        Assert.AreEqual(100, attacks.Cards[0].NextHandPercent);
        var other = state.Categories.First(c => c.Category == "Other");
        Assert.AreEqual(0, other.InDraw);
        Assert.AreEqual(1, other.Total);
        Assert.IsNull(other.Cards[0].Cost);
        Assert.AreEqual(50, OverlayCalculators.CalculateHypergeometricAtLeastOne(populationSize: 10, successesInPopulation: 1, sampleSize: 5));
    }

    [TestMethod]
    public void CalculateGoldLeft_ShouldReturnRemainingOrDeficitGoldWhenHoveredAndStocked()
    {
        var slots = new[] { (Cost: 165, IsStocked: true, IsHovered: true) };

        var affordable = OverlayCalculators.CalculateGoldLeft(250, slots);
        Assert.IsNotNull(affordable);
        Assert.AreEqual(85, affordable.Value.RemainingGold);

        var unaffordable = OverlayCalculators.CalculateGoldLeft(100, slots);
        Assert.IsNotNull(unaffordable);
        Assert.AreEqual(-65, unaffordable.Value.RemainingGold);

        var unhovered = OverlayCalculators.CalculateGoldLeft(250, new[] { (Cost: 165, IsStocked: true, IsHovered: false) });
        Assert.IsNull(unhovered);
    }

    [TestMethod]
    public void ParseIntoDictionary_ShouldParseLiveUntappedApiJson()
    {
        string untappedJson = """
        {
          "game_version": "0.100.0",
          "tier_list": {
            "name": "Silent Tier List",
            "tiers": [
              {
                "name": "S",
                "cards": [{ "card_id": "ADRENALINE", "commentary": "Burst energy and draw." }],
                "relics": [{ "item_id": "KUNAI", "commentary": "Dexterity scaling." }],
                "potions": [{ "item_id": "GHOST_IN_A_JAR", "commentary": "Intangible." }]
              }
            ]
          }
        }
        """;

        var dict = new Dictionary<string, SlayTheSpireOverlay.Core.Models.CardTierData>();
        HttpTierListProvider.ParseIntoDictionary(untappedJson, dict);

        Assert.HasCount(3, dict);
        Assert.AreEqual("S", dict["ADRENALINE"].Tier);
        Assert.AreEqual(95.0, dict["ADRENALINE"].Score);
        Assert.AreEqual("S", dict["KUNAI"].Tier);
        Assert.AreEqual("S", dict["GHOST_IN_A_JAR"].Tier);

        string orderedTierJson = """
        {
          "tier_list": {
            "tiers": [
              {
                "name": "Always Amazing",
                "cards": [
                  { "card_id": "OFFERING", "commentary": "Top of tier" },
                  { "card_id": "IMPERVIOUS", "commentary": "Middle of tier" },
                  { "card_id": "REAPER", "commentary": "Bottom of tier" }
                ]
              }
            ]
          }
        }
        """;
        HttpTierListProvider.ParseIntoDictionary(orderedTierJson, dict);
        Assert.IsGreaterThan(dict["IMPERVIOUS"].Score, dict["OFFERING"].Score);
        Assert.IsGreaterThan(dict["REAPER"].Score, dict["IMPERVIOUS"].Score);
        Assert.AreEqual(95.0, dict["IMPERVIOUS"].Score);
    }

    [TestMethod]
    public void GetUuidsForAuthor_And_ParseEventCommentary_ShouldSupportCreatorsAndEvents()
    {
        var baalorUuids = HttpTierListProvider.GetUuidsForAuthor("Baalorlord");
        var jorbsUuids = HttpTierListProvider.GetUuidsForAuthor("Jorbs");

        Assert.HasCount(6, baalorUuids);
        Assert.HasCount(6, jorbsUuids);
        CollectionAssert.AreNotEqual(baalorUuids, jorbsUuids);
        Assert.IsNull(HttpTierListProvider.GetCacheFileOverride("Baalorlord", "untapped"));
        Assert.AreEqual("tier_list_cache_jorbs.json", HttpTierListProvider.GetCacheFileOverride("Jorbs", "untapped"));

        string eventsJson = """
        {
          "Baalorlord": {
            "SHRINE_OF_LIGHT": "Upgrade your highest-impact scaling card here."
          }
        }
        """;

        var dict = new Dictionary<string, SlayTheSpireOverlay.Core.Models.CardTierData>();
        int count = HttpTierListProvider.ParseEventCommentary(eventsJson, "Jorbs", dict);

        Assert.AreEqual(1, count);
        Assert.IsTrue(dict.ContainsKey("EVENT_SHRINE_OF_LIGHT"));
        Assert.AreEqual("Baalorlord", dict["EVENT_SHRINE_OF_LIGHT"].Tier);
        Assert.AreEqual("Upgrade your highest-impact scaling card here.", dict["EVENT_SHRINE_OF_LIGHT"].Commentary);
    }

    [TestMethod]
    public void NormalizeKeybindName_And_OverlayConfigDefaults_ShouldValidateAndFallback()
    {
        var cfg = new SlayTheSpireOverlay.Core.Options.OverlayConfig();
        Assert.IsTrue(cfg.EnableQuickToggleBar);
        Assert.IsTrue(cfg.EnableControllerChords);
        Assert.AreEqual("F6", cfg.ToggleAuthorKey);
        Assert.AreEqual("F7", cfg.CollapseDeckTrackerKey);
        Assert.AreEqual("F8", cfg.ToggleOverlayKey);
        Assert.AreEqual("F9", cfg.RefreshTiersKey);

        Assert.AreEqual("F6", OverlayCalculators.NormalizeKeybindName(null, "F6"));
        Assert.AreEqual("F7", OverlayCalculators.NormalizeKeybindName("   ", "F7"));
        Assert.AreEqual("F10", OverlayCalculators.NormalizeKeybindName("Key.f10", "F8"));
        Assert.AreEqual("F12", OverlayCalculators.NormalizeKeybindName("KEY_F12", "F9"));
        Assert.AreEqual("K", OverlayCalculators.NormalizeKeybindName("k", "F6"));
        Assert.AreEqual("5", OverlayCalculators.NormalizeKeybindName("5", "F7"));
        Assert.AreEqual("TAB", OverlayCalculators.NormalizeKeybindName("tab", "F8"));
        Assert.AreEqual("F8", OverlayCalculators.NormalizeKeybindName("F25", "F8"));
        Assert.AreEqual("F8", OverlayCalculators.NormalizeKeybindName("F0", "F8"));
        Assert.AreEqual("F8", OverlayCalculators.NormalizeKeybindName("INVALID_KEY_999", "F8"));
        Assert.AreEqual("F8", OverlayCalculators.NormalizeKeybindName("Key.", "F8"));
    }

    [TestMethod]
    public void UpgradeAndEnchantmentEvaluation_ShouldFallbackAndExtractUpgradeNotes()
    {
        var tiers = new Dictionary<string, SlayTheSpireOverlay.Core.Models.CardTierData>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["BASH"] = new("BASH", "C", 50.0, "Expensive vulnerable source. Upgraded version adds an extra turn of Vulnerable which makes it much smoother."),
            ["TRUE_GRIT"] = new("TRUE_GRIT", "B", 65.0, "Random exhaust at base."),
            ["TRUE_GRIT+"] = new("TRUE_GRIT+", "A", 82.0, "Targeted exhaust when upgraded is fantastic.")
        };

        var baseBash = OverlayCalculators.ResolveCardTierData(tiers, "CARD:Bash", upgradeLevel: 0);
        Assert.IsNotNull(baseBash);
        Assert.AreEqual("C", baseBash.Tier);

        var upgBash = OverlayCalculators.ResolveCardTierData(tiers, "CARD:Bash", upgradeLevel: 1);
        Assert.IsNotNull(upgBash);
        Assert.AreEqual("C", upgBash.Tier);

        var upgGrit = OverlayCalculators.ResolveCardTierData(tiers, "CARD:TrueGrit", upgradeLevel: 1);
        Assert.IsNotNull(upgGrit);
        Assert.AreEqual("A", upgGrit.Tier);
        Assert.AreEqual(82.0, upgGrit.Score);

        Assert.AreEqual("C+", OverlayCalculators.FormatBadgeTierLabel("C", upgradeLevel: 1));
        Assert.AreEqual("C+2 ✦", OverlayCalculators.FormatBadgeTierLabel("C", upgradeLevel: 2, enchantmentId: "Swift"));

        string extracted = OverlayCalculators.ExtractUpgradeCommentary(baseBash.Commentary);
        Assert.AreEqual("Upgraded version adds an extra turn of Vulnerable which makes it much smoother.", extracted);

        string hoverBase = OverlayCalculators.FormatCardHoverCommentary("C", baseBash.Commentary, "Baalorlord", upgradeLevel: 0);
        Assert.DoesNotContain("★ Upgrade Note:", hoverBase);

        string hoverUpg = OverlayCalculators.FormatCardHoverCommentary("C", baseBash.Commentary, "Baalorlord", upgradeLevel: 1, enchantmentId: "Swift");
        StringAssert.Contains(hoverUpg, "[Baalorlord · C Tier · Upgraded + · Enchanted: SWIFT]");
        StringAssert.Contains(hoverUpg, "★ Upgrade Note: Upgraded version adds an extra turn of Vulnerable which makes it much smoother.");

        // Ensure numeric "3+ enemies" does not trigger false-positive upgrade note, while "Bash+" does
        Assert.AreEqual(string.Empty, OverlayCalculators.ExtractUpgradeCommentary("Great AoE against 3+ enemies in Act 2."));
        Assert.AreEqual("Bash+ is much better.", OverlayCalculators.ExtractUpgradeCommentary("Slow at base. Bash+ is much better."));
        Assert.AreEqual("A", OverlayCalculators.ResolveCardTierData(tiers, "TRUE_GRIT+2", upgradeLevel: 2)?.Tier);
    }

    [TestMethod]
    public void CalculateDeckTracker_And_EffectiveDrawCount_ShouldAdjustForDynamicSampleSizes()
    {
        Assert.AreEqual(5, OverlayCalculators.CalculateEffectiveDrawCount(5, null));
        Assert.AreEqual(0, OverlayCalculators.CalculateEffectiveDrawCount(0, null));
        Assert.AreEqual(7, OverlayCalculators.CalculateEffectiveDrawCount(5, new (string?, int)[]
        {
            ("MachineLearningPower", 1),
            ("DrawCardNextTurnPower", 1)
        }));
        Assert.AreEqual(4, OverlayCalculators.CalculateEffectiveDrawCount(5, new (string?, int)[]
        {
            ("DrawReductionPower", 1)
        }));
        Assert.AreEqual(10, OverlayCalculators.CalculateEffectiveDrawCount(5, new (string?, int)[]
        {
            ("DrawCardNextTurnPower", 8)
        }));

        var draw = new List<CombatCard>
        {
            new("STRIKE", "Strike", "Attack", 1, false, 0),
            new("DEFEND", "Defend", "Skill", 1, false, 0),
            new("DEFEND", "Defend", "Skill", 1, false, 0),
            new("DEFEND", "Defend", "Skill", 1, false, 0),
            new("DEFEND", "Defend", "Skill", 1, false, 0),
            new("DEFEND", "Defend", "Skill", 1, false, 0)
        };
        var discard = new List<CombatCard>
        {
            new("BASH", "Bash", "Attack", 2, false, 0),
            new("POMMEL", "Pommel Strike", "Attack", 1, false, 0)
        };

        // Sample size 0 -> 0% odds
        var state0 = OverlayCalculators.CalculateDeckTracker(draw, discard, null, nextHandSampleSize: 0);
        Assert.AreEqual(0, state0.NextHandSampleSize);
        Assert.AreEqual(0, state0.Categories.First(c => c.Category == "Attack").Cards.First(c => c.Title == "Strike").NextHandPercent);

        // Sample size 3 out of 6 draw cards -> 50% for 1 copy of Strike, 0% for Bash in discard
        var state3 = OverlayCalculators.CalculateDeckTracker(draw, discard, null, nextHandSampleSize: 3);
        Assert.AreEqual(3, state3.NextHandSampleSize);
        Assert.AreEqual(50, state3.Categories.First(c => c.Category == "Attack").Cards.First(c => c.Title == "Strike").NextHandPercent);
        Assert.AreEqual(0, state3.Categories.First(c => c.Category == "Attack").Cards.First(c => c.Title == "Bash").NextHandPercent);

        // Sample size 7 -> draws all 6 from draw pile (100% Strike) + 1 from 2 discard cards (50% Bash)
        var state7 = OverlayCalculators.CalculateDeckTracker(draw, discard, null, nextHandSampleSize: 7);
        Assert.AreEqual(7, state7.NextHandSampleSize);
        Assert.AreEqual(100, state7.Categories.First(c => c.Category == "Attack").Cards.First(c => c.Title == "Strike").NextHandPercent);
        Assert.AreEqual(50, state7.Categories.First(c => c.Category == "Attack").Cards.First(c => c.Title == "Bash").NextHandPercent);

        // Sample size 10 -> draws all 6 from draw + all 2 from discard (100% Bash)
        var state10 = OverlayCalculators.CalculateDeckTracker(draw, discard, null, nextHandSampleSize: 10);
        Assert.AreEqual(100, state10.Categories.First(c => c.Category == "Attack").Cards.First(c => c.Title == "Bash").NextHandPercent);
    }

    [TestMethod]
    public void EndOfTurnEffects_And_IncomingAttacks_ShouldAccountForPassiveBlockSelfDamageAndDoomedEnemies()
    {
        // 1. WillEnemyDieBeforeAttack: Poison + Doom >= HP -> true
        Assert.IsFalse(OverlayCalculators.WillEnemyDieBeforeAttack(15, new (string?, int)[] { ("PoisonPower", 10) }));
        Assert.IsTrue(OverlayCalculators.WillEnemyDieBeforeAttack(15, new (string?, int)[] { ("PoisonPower", 8), ("DoomPower", 7) }));

        // 2. Orichalcum triggers only when currentPlayerBlock == 0 AND no other passive block
        var (oriBlock0, _) = OverlayCalculators.CalculatePlayerEndOfTurnEffects(
            currentPlayerBlock: 0,
            playerEffects: new (string?, int)[] { ("RELIC.ORICHALCUM", 0) });
        Assert.AreEqual(6, oriBlock0);

        var (oriBlockWithExisting, _) = OverlayCalculators.CalculatePlayerEndOfTurnEffects(
            currentPlayerBlock: 1,
            playerEffects: new (string?, int)[] { ("RELIC.ORICHALCUM", 0) });
        Assert.AreEqual(0, oriBlockWithExisting);

        var (oriWithMetallicize, _) = OverlayCalculators.CalculatePlayerEndOfTurnEffects(
            currentPlayerBlock: 0,
            playerEffects: new (string?, int)[] { ("RELIC.ORICHALCUM", 0), ("MetallicizePower", 4) });
        Assert.AreEqual(4, oriWithMetallicize);

        // 3. Plated Armor + Frost + Cloak Clasp + Burn/Burn+/Decay/Regret/Constricted
        var (combinedBlock, combinedSelfDmg) = OverlayCalculators.CalculatePlayerEndOfTurnEffects(
            currentPlayerBlock: 5,
            playerEffects: new (string?, int)[]
            {
                ("PlatedArmorPower", 4),
                ("FrostOrb", 2),
                ("FrostbitePower", 6),
                ("CloakClasp", 1),
                ("ConstrictedPower", 5)
            },
            handCards: new (string?, int)[]
            {
                ("BURN", 0),
                ("BURN", 1),
                ("DECAY", 0),
                ("REGRET", 0)
            });
        // Passive block: 4 (Plated) + 2 (Frost) + 4 (Cloak Clasp * 4 cards) = 10
        Assert.AreEqual(10, combinedBlock);
        // Self damage: 5 (Constricted) + 2 (Burn) + 4 (Burn+) + 2 (Decay) + 4 (Regret with 4 cards) = 17
        Assert.AreEqual(17, combinedSelfDmg);

        // 4. CalculateIncomingAttacks with 1 doomed attacker, 1 live attacker, passive block, and self damage
        var enemies = new (int, IEnumerable<(string?, string?)>)[]
        {
            (10, new (string?, string?)[] { ("SingleAttackIntent", "20") }), // Doomed
            (25, new (string?, string?)[] { ("MultiAttackIntent", "6x2") })  // Alive (12 dmg, 2 hits)
        };
        var summary = OverlayCalculators.CalculateIncomingAttacks(
            enemies,
            playerBlock: 5,
            playerHp: 10,
            passiveEndTurnBlock: 6,
            endOfTurnSelfDamage: 4,
            enemyWillDieBeforeAttack: new[] { true, false });

        Assert.AreEqual(12, summary.TotalDamage);
        Assert.AreEqual(2, summary.TotalHits);
        Assert.AreEqual(1, summary.AttackingEnemies);
        Assert.AreEqual(1, summary.DoomedEnemies);
        Assert.AreEqual(6, summary.PassiveBlock);
        Assert.AreEqual(4, summary.SelfDamage);
        // Total incoming = 12 + 4 = 16; Total block = 5 + 6 = 11; Unblocked = 5 (< 10 HP -> not lethal)
        Assert.AreEqual(5, summary.UnblockedDamage);
        Assert.IsFalse(summary.IsLethal);
    }

    [TestMethod]
    public void UiScale_And_PanelPositioning_ShouldClampAndRespectViewportBounds()
    {
        Assert.AreEqual(1.0f, OverlayCalculators.ClampUiScale(float.NaN));
        Assert.AreEqual(1.0f, OverlayCalculators.ClampUiScale(0f));
        Assert.AreEqual(0.75f, OverlayCalculators.ClampUiScale(0.25f));
        Assert.AreEqual(1.50f, OverlayCalculators.ClampUiScale(2.50f));
        Assert.AreEqual("125%", OverlayCalculators.FormatUiScalePercent(1.25f));

        Assert.AreEqual(1.25f, OverlayCalculators.CycleUiScale(1.00f));
        Assert.AreEqual(1.50f, OverlayCalculators.CycleUiScale(1.25f));
        Assert.AreEqual(0.75f, OverlayCalculators.CycleUiScale(1.50f));
        Assert.AreEqual(1.00f, OverlayCalculators.CycleUiScale(0.75f));

        // 1920x1080 right-aligned AttackSummaryPanel at 1.0x vs 1.5x scale
        var pos1x = OverlayCalculators.ComputeScaledPanelPosition(
            1920f - 240f, 216f, 0f, 0f, 224f, 64f, 1.0f, 1920f, 1080f, rightAligned: true);
        Assert.AreEqual(1680f, pos1x.X, 0.01f);
        Assert.AreEqual(216f, pos1x.Y, 0.01f);

        var pos15x = OverlayCalculators.ComputeScaledPanelPosition(
            1920f - 240f, 216f, 0f, 0f, 224f, 64f, 1.5f, 1920f, 1080f, rightAligned: true);
        // Scaled width = 224 * 1.5 = 336, right margin = 16 -> X = 1920 - 16 - 336 = 1568 (stays inside viewport!)
        Assert.AreEqual(1568f, pos15x.X, 0.01f);
        Assert.AreEqual(1904f, pos15x.X + 224f * 1.5f, 0.01f);

        // Steam Deck (1280x800) with extreme offsets -> clamped safely within [8, vp - scaledSize - 8]
        var deckPos = OverlayCalculators.ComputeScaledPanelPosition(
            16f, 120f, -500f, 9999f, 240f, 320f, 1.25f, 1280f, 800f, rightAligned: false);
        Assert.AreEqual(8f, deckPos.X, 0.01f);
        Assert.AreEqual(800f - (320f * 1.25f) - 8f, deckPos.Y, 0.01f);

        // Ultrawide (3440x1440) right-aligned panel
        var ultraPos = OverlayCalculators.ComputeScaledPanelPosition(
            3440f - 420f, 172f, 0f, 0f, 404f, 140f, 1.25f, 3440f, 1440f, rightAligned: true);
        Assert.AreEqual(3440f - 16f - (404f * 1.25f), ultraPos.X, 0.01f);
    }

    [TestMethod]
    public void ResolveBossPrep_And_BannerFormatting_ShouldHandleKnownBossesAndFogMasking()
    {
        // 1. Fogged boss masking
        var fogged = OverlayCalculators.ResolveBossPrep("SLIME_BOSS", "Slime Boss", isFogged: true);
        Assert.IsTrue(fogged.IsFogged);
        Assert.AreEqual("??? (Fogged)", fogged.DisplayName);
        StringAssert.Contains(fogged.StrategicTip, "hidden by Map Fog");
        StringAssert.Contains(OverlayCalculators.FormatBossPrepBannerText(fogged), "Act Boss: ??? (Fogged)");

        // 2. Slime Boss
        var slime = OverlayCalculators.ResolveBossPrep("ENCOUNTER:SlimeBoss");
        Assert.IsFalse(slime.IsFogged);
        Assert.AreEqual("Slime Boss", slime.DisplayName);
        StringAssert.Contains(slime.StrategicTip, "burst before 50% split");

        // 3. Time Eater with custom localized title
        var timeEater = OverlayCalculators.ResolveBossPrep("TimeEater", localizedTitle: "Chronos");
        Assert.AreEqual("Chronos", timeEater.DisplayName);
        StringAssert.Contains(timeEater.StrategicTip, "Limits card plays to 12");

        // 4. Hexaghost & Heart
        var hexaghost = OverlayCalculators.ResolveBossPrep("HEXAGHOST");
        StringAssert.Contains(hexaghost.StrategicTip, "12x hit attack turn 2");

        var heart = OverlayCalculators.ResolveBossPrep("CORRUPT_HEART");
        StringAssert.Contains(heart.StrategicTip, "Beat of Death");

        // 5. Unknown boss fallback
        var unknown = OverlayCalculators.ResolveBossPrep("NEW_EXPANSION_BOSS");
        Assert.AreEqual("New Expansion Boss", unknown.DisplayName);
        StringAssert.Contains(unknown.StrategicTip, "scaling");
    }

    [TestMethod]
    public void CalculateShopBudget_ShouldSupportMultiItemPinnedCartAndSingleHover()
    {
        // 1. Null / Empty / No activity -> returns null
        Assert.IsNull(OverlayCalculators.CalculateShopBudget(100, null));
        Assert.IsNull(OverlayCalculators.CalculateShopBudget(100, Array.Empty<(int, bool, bool, bool)>()));
        Assert.IsNull(OverlayCalculators.CalculateShopBudget(100, new (int, bool, bool, bool)[]
        {
            (50, true, false, false), // stocked, neither hovered nor pinned
            (75, true, false, false)
        }));

        // 2. Single hovered slot (no pinned items)
        var singleHover = OverlayCalculators.CalculateShopBudget(250, new (int, bool, bool, bool)[]
        {
            (75, true, true, false), // hovered
            (100, true, false, false)
        });
        Assert.IsNotNull(singleHover);
        Assert.AreEqual(75, singleHover.HoveredCost);
        Assert.AreEqual(175, singleHover.HoveredRemainingGold);
        Assert.AreEqual(0, singleHover.PinnedCount);
        Assert.AreEqual(0, singleHover.PinnedTotalCost);
        Assert.AreEqual(250, singleHover.PinnedRemainingGold);
        Assert.AreEqual(175, singleHover.CombinedRemainingGold);
        Assert.IsTrue(singleHover.HasHoveredItem);
        Assert.IsFalse(singleHover.HasPinnedItems);

        // Also verify legacy CalculateGoldLeft delegates properly
        var legacy = OverlayCalculators.CalculateGoldLeft(250, new (int, bool, bool)[]
        {
            (75, true, true),
            (100, true, false)
        });
        Assert.IsNotNull(legacy);
        Assert.AreEqual(175, legacy.Value.RemainingGold);
        Assert.AreEqual(75, legacy.Value.Cost);

        // 3. Multi-pinned items (Cart of 3 items: 75g + 165g + 55g = 295g) with 350g -> Affordable (55g left)
        var multiPinnedAffordable = OverlayCalculators.CalculateShopBudget(350, new (int, bool, bool, bool)[]
        {
            (75, true, false, true),   // Card Removal: pinned
            (165, true, false, true),  // Relic: pinned
            (55, true, false, true),   // Card: pinned
            (200, true, false, false)  // Other item: unpinned, unhovered
        });
        Assert.IsNotNull(multiPinnedAffordable);
        Assert.IsNull(multiPinnedAffordable.HoveredCost);
        Assert.IsNull(multiPinnedAffordable.HoveredRemainingGold);
        Assert.AreEqual(3, multiPinnedAffordable.PinnedCount);
        Assert.AreEqual(295, multiPinnedAffordable.PinnedTotalCost);
        Assert.AreEqual(55, multiPinnedAffordable.PinnedRemainingGold);
        Assert.AreEqual(55, multiPinnedAffordable.CombinedRemainingGold);
        Assert.IsFalse(multiPinnedAffordable.HasHoveredItem);
        Assert.IsTrue(multiPinnedAffordable.HasPinnedItems);

        // 4. Multi-pinned cart (250g) + Hovering unpinned item (80g) with 300g -> Combined deficit (-30g)
        var combinedDeficit = OverlayCalculators.CalculateShopBudget(300, new (int, bool, bool, bool)[]
        {
            (150, true, false, true), // pinned
            (100, true, false, true), // pinned
            (80, true, true, false)   // hovered, unpinned
        });
        Assert.IsNotNull(combinedDeficit);
        Assert.AreEqual(80, combinedDeficit.HoveredCost);
        Assert.AreEqual(220, combinedDeficit.HoveredRemainingGold);
        Assert.AreEqual(2, combinedDeficit.PinnedCount);
        Assert.AreEqual(250, combinedDeficit.PinnedTotalCost);
        Assert.AreEqual(50, combinedDeficit.PinnedRemainingGold);
        Assert.AreEqual(-30, combinedDeficit.CombinedRemainingGold);
        Assert.IsTrue(combinedDeficit.HasHoveredItem);
        Assert.IsTrue(combinedDeficit.HasPinnedItems);

        // 5. Hovering an ALREADY PINNED item must NOT double-count its cost in CombinedRemainingGold
        var hoverPinned = OverlayCalculators.CalculateShopBudget(200, new (int, bool, bool, bool)[]
        {
            (120, true, true, true) // both hovered and pinned
        });
        Assert.IsNotNull(hoverPinned);
        Assert.AreEqual(120, hoverPinned.HoveredCost);
        Assert.AreEqual(80, hoverPinned.HoveredRemainingGold);
        Assert.AreEqual(1, hoverPinned.PinnedCount);
        Assert.AreEqual(120, hoverPinned.PinnedTotalCost);
        Assert.AreEqual(80, hoverPinned.PinnedRemainingGold);
        Assert.AreEqual(80, hoverPinned.CombinedRemainingGold); // 200 - 120 = 80, not 200 - 240!

        // 6. Purchased / unstocked items (IsStocked == false) auto-excluded from both pinned cart & hover
        var purchased = OverlayCalculators.CalculateShopBudget(200, new (int, bool, bool, bool)[]
        {
            (150, false, false, true), // pinned item that was purchased (IsStocked = false)
            (60, true, true, false)    // hovered unpinned item
        });
        Assert.IsNotNull(purchased);
        Assert.AreEqual(0, purchased.PinnedCount);
        Assert.AreEqual(0, purchased.PinnedTotalCost);
        Assert.AreEqual(200, purchased.PinnedRemainingGold);
        Assert.AreEqual(60, purchased.HoveredCost);
        Assert.AreEqual(140, purchased.HoveredRemainingGold);
        Assert.AreEqual(140, purchased.CombinedRemainingGold);
    }

    [TestMethod]
    public void MergeDualTierLists_And_BothAuthorFormatting_ShouldCombineRatingsAndCommentary()
    {
        var baalor = new Dictionary<string, SlayTheSpireOverlay.Core.Models.CardTierData>(StringComparer.OrdinalIgnoreCase)
        {
            ["ADRENALINE"] = new("ADRENALINE", "S", 95.0, "Free energy and card draw."),
            ["BASH"] = new("BASH", "C", 50.0, "Slow vulnerable at base. Bash+ is much better."),
            ["ONLY_BAALOR"] = new("ONLY_BAALOR", "B", 65.0, "Great in poison builds.")
        };

        var jorbs = new Dictionary<string, SlayTheSpireOverlay.Core.Models.CardTierData>(StringComparer.OrdinalIgnoreCase)
        {
            ["ADRENALINE"] = new("ADRENALINE", "S", 95.0, "Always take, cycle is king."),
            ["BASH"] = new("BASH", "A", 80.0, "Solid damage acceleration."),
            ["ONLY_JORBS"] = new("ONLY_JORBS", "D", 35.0, "Too inconsistent.")
        };

        var merged = OverlayCalculators.MergeDualTierLists(baalor, jorbs);

        // 1. Consensus identical rating (S & S)
        Assert.IsTrue(merged.TryGetValue("ADRENALINE", out var adr));
        Assert.IsNotNull(adr);
        Assert.AreEqual("B:S J:S", adr.Tier);
        Assert.AreEqual(95.0, adr.Score);
        StringAssert.Contains(adr.Commentary, "[Baalorlord · S Tier]\nFree energy and card draw.");
        StringAssert.Contains(adr.Commentary, "[Jorbs · S Tier]\nAlways take, cycle is king.");

        // 2. Disagreement (C vs A) -> Average score (50 + 80) / 2 = 65.0
        Assert.IsTrue(merged.TryGetValue("BASH", out var bash));
        Assert.IsNotNull(bash);
        Assert.AreEqual("B:C J:A", bash.Tier);
        Assert.AreEqual(65.0, bash.Score);
        StringAssert.Contains(bash.Commentary, "[Baalorlord · C Tier]");
        StringAssert.Contains(bash.Commentary, "[Jorbs · A Tier]");

        // 3. Single author entries
        Assert.IsTrue(merged.TryGetValue("ONLY_BAALOR", out var ob));
        Assert.IsNotNull(ob);
        Assert.AreEqual("B:B J:-", ob.Tier);
        Assert.AreEqual(65.0, ob.Score);

        Assert.IsTrue(merged.TryGetValue("ONLY_JORBS", out var oj));
        Assert.IsNotNull(oj);
        Assert.AreEqual("B:- J:D", oj.Tier);
        Assert.AreEqual(35.0, oj.Score);

        // 4. Hover commentary in "Both" mode produces "[Consensus · ...]" header with stacked sections and upgrade note
        string hoverBoth = OverlayCalculators.FormatCardHoverCommentary(
            bash.Tier,
            bash.Commentary,
            author: "Both",
            upgradeLevel: 1);

        StringAssert.Contains(hoverBoth, "[Consensus · B:C J:A · Upgraded +]");
        StringAssert.Contains(hoverBoth, "[Baalorlord · C Tier]");
        StringAssert.Contains(hoverBoth, "[Jorbs · A Tier]");
        StringAssert.Contains(hoverBoth, "★ Upgrade Note: Bash+ is much better.");

        // 5. Badge tier label formatting for dual author ratings
        Assert.AreEqual("B:C J:A", OverlayCalculators.FormatBadgeTierLabel("B:C J:A", upgradeLevel: 0));
        Assert.AreEqual("B:C J:A+", OverlayCalculators.FormatBadgeTierLabel("B:C J:A", upgradeLevel: 1));
        Assert.AreEqual("B:C J:A+ ✦", OverlayCalculators.FormatBadgeTierLabel("B:C J:A", upgradeLevel: 1, enchantmentId: "Swift"));
    }

    [TestMethod]
    public void ClampFogOpacity_ShouldClampBetweenPointThreeAndOne()
    {
        Assert.AreEqual(0.90f, SlayTheSpireMapFog.Core.Services.MapFogCalculator.ClampFogOpacity(0.90f), 0.001f);
        Assert.AreEqual(0.90f, SlayTheSpireMapFog.Core.Services.MapFogCalculator.ClampFogOpacity(float.NaN), 0.001f);
        Assert.AreEqual(0.90f, SlayTheSpireMapFog.Core.Services.MapFogCalculator.ClampFogOpacity(-1.0f), 0.001f);
        Assert.AreEqual(0.90f, SlayTheSpireMapFog.Core.Services.MapFogCalculator.ClampFogOpacity(0f), 0.001f);
        Assert.AreEqual(0.30f, SlayTheSpireMapFog.Core.Services.MapFogCalculator.ClampFogOpacity(0.10f), 0.001f);
        Assert.AreEqual(1.00f, SlayTheSpireMapFog.Core.Services.MapFogCalculator.ClampFogOpacity(1.50f), 0.001f);
        Assert.AreEqual(0.70f, SlayTheSpireMapFog.Core.Services.MapFogCalculator.ClampFogOpacity(0.70f), 0.001f);
    }

    [TestMethod]
    public void CalculateAllyAttackStatus_SafeAndLethalCases_ShouldCalculateCorrectly()
    {
        // 1. Fully blocked
        var blocked = OverlayCalculators.CalculateAllyAttackStatus("Ironclad", currentHp: 20, currentBlock: 15, incomingDamage: 10, totalHits: 1);
        Assert.AreEqual("Ironclad", blocked.Name);
        Assert.AreEqual(0, blocked.UnblockedDamage);
        Assert.IsFalse(blocked.IsLethal);

        // 2. Survives partial damage
        var partial = OverlayCalculators.CalculateAllyAttackStatus("Silent", currentHp: 20, currentBlock: 5, incomingDamage: 15, totalHits: 2);
        Assert.AreEqual("Silent", partial.Name);
        Assert.AreEqual(10, partial.UnblockedDamage);
        Assert.IsFalse(partial.IsLethal);

        // 3. Exact lethal damage
        var exactLethal = OverlayCalculators.CalculateAllyAttackStatus("Defect", currentHp: 20, currentBlock: 5, incomingDamage: 25, totalHits: 1);
        Assert.AreEqual("Defect", exactLethal.Name);
        Assert.AreEqual(20, exactLethal.UnblockedDamage);
        Assert.IsTrue(exactLethal.IsLethal);

        // 4. Overkill damage
        var overkill = OverlayCalculators.CalculateAllyAttackStatus("", currentHp: 15, currentBlock: 0, incomingDamage: 30, totalHits: 3);
        Assert.AreEqual("Ally", overkill.Name); // Fallback for blank name
        Assert.AreEqual(30, overkill.UnblockedDamage);
        Assert.IsTrue(overkill.IsLethal);
    }

    [TestMethod]
    public void CalculateIncomingAttacks_Multiplayer_ShouldIncludeAlliesAndTrackLethal()
    {
        var enemies = new List<(int CurrentHp, IEnumerable<(string? IntentType, string? LabelText)> Intents)>
        {
            (30, new (string?, string?)[] { ("SingleAttackIntent", "12") })
        };

        // Single-player (allies null)
        var spSummary = OverlayCalculators.CalculateIncomingAttacks(enemies, playerBlock: 15, playerHp: 25);
        Assert.IsNull(spSummary.Allies);
        Assert.AreEqual(0, spSummary.UnblockedDamage);
        Assert.IsFalse(spSummary.IsLethal);

        // Multiplayer with 2 allies
        var allyInputs = new List<(string Name, int CurrentHp, int Block, int IncomingDamage, int TotalHits)>
        {
            ("Silent", 25, 10, 15, 1),
            ("Defect", 12, 0, 18, 2)
        };
        var allies = OverlayCalculators.CalculateAlliesAttackStatus(allyInputs);
        var mpSummary = OverlayCalculators.CalculateIncomingAttacks(
            enemies,
            playerBlock: 15,
            playerHp: 25,
            allies: allies);

        Assert.IsNotNull(mpSummary.Allies);
        Assert.HasCount(2, mpSummary.Allies);

        var silent = mpSummary.Allies[0];
        Assert.AreEqual("Silent", silent.Name);
        Assert.AreEqual(5, silent.UnblockedDamage);
        Assert.IsFalse(silent.IsLethal);

        var defect = mpSummary.Allies[1];
        Assert.AreEqual("Defect", defect.Name);
        Assert.AreEqual(18, defect.UnblockedDamage);
        Assert.IsTrue(defect.IsLethal);
    }

    [TestMethod]
    public void CalculateMaxOutgoingHandDamage_KnapsackEnergyConstraints_ShouldPickHighestDamageCombination()
    {
        var cards = new List<PlayableAttackCard>
        {
            new("Shiv", Cost: 0, Damage: 4, Hits: 1),
            new("Strike", Cost: 1, Damage: 6, Hits: 1),
            new("Twin Strike", Cost: 1, Damage: 5, Hits: 2), // 10 dmg for 1 cost
            new("Carnage", Cost: 2, Damage: 20, Hits: 1),    // 20 dmg for 2 cost
            new("Bludgeon", Cost: 3, Damage: 32, Hits: 1)    // 32 dmg for 3 cost
        };

        // Energy = 3:
        // Option A: Bludgeon (32) + Shiv (4) = 36 dmg
        // Option B: Carnage (20) + Twin Strike (10) + Shiv (4) = 34 dmg
        // Option C: Carnage (20) + Strike (6) + Shiv (4) = 30 dmg
        // Knapsack must pick Option A (36 damage, 2 cards played)
        var summary3 = OverlayCalculators.CalculateMaxOutgoingHandDamage(3, cards, null);
        Assert.AreEqual(36, summary3.MaxSingleTargetDamage);
        Assert.AreEqual(0, summary3.TotalAoeDamage);
        Assert.AreEqual(2, summary3.TotalPlayableCards);
        Assert.AreEqual(5, summary3.TotalAttacksInHand);

        // Energy = 2:
        // Option A: Carnage (20) + Shiv (4) = 24 dmg
        // Option B: Twin Strike (10) + Strike (6) + Shiv (4) = 20 dmg
        var summary2 = OverlayCalculators.CalculateMaxOutgoingHandDamage(2, cards, null);
        Assert.AreEqual(24, summary2.MaxSingleTargetDamage);
        Assert.AreEqual(2, summary2.TotalPlayableCards);

        // Energy = 0: Only 0-cost Shiv can be played
        var summary0 = OverlayCalculators.CalculateMaxOutgoingHandDamage(0, cards, null);
        Assert.AreEqual(4, summary0.MaxSingleTargetDamage);
        Assert.AreEqual(1, summary0.TotalPlayableCards);

        // Energy = 1: Twin Strike (10) + Shiv (4) = 14 dmg
        var summary1 = OverlayCalculators.CalculateMaxOutgoingHandDamage(1, cards, null);
        Assert.AreEqual(14, summary1.MaxSingleTargetDamage);
        Assert.AreEqual(2, summary1.TotalPlayableCards);
    }

    [TestMethod]
    public void CalculateMaxOutgoingHandDamage_WithXCostCards_ShouldScaleWithRemainingEnergy()
    {
        var cards = new List<PlayableAttackCard>
        {
            new("Strike", Cost: 1, Damage: 6, Hits: 1),
            new("Skewer", Cost: 0, Damage: 10, Hits: 1, CostsX: true)
        };

        // Energy = 3:
        // Option A: Strike (6) + Skewer(X=2 -> 20) = 26 dmg
        // Option B: Skewer(X=3 -> 30) = 30 dmg
        // Best: Option B
        var summary3 = OverlayCalculators.CalculateMaxOutgoingHandDamage(3, cards, null);
        Assert.AreEqual(30, summary3.MaxSingleTargetDamage);
        Assert.AreEqual(1, summary3.TotalPlayableCards);

        // AOE Whirlwind: 8 dmg per energy
        var aoeCards = new List<PlayableAttackCard>
        {
            new("Whirlwind", Cost: 0, Damage: 8, Hits: 1, CostsX: true, IsAoe: true),
            new("Cleave", Cost: 1, Damage: 8, Hits: 1, CostsX: false, IsAoe: true)
        };
        // Energy = 2: Whirlwind(X=2 -> 16) vs Cleave(8) + Whirlwind(X=1 -> 8) = 16
        var aoeSummary = OverlayCalculators.CalculateMaxOutgoingHandDamage(2, aoeCards, null);
        Assert.AreEqual(0, aoeSummary.MaxSingleTargetDamage);
        Assert.AreEqual(16, aoeSummary.TotalAoeDamage);

        // Energy = 0: X cards deal 0 damage
        var zeroEnergySummary = OverlayCalculators.CalculateMaxOutgoingHandDamage(0, aoeCards, null);
        Assert.AreEqual(0, zeroEnergySummary.TotalAoeDamage);
        Assert.AreEqual(0, zeroEnergySummary.TotalPlayableCards);
    }

    [TestMethod]
    public void CalculateMaxOutgoingHandDamage_LethalDetection_SingleTargetAndBoardLethal()
    {
        var attacks = new List<PlayableAttackCard>
        {
            new("Strike", Cost: 1, Damage: 6),
            new("Carnage", Cost: 2, Damage: 20)
        };

        // 1. Single enemy with 25 HP, 5 Block (Effective HP = 30).
        // With 3 energy: Strike(6) + Carnage(20) = 26 damage (< 30) -> Not lethal
        var notLethal = OverlayCalculators.CalculateMaxOutgoingHandDamage(
            3,
            attacks,
            new[] { new EnemyTarget("Lagavulin", 25, 5) });
        Assert.AreEqual(26, notLethal.MaxSingleTargetDamage);
        Assert.IsFalse(notLethal.HasTargetLethal);
        Assert.IsNull(notLethal.LethalTargetName);
        Assert.IsFalse(notLethal.HasAllEnemiesLethal);

        // With 20 HP, 5 Block (Effective HP = 25).
        // 26 damage >= 25 -> Lethal! Single enemy board -> HasAllEnemiesLethal = true
        var lethal = OverlayCalculators.CalculateMaxOutgoingHandDamage(
            3,
            attacks,
            new[] { new EnemyTarget("Lagavulin", 20, 5) });
        Assert.IsTrue(lethal.HasTargetLethal);
        Assert.AreEqual("Lagavulin", lethal.LethalTargetName);
        Assert.IsTrue(lethal.HasAllEnemiesLethal);

        // 2. Multi-enemy board with AOE + Single target:
        // Enemy A: 10 HP. Enemy B: 25 HP.
        // Hand has Cleave (AOE 10) + Carnage (Single 15).
        // Cleave kills Enemy A (10 >= 10).
        // Cleave + Carnage kills Enemy B (25 >= 25). Both die!
        var mixedCards = new List<PlayableAttackCard>
        {
            new("Cleave", Cost: 1, Damage: 10, Hits: 1, CostsX: false, IsAoe: true),
            new("Carnage", Cost: 2, Damage: 15, Hits: 1, CostsX: false, IsAoe: false)
        };
        var enemies = new[]
        {
            new EnemyTarget("Acid Slime", 10, 0),
            new EnemyTarget("Spike Slime", 25, 0)
        };
        var fullBoardLethal = OverlayCalculators.CalculateMaxOutgoingHandDamage(3, mixedCards, enemies);
        Assert.AreEqual(15, fullBoardLethal.MaxSingleTargetDamage);
        Assert.AreEqual(10, fullBoardLethal.TotalAoeDamage);
        Assert.IsTrue(fullBoardLethal.HasTargetLethal);
        Assert.AreEqual("Acid Slime", fullBoardLethal.LethalTargetName);
        Assert.IsTrue(fullBoardLethal.HasAllEnemiesLethal);

        // 3. Multi-enemy board where both need single-target:
        // Enemy A: 25 HP. Enemy B: 25 HP.
        // Neither dies purely to Cleave (10 < 25), only one can take Carnage (25).
        // Target lethal is true, but board lethal is false!
        var toughEnemies = new[]
        {
            new EnemyTarget("Slime A", 25, 0),
            new EnemyTarget("Slime B", 25, 0)
        };
        var partialLethal = OverlayCalculators.CalculateMaxOutgoingHandDamage(3, mixedCards, toughEnemies);
        Assert.IsTrue(partialLethal.HasTargetLethal);
        Assert.IsFalse(partialLethal.HasAllEnemiesLethal);
    }

    [TestMethod]
    public void CalculateMaxOutgoingHandDamage_EmptyOrNegativeEnergy_ShouldHandleSafely()
    {
        var summary = OverlayCalculators.CalculateMaxOutgoingHandDamage(-2, null, null);
        Assert.AreEqual(0, summary.MaxSingleTargetDamage);
        Assert.AreEqual(0, summary.TotalAoeDamage);
        Assert.AreEqual(0, summary.TotalPlayableCards);
        Assert.AreEqual(0, summary.TotalAttacksInHand);
        Assert.IsFalse(summary.HasTargetLethal);
        Assert.IsNull(summary.LethalTargetName);
        Assert.IsFalse(summary.HasAllEnemiesLethal);
    }
}
