using System.Diagnostics;

namespace CompoundDocs.McpServer.Observability;

/// <summary>
/// Collects and exposes operational metrics for the MCP server.
/// </summary>
public interface IMetricsCollector : IDisposable
{
    /// <summary>
    /// Gets the ActivitySource for creating tracing activities.
    /// </summary>
    ActivitySource ActivitySource { get; }

    /// <summary>
    /// Records a document being indexed.
    /// </summary>
    void RecordDocumentIndexed(int chunkCount, double durationMs);

    /// <summary>
    /// Records a query being executed.
    /// </summary>
    void RecordQuery(double durationMs, int resultCount);

    /// <summary>
    /// Records an embedding generation operation.
    /// </summary>
    void RecordEmbeddingGeneration(double durationMs, int contentLength);

    /// <summary>
    /// Records a cache hit.
    /// </summary>
    void RecordCacheHit();

    /// <summary>
    /// Records a cache miss.
    /// </summary>
    void RecordCacheMiss();

    /// <summary>
    /// Records an error occurrence.
    /// </summary>
    void RecordError(string errorType);

    /// <summary>
    /// Starts a new activity for tracing.
    /// </summary>
    Activity? StartActivity(string operationName, ActivityKind kind = ActivityKind.Internal);

    /// <summary>
    /// Gets the current metrics snapshot.
    /// </summary>
    MetricsSnapshot GetSnapshot();

    /// <summary>
    /// Calculates the cache hit rate.
    /// </summary>
    double CalculateCacheHitRate();
}
