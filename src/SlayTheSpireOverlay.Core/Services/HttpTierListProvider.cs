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
    private readonly HttpClient _httpClient;
    private readonly LocalCacheManager _cacheManager;
    private readonly OverlayConfig _config;
    private IReadOnlyDictionary<string, CardTierData>? _memoryCache;

    private readonly object _lock = new object();
    private Task<IReadOnlyDictionary<string, CardTierData>>? _initTask;

    public HttpTierListProvider(HttpClient httpClient, LocalCacheManager cacheManager, OverlayConfig config)
    {
        _httpClient = httpClient;
        _cacheManager = cacheManager;
        _config = config;
    }

    public Task<IReadOnlyDictionary<string, CardTierData>> GetTierListAsync(bool forceRefresh = false)
    {
        if (_memoryCache != null && !forceRefresh)
        {
            return Task.FromResult(_memoryCache);
        }

        lock (_lock)
        {
            if (_initTask == null || forceRefresh)
            {
                _initTask = LoadDataInternalAsync(forceRefresh);
            }
            return _initTask;
        }
    }

    private async Task<IReadOnlyDictionary<string, CardTierData>> LoadDataInternalAsync(bool forceRefresh)
    {
        // Try to load from local cache first for speed (instant startup)
        if (!forceRefresh)
        {
            var cached = await _cacheManager.LoadFromCacheAsync().ConfigureAwait(false);
            if (cached != null)
            {
                var normalizedCached = new Dictionary<string, CardTierData>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in cached)
                {
                    string key = kvp.Key.ToUpperInvariant();
                    normalizedCached[key] = kvp.Value;
                    string stripped = key.Replace("_", "");
                    if (stripped != key)
                    {
                        normalizedCached.TryAdd(stripped, kvp.Value);
                    }
                }
                _memoryCache = normalizedCached;
                // Start a background fetch to update the cache in the background without blocking the UI
                _ = Task.Run(async () => await FetchAndCacheRemoteDataAsync().ConfigureAwait(false));
                return _memoryCache;
            }
        }

        return await FetchAndCacheRemoteDataAsync().ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, CardTierData>> FetchAndCacheRemoteDataAsync()
    {
        try
        {
            var json = await _httpClient.GetStringAsync(_config.RemoteUrl).ConfigureAwait(false);
            var data = JsonSerializer.Deserialize<Dictionary<string, CardTierData>>(json);
            if (data != null)
            {
                var normalizedData = new Dictionary<string, CardTierData>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in data)
                {
                    string key = kvp.Key.ToUpperInvariant();
                    normalizedData[key] = kvp.Value;
                    string stripped = key.Replace("_", "");
                    if (stripped != key)
                    {
                        normalizedData.TryAdd(stripped, kvp.Value);
                    }
                }
                _memoryCache = normalizedData;
                await _cacheManager.SaveToCacheAsync(normalizedData).ConfigureAwait(false);
                return normalizedData;
            }
        }
        catch (Exception)
        {
            // Log exception or handle network failure gracefully
            // Fallback to expired cache if available
            var expiredCache = await _cacheManager.LoadFromCacheAsync().ConfigureAwait(false);
            if (expiredCache != null)
            {
                var normalizedExpired = new Dictionary<string, CardTierData>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in expiredCache)
                {
                    string key = kvp.Key.ToUpperInvariant();
                    normalizedExpired[key] = kvp.Value;
                    string stripped = key.Replace("_", "");
                    if (stripped != key)
                    {
                        normalizedExpired.TryAdd(stripped, kvp.Value);
                    }
                }
                return normalizedExpired;
            }
        }

        return new Dictionary<string, CardTierData>(StringComparer.OrdinalIgnoreCase); // Empty fallback to prevent crashes
    }
}
