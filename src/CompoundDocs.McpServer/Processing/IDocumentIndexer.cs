namespace CompoundDocs.McpServer.Processing;

/// <summary>
/// Low-level processing pipeline interface for document indexing.
/// Handles parsing, chunking, embedding generation, and storage with timing metrics.
/// </summary>
/// <remarks>
/// This is distinct from <see cref="Services.DocumentProcessing.IDocumentIndexer"/> which is a
/// higher-level service interface with tenant context and link graph management.
/// </remarks>
public interface IDocumentIndexer
{
    /// <summary>
    /// Indexes a document from a file path.
    /// </summary>
    /// <param name="filePath">The file path to index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The index result containing status and metadata.</returns>
    Task<IndexResult> IndexAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes a document from content string.
    /// </summary>
    /// <param name="content">The document content.</param>
    /// <param name="filePath">The logical file path (for identification).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The index result containing status and metadata.</returns>
    Task<IndexResult> IndexContentAsync(string content, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document from the index.
    /// </summary>
    /// <param name="documentId">The document ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the document was successfully deleted.</returns>
    Task<bool> DeleteAsync(string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reindexes all documents in the store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of documents that were reindexed.</returns>
    Task<int> ReindexAllAsync(CancellationToken cancellationToken = default);
}
