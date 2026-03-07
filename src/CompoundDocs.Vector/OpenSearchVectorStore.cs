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

    private readonly IOpenSearchClientFactory _clientFactory;
    private readonly Func<OpenSearchConfig> _configAccessor;
    private readonly ILogger<OpenSearchVectorStore> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public OpenSearchVectorStore(
        IOpenSearchClientFactory clientFactory,
        IOptionsMonitor<OpenSearchConfig> options,
        ILogger<OpenSearchVectorStore> logger)
    {
        _clientFactory = clientFactory;
        _configAccessor = () => options.CurrentValue;
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
        IOpenSearchClientFactory clientFactory,
        OpenSearchConfig config,
        ILogger<OpenSearchVectorStore> logger,
        ResiliencePipeline? retryPipeline = null)
    {
        _clientFactory = clientFactory;
        _configAccessor = () => config;
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

            var document = new OpenSearchChunkDocument
            {
                ChunkId = chunkId, Embedding = embedding, Metadata = metadata
            };

            var response = await _clientFactory.GetClient().IndexAsync(document, i => i
                .Index(_configAccessor().IndexName)
                .Id(chunkId), token);

            if (!response.IsValid)
                throw new InvalidOperationException($"Failed to index chunk {chunkId}: {response.DebugInformation}");
        }, ct);
    }

    public async Task DeleteByDocumentIdAsync(
        string documentId,
        CancellationToken ct = default)
    {
        await _retryPipeline.ExecuteAsync(async token =>
        {
            LogDeletingVectors(documentId);

            var response = await _clientFactory.GetClient().DeleteByQueryAsync<OpenSearchChunkDocument>(d => d
                .Index(_configAccessor().IndexName)
                .Query(q => q
                    .Term(t => t.Field("metadata.document_id").Value(documentId))), token);

            if (!response.IsValid)
                throw new InvalidOperationException($"Failed to delete vectors for document {documentId}: {response.DebugInformation}");
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

            var response = await _clientFactory.GetClient().SearchAsync<OpenSearchChunkDocument>(s =>
            {
                s.Index(_configAccessor().IndexName)
                 .Size(topK)
                 .Query(q => q
                     .Knn(knn =>
                     {
                         knn.Field(f => f.Embedding)
                            .Vector(queryEmbedding)
                            .K(topK);

                         if (filters is { Count: > 0 })
                         {
                             knn.Filter(f => f
                                 .Bool(b => b
                                     .Must(filters.Select(filter =>
                                         (Func<QueryContainerDescriptor<OpenSearchChunkDocument>, QueryContainer>)
                                         (qc => qc.Term(t => t
                                             .Field($"metadata.{filter.Key}")
                                             .Value(filter.Value)))
                                     ).ToArray())));
                         }

                         return knn;
                     }));
                return s;
            }, token);

            if (!response.IsValid)
                throw new InvalidOperationException($"Vector search failed: {response.DebugInformation}");

            return response.Hits.Select(hit => new VectorSearchResult
            {
                ChunkId = hit.Source.ChunkId,
                Score = hit.Score ?? 0.0,
                Metadata = hit.Source.Metadata ?? []
            }).ToList();
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
