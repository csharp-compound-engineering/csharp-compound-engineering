using CompoundDocs.Bedrock;
using CompoundDocs.Common.Configuration;
using CompoundDocs.Common.Models;
using CompoundDocs.Graph;
using CompoundDocs.Vector;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.GraphRag;

internal sealed partial class GraphRagPipeline : IGraphRagPipeline
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Starting GraphRAG query pipeline: maxChunks={MaxChunks}, minScore={MinScore:F2}")]
    private partial void LogPipelineStarted(int maxChunks, double minScore);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Vector search returned {Count} results, {FilteredCount} after score filter")]
    private partial void LogVectorSearchResults(int count, int filteredCount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Pipeline complete: {SourceCount} sources, {ConceptCount} concepts, confidence={Confidence:F2}")]
    private partial void LogPipelineComplete(int sourceCount, int conceptCount, double confidence);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "Graph enrichment failed (concepts): {ErrorMessage}")]
    private partial void LogConceptEnrichmentFailed(string errorMessage);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning,
        Message = "Graph enrichment failed (linked documents for {DocumentId}): {ErrorMessage}")]
    private partial void LogLinkedDocEnrichmentFailed(string documentId, string errorMessage);

    [LoggerMessage(EventId = 6, Level = LogLevel.Warning,
        Message = "Cross-repo entity resolution failed for concept {ConceptName}: {ErrorMessage}")]
    private partial void LogCrossRepoResolutionFailed(string conceptName, string errorMessage);

    private readonly IBedrockEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly IGraphRepository _graphRepository;
    private readonly IBedrockLlmService _llmService;
    private readonly ICrossRepoEntityResolver _crossRepoEntityResolver;
    private readonly GraphRagConfig _config;
    private readonly ILogger<GraphRagPipeline> _logger;

    public GraphRagPipeline(
        IBedrockEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IGraphRepository graphRepository,
        IBedrockLlmService llmService,
        ICrossRepoEntityResolver crossRepoEntityResolver,
        IOptions<CompoundDocsCloudConfig> options,
        ILogger<GraphRagPipeline> logger)
    {
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _graphRepository = graphRepository;
        _llmService = llmService;
        _crossRepoEntityResolver = crossRepoEntityResolver;
        _config = options.Value.GraphRag;
        _logger = logger;
    }

    public async Task<GraphRagResult> QueryAsync(
        string query,
        GraphRagOptions? options = null,
        CancellationToken ct = default)
    {
        var maxChunks = options?.MaxChunks ?? _config.MaxChunksPerQuery;
        var minScore = options?.MinRelevanceScore ?? _config.MinRelevanceScore;
        var useCrossRepoLinks = options?.UseCrossRepoLinks ?? _config.UseCrossRepoLinks;

        LogPipelineStarted(maxChunks, minScore);

        // 1. Embed query
        var embedding = await _embeddingService.GenerateEmbeddingAsync(query, ct);

        // 2. Vector search
        var filters = BuildFilters(options);
        var vectorResults = await _vectorStore.SearchAsync(embedding, maxChunks, filters, ct);

        // 3. Filter by min relevance score
        var filtered = vectorResults.Where(r => r.Score >= minScore).ToList();

        LogVectorSearchResults(vectorResults.Count, filtered.Count);

        // 4. Early return if no results
        if (filtered.Count == 0)
        {
            return new GraphRagResult
            {
                Answer = "No relevant documents found for your query.",
                Confidence = 0
            };
        }

        // 5. Retrieve chunks
        var chunkIds = filtered.Select(r => r.ChunkId).ToList();
        var chunks = await _graphRepository.GetChunksByIdsAsync(chunkIds, ct);

        // 6. Graph enrichment (best-effort)
        var relatedConcepts = new List<string>();
        try
        {
            var concepts = await _graphRepository.GetConceptsByChunkIdsAsync(chunkIds, ct);
            relatedConcepts = concepts.Select(c => c.Name).ToList();
        }
        catch (Exception ex)
        {
            LogConceptEnrichmentFailed(ex.Message);
        }

        if (useCrossRepoLinks)
        {
            var documentIds = filtered
                .Select(r => r.Metadata.GetValueOrDefault("document_id"))
                .Where(id => id is not null)
                .Distinct()
                .ToList();

            foreach (var docId in documentIds)
            {
                try
                {
                    await _graphRepository.GetLinkedDocumentsAsync(docId!, ct);
                }
                catch (Exception ex)
                {
                    LogLinkedDocEnrichmentFailed(docId!, ex.Message);
                }
            }

            // Cross-repo concept resolution
            var currentRepos = filtered
                .Select(r => r.Metadata.GetValueOrDefault("repository"))
                .Where(r => r is not null)
                .Distinct()
                .ToHashSet();

            foreach (var conceptName in relatedConcepts.ToList())
            {
                try
                {
                    var resolved = await _crossRepoEntityResolver.ResolveAsync(conceptName, ct);
                    if (resolved is not null &&
                        !string.IsNullOrEmpty(resolved.Repository) &&
                        !currentRepos.Contains(resolved.Repository))
                    {
                        foreach (var name in resolved.RelatedConceptNames)
                        {
                            if (!relatedConcepts.Contains(name))
                            {
                                relatedConcepts.Add(name);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogCrossRepoResolutionFailed(conceptName, ex.Message);
                }
            }
        }

        // 7. Build LLM prompt and synthesize
        var systemPrompt = BuildSystemPrompt();
        var userMessage = FormatChunkContext(chunks, filtered);

        var answer = await _llmService.GenerateAsync(
            systemPrompt,
            [new BedrockMessage("user", $"{query}\n\n{userMessage}")],
            ModelTier.Sonnet,
            ct);

        // 8. Compute confidence and build result
        var confidence = ComputeConfidence(
            filtered.Select(r => r.Score).ToList(),
            maxChunks);

        var sources = filtered.Select(r => new GraphRagSource
        {
            DocumentId = r.Metadata.GetValueOrDefault("document_id") ?? r.ChunkId,
            ChunkId = r.ChunkId,
            Repository = r.Metadata.GetValueOrDefault("repository") ?? string.Empty,
            FilePath = r.Metadata.GetValueOrDefault("file_path") ?? string.Empty,
            RelevanceScore = r.Score
        }).ToList();

        LogPipelineComplete(sources.Count, relatedConcepts.Count, confidence);

        return new GraphRagResult
        {
            Answer = answer,
            Sources = sources,
            RelatedConcepts = relatedConcepts,
            Confidence = confidence
        };
    }

    internal static string BuildSystemPrompt() =>
        """
        You are a knowledgeable documentation assistant. Answer the user's question based on the provided context chunks.

        Guidelines:
        - Base your answer ONLY on the provided context. Do not make up information.
        - If the context doesn't contain enough information, say so clearly.
        - Reference specific sources when possible.
        - Be concise but thorough.
        - Use code examples from the context when relevant.
        """;

    internal static string FormatChunkContext(
        List<ChunkNode> chunks,
        List<VectorSearchResult> vectorResults)
    {
        var scoreMap = vectorResults.ToDictionary(r => r.ChunkId, r => r.Score);
        var lines = new List<string> { "## Context" };

        foreach (var chunk in chunks)
        {
            var score = scoreMap.GetValueOrDefault(chunk.Id);
            var filePath = string.Empty;
            var matchingResult = vectorResults.FirstOrDefault(r => r.ChunkId == chunk.Id);
            if (matchingResult is not null)
            {
                filePath = matchingResult.Metadata.GetValueOrDefault("file_path") ?? string.Empty;
            }

            lines.Add($"### Source: {filePath} (relevance: {score:F2})");
            lines.Add(chunk.Content);
            lines.Add(string.Empty);
        }

        return string.Join('\n', lines);
    }

    internal static double ComputeConfidence(List<double> scores, int requestedCount)
    {
        if (scores.Count == 0)
        {
            return 0;
        }

        var averageScore = scores.Average();
        var coverageRatio = Math.Min(1.0, (double)scores.Count / requestedCount);
        return averageScore * coverageRatio;
    }

    private static Dictionary<string, string>? BuildFilters(GraphRagOptions? options)
    {
        if (options is null)
        {
            return null;
        }

        var filters = new Dictionary<string, string>();

        if (options.RepositoryFilter is not null)
        {
            filters["repository"] = options.RepositoryFilter;
        }

        if (options.DocTypeFilter is not null)
        {
            filters["doc_type"] = options.DocTypeFilter;
        }

        return filters.Count > 0 ? filters : null;
    }
}
