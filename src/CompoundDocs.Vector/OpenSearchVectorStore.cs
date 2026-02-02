using System.Net.Http.Json;
using System.Text.Json;
using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace CompoundDocs.Vector;

public sealed class OpenSearchVectorStore : IVectorStore
{
    private readonly HttpClient _httpClient;
    private readonly OpenSearchConfig _config;
    private readonly ILogger<OpenSearchVectorStore> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public OpenSearchVectorStore(
        HttpClient httpClient,
        IOptions<OpenSearchConfig> options,
        ILogger<OpenSearchVectorStore> logger)
    {
        _httpClient = httpClient;
        _config = options.Value;
        _logger = logger;

        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex =>
                    ex is HttpRequestException ||
                    ex is TaskCanceledException)
            })
            .Build();
    }

    internal OpenSearchVectorStore(
        HttpClient httpClient,
        OpenSearchConfig config,
        ILogger<OpenSearchVectorStore> logger,
        ResiliencePipeline? retryPipeline = null)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _retryPipeline = retryPipeline ?? ResiliencePipeline.Empty;
    }

    public async Task IndexAsync(
        string chunkId,
        float[] embedding,
        Dictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        await _retryPipeline.ExecuteAsync(async token =>
        {
            _logger.LogDebug("Indexing chunk {ChunkId}", chunkId);

            var document = new Dictionary<string, object>
            {
                ["chunk_id"] = chunkId,
                ["embedding"] = embedding,
                ["metadata"] = metadata
            };

            var url = $"{_config.CollectionEndpoint}/{_config.IndexName}/_doc/{chunkId}";
            var response = await _httpClient.PutAsJsonAsync(url, document, token);
            response.EnsureSuccessStatusCode();
        }, ct);
    }

    public async Task DeleteByDocumentIdAsync(
        string documentId,
        CancellationToken ct = default)
    {
        await _retryPipeline.ExecuteAsync(async token =>
        {
            _logger.LogDebug("Deleting vectors for document {DocumentId}", documentId);

            var query = new
            {
                query = new
                {
                    term = new Dictionary<string, object>
                    {
                        ["metadata.document_id"] = documentId
                    }
                }
            };

            var url = $"{_config.CollectionEndpoint}/{_config.IndexName}/_delete_by_query";
            var response = await _httpClient.PostAsJsonAsync(url, query, token);
            response.EnsureSuccessStatusCode();
        }, ct);
    }

    public async Task<List<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding,
        int topK = 10,
        Dictionary<string, string>? filters = null,
        CancellationToken ct = default)
    {
        return await _retryPipeline.ExecuteAsync(async token =>
        {
            _logger.LogDebug("Searching vectors, topK={TopK}", topK);

            var knnQuery = new Dictionary<string, object>
            {
                ["vector"] = queryEmbedding,
                ["k"] = topK
            };

            if (filters is { Count: > 0 })
            {
                var filterTerms = filters.Select(f => new Dictionary<string, object>
                {
                    ["term"] = new Dictionary<string, object>
                    {
                        [$"metadata.{f.Key}"] = f.Value
                    }
                }).ToList();

                knnQuery["filter"] = new { @bool = new { must = filterTerms } };
            }

            var searchBody = new
            {
                size = topK,
                query = new { knn = new { embedding = knnQuery } }
            };

            var url = $"{_config.CollectionEndpoint}/{_config.IndexName}/_search";
            var response = await _httpClient.PostAsJsonAsync(url, searchBody, token);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(token);
            using var doc = JsonDocument.Parse(content);

            var results = new List<VectorSearchResult>();
            var hits = doc.RootElement.GetProperty("hits").GetProperty("hits");

            foreach (var hit in hits.EnumerateArray())
            {
                var score = hit.GetProperty("_score").GetDouble();
                var source = hit.GetProperty("_source");
                var chunkId = source.GetProperty("chunk_id").GetString()!;
                var metadata = new Dictionary<string, string>();

                if (source.TryGetProperty("metadata", out var metaElement))
                {
                    foreach (var prop in metaElement.EnumerateObject())
                    {
                        metadata[prop.Name] = prop.Value.GetString() ?? string.Empty;
                    }
                }

                results.Add(new VectorSearchResult
                {
                    ChunkId = chunkId,
                    Score = score,
                    Metadata = metadata
                });
            }

            return results;
        }, ct);
    }

    public async Task BatchIndexAsync(
        IEnumerable<VectorDocument> documents,
        CancellationToken ct = default)
    {
        foreach (var doc in documents)
        {
            await IndexAsync(doc.ChunkId, doc.Embedding, doc.Metadata, ct);
        }
    }
}
