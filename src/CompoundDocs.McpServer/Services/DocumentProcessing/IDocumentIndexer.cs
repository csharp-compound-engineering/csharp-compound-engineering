using CompoundDocs.McpServer.Models;

namespace CompoundDocs.McpServer.Services.DocumentProcessing;

/// <summary>
/// High-level service interface for indexing documents with tenant context.
/// Orchestrates document processing, storage, and link graph management.
/// </summary>
/// <remarks>
/// This is distinct from <see cref="Processing.IDocumentIndexer"/> which is a lower-level
/// interface focused on the processing pipeline with timing metrics.
/// </remarks>
public interface IDocumentIndexer
{
    /// <summary>
    /// Indexes a single document by processing and storing it.
    /// </summary>
    /// <param name="filePath">The relative file path of the document.</param>
    /// <param name="content">The raw markdown content.</param>
    /// <param name="tenantKey">The tenant key for the document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The indexed compound document, or null if indexing failed.</returns>
    Task<DocumentIndexingResult> IndexDocumentAsync(
        string filePath,
        string content,
        string tenantKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes multiple documents in batch.
    /// </summary>
    /// <param name="documents">Collection of (filePath, content) tuples.</param>
    /// <param name="tenantKey">The tenant key for the documents.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of index results for each document.</returns>
    Task<IReadOnlyList<DocumentIndexingResult>> IndexDocumentsAsync(
        IEnumerable<(string FilePath, string Content)> documents,
        string tenantKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document from the index.
    /// </summary>
    /// <param name="tenantKey">The tenant key of the document.</param>
    /// <param name="filePath">The relative file path of the document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the document was found and deleted.</returns>
    Task<bool> DeleteDocumentAsync(
        string tenantKey,
        string filePath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a document indexing operation from the service layer.
/// Distinct from Processing.IndexResult which contains timing metrics.
/// </summary>
public sealed class DocumentIndexingResult
{
    /// <summary>
    /// The file path of the indexed document.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Whether the indexing operation succeeded.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// The indexed document if successful.
    /// </summary>
    public CompoundDocument? Document { get; init; }

    /// <summary>
    /// Number of chunks created for this document.
    /// </summary>
    public int ChunkCount { get; init; }

    /// <summary>
    /// Error message if indexing failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Validation warnings for the document.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Creates a successful index result.
    /// </summary>
    public static DocumentIndexingResult Success(
        string filePath,
        CompoundDocument document,
        int chunkCount,
        IReadOnlyList<string>? warnings = null)
    {
        return new DocumentIndexingResult
        {
            FilePath = filePath,
            IsSuccess = true,
            Document = document,
            ChunkCount = chunkCount,
            Warnings = warnings ?? []
        };
    }

    /// <summary>
    /// Creates a failed index result.
    /// </summary>
    public static DocumentIndexingResult Failure(string filePath, string error)
    {
        return new DocumentIndexingResult
        {
            FilePath = filePath,
            IsSuccess = false,
            Error = error
        };
    }
}
