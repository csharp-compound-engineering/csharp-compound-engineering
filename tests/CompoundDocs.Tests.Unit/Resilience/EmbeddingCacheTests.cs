using CompoundDocs.McpServer.Observability;
using CompoundDocs.McpServer.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CompoundDocs.Tests.Unit.Resilience;

public sealed class EmbeddingCacheTests
{
    private static readonly ReadOnlyMemory<float> SampleEmbedding = new float[] { 0.1f, 0.2f, 0.3f };

    private static IOptions<EmbeddingCacheOptions> CreateOptions(Action<EmbeddingCacheOptions>? configure = null)
    {
        var options = new EmbeddingCacheOptions();
        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new EmbeddingCache(null!, NullLogger<EmbeddingCache>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new EmbeddingCache(CreateOptions(), null!));
    }

    [Fact]
    public void Constructor_ValidArguments_CreatesInstance()
    {
        using var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance);

        cache.ShouldNotBeNull();
        cache.Count.ShouldBe(0);
    }

    #endregion

    #region IsEnabled Tests

    [Fact]
    public void IsEnabled_DefaultOptions_ReturnsTrue()
    {
        using var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance);

        cache.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void IsEnabled_DisabledOptions_ReturnsFalse()
    {
        using var cache = new EmbeddingCache(
            CreateOptions(o => o.Enabled = false),
            NullLogger<EmbeddingCache>.Instance);

        cache.IsEnabled.ShouldBeFalse();
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_CacheDisabled_ReturnsFalse()
    {
        using var cache = new EmbeddingCache(
            CreateOptions(o => o.Enabled = false),
            NullLogger<EmbeddingCache>.Instance);

        cache.Set("test content", SampleEmbedding); // no-op when disabled

        var result = cache.TryGet("test content", out var embedding);

        result.ShouldBeFalse();
        embedding.Length.ShouldBe(0);
    }

    [Fact]
    public void TryGet_ContentNotCached_ReturnsFalse()
    {
        using var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance);

        var result = cache.TryGet("nonexistent", out var embedding);

        result.ShouldBeFalse();
        embedding.Length.ShouldBe(0);
    }

    [Fact]
    public void TryGet_ContentCached_ReturnsTrueAndEmbedding()
    {
        using var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance);
        cache.Set("test content", SampleEmbedding);

        var result = cache.TryGet("test content", out var embedding);

        result.ShouldBeTrue();
        embedding.Length.ShouldBe(3);
        embedding.Span[0].ShouldBe(0.1f);
        embedding.Span[1].ShouldBe(0.2f);
        embedding.Span[2].ShouldBe(0.3f);
    }

    [Fact]
    public void TryGet_ExpiredEntry_ReturnsFalseAndRemovesEntry()
    {
        using var cache = new EmbeddingCache(
            CreateOptions(o => o.ExpirationHours = 0),
            NullLogger<EmbeddingCache>.Instance);

        cache.Set("test content", SampleEmbedding);

        // With 0 expiration hours, any entry is immediately expired on next TryGet
        // We need a small delay to ensure DateTime.UtcNow difference > TimeSpan.FromHours(0)
        Thread.Sleep(10);

        var result = cache.TryGet("test content", out var embedding);

        result.ShouldBeFalse();
        cache.Count.ShouldBe(0);
    }

    [Fact]
    public void TryGet_IncrementsAccessCount()
    {
        using var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance);
        cache.Set("test content", SampleEmbedding);

        cache.TryGet("test content", out _);
        cache.TryGet("test content", out _);
        cache.TryGet("test content", out _);

        var stats = cache.GetStats();
        stats.TotalAccessCount.ShouldBe(3);
        stats.MostAccessedCount.ShouldBe(3);
    }

    [Fact]
    public void TryGet_SameContentDifferentCalls_ReturnsSameEmbedding()
    {
        using var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance);
        cache.Set("identical content", SampleEmbedding);

        cache.TryGet("identical content", out var first);
        cache.TryGet("identical content", out var second);

        first.Span.SequenceEqual(second.Span).ShouldBeTrue();
    }

    #endregion

    #region Set Tests

    [Fact]
    public void Set_CacheDisabled_DoesNotStore()
    {
        using var cache = new EmbeddingCache(
            CreateOptions(o => o.Enabled = false),
            NullLogger<EmbeddingCache>.Instance);

        cache.Set("test content", SampleEmbedding);

        cache.Count.ShouldBe(0);
    }

    [Fact]
    public void Set_NewContent_IncrementsCount()
    {
        using var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance);

        cache.Set("content 1", SampleEmbedding);
        cache.Set("content 2", SampleEmbedding);

        cache.Count.ShouldBe(2);
    }

    [Fact]
    public void Set_DuplicateContent_OverwritesEntry()
    {
        using var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance);
        var updatedEmbedding = new ReadOnlyMemory<float>(new float[] { 0.9f, 0.8f });

        cache.Set("test content", SampleEmbedding);
        cache.Set("test content", updatedEmbedding);

        cache.Count.ShouldBe(1);
        cache.TryGet("test content", out var embedding);
        embedding.Span[0].ShouldBe(0.9f);
    }

    [Fact]
    public void Set_AtCapacity_EvictsLeastRecentlyUsed()
    {
        using var cache = new EmbeddingCache(
            CreateOptions(o => o.MaxCachedItems = 2),
            NullLogger<EmbeddingCache>.Instance);

        cache.Set("first", SampleEmbedding);
        Thread.Sleep(10); // Ensure different timestamps
        cache.Set("second", SampleEmbedding);

        // Access "first" to make it more recently used
        cache.TryGet("first", out _);
        Thread.Sleep(10);

        // This should evict "second" (least recently used)
        cache.Set("third", SampleEmbedding);

        cache.Count.ShouldBe(2);
        cache.TryGet("first", out _).ShouldBeTrue();
        cache.TryGet("third", out _).ShouldBeTrue();
    }

    #endregion

    #region Remove Tests

    [Fact]
    public void Remove_ExistingContent_ReturnsTrueAndRemoves()
    {
        using var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance);
        cache.Set("test content", SampleEmbedding);

        var result = cache.Remove("test content");

        result.ShouldBeTrue();
        cache.Count.ShouldBe(0);
    }

    [Fact]
    public void Remove_NonexistentContent_ReturnsFalse()
    {
        using var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance);

        var result = cache.Remove("nonexistent");

        result.ShouldBeFalse();
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_WithEntries_RemovesAll()
    {
        using var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance);
        cache.Set("content 1", SampleEmbedding);
        cache.Set("content 2", SampleEmbedding);
        cache.Set("content 3", SampleEmbedding);

        cache.Clear();

        cache.Count.ShouldBe(0);
    }

    [Fact]
    public void Clear_EmptyCache_DoesNotThrow()
    {
        using var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance);

        Should.NotThrow(() => cache.Clear());
        cache.Count.ShouldBe(0);
    }

    #endregion

    #region GetStats Tests

    [Fact]
    public void GetStats_EmptyCache_ReturnsZeroStats()
    {
        using var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance);

        var stats = cache.GetStats();

        stats.TotalEntries.ShouldBe(0);
        stats.TotalAccessCount.ShouldBe(0);
        stats.OldestEntry.ShouldBeNull();
        stats.NewestEntry.ShouldBeNull();
        stats.MostAccessedCount.ShouldBe(0);
        stats.ExpiredEntries.ShouldBe(0);
        stats.CacheEnabled.ShouldBeTrue();
        stats.MaxCapacity.ShouldBe(10000);
    }

    [Fact]
    public void GetStats_WithEntries_ReturnsAccurateStats()
    {
        using var cache = new EmbeddingCache(
            CreateOptions(o => o.MaxCachedItems = 500),
            NullLogger<EmbeddingCache>.Instance);

        cache.Set("content 1", SampleEmbedding);
        cache.Set("content 2", SampleEmbedding);

        // Access content 1 twice
        cache.TryGet("content 1", out _);
        cache.TryGet("content 1", out _);

        var stats = cache.GetStats();

        stats.TotalEntries.ShouldBe(2);
        stats.TotalAccessCount.ShouldBe(2);
        stats.OldestEntry.ShouldNotBeNull();
        stats.NewestEntry.ShouldNotBeNull();
        stats.MostAccessedCount.ShouldBe(2);
        stats.ExpiredEntries.ShouldBe(0);
        stats.CacheEnabled.ShouldBeTrue();
        stats.MaxCapacity.ShouldBe(500);
    }

    [Fact]
    public void GetStats_DisabledCache_ReportsDisabled()
    {
        using var cache = new EmbeddingCache(
            CreateOptions(o => o.Enabled = false),
            NullLogger<EmbeddingCache>.Instance);

        var stats = cache.GetStats();

        stats.CacheEnabled.ShouldBeFalse();
    }

    #endregion

    #region EmbeddingCacheOptions Defaults Tests

    [Fact]
    public void EmbeddingCacheOptions_Defaults_AreCorrect()
    {
        var options = new EmbeddingCacheOptions();

        options.Enabled.ShouldBeTrue();
        options.MaxCachedItems.ShouldBe(10000);
        options.ExpirationHours.ShouldBe(24);
        EmbeddingCacheOptions.SectionName.ShouldBe("EmbeddingCache");
    }

    #endregion

    #region MetricsCollector Integration Tests

    [Fact]
    public void TryGet_CacheHit_RecordsCacheHitMetric()
    {
        using var metrics = new MetricsCollector();
        using var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance, metrics);
        cache.Set("test content", SampleEmbedding);

        cache.TryGet("test content", out _);

        var snapshot = metrics.GetSnapshot();
        snapshot.CacheHitRate.ShouldBe(1.0);
    }

    [Fact]
    public void TryGet_CacheMiss_RecordsCacheMissMetric()
    {
        using var metrics = new MetricsCollector();
        using var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance, metrics);

        cache.TryGet("nonexistent", out _);

        var snapshot = metrics.GetSnapshot();
        snapshot.CacheHitRate.ShouldBe(0.0);
    }

    [Fact]
    public void TryGet_WithoutMetrics_DoesNotThrow()
    {
        using var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance);
        cache.Set("test content", SampleEmbedding);

        Should.NotThrow(() => cache.TryGet("test content", out _));
        Should.NotThrow(() => cache.TryGet("nonexistent", out _));
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var cache = new EmbeddingCache(CreateOptions(), NullLogger<EmbeddingCache>.Instance);

        Should.NotThrow(() =>
        {
            cache.Dispose();
            cache.Dispose();
        });
    }

    #endregion
}
