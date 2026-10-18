using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SlayTheSpireOverlay.Core.Interfaces;
using SlayTheSpireOverlay.Core.Models;
using SlayTheSpireOverlay.Core.Options;

namespace SlayTheSpireOverlay.Core.Services;

public class HttpTierListProvider : ITierListProvider
{
    private static readonly string[] BaalorlordUuids =
    {
        "004de170-026a-4dd4-a280-3b904be0b5d6", // Ironclad
        "6d61ea21-0552-4c49-8bb5-a5c15530fc00", // Silent
        "0e6c1e23-bec6-4887-a9e0-dbf49ede974d", // Regent
        "43d0b41f-7d6d-4ce9-928e-c1310a413983", // Necrobinder
        "5a512e04-4583-4a16-9271-d46864c6cb4c", // Defect
        "0a626c22-dc49-433e-ac6e-76cc9abf5684"  // Colorless / Relics / Potions
    };

    private static readonly string[] JorbsUuids =
    {
        "6b3390b7-bcbc-4ebc-ae78-ae9a400c66dc", // Ironclad
        "2cd72d4b-eeb4-42ab-89ca-4ac682137672", // Silent
        "c47dbfda-4983-4107-8c9c-c33d5ae32b23", // Regent
        "3ae661e6-6190-4a45-b00e-63e8400fff94", // Necrobinder
        "5f8d3e04-1f50-4b56-b51a-d8650b000ce3", // Defect
        "81684acf-26a7-4e44-a649-84f4c2824ec0"  // Colorless / Relics / Potions
    };

    private const string EventCommentaryUrl = "https://sts2.untapped.gg/api/v1/commentary/events";

    private readonly HttpClient _httpClient;
    private readonly LocalCacheManager _cacheManager;
    private readonly OverlayConfig _config;
    private IReadOnlyDictionary<string, CardTierData>? _memoryCache;
    private string? _loadedAuthor;
    private string? _loadedRemoteUrl;

    private readonly object _lock = new object();
    private Task<IReadOnlyDictionary<string, CardTierData>>? _initTask;

    public HttpTierListProvider(HttpClient httpClient, LocalCacheManager cacheManager, OverlayConfig config)
    {
        _httpClient = httpClient;
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (X11; Linux x86_64) SlayTheSpireOverlayLinux/1.0");
        }
        _cacheManager = cacheManager;
        _config = config;
    }

    public static string[] GetUuidsForAuthor(string? author) =>
        string.Equals(author?.Trim(), "Jorbs", StringComparison.OrdinalIgnoreCase)
            ? JorbsUuids
            : BaalorlordUuids;

    public static string? GetCacheFileOverride(string? author, string? remoteUrl) =>
        IsUntappedDefaultUrl(remoteUrl) && string.Equals(author?.Trim(), "Jorbs", StringComparison.OrdinalIgnoreCase)
            ? "tier_list_cache_jorbs.json"
            : null;

    public Task<IReadOnlyDictionary<string, CardTierData>> GetTierListAsync(bool forceRefresh = false)
    {
        string currentAuthor = _config.Author?.Trim() ?? "Baalorlord";
        string currentUrl = _config.RemoteUrl?.Trim() ?? "untapped";
        bool contextChanged = !string.Equals(_loadedAuthor, currentAuthor, StringComparison.OrdinalIgnoreCase) ||
                              !string.Equals(_loadedRemoteUrl, currentUrl, StringComparison.OrdinalIgnoreCase);

        if (_memoryCache != null && !forceRefresh && !contextChanged)
        {
            return Task.FromResult(_memoryCache);
        }

        lock (_lock)
        {
            contextChanged = !string.Equals(_loadedAuthor, currentAuthor, StringComparison.OrdinalIgnoreCase) ||
                             !string.Equals(_loadedRemoteUrl, currentUrl, StringComparison.OrdinalIgnoreCase);
            if (_initTask == null || forceRefresh || contextChanged)
            {
                _loadedAuthor = currentAuthor;
                _loadedRemoteUrl = currentUrl;
                _memoryCache = null;
                _initTask = LoadDataInternalAsync(forceRefresh, currentAuthor);
            }
            return _initTask;
        }
    }

    private async Task<IReadOnlyDictionary<string, CardTierData>> LoadSingleAuthorDataAsync(bool forceRefresh, string? author)
    {
        string safeAuthor = string.IsNullOrWhiteSpace(author) ? "Baalorlord" : author.Trim();
        string? cacheFileOverride = GetCacheFileOverride(safeAuthor, _config.RemoteUrl);
        if (!forceRefresh)
        {
            var cached = await _cacheManager.LoadFromCacheAsync(ignoreExpiry: false, cacheFileOverride).ConfigureAwait(false);
            if (cached != null)
            {
                return NormalizeDictionary(cached);
            }
        }

        return await FetchAndCacheRemoteDataAsync(safeAuthor, cacheFileOverride).ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, CardTierData>> LoadDataInternalAsync(bool forceRefresh, string? author)
    {
        string safeAuthor = string.IsNullOrWhiteSpace(author) ? "Baalorlord" : author.Trim();
        if (string.Equals(safeAuthor, "Both", StringComparison.OrdinalIgnoreCase))
        {
            var baalorTask = LoadSingleAuthorDataAsync(forceRefresh, "Baalorlord");
            var jorbsTask = LoadSingleAuthorDataAsync(forceRefresh, "Jorbs");
            await Task.WhenAll(baalorTask, jorbsTask).ConfigureAwait(false);

            var merged = OverlayCalculators.MergeDualTierLists(baalorTask.Result, jorbsTask.Result);
            _memoryCache = NormalizeDictionary(merged);
            return _memoryCache;
        }

        var singleResult = await LoadSingleAuthorDataAsync(forceRefresh, safeAuthor).ConfigureAwait(false);
        _memoryCache = singleResult;
        return _memoryCache;
    }

    private async Task<IReadOnlyDictionary<string, CardTierData>> FetchAndCacheRemoteDataAsync(string author, string? cacheFileOverride)
    {
        var existingCache = await _cacheManager.LoadFromCacheAsync(ignoreExpiry: true, cacheFileOverride).ConfigureAwait(false);
        try
        {
            var merged = existingCache != null
                ? new Dictionary<string, CardTierData>(existingCache, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, CardTierData>(StringComparer.OrdinalIgnoreCase);

            int fetchedCount = 0;
            if (IsUntappedDefaultUrl(_config.RemoteUrl))
            {
                foreach (string uuid in GetUuidsForAuthor(author))
                {
                    try
                    {
                        string url = $"https://api.sts2.untapped.gg/api/v1/tier-lists/{uuid}/resolve/?game_version=public";
                        var json = await _httpClient.GetStringAsync(url).ConfigureAwait(false);
                        fetchedCount += ParseIntoDictionary(json, merged);
                    }
                    catch
                    {
                        // Continue fetching remaining character lists if one UUID fails
                    }
                }

                try
                {
                    var eventsJson = await _httpClient.GetStringAsync(EventCommentaryUrl).ConfigureAwait(false);
                    fetchedCount += ParseEventCommentary(eventsJson, author, merged);
                }
                catch
                {
                    // Event commentary is optional
                }
            }
            else
            {
                var json = await _httpClient.GetStringAsync(_config.RemoteUrl).ConfigureAwait(false);
                fetchedCount += ParseIntoDictionary(json, merged);
            }

            if (fetchedCount > 0)
            {
                var normalizedData = NormalizeDictionary(merged);
                _memoryCache = normalizedData;
                await _cacheManager.SaveToCacheAsync(normalizedData, cacheFileOverride).ConfigureAwait(false);
                return normalizedData;
            }
        }
        catch (Exception)
        {
            // Fall through to author-matched cache fallback below
        }

        if (existingCache != null)
        {
            _memoryCache = NormalizeDictionary(existingCache);
            return _memoryCache;
        }

        return new Dictionary<string, CardTierData>(StringComparer.OrdinalIgnoreCase);
    }

    public static int ParseIntoDictionary(string json, IDictionary<string, CardTierData> target)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return 0;

        int count = 0;

        // 1. Live Untapped.gg /api/v1/tier-lists/{uuid}/resolve/ payload
        if (root.TryGetProperty("tier_list", out var tl) &&
            tl.ValueKind == JsonValueKind.Object &&
            tl.TryGetProperty("tiers", out var tiers) &&
            tiers.ValueKind == JsonValueKind.Array)
        {
            string listName = tl.TryGetProperty("name", out var n) ? n.GetString() ?? "Tier List" : "Tier List";
            foreach (var tier in tiers.EnumerateArray())
            {
                string tierName = tier.TryGetProperty("name", out var tn) ? tn.GetString() ?? "N/A" : "N/A";
                double score = TierToScore(tierName);

                count += ReadUntappedItems(tier, "cards", "card_id", tierName, score, listName, target);
                count += ReadUntappedItems(tier, "relics", "item_id", tierName, score, listName, target);
                count += ReadUntappedItems(tier, "potions", "item_id", tierName, score, listName, target);
            }
            return count;
        }

        // 2. Flat Dictionary<string, CardTierData> JSON payload
        var flat = JsonSerializer.Deserialize<Dictionary<string, CardTierData>>(json);
        if (flat != null)
        {
            foreach (var kvp in flat)
            {
                target[kvp.Key] = kvp.Value;
                count++;
            }
        }
        return count;
    }

    public static int ParseEventCommentary(string json, string? preferredAuthor, IDictionary<string, CardTierData> target)
    {
        if (string.IsNullOrWhiteSpace(json)) return 0;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return 0;

        // Payload shape: { "Baalorlord": { "EVENT_ID": "Commentary..." } }
        JsonElement authorObj = default;
        string matchedAuthor = "Baalorlord";
        bool found = false;
        if (!string.IsNullOrWhiteSpace(preferredAuthor) &&
            root.TryGetProperty(preferredAuthor.Trim(), out authorObj) &&
            authorObj.ValueKind == JsonValueKind.Object)
        {
            matchedAuthor = preferredAuthor.Trim();
            found = true;
        }
        else if (root.TryGetProperty("Baalorlord", out authorObj) && authorObj.ValueKind == JsonValueKind.Object)
        {
            matchedAuthor = "Baalorlord";
            found = true;
        }

        if (!found) return 0;

        int count = 0;
        foreach (var prop in authorObj.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.String) continue;
            string commentary = prop.Value.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(commentary)) continue;

            string eventId = OverlayCalculators.NormalizeId(prop.Name);
            if (string.IsNullOrEmpty(eventId)) continue;

            target[$"EVENT_{eventId}"] = new CardTierData(eventId, matchedAuthor, 0.0, commentary);
            count++;
        }
        return count;
    }

    private static int ReadUntappedItems(
        JsonElement tierElem,
        string arrayProp,
        string idProp,
        string tierName,
        double score,
        string listName,
        IDictionary<string, CardTierData> target)
    {
        if (!tierElem.TryGetProperty(arrayProp, out var arr) || arr.ValueKind != JsonValueKind.Array) return 0;

        int totalItems = arr.GetArrayLength();
        int idx = 0;
        int added = 0;
        foreach (var item in arr.EnumerateArray())
        {
            int currentIndex = idx++;
            if (!item.TryGetProperty(idProp, out var idEl) || idEl.ValueKind != JsonValueKind.String) continue;
            string id = OverlayCalculators.NormalizeId(idEl.GetString());
            if (string.IsNullOrEmpty(id)) continue;

            string commentary = item.TryGetProperty("commentary", out var cEl) && cEl.ValueKind == JsonValueKind.String
                ? cEl.GetString() ?? ""
                : "";
            if (string.IsNullOrWhiteSpace(commentary))
            {
                commentary = $"{id} [{listName}] - Rated '{tierName}'.";
            }

            double itemScore = score > 0 && totalItems > 1
                ? Math.Round(score + 4.0 * (1.0 - (2.0 * currentIndex / (totalItems - 1))), 1)
                : score;

            target[id] = new CardTierData(id, tierName, itemScore, commentary);
            added++;
        }
        return added;
    }

    private static bool IsUntappedDefaultUrl(string? url) =>
        string.IsNullOrWhiteSpace(url) ||
        url.Equals("untapped", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("raw.githubusercontent.com/community/sts2-tierlist", StringComparison.OrdinalIgnoreCase);

    private static double TierToScore(string tier) => tier.Trim().ToUpperInvariant() switch
    {
        "S" or "S TIER" or "ALWAYS AMAZING" => 95.0,
        "A" or "A TIER" or "ALWAYS GOOD" => 80.0,
        "B" or "B TIER" or "GREAT" or "GOOD" => 65.0,
        "C" or "C TIER" or "NEEDS SYNERGY" => 50.0,
        "D" or "D TIER" or "INCONSISTENT" or "SITUATIONAL" or "MAP DEPENDENT" => 35.0,
        "F" or "F TIER" or "ALMOST NEVER" or "NEVER TAKE" or "SKIP" => 15.0,
        _ => 0.0
    };

    private static Dictionary<string, CardTierData> NormalizeDictionary(IReadOnlyDictionary<string, CardTierData> source)
    {
        var normalized = new Dictionary<string, CardTierData>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in source)
        {
            string key = kvp.Key.ToUpperInvariant();
            normalized[key] = kvp.Value;
            string stripped = key.Replace("_", "");
            if (stripped != key)
            {
                normalized.TryAdd(stripped, kvp.Value);
            }
        }
        return normalized;
    }
}
