namespace CompoundDocs.McpServer.Services.Queuing;

/// <summary>
/// Represents a document that is waiting to be indexed when Ollama becomes available.
/// </summary>
/// <param name="FilePath">Absolute path to the document file.</param>
/// <param name="ContentHash">SHA256 hash of document content at time of queueing.</param>
/// <param name="QueuedAt">UTC timestamp when document was added to queue.</param>
/// <param name="RetryCount">Number of times indexing has been attempted for this document.</param>
public sealed record DeferredDocument(
    string FilePath,
    string ContentHash,
    DateTimeOffset QueuedAt,
    int RetryCount = 0)
{
    /// <summary>
    /// Creates a new instance with incremented retry count.
    /// </summary>
    public DeferredDocument WithRetry() =>
        this with { RetryCount = RetryCount + 1 };
}
