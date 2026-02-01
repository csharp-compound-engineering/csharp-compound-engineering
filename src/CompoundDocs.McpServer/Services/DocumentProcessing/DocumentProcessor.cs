using System.Text.Json;
using CompoundDocs.Common.Parsing;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.SemanticKernel;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Services.DocumentProcessing;

/// <summary>
/// Main document processor implementation.
/// Parses markdown files, extracts frontmatter, validates against doc-type schemas,
/// generates embeddings, and chunks large documents.
/// </summary>
public sealed class DocumentProcessor : IDocumentProcessor
{
    private readonly MarkdownParser _markdownParser;
    private readonly FrontmatterParser _frontmatterParser;
    private readonly SchemaValidator _schemaValidator;
    private readonly IEmbeddingService _embeddingService;
    private readonly DocumentChunker _documentChunker;
    private readonly ILogger<DocumentProcessor> _logger;

    /// <summary>
    /// JSON schemas for built-in doc-types.
    /// Keys are doc-type names, values are JSON schema strings.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> BuiltInSchemas = new Dictionary<string, string>
    {
        [DocumentTypes.Problem] = GetProblemSchema(),
        [DocumentTypes.Insight] = GetInsightSchema(),
        [DocumentTypes.Codebase] = GetCodebaseSchema(),
        [DocumentTypes.Tool] = GetToolSchema(),
        [DocumentTypes.Style] = GetStyleSchema()
    };

    public DocumentProcessor(
        MarkdownParser markdownParser,
        FrontmatterParser frontmatterParser,
        SchemaValidator schemaValidator,
        IEmbeddingService embeddingService,
        DocumentChunker documentChunker,
        ILogger<DocumentProcessor> logger)
    {
        _markdownParser = markdownParser ?? throw new ArgumentNullException(nameof(markdownParser));
        _frontmatterParser = frontmatterParser ?? throw new ArgumentNullException(nameof(frontmatterParser));
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _documentChunker = documentChunker ?? throw new ArgumentNullException(nameof(documentChunker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ProcessedDocument> ProcessDocumentAsync(
        string filePath,
        string content,
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Processing document: {FilePath}; TenantKey={TenantKey}; ContentLength={ContentLength}",
                filePath, tenantKey, content.Length);

            // Parse frontmatter
            var frontmatterResult = _frontmatterParser.Parse(content);
            var frontmatter = frontmatterResult.Frontmatter;
            var bodyContent = frontmatterResult.Body;

            // Extract metadata from frontmatter
            var title = ExtractTitle(frontmatter, bodyContent, filePath);
            var docType = ExtractDocType(frontmatter);
            var promotionLevel = ExtractPromotionLevel(frontmatter);

            // Validate against doc-type schema
            var validationResult = await ValidateDocTypeAsync(docType, frontmatter, cancellationToken);

            // Parse markdown for links
            var document = _markdownParser.Parse(bodyContent);
            var links = _markdownParser.ExtractLinks(document);

            // Determine if document should be chunked
            var shouldChunk = _documentChunker.ShouldChunk(bodyContent);
            var processedChunks = new List<ProcessedChunk>();
            ReadOnlyMemory<float>? documentEmbedding = null;

            if (shouldChunk)
            {
                _logger.LogDebug("Document {FilePath} exceeds {Threshold} lines, chunking; TenantKey={TenantKey}",
                    filePath, _documentChunker.ChunkThreshold, tenantKey);

                // Create chunks
                var chunks = _documentChunker.CreateProcessedChunks(bodyContent);

                // Generate embeddings for all chunks in batch
                var chunkContents = chunks.Select(c => c.Content).ToList();
                var chunkEmbeddings = await _embeddingService.GenerateEmbeddingsAsync(
                    chunkContents, cancellationToken);

                // Combine chunks with their embeddings
                for (int i = 0; i < chunks.Count; i++)
                {
                    processedChunks.Add(new ProcessedChunk
                    {
                        Index = chunks[i].Index,
                        HeaderPath = chunks[i].HeaderPath,
                        StartLine = chunks[i].StartLine,
                        EndLine = chunks[i].EndLine,
                        Content = chunks[i].Content,
                        Embedding = i < chunkEmbeddings.Count ? chunkEmbeddings[i] : null
                    });
                }

                // For chunked documents, generate embedding from title + first chunk summary
                var summaryContent = $"{title}\n\n{chunks.FirstOrDefault()?.Content ?? bodyContent}";
                if (summaryContent.Length > 8000)
                    summaryContent = summaryContent[..8000];

                documentEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                    summaryContent, cancellationToken);
            }
            else
            {
                // Generate single embedding for the entire document
                documentEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                    bodyContent, cancellationToken);
            }

            _logger.LogDebug("Processed document {FilePath}: {ChunkCount} chunks, valid={IsValid}, DocType={DocType}, TenantKey={TenantKey}",
                filePath, processedChunks.Count, validationResult.IsValid, docType, tenantKey);

            return ProcessedDocument.Success(
                filePath,
                title,
                docType,
                promotionLevel,
                bodyContent,
                documentEmbedding,
                processedChunks,
                links,
                frontmatter,
                validationResult,
                tenantKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process document: {FilePath}; TenantKey={TenantKey}; ErrorMessage={ErrorMessage}",
                filePath, tenantKey, ex.Message);
            return ProcessedDocument.Failure(filePath, tenantKey, ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProcessedDocument>> ProcessDocumentsAsync(
        IEnumerable<(string FilePath, string Content)> documents,
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        var documentList = documents.ToList();
        var results = new List<ProcessedDocument>(documentList.Count);

        _logger.LogInformation("Processing {Count} documents", documentList.Count);

        // Process documents in parallel with limited concurrency
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
        var tasks = documentList.Select(async doc =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ProcessDocumentAsync(doc.FilePath, doc.Content, tenantKey, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var processedDocs = await Task.WhenAll(tasks);
        results.AddRange(processedDocs);

        var successCount = results.Count(r => r.IsSuccess);
        _logger.LogInformation("Processed {Count} documents: {Success} succeeded, {Failed} failed",
            documentList.Count, successCount, documentList.Count - successCount);

        return results;
    }

    /// <summary>
    /// Extracts the document title from frontmatter or first heading.
    /// </summary>
    private string ExtractTitle(
        IReadOnlyDictionary<string, object?>? frontmatter,
        string bodyContent,
        string filePath)
    {
        // Try to get title from frontmatter
        if (frontmatter?.TryGetValue("title", out var titleObj) == true && titleObj is string title)
        {
            return title;
        }

        // Fall back to first heading
        var document = _markdownParser.Parse(bodyContent);
        var headers = _markdownParser.ExtractHeaders(document);
        if (headers.Count > 0)
        {
            return headers[0].Text;
        }

        // Fall back to file name
        return Path.GetFileNameWithoutExtension(filePath);
    }

    /// <summary>
    /// Extracts the document type from frontmatter.
    /// </summary>
    private static string ExtractDocType(IReadOnlyDictionary<string, object?>? frontmatter)
    {
        if (frontmatter?.TryGetValue("doc_type", out var docTypeObj) == true && docTypeObj is string docType)
        {
            return docType.ToLowerInvariant();
        }

        // Also check for "type" as an alternative key
        if (frontmatter?.TryGetValue("type", out var typeObj) == true && typeObj is string type)
        {
            return type.ToLowerInvariant();
        }

        return DocumentTypes.Doc; // Default to generic doc type
    }

    /// <summary>
    /// Extracts the promotion level from frontmatter.
    /// </summary>
    private static string ExtractPromotionLevel(IReadOnlyDictionary<string, object?>? frontmatter)
    {
        if (frontmatter?.TryGetValue("promotion_level", out var levelObj) == true && levelObj is string level)
        {
            if (PromotionLevels.IsValid(level))
            {
                return level.ToLowerInvariant();
            }
        }

        // Also check for "promotion" as an alternative key
        if (frontmatter?.TryGetValue("promotion", out var promoObj) == true && promoObj is string promo)
        {
            if (PromotionLevels.IsValid(promo))
            {
                return promo.ToLowerInvariant();
            }
        }

        return PromotionLevels.Standard;
    }

    /// <summary>
    /// Validates frontmatter against the doc-type schema.
    /// </summary>
    private async Task<DocumentValidationResult> ValidateDocTypeAsync(
        string docType,
        IReadOnlyDictionary<string, object?>? frontmatter,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(docType))
        {
            return DocumentValidationResult.NoDocType();
        }

        if (!BuiltInSchemas.TryGetValue(docType, out var schema))
        {
            return DocumentValidationResult.NoSchema(docType);
        }

        if (frontmatter == null)
        {
            return DocumentValidationResult.Failure(docType,
                ["Document has no frontmatter but requires validation against schema."]);
        }

        try
        {
            var result = await _schemaValidator.ValidateAsync(frontmatter, schema, cancellationToken);

            if (result.IsValid)
            {
                return DocumentValidationResult.Success(docType);
            }

            var errors = result.Errors.Select(e => $"{e.Path}: {e.Message}").ToList();
            return DocumentValidationResult.Failure(docType, errors);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Schema validation failed for doc-type {DocType}", docType);
            return DocumentValidationResult.Failure(docType, [$"Schema validation error: {ex.Message}"]);
        }
    }

    #region Built-in Schemas

    private static string GetProblemSchema() => JsonSerializer.Serialize(new
    {
        type = "object",
        properties = new
        {
            title = new { type = "string" },
            doc_type = new { type = "string", @const = "problem" },
            status = new { type = "string", @enum = new[] { "open", "investigating", "resolved", "wont-fix" } },
            severity = new { type = "string", @enum = new[] { "low", "medium", "high", "critical" } },
            tags = new { type = "array", items = new { type = "string" } },
            created = new { type = "string", format = "date" },
            updated = new { type = "string", format = "date" }
        },
        required = new[] { "title", "doc_type" }
    });

    private static string GetInsightSchema() => JsonSerializer.Serialize(new
    {
        type = "object",
        properties = new
        {
            title = new { type = "string" },
            doc_type = new { type = "string", @const = "insight" },
            category = new { type = "string" },
            tags = new { type = "array", items = new { type = "string" } },
            confidence = new { type = "string", @enum = new[] { "low", "medium", "high" } },
            created = new { type = "string", format = "date" }
        },
        required = new[] { "title", "doc_type" }
    });

    private static string GetCodebaseSchema() => JsonSerializer.Serialize(new
    {
        type = "object",
        properties = new
        {
            title = new { type = "string" },
            doc_type = new { type = "string", @const = "codebase" },
            component = new { type = "string" },
            language = new { type = "string" },
            tags = new { type = "array", items = new { type = "string" } },
            version = new { type = "string" }
        },
        required = new[] { "title", "doc_type" }
    });

    private static string GetToolSchema() => JsonSerializer.Serialize(new
    {
        type = "object",
        properties = new
        {
            title = new { type = "string" },
            doc_type = new { type = "string", @const = "tool" },
            tool_name = new { type = "string" },
            version = new { type = "string" },
            tags = new { type = "array", items = new { type = "string" } },
            url = new { type = "string", format = "uri" }
        },
        required = new[] { "title", "doc_type" }
    });

    private static string GetStyleSchema() => JsonSerializer.Serialize(new
    {
        type = "object",
        properties = new
        {
            title = new { type = "string" },
            doc_type = new { type = "string", @const = "style" },
            language = new { type = "string" },
            scope = new { type = "string", @enum = new[] { "project", "team", "organization" } },
            tags = new { type = "array", items = new { type = "string" } },
            version = new { type = "string" }
        },
        required = new[] { "title", "doc_type" }
    });

    #endregion
}
