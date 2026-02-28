using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Serialization;
using CompoundDocs.Bedrock;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using CompoundDocs.McpServer.Observability;
using CompoundDocs.Vector;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// MCP tool for RAG-based Q&amp;A with source attribution.
/// Uses GraphRAG pipeline: embed → search OpenSearch → enrich with graph → synthesize via Bedrock.
/// </summary>
[McpServerToolType]
public sealed partial class RagQueryTool
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "RAG query: '{Query}', maxResults={MaxResults}")]
    private partial void LogRagQueryStarted(string query, int maxResults);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "RAG query completed: {SourceCount} sources, confidence={Confidence:F2}")]
    private partial void LogRagQueryCompleted(int sourceCount, double confidence);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "RAG query cancelled")]
    private partial void LogRagQueryCancelled();

    [LoggerMessage(EventId = 4, Level = LogLevel.Error,
        Message = "Unexpected error during RAG query")]
    private partial void LogRagQueryError(Exception exception);

    private readonly IVectorStore _vectorStore;
    private readonly IGraphRepository _graphRepository;
    private readonly IBedrockEmbeddingService _embeddingService;
    private readonly IBedrockLlmService _llmService;
    private readonly IGraphRagPipeline _graphRagPipeline;
    private readonly IMetricsCollector _metrics;
    private readonly ILogger<RagQueryTool> _logger;

    public RagQueryTool(
        IVectorStore vectorStore,
        IGraphRepository graphRepository,
        IBedrockEmbeddingService embeddingService,
        IBedrockLlmService llmService,
        IGraphRagPipeline graphRagPipeline,
        IMetricsCollector metrics,
        ILogger<RagQueryTool> logger)
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _graphRepository = graphRepository ?? throw new ArgumentNullException(nameof(graphRepository));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _graphRagPipeline = graphRagPipeline ?? throw new ArgumentNullException(nameof(graphRagPipeline));
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Perform RAG-based question answering with source attribution.
    /// </summary>
    [McpServerTool(Name = "rag_query")]
    [Description("Perform RAG-based question answering with source attribution. Retrieves relevant documentation and synthesizes an answer.")]
    public async Task<ToolResponse<RagQueryResult>> QueryAsync(
        [Description("The question or query to answer")] string query,
        [Description("Maximum number of source documents to use (default: 5)")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolResponse<RagQueryResult>.Fail(ToolErrors.EmptyQuery);
        }

        if (maxResults <= 0)
            maxResults = 5;
        else if (maxResults > 20)
            maxResults = 20;

        LogRagQueryStarted(query, maxResults);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await _graphRagPipeline.QueryAsync(
                query,
                new GraphRagOptions { MaxChunks = maxResults },
                cancellationToken);

            var sources = result.Sources.Select(s => new RagSource
            {
                DocumentId = s.DocumentId,
                ChunkId = s.ChunkId,
                FilePath = s.FilePath,
                RelevanceScore = (float)s.RelevanceScore
            }).ToList();

            stopwatch.Stop();
            _metrics.RecordQuery(stopwatch.Elapsed.TotalMilliseconds, sources.Count);

            LogRagQueryCompleted(sources.Count, result.Confidence);

            return ToolResponse<RagQueryResult>.Ok(new RagQueryResult
            {
                Query = query,
                Answer = result.Answer,
                Sources = sources,
                RelatedConcepts = result.RelatedConcepts,
                ConfidenceScore = (float)result.Confidence
            });
        }
        catch (OperationCanceledException)
        {
            LogRagQueryCancelled();
            _metrics.RecordError("OperationCancelled");
            return ToolResponse<RagQueryResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            LogRagQueryError(ex);
            _metrics.RecordError(ex.GetType().Name);
            return ToolResponse<RagQueryResult>.Fail(ToolErrors.RagSynthesisFailed(ex.Message));
        }
    }
}

/// <summary>
/// Result data for RAG query.
/// </summary>
public sealed class RagQueryResult
{
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    [JsonPropertyName("answer")]
    public required string Answer { get; init; }

    [JsonPropertyName("sources")]
    public required List<RagSource> Sources { get; init; }

    [JsonPropertyName("related_concepts")]
    public List<string> RelatedConcepts { get; init; } = [];

    [JsonPropertyName("confidence_score")]
    public required float ConfidenceScore { get; init; }
}

/// <summary>
/// A source document for RAG attribution.
/// </summary>
public sealed class RagSource
{
    [JsonPropertyName("document_id")]
    public required string DocumentId { get; init; }

    [JsonPropertyName("chunk_id")]
    public required string ChunkId { get; init; }

    [JsonPropertyName("file_path")]
    public required string FilePath { get; init; }

    [JsonPropertyName("relevance_score")]
    public required float RelevanceScore { get; init; }
}
