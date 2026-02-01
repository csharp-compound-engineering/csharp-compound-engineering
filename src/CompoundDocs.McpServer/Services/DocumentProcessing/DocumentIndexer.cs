using System.Text.Json;
using CompoundDocs.Common.Graph;
using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Models;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Services.DocumentProcessing;

/// <summary>
/// Implementation of document indexing operations.
/// Orchestrates processing, storage, and link graph management.
/// </summary>
public sealed class DocumentIndexer : IDocumentIndexer
{
    private readonly IDocumentProcessor _documentProcessor;
    private readonly IDocumentRepository _documentRepository;
    private readonly DocumentLinkGraph _linkGraph;
    private readonly ILogger<DocumentIndexer> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public DocumentIndexer(
        IDocumentProcessor documentProcessor,
        IDocumentRepository documentRepository,
        DocumentLinkGraph linkGraph,
        ILogger<DocumentIndexer> logger)
    {
        _documentProcessor = documentProcessor ?? throw new ArgumentNullException(nameof(documentProcessor));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _linkGraph = linkGraph ?? throw new ArgumentNullException(nameof(linkGraph));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<DocumentIndexingResult> IndexDocumentAsync(
        string filePath,
        string content,
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Indexing document: {FilePath} for tenant {TenantKey}", filePath, tenantKey);

            // Process the document
            var processedDoc = await _documentProcessor.ProcessDocumentAsync(
                filePath, content, tenantKey, cancellationToken);

            if (!processedDoc.IsSuccess)
            {
                return DocumentIndexingResult.Failure(filePath, processedDoc.Error ?? "Processing failed");
            }

            // Create or update the document in repository
            var compoundDocument = await CreateCompoundDocumentAsync(processedDoc, tenantKey, cancellationToken);

            // Create chunks if document was chunked
            var chunkCount = 0;
            if (processedDoc.IsChunked)
            {
                chunkCount = await CreateChunksAsync(compoundDocument, processedDoc.Chunks, cancellationToken);
            }
            else
            {
                // Clear any existing chunks for this document
                await _documentRepository.DeleteChunksAsync(compoundDocument.Id, cancellationToken);
            }

            // Update link graph
            await UpdateLinkGraphAsync(filePath, processedDoc.Links);

            // Collect warnings
            var warnings = new List<string>();
            if (processedDoc.ValidationResult?.Warnings.Count > 0)
            {
                warnings.AddRange(processedDoc.ValidationResult.Warnings);
            }
            if (!processedDoc.ValidationResult?.IsValid == true)
            {
                warnings.AddRange(processedDoc.ValidationResult?.Errors ?? []);
            }

            _logger.LogInformation(
                "Indexed document {FilePath}: {ChunkCount} chunks, {LinkCount} links",
                filePath, chunkCount, processedDoc.Links.Count);

            return DocumentIndexingResult.Success(filePath, compoundDocument, chunkCount, warnings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document: {FilePath}", filePath);
            return DocumentIndexingResult.Failure(filePath, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentIndexingResult>> IndexDocumentsAsync(
        IEnumerable<(string FilePath, string Content)> documents,
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        var documentList = documents.ToList();
        var results = new List<DocumentIndexingResult>(documentList.Count);

        _logger.LogInformation("Indexing {Count} documents for tenant {TenantKey}",
            documentList.Count, tenantKey);

        // Process documents first (can be parallelized)
        var processedDocs = await _documentProcessor.ProcessDocumentsAsync(
            documentList, tenantKey, cancellationToken);

        // Index each processed document (needs to be sequential for thread-safety)
        foreach (var processedDoc in processedDocs)
        {
            if (!processedDoc.IsSuccess)
            {
                results.Add(DocumentIndexingResult.Failure(processedDoc.FilePath, processedDoc.Error ?? "Processing failed"));
                continue;
            }

            try
            {
                // Create or update the document
                var compoundDocument = await CreateCompoundDocumentAsync(processedDoc, tenantKey, cancellationToken);

                // Create chunks if needed
                var chunkCount = 0;
                if (processedDoc.IsChunked)
                {
                    chunkCount = await CreateChunksAsync(compoundDocument, processedDoc.Chunks, cancellationToken);
                }
                else
                {
                    await _documentRepository.DeleteChunksAsync(compoundDocument.Id, cancellationToken);
                }

                // Update link graph
                await UpdateLinkGraphAsync(processedDoc.FilePath, processedDoc.Links);

                // Collect warnings
                var warnings = new List<string>();
                if (processedDoc.ValidationResult?.Warnings.Count > 0)
                {
                    warnings.AddRange(processedDoc.ValidationResult.Warnings);
                }

                results.Add(DocumentIndexingResult.Success(processedDoc.FilePath, compoundDocument, chunkCount, warnings));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to store document: {FilePath}", processedDoc.FilePath);
                results.Add(DocumentIndexingResult.Failure(processedDoc.FilePath, ex.Message));
            }
        }

        var successCount = results.Count(r => r.IsSuccess);
        _logger.LogInformation(
            "Indexed {Count} documents: {Success} succeeded, {Failed} failed",
            documentList.Count, successCount, documentList.Count - successCount);

        return results;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDocumentAsync(
        string tenantKey,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Deleting document: {FilePath} from tenant {TenantKey}", filePath, tenantKey);

            // Find the document
            var document = await _documentRepository.GetByTenantKeyAsync(tenantKey, filePath, cancellationToken);
            if (document == null)
            {
                _logger.LogDebug("Document not found: {FilePath}", filePath);
                return false;
            }

            // Delete chunks first
            await _documentRepository.DeleteChunksAsync(document.Id, cancellationToken);

            // Delete the document
            var deleted = await _documentRepository.DeleteAsync(document.Id, cancellationToken);

            // Update link graph
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _linkGraph.RemoveDocument(filePath);
            }
            finally
            {
                _lock.Release();
            }

            _logger.LogInformation("Deleted document: {FilePath}", filePath);
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document: {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Creates or updates a CompoundDocument from a ProcessedDocument.
    /// </summary>
    private async Task<CompoundDocument> CreateCompoundDocumentAsync(
        ProcessedDocument processedDoc,
        string tenantKey,
        CancellationToken cancellationToken)
    {
        // Check if document already exists
        var existing = await _documentRepository.GetByTenantKeyAsync(
            tenantKey, processedDoc.FilePath, cancellationToken);

        var document = existing ?? new CompoundDocument();

        document.TenantKey = tenantKey;
        document.FilePath = processedDoc.FilePath;
        document.Title = processedDoc.Title;
        document.DocType = processedDoc.DocType;
        document.PromotionLevel = processedDoc.PromotionLevel;
        document.Content = processedDoc.Content;
        document.Vector = processedDoc.Embedding;
        document.LastModified = DateTimeOffset.UtcNow;

        // Serialize links as JSON
        if (processedDoc.Links.Count > 0)
        {
            var linkUrls = processedDoc.Links.Select(l => l.Url).ToList();
            document.Links = JsonSerializer.Serialize(linkUrls);
        }
        else
        {
            document.Links = null;
        }

        return await _documentRepository.UpsertAsync(document, cancellationToken);
    }

    /// <summary>
    /// Creates DocumentChunks from ProcessedChunks.
    /// </summary>
    private async Task<int> CreateChunksAsync(
        CompoundDocument parentDocument,
        IReadOnlyList<ProcessedChunk> processedChunks,
        CancellationToken cancellationToken)
    {
        // First delete existing chunks
        await _documentRepository.DeleteChunksAsync(parentDocument.Id, cancellationToken);

        if (processedChunks.Count == 0)
            return 0;

        // Create new chunks
        var chunks = processedChunks.Select(pc => new DocumentChunk
        {
            DocumentId = parentDocument.Id,
            TenantKey = parentDocument.TenantKey,
            HeaderPath = pc.HeaderPath,
            StartLine = pc.StartLine,
            EndLine = pc.EndLine,
            Content = pc.Content,
            PromotionLevel = parentDocument.PromotionLevel,
            Vector = pc.Embedding
        }).ToList();

        return await _documentRepository.UpsertChunksAsync(chunks, cancellationToken);
    }

    /// <summary>
    /// Updates the document link graph.
    /// </summary>
    private async Task UpdateLinkGraphAsync(
        string filePath,
        IReadOnlyList<Common.Parsing.LinkInfo> links)
    {
        await _lock.WaitAsync();
        try
        {
            // Add or update the document vertex
            _linkGraph.AddDocument(filePath);

            // Clear existing outgoing links
            _linkGraph.ClearLinksFrom(filePath);

            // Add new links
            foreach (var link in links)
            {
                // Only add links to internal documents (relative paths)
                if (!link.Url.StartsWith("http://") && !link.Url.StartsWith("https://"))
                {
                    // Normalize the link URL to a relative path
                    var targetPath = NormalizeLinkPath(filePath, link.Url);
                    _linkGraph.AddLink(filePath, targetPath);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Normalizes a link URL relative to the source document.
    /// </summary>
    private static string NormalizeLinkPath(string sourceFilePath, string linkUrl)
    {
        // Remove anchor if present
        var hashIndex = linkUrl.IndexOf('#');
        var cleanUrl = hashIndex >= 0 ? linkUrl[..hashIndex] : linkUrl;

        if (string.IsNullOrEmpty(cleanUrl))
            return sourceFilePath; // Self-reference

        // Handle relative paths
        if (cleanUrl.StartsWith("./"))
        {
            cleanUrl = cleanUrl[2..];
        }

        var sourceDir = Path.GetDirectoryName(sourceFilePath) ?? string.Empty;

        // Handle parent directory references
        while (cleanUrl.StartsWith("../"))
        {
            cleanUrl = cleanUrl[3..];
            sourceDir = Path.GetDirectoryName(sourceDir) ?? string.Empty;
        }

        // Combine paths
        var targetPath = string.IsNullOrEmpty(sourceDir)
            ? cleanUrl
            : Path.Combine(sourceDir, cleanUrl).Replace('\\', '/');

        return targetPath;
    }
}
