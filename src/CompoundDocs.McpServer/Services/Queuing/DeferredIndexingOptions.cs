namespace CompoundDocs.McpServer.Services.Queuing;

/// <summary>
/// Configuration options for the deferred indexing queue.
/// </summary>
public class DeferredIndexingOptions
{
    public const string SectionName = "DeferredIndexing";

    /// <summary>
    /// Maximum number of documents that can be queued.
    /// Default: 1000
    /// </summary>
    public int MaxQueueSize { get; set; } = 1000;

    /// <summary>
    /// Strategy for handling queue overflow.
    /// Default: DropOldest
    /// </summary>
    public OverflowStrategy OverflowStrategy { get; set; } = OverflowStrategy.DropOldest;

    /// <summary>
    /// Percentage of queue capacity that triggers a warning log.
    /// Default: 80 (80%)
    /// </summary>
    public int WarningThresholdPercent { get; set; } = 80;

    /// <summary>
    /// Maximum retry attempts before dropping a document.
    /// Default: 5
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>
    /// Base delay for exponential backoff between retry attempts.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Delay between processing batches to avoid overwhelming Ollama.
    /// Default: 1 second
    /// </summary>
    public TimeSpan ProcessingBatchDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Number of documents to process per batch during recovery.
    /// Default: 10
    /// </summary>
    public int ProcessingBatchSize { get; set; } = 10;

    /// <summary>
    /// Interval for checking Ollama availability when queue has items.
    /// Default: 10 seconds
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Strategy for handling queue overflow when at capacity.
/// </summary>
public enum OverflowStrategy
{
    /// <summary>
    /// Remove the oldest document to make room for new ones.
    /// </summary>
    DropOldest,

    /// <summary>
    /// Drop the newest incoming document (keep queue as-is).
    /// </summary>
    DropNewest,

    /// <summary>
    /// Reject new documents when queue is full (caller handles).
    /// </summary>
    RejectNew
}
