using CompoundDocs.McpServer.Observability;
using CompoundDocs.McpServer.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CompoundDocs.Tests.Unit.Resilience;

public sealed class EmbeddingCacheTests
{
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
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());

        Should.Throw<ArgumentNullException>(() =>
            new EmbeddingCache(options, null!));
    }

    [Fact]
    public void Constructor_ValidArguments_CreatesInstance()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);

        cache.ShouldNotBeNull();
        cache.Count.ShouldBe(0);
    }

    #endregion

    #region IsEnabled Tests

    [Fact]
    public void IsEnabled_DefaultOptions_ReturnsTrue()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);

        cache.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void IsEnabled_DisabledOptions_ReturnsFalse()
    {
        var opts = new EmbeddingCacheOptions { Enabled = false };
        var options = Microsoft.Extensions.Options.Options.Create(opts);
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);

        cache.IsEnabled.ShouldBeFalse();
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_CacheDisabled_ReturnsFalse()
    {
        var opts = new EmbeddingCacheOptions { Enabled = false };
        var options = Microsoft.Extensions.Options.Options.Create(opts);
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);
        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });

        cache.Set("test content", embedding); // no-op when disabled

        var result = cache.TryGet("test content", out var retrieved);

        result.ShouldBeFalse();
        retrieved.Length.ShouldBe(0);
    }

    [Fact]
    public void TryGet_ContentNotCached_ReturnsFalse()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);

        var result = cache.TryGet("nonexistent", out var embedding);

        result.ShouldBeFalse();
        embedding.Length.ShouldBe(0);
    }

    [Fact]
    public void TryGet_ContentCached_ReturnsTrueAndEmbedding()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);
        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });
        cache.Set("test content", embedding);

        var result = cache.TryGet("test content", out var retrieved);

        result.ShouldBeTrue();
        retrieved.Length.ShouldBe(3);
        retrieved.Span[0].ShouldBe(0.1f);
        retrieved.Span[1].ShouldBe(0.2f);
        retrieved.Span[2].ShouldBe(0.3f);
    }

    [Fact]
    public void TryGet_ExpiredEntry_ReturnsFalseAndRemovesEntry()
    {
        var opts = new EmbeddingCacheOptions { ExpirationHours = 0 };
        var options = Microsoft.Extensions.Options.Options.Create(opts);
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);
        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });

        cache.Set("test content", embedding);

        // With 0 expiration hours, any entry is immediately expired on next TryGet
        // We need a small delay to ensure DateTime.UtcNow difference > TimeSpan.FromHours(0)
        Thread.Sleep(10);

        var result = cache.TryGet("test content", out var retrieved);

        result.ShouldBeFalse();
        cache.Count.ShouldBe(0);
    }

    [Fact]
    public void TryGet_IncrementsAccessCount()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);
        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });
        cache.Set("test content", embedding);

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
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);
        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });
        cache.Set("identical content", embedding);

        cache.TryGet("identical content", out var first);
        cache.TryGet("identical content", out var second);

        first.Span.SequenceEqual(second.Span).ShouldBeTrue();
    }

    #endregion

    #region Set Tests

    [Fact]
    public void Set_CacheDisabled_DoesNotStore()
    {
        var opts = new EmbeddingCacheOptions { Enabled = false };
        var options = Microsoft.Extensions.Options.Options.Create(opts);
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);
        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });

        cache.Set("test content", embedding);

        cache.Count.ShouldBe(0);
    }

    [Fact]
    public void Set_NewContent_IncrementsCount()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);
        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });

        cache.Set("content 1", embedding);
        cache.Set("content 2", embedding);

        cache.Count.ShouldBe(2);
    }

    [Fact]
    public void Set_DuplicateContent_OverwritesEntry()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);
        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });
        var updatedEmbedding = new ReadOnlyMemory<float>(new float[] { 0.9f, 0.8f });

        cache.Set("test content", embedding);
        cache.Set("test content", updatedEmbedding);

        cache.Count.ShouldBe(1);
        cache.TryGet("test content", out var retrieved);
        retrieved.Span[0].ShouldBe(0.9f);
    }

    [Fact]
    public void Set_AtCapacity_EvictsLeastRecentlyUsed()
    {
        var opts = new EmbeddingCacheOptions { MaxCachedItems = 2 };
        var options = Microsoft.Extensions.Options.Options.Create(opts);
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);
        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });

        cache.Set("first", embedding);
        Thread.Sleep(10); // Ensure different timestamps
        cache.Set("second", embedding);

        // Access "first" to make it more recently used
        cache.TryGet("first", out _);
        Thread.Sleep(10);

        // This should evict "second" (least recently used)
        cache.Set("third", embedding);

        cache.Count.ShouldBe(2);
        cache.TryGet("first", out _).ShouldBeTrue();
        cache.TryGet("third", out _).ShouldBeTrue();
    }

    #endregion

    #region Remove Tests

    [Fact]
    public void Remove_ExistingContent_ReturnsTrueAndRemoves()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);
        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });
        cache.Set("test content", embedding);

        var result = cache.Remove("test content");

        result.ShouldBeTrue();
        cache.Count.ShouldBe(0);
    }

    [Fact]
    public void Remove_NonexistentContent_ReturnsFalse()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);

        var result = cache.Remove("nonexistent");

        result.ShouldBeFalse();
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_WithEntries_RemovesAll()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);
        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });
        cache.Set("content 1", embedding);
        cache.Set("content 2", embedding);
        cache.Set("content 3", embedding);

        cache.Clear();

        cache.Count.ShouldBe(0);
    }

    [Fact]
    public void Clear_EmptyCache_DoesNotThrow()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);

        Should.NotThrow(() => cache.Clear());
        cache.Count.ShouldBe(0);
    }

    #endregion

    #region GetStats Tests

    [Fact]
    public void GetStats_EmptyCache_ReturnsZeroStats()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);

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
        var opts = new EmbeddingCacheOptions { MaxCachedItems = 500 };
        var options = Microsoft.Extensions.Options.Options.Create(opts);
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);
        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });

        cache.Set("content 1", embedding);
        cache.Set("content 2", embedding);

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
        var opts = new EmbeddingCacheOptions { Enabled = false };
        var options = Microsoft.Extensions.Options.Options.Create(opts);
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);

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
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        var metricsMock = new Mock<IMetricsCollector>();
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance, metricsMock.Object);
        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });
        cache.Set("test content", embedding);

        cache.TryGet("test content", out _);

        metricsMock.Verify(m => m.RecordCacheHit(), Times.Once);
    }

    [Fact]
    public void TryGet_CacheMiss_RecordsCacheMissMetric()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        var metricsMock = new Mock<IMetricsCollector>();
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance, metricsMock.Object);

        cache.TryGet("nonexistent", out _);

        metricsMock.Verify(m => m.RecordCacheMiss(), Times.Once);
    }

    [Fact]
    public void TryGet_WithoutMetrics_DoesNotThrow()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        using var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);
        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f, 0.3f });
        cache.Set("test content", embedding);

        Should.NotThrow(() => cache.TryGet("test content", out _));
        Should.NotThrow(() => cache.TryGet("nonexistent", out _));
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new EmbeddingCacheOptions());
        var cache = new EmbeddingCache(options, NullLogger<EmbeddingCache>.Instance);

        Should.NotThrow(() =>
        {
            cache.Dispose();
            cache.Dispose();
        });
    }

    #endregion
}
