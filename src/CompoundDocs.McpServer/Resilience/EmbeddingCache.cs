using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Resilience;

/// <summary>
/// Configuration options for embedding cache.
/// </summary>
public sealed class EmbeddingCacheOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "EmbeddingCache";

    /// <summary>
    /// Whether caching is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of cached embeddings.
    /// </summary>
    public int MaxCachedItems { get; set; } = 10000;

    /// <summary>
    /// Cache entry expiration in hours.
    /// </summary>
    public int ExpirationHours { get; set; } = 24;

    /// <summary>
    /// Whether to persist cache to disk.
    /// </summary>
    public bool PersistToDisk { get; set; } = false;

    /// <summary>
    /// Path for cache persistence (if enabled).
    /// </summary>
    public string? CachePath { get; set; }
}

/// <summary>
/// Cached embedding entry with metadata.
/// </summary>
public sealed record CachedEmbedding
{
    /// <summary>
    /// The content hash (key).
    /// </summary>
    public required string ContentHash { get; init; }

    /// <summary>
    /// The embedding vector.
    /// </summary>
    public required ReadOnlyMemory<float> Embedding { get; init; }

    /// <summary>
    /// When the entry was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the entry was last accessed.
    /// </summary>
    public DateTime LastAccessedAt { get; set; }

    /// <summary>
    /// Number of times this entry was accessed.
    /// </summary>
    public int AccessCount { get; set; }
}

/// <summary>
/// In-memory cache for embeddings to support graceful degradation
/// when Ollama is unavailable.
/// </summary>
public sealed class EmbeddingCache : IEmbeddingCache, IDisposable
{
    private readonly ILogger<EmbeddingCache> _logger;
    private readonly EmbeddingCacheOptions _options;
    private readonly ConcurrentDictionary<string, CachedEmbedding> _cache = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of EmbeddingCache.
    /// </summary>
    /// <param name="options">Cache options.</param>
    /// <param name="logger">Logger instance.</param>
    public EmbeddingCache(
        IOptions<EmbeddingCacheOptions> options,
        ILogger<EmbeddingCache> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Clean up expired entries every hour
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.Enabled;

    /// <inheritdoc />
    public int Count => _cache.Count;

    /// <inheritdoc />
    public bool TryGet(string content, out ReadOnlyMemory<float> embedding)
    {
        embedding = default;

        if (!_options.Enabled)
        {
            return false;
        }

        var hash = ComputeHash(content);

        if (_cache.TryGetValue(hash, out var cached))
        {
            // Check expiration
            if (DateTime.UtcNow - cached.CreatedAt > TimeSpan.FromHours(_options.ExpirationHours))
            {
                _cache.TryRemove(hash, out _);
                return false;
            }

            // Update access statistics
            cached.LastAccessedAt = DateTime.UtcNow;
            cached.AccessCount++;

            embedding = cached.Embedding;

            _logger.LogDebug(
                "Cache hit for content hash {Hash}. Access count: {AccessCount}",
                hash[..8],
                cached.AccessCount);

            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public void Set(string content, ReadOnlyMemory<float> embedding)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var hash = ComputeHash(content);

        // Evict if at capacity
        if (_cache.Count >= _options.MaxCachedItems)
        {
            EvictLeastRecentlyUsed();
        }

        var cached = new CachedEmbedding
        {
            ContentHash = hash,
            Embedding = embedding,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            AccessCount = 0
        };

        _cache.AddOrUpdate(hash, cached, (_, _) => cached);

        _logger.LogDebug(
            "Cached embedding for content hash {Hash}. Cache size: {CacheSize}",
            hash[..8],
            _cache.Count);
    }

    /// <inheritdoc />
    public bool Remove(string content)
    {
        var hash = ComputeHash(content);
        return _cache.TryRemove(hash, out _);
    }

    /// <inheritdoc />
    public void Clear()
    {
        _cache.Clear();
        _logger.LogInformation("Embedding cache cleared");
    }

    /// <inheritdoc />
    public EmbeddingCacheStats GetStats()
    {
        var entries = _cache.Values.ToList();
        var now = DateTime.UtcNow;

        return new EmbeddingCacheStats
        {
            TotalEntries = entries.Count,
            TotalAccessCount = entries.Sum(e => e.AccessCount),
            OldestEntry = entries.MinBy(e => e.CreatedAt)?.CreatedAt,
            NewestEntry = entries.MaxBy(e => e.CreatedAt)?.CreatedAt,
            MostAccessedCount = entries.MaxBy(e => e.AccessCount)?.AccessCount ?? 0,
            ExpiredEntries = entries.Count(e => now - e.CreatedAt > TimeSpan.FromHours(_options.ExpirationHours)),
            CacheEnabled = _options.Enabled,
            MaxCapacity = _options.MaxCachedItems
        };
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private void EvictLeastRecentlyUsed()
    {
        // Find the least recently used entry
        var lru = _cache.Values
            .OrderBy(e => e.LastAccessedAt)
            .ThenBy(e => e.AccessCount)
            .FirstOrDefault();

        if (lru != null)
        {
            _cache.TryRemove(lru.ContentHash, out _);
            _logger.LogDebug(
                "Evicted LRU cache entry {Hash}. Last accessed: {LastAccess}",
                lru.ContentHash[..8],
                lru.LastAccessedAt);
        }
    }

    private void CleanupExpiredEntries(object? state)
    {
        var now = DateTime.UtcNow;
        var expiration = TimeSpan.FromHours(_options.ExpirationHours);
        var expiredCount = 0;

        foreach (var kvp in _cache)
        {
            if (now - kvp.Value.CreatedAt > expiration)
            {
                if (_cache.TryRemove(kvp.Key, out _))
                {
                    expiredCount++;
                }
            }
        }

        if (expiredCount > 0)
        {
            _logger.LogInformation(
                "Cleaned up {ExpiredCount} expired cache entries. Remaining: {RemainingCount}",
                expiredCount,
                _cache.Count);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Interface for embedding cache operations.
/// </summary>
public interface IEmbeddingCache
{
    /// <summary>
    /// Gets whether caching is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets the number of cached entries.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Attempts to get a cached embedding.
    /// </summary>
    /// <param name="content">The content to look up.</param>
    /// <param name="embedding">The cached embedding if found.</param>
    /// <returns>True if found in cache, false otherwise.</returns>
    bool TryGet(string content, out ReadOnlyMemory<float> embedding);

    /// <summary>
    /// Stores an embedding in the cache.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="embedding">The embedding to cache.</param>
    void Set(string content, ReadOnlyMemory<float> embedding);

    /// <summary>
    /// Removes an entry from the cache.
    /// </summary>
    /// <param name="content">The content to remove.</param>
    /// <returns>True if removed, false if not found.</returns>
    bool Remove(string content);

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    /// <returns>Cache statistics.</returns>
    EmbeddingCacheStats GetStats();
}

/// <summary>
/// Statistics about the embedding cache.
/// </summary>
public sealed record EmbeddingCacheStats
{
    /// <summary>
    /// Total number of cached entries.
    /// </summary>
    public required int TotalEntries { get; init; }

    /// <summary>
    /// Total number of cache accesses.
    /// </summary>
    public required long TotalAccessCount { get; init; }

    /// <summary>
    /// Creation time of the oldest entry.
    /// </summary>
    public DateTime? OldestEntry { get; init; }

    /// <summary>
    /// Creation time of the newest entry.
    /// </summary>
    public DateTime? NewestEntry { get; init; }

    /// <summary>
    /// Access count of the most accessed entry.
    /// </summary>
    public required int MostAccessedCount { get; init; }

    /// <summary>
    /// Number of expired entries.
    /// </summary>
    public required int ExpiredEntries { get; init; }

    /// <summary>
    /// Whether the cache is enabled.
    /// </summary>
    public required bool CacheEnabled { get; init; }

    /// <summary>
    /// Maximum cache capacity.
    /// </summary>
    public required int MaxCapacity { get; init; }
}
