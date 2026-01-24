using CompoundDocs.Bedrock;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using CompoundDocs.McpServer.Tools;
using CompoundDocs.Vector;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Tools;

public class RagQueryToolTests
{
    private readonly Mock<IVectorStore> _vectorStore = new();
    private readonly Mock<IGraphRepository> _graphRepository = new();
    private readonly Mock<IBedrockEmbeddingService> _embeddingService = new();
    private readonly Mock<IBedrockLlmService> _llmService = new();
    private readonly Mock<IGraphRagPipeline> _graphRagPipeline = new();
    private readonly ILogger<RagQueryTool> _logger = NullLogger<RagQueryTool>.Instance;
    private readonly RagQueryTool _sut;

    public RagQueryToolTests()
    {
        _sut = new RagQueryTool(
            _vectorStore.Object, _graphRepository.Object, _embeddingService.Object,
            _llmService.Object, _graphRagPipeline.Object, _logger);
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

        _graphRagPipeline.Setup(p => p.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graphRagResult);

        var result = await _sut.QueryAsync("test query", 5, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.Query.ShouldBe("test query");
        result.Data.Answer.ShouldBe("Test answer");
        result.Data.Sources.Count.ShouldBe(1);
        result.Data.Sources[0].DocumentId.ShouldBe("doc1");
        (result.Data.ConfidenceScore - 0.9f).ShouldBeLessThan(0.01f);
    }

    [Fact]
    public async Task QueryAsync_MaxResultsNegative_DefaultsTo5()
    {
        _graphRagPipeline.Setup(p => p.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphRagResult { Answer = "answer" });

        await _sut.QueryAsync("query", -1, CancellationToken.None);

        _graphRagPipeline.Verify(p => p.QueryAsync(
            "query",
            It.Is<GraphRagOptions>(o => o.MaxChunks == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_MaxResultsOver20_CapsAt20()
    {
        _graphRagPipeline.Setup(p => p.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphRagResult { Answer = "answer" });

        await _sut.QueryAsync("query", 50, CancellationToken.None);

        _graphRagPipeline.Verify(p => p.QueryAsync(
            "query",
            It.Is<GraphRagOptions>(o => o.MaxChunks == 20),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_CancellationRequested_ReturnsOperationCancelled()
    {
        _graphRagPipeline.Setup(p => p.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await _sut.QueryAsync("query", 5, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("OPERATION_CANCELLED");
    }

    [Fact]
    public async Task QueryAsync_PipelineThrows_ReturnsRagSynthesisFailed()
    {
        _graphRagPipeline.Setup(p => p.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Pipeline failure"));

        var result = await _sut.QueryAsync("query", 5, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("RAG_SYNTHESIS_FAILED");
        result.Error!.ShouldContain("Pipeline failure");
    }

    [Fact]
    public void Constructor_NullDependency_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RagQueryTool(null!, _graphRepository.Object, _embeddingService.Object, _llmService.Object, _graphRagPipeline.Object, _logger))
            .ParamName.ShouldBe("vectorStore");

        Should.Throw<ArgumentNullException>(() =>
            new RagQueryTool(_vectorStore.Object, null!, _embeddingService.Object, _llmService.Object, _graphRagPipeline.Object, _logger))
            .ParamName.ShouldBe("graphRepository");

        Should.Throw<ArgumentNullException>(() =>
            new RagQueryTool(_vectorStore.Object, _graphRepository.Object, null!, _llmService.Object, _graphRagPipeline.Object, _logger))
            .ParamName.ShouldBe("embeddingService");

        Should.Throw<ArgumentNullException>(() =>
            new RagQueryTool(_vectorStore.Object, _graphRepository.Object, _embeddingService.Object, null!, _graphRagPipeline.Object, _logger))
            .ParamName.ShouldBe("llmService");

        Should.Throw<ArgumentNullException>(() =>
            new RagQueryTool(_vectorStore.Object, _graphRepository.Object, _embeddingService.Object, _llmService.Object, null!, _logger))
            .ParamName.ShouldBe("graphRagPipeline");

        Should.Throw<ArgumentNullException>(() =>
            new RagQueryTool(_vectorStore.Object, _graphRepository.Object, _embeddingService.Object, _llmService.Object, _graphRagPipeline.Object, null!))
            .ParamName.ShouldBe("logger");
    }
}
