using CompoundDocs.Bedrock;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using CompoundDocs.McpServer.Observability;
using CompoundDocs.McpServer.Tools;
using CompoundDocs.Vector;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Unit.Tools;

public class RagQueryToolTests
{
    [Fact]
    public async Task QueryAsync_EmptyQuery_ReturnsEmptyQueryError()
    {
        var sut = new RagQueryTool(
            new Mock<IVectorStore>().Object,
            new Mock<IGraphRepository>().Object,
            new Mock<IBedrockEmbeddingService>().Object,
            new Mock<IBedrockLlmService>().Object,
            new Mock<IGraphRagPipeline>().Object,
            new Mock<IMetricsCollector>().Object,
            NullLogger<RagQueryTool>.Instance);

        var result = await sut.QueryAsync("", 5, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public async Task QueryAsync_WhitespaceQuery_ReturnsEmptyQueryError()
    {
        var sut = new RagQueryTool(
            new Mock<IVectorStore>().Object,
            new Mock<IGraphRepository>().Object,
            new Mock<IBedrockEmbeddingService>().Object,
            new Mock<IBedrockLlmService>().Object,
            new Mock<IGraphRagPipeline>().Object,
            new Mock<IMetricsCollector>().Object,
            NullLogger<RagQueryTool>.Instance);

        var result = await sut.QueryAsync("   ", 5, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public async Task QueryAsync_ValidQuery_ReturnsSuccessWithResult()
    {
        var graphRagPipeline = new Mock<IGraphRagPipeline>();
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

        graphRagPipeline.Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(graphRagResult);

        var sut = new RagQueryTool(
            new Mock<IVectorStore>().Object,
            new Mock<IGraphRepository>().Object,
            new Mock<IBedrockEmbeddingService>().Object,
            new Mock<IBedrockLlmService>().Object,
            graphRagPipeline.Object,
            new Mock<IMetricsCollector>().Object,
            NullLogger<RagQueryTool>.Instance);
        var result = await sut.QueryAsync("test query", 5, CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data.Query.ShouldBe("test query");
        result.Data.Answer.ShouldBe("Test answer");
        result.Data.Sources.Count.ShouldBe(1);
        result.Data.Sources[0].DocumentId.ShouldBe("doc1");
        result.Data.ConfidenceScore.ShouldBe(0.9f, 0.01f);
    }

    [Fact]
    public async Task QueryAsync_ValidQuery_RecordsQueryMetrics()
    {
        var graphRagPipeline = new Mock<IGraphRagPipeline>();
        var metricsMock = new Mock<IMetricsCollector>();
        graphRagPipeline.Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphRagResult
            {
                Answer = "answer",
                Sources = [new GraphRagSource { DocumentId = "d1", ChunkId = "c1", Repository = "r1", FilePath = "f.md", RelevanceScore = 0.9 }]
            });

        var sut = new RagQueryTool(
            new Mock<IVectorStore>().Object,
            new Mock<IGraphRepository>().Object,
            new Mock<IBedrockEmbeddingService>().Object,
            new Mock<IBedrockLlmService>().Object,
            graphRagPipeline.Object,
            metricsMock.Object,
            NullLogger<RagQueryTool>.Instance);
        await sut.QueryAsync("test query", 5, CancellationToken.None);

        metricsMock.Verify(m => m.RecordQuery(It.Is<double>(d => d >= 0), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_MaxResultsNegative_DefaultsTo5()
    {
        var graphRagPipeline = new Mock<IGraphRagPipeline>();
        graphRagPipeline.Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphRagResult { Answer = "answer" });

        var sut = new RagQueryTool(
            new Mock<IVectorStore>().Object,
            new Mock<IGraphRepository>().Object,
            new Mock<IBedrockEmbeddingService>().Object,
            new Mock<IBedrockLlmService>().Object,
            graphRagPipeline.Object,
            new Mock<IMetricsCollector>().Object,
            NullLogger<RagQueryTool>.Instance);
        await sut.QueryAsync("query", -1, CancellationToken.None);

        graphRagPipeline.Verify(m => m.QueryAsync(
            "query",
            It.Is<GraphRagOptions>(o => o.MaxChunks == 5),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_MaxResultsOver20_CapsAt20()
    {
        var graphRagPipeline = new Mock<IGraphRagPipeline>();
        graphRagPipeline.Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphRagResult { Answer = "answer" });

        var sut = new RagQueryTool(
            new Mock<IVectorStore>().Object,
            new Mock<IGraphRepository>().Object,
            new Mock<IBedrockEmbeddingService>().Object,
            new Mock<IBedrockLlmService>().Object,
            graphRagPipeline.Object,
            new Mock<IMetricsCollector>().Object,
            NullLogger<RagQueryTool>.Instance);
        await sut.QueryAsync("query", 50, CancellationToken.None);

        graphRagPipeline.Verify(m => m.QueryAsync(
            "query",
            It.Is<GraphRagOptions>(o => o.MaxChunks == 20),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryAsync_CancellationRequested_ReturnsOperationCancelled()
    {
        var graphRagPipeline = new Mock<IGraphRagPipeline>();
        graphRagPipeline.Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var sut = new RagQueryTool(
            new Mock<IVectorStore>().Object,
            new Mock<IGraphRepository>().Object,
            new Mock<IBedrockEmbeddingService>().Object,
            new Mock<IBedrockLlmService>().Object,
            graphRagPipeline.Object,
            new Mock<IMetricsCollector>().Object,
            NullLogger<RagQueryTool>.Instance);
        var result = await sut.QueryAsync("query", 5, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("OPERATION_CANCELLED");
    }

    [Fact]
    public async Task QueryAsync_PipelineThrows_ReturnsRagSynthesisFailed()
    {
        var graphRagPipeline = new Mock<IGraphRagPipeline>();
        graphRagPipeline.Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Pipeline failure"));

        var sut = new RagQueryTool(
            new Mock<IVectorStore>().Object,
            new Mock<IGraphRepository>().Object,
            new Mock<IBedrockEmbeddingService>().Object,
            new Mock<IBedrockLlmService>().Object,
            graphRagPipeline.Object,
            new Mock<IMetricsCollector>().Object,
            NullLogger<RagQueryTool>.Instance);
        var result = await sut.QueryAsync("query", 5, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("RAG_SYNTHESIS_FAILED");
        result.Error!.ShouldContain("Pipeline failure");
    }

    [Fact]
    public async Task QueryAsync_PipelineThrows_RecordsErrorMetric()
    {
        var graphRagPipeline = new Mock<IGraphRagPipeline>();
        var metricsMock = new Mock<IMetricsCollector>();
        graphRagPipeline.Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Pipeline failure"));

        var sut = new RagQueryTool(
            new Mock<IVectorStore>().Object,
            new Mock<IGraphRepository>().Object,
            new Mock<IBedrockEmbeddingService>().Object,
            new Mock<IBedrockLlmService>().Object,
            graphRagPipeline.Object,
            metricsMock.Object,
            NullLogger<RagQueryTool>.Instance);
        await sut.QueryAsync("query", 5, CancellationToken.None);

        metricsMock.Verify(m => m.RecordError("InvalidOperationException"), Times.Once);
    }

    [Fact]
    public void Constructor_NullDependency_ThrowsArgumentNullException()
    {
        var vectorStore = new Mock<IVectorStore>();
        var graphRepository = new Mock<IGraphRepository>();
        var embeddingService = new Mock<IBedrockEmbeddingService>();
        var llmService = new Mock<IBedrockLlmService>();
        var graphRagPipeline = new Mock<IGraphRagPipeline>();
        var metricsMock = new Mock<IMetricsCollector>();
        var logger = NullLogger<RagQueryTool>.Instance;

        var ex = Should.Throw<ArgumentNullException>(() => new RagQueryTool(null!, graphRepository.Object, embeddingService.Object, llmService.Object, graphRagPipeline.Object, metricsMock.Object, logger));
        ex.ParamName.ShouldBe("vectorStore");

        ex = Should.Throw<ArgumentNullException>(() => new RagQueryTool(vectorStore.Object, null!, embeddingService.Object, llmService.Object, graphRagPipeline.Object, metricsMock.Object, logger));
        ex.ParamName.ShouldBe("graphRepository");

        ex = Should.Throw<ArgumentNullException>(() => new RagQueryTool(vectorStore.Object, graphRepository.Object, null!, llmService.Object, graphRagPipeline.Object, metricsMock.Object, logger));
        ex.ParamName.ShouldBe("embeddingService");

        ex = Should.Throw<ArgumentNullException>(() => new RagQueryTool(vectorStore.Object, graphRepository.Object, embeddingService.Object, null!, graphRagPipeline.Object, metricsMock.Object, logger));
        ex.ParamName.ShouldBe("llmService");

        ex = Should.Throw<ArgumentNullException>(() => new RagQueryTool(vectorStore.Object, graphRepository.Object, embeddingService.Object, llmService.Object, null!, metricsMock.Object, logger));
        ex.ParamName.ShouldBe("graphRagPipeline");

        ex = Should.Throw<ArgumentNullException>(() => new RagQueryTool(vectorStore.Object, graphRepository.Object, embeddingService.Object, llmService.Object, graphRagPipeline.Object, null!, logger));
        ex.ParamName.ShouldBe("metrics");

        ex = Should.Throw<ArgumentNullException>(() => new RagQueryTool(vectorStore.Object, graphRepository.Object, embeddingService.Object, llmService.Object, graphRagPipeline.Object, metricsMock.Object, null!));
        ex.ParamName.ShouldBe("logger");
    }
}
