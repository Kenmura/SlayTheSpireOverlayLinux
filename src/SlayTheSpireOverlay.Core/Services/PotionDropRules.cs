namespace SlayTheSpireOverlay.Core.Services;

/// <summary>
/// Presentation rules for the player's potion reward odds (<c>Player.PlayerOdds.PotionReward.CurrentValue</c>, 0..1).
/// The game rolls these odds when rewards are generated and then moves them by +/-0.1, so on the reward screen the value
/// is already the chance for the next normal combat. Elites add a further 0.125 to the roll threshold (not shown here).
/// </summary>
public static class PotionDropRules
{
    /// <summary>Whole percent, clamped to 0..100; null when the value is unknown or not a finite number.</summary>
    public static int? ToPercent(float? odds)
    {
        if (odds is not { } value || !float.IsFinite(value)) return null;
        // Rounding to 4 decimals first absorbs float drift such as 0.70000005 after repeated +/-0.1 steps.
        int percent = (int)Math.Round(Math.Round(value, 4) * 100, MidpointRounding.AwayFromZero);
        return Math.Clamp(percent, 0, 100);
    }

    public static string? FormatLabel(float? odds) =>
        ToPercent(odds) is { } percent ? $"🧪 Potion Drop Chance: {percent}%" : null;
}
