using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SlayTheSpireOverlay.Core.Services;

public record AttackSummary(
    int TotalDamage,
    int TotalHits,
    int AttackingEnemies,
    int UnblockedDamage = 0,
    bool IsLethal = false);

public record CombatCard(string Id, string Title, string Type, int? Cost, bool CostsX, int UpgradeLevel, bool IsPermanent = true);

public record DeckCardGroup(
    string Title,
    int? Cost,
    bool CostsX,
    int UpgradeLevel,
    int InDraw,
    int Total,
    int NextHandPercent = 0);

public record DeckCategorySummary(string Category, int InDraw, int Total, int ProbabilityPercent, IReadOnlyList<DeckCardGroup> Cards);

public record DeckTrackerState(
    int DrawCount,
    IReadOnlyList<DeckCategorySummary> Categories,
    int DiscardCount = 0,
    int ExhaustCount = 0,
    int HandCount = 0);

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
        string.IsNullOrEmpty(text) ? string.Empty : BbCodeRegex.Replace(text, "").Trim();

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

    public static AttackSummary CalculateIncomingAttacks(
        IEnumerable<(int CurrentHp, IEnumerable<(string? IntentType, string? LabelText)> Intents)>? enemies,
        int playerBlock = 0,
        int playerHp = 0)
    {
        if (enemies == null) return new AttackSummary(0, 0, 0, 0, false);

        int totalDamage = 0, totalHits = 0, attackers = 0;
        foreach (var (hp, intents) in enemies)
        {
            if (hp <= 0 || intents == null) continue;
            bool attacking = false;

            foreach (var (type, rawText) in intents)
            {
                if (string.IsNullOrEmpty(type) || !type.Contains("AttackIntent", StringComparison.OrdinalIgnoreCase) &&
                    !type.EndsWith("DeathBlowIntent", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string clean = StripBbCode(rawText);
                var multi = MultiHitRegex.Match(clean);
                if (multi.Success && int.TryParse(multi.Groups[1].Value, out int dmg) && int.TryParse(multi.Groups[2].Value, out int hits) && hits > 0)
                {
                    totalDamage += dmg * hits;
                    totalHits += hits;
                    attacking = true;
                    continue;
                }

                var single = SingleHitRegex.Match(clean);
                if (single.Success && int.TryParse(single.Groups[1].Value, out int sDmg))
                {
                    totalDamage += sDmg;
                    totalHits += 1;
                    attacking = true;
                }
            }

            if (attacking) attackers++;
        }

        int unblocked = Math.Max(0, totalDamage - Math.Max(0, playerBlock));
        bool isLethal = totalHits > 0 && playerHp > 0 && unblocked >= playerHp;
        return new AttackSummary(totalDamage, totalHits, attackers, unblocked, isLethal);
    }

    public static DeckTrackerState CalculateDeckTracker(
        IReadOnlyCollection<CombatCard>? drawPile,
        IReadOnlyCollection<CombatCard>? discardPile,
        IReadOnlyCollection<CombatCard>? handPile,
        int exhaustCount = 0)
    {
        var draw = drawPile ?? Array.Empty<CombatCard>();
        var discard = discardPile ?? Array.Empty<CombatCard>();
        var hand = handPile ?? Array.Empty<CombatCard>();

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
                        ? CalculateHypergeometricAtLeastOne(drawTotal, inDraw, 5)
                        : (drawTotal < 5 && discardTotal > 0 && inDiscard > 0
                            ? CalculateHypergeometricAtLeastOne(discardTotal, inDiscard, 5 - drawTotal)
                            : 0);

                    string cleanTitle = StripBbCode(first.Title);
                    if (string.IsNullOrEmpty(cleanTitle)) cleanTitle = first.Id;
                    if (first.UpgradeLevel > 0 && !cleanTitle.EndsWith('+'))
                    {
                        cleanTitle += first.UpgradeLevel > 1 ? $"+{first.UpgradeLevel}" : "+";
                    }

                    return new DeckCardGroup(cleanTitle, first.Cost, first.CostsX, first.UpgradeLevel, inDraw, g.Count(), nextHandPct);
                })
                .OrderByDescending(c => c.InDraw > 0)
                .ThenBy(c => c.CostsX ? 1 : 2)
                .ThenBy(c => c.Cost ?? -1)
                .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            groups.Add(new DeckCategorySummary(cat, catInDraw, matching.Count, pct, cards));
        }

        return new DeckTrackerState(drawTotal, groups, discardTotal, Math.Max(0, exhaustCount), hand.Count);
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

    public static (int RemainingGold, int Cost)? CalculateGoldLeft(
        int playerGold,
        IEnumerable<(int Cost, bool IsStocked, bool IsHovered)>? slots)
    {
        if (slots == null) return null;
        foreach (var (cost, isStocked, isHovered) in slots)
        {
            if (isHovered && isStocked && cost >= 0)
            {
                return (playerGold - cost, cost);
            }
        }
        return null;
    }

    private static string NormalizeCategory(string? type) => (type?.Trim()) switch
    {
        "Attack" => "Attack",
        "Skill" => "Skill",
        "Power" => "Power",
        _ => "Other"
    };
}
