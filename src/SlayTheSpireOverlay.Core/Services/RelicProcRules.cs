namespace SlayTheSpireOverlay.Core.Services;

/// <summary>Current progress of a counter relic towards its next proc.</summary>
public sealed record RelicProcInfo(string RelicId, int CurrentCount, int MaxCount, string? ProcEffect)
{
    /// <summary>True when the relic is exactly one tick away from triggering.</summary>
    public bool IsPreProc => CurrentCount == MaxCount - 1;

    /// <summary>Whether the counter counts turns ("[T:5/6]") rather than card plays ("[9/10]").</summary>
    public bool CountsTurns { get; init; }

    public string BadgeText => CountsTurns ? $"[T:{CurrentCount}/{MaxCount}]" : $"[{CurrentCount}/{MaxCount}]";
}

/// <summary>Pure rules for the relics that proc on a multi-turn or multi-action cycle.</summary>
public static class RelicProcRules
{
    public const string PreProcColorHex = "#ffdd44";

    private sealed record Rule(int DefaultMax, bool CountsTurns, string Effect);

    private static readonly Dictionary<string, Rule> Rules = new(StringComparer.Ordinal)
    {
        ["PENNIB"] = new(10, false, "Next attack deals double damage"),
        ["INCENSEBURNER"] = new(6, true, "Gain 1 Intangible"),
        ["HAPPYFLOWER"] = new(3, true, "Gain 1 Energy"),
        ["KUNAI"] = new(3, false, "Gain 1 Dexterity"),
        ["SHURIKEN"] = new(3, false, "Gain 1 Strength"),
        ["STONECALENDAR"] = new(7, true, "Deal damage to all enemies"),
    };

    /// <summary>Normalises "RELIC:PEN_NIB", "PEN_NIB" and "PenNib" to "PENNIB".</summary>
    public static string NormalizeId(string? relicId)
    {
        if (string.IsNullOrEmpty(relicId)) return string.Empty;
        int colon = relicId.LastIndexOf(':');
        string entry = colon >= 0 ? relicId[(colon + 1)..] : relicId;
        return new string(entry.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    }

    public static bool IsSupported(string? relicId) => Rules.ContainsKey(NormalizeId(relicId));

    /// <summary>
    /// Builds proc info for a supported relic. Returns null for unsupported relics or a negative/unknown count.
    /// <paramref name="maxOverride"/> (read from the game's own threshold) wins over the built-in default when positive.
    /// </summary>
    public static RelicProcInfo? TryGetProcInfo(string? relicId, int? currentCount, int? maxOverride = null)
    {
        if (currentCount is null or < 0) return null;
        if (!Rules.TryGetValue(NormalizeId(relicId), out var rule)) return null;
        int max = maxOverride is > 0 ? maxOverride.Value : rule.DefaultMax;
        return new RelicProcInfo(NormalizeId(relicId), currentCount.Value, max, rule.Effect) { CountsTurns = rule.CountsTurns };
    }
}
