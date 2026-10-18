using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SlayTheSpireOverlay.Core.Services;

public record DeckCardInfo(
    string Id,
    string Title,
    string Type,
    int? Cost,
    bool CostsX = false,
    int UpgradeLevel = 0,
    IEnumerable<string>? Keywords = null,
    string? Description = null);

public record DeckSynergySummary(
    int TotalCards,
    int Attacks,
    int Skills,
    int Powers,
    int Other,
    double AverageCost,
    IReadOnlyDictionary<string, int> CostDistribution,
    IReadOnlyList<(string Tag, int Count)> TopMechanicTags,
    string FormattedSummary);

/// <summary>
/// Pure, unit-testable analyzer for master deck composition, energy curve,
/// and archetype synergy tags (Block, Draw, Energy, Poison, Exhaust, Strength, etc.).
/// </summary>
public static class DeckSynergyAnalyzer
{
    private static readonly (string Tag, Regex Pattern)[] SynergyRules =
    {
        ("Block", new Regex(@"\b(BLOCK|DEFEND|BARRIER|ARMOR)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("Draw", new Regex(@"\b(DRAW|DRAWS|DRAWING|CYCLE|CYCLING)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("Energy", new Regex(@"\b(ENERGY|PLASMA)\b|\[E\]", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("Exhaust", new Regex(@"\b(EXHAUST|EXHAUSTS|EXHAUSTED)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("Vulnerable", new Regex(@"\b(VULNERABLE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("Weak", new Regex(@"\b(WEAK)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("Strength", new Regex(@"\b(STRENGTH)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("Dexterity", new Regex(@"\b(DEXTERITY)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("Poison", new Regex(@"\b(POISON|POISONED|ENVENOM|CATALYST)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("Shiv", new Regex(@"\b(SHIV|SHIVS|BLADE|BLADES)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("Summon", new Regex(@"\b(SUMMON|SUMMONS|MINION|MINIONS|OSTY|UNDEAD|SOUL|SOULS|TOMBSTONE|GRAVE)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("Star", new Regex(@"\b(STAR|STARS|STELLAR|COSMIC)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("Orb", new Regex(@"\b(ORB|ORBS|CHANNEL|EVOKE|LIGHTNING|FROST|DARK)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)),
        ("Retain", new Regex(@"\b(RETAIN|RETAINED)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase))
    };

    public static DeckSynergySummary AnalyzeDeck(IEnumerable<DeckCardInfo>? cards, int maxTopTags = 5)
    {
        var cardList = cards?.ToList() ?? new List<DeckCardInfo>();
        int total = cardList.Count;

        int attacks = 0, skills = 0, powers = 0, other = 0;
        int cost0 = 0, cost1 = 0, cost2 = 0, cost3Plus = 0, costX = 0, unplayable = 0;
        double costSum = 0;
        int playableCount = 0;

        var tagCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in cardList)
        {
            // Category counting
            string type = (card.Type ?? string.Empty).Trim();
            if (type.Equals("Attack", StringComparison.OrdinalIgnoreCase)) attacks++;
            else if (type.Equals("Skill", StringComparison.OrdinalIgnoreCase)) skills++;
            else if (type.Equals("Power", StringComparison.OrdinalIgnoreCase)) powers++;
            else other++;

            // Cost counting
            if (card.CostsX)
            {
                costX++;
            }
            else if (card.Cost.HasValue && card.Cost.Value >= 0)
            {
                int c = card.Cost.Value;
                costSum += c;
                playableCount++;
                if (c == 0) cost0++;
                else if (c == 1) cost1++;
                else if (c == 2) cost2++;
                else cost3Plus++;
            }
            else
            {
                unplayable++;
            }

            // Synergy tag detection
            var detectedForCard = DetectSynergyTags(card);
            foreach (string tag in detectedForCard)
            {
                tagCounts[tag] = tagCounts.TryGetValue(tag, out int current) ? current + 1 : 1;
            }
        }

        double avgCost = playableCount > 0 ? Math.Round(costSum / playableCount, 2) : 0.0;

        var costDist = new Dictionary<string, int>
        {
            ["0"] = cost0,
            ["1"] = cost1,
            ["2"] = cost2,
            ["3+"] = cost3Plus,
            ["X"] = costX,
            ["-"] = unplayable
        };

        var topTags = tagCounts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Take(Math.Max(1, maxTopTags))
            .Select(kv => (kv.Key, kv.Value))
            .ToList();

        string formatted = FormatSummaryText(total, attacks, skills, powers, other, avgCost, topTags);

        return new DeckSynergySummary(
            total,
            attacks,
            skills,
            powers,
            other,
            avgCost,
            costDist,
            topTags,
            formatted);
    }

    public static HashSet<string> DetectSynergyTags(DeckCardInfo card)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string normalizedId = OverlayCalculators.NormalizeId(card.Id).Replace('_', ' ');
        string title = card.Title ?? string.Empty;
        string desc = card.Description ?? string.Empty;

        string combined = $"{normalizedId} {title} {desc}";
        if (card.Keywords != null)
        {
            combined += " " + string.Join(" ", card.Keywords.Select(k => OverlayCalculators.NormalizeId(k).Replace('_', ' ')));
        }

        foreach (var (tag, pattern) in SynergyRules)
        {
            if (pattern.IsMatch(combined))
            {
                found.Add(tag);
            }
        }

        return found;
    }

    private static string FormatSummaryText(
        int total,
        int attacks,
        int skills,
        int powers,
        int other,
        double avgCost,
        IReadOnlyList<(string Tag, int Count)> topTags)
    {
        string otherPart = other > 0 ? $" / {other} Other" : "";
        string typeBreakdown = $"{attacks}A / {skills}S / {powers}P{otherPart}";

        string tagsPart = topTags.Count > 0
            ? " · " + string.Join(", ", topTags.Select(t => $"{t.Tag} ({t.Count})"))
            : "";

        string costStr = avgCost.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return $"Deck: {total} Cards ({typeBreakdown}) · Avg Cost: {costStr}{tagsPart}";
    }
}
