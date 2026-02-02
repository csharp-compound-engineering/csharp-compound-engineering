using CompoundDocs.Bedrock;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using CompoundDocs.McpServer.Tools;
using CompoundDocs.Vector;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Unit.Tools;

public class RagQueryToolTests
{
    private readonly IVectorStore _vectorStore = Substitute.For<IVectorStore>();
    private readonly IGraphRepository _graphRepository = Substitute.For<IGraphRepository>();
    private readonly IBedrockEmbeddingService _embeddingService = Substitute.For<IBedrockEmbeddingService>();
    private readonly IBedrockLlmService _llmService = Substitute.For<IBedrockLlmService>();
    private readonly IGraphRagPipeline _graphRagPipeline = Substitute.For<IGraphRagPipeline>();
    private readonly ILogger<RagQueryTool> _logger = NullLogger<RagQueryTool>.Instance;
    private readonly RagQueryTool _sut;

    public RagQueryToolTests()
    {
        _sut = new RagQueryTool(
            _vectorStore, _graphRepository, _embeddingService,
            _llmService, _graphRagPipeline, _logger);
    }

    [Fact]
    public async Task QueryAsync_EmptyQuery_ReturnsEmptyQueryError()
    {
        var result = await _sut.QueryAsync("", 5, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("EMPTY_QUERY");
    }

    [Fact]
    public async Task QueryAsync_WhitespaceQuery_ReturnsEmptyQueryError()
    {
        var result = await _sut.QueryAsync("   ", 5, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("EMPTY_QUERY");
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

        _graphRagPipeline.QueryAsync(Arg.Any<string>(), Arg.Any<GraphRagOptions>(), Arg.Any<CancellationToken>())
            .Returns(graphRagResult);

        var result = await _sut.QueryAsync("test query", 5, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Query.Should().Be("test query");
        result.Data.Answer.Should().Be("Test answer");
        result.Data.Sources.Should().HaveCount(1);
        result.Data.Sources[0].DocumentId.Should().Be("doc1");
        result.Data.ConfidenceScore.Should().BeApproximately(0.9f, 0.01f);
    }

    [Fact]
    public async Task QueryAsync_MaxResultsNegative_DefaultsTo5()
    {
        _graphRagPipeline.QueryAsync(Arg.Any<string>(), Arg.Any<GraphRagOptions>(), Arg.Any<CancellationToken>())
            .Returns(new GraphRagResult { Answer = "answer" });

        await _sut.QueryAsync("query", -1, CancellationToken.None);

        await _graphRagPipeline.Received(1).QueryAsync(
            "query",
            Arg.Is<GraphRagOptions>(o => o.MaxChunks == 5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_MaxResultsOver20_CapsAt20()
    {
        _graphRagPipeline.QueryAsync(Arg.Any<string>(), Arg.Any<GraphRagOptions>(), Arg.Any<CancellationToken>())
            .Returns(new GraphRagResult { Answer = "answer" });

        await _sut.QueryAsync("query", 50, CancellationToken.None);

        await _graphRagPipeline.Received(1).QueryAsync(
            "query",
            Arg.Is<GraphRagOptions>(o => o.MaxChunks == 20),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_CancellationRequested_ReturnsOperationCancelled()
    {
        _graphRagPipeline.QueryAsync(Arg.Any<string>(), Arg.Any<GraphRagOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var result = await _sut.QueryAsync("query", 5, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("OPERATION_CANCELLED");
    }

    [Fact]
    public async Task QueryAsync_PipelineThrows_ReturnsRagSynthesisFailed()
    {
        _graphRagPipeline.QueryAsync(Arg.Any<string>(), Arg.Any<GraphRagOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Pipeline failure"));

        var result = await _sut.QueryAsync("query", 5, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("RAG_SYNTHESIS_FAILED");
        result.Error.Should().Contain("Pipeline failure");
    }

    [Fact]
    public void Constructor_NullDependency_ThrowsArgumentNullException()
    {
        var act = () => new RagQueryTool(null!, _graphRepository, _embeddingService, _llmService, _graphRagPipeline, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("vectorStore");

        act = () => new RagQueryTool(_vectorStore, null!, _embeddingService, _llmService, _graphRagPipeline, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("graphRepository");

        act = () => new RagQueryTool(_vectorStore, _graphRepository, null!, _llmService, _graphRagPipeline, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("embeddingService");

        act = () => new RagQueryTool(_vectorStore, _graphRepository, _embeddingService, null!, _graphRagPipeline, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("llmService");

        act = () => new RagQueryTool(_vectorStore, _graphRepository, _embeddingService, _llmService, null!, _logger);
        act.Should().Throw<ArgumentNullException>().WithParameterName("graphRagPipeline");

        act = () => new RagQueryTool(_vectorStore, _graphRepository, _embeddingService, _llmService, _graphRagPipeline, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }
}
