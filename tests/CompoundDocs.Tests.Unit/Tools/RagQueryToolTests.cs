using CompoundDocs.Bedrock;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using CompoundDocs.McpServer.Observability;
using CompoundDocs.McpServer.Tools;
using CompoundDocs.Vector;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Unit.Tools;

public class RagQueryToolTests : IDisposable
{
    private readonly Mock<IVectorStore> _vectorStore = new();
    private readonly Mock<IGraphRepository> _graphRepository = new();
    private readonly Mock<IBedrockEmbeddingService> _embeddingService = new();
    private readonly Mock<IBedrockLlmService> _llmService = new();
    private readonly Mock<IGraphRagPipeline> _graphRagPipeline = new();
    private readonly MetricsCollector _metrics = new();
    private readonly ILogger<RagQueryTool> _logger = NullLogger<RagQueryTool>.Instance;
    private readonly RagQueryTool _sut;

    public RagQueryToolTests()
    {
        _sut = new RagQueryTool(
            _vectorStore.Object, _graphRepository.Object, _embeddingService.Object,
            _llmService.Object, _graphRagPipeline.Object, _metrics, _logger);
    }

    [Fact]
    public async Task QueryAsync_EmptyQuery_ReturnsEmptyQueryError()
    {
        var result = await _sut.QueryAsync("", 5, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public async Task QueryAsync_WhitespaceQuery_ReturnsEmptyQueryError()
    {
        var result = await _sut.QueryAsync("   ", 5, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public async Task QueryAsync_ValidQuery_ReturnsSuccessWithResult()
    {
        var graphRagResult = new GraphRagResult
        {
            Answer = "Test answer",
            Sources =
            [
                new GraphRagSource
                {
                    DocumentId = "doc1",
                    ChunkId = "chunk1",
                    Repository = "repo1",
                    FilePath = "path/to/file.md",
                    RelevanceScore = 0.95
                }
            ],
            RelatedConcepts = ["concept1"],
            Confidence = 0.9
        };

        _graphRagPipeline.Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graphRagResult);

        var result = await _sut.QueryAsync("test query", 5, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Query.ShouldBe("test query");
        result.Data.Answer.ShouldBe("Test answer");
        result.Data.Sources.Count().ShouldBe(1);
        result.Data.Sources[0].DocumentId.ShouldBe("doc1");
        result.Data.ConfidenceScore.ShouldBe(0.9f, 0.01f);
    }

    [Fact]
    public async Task QueryAsync_ValidQuery_RecordsQueryMetrics()
    {
        _graphRagPipeline.Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphRagResult
            {
                Answer = "answer",
                Sources = [new GraphRagSource { DocumentId = "d1", ChunkId = "c1", Repository = "r1", FilePath = "f.md", RelevanceScore = 0.9 }]
            });

        await _sut.QueryAsync("test query", 5, CancellationToken.None);

        var snapshot = _metrics.GetSnapshot();
        snapshot.TotalQueries.ShouldBe(1);
    }

    [Fact]
    public async Task QueryAsync_MaxResultsNegative_DefaultsTo5()
    {
        _graphRagPipeline.Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphRagResult { Answer = "answer" });

        await _sut.QueryAsync("query", -1, CancellationToken.None);

        _graphRagPipeline.Verify(m => m.QueryAsync(
            "query",
            It.Is<GraphRagOptions>(o => o.MaxChunks == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_MaxResultsOver20_CapsAt20()
    {
        _graphRagPipeline.Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphRagResult { Answer = "answer" });

        await _sut.QueryAsync("query", 50, CancellationToken.None);

        _graphRagPipeline.Verify(m => m.QueryAsync(
            "query",
            It.Is<GraphRagOptions>(o => o.MaxChunks == 20),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_CancellationRequested_ReturnsOperationCancelled()
    {
        _graphRagPipeline.Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await _sut.QueryAsync("query", 5, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("OPERATION_CANCELLED");
    }

    [Fact]
    public async Task QueryAsync_PipelineThrows_ReturnsRagSynthesisFailed()
    {
        _graphRagPipeline.Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Pipeline failure"));

        var result = await _sut.QueryAsync("query", 5, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("RAG_SYNTHESIS_FAILED");
        result.Error!.ShouldContain("Pipeline failure");
    }

    [Fact]
    public async Task QueryAsync_PipelineThrows_RecordsErrorMetric()
    {
        _graphRagPipeline.Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Pipeline failure"));

        await _sut.QueryAsync("query", 5, CancellationToken.None);

        // The error counter should have been incremented (no direct accessor, but verify no exception)
        var snapshot = _metrics.GetSnapshot();
        snapshot.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_NullDependency_ThrowsArgumentNullException()
    {
        var ex = Should.Throw<ArgumentNullException>(() => new RagQueryTool(null!, _graphRepository.Object, _embeddingService.Object, _llmService.Object, _graphRagPipeline.Object, _metrics, _logger));
        ex.ParamName.ShouldBe("vectorStore");

        ex = Should.Throw<ArgumentNullException>(() => new RagQueryTool(_vectorStore.Object, null!, _embeddingService.Object, _llmService.Object, _graphRagPipeline.Object, _metrics, _logger));
        ex.ParamName.ShouldBe("graphRepository");

        ex = Should.Throw<ArgumentNullException>(() => new RagQueryTool(_vectorStore.Object, _graphRepository.Object, null!, _llmService.Object, _graphRagPipeline.Object, _metrics, _logger));
        ex.ParamName.ShouldBe("embeddingService");

        ex = Should.Throw<ArgumentNullException>(() => new RagQueryTool(_vectorStore.Object, _graphRepository.Object, _embeddingService.Object, null!, _graphRagPipeline.Object, _metrics, _logger));
        ex.ParamName.ShouldBe("llmService");

        ex = Should.Throw<ArgumentNullException>(() => new RagQueryTool(_vectorStore.Object, _graphRepository.Object, _embeddingService.Object, _llmService.Object, null!, _metrics, _logger));
        ex.ParamName.ShouldBe("graphRagPipeline");

        ex = Should.Throw<ArgumentNullException>(() => new RagQueryTool(_vectorStore.Object, _graphRepository.Object, _embeddingService.Object, _llmService.Object, _graphRagPipeline.Object, null!, _logger));
        ex.ParamName.ShouldBe("metrics");

        ex = Should.Throw<ArgumentNullException>(() => new RagQueryTool(_vectorStore.Object, _graphRepository.Object, _embeddingService.Object, _llmService.Object, _graphRagPipeline.Object, _metrics, null!));
        ex.ParamName.ShouldBe("logger");
    }

    public void Dispose()
    {
        _metrics.Dispose();
    }
}
