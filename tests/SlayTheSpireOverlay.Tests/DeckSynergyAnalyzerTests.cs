using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SlayTheSpireOverlay.Core.Services;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class DeckSynergyAnalyzerTests
{
    [TestMethod]
    public void AnalyzeDeck_WithEmptyOrNullCards_ShouldReturnZeroesGracefully()
    {
        var summaryNull = DeckSynergyAnalyzer.AnalyzeDeck(null);
        Assert.AreEqual(0, summaryNull.TotalCards);
        Assert.AreEqual(0, summaryNull.Attacks);
        Assert.AreEqual(0, summaryNull.Skills);
        Assert.AreEqual(0, summaryNull.Powers);
        Assert.AreEqual(0, summaryNull.Other);
        Assert.AreEqual(0.0, summaryNull.AverageCost);
        Assert.IsEmpty(summaryNull.TopMechanicTags);

        var summaryEmpty = DeckSynergyAnalyzer.AnalyzeDeck(new List<DeckCardInfo>());
        Assert.AreEqual(0, summaryEmpty.TotalCards);
    }

    [TestMethod]
    public void AnalyzeDeck_IroncladStarterPlusSynergies_ShouldComputeAccurateTagsAndCurve()
    {
        // 5 Strike, 4 Defend, 1 Bash, 1 Demon Form (Power, cost 3), 1 Whirlwind (X cost), 1 True Grit (Skill, Exhaust), 1 AscendersBane (Curse)
        var deck = new List<DeckCardInfo>
        {
            new("STRIKE_RED", "Strike", "Attack", 1),
            new("STRIKE_RED", "Strike", "Attack", 1),
            new("STRIKE_RED", "Strike", "Attack", 1),
            new("STRIKE_RED", "Strike", "Attack", 1),
            new("STRIKE_RED", "Strike", "Attack", 1),
            new("DEFEND_RED", "Defend", "Skill", 1, Description: "Gain 5 Block."),
            new("DEFEND_RED", "Defend", "Skill", 1, Description: "Gain 5 Block."),
            new("DEFEND_RED", "Defend", "Skill", 1, Description: "Gain 5 Block."),
            new("DEFEND_RED", "Defend", "Skill", 1, Description: "Gain 5 Block."),
            new("BASH", "Bash", "Attack", 2, Description: "Deal 8 damage. Apply 2 Vulnerable."),
            new("DEMON_FORM", "Demon Form", "Power", 3, Description: "At the start of your turn, gain 2 Strength."),
            new("WHIRLWIND", "Whirlwind", "Attack", null, CostsX: true, Description: "Deal 5 damage to ALL enemies X times."),
            new("TRUE_GRIT", "True Grit", "Skill", 1, Description: "Gain 7 Block. Exhaust a random card in hand."),
            new("ASCENDERS_BANE", "Ascender's Bane", "Curse", null, Description: "Unplayable. Cannot be removed from your deck.")
        };

        var summary = DeckSynergyAnalyzer.AnalyzeDeck(deck, maxTopTags: 4);

        Assert.AreEqual(14, summary.TotalCards);
        Assert.AreEqual(7, summary.Attacks);
        Assert.AreEqual(5, summary.Skills);
        Assert.AreEqual(1, summary.Powers);
        Assert.AreEqual(1, summary.Other);

        // Playable costs: 5x1 (Strikes) + 4x1 (Defends) + 1x2 (Bash) + 1x3 (Demon Form) + 1x1 (True Grit) = 15 total cost / 12 playable cards = 1.25 avg
        Assert.AreEqual(1.25, summary.AverageCost);
        Assert.AreEqual(10, summary.CostDistribution["1"]);
        Assert.AreEqual(1, summary.CostDistribution["2"]);
        Assert.AreEqual(1, summary.CostDistribution["3+"]);
        Assert.AreEqual(1, summary.CostDistribution["X"]);
        Assert.AreEqual(1, summary.CostDistribution["-"]);

        // Synergy tags: Block (5: 4 Defend + 1 True Grit), Vulnerable (1), Strength (1), Exhaust (1)
        var topTagDict = summary.TopMechanicTags.ToDictionary(t => t.Tag, t => t.Count);
        Assert.IsTrue(topTagDict.ContainsKey("Block"));
        Assert.AreEqual(5, topTagDict["Block"]);
        Assert.IsTrue(topTagDict.ContainsKey("Vulnerable"));
        Assert.AreEqual(1, topTagDict["Vulnerable"]);
        Assert.IsTrue(topTagDict.ContainsKey("Strength"));
        Assert.AreEqual(1, topTagDict["Strength"]);
        Assert.IsTrue(topTagDict.ContainsKey("Exhaust"));
        Assert.AreEqual(1, topTagDict["Exhaust"]);

        StringAssert.Contains(summary.FormattedSummary, "Deck: 14 Cards (7A / 5S / 1P / 1 Other) · Avg Cost: 1.25");
        StringAssert.Contains(summary.FormattedSummary, "Block (5)");
    }

    [TestMethod]
    public void AnalyzeDeck_SilentPoisonAndShivDecks_ShouldDetectPoisonAndShivArchetypes()
    {
        var deck = new List<DeckCardInfo>
        {
            new("DEADLY_POISON", "Deadly Poison", "Skill", 1, Description: "Apply 5 Poison."),
            new("NOXIOUS_FUMES", "Noxious Fumes", "Power", 1, Description: "At the start of your turn, apply 2 Poison to ALL enemies."),
            new("CATALYST", "Catalyst", "Skill", 1, Description: "Double an enemy's Poison."),
            new("BLADE_DANCE", "Blade Dance", "Skill", 1, Description: "Add 3 Shivs into your hand."),
            new("CLOAK_AND_DAGGER", "Cloak and Dagger", "Skill", 1, Description: "Gain 6 Block. Add 1 Shiv into your hand."),
            new("BACKSTAB", "Backstab", "Attack", 0, Description: "Deal 11 damage. Innate. Exhaust.")
        };

        var summary = DeckSynergyAnalyzer.AnalyzeDeck(deck);

        var topTags = summary.TopMechanicTags.ToDictionary(t => t.Tag, t => t.Count);
        Assert.AreEqual(3, topTags["Poison"]);
        Assert.AreEqual(2, topTags["Shiv"]);
        Assert.AreEqual(1, topTags["Block"]);
        Assert.AreEqual(1, topTags["Exhaust"]);
        Assert.AreEqual(0.83, summary.AverageCost);
    }

    [TestMethod]
    public void AnalyzeDeck_DefectRegentNecrobinderMechanics_ShouldDetectOrbsStarsAndSummons()
    {
        var deck = new List<DeckCardInfo>
        {
            // Defect Orb mechanics
            new("ZAP", "Zap", "Skill", 1, Keywords: new[] { "CHANNEL_LIGHTNING" }, Description: "Channel 1 Lightning."),
            new("BLIZZARD", "Blizzard", "Attack", 1, Description: "Deal damage equal to 2x Frost channeled."),
            // Regent Star / Stellar mechanics
            new("STELLAR_ALIGNMENT", "Stellar Alignment", "Power", 2, Description: "Gain 3 Stars each turn."),
            // Necrobinder Osty / Summon mechanics
            new("CALL_MINION", "Call Minion", "Skill", 1, Description: "Summon Osty to the graveyard."),
            new("SOUL_HARVEST", "Soul Harvest", "Attack", 2, Description: "Gain 2 Souls from the tombstone.")
        };

        var summary = DeckSynergyAnalyzer.AnalyzeDeck(deck);

        var topTags = summary.TopMechanicTags.ToDictionary(t => t.Tag, t => t.Count);
        Assert.IsTrue(topTags.ContainsKey("Orb"));
        Assert.AreEqual(2, topTags["Orb"]);
        Assert.IsTrue(topTags.ContainsKey("Star"));
        Assert.AreEqual(1, topTags["Star"]);
        Assert.IsTrue(topTags.ContainsKey("Summon"));
        Assert.AreEqual(2, topTags["Summon"]);
    }
}
