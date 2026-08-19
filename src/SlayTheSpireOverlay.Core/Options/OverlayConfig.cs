namespace SlayTheSpireOverlay.Core.Options;

public class OverlayConfig
{
    public string RemoteUrl { get; set; } = "https://raw.githubusercontent.com/community/sts2-tierlist/main/tiers.json";
    public int CacheExpiryHours { get; set; } = 24;
}
