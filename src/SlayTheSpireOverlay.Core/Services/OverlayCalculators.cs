using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SlayTheSpireOverlay.Core.Services;

public record AllyAttackStatus(
    string Name,
    int CurrentHp,
    int Block,
    int IncomingDamage,
    int UnblockedDamage,
    bool IsLethal,
    int TotalHits = 0);

public record PlayableAttackCard(
    string Title,
    int Cost,
    int Damage,
    int Hits = 1,
    bool CostsX = false,
    bool IsAoe = false)
{
    public int TotalDamage => Damage * Math.Max(1, Hits);
}

public record EnemyTarget(string Name, int CurrentHp, int Block)
{
    public int EffectiveHp => CurrentHp + Math.Max(0, Block);
}

public record OutgoingDamageSummary(
    int MaxSingleTargetDamage,
    int TotalAoeDamage,
    int TotalPlayableCards,
    int TotalAttacksInHand,
    bool HasTargetLethal,
    string? LethalTargetName,
    bool HasAllEnemiesLethal);

public record AttackSummary(
    int TotalDamage,
    int TotalHits,
    int AttackingEnemies,
    int UnblockedDamage = 0,
    bool IsLethal = false,
    int PassiveBlock = 0,
    int SelfDamage = 0,
    int DoomedEnemies = 0,
    IReadOnlyList<AllyAttackStatus>? Allies = null,
    OutgoingDamageSummary? OutgoingDamage = null);

public record BossPrepInfo(string BossId, string DisplayName, string StrategicTip, bool IsFogged = false);

public record ShopBudgetSummary(
    int? HoveredCost,
    int? HoveredRemainingGold,
    int PinnedCount,
    int PinnedTotalCost,
    int PinnedRemainingGold,
    int? CombinedRemainingGold)
{
    public bool HasHoveredItem => HoveredCost.HasValue;
    public bool HasPinnedItems => PinnedCount > 0;
}

public record CombatCard(string Id, string Title, string Type, int? Cost, bool CostsX, int UpgradeLevel, bool IsPermanent = true, string Description = "");

public record DeckCardGroup(
    string Title,
    int? Cost,
    bool CostsX,
    int UpgradeLevel,
    int InDraw,
    int Total,
    int NextHandPercent = 0,
    string Description = "",
    string Type = "");

public record DeckCategorySummary(string Category, int InDraw, int Total, int ProbabilityPercent, IReadOnlyList<DeckCardGroup> Cards);

public record DeckTrackerState(
    int DrawCount,
    IReadOnlyList<DeckCategorySummary> Categories,
    int DiscardCount = 0,
    int ExhaustCount = 0,
    int HandCount = 0,
    int NextHandSampleSize = 5);

/// <summary>
/// Minimal, stateless calculators for the in-game overlay features:
/// ID normalization, incoming enemy attack summary, draw pile tracker, and shop gold remaining.
/// </summary>
public static class OverlayCalculators
{
    private static readonly Regex BbCodeRegex = new(@"\[[^\]]*\]", RegexOptions.Compiled);
    private static readonly Regex MultiHitRegex = new(@"(\d+)\s*[xX\*×]\s*(\d+)", RegexOptions.Compiled);
    private static readonly Regex SingleHitRegex = new(@"(\d+)", RegexOptions.Compiled);
    private static readonly string[] Categories = { "Attack", "Skill", "Power", "Other" };

    public static string StripBbCode(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : BbCodeRegex.Replace(ImgTagRegex.Replace(text, ""), "").Trim();

    private static readonly Regex ImgTagRegex = new(@"\[img[^\]]*\].*?\[/img\]", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static string NormalizeId(string? rawId, bool stripOptionSuffix = false)
    {
        if (string.IsNullOrWhiteSpace(rawId)) return string.Empty;

        string id = rawId.Trim();
        int sep = id.AsSpan().IndexOfAny(':', '.');
        if (sep >= 0 && sep < id.Length - 1)
        {
            id = id.Substring(sep + 1);
        }

        var sb = new StringBuilder(id.Length + 6);
        for (int i = 0; i < id.Length; i++)
        {
            char c = id[i];
            if (char.IsUpper(c) && i > 0 && id[i - 1] != '_' &&
                (char.IsLower(id[i - 1]) || (i + 1 < id.Length && char.IsLower(id[i + 1]))))
            {
                sb.Append('_');
            }
            sb.Append(char.ToUpperInvariant(c));
        }

        string result = sb.ToString();
        if (stripOptionSuffix && result.EndsWith("_OPTION", StringComparison.Ordinal))
        {
            result = result.Substring(0, result.Length - 7);
        }
        return result;
    }

    public static string ExtractEventOptionId(string? relicId, string? hoverTipId, string? textKey)
    {
        if (!string.IsNullOrWhiteSpace(relicId)) return NormalizeId(relicId, true);
        if (!string.IsNullOrWhiteSpace(hoverTipId)) return NormalizeId(hoverTipId, true);
        if (!string.IsNullOrWhiteSpace(textKey))
        {
            int dot = textKey.LastIndexOf('.');
            string tail = dot >= 0 ? textKey.Substring(dot + 1) : textKey;
            return NormalizeId(tail, true);
        }
        return string.Empty;
    }

    public static bool WillEnemyDieBeforeAttack(
        int currentHp,
        IEnumerable<(string? PowerId, int Amount)>? enemyPowers)
    {
        if (currentHp <= 0) return true;
        if (enemyPowers == null) return false;

        int dotDamage = 0;
        foreach (var (rawId, amount) in enemyPowers)
        {
            if (amount <= 0) continue;
            string id = NormalizeId(rawId);
            if (id.Contains("POISON", StringComparison.Ordinal) ||
                id.Contains("DOOM", StringComparison.Ordinal))
            {
                dotDamage += amount;
            }
        }
        return dotDamage >= currentHp;
    }

    public static (int PassiveBlock, int SelfDamage) CalculatePlayerEndOfTurnEffects(
        int currentPlayerBlock,
        IEnumerable<(string? Id, int Amount)>? playerEffects = null,
        IEnumerable<(string? CardId, int UpgradeLevel)>? handCards = null)
    {
        int passiveBlock = 0;
        int selfDamage = 0;
        bool hasOrichalcum = false;
        int orichalcumAmount = 6;
        int cloakClaspPerCard = 0;

        var cards = handCards?.ToList() ?? new List<(string? CardId, int UpgradeLevel)>();
        int handCount = cards.Count;

        if (playerEffects != null)
        {
            foreach (var (rawId, amount) in playerEffects)
            {
                string id = NormalizeId(rawId);
                if (string.IsNullOrEmpty(id)) continue;

                if (id.Contains("ORICHALCUM", StringComparison.Ordinal))
                {
                    hasOrichalcum = true;
                    if (amount > 0) orichalcumAmount = amount;
                }
                else if (id.Contains("CLOAK_CLASP", StringComparison.Ordinal))
                {
                    cloakClaspPerCard += amount > 0 ? amount : 1;
                }
                else if (id.Contains("METALLICIZE", StringComparison.Ordinal) ||
                         id.Contains("PLATED_ARMOR", StringComparison.Ordinal) ||
                         id.Contains("LIKE_WATER", StringComparison.Ordinal))
                {
                    if (amount > 0) passiveBlock += amount;
                }
                else if (id == "FROST" ||
                         id.EndsWith("_FROST", StringComparison.Ordinal) ||
                         id.StartsWith("FROST_ORB", StringComparison.Ordinal))
                {
                    passiveBlock += amount > 0 ? amount : 2;
                }
                else if (id.Contains("CONSTRICTED", StringComparison.Ordinal))
                {
                    if (amount > 0) selfDamage += amount;
                }
            }
        }

        if (cloakClaspPerCard > 0 && handCount > 0)
        {
            passiveBlock += cloakClaspPerCard * handCount;
        }

        if (hasOrichalcum && currentPlayerBlock <= 0 && passiveBlock == 0)
        {
            passiveBlock += orichalcumAmount;
        }

        foreach (var (rawCardId, upgradeLevel) in cards)
        {
            string cardId = NormalizeId(rawCardId);
            if (string.IsNullOrEmpty(cardId)) continue;

            if (cardId == "BURN" || cardId.EndsWith("_BURN", StringComparison.Ordinal))
            {
                selfDamage += upgradeLevel > 0 ? 4 : 2;
            }
            else if (cardId == "DECAY")
            {
                selfDamage += 2;
            }
            else if (cardId == "REGRET")
            {
                selfDamage += Math.Max(1, handCount);
            }
        }

        return (passiveBlock, selfDamage);
    }

    public static (int Damage, int Hits) ParseAttackIntentValues(string? rawText)
    {
        if (string.IsNullOrEmpty(rawText)) return (0, 0);
        string clean = StripBbCode(rawText);
        var multi = MultiHitRegex.Match(clean);
        if (multi.Success && int.TryParse(multi.Groups[1].Value, out int dmg) && int.TryParse(multi.Groups[2].Value, out int hits) && hits > 0)
        {
            return (dmg * hits, hits);
        }

        var single = SingleHitRegex.Match(clean);
        if (single.Success && int.TryParse(single.Groups[1].Value, out int sDmg))
        {
            return (sDmg, 1);
        }

        return (0, 0);
    }

    public static AllyAttackStatus CalculateAllyAttackStatus(
        string name,
        int currentHp,
        int currentBlock,
        int incomingDamage,
        int totalHits = 1)
    {
        int unblocked = Math.Max(0, incomingDamage - Math.Max(0, currentBlock));
        bool isLethal = currentHp > 0 && unblocked >= currentHp;
        return new AllyAttackStatus(
            string.IsNullOrWhiteSpace(name) ? "Ally" : name,
            currentHp,
            currentBlock,
            incomingDamage,
            unblocked,
            isLethal,
            totalHits);
    }

    public static IReadOnlyList<AllyAttackStatus> CalculateAlliesAttackStatus(
        IEnumerable<(string Name, int CurrentHp, int Block, int IncomingDamage, int TotalHits)>? allies)
    {
        if (allies == null) return Array.Empty<AllyAttackStatus>();
        var list = new List<AllyAttackStatus>();
        foreach (var (name, hp, block, dmg, hits) in allies)
        {
            list.Add(CalculateAllyAttackStatus(name, hp, block, dmg, hits));
        }
        return list;
    }

    public static AttackSummary CalculateIncomingAttacks(
        IEnumerable<(int CurrentHp, IEnumerable<(string? IntentType, string? LabelText)> Intents)>? enemies,
        int playerBlock = 0,
        int playerHp = 0,
        int passiveEndTurnBlock = 0,
        int endOfTurnSelfDamage = 0,
        IEnumerable<bool>? enemyWillDieBeforeAttack = null,
        IReadOnlyList<AllyAttackStatus>? allies = null,
        OutgoingDamageSummary? outgoingDamage = null)
    {
        int effectivePassiveBlock = Math.Max(0, passiveEndTurnBlock);
        int effectiveSelfDamage = Math.Max(0, endOfTurnSelfDamage);

        if (enemies == null && effectiveSelfDamage == 0 && (allies == null || allies.Count == 0) && outgoingDamage == null)
        {
            return new AttackSummary(0, 0, 0, 0, false, effectivePassiveBlock, 0, 0, allies, outgoingDamage);
        }

        var doomedFlags = enemyWillDieBeforeAttack?.ToList();
        int totalDamage = 0, totalHits = 0, attackers = 0, doomedEnemies = 0, idx = 0;

        if (enemies != null)
        {
            foreach (var (hp, intents) in enemies)
            {
                bool doomed = doomedFlags != null && idx < doomedFlags.Count && doomedFlags[idx];
                idx++;

                if (hp <= 0 || intents == null) continue;
                int enemyDmg = 0, enemyHits = 0;

                foreach (var (type, rawText) in intents)
                {
                    if (string.IsNullOrEmpty(type) || (!type.Contains("AttackIntent", StringComparison.OrdinalIgnoreCase) &&
                        !type.EndsWith("DeathBlowIntent", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var (parsedDmg, parsedHits) = ParseAttackIntentValues(rawText);
                    enemyDmg += parsedDmg;
                    enemyHits += parsedHits;
                }

                if (enemyHits > 0)
                {
                    if (doomed)
                    {
                        doomedEnemies++;
                    }
                    else
                    {
                        totalDamage += enemyDmg;
                        totalHits += enemyHits;
                        attackers++;
                    }
                }
            }
        }

        int totalIncoming = totalDamage + effectiveSelfDamage;
        int totalBlock = Math.Max(0, playerBlock) + effectivePassiveBlock;
        int unblocked = Math.Max(0, totalIncoming - totalBlock);
        bool isLethal = (totalHits > 0 || effectiveSelfDamage > 0) && playerHp > 0 && unblocked >= playerHp;
        return new AttackSummary(
            totalDamage,
            totalHits,
            attackers,
            unblocked,
            isLethal,
            effectivePassiveBlock,
            effectiveSelfDamage,
            doomedEnemies,
            allies,
            outgoingDamage);
    }

    private const int MaxFixedCostCards = 14;

    public static OutgoingDamageSummary CalculateMaxOutgoingHandDamage(
        int availableEnergy,
        IEnumerable<PlayableAttackCard>? handAttacks,
        IEnumerable<EnemyTarget>? enemies)
    {
        var attacks = handAttacks?.Where(a => a != null && a.Damage > 0).ToList() ?? new List<PlayableAttackCard>();
        if (attacks.Count == 0)
        {
            return new OutgoingDamageSummary(0, 0, 0, 0, false, null, false);
        }

        int energy = Math.Max(0, availableEnergy);
        var living = enemies?.Where(e => e != null && e.CurrentHp > 0).ToList() ?? new List<EnemyTarget>();

        var free = attacks.Where(a => !a.CostsX && a.Cost <= 0).ToList();
        var fixedCost = attacks.Where(a => !a.CostsX && a.Cost > 0 && a.Cost <= energy)
            .OrderByDescending(a => a.TotalDamage).Take(MaxFixedCostCards).ToList();
        var bestX = attacks.Where(a => a.CostsX).OrderByDescending(a => a.TotalDamage).FirstOrDefault();

        // Every playable line = all free cards + a subset of fixed-cost cards + best X card with leftover energy.
        // Hands are small (<= ~10 cards) so exhaustive enumeration is exact and cheap.
        bool bestSet = false;
        int bestSingle = 0, bestAoe = 0, bestCount = 0;
        bool anyTargetLethal = false, allLethal = false;
        string? lethalName = null;
        int targetLethalBestTotal = -1;

        for (int mask = 0; mask < (1 << fixedCost.Count); mask++)
        {
            int spent = 0;
            var lineSingles = new List<int>();
            int aoe = 0;
            int count = 0;
            foreach (var c in free) { count++; if (c.IsAoe) aoe += c.TotalDamage; else lineSingles.Add(c.TotalDamage); }
            for (int i = 0; i < fixedCost.Count; i++)
            {
                if ((mask & (1 << i)) == 0) continue;
                var c = fixedCost[i];
                spent += c.Cost;
                count++;
                if (c.IsAoe) aoe += c.TotalDamage; else lineSingles.Add(c.TotalDamage);
            }
            if (spent > energy) continue;

            int remaining = energy - spent;
            if (bestX != null && remaining > 0)
            {
                count++;
                int xDmg = bestX.TotalDamage * remaining;
                if (bestX.IsAoe) aoe += xDmg; else lineSingles.Add(xDmg);
            }

            int single = lineSingles.Sum();
            int total = single + aoe;

            if (!bestSet || total > bestSingle + bestAoe || (total == bestSingle + bestAoe && count < bestCount))
            {
                bestSet = true;
                bestSingle = single;
                bestAoe = aoe;
                bestCount = count;
            }

            if (living.Count == 0) continue;

            // Single-target lethal: every damage source can be focused on one enemy.
            var killable = living.Where(e => total >= e.EffectiveHp).ToList();
            if (killable.Count > 0 && total > targetLethalBestTotal)
            {
                anyTargetLethal = true;
                targetLethalBestTotal = total;
                lethalName = killable[0].Name;
            }

            // Whole-board lethal: AoE hits everyone, then single-target cards are split among survivors.
            if (!allLethal)
            {
                var residual = living.Select(e => Math.Max(0, e.EffectiveHp - aoe)).Where(r => r > 0).OrderByDescending(r => r).ToList();
                if (CanCoverAll(residual, lineSingles.OrderByDescending(d => d).ToList()))
                {
                    allLethal = true;
                }
            }
        }

        return new OutgoingDamageSummary(
            bestSingle,
            bestAoe,
            bestCount,
            attacks.Count,
            anyTargetLethal,
            lethalName,
            allLethal);
    }

    // Exact backtracking: can the single-target hits be partitioned so every residual HP is reached?
    private static bool CanCoverAll(List<int> residuals, List<int> hits)
    {
        if (residuals.Count == 0) return true;
        if (hits.Sum() < residuals.Sum()) return false;
        var sums = new int[residuals.Count];
        return Assign(0);

        bool Assign(int hitIndex)
        {
            bool done = true;
            for (int i = 0; i < residuals.Count; i++) if (sums[i] < residuals[i]) { done = false; break; }
            if (done) return true;
            if (hitIndex >= hits.Count) return false;

            for (int i = 0; i < residuals.Count; i++)
            {
                if (sums[i] >= residuals[i]) continue; // already dead; extra damage is wasted
                sums[i] += hits[hitIndex];
                bool ok = Assign(hitIndex + 1);
                sums[i] -= hits[hitIndex];
                if (ok) return true;
            }
            return Assign(hitIndex + 1); // skip this hit
        }
    }

    public static int CalculateEffectiveDrawCount(
        int baseDrawCount = 5,
        IEnumerable<(string? PowerId, int Amount)>? activePowers = null)
    {
        int draw = baseDrawCount >= 0 ? baseDrawCount : 5;
        if (activePowers != null)
        {
            foreach (var (rawId, amount) in activePowers)
            {
                string id = NormalizeId(rawId);
                if (string.IsNullOrEmpty(id)) continue;

                if (id.Contains("DRAW_REDUCTION", StringComparison.Ordinal))
                {
                    draw -= Math.Max(1, Math.Abs(amount));
                }
                else if (id.Contains("DRAW_CARD", StringComparison.Ordinal) ||
                         id.Contains("NEXT_TURN_DRAW", StringComparison.Ordinal) ||
                         id.Contains("MACHINE_LEARNING", StringComparison.Ordinal) ||
                         id.Contains("TOOLS_OF_THE_TRADE", StringComparison.Ordinal) ||
                         id.Contains("BRUTALITY", StringComparison.Ordinal))
                {
                    draw += amount != 0 ? amount : 1;
                }
            }
        }
        return Math.Clamp(draw, 0, 10);
    }

    public static DeckTrackerState CalculateDeckTracker(
        IReadOnlyCollection<CombatCard>? drawPile,
        IReadOnlyCollection<CombatCard>? discardPile,
        IReadOnlyCollection<CombatCard>? handPile,
        int exhaustCount = 0,
        int nextHandSampleSize = 5)
    {
        var draw = drawPile ?? Array.Empty<CombatCard>();
        var discard = discardPile ?? Array.Empty<CombatCard>();
        var hand = handPile ?? Array.Empty<CombatCard>();
        int sampleSize = Math.Max(0, nextHandSampleSize);

        // Zone: 0 = Draw, 1 = Discard, 2 = Hand
        var all = new List<(CombatCard Card, int Zone)>();
        foreach (var c in draw) all.Add((c, 0));
        foreach (var c in discard) all.Add((c, 1));
        foreach (var c in hand.Where(c => c.IsPermanent || NormalizeCategory(c.Type) == "Other")) all.Add((c, 2));

        int drawTotal = draw.Count;
        int discardTotal = discard.Count;
        var groups = new List<DeckCategorySummary>(Categories.Length);

        foreach (string cat in Categories)
        {
            var matching = all.Where(x => NormalizeCategory(x.Card.Type) == cat).ToList();
            int catInDraw = matching.Count(x => x.Zone == 0);
            int pct = drawTotal > 0 ? (int)Math.Round((double)catInDraw / drawTotal * 100.0) : 0;

            var cards = matching
                .GroupBy(x => $"{NormalizeId(x.Card.Id)}:{x.Card.UpgradeLevel}")
                .Select(g =>
                {
                    var first = g.First().Card;
                    int inDraw = g.Count(x => x.Zone == 0);
                    int inDiscard = g.Count(x => x.Zone == 1);
                    int nextHandPct = inDraw > 0
                        ? CalculateHypergeometricAtLeastOne(drawTotal, inDraw, sampleSize)
                        : (drawTotal < sampleSize && discardTotal > 0 && inDiscard > 0
                            ? CalculateHypergeometricAtLeastOne(discardTotal, inDiscard, sampleSize - drawTotal)
                            : 0);

                    string cleanTitle = StripBbCode(first.Title);
                    if (string.IsNullOrEmpty(cleanTitle)) cleanTitle = first.Id;
                    if (first.UpgradeLevel > 0 && !cleanTitle.EndsWith('+'))
                    {
                        cleanTitle += first.UpgradeLevel > 1 ? $"+{first.UpgradeLevel}" : "+";
                    }

                    return new DeckCardGroup(cleanTitle, first.Cost, first.CostsX, first.UpgradeLevel, inDraw, g.Count(), nextHandPct, StripBbCode(first.Description).Trim(), first.Type);
                })
                .OrderByDescending(c => c.InDraw > 0)
                .ThenBy(c => c.CostsX ? 1 : 2)
                .ThenBy(c => c.Cost ?? -1)
                .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            groups.Add(new DeckCategorySummary(cat, catInDraw, matching.Count, pct, cards));
        }

        return new DeckTrackerState(drawTotal, groups, discardTotal, Math.Max(0, exhaustCount), hand.Count, sampleSize);
    }

    public static int CalculateHypergeometricAtLeastOne(int populationSize, int successesInPopulation, int sampleSize = 5)
    {
        if (successesInPopulation <= 0 || populationSize <= 0 || sampleSize <= 0) return 0;
        if (sampleSize >= populationSize || populationSize - successesInPopulation < sampleSize) return 100;

        double probZero = 1.0;
        for (int i = 0; i < sampleSize; i++)
        {
            probZero *= (double)(populationSize - successesInPopulation - i) / (populationSize - i);
        }
        return (int)Math.Round((1.0 - probZero) * 100.0);
    }

    public static ShopBudgetSummary? CalculateShopBudget(
        int playerGold,
        IEnumerable<(int Cost, bool IsStocked, bool IsHovered, bool IsPinned)>? slots)
    {
        if (slots == null) return null;

        int pinnedCount = 0;
        int pinnedTotalCost = 0;
        int? hoveredCost = null;
        bool hoveredIsPinned = false;

        foreach (var (cost, isStocked, isHovered, isPinned) in slots)
        {
            if (!isStocked || cost < 0) continue;

            if (isPinned)
            {
                pinnedCount++;
                pinnedTotalCost += cost;
            }

            if (isHovered && !hoveredCost.HasValue)
            {
                hoveredCost = cost;
                hoveredIsPinned = isPinned;
            }
        }

        if (pinnedCount == 0 && !hoveredCost.HasValue)
        {
            return null;
        }

        int pinnedRemaining = playerGold - pinnedTotalCost;
        int? hoveredRemaining = hoveredCost.HasValue ? playerGold - hoveredCost.Value : null;

        int? combinedRemaining = null;
        if (pinnedCount > 0 && hoveredCost.HasValue)
        {
            int additionalHoveredCost = hoveredIsPinned ? 0 : hoveredCost.Value;
            combinedRemaining = playerGold - (pinnedTotalCost + additionalHoveredCost);
        }
        else if (pinnedCount > 0)
        {
            combinedRemaining = pinnedRemaining;
        }
        else if (hoveredCost.HasValue)
        {
            combinedRemaining = hoveredRemaining;
        }

        return new ShopBudgetSummary(
            HoveredCost: hoveredCost,
            HoveredRemainingGold: hoveredRemaining,
            PinnedCount: pinnedCount,
            PinnedTotalCost: pinnedTotalCost,
            PinnedRemainingGold: pinnedRemaining,
            CombinedRemainingGold: combinedRemaining);
    }

    public static (int RemainingGold, int Cost)? CalculateGoldLeft(
        int playerGold,
        IEnumerable<(int Cost, bool IsStocked, bool IsHovered)>? slots)
    {
        if (slots == null) return null;
        var budget = CalculateShopBudget(playerGold, slots.Select(s => (s.Cost, s.IsStocked, s.IsHovered, IsPinned: false)));
        if (budget != null && budget.HoveredCost.HasValue && budget.HoveredRemainingGold.HasValue)
        {
            return (budget.HoveredRemainingGold.Value, budget.HoveredCost.Value);
        }
        return null;
    }

    private static readonly HashSet<string> AllowedNamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "TAB", "SPACE", "QUOTELEFT", "MINUS", "EQUAL", "BACKSLASH",
        "BRACKETLEFT", "BRACKETRIGHT", "SEMICOLON", "APOSTROPHE",
        "COMMA", "PERIOD", "SLASH", "HOME", "END", "INSERT", "DELETE",
        "PAGEUP", "PAGEDOWN", "BACKSPACE", "ESCAPE"
    };

    public static string NormalizeKeybindName(string? configuredKey, string fallbackKey)
    {
        string safeFallback = string.IsNullOrWhiteSpace(fallbackKey) ? "F8" : fallbackKey.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(configuredKey)) return safeFallback;

        string candidate = configuredKey.Trim();
        if (candidate.StartsWith("Key.", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate.Substring(4).Trim();
        }
        else if (candidate.StartsWith("KEY_", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate.Substring(4).Trim();
        }

        if (string.IsNullOrEmpty(candidate)) return safeFallback;

        string upper = candidate.ToUpperInvariant();

        // Single letter A-Z or digit 0-9
        if (upper.Length == 1 && ((upper[0] >= 'A' && upper[0] <= 'Z') || (upper[0] >= '0' && upper[0] <= '9')))
        {
            return upper;
        }

        // Function keys F1..F24
        if (upper[0] == 'F' && upper.Length >= 2 && upper.Length <= 3 &&
            int.TryParse(upper.AsSpan(1), out int fn) && fn >= 1 && fn <= 24)
        {
            return $"F{fn}";
        }

        if (AllowedNamedKeys.Contains(upper))
        {
            return upper;
        }

        return safeFallback;
    }

    private static readonly Regex UpgradeKeywordRegex = new(
        @"(\b(?:upgrad\w*|smith\w*)\b|[A-Za-z]\+(?![A-Za-z0-9]))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SentenceSplitRegex = new(
        @"(?<=[.!?])\s+|\r?\n+",
        RegexOptions.Compiled);
    private static readonly Regex TrailingUpgradeSuffixRegex = new(
        @"\+\d*$",
        RegexOptions.Compiled);

    public static Models.CardTierData? ResolveCardTierData(
        IReadOnlyDictionary<string, Models.CardTierData>? tierList,
        string? rawId,
        int upgradeLevel = 0,
        bool stripOptionSuffix = false)
    {
        if (tierList == null || string.IsNullOrWhiteSpace(rawId)) return null;

        string lookupKey = NormalizeId(rawId, stripOptionSuffix);
        if (string.IsNullOrEmpty(lookupKey)) return null;

        bool hasTrailingPlus = TrailingUpgradeSuffixRegex.IsMatch(lookupKey);
        string baseKey = hasTrailingPlus ? TrailingUpgradeSuffixRegex.Replace(lookupKey, "") : lookupKey;
        string strippedBase = baseKey.Replace("_", "", StringComparison.Ordinal);
        bool wantUpgraded = upgradeLevel > 0 || hasTrailingPlus;

        if (wantUpgraded)
        {
            if (tierList.TryGetValue($"{baseKey}+", out var upgData)) return upgData;
            if (strippedBase != baseKey && tierList.TryGetValue($"{strippedBase}+", out upgData)) return upgData;
        }

        if (tierList.TryGetValue(baseKey, out var baseData)) return baseData;
        if (strippedBase != baseKey && tierList.TryGetValue(strippedBase, out baseData)) return baseData;
        return null;
    }

    public static string FormatBadgeTierLabel(string? tier, int upgradeLevel = 0, string? enchantmentId = null)
    {
        string trimmed = string.IsNullOrWhiteSpace(tier) ? "N/A" : tier.Trim();
        if (upgradeLevel > 0 && !trimmed.EndsWith('+'))
        {
            trimmed += upgradeLevel > 1 ? $"+{upgradeLevel}" : "+";
        }
        if (!string.IsNullOrWhiteSpace(enchantmentId))
        {
            trimmed += " ✦";
        }
        return trimmed;
    }

    public static string ExtractUpgradeCommentary(string? commentary)
    {
        if (string.IsNullOrWhiteSpace(commentary)) return string.Empty;

        string[] sentences = SentenceSplitRegex.Split(commentary.Trim());
        var matches = new List<string>();
        foreach (string raw in sentences)
        {
            string s = raw.Trim();
            if (s.Length == 0) continue;
            if (UpgradeKeywordRegex.IsMatch(s))
            {
                matches.Add(s);
            }
        }
        return matches.Count > 0 ? string.Join(" ", matches) : string.Empty;
    }

    public static Dictionary<string, Models.CardTierData> MergeDualTierLists(
        IReadOnlyDictionary<string, Models.CardTierData>? baalorlordTiers,
        IReadOnlyDictionary<string, Models.CardTierData>? jorbsTiers)
    {
        var merged = new Dictionary<string, Models.CardTierData>(StringComparer.OrdinalIgnoreCase);
        baalorlordTiers ??= new Dictionary<string, Models.CardTierData>();
        jorbsTiers ??= new Dictionary<string, Models.CardTierData>();

        var allKeys = new HashSet<string>(baalorlordTiers.Keys, StringComparer.OrdinalIgnoreCase);
        allKeys.UnionWith(jorbsTiers.Keys);

        foreach (string key in allKeys)
        {
            bool hasB = baalorlordTiers.TryGetValue(key, out var b);
            bool hasJ = jorbsTiers.TryGetValue(key, out var j);

            if (hasB && hasJ && b != null && j != null)
            {
                string bTier = b.Tier?.Trim().ToUpperInvariant() ?? "?";
                string jTier = j.Tier?.Trim().ToUpperInvariant() ?? "?";
                string combinedTier = $"B:{bTier} J:{jTier}";
                double avgScore = Math.Round((b.Score + j.Score) / 2.0, 1);

                var sb = new StringBuilder();
                sb.AppendLine($"[Baalorlord · {bTier} Tier]");
                sb.AppendLine(b.Commentary);
                sb.AppendLine();
                sb.AppendLine($"[Jorbs · {jTier} Tier]");
                sb.Append(j.Commentary);

                merged[key] = new Models.CardTierData(b.CardId ?? key, combinedTier, avgScore, sb.ToString().Trim());
            }
            else if (hasB && b != null)
            {
                string bTier = b.Tier?.Trim().ToUpperInvariant() ?? "?";
                string combinedTier = $"B:{bTier} J:-";
                string commentary = $"[Baalorlord · {bTier} Tier]\n{b.Commentary}";
                merged[key] = new Models.CardTierData(b.CardId ?? key, combinedTier, b.Score, commentary.Trim());
            }
            else if (hasJ && j != null)
            {
                string jTier = j.Tier?.Trim().ToUpperInvariant() ?? "?";
                string combinedTier = $"B:- J:{jTier}";
                string commentary = $"[Jorbs · {jTier} Tier]\n{j.Commentary}";
                merged[key] = new Models.CardTierData(j.CardId ?? key, combinedTier, j.Score, commentary.Trim());
            }
        }

        return merged;
    }

    public static string FormatCardHoverCommentary(
        string tier,
        string? commentary,
        string author = "Baalorlord",
        int upgradeLevel = 0,
        string? enchantmentId = null)
    {
        string trimmedTier = tier?.Trim() ?? "N/A";
        string tierLabel = trimmedTier.Length == 1 ? $"{trimmedTier} Tier" : trimmedTier;

        string authorLabel = string.Equals(author?.Trim(), "Both", StringComparison.OrdinalIgnoreCase)
            ? "Consensus"
            : (author ?? "Baalorlord");
        var metaParts = new List<string>(3) { authorLabel, tierLabel };
        if (upgradeLevel > 0)
        {
            metaParts.Add(upgradeLevel > 1 ? $"Upgraded +{upgradeLevel}" : "Upgraded +");
        }
        if (!string.IsNullOrWhiteSpace(enchantmentId))
        {
            metaParts.Add($"Enchanted: {NormalizeId(enchantmentId)}");
        }

        string header = $"[{string.Join(" · ", metaParts)}]";
        string trimmedBody = commentary?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedBody)) return header;

        if (upgradeLevel > 0)
        {
            string upgradeNote = ExtractUpgradeCommentary(trimmedBody);
            if (!string.IsNullOrEmpty(upgradeNote))
            {
                if (string.Equals(upgradeNote, trimmedBody, StringComparison.Ordinal))
                {
                    return $"{header}\n★ Upgrade Note: {trimmedBody}";
                }
                return $"{header}\n★ Upgrade Note: {upgradeNote}\n{trimmedBody}";
            }
        }

        return $"{header}\n{trimmedBody}";
    }

    public static float ClampUiScale(float scale)
    {
        if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
        {
            return 1.0f;
        }
        return Math.Clamp(scale, 0.75f, 1.50f);
    }

    public static float CycleUiScale(float currentScale)
    {
        float clamped = ClampUiScale(currentScale);
        if (clamped < 0.875f) return 1.00f;
        if (clamped < 1.125f) return 1.25f;
        if (clamped < 1.375f) return 1.50f;
        return 0.75f;
    }

    public static string FormatUiScalePercent(float scale) =>
        $"{(int)Math.Round(ClampUiScale(scale) * 100f)}%";

    public static (float X, float Y) ComputeScaledPanelPosition(
        float baseX,
        float baseY,
        float offsetX,
        float offsetY,
        float estimatedWidth,
        float estimatedHeight,
        float uiScale,
        float viewportWidth,
        float viewportHeight,
        bool rightAligned = false)
    {
        float scale = ClampUiScale(uiScale);
        float safeVpW = viewportWidth > 100f ? viewportWidth : 1920f;
        float safeVpH = viewportHeight > 100f ? viewportHeight : 1080f;
        float safeOffX = float.IsNaN(offsetX) || float.IsInfinity(offsetX) ? 0f : offsetX;
        float safeOffY = float.IsNaN(offsetY) || float.IsInfinity(offsetY) ? 0f : offsetY;

        float scaledW = Math.Max(0f, estimatedWidth) * scale;
        float scaledH = Math.Max(0f, estimatedHeight) * scale;

        float rawX;
        if (rightAligned)
        {
            float unscaledRightMargin = Math.Max(8f, safeVpW - (baseX + Math.Max(0f, estimatedWidth)));
            rawX = safeVpW - unscaledRightMargin - scaledW + safeOffX;
        }
        else
        {
            rawX = baseX + safeOffX;
        }

        float rawY = baseY + safeOffY;
        float maxX = Math.Max(8f, safeVpW - scaledW - 8f);
        float maxY = Math.Max(8f, safeVpH - scaledH - 8f);

        return (Math.Clamp(rawX, 8f, maxX), Math.Clamp(rawY, 8f, maxY));
    }

    public static BossPrepInfo ResolveBossPrep(string? rawBossId, string? localizedTitle = null, bool isFogged = false)
    {
        if (isFogged)
        {
            return new BossPrepInfo("UNKNOWN", "??? (Fogged)", "Boss identity hidden by Map Fog. Explore further to reveal.", true);
        }

        string normId = NormalizeId(rawBossId);
        string displayName = !string.IsNullOrWhiteSpace(localizedTitle) ? localizedTitle.Trim() : FormatBossName(normId);

        string tip = normId switch
        {
            var s when s.Contains("SLIME_BOSS", StringComparison.Ordinal) =>
                "High burst before 50% split (deals 35+ crush); slimed/status card handling.",
            var s when s.Contains("HEXAGHOST", StringComparison.Ordinal) =>
                "12x hit attack turn 2 scales with your current HP; requires fast frontload scaling.",
            var s when s.Contains("GUARDIAN", StringComparison.Ordinal) =>
                "Burst during Offensive mode; block 4x9 Whirlwind; Defensive mode punishes multi-hits.",
            var s when s.Contains("CHAMP", StringComparison.Ordinal) =>
                "Execute at < 50% HP cleanses debuffs and buffs Strength; prepare cleanse & scaling.",
            var s when s.Contains("COLLECTOR", StringComparison.Ordinal) =>
                "AoE to clear Torchheads; burst down before Mega-Debuff (Vulnerable/Weak/Frail) Turn 4.",
            var s when s.Contains("AUTOMATON", StringComparison.Ordinal) =>
                "Hyperbeam deals 50+ DMG Turn 6; kill Bronze Orbs stealing your highest-impact cards.",
            var s when s.Contains("TIME_EATER", StringComparison.Ordinal) =>
                "Limits card plays to 12 per turn; prioritize high-impact cards over low-cost spam.",
            var s when s.Contains("AWAKENED_ONE", StringComparison.Ordinal) =>
                "Phase 1 gains +2 Strength per Power played; save high-damage burst for Phase 2 revival.",
            var s when s.Contains("DONU", StringComparison.Ordinal) || s.Contains("DECA", StringComparison.Ordinal) =>
                "Focus Donu first to halt permanent +3 Strength scaling every 2 turns; scalable defense.",
            var s when s.Contains("HEART", StringComparison.Ordinal) =>
                "Beat of Death damages per card played; prepare for 2x15 multi-attack and 45+ single hit.",
            _ => "Prepare multi-turn scaling, high-impact defense, and steady damage mitigation."
        };

        return new BossPrepInfo(normId, displayName, tip, false);
    }

    public static string FormatBossPrepBannerText(BossPrepInfo info) =>
        $"Act Boss: {info.DisplayName} · {info.StrategicTip}";

    public static string FormatBossName(string normalizedId)
    {
        if (string.IsNullOrWhiteSpace(normalizedId)) return "Unknown Boss";

        string clean = normalizedId.Replace("ENCOUNTER_", "").Replace("BOSS_", "");
        var parts = clean.Split('_', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
            {
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1).ToLowerInvariant();
            }
        }
        return string.Join(" ", parts);
    }

    private static string NormalizeCategory(string? type) => (type?.Trim()) switch
    {
        "Attack" => "Attack",
        "Skill" => "Skill",
        "Power" => "Power",
        _ => "Other"
    };
}
