using System.ComponentModel;
using System.Text.Json.Serialization;
using CompoundDocs.McpServer.Services.DocumentProcessing;
using CompoundDocs.McpServer.Session;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// MCP tool for manually triggering document indexing.
/// </summary>
[McpServerToolType]
public sealed class IndexDocumentTool
{
    private readonly IDocumentIndexer _documentIndexer;
    private readonly ISessionContext _sessionContext;
    private readonly ILogger<IndexDocumentTool> _logger;

    /// <summary>
    /// Creates a new instance of IndexDocumentTool.
    /// </summary>
    public IndexDocumentTool(
        IDocumentIndexer documentIndexer,
        ISessionContext sessionContext,
        ILogger<IndexDocumentTool> logger)
    {
        _documentIndexer = documentIndexer ?? throw new ArgumentNullException(nameof(documentIndexer));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Index a document by its file path.
    /// </summary>
    /// <param name="filePath">The relative path to the document from the project root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The indexing result.</returns>
    [McpServerTool(Name = "index_document")]
    [Description("Manually trigger indexing of a document. The document will be processed, chunked, and stored in the vector database.")]
    public async Task<ToolResponse<IndexDocumentResult>> IndexDocumentAsync(
        [Description("The relative path to the document from the project root")] string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!_sessionContext.IsProjectActive)
        {
            return ToolResponse<IndexDocumentResult>.Fail(ToolErrors.NoActiveProject);
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ToolResponse<IndexDocumentResult>.Fail(
                ToolErrors.MissingParameter("file_path"));
        }

        _logger.LogInformation(
            "Indexing document: {FilePath} for tenant {TenantKey}",
            filePath,
            _sessionContext.TenantKey);

        try
        {
            // Resolve full path
            var fullPath = Path.Combine(_sessionContext.ActiveProjectPath!, filePath);

            if (!File.Exists(fullPath))
            {
                return ToolResponse<IndexDocumentResult>.Fail(
                    ToolErrors.FileNotFound(filePath));
            }

            // Read file content
            string content;
            try
            {
                content = await File.ReadAllTextAsync(fullPath, cancellationToken);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Failed to read file: {FilePath}", filePath);
                return ToolResponse<IndexDocumentResult>.Fail(
                    ToolErrors.FileReadError(filePath, ex.Message));
            }

            // Index the document
            var result = await _documentIndexer.IndexDocumentAsync(
                filePath,
                content,
                _sessionContext.TenantKey!,
                cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Document indexing failed for {FilePath}: {Error}",
                    filePath,
                    result.Error);

                return ToolResponse<IndexDocumentResult>.Fail(
                    ToolErrors.IndexingFailed(filePath, result.Error ?? "Unknown error"));
            }

            _logger.LogInformation(
                "Document indexed successfully: {FilePath} with {ChunkCount} chunks",
                filePath,
                result.ChunkCount);

            return ToolResponse<IndexDocumentResult>.Ok(new IndexDocumentResult
            {
                FilePath = filePath,
                DocumentId = result.Document!.Id,
                Title = result.Document.Title,
                DocType = result.Document.DocType,
                ChunkCount = result.ChunkCount,
                Warnings = result.Warnings.ToList(),
                Message = $"Document indexed successfully with {result.ChunkCount} chunks"
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Document indexing cancelled for {FilePath}", filePath);
            return ToolResponse<IndexDocumentResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during document indexing: {FilePath}", filePath);
            return ToolResponse<IndexDocumentResult>.Fail(
                ToolErrors.UnexpectedError(ex.Message));
        }
    }
}

/// <summary>
/// Result data for document indexing.
/// </summary>
public sealed class IndexDocumentResult
{
    /// <summary>
    /// The file path of the indexed document.
    /// </summary>
    [JsonPropertyName("file_path")]
    public required string FilePath { get; init; }

    /// <summary>
    /// The unique document ID.
    /// </summary>
    [JsonPropertyName("document_id")]
    public required string DocumentId { get; init; }

    /// <summary>
    /// The document title.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// The document type.
    /// </summary>
    [JsonPropertyName("doc_type")]
    public required string DocType { get; init; }

    /// <summary>
    /// Number of chunks created.
    /// </summary>
    [JsonPropertyName("chunk_count")]
    public required int ChunkCount { get; init; }

    /// <summary>
    /// Any warnings generated during indexing.
    /// </summary>
    [JsonPropertyName("warnings")]
    public required List<string> Warnings { get; init; }

    /// <summary>
    /// Human-readable success message.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}
