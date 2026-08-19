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

    public HttpTierListProvider(HttpClient httpClient, LocalCacheManager cacheManager, OverlayConfig config)
    {
        _httpClient = httpClient;
        _cacheManager = cacheManager;
        _config = config;
    }

    public async Task<IReadOnlyDictionary<string, CardTierData>> GetTierListAsync(bool forceRefresh = false)
    {
        if (_memoryCache != null && !forceRefresh)
        {
            return _memoryCache;
        }

        // Try to load from local cache first for speed (instant startup)
        if (!forceRefresh)
        {
            var cached = await _cacheManager.LoadFromCacheAsync();
            if (cached != null)
            {
                _memoryCache = cached;
                // Start a background fetch to update the cache in the background without blocking the UI
                _ = FetchAndCacheRemoteDataAsync();
                return _memoryCache;
            }
        }

        return await FetchAndCacheRemoteDataAsync();
    }

    private async Task<IReadOnlyDictionary<string, CardTierData>> FetchAndCacheRemoteDataAsync()
    {
        try
        {
            var json = await _httpClient.GetStringAsync(_config.RemoteUrl);
            var data = JsonSerializer.Deserialize<Dictionary<string, CardTierData>>(json);
            if (data != null)
            {
                _memoryCache = data;
                await _cacheManager.SaveToCacheAsync(data);
                return data;
            }
        }
        catch (Exception)
        {
            // Log exception or handle network failure gracefully
            // Fallback to expired cache if available
            var expiredCache = await _cacheManager.LoadFromCacheAsync();
            if (expiredCache != null)
            {
                return expiredCache;
            }
        }

        return new Dictionary<string, CardTierData>(); // Empty fallback to prevent crashes
    }
}
