namespace CompoundDocs.McpServer.Services.Queuing;

/// <summary>
/// Queue for documents pending indexing due to embedding service unavailability.
/// </summary>
public interface IDeferredIndexingQueue
{
    /// <summary>
    /// Adds a document to the deferred indexing queue.
    /// </summary>
    /// <param name="document">The document to queue.</param>
    /// <returns>True if enqueued successfully, false if rejected (e.g., overflow).</returns>
    bool TryEnqueue(DeferredDocument document);

    /// <summary>
    /// Attempts to remove and return the next document from the queue.
    /// </summary>
    /// <param name="document">The dequeued document, if available.</param>
    /// <returns>True if a document was dequeued, false if queue is empty.</returns>
    bool TryDequeue(out DeferredDocument? document);

    /// <summary>
    /// Returns the document at the front of the queue without removing it.
    /// </summary>
    /// <param name="document">The peeked document, if available.</param>
    /// <returns>True if a document exists, false if queue is empty.</returns>
    bool TryPeek(out DeferredDocument? document);

    /// <summary>
    /// Checks if a document with the given path is already in the queue.
    /// </summary>
    bool Contains(string filePath);

    /// <summary>
    /// Removes a document from the queue by file path (e.g., if file was deleted).
    /// </summary>
    /// <returns>True if document was found and removed.</returns>
    bool TryRemove(string filePath);

    /// <summary>
    /// Gets the current number of documents in the queue.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the maximum allowed queue size.
    /// </summary>
    int MaxSize { get; }

    /// <summary>
    /// Gets whether the queue is at or above capacity.
    /// </summary>
    bool IsFull { get; }

    /// <summary>
    /// Gets all queued documents (for diagnostics only).
    /// </summary>
    IReadOnlyList<DeferredDocument> GetSnapshot();

    /// <summary>
    /// Clears all documents from the queue.
    /// </summary>
    void Clear();
}
