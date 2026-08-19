using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SlayTheSpireOverlay.Core.Models;
using SlayTheSpireOverlay.Core.Options;

namespace SlayTheSpireOverlay.Core.Services;

public class LocalCacheManager
{
    private readonly CacheOptions _options;

    public LocalCacheManager(CacheOptions options)
    {
        _options = options;
    }

    public async Task SaveToCacheAsync(IReadOnlyDictionary<string, CardTierData> data)
    {
        if (string.IsNullOrEmpty(_options.CacheDirectory)) return;
        
        Directory.CreateDirectory(_options.CacheDirectory);
        var path = Path.Combine(_options.CacheDirectory, _options.CacheFileName);
        
        var json = JsonSerializer.Serialize(data);
        await File.WriteAllTextAsync(path, json);
    }

    public async Task<IReadOnlyDictionary<string, CardTierData>?> LoadFromCacheAsync()
    {
        var path = Path.Combine(_options.CacheDirectory, _options.CacheFileName);
        if (!File.Exists(path)) return null;

        // Check expiry
        if (File.GetLastWriteTime(path).AddHours(_options.CacheExpiryHours) < DateTime.Now)
        {
            return null; // Cache expired
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<Dictionary<string, CardTierData>>(json);
        }
        catch
        {
            return null; // Corrupted cache fallback
        }
    }
}
