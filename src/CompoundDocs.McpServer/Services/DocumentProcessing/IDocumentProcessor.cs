using CompoundDocs.McpServer.Models;

namespace CompoundDocs.McpServer.Services.DocumentProcessing;

/// <summary>
/// Interface for processing markdown documents into indexed format.
/// </summary>
public interface IDocumentProcessor
{
    /// <summary>
    /// Processes a single markdown document.
    /// </summary>
    /// <param name="filePath">The relative file path of the document.</param>
    /// <param name="content">The raw markdown content.</param>
    /// <param name="tenantKey">The tenant key for the document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The processed document with metadata and embeddings.</returns>
    Task<ProcessedDocument> ProcessDocumentAsync(
        string filePath,
        string content,
        string tenantKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes multiple markdown documents in batch.
    /// </summary>
    /// <param name="documents">Collection of (filePath, content) tuples.</param>
    /// <param name="tenantKey">The tenant key for the documents.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of processed documents with metadata and embeddings.</returns>
    Task<IReadOnlyList<ProcessedDocument>> ProcessDocumentsAsync(
        IEnumerable<(string FilePath, string Content)> documents,
        string tenantKey,
        CancellationToken cancellationToken = default);
}
