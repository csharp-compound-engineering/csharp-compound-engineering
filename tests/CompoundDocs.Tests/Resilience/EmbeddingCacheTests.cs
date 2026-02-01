using CompoundDocs.McpServer.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CompoundDocs.Tests.Resilience;

/// <summary>
/// Unit tests for EmbeddingCache.
/// </summary>
public sealed class EmbeddingCacheTests : IDisposable
{
    private readonly EmbeddingCache _cache;
    private readonly EmbeddingCacheOptions _options;

    public EmbeddingCacheTests()
    {
        _options = new EmbeddingCacheOptions
        {
            Enabled = true,
            MaxCachedItems = 100,
            ExpirationHours = 24
        };

        _cache = new EmbeddingCache(
            Options.Create(_options),
            NullLogger<EmbeddingCache>.Instance);
    }

    [Fact]
    public void IsEnabled_WhenEnabled_ReturnsTrue()
    {
        // Assert
        _cache.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void IsEnabled_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var options = new EmbeddingCacheOptions { Enabled = false };
        using var cache = new EmbeddingCache(Options.Create(options), NullLogger<EmbeddingCache>.Instance);

        // Assert
        cache.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void TryGet_WhenNotCached_ReturnsFalse()
    {
        // Act
        var result = _cache.TryGet("non-existent content", out var embedding);

        // Assert
        result.ShouldBeFalse();
        embedding.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void TryGet_WhenCached_ReturnsTrue()
    {
        // Arrange
        var content = "test content";
        var embedding = CreateTestEmbedding(1024);
        _cache.Set(content, embedding);

        // Act
        var result = _cache.TryGet(content, out var retrieved);

        // Assert
        result.ShouldBeTrue();
        retrieved.Length.ShouldBe(1024);
    }

    [Fact]
    public void TryGet_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var options = new EmbeddingCacheOptions { Enabled = false };
        using var cache = new EmbeddingCache(Options.Create(options), NullLogger<EmbeddingCache>.Instance);
        var content = "test content";
        var embedding = CreateTestEmbedding(1024);

        // Set should be no-op when disabled
        cache.Set(content, embedding);

        // Act
        var result = cache.TryGet(content, out var retrieved);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Set_StoresEmbedding()
    {
        // Arrange
        var content = "unique content";
        var embedding = CreateTestEmbedding(1024, seed: 42);

        // Act
        _cache.Set(content, embedding);

        // Assert
        _cache.TryGet(content, out var retrieved).ShouldBeTrue();
        retrieved.Span[0].ShouldBe(embedding.Span[0]);
    }

    [Fact]
    public void Set_WhenDisabled_DoesNotStore()
    {
        // Arrange
        var options = new EmbeddingCacheOptions { Enabled = false };
        using var cache = new EmbeddingCache(Options.Create(options), NullLogger<EmbeddingCache>.Instance);

        // Act
        cache.Set("content", CreateTestEmbedding(1024));

        // Assert
        cache.Count.ShouldBe(0);
    }

    [Fact]
    public void Count_ReturnsCorrectValue()
    {
        // Arrange & Act
        _cache.Set("content1", CreateTestEmbedding(1024));
        _cache.Set("content2", CreateTestEmbedding(1024));
        _cache.Set("content3", CreateTestEmbedding(1024));

        // Assert
        _cache.Count.ShouldBe(3);
    }

    [Fact]
    public void Set_EvictsLRU_WhenAtCapacity()
    {
        // Arrange
        var options = new EmbeddingCacheOptions
        {
            Enabled = true,
            MaxCachedItems = 3,
            ExpirationHours = 24
        };
        using var cache = new EmbeddingCache(Options.Create(options), NullLogger<EmbeddingCache>.Instance);

        // Fill to capacity
        cache.Set("content1", CreateTestEmbedding(1024));
        cache.Set("content2", CreateTestEmbedding(1024));
        cache.Set("content3", CreateTestEmbedding(1024));

        // Access content2 to make it more recently used
        cache.TryGet("content2", out _);

        // Act - Add one more, should evict content1 (LRU)
        cache.Set("content4", CreateTestEmbedding(1024));

        // Assert
        cache.Count.ShouldBe(3);
        cache.TryGet("content1", out _).ShouldBeFalse(); // Evicted
        cache.TryGet("content2", out _).ShouldBeTrue(); // Still present (accessed recently)
        cache.TryGet("content3", out _).ShouldBeTrue(); // Still present
        cache.TryGet("content4", out _).ShouldBeTrue(); // Newly added
    }

    [Fact]
    public void Remove_DeletesEntry()
    {
        // Arrange
        var content = "to be removed";
        _cache.Set(content, CreateTestEmbedding(1024));
        _cache.TryGet(content, out _).ShouldBeTrue();

        // Act
        var result = _cache.Remove(content);

        // Assert
        result.ShouldBeTrue();
        _cache.TryGet(content, out _).ShouldBeFalse();
    }

    [Fact]
    public void Remove_NonExistent_ReturnsFalse()
    {
        // Act
        var result = _cache.Remove("non-existent");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        // Arrange
        _cache.Set("content1", CreateTestEmbedding(1024));
        _cache.Set("content2", CreateTestEmbedding(1024));
        _cache.Count.ShouldBe(2);

        // Act
        _cache.Clear();

        // Assert
        _cache.Count.ShouldBe(0);
    }

    [Fact]
    public void GetStats_ReturnsCorrectStatistics()
    {
        // Arrange
        _cache.Set("content1", CreateTestEmbedding(1024));
        _cache.Set("content2", CreateTestEmbedding(1024));
        _cache.TryGet("content1", out _); // Access once
        _cache.TryGet("content1", out _); // Access twice

        // Act
        var stats = _cache.GetStats();

        // Assert
        stats.TotalEntries.ShouldBe(2);
        stats.CacheEnabled.ShouldBeTrue();
        stats.MaxCapacity.ShouldBe(100);
        stats.TotalAccessCount.ShouldBe(2); // content1 accessed twice
        stats.MostAccessedCount.ShouldBe(2);
    }

    [Fact]
    public void TryGet_SameContent_ReturnsSameEmbedding()
    {
        // Arrange
        var content = "same content";
        var embedding = CreateTestEmbedding(1024, seed: 123);
        _cache.Set(content, embedding);

        // Act
        _cache.TryGet(content, out var retrieved1);
        _cache.TryGet(content, out var retrieved2);

        // Assert
        retrieved1.Span.SequenceEqual(retrieved2.Span).ShouldBeTrue();
    }

    [Fact]
    public void TryGet_DifferentContent_ReturnsDifferentEmbeddings()
    {
        // Arrange
        var embedding1 = CreateTestEmbedding(1024, seed: 1);
        var embedding2 = CreateTestEmbedding(1024, seed: 2);
        _cache.Set("content1", embedding1);
        _cache.Set("content2", embedding2);

        // Act
        _cache.TryGet("content1", out var retrieved1);
        _cache.TryGet("content2", out var retrieved2);

        // Assert
        retrieved1.Span.SequenceEqual(retrieved2.Span).ShouldBeFalse();
    }

    [Fact]
    public void Set_UpdatesExistingEntry()
    {
        // Arrange
        var content = "updatable content";
        var embedding1 = CreateTestEmbedding(1024, seed: 1);
        var embedding2 = CreateTestEmbedding(1024, seed: 2);
        _cache.Set(content, embedding1);

        // Act
        _cache.Set(content, embedding2);

        // Assert
        _cache.TryGet(content, out var retrieved);
        retrieved.Span[0].ShouldBe(embedding2.Span[0]);
        _cache.Count.ShouldBe(1); // Still only one entry
    }

    private static ReadOnlyMemory<float> CreateTestEmbedding(int dimensions, int seed = 0)
    {
        var random = new Random(seed);
        var values = new float[dimensions];
        for (var i = 0; i < dimensions; i++)
        {
            values[i] = (float)random.NextDouble();
        }
        return new ReadOnlyMemory<float>(values);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }
}
