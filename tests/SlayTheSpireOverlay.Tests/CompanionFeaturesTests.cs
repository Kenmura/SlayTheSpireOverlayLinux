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
}
