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

    public async Task SaveToCacheAsync(IReadOnlyDictionary<string, CardTierData> data, string? fileNameOverride = null)
    {
        if (string.IsNullOrEmpty(_options.CacheDirectory)) return;

        Directory.CreateDirectory(_options.CacheDirectory);
        string fileName = string.IsNullOrWhiteSpace(fileNameOverride) ? _options.CacheFileName : fileNameOverride;
        var path = Path.Combine(_options.CacheDirectory, fileName);
        var tmpPath = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            var json = JsonSerializer.Serialize(data);
            await File.WriteAllTextAsync(tmpPath, json).ConfigureAwait(false);
            File.Move(tmpPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tmpPath))
            {
                try { File.Delete(tmpPath); } catch { }
            }
        }
    }

    public async Task<IReadOnlyDictionary<string, CardTierData>?> LoadFromCacheAsync(bool ignoreExpiry = false, string? fileNameOverride = null)
    {
        if (string.IsNullOrEmpty(_options.CacheDirectory)) return null;

        string fileName = string.IsNullOrWhiteSpace(fileNameOverride) ? _options.CacheFileName : fileNameOverride;
        var path = Path.Combine(_options.CacheDirectory, fileName);
        if (!File.Exists(path)) return null;

        // Check UTC expiry unless caller explicitly requests stale fallback
        if (!ignoreExpiry && File.GetLastWriteTimeUtc(path).AddHours(_options.CacheExpiryHours) < DateTime.UtcNow)
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            return JsonSerializer.Deserialize<Dictionary<string, CardTierData>>(json);
        }
        catch
        {
            return null;
        }
    }
}
