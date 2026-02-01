using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Session;
using Microsoft.Extensions.Logging;
using ProcessingIndexer = CompoundDocs.McpServer.Processing.IDocumentIndexer;

namespace CompoundDocs.McpServer.Services.FileWatcher;

/// <summary>
/// Production implementation of IFileWatcherDocumentIndexer that uses the Processing.DocumentIndexer
/// for actual indexing operations.
/// </summary>
public sealed class DatabaseDocumentIndexer : IFileWatcherDocumentIndexer
{
    private readonly Func<ProcessingIndexer> _indexerFactory;
    private readonly IDocumentRepository _documentRepository;
    private readonly ISessionContext _sessionContext;
    private readonly ILogger<DatabaseDocumentIndexer> _logger;

    /// <summary>
    /// Creates a new instance of DatabaseDocumentIndexer.
    /// </summary>
    /// <param name="indexerFactory">Factory for creating document indexers with current session context.</param>
    /// <param name="documentRepository">The document repository for fallback operations.</param>
    /// <param name="sessionContext">The session context for tenant information.</param>
    /// <param name="logger">The logger instance.</param>
    public DatabaseDocumentIndexer(
        Func<ProcessingIndexer> indexerFactory,
        IDocumentRepository documentRepository,
        ISessionContext sessionContext,
        ILogger<DatabaseDocumentIndexer> logger)
    {
        _indexerFactory = indexerFactory ?? throw new ArgumentNullException(nameof(indexerFactory));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> IndexDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var tenantKey = _sessionContext.TenantKey;
        if (string.IsNullOrEmpty(tenantKey))
        {
            _logger.LogWarning("No active project, cannot index document: {FilePath}", filePath);
            return false;
        }

        _logger.LogDebug("Indexing document: {FilePath} for tenant: {TenantKey}", filePath, tenantKey);

        try
        {
            var indexer = _indexerFactory();
            var result = await indexer.IndexAsync(filePath, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Document indexing failed for {FilePath}: {Errors}",
                    filePath,
                    string.Join("; ", result.Errors));
                return false;
            }

            _logger.LogInformation(
                "Successfully indexed document: {FilePath}, ID: {DocumentId}",
                filePath,
                result.DocumentId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing document: {FilePath}", filePath);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var tenantKey = _sessionContext.TenantKey;
        if (string.IsNullOrEmpty(tenantKey))
        {
            _logger.LogWarning("No active project, cannot delete document: {FilePath}", filePath);
            return false;
        }

        _logger.LogDebug("Deleting document: {FilePath} for tenant: {TenantKey}", filePath, tenantKey);

        try
        {
            // Find the document by tenant key and file path
            var document = await _documentRepository.GetByTenantKeyAsync(tenantKey, filePath, cancellationToken);
            if (document == null)
            {
                _logger.LogDebug("Document not found for deletion: {FilePath}", filePath);
                return true; // Consider it successful if already gone
            }

            // Use the processing indexer for deletion (handles hooks)
            var indexer = _indexerFactory();
            var deleted = await indexer.DeleteAsync(document.Id, cancellationToken);

            if (deleted)
            {
                _logger.LogInformation("Successfully deleted document: {FilePath}", filePath);
            }
            else
            {
                _logger.LogWarning("Failed to delete document: {FilePath}", filePath);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document: {FilePath}", filePath);
            return false;
        }
    }
}
