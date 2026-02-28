using System.Text.Json;
using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenSearch.Client;
using OpenSearch.Net;
using Polly;
using Polly.Retry;

namespace CompoundDocs.Vector;

public sealed partial class OpenSearchVectorStore : IVectorStore
{
    [LoggerMessage(EventId = 1, Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Indexing chunk {ChunkId}")]
    private partial void LogIndexingChunk(string chunkId);

    [LoggerMessage(EventId = 2, Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Deleting vectors for document {DocumentId}")]
    private partial void LogDeletingVectors(string documentId);

    [LoggerMessage(EventId = 3, Level = Microsoft.Extensions.Logging.LogLevel.Debug,
        Message = "Searching vectors, topK={TopK}")]
    private partial void LogSearchingVectors(int topK);

    private readonly IOpenSearchClient _client;
    private readonly OpenSearchConfig _config;
    private readonly ILogger<OpenSearchVectorStore> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public OpenSearchVectorStore(
        IOpenSearchClient client,
        IOptions<OpenSearchConfig> options,
        ILogger<OpenSearchVectorStore> logger)
    {
        _client = client;
        _config = options.Value;
        _logger = logger;

        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex =>
                    ex is OpenSearchClientException ||
                    ex is HttpRequestException ||
                    ex is TaskCanceledException)
            })
            .Build();
    }

    internal OpenSearchVectorStore(
        IOpenSearchClient client,
        OpenSearchConfig config,
        ILogger<OpenSearchVectorStore> logger,
        ResiliencePipeline? retryPipeline = null)
    {
        _client = client;
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
            LogIndexingChunk(chunkId);

            var document = new Dictionary<string, object>
            {
                ["chunk_id"] = chunkId,
                ["embedding"] = embedding,
                ["metadata"] = metadata
            };

            var response = await _client.LowLevel.IndexAsync<StringResponse>(
                _config.IndexName, chunkId, PostData.Serializable(document), ctx: token);

            if (!response.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to index chunk {chunkId}: {response.Body}");
            }
        }, ct);
    }

    public async Task DeleteByDocumentIdAsync(
        string documentId,
        CancellationToken ct = default)
    {
        await _retryPipeline.ExecuteAsync(async token =>
        {
            LogDeletingVectors(documentId);

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

            var response = await _client.LowLevel.DeleteByQueryAsync<StringResponse>(
                _config.IndexName, PostData.Serializable(query), ctx: token);

            if (!response.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to delete vectors for document {documentId}: {response.Body}");
            }
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
            LogSearchingVectors(topK);

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

            var response = await _client.LowLevel.SearchAsync<StringResponse>(
                _config.IndexName, PostData.Serializable(searchBody), ctx: token);

            if (!response.Success)
            {
                throw new InvalidOperationException(
                    $"Vector search failed: {response.Body}");
            }

            using var doc = JsonDocument.Parse(response.Body);

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
