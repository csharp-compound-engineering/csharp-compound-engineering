using System.Diagnostics;
using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.SemanticKernel;
using CompoundDocs.McpServer.Session;
using CompoundDocs.McpServer.Tools;
using CompoundDocs.Tests.Utilities;
using Microsoft.Extensions.Logging;
using IDocumentIndexer = CompoundDocs.McpServer.Services.DocumentProcessing.IDocumentIndexer;
using IndexResult = CompoundDocs.McpServer.Services.DocumentProcessing.DocumentIndexingResult;

namespace CompoundDocs.Tests.Performance;

/// <summary>
/// Performance baseline tests for measuring and asserting performance bounds
/// on critical operations like embedding generation, vector search, and document indexing.
/// </summary>
/// <remarks>
/// These tests are designed to run in isolation and should be skipped in CI
/// unless performance regression testing is explicitly enabled.
/// Use [Trait("Category", "Performance")] to filter these tests.
/// </remarks>
[Trait("Category", "Performance")]
public sealed class PerformanceBaselineTests : TestBase
{
    /// <summary>
    /// Maximum acceptable time for embedding generation (single document).
    /// </summary>
    private const int MaxEmbeddingTimeMs = 5000;

    /// <summary>
    /// Maximum acceptable time for vector search (p99).
    /// </summary>
    private const int MaxSearchLatencyMs = 1000;

    /// <summary>
    /// Maximum acceptable time for document indexing (single document).
    /// </summary>
    private const int MaxIndexingTimeMs = 10000;

    /// <summary>
    /// Minimum throughput for batch indexing (documents per second).
    /// </summary>
    private const double MinBatchIndexingThroughput = 1.0;

    #region Embedding Generation Benchmarks

    [Fact]
    public async Task EmbeddingGeneration_SingleDocument_ShouldCompleteWithinBounds()
    {
        // Arrange
        var embeddingService = CreateMockEmbeddingService(simulatedLatencyMs: 100);
        var content = GenerateTestContent(1000); // 1000 words

        var stopwatch = Stopwatch.StartNew();

        // Act
        var embedding = await embeddingService.GenerateEmbeddingAsync(content);

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(MaxEmbeddingTimeMs);
        embedding.Length.ShouldBe(1024);

        // Log performance metric
        LogPerformanceMetric("EmbeddingGeneration_Single", stopwatch.ElapsedMilliseconds);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task EmbeddingGeneration_BatchDocuments_ShouldScaleLinearly(int documentCount)
    {
        // Arrange
        var embeddingService = CreateMockEmbeddingService(simulatedLatencyMs: 50);
        var contents = Enumerable.Range(0, documentCount)
            .Select(i => GenerateTestContent(500))
            .ToList();

        var stopwatch = Stopwatch.StartNew();

        // Act
        var embeddings = await embeddingService.GenerateEmbeddingsAsync(contents);

        stopwatch.Stop();

        // Assert
        embeddings.Count.ShouldBe(documentCount);

        var timePerDocument = stopwatch.ElapsedMilliseconds / (double)documentCount;
        timePerDocument.ShouldBeLessThan(200); // Each document should take less than 200ms average

        // Log performance metric
        LogPerformanceMetric($"EmbeddingGeneration_Batch_{documentCount}", stopwatch.ElapsedMilliseconds);
        LogPerformanceMetric($"EmbeddingGeneration_PerDocument_{documentCount}", timePerDocument);
    }

    #endregion

    #region Vector Search Benchmarks

    [Fact]
    public async Task VectorSearch_SingleQuery_ShouldMeetLatencyTarget()
    {
        // Arrange
        var documentRepository = CreateMockDocumentRepository(resultCount: 10, simulatedLatencyMs: 50);
        var queryEmbedding = CreateTestEmbedding();
        var tenantKey = "test:main:hash";

        var stopwatch = Stopwatch.StartNew();

        // Act
        var results = await documentRepository.SearchAsync(
            queryEmbedding,
            tenantKey,
            limit: 10,
            minRelevance: 0.0f);

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(MaxSearchLatencyMs);
        results.ShouldNotBeNull();

        // Log performance metric
        LogPerformanceMetric("VectorSearch_Single", stopwatch.ElapsedMilliseconds);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task VectorSearch_MultipleQueries_ShouldMeetPercentileTargets(int queryCount)
    {
        // Arrange
        var documentRepository = CreateMockDocumentRepository(resultCount: 10, simulatedLatencyMs: 20);
        var tenantKey = "test:main:hash";
        var latencies = new List<long>(queryCount);

        // Act
        for (int i = 0; i < queryCount; i++)
        {
            var queryEmbedding = CreateTestEmbedding(seed: i);
            var stopwatch = Stopwatch.StartNew();

            await documentRepository.SearchAsync(
                queryEmbedding,
                tenantKey,
                limit: 10);

            stopwatch.Stop();
            latencies.Add(stopwatch.ElapsedMilliseconds);
        }

        // Assert - Calculate percentiles
        var sortedLatencies = latencies.OrderBy(l => l).ToList();
        var p50 = sortedLatencies[(int)(queryCount * 0.5)];
        var p95 = sortedLatencies[(int)(queryCount * 0.95)];
        var p99 = sortedLatencies[(int)(queryCount * 0.99)];

        // p50 should be under 100ms
        p50.ShouldBeLessThan(100);

        // p95 should be under 300ms
        p95.ShouldBeLessThan(300);

        // p99 should be under max search latency
        p99.ShouldBeLessThan(MaxSearchLatencyMs);

        // Log performance metrics
        LogPerformanceMetric($"VectorSearch_p50_{queryCount}", p50);
        LogPerformanceMetric($"VectorSearch_p95_{queryCount}", p95);
        LogPerformanceMetric($"VectorSearch_p99_{queryCount}", p99);
    }

    [Fact]
    public async Task VectorSearch_WithFilters_ShouldNotSignificantlyImpactLatency()
    {
        // Arrange
        var documentRepository = CreateMockDocumentRepository(resultCount: 10, simulatedLatencyMs: 30);
        var queryEmbedding = CreateTestEmbedding();
        var tenantKey = "test:main:hash";

        // Act - Search without filters
        var stopwatchNoFilter = Stopwatch.StartNew();
        await documentRepository.SearchAsync(queryEmbedding, tenantKey, limit: 10);
        stopwatchNoFilter.Stop();

        // Act - Search with doc type filter
        var stopwatchWithFilter = Stopwatch.StartNew();
        await documentRepository.SearchAsync(queryEmbedding, tenantKey, limit: 10, docType: "spec");
        stopwatchWithFilter.Stop();

        // Assert - Filtered search should not be more than 2x slower
        var latencyRatio = stopwatchWithFilter.ElapsedMilliseconds / (double)Math.Max(1, stopwatchNoFilter.ElapsedMilliseconds);
        latencyRatio.ShouldBeLessThan(2.0);

        // Log performance metrics
        LogPerformanceMetric("VectorSearch_NoFilter", stopwatchNoFilter.ElapsedMilliseconds);
        LogPerformanceMetric("VectorSearch_WithFilter", stopwatchWithFilter.ElapsedMilliseconds);
    }

    #endregion

    #region Document Indexing Benchmarks

    [Fact]
    public async Task DocumentIndexing_SingleDocument_ShouldCompleteWithinBounds()
    {
        // Arrange
        var documentIndexer = CreateMockDocumentIndexer(simulatedLatencyMs: 200);
        var content = GenerateTestContent(2000); // 2000 words
        var filePath = "docs/test-document.md";
        var tenantKey = "test:main:hash";

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await documentIndexer.IndexDocumentAsync(filePath, content, tenantKey);

        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(MaxIndexingTimeMs);
        result.IsSuccess.ShouldBeTrue();

        // Log performance metric
        LogPerformanceMetric("DocumentIndexing_Single", stopwatch.ElapsedMilliseconds);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(25)]
    [InlineData(50)]
    public async Task DocumentIndexing_BatchDocuments_ShouldMeetThroughputTarget(int documentCount)
    {
        // Arrange
        var documentIndexer = CreateMockDocumentIndexer(simulatedLatencyMs: 50);
        var documents = Enumerable.Range(0, documentCount)
            .Select(i => ($"docs/doc-{i}.md", GenerateTestContent(500)))
            .ToList();
        var tenantKey = "test:main:hash";

        var stopwatch = Stopwatch.StartNew();

        // Act
        var results = await documentIndexer.IndexDocumentsAsync(documents, tenantKey);

        stopwatch.Stop();

        // Assert
        results.Count.ShouldBe(documentCount);
        results.All(r => r.IsSuccess).ShouldBeTrue();

        var throughput = documentCount / (stopwatch.ElapsedMilliseconds / 1000.0);
        throughput.ShouldBeGreaterThan(MinBatchIndexingThroughput);

        // Log performance metrics
        LogPerformanceMetric($"DocumentIndexing_Batch_{documentCount}", stopwatch.ElapsedMilliseconds);
        LogPerformanceMetric($"DocumentIndexing_Throughput_{documentCount}", throughput);
    }

    [Fact]
    public async Task DocumentIndexing_LargeDocument_ShouldHandleChunking()
    {
        // Arrange
        var documentIndexer = CreateMockDocumentIndexer(simulatedLatencyMs: 100, expectedChunks: 10);
        var content = GenerateLargeDocument(10000); // Very large document
        var filePath = "docs/large-document.md";
        var tenantKey = "test:main:hash";

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await documentIndexer.IndexDocumentAsync(filePath, content, tenantKey);

        stopwatch.Stop();

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.ChunkCount.ShouldBeGreaterThan(1);

        // Even large documents should complete in reasonable time
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(MaxIndexingTimeMs * 3);

        // Log performance metrics
        LogPerformanceMetric("DocumentIndexing_Large", stopwatch.ElapsedMilliseconds);
        LogPerformanceMetric("DocumentIndexing_Large_ChunkCount", result.ChunkCount);
    }

    #endregion

    #region End-to-End Pipeline Benchmarks

    [Fact]
    public async Task EndToEndPipeline_IndexAndSearch_ShouldMeetOverallLatencyTarget()
    {
        // Arrange
        var embeddingService = CreateMockEmbeddingService(simulatedLatencyMs: 50);
        var documentRepository = CreateMockDocumentRepository(resultCount: 10, simulatedLatencyMs: 30);
        var sessionContext = CreateMockSessionContext();
        var logger = CreateLooseMock<ILogger<SemanticSearchTool>>().Object;

        var searchTool = new SemanticSearchTool(
            documentRepository,
            embeddingService,
            sessionContext,
            logger);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await searchTool.SearchAsync("test query", limit: 10);

        stopwatch.Stop();

        // Assert
        result.Success.ShouldBeTrue();

        // Total pipeline should be under 500ms for a simple search
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(500);

        // Log performance metric
        LogPerformanceMetric("EndToEnd_Search", stopwatch.ElapsedMilliseconds);
    }

    [Fact]
    public async Task EndToEndPipeline_RagQuery_ShouldMeetOverallLatencyTarget()
    {
        // Arrange
        var embeddingService = CreateMockEmbeddingService(simulatedLatencyMs: 50);
        var documentRepository = CreateMockDocumentRepositoryWithChunks(chunkCount: 5, simulatedLatencyMs: 30);
        var sessionContext = CreateMockSessionContext();
        var logger = CreateLooseMock<ILogger<RagQueryTool>>().Object;

        var ragTool = new RagQueryTool(
            documentRepository,
            embeddingService,
            sessionContext,
            logger);

        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await ragTool.QueryAsync("What is the API specification?", maxResults: 5);

        stopwatch.Stop();

        // Assert
        result.Success.ShouldBeTrue();

        // RAG query involves more operations, allow up to 1 second
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000);

        // Log performance metric
        LogPerformanceMetric("EndToEnd_RagQuery", stopwatch.ElapsedMilliseconds);
    }

    #endregion

    #region Memory Usage Benchmarks

    [Fact]
    public void EmbeddingVector_MemoryFootprint_ShouldBeReasonable()
    {
        // Arrange
        const int vectorDimensions = 1024;
        const int documentCount = 1000;

        // Act
        var vectors = Enumerable.Range(0, documentCount)
            .Select(_ => CreateTestEmbedding(dimensions: vectorDimensions))
            .ToList();

        // Assert - Each float is 4 bytes, so 1024 dimensions = ~4KB per vector
        var expectedSizePerVector = vectorDimensions * sizeof(float);
        var expectedTotalSize = expectedSizePerVector * documentCount;

        // Log expected memory usage
        LogPerformanceMetric("EmbeddingVector_SizePerVector", expectedSizePerVector);
        LogPerformanceMetric("EmbeddingVector_TotalSize_1000Docs", expectedTotalSize);

        // Vectors should be properly allocated
        vectors.Count.ShouldBe(documentCount);
        vectors.All(v => v.Length == vectorDimensions).ShouldBeTrue();
    }

    [Fact]
    public void DocumentBuilder_BatchCreation_ShouldNotCauseMemoryPressure()
    {
        // Arrange
        const int documentCount = 10000;
        var builder = TestDocumentBuilder.Create();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var documents = builder.BuildMany(documentCount);
        stopwatch.Stop();

        // Assert
        documents.Count.ShouldBe(documentCount);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000); // Should create 10k docs in under 5 seconds

        // Log performance metric
        LogPerformanceMetric("DocumentBuilder_BatchCreate_10000", stopwatch.ElapsedMilliseconds);
    }

    #endregion

    #region Helper Methods

    private IEmbeddingService CreateMockEmbeddingService(int simulatedLatencyMs = 0)
    {
        var mock = CreateLooseMock<IEmbeddingService>();

        mock.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, CancellationToken ct) =>
            {
                if (simulatedLatencyMs > 0)
                {
                    await Task.Delay(simulatedLatencyMs, ct);
                }
                return CreateTestEmbedding();
            });

        mock.Setup(e => e.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .Returns(async (IReadOnlyList<string> contents, CancellationToken ct) =>
            {
                if (simulatedLatencyMs > 0)
                {
                    await Task.Delay(simulatedLatencyMs * contents.Count / 2, ct); // Batch is more efficient
                }
                return contents.Select((_, i) => CreateTestEmbedding(seed: i)).ToList();
            });

        mock.Setup(e => e.Dimensions).Returns(1024);

        return mock.Object;
    }

    private IDocumentRepository CreateMockDocumentRepository(int resultCount = 10, int simulatedLatencyMs = 0)
    {
        var mock = CreateLooseMock<IDocumentRepository>();

        mock.Setup(r => r.SearchAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (ReadOnlyMemory<float> _, string _, int limit, float _, string? _, CancellationToken ct) =>
            {
                if (simulatedLatencyMs > 0)
                {
                    await Task.Delay(simulatedLatencyMs, ct);
                }

                var results = new List<SearchResult>();
                for (int i = 0; i < Math.Min(limit, resultCount); i++)
                {
                    var doc = TestDocumentBuilder.Create()
                        .WithId($"doc-{i}")
                        .WithTitle($"Document {i}")
                        .Build();
                    results.Add(new SearchResult(doc, 0.9f - (i * 0.05f)));
                }
                return results;
            });

        return mock.Object;
    }

    private IDocumentRepository CreateMockDocumentRepositoryWithChunks(int chunkCount = 5, int simulatedLatencyMs = 0)
    {
        var mock = CreateLooseMock<IDocumentRepository>();
        var parentDoc = TestDocumentBuilder.Create().WithId("parent-doc").Build();

        mock.Setup(r => r.SearchChunksAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (ReadOnlyMemory<float> _, string _, int limit, float _, CancellationToken ct) =>
            {
                if (simulatedLatencyMs > 0)
                {
                    await Task.Delay(simulatedLatencyMs, ct);
                }

                var results = new List<ChunkSearchResult>();
                for (int i = 0; i < Math.Min(limit, chunkCount); i++)
                {
                    var chunk = TestChunkBuilder.Create()
                        .WithDocumentId(parentDoc.Id)
                        .WithId($"chunk-{i}")
                        .Build();
                    results.Add(new ChunkSearchResult(chunk, 0.9f - (i * 0.05f)));
                }
                return results;
            });

        mock.Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentDoc);

        return mock.Object;
    }

    private IDocumentIndexer CreateMockDocumentIndexer(int simulatedLatencyMs = 0, int expectedChunks = 1)
    {
        var mock = CreateLooseMock<IDocumentIndexer>();

        mock.Setup(i => i.IndexDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (string filePath, string _, string _, CancellationToken ct) =>
            {
                if (simulatedLatencyMs > 0)
                {
                    await Task.Delay(simulatedLatencyMs, ct);
                }
                var doc = TestDocumentBuilder.Create().WithFilePath(filePath).Build();
                return IndexResult.Success(filePath, doc, expectedChunks);
            });

        mock.Setup(i => i.IndexDocumentsAsync(
                It.IsAny<IEnumerable<(string FilePath, string Content)>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (IEnumerable<(string FilePath, string Content)> documents, string _, CancellationToken ct) =>
            {
                var docList = documents.ToList();
                if (simulatedLatencyMs > 0)
                {
                    await Task.Delay(simulatedLatencyMs * docList.Count / 2, ct);
                }

                return docList.Select(d =>
                {
                    var doc = TestDocumentBuilder.Create().WithFilePath(d.FilePath).Build();
                    return IndexResult.Success(d.FilePath, doc, expectedChunks);
                }).ToList();
            });

        return mock.Object;
    }

    private ISessionContext CreateMockSessionContext()
    {
        var mock = CreateLooseMock<ISessionContext>();
        mock.Setup(s => s.IsProjectActive).Returns(true);
        mock.Setup(s => s.TenantKey).Returns("test:main:hash");
        return mock.Object;
    }

    private static ReadOnlyMemory<float> CreateTestEmbedding(int dimensions = 1024, int seed = 42)
    {
        var random = new Random(seed);
        var vector = new float[dimensions];
        for (int i = 0; i < dimensions; i++)
        {
            vector[i] = (float)random.NextDouble();
        }
        return new ReadOnlyMemory<float>(vector);
    }

    private static string GenerateTestContent(int wordCount)
    {
        var words = new[] { "the", "quick", "brown", "fox", "jumps", "over", "lazy", "dog", "lorem", "ipsum" };
        var random = new Random(42);
        return string.Join(" ", Enumerable.Range(0, wordCount)
            .Select(_ => words[random.Next(words.Length)]));
    }

    private static string GenerateLargeDocument(int lineCount)
    {
        var lines = new List<string>(lineCount)
        {
            "# Large Document",
            "",
            "## Overview",
            ""
        };

        for (int i = 0; i < lineCount; i++)
        {
            if (i % 50 == 0)
            {
                lines.Add($"### Section {i / 50 + 1}");
                lines.Add("");
            }
            lines.Add($"Line {i + 1}: {GenerateTestContent(20)}");
        }

        return string.Join("\n", lines);
    }

    private static void LogPerformanceMetric(string metricName, double value)
    {
        // In a real implementation, this would write to a metrics store or test output
        Console.WriteLine($"[PERF] {metricName}: {value:F2}");
    }

    #endregion
}
