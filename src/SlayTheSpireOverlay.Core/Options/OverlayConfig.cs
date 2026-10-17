namespace SlayTheSpireOverlay.Core.Options;

public class OverlayConfig
{
    /// <summary>
    /// Defaults to Untapped.gg's live STS2 tier list API ("untapped").
    /// Can be overridden with a custom JSON URL if desired.
    /// </summary>
    public string RemoteUrl { get; set; } = "untapped";

    /// <summary>
    /// Tier list creator to use when RemoteUrl is "untapped" ("Baalorlord" or "Jorbs").
    /// </summary>
    public string Author { get; set; } = "Baalorlord";

    public int CacheExpiryHours { get; set; } = 24;
    public bool EnableOverlay { get; set; } = true;
    public bool EnableCardBadges { get; set; } = true;
    public bool EnableRelicBadges { get; set; } = true;
    public bool EnablePotionBadges { get; set; } = true;
    public bool EnableHoverCommentary { get; set; } = true;
    public bool EnableAttackSummary { get; set; } = true;
    public bool EnableDeckTracker { get; set; } = true;
    public bool EnableMerchantGoldLeft { get; set; } = true;
    public bool EnableEventCommentary { get; set; } = true;
}
