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
    public bool EnableQuickToggleBar { get; set; } = true;
    public bool EnableControllerChords { get; set; } = true;
    public bool EnableDeckSynergyBar { get; set; } = true;
    public bool EnableBossPrepBanner { get; set; } = true;
    public bool EnableOutgoingDamageSummary { get; set; } = true;
    public bool EnableDeckTrackerCardHover { get; set; } = true;
    public bool EnableRelicProcCounters { get; set; } = true;
    public string ToggleAuthorKey { get; set; } = "F6";
    public string CollapseDeckTrackerKey { get; set; } = "F7";
    public string ToggleOverlayKey { get; set; } = "F8";
    public string RefreshTiersKey { get; set; } = "F9";
    public string PinShopItemKey { get; set; } = "F5";
    public float UiScale { get; set; } = 1.0f;
    public float DeckTrackerOffsetX { get; set; }
    public float DeckTrackerOffsetY { get; set; }
    public float AttackSummaryOffsetX { get; set; }
    public float AttackSummaryOffsetY { get; set; }
}
