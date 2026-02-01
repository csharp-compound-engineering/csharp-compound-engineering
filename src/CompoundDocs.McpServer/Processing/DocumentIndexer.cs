using System.Diagnostics;
using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Hooks;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.SemanticKernel;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Processing;

/// <summary>
/// Implements document indexing operations including parsing, chunking, embedding generation, and storage.
/// Orchestrates the complete document processing pipeline with hook support.
/// </summary>
public sealed class DocumentIndexer : IDocumentIndexer
{
    private readonly DocumentParser _documentParser;
    private readonly ChunkingStrategy _chunkingStrategy;
    private readonly FrontmatterParser _frontmatterParser;
    private readonly DocumentValidator _documentValidator;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentRepository _documentRepository;
    private readonly DocumentHookExecutor? _hookExecutor;
    private readonly ILogger<DocumentIndexer> _logger;
    private readonly string _tenantKey;
    private readonly string _basePath;

    /// <summary>
    /// Creates a new instance of DocumentIndexer.
    /// </summary>
    /// <param name="documentParser">Parser for markdown documents.</param>
    /// <param name="chunkingStrategy">Strategy for chunking large documents.</param>
    /// <param name="frontmatterParser">Parser for YAML frontmatter.</param>
    /// <param name="documentValidator">Validator for document structure.</param>
    /// <param name="embeddingService">Service for generating embeddings.</param>
    /// <param name="documentRepository">Repository for storing documents.</param>
    /// <param name="hookExecutor">Optional hook executor for document lifecycle events.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="tenantKey">The tenant key for document isolation.</param>
    /// <param name="basePath">The base path for resolving file paths.</param>
    public DocumentIndexer(
        DocumentParser documentParser,
        ChunkingStrategy chunkingStrategy,
        FrontmatterParser frontmatterParser,
        DocumentValidator documentValidator,
        IEmbeddingService embeddingService,
        IDocumentRepository documentRepository,
        DocumentHookExecutor? hookExecutor,
        ILogger<DocumentIndexer> logger,
        string tenantKey,
        string basePath = "")
    {
        _documentParser = documentParser ?? throw new ArgumentNullException(nameof(documentParser));
        _chunkingStrategy = chunkingStrategy ?? throw new ArgumentNullException(nameof(chunkingStrategy));
        _frontmatterParser = frontmatterParser ?? throw new ArgumentNullException(nameof(frontmatterParser));
        _documentValidator = documentValidator ?? throw new ArgumentNullException(nameof(documentValidator));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _hookExecutor = hookExecutor;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tenantKey = tenantKey ?? throw new ArgumentNullException(nameof(tenantKey));
        _basePath = basePath ?? string.Empty;
    }

    /// <inheritdoc />
    public async Task<IndexResult> IndexAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var builder = new IndexResultBuilder().WithFilePath(filePath);

        try
        {
            var fullPath = GetFullPath(filePath);

            if (!File.Exists(fullPath))
            {
                return builder.WithError($"File not found: {fullPath}").Build();
            }

            var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
            return await IndexContentInternalAsync(content, filePath, builder, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return builder.WithError("Operation was cancelled").Build();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document: {FilePath}", filePath);
            return builder.WithError($"Unexpected error: {ex.Message}").Build();
        }
    }

    /// <inheritdoc />
    public async Task<IndexResult> IndexContentAsync(string content, string filePath, CancellationToken cancellationToken = default)
    {
        var builder = new IndexResultBuilder().WithFilePath(filePath);

        try
        {
            return await IndexContentInternalAsync(content, filePath, builder, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return builder.WithError("Operation was cancelled").Build();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document content: {FilePath}", filePath);
            return builder.WithError($"Unexpected error: {ex.Message}").Build();
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Deleting document: {DocumentId}", documentId);

            // Get document for hook context
            var document = await _documentRepository.GetByIdAsync(documentId, cancellationToken);
            if (document == null)
            {
                _logger.LogDebug("Document not found for deletion: {DocumentId}", documentId);
                return false;
            }

            // Execute before-delete hooks
            if (_hookExecutor != null)
            {
                var hookContext = new DocumentHookContext
                {
                    Document = document,
                    FilePath = document.FilePath,
                    TenantKey = _tenantKey,
                    DocType = document.DocType
                };

                var hookResult = await _hookExecutor.ExecuteBeforeDeleteAsync(hookContext, cancellationToken);
                if (!hookResult.ShouldProceed)
                {
                    _logger.LogInformation("Document deletion cancelled by hook: {Reason}", hookResult.CancelReason);
                    return false;
                }
            }

            // Delete chunks first
            await _documentRepository.DeleteChunksAsync(documentId, cancellationToken);

            // Delete the document
            var deleted = await _documentRepository.DeleteAsync(documentId, cancellationToken);

            // Execute after-delete hooks
            if (deleted && _hookExecutor != null)
            {
                var hookContext = new DocumentHookContext
                {
                    Document = document,
                    FilePath = document.FilePath,
                    TenantKey = _tenantKey,
                    DocType = document.DocType
                };

                await _hookExecutor.ExecuteAfterDeleteAsync(hookContext, cancellationToken);
            }

            _logger.LogInformation("Deleted document: {DocumentId}", documentId);
            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document: {DocumentId}", documentId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<int> ReindexAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting reindex of all documents for tenant: {TenantKey}", _tenantKey);

            var documents = await _documentRepository.GetAllForTenantAsync(_tenantKey, cancellationToken: cancellationToken);
            var reindexedCount = 0;

            foreach (var document in documents)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    // Re-read file if it exists
                    var fullPath = GetFullPath(document.FilePath);
                    if (File.Exists(fullPath))
                    {
                        var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
                        var result = await IndexContentAsync(content, document.FilePath, cancellationToken);
                        if (result.IsSuccess)
                        {
                            reindexedCount++;
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Failed to reindex document {FilePath}: {Errors}",
                                document.FilePath,
                                string.Join("; ", result.Errors));
                        }
                    }
                    else
                    {
                        // Document file no longer exists - delete from index
                        await DeleteAsync(document.Id, cancellationToken);
                        _logger.LogInformation(
                            "Removed document from index (file not found): {FilePath}",
                            document.FilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reindexing document: {FilePath}", document.FilePath);
                }
            }

            _logger.LogInformation(
                "Reindex completed: {Count} documents reindexed for tenant {TenantKey}",
                reindexedCount,
                _tenantKey);

            return reindexedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reindex all documents");
            return 0;
        }
    }

    /// <summary>
    /// Internal implementation of content indexing with builder support.
    /// </summary>
    private async Task<IndexResult> IndexContentInternalAsync(
        string content,
        string filePath,
        IndexResultBuilder builder,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Indexing document: {FilePath}", filePath);

        // Parse the document
        var parsedDocument = _documentParser.ParseDetailed(content);
        if (!parsedDocument.IsSuccess)
        {
            return builder.WithError($"Failed to parse document: {parsedDocument.Error}").Build();
        }

        // Validate the document
        var validationResult = _documentValidator.Validate(parsedDocument);
        if (!validationResult.IsValid)
        {
            builder.WithErrors(validationResult.Errors);
            builder.WithWarnings(validationResult.Warnings);
            return builder.Build();
        }

        builder.WithWarnings(validationResult.Warnings);
        builder.WithDocType(validationResult.DocType);
        builder.WithTitle(parsedDocument.Title);

        // Get doc type and promotion level from frontmatter
        var docType = validationResult.DocType;
        var promotionLevel = GetPromotionLevel(parsedDocument.Frontmatter);

        // Execute before-index hooks
        if (_hookExecutor != null)
        {
            var hookContext = new DocumentHookContext
            {
                FilePath = filePath,
                TenantKey = _tenantKey,
                DocType = docType,
                RawContent = content,
                Frontmatter = parsedDocument.Frontmatter
            };

            var hookResult = await _hookExecutor.ExecuteBeforeIndexAsync(hookContext, cancellationToken);
            if (!hookResult.ShouldProceed)
            {
                return builder
                    .WithError($"Indexing cancelled by hook: {hookResult.CancelReason}")
                    .WithWarnings(hookResult.Warnings)
                    .Build();
            }

            builder.WithWarnings(hookResult.Warnings);
        }

        // Generate document embedding
        builder.StartEmbeddingTimer();
        ReadOnlyMemory<float>? documentEmbedding;
        try
        {
            documentEmbedding = await _embeddingService.GenerateEmbeddingAsync(parsedDocument.Body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for document: {FilePath}", filePath);
            return builder.WithError($"Failed to generate embedding: {ex.Message}").Build();
        }

        // Check for existing document
        var existingDocument = await _documentRepository.GetByTenantKeyAsync(_tenantKey, filePath, cancellationToken);
        var document = existingDocument ?? new CompoundDocument { Id = Guid.NewGuid().ToString() };

        // Update document properties
        document.TenantKey = _tenantKey;
        document.FilePath = filePath;
        document.Title = parsedDocument.Title;
        document.DocType = docType;
        document.PromotionLevel = promotionLevel;
        document.Content = parsedDocument.Body;
        document.Vector = documentEmbedding;
        document.LastModified = DateTimeOffset.UtcNow;

        // Process chunks if document is large
        var chunks = new List<DocumentChunk>();
        if (_chunkingStrategy.ShouldChunk(parsedDocument.Body))
        {
            var contentChunks = _chunkingStrategy.Chunk(parsedDocument.Body, document.Id);

            // Generate embeddings for chunks
            if (contentChunks.Count > 0)
            {
                var chunkContents = contentChunks.Select(c => c.Content).ToList();
                IReadOnlyList<ReadOnlyMemory<float>> chunkEmbeddings;

                try
                {
                    chunkEmbeddings = await _embeddingService.GenerateEmbeddingsAsync(chunkContents, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate chunk embeddings for document: {FilePath}", filePath);
                    return builder.WithError($"Failed to generate chunk embeddings: {ex.Message}").Build();
                }

                for (var i = 0; i < contentChunks.Count; i++)
                {
                    var contentChunk = contentChunks[i];
                    chunks.Add(new DocumentChunk
                    {
                        Id = Guid.NewGuid().ToString(),
                        DocumentId = document.Id,
                        TenantKey = _tenantKey,
                        HeaderPath = $"Chunk {contentChunk.Index}",
                        StartLine = GetLineNumber(parsedDocument.Body, contentChunk.StartOffset),
                        EndLine = GetLineNumber(parsedDocument.Body, contentChunk.EndOffset),
                        Content = contentChunk.Content,
                        PromotionLevel = promotionLevel,
                        Vector = chunkEmbeddings[i]
                    });
                }
            }
        }

        builder.StopEmbeddingTimer();
        builder.WithChunkCount(chunks.Count);

        // Save document
        var savedDocument = await _documentRepository.UpsertAsync(document, cancellationToken);
        builder.WithDocumentId(savedDocument.Id);

        // Save chunks (delete old ones first if updating)
        if (existingDocument != null)
        {
            await _documentRepository.DeleteChunksAsync(savedDocument.Id, cancellationToken);
        }

        if (chunks.Count > 0)
        {
            await _documentRepository.UpsertChunksAsync(chunks, cancellationToken);
        }

        // Execute after-index hooks
        if (_hookExecutor != null)
        {
            var hookContext = new DocumentHookContext
            {
                Document = savedDocument,
                FilePath = filePath,
                TenantKey = _tenantKey,
                DocType = docType,
                RawContent = content,
                Frontmatter = parsedDocument.Frontmatter
            };

            var hookResult = await _hookExecutor.ExecuteAfterIndexAsync(hookContext, cancellationToken);
            builder.WithWarnings(hookResult.Warnings);
        }

        _logger.LogInformation(
            "Indexed document {FilePath}: {ChunkCount} chunks, DocType={DocType}",
            filePath,
            chunks.Count,
            docType);

        return builder.Build();
    }

    /// <summary>
    /// Gets the full path for a file by combining with the base path.
    /// </summary>
    private string GetFullPath(string filePath)
    {
        if (string.IsNullOrEmpty(_basePath))
        {
            return filePath;
        }

        return Path.Combine(_basePath, filePath);
    }

    /// <summary>
    /// Gets the promotion level from frontmatter.
    /// </summary>
    private static string GetPromotionLevel(Dictionary<string, object?>? frontmatter)
    {
        if (frontmatter == null)
        {
            return PromotionLevels.Standard;
        }

        if (frontmatter.TryGetValue("promotion_level", out var levelValue) && levelValue is string level)
        {
            if (PromotionLevels.IsValid(level))
            {
                return level.ToLowerInvariant();
            }
        }

        if (frontmatter.TryGetValue("promotionLevel", out levelValue) && levelValue is string level2)
        {
            if (PromotionLevels.IsValid(level2))
            {
                return level2.ToLowerInvariant();
            }
        }

        return PromotionLevels.Standard;
    }

    /// <summary>
    /// Gets the line number for a character offset in content.
    /// </summary>
    private static int GetLineNumber(string content, int offset)
    {
        if (string.IsNullOrEmpty(content) || offset <= 0)
        {
            return 1;
        }

        var safeOffset = Math.Min(offset, content.Length);
        return content[..safeOffset].Count(c => c == '\n') + 1;
    }
}
