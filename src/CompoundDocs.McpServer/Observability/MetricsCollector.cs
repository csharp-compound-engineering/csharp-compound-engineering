using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json.Serialization;

namespace CompoundDocs.McpServer.Observability;

/// <summary>
/// Collects and exposes operational metrics for the MCP server.
/// Uses System.Diagnostics.Metrics for .NET 9.0 native metrics and ActivitySource for tracing.
/// </summary>
public sealed class MetricsCollector : IDisposable
{
    /// <summary>
    /// The meter name for CompoundDocs metrics.
    /// </summary>
    public const string MeterName = "CompoundDocs.McpServer";

    /// <summary>
    /// The activity source name for tracing.
    /// </summary>
    public const string ActivitySourceName = "CompoundDocs.McpServer";

    private readonly Meter _meter;
    private readonly ActivitySource _activitySource;

    // Counters
    private readonly Counter<long> _documentIndexedCounter;
    private readonly Counter<long> _chunkIndexedCounter;
    private readonly Counter<long> _queryCounter;
    private readonly Counter<long> _cacheHitCounter;
    private readonly Counter<long> _cacheMissCounter;
    private readonly Counter<long> _errorCounter;

    // Histograms for latency tracking
    private readonly Histogram<double> _queryLatencyHistogram;
    private readonly Histogram<double> _embeddingLatencyHistogram;
    private readonly Histogram<double> _indexingLatencyHistogram;

    // Observable gauges for current state
    private readonly ObservableGauge<int> _activeProjectGauge;

    // Internal storage for calculated metrics
    private readonly ConcurrentDictionary<string, TenantMetrics> _tenantMetrics = new();
    private readonly LatencyTracker _queryLatencyTracker = new();
    private readonly LatencyTracker _embeddingLatencyTracker = new();
    private long _cacheHits;
    private long _cacheMisses;
    private int _activeProjects;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of the MetricsCollector.
    /// </summary>
    public MetricsCollector()
    {
        _meter = new Meter(MeterName, "1.0.0");
        _activitySource = new ActivitySource(ActivitySourceName, "1.0.0");

        // Initialize counters
        _documentIndexedCounter = _meter.CreateCounter<long>(
            "compounddocs.documents.indexed",
            unit: "{documents}",
            description: "Total number of documents indexed");

        _chunkIndexedCounter = _meter.CreateCounter<long>(
            "compounddocs.chunks.indexed",
            unit: "{chunks}",
            description: "Total number of document chunks indexed");

        _queryCounter = _meter.CreateCounter<long>(
            "compounddocs.queries.total",
            unit: "{queries}",
            description: "Total number of queries executed");

        _cacheHitCounter = _meter.CreateCounter<long>(
            "compounddocs.cache.hits",
            unit: "{hits}",
            description: "Number of cache hits");

        _cacheMissCounter = _meter.CreateCounter<long>(
            "compounddocs.cache.misses",
            unit: "{misses}",
            description: "Number of cache misses");

        _errorCounter = _meter.CreateCounter<long>(
            "compounddocs.errors.total",
            unit: "{errors}",
            description: "Total number of errors");

        // Initialize histograms
        _queryLatencyHistogram = _meter.CreateHistogram<double>(
            "compounddocs.query.latency",
            unit: "ms",
            description: "Query latency in milliseconds");

        _embeddingLatencyHistogram = _meter.CreateHistogram<double>(
            "compounddocs.embedding.latency",
            unit: "ms",
            description: "Embedding generation latency in milliseconds");

        _indexingLatencyHistogram = _meter.CreateHistogram<double>(
            "compounddocs.indexing.latency",
            unit: "ms",
            description: "Document indexing latency in milliseconds");

        // Initialize observable gauges
        _activeProjectGauge = _meter.CreateObservableGauge(
            "compounddocs.projects.active",
            observeValue: () => _activeProjects,
            unit: "{projects}",
            description: "Number of active projects");
    }

    /// <summary>
    /// Gets the ActivitySource for creating tracing activities.
    /// </summary>
    public ActivitySource ActivitySource => _activitySource;

    /// <summary>
    /// Records a document being indexed.
    /// </summary>
    /// <param name="tenantKey">The tenant key for the document.</param>
    /// <param name="chunkCount">Number of chunks in the document.</param>
    /// <param name="durationMs">Time taken to index in milliseconds.</param>
    public void RecordDocumentIndexed(string tenantKey, int chunkCount, double durationMs)
    {
        _documentIndexedCounter.Add(1, new KeyValuePair<string, object?>("tenant", tenantKey));
        _chunkIndexedCounter.Add(chunkCount, new KeyValuePair<string, object?>("tenant", tenantKey));
        _indexingLatencyHistogram.Record(durationMs, new KeyValuePair<string, object?>("tenant", tenantKey));

        var metrics = _tenantMetrics.GetOrAdd(tenantKey, _ => new TenantMetrics());
        Interlocked.Increment(ref metrics.DocumentCount);
        Interlocked.Add(ref metrics.ChunkCount, chunkCount);
    }

    /// <summary>
    /// Records a query being executed.
    /// </summary>
    /// <param name="tenantKey">The tenant key for the query.</param>
    /// <param name="durationMs">Time taken in milliseconds.</param>
    /// <param name="resultCount">Number of results returned.</param>
    public void RecordQuery(string tenantKey, double durationMs, int resultCount)
    {
        _queryCounter.Add(1, new KeyValuePair<string, object?>("tenant", tenantKey));
        _queryLatencyHistogram.Record(durationMs, new KeyValuePair<string, object?>("tenant", tenantKey));
        _queryLatencyTracker.Record(durationMs);

        var metrics = _tenantMetrics.GetOrAdd(tenantKey, _ => new TenantMetrics());
        Interlocked.Increment(ref metrics.QueryCount);
    }

    /// <summary>
    /// Records an embedding generation operation.
    /// </summary>
    /// <param name="durationMs">Time taken in milliseconds.</param>
    /// <param name="contentLength">Length of content embedded.</param>
    public void RecordEmbeddingGeneration(double durationMs, int contentLength)
    {
        _embeddingLatencyHistogram.Record(durationMs, new KeyValuePair<string, object?>("content_length", contentLength));
        _embeddingLatencyTracker.Record(durationMs);
    }

    /// <summary>
    /// Records a cache hit.
    /// </summary>
    public void RecordCacheHit()
    {
        _cacheHitCounter.Add(1);
        Interlocked.Increment(ref _cacheHits);
    }

    /// <summary>
    /// Records a cache miss.
    /// </summary>
    public void RecordCacheMiss()
    {
        _cacheMissCounter.Add(1);
        Interlocked.Increment(ref _cacheMisses);
    }

    /// <summary>
    /// Records an error occurrence.
    /// </summary>
    /// <param name="errorType">Type of error that occurred.</param>
    public void RecordError(string errorType)
    {
        _errorCounter.Add(1, new KeyValuePair<string, object?>("error_type", errorType));
    }

    /// <summary>
    /// Records a project activation.
    /// </summary>
    public void RecordProjectActivated()
    {
        Interlocked.Increment(ref _activeProjects);
    }

    /// <summary>
    /// Records a project deactivation.
    /// </summary>
    public void RecordProjectDeactivated()
    {
        Interlocked.Decrement(ref _activeProjects);
    }

    /// <summary>
    /// Starts a new activity for tracing.
    /// </summary>
    /// <param name="operationName">Name of the operation being traced.</param>
    /// <param name="kind">The kind of activity.</param>
    /// <returns>The started activity, or null if no listeners are registered.</returns>
    public Activity? StartActivity(string operationName, ActivityKind kind = ActivityKind.Internal)
    {
        return _activitySource.StartActivity(operationName, kind);
    }

    /// <summary>
    /// Gets the current metrics snapshot.
    /// </summary>
    /// <returns>A snapshot of current metrics.</returns>
    public MetricsSnapshot GetSnapshot()
    {
        var tenantSnapshots = _tenantMetrics.Select(kvp => new TenantMetricsSnapshot
        {
            TenantKey = kvp.Key,
            DocumentCount = kvp.Value.DocumentCount,
            ChunkCount = kvp.Value.ChunkCount,
            QueryCount = kvp.Value.QueryCount
        }).ToList();

        return new MetricsSnapshot
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            TenantMetrics = tenantSnapshots,
            QueryLatency = _queryLatencyTracker.GetPercentiles(),
            EmbeddingLatency = _embeddingLatencyTracker.GetPercentiles(),
            CacheHitRate = CalculateCacheHitRate(),
            ActiveProjects = _activeProjects
        };
    }

    private double CalculateCacheHitRate()
    {
        var total = Interlocked.Read(ref _cacheHits) + Interlocked.Read(ref _cacheMisses);
        if (total == 0)
        {
            return 0.0;
        }
        return (double)Interlocked.Read(ref _cacheHits) / total;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _meter.Dispose();
        _activitySource.Dispose();
    }

    /// <summary>
    /// Internal class to track per-tenant metrics.
    /// </summary>
    private sealed class TenantMetrics
    {
        public long DocumentCount;
        public long ChunkCount;
        public long QueryCount;
    }
}

/// <summary>
/// Tracks latency values and calculates percentiles.
/// </summary>
internal sealed class LatencyTracker
{
    private readonly object _lock = new();
    private readonly List<double> _samples = new();
    private const int MaxSamples = 10000;

    /// <summary>
    /// Records a latency sample.
    /// </summary>
    /// <param name="durationMs">The duration in milliseconds.</param>
    public void Record(double durationMs)
    {
        lock (_lock)
        {
            _samples.Add(durationMs);

            // Keep only the last MaxSamples
            if (_samples.Count > MaxSamples)
            {
                _samples.RemoveAt(0);
            }
        }
    }

    /// <summary>
    /// Gets percentile values from the samples.
    /// </summary>
    /// <returns>Percentile statistics.</returns>
    public LatencyPercentiles GetPercentiles()
    {
        lock (_lock)
        {
            if (_samples.Count == 0)
            {
                return new LatencyPercentiles
                {
                    P50 = 0,
                    P95 = 0,
                    P99 = 0,
                    SampleCount = 0
                };
            }

            var sorted = _samples.OrderBy(x => x).ToList();

            return new LatencyPercentiles
            {
                P50 = GetPercentile(sorted, 50),
                P95 = GetPercentile(sorted, 95),
                P99 = GetPercentile(sorted, 99),
                SampleCount = sorted.Count
            };
        }
    }

    private static double GetPercentile(List<double> sorted, int percentile)
    {
        var index = (int)Math.Ceiling(sorted.Count * percentile / 100.0) - 1;
        index = Math.Max(0, Math.Min(index, sorted.Count - 1));
        return sorted[index];
    }
}

/// <summary>
/// Latency percentile statistics.
/// </summary>
public sealed class LatencyPercentiles
{
    /// <summary>
    /// 50th percentile (median) latency in milliseconds.
    /// </summary>
    [JsonPropertyName("p50_ms")]
    public double P50 { get; init; }

    /// <summary>
    /// 95th percentile latency in milliseconds.
    /// </summary>
    [JsonPropertyName("p95_ms")]
    public double P95 { get; init; }

    /// <summary>
    /// 99th percentile latency in milliseconds.
    /// </summary>
    [JsonPropertyName("p99_ms")]
    public double P99 { get; init; }

    /// <summary>
    /// Number of samples used to calculate percentiles.
    /// </summary>
    [JsonPropertyName("sample_count")]
    public int SampleCount { get; init; }
}

/// <summary>
/// Per-tenant metrics snapshot.
/// </summary>
public sealed class TenantMetricsSnapshot
{
    /// <summary>
    /// The tenant key.
    /// </summary>
    [JsonPropertyName("tenant_key")]
    public required string TenantKey { get; init; }

    /// <summary>
    /// Number of documents indexed for this tenant.
    /// </summary>
    [JsonPropertyName("document_count")]
    public long DocumentCount { get; init; }

    /// <summary>
    /// Number of chunks indexed for this tenant.
    /// </summary>
    [JsonPropertyName("chunk_count")]
    public long ChunkCount { get; init; }

    /// <summary>
    /// Number of queries executed for this tenant.
    /// </summary>
    [JsonPropertyName("query_count")]
    public long QueryCount { get; init; }
}

/// <summary>
/// Complete metrics snapshot.
/// </summary>
public sealed class MetricsSnapshot
{
    /// <summary>
    /// Timestamp when the snapshot was generated.
    /// </summary>
    [JsonPropertyName("generated_at")]
    public DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// Per-tenant metrics.
    /// </summary>
    [JsonPropertyName("tenant_metrics")]
    public required IReadOnlyList<TenantMetricsSnapshot> TenantMetrics { get; init; }

    /// <summary>
    /// Query latency percentiles.
    /// </summary>
    [JsonPropertyName("query_latency")]
    public required LatencyPercentiles QueryLatency { get; init; }

    /// <summary>
    /// Embedding generation latency percentiles.
    /// </summary>
    [JsonPropertyName("embedding_latency")]
    public required LatencyPercentiles EmbeddingLatency { get; init; }

    /// <summary>
    /// Cache hit rate (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("cache_hit_rate")]
    public double CacheHitRate { get; init; }

    /// <summary>
    /// Number of currently active projects.
    /// </summary>
    [JsonPropertyName("active_projects")]
    public int ActiveProjects { get; init; }
}
