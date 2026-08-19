using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SlayTheSpireOverlay.Core.Models;
using SlayTheSpireOverlay.Core.Options;
using SlayTheSpireOverlay.Core.Services;

namespace SlayTheSpireOverlay.Tests;

[TestClass]
public class CacheAndMappingTests
{
    private string _tempDirectory = null!;
    private CacheOptions _options = null!;
    private LocalCacheManager _cacheManager = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
        _options = new CacheOptions
        {
            CacheDirectory = _tempDirectory,
            CacheFileName = "test_cache.json",
            CacheExpiryHours = 1
        };
        _cacheManager = new LocalCacheManager(_options);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [TestMethod]
    public async Task TestCacheSaveAndLoad_ValidData_ShouldSerializeAndDeserialize()
    {
        // Arrange
        var testData = new Dictionary<string, CardTierData>
        {
            ["Strike"] = new CardTierData("Strike", "C", 50.0, "Basic damage card")
        };

        // Act
        await _cacheManager.SaveToCacheAsync(testData);
        var loaded = await _cacheManager.LoadFromCacheAsync();

        // Assert
        Assert.IsNotNull(loaded);
        Assert.IsTrue(loaded.ContainsKey("Strike"));
        Assert.AreEqual("C", loaded["Strike"].Tier);
        Assert.AreEqual(50.0, loaded["Strike"].Score);
        Assert.AreEqual("Basic damage card", loaded["Strike"].Commentary);
    }

    [TestMethod]
    public async Task TestHttpProvider_SuccessFetch_ShouldCacheAndReturn()
    {
        // Arrange
        var jsonResponse = "{\"Defend\": {\"CardId\":\"Defend\",\"Tier\":\"B\",\"Score\":70.0,\"Commentary\":\"Basic defense card\"}}";
        var mockHandler = new MockHttpMessageHandler(HttpStatusCode.OK, jsonResponse);
        using var client = new HttpClient(mockHandler);

        var config = new OverlayConfig { RemoteUrl = "https://example.com/tiers.json" };
        var provider = new HttpTierListProvider(client, _cacheManager, config);

        // Act
        var result = await provider.GetTierListAsync(forceRefresh: true);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.ContainsKey("Defend"));
        Assert.AreEqual("B", result["Defend"].Tier);

        // Verify cache file was created
        var cached = await _cacheManager.LoadFromCacheAsync();
        Assert.IsNotNull(cached);
        Assert.IsTrue(cached.ContainsKey("Defend"));
    }

    [TestMethod]
    public async Task TestHttpProvider_NetworkFailureWithCachePresent_ShouldFallbackToCache()
    {
        // Arrange: Populate cache
        var cachedData = new Dictionary<string, CardTierData>
        {
            ["Bash"] = new CardTierData("Bash", "S", 90.0, "Applies Vulnerable")
        };
        await _cacheManager.SaveToCacheAsync(cachedData);

        // Simulate HTTP failure
        var mockHandler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, "Error");
        using var client = new HttpClient(mockHandler);

        var config = new OverlayConfig { RemoteUrl = "https://example.com/tiers.json" };
        var provider = new HttpTierListProvider(client, _cacheManager, config);

        // Act
        var result = await provider.GetTierListAsync(forceRefresh: true);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.ContainsKey("Bash"));
        Assert.AreEqual("S", result["Bash"].Tier);
        Assert.AreEqual("Applies Vulnerable", result["Bash"].Commentary);
    }

    // Simple mock message handler for testing HttpClient without Moq
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public MockHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content)
            };
            return Task.FromResult(response);
        }
    }
}
