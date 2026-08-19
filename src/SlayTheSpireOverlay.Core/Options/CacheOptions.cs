namespace SlayTheSpireOverlay.Core.Options;

public class CacheOptions
{
    public string CacheDirectory { get; set; } = string.Empty;
    public string CacheFileName { get; set; } = "tier_list_cache.json";
    public int CacheExpiryHours { get; set; } = 24;
}
