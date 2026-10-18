using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SlayTheSpireOverlay.Core.Services;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class OutgoingDamageTests
{
    private static OutgoingDamageSummary Calc(int energy, PlayableAttackCard[] cards, params EnemyTarget[] enemies) =>
        OverlayCalculators.CalculateMaxOutgoingHandDamage(energy, cards, enemies);

    [TestMethod]
    public void Knapsack_PicksBestCombinationWithinEnergy()
    {
        var cards = new[]
        {
            new PlayableAttackCard("A", 1, 6),
            new PlayableAttackCard("B", 2, 14),
            new PlayableAttackCard("C", 3, 20),
        };
        // 3 energy: B+A = 20 ties C = 20; 2 energy: B = 14 beats A alone
        Assert.AreEqual(20, Calc(3, cards).MaxSingleTargetDamage);
        Assert.AreEqual(14, Calc(2, cards).MaxSingleTargetDamage);
        Assert.AreEqual(0, Calc(0, cards).MaxSingleTargetDamage);
    }

    [TestMethod]
    public void ZeroCostAttacks_AlwaysCountAndHitsMultiply()
    {
        var cards = new[] { new PlayableAttackCard("Shiv", 0, 4, Hits: 2), new PlayableAttackCard("Big", 2, 10) };
        var r = Calc(0, cards);
        Assert.AreEqual(8, r.MaxSingleTargetDamage);
        Assert.AreEqual(1, r.TotalPlayableCards);
        Assert.AreEqual(2, r.TotalAttacksInHand);
    }

    [TestMethod]
    public void XCost_SpendsRemainingEnergy()
    {
        var cards = new[] { new PlayableAttackCard("Whirl", 0, 5, CostsX: true), new PlayableAttackCard("Hit", 1, 6) };
        // 3 energy: Hit(1)+X(2)=6+10=16 vs X(3)=15
        Assert.AreEqual(16, Calc(3, cards).MaxSingleTargetDamage);
        Assert.AreEqual(15, Calc(3, new[] { cards[0] }).MaxSingleTargetDamage);
    }

    [TestMethod]
    public void NonAttackOrZeroDamage_AreIgnored()
    {
        var r = Calc(3, new[] { new PlayableAttackCard("Dud", 1, 0) }, new EnemyTarget("E", 5, 0));
        Assert.AreEqual(0, r.TotalAttacksInHand);
        Assert.IsFalse(r.HasTargetLethal);
    }

    [TestMethod]
    public void Lethal_ComparesAgainstHpPlusBlock()
    {
        var cards = new[] { new PlayableAttackCard("Hit", 1, 10) };
        Assert.IsTrue(Calc(1, cards, new EnemyTarget("Slime", 10, 0)).HasTargetLethal);
        var blocked = Calc(1, cards, new EnemyTarget("Slime", 6, 5));
        Assert.IsFalse(blocked.HasTargetLethal);
        Assert.IsNull(blocked.LethalTargetName);
    }

    [TestMethod]
    public void DeadEnemies_AreIgnored_AndAoeKillsWholeBoard()
    {
        var cards = new[] { new PlayableAttackCard("Cleave", 1, 8, IsAoe: true) };
        var r = Calc(1, cards, new EnemyTarget("A", 8, 0), new EnemyTarget("B", 5, 2), new EnemyTarget("Dead", 0, 0));
        Assert.IsTrue(r.HasAllEnemiesLethal);
        Assert.IsTrue(r.HasTargetLethal);
        var notAll = Calc(1, cards, new EnemyTarget("A", 8, 0), new EnemyTarget("B", 20, 0));
        Assert.IsTrue(notAll.HasTargetLethal);
        Assert.IsFalse(notAll.HasAllEnemiesLethal);
    }

    [TestMethod]
    public void SingleTargetDamage_DoesNotKillWholeMultiEnemyBoard()
    {
        var cards = new[] { new PlayableAttackCard("Strike", 1, 30) };
        var r = Calc(1, cards, new EnemyTarget("A", 10, 0), new EnemyTarget("B", 10, 0));
        Assert.IsTrue(r.HasTargetLethal);
        Assert.IsFalse(r.HasAllEnemiesLethal);
    }
}

[TestClass]
public class DeckTrackerDescriptionTests
{
    [TestMethod]
    public void DeckCardGroup_PropagatesStrippedDescriptionAndType()
    {
        var draw = new List<CombatCard>
        {
            new("CARD:Bash", "Bash", "Attack", 2, false, 0, true, "[gold]Deal[/gold] [b]8[/b] damage.\nApply 2 [color=#ff0000]Vulnerable[/color]."),
            new("CARD:Bash", "Bash", "Attack", 2, false, 0, true, "ignored duplicate"),
        };
        var state = OverlayCalculators.CalculateDeckTracker(draw, new List<CombatCard>(), new List<CombatCard>());
        var group = state.Categories.SelectMany(c => c.Cards).Single();
        Assert.AreEqual("Deal 8 damage.\nApply 2 Vulnerable.", group.Description);
        Assert.AreEqual("Attack", group.Type);
        Assert.AreEqual(2, group.Total);
    }

    [TestMethod]
    public void DeckCardGroup_MissingDescription_DefaultsToEmpty()
    {
        var draw = new List<CombatCard> { new("CARD:Strike", "Strike", "Attack", 1, false, 0) };
        var group = OverlayCalculators.CalculateDeckTracker(draw, null, null).Categories.SelectMany(c => c.Cards).Single();
        Assert.AreEqual("", group.Description);
    }

    [TestMethod]
    public void OverlayConfig_CardHoverDefaultsOn()
    {
        Assert.IsTrue(new SlayTheSpireOverlay.Core.Options.OverlayConfig().EnableDeckTrackerCardHover);
    }
}

[TestClass]
public class OutgoingDamageReviewRegressionTests
{
    private static OutgoingDamageSummary Calc(int energy, PlayableAttackCard[] cards, params EnemyTarget[] enemies) =>
        OverlayCalculators.CalculateMaxOutgoingHandDamage(energy, cards, enemies);

    [TestMethod]
    public void AoeCard_KillsBoard_EvenWhenSingleCardHasMoreDamage()
    {
        var cards = new[] { new PlayableAttackCard("Cleave", 1, 8, IsAoe: true), new PlayableAttackCard("Strike", 1, 9) };
        Assert.IsTrue(Calc(1, cards, new EnemyTarget("A", 8, 0), new EnemyTarget("B", 8, 0)).HasAllEnemiesLethal);
    }

    [TestMethod]
    public void AoePlusSingle_SplitAcrossEnemies()
    {
        var cards = new[] { new PlayableAttackCard("Cleave", 1, 10, IsAoe: true), new PlayableAttackCard("Smash", 2, 25) };
        Assert.IsTrue(Calc(3, cards, new EnemyTarget("A", 10, 0), new EnemyTarget("B", 30, 0)).HasAllEnemiesLethal);
        Assert.IsFalse(Calc(2, cards, new EnemyTarget("A", 10, 0), new EnemyTarget("B", 30, 0)).HasAllEnemiesLethal);
    }

    [TestMethod]
    public void MultipleSinglesCanKillMultipleEnemies()
    {
        var cards = new[] { new PlayableAttackCard("S1", 1, 6), new PlayableAttackCard("S2", 1, 6) };
        Assert.IsTrue(Calc(2, cards, new EnemyTarget("A", 6, 0), new EnemyTarget("B", 6, 0)).HasAllEnemiesLethal);
        Assert.IsFalse(Calc(1, cards, new EnemyTarget("A", 6, 0), new EnemyTarget("B", 6, 0)).HasAllEnemiesLethal);
    }

    [TestMethod]
    public void BlockBoundary_ExactAndOffByOne()
    {
        var cards = new[] { new PlayableAttackCard("Hit", 1, 12) };
        Assert.IsTrue(Calc(1, cards, new EnemyTarget("E", 7, 5)).HasTargetLethal);
        Assert.IsFalse(Calc(1, cards, new EnemyTarget("E", 8, 5)).HasTargetLethal);
    }

    [TestMethod]
    public void MixedCosts_CardOverEnergyIsIgnored_AndAoeXScales()
    {
        var cards = new[]
        {
            new PlayableAttackCard("Free", 0, 3),
            new PlayableAttackCard("Two", 2, 11),
            new PlayableAttackCard("Three", 3, 30),
            new PlayableAttackCard("XAoe", 0, 4, CostsX: true, IsAoe: true),
        };
        var r = Calc(2, cards);
        // Options: Free+Two=14, or Free + X(2 AoE)=3+8=11 -> 14 wins; Three is unaffordable.
        Assert.AreEqual(14, r.MaxSingleTargetDamage + r.TotalAoeDamage);
        var onlyX = Calc(3, new[] { cards[3] });
        Assert.AreEqual(12, onlyX.TotalAoeDamage);
    }

    [TestMethod]
    public void TieBreak_PrefersFewerCards()
    {
        var cards = new[] { new PlayableAttackCard("A", 1, 5), new PlayableAttackCard("B", 1, 5), new PlayableAttackCard("C", 2, 10) };
        Assert.AreEqual(1, Calc(2, cards).TotalPlayableCards);
    }
}

[TestClass]
public class StripBbCodeImgTests
{
    [TestMethod]
    public void StripBbCode_RemovesImgTagsWithTheirPath()
    {
        Assert.AreEqual("Gain 2 Energy.", OverlayCalculators.StripBbCode("[img]res://images/energy.png[/img]Gain [b]2[/b] Energy."));
    }
}
