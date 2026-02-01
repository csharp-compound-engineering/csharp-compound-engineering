using System.ComponentModel;
using System.Text;
using System.Text.Json.Serialization;
using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.SemanticKernel;
using CompoundDocs.McpServer.Session;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// MCP tool for RAG-based Q&amp;A with source attribution.
/// Uses Semantic Kernel for RAG synthesis.
/// </summary>
[McpServerToolType]
public sealed class RagQueryTool
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISessionContext _sessionContext;
    private readonly ILogger<RagQueryTool> _logger;

    /// <summary>
    /// Creates a new instance of RagQueryTool.
    /// </summary>
    public RagQueryTool(
        IDocumentRepository documentRepository,
        IEmbeddingService embeddingService,
        ISessionContext sessionContext,
        ILogger<RagQueryTool> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Perform RAG-based Q&amp;A with source attribution.
    /// </summary>
    /// <param name="query">The question or query to answer.</param>
    /// <param name="maxResults">Maximum number of source documents to use (default: 5).</param>
    /// <param name="includeChunks">Whether to include individual chunks in the response (default: false).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The RAG response with sources.</returns>
    [McpServerTool(Name = "rag_query")]
    [Description("Perform RAG-based question answering with source attribution. Retrieves relevant documentation and synthesizes an answer.")]
    public async Task<ToolResponse<RagQueryResult>> QueryAsync(
        [Description("The question or query to answer")] string query,
        [Description("Maximum number of source documents to use (default: 5)")] int maxResults = 5,
        [Description("Whether to include individual chunks in the response (default: false)")] bool includeChunks = false,
        CancellationToken cancellationToken = default)
    {
        if (!_sessionContext.IsProjectActive)
        {
            return ToolResponse<RagQueryResult>.Fail(ToolErrors.NoActiveProject);
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolResponse<RagQueryResult>.Fail(ToolErrors.EmptyQuery);
        }

        if (maxResults <= 0)
        {
            maxResults = 5;
        }
        else if (maxResults > 20)
        {
            maxResults = 20;
        }

        _logger.LogInformation(
            "RAG query: '{Query}', maxResults={MaxResults}, includeChunks={IncludeChunks}",
            query,
            maxResults,
            includeChunks);

        try
        {
            // Generate query embedding
            var embedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

            // Search for relevant chunks
            var chunkResults = await _documentRepository.SearchChunksAsync(
                embedding,
                _sessionContext.TenantKey!,
                limit: maxResults * 2, // Get more chunks initially for diversity
                minRelevance: 0.0f,
                cancellationToken: cancellationToken);

            if (chunkResults.Count == 0)
            {
                return ToolResponse<RagQueryResult>.Ok(new RagQueryResult
                {
                    Query = query,
                    Answer = "No relevant documentation found for your query. Try rephrasing or using different terms.",
                    Sources = [],
                    Chunks = [],
                    ConfidenceScore = 0.0f
                });
            }

            // Deduplicate by document and select top chunks
            var documentChunks = chunkResults
                .GroupBy(c => c.Chunk.DocumentId)
                .Take(maxResults)
                .ToList();

            // Get parent documents for source attribution
            var sources = new List<RagSource>();
            var chunks = new List<RagChunk>();

            foreach (var docGroup in documentChunks)
            {
                var topChunk = docGroup.OrderByDescending(c => ApplyPromotionBoost(c.RelevanceScore, c.Chunk.PromotionLevel)).First();
                var document = await _documentRepository.GetByIdAsync(topChunk.Chunk.DocumentId, cancellationToken);

                if (document != null)
                {
                    sources.Add(new RagSource
                    {
                        FilePath = document.FilePath,
                        Title = document.Title,
                        DocType = document.DocType,
                        RelevanceScore = topChunk.RelevanceScore
                    });
                }

                if (includeChunks)
                {
                    foreach (var chunkResult in docGroup.Take(3)) // Max 3 chunks per document
                    {
                        chunks.Add(new RagChunk
                        {
                            DocumentId = chunkResult.Chunk.DocumentId,
                            HeaderPath = chunkResult.Chunk.HeaderPath,
                            Content = chunkResult.Chunk.Content,
                            RelevanceScore = chunkResult.RelevanceScore,
                            StartLine = chunkResult.Chunk.StartLine,
                            EndLine = chunkResult.Chunk.EndLine
                        });
                    }
                }
            }

            // Build context for RAG synthesis
            var context = BuildContext(chunkResults.Take(maxResults * 2).ToList());

            // Synthesize answer using the retrieved context
            // Note: In a full implementation, this would use Semantic Kernel's chat completion
            // For now, we return the context with source attribution
            var answer = SynthesizeAnswer(query, context, sources);

            // Calculate confidence based on relevance scores
            var avgRelevance = chunkResults.Take(maxResults).Average(c => c.RelevanceScore);
            var confidenceScore = Math.Min(1.0f, avgRelevance * 1.2f); // Scale up slightly

            _logger.LogInformation(
                "RAG query completed: {SourceCount} sources, confidence={Confidence:F2}",
                sources.Count,
                confidenceScore);

            return ToolResponse<RagQueryResult>.Ok(new RagQueryResult
            {
                Query = query,
                Answer = answer,
                Sources = sources,
                Chunks = includeChunks ? chunks : [],
                ConfidenceScore = confidenceScore
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("RAG query cancelled");
            return ToolResponse<RagQueryResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during RAG query");
            return ToolResponse<RagQueryResult>.Fail(
                ToolErrors.RagSynthesisFailed(ex.Message));
        }
    }

    private static float ApplyPromotionBoost(float score, string promotionLevel)
    {
        return score * PromotionLevels.GetBoostFactor(promotionLevel);
    }

    private static string BuildContext(IReadOnlyList<ChunkSearchResult> chunks)
    {
        var sb = new StringBuilder();

        foreach (var chunk in chunks.OrderByDescending(c => c.RelevanceScore))
        {
            sb.AppendLine($"--- [{chunk.Chunk.HeaderPath}] ---");
            sb.AppendLine(chunk.Chunk.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string SynthesizeAnswer(string query, string context, List<RagSource> sources)
    {
        // In a full implementation, this would use Semantic Kernel's chat completion
        // to synthesize an answer from the context.
        // For now, we provide the context with source attribution.

        var sb = new StringBuilder();
        sb.AppendLine("Based on the project documentation, here is the relevant information:");
        sb.AppendLine();
        sb.AppendLine(context);
        sb.AppendLine();
        sb.AppendLine("Sources:");
        foreach (var source in sources)
        {
            sb.AppendLine($"- [{source.Title}]({source.FilePath}) (relevance: {source.RelevanceScore:F2})");
        }

        return sb.ToString();
    }
}

/// <summary>
/// Result data for RAG query.
/// </summary>
public sealed class RagQueryResult
{
    /// <summary>
    /// The original query.
    /// </summary>
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    /// <summary>
    /// The synthesized answer.
    /// </summary>
    [JsonPropertyName("answer")]
    public required string Answer { get; init; }

    /// <summary>
    /// Source documents used for the answer.
    /// </summary>
    [JsonPropertyName("sources")]
    public required List<RagSource> Sources { get; init; }

    /// <summary>
    /// Individual chunks if requested.
    /// </summary>
    [JsonPropertyName("chunks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RagChunk>? Chunks { get; init; }

    /// <summary>
    /// Confidence score for the answer (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("confidence_score")]
    public required float ConfidenceScore { get; init; }
}

/// <summary>
/// A source document for RAG attribution.
/// </summary>
public sealed class RagSource
{
    /// <summary>
    /// The file path of the source document.
    /// </summary>
    [JsonPropertyName("file_path")]
    public required string FilePath { get; init; }

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
    /// The relevance score.
    /// </summary>
    [JsonPropertyName("relevance_score")]
    public required float RelevanceScore { get; init; }
}

/// <summary>
/// A chunk used in RAG response.
/// </summary>
public sealed class RagChunk
{
    /// <summary>
    /// The parent document ID.
    /// </summary>
    [JsonPropertyName("document_id")]
    public required string DocumentId { get; init; }

    /// <summary>
    /// The header path within the document.
    /// </summary>
    [JsonPropertyName("header_path")]
    public required string HeaderPath { get; init; }

    /// <summary>
    /// The chunk content.
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>
    /// The relevance score.
    /// </summary>
    [JsonPropertyName("relevance_score")]
    public required float RelevanceScore { get; init; }

    /// <summary>
    /// Starting line number in the source document.
    /// </summary>
    [JsonPropertyName("start_line")]
    public required int StartLine { get; init; }

    /// <summary>
    /// Ending line number in the source document.
    /// </summary>
    [JsonPropertyName("end_line")]
    public required int EndLine { get; init; }
}
