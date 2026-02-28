using CompoundDocs.GraphRag;

namespace CompoundDocs.Tests.Unit.Services;

public class GraphRagPipelineTests
{
    [Fact]
    public async Task QueryAsync_ValidQuery_ReturnsResult()
    {
        var pipeline = new Mock<IGraphRagPipeline>();
        var expected = new GraphRagResult
        {
            Answer = "Test answer",
            Sources =
            [
                new GraphRagSource
                {
                    DocumentId = "doc1",
                    ChunkId = "chunk1",
                    Repository = "repo1",
                    FilePath = "file.md",
                    RelevanceScore = 0.95
                }
            ],
            RelatedConcepts = ["concept1", "concept2"],
            Confidence = 0.85
        };

        pipeline.Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await pipeline.Object.QueryAsync("test query");

        result.Answer.ShouldBe("Test answer");
        result.Sources.Count().ShouldBe(1);
        result.RelatedConcepts.Count().ShouldBe(2);
        result.Confidence.ShouldBe(0.85, 0.01);
    }

    [Fact]
    public async Task QueryAsync_WithOptions_PassesOptions()
    {
        var pipeline = new Mock<IGraphRagPipeline>();
        pipeline.Setup(m => m.QueryAsync(It.IsAny<string>(), It.IsAny<GraphRagOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphRagResult { Answer = "answer" });

        var options = new GraphRagOptions
        {
            MaxChunks = 15,
            MaxTraversalSteps = 3,
            MinRelevanceScore = 0.8,
            UseCrossRepoLinks = false,
            DocTypeFilter = "insight"
        };

        await pipeline.Object.QueryAsync("query", options);

        pipeline.Verify(m => m.QueryAsync("query", options, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void GraphRagOptions_DefaultValues()
    {
        var options = new GraphRagOptions();

        options.MaxChunks.ShouldBe(10);
        options.MaxTraversalSteps.ShouldBe(5);
        options.MinRelevanceScore.ShouldBe(0.7);
        options.UseCrossRepoLinks.ShouldBeTrue();
        options.RepositoryFilter.ShouldBeNull();
        options.DocTypeFilter.ShouldBeNull();
    }

    [Fact]
    public void GraphRagResult_DefaultCollections()
    {
        var result = new GraphRagResult { Answer = "test" };

        result.Sources.ShouldBeEmpty();
        result.RelatedConcepts.ShouldBeEmpty();
        result.Confidence.ShouldBe(0);
    }

    [Fact]
    public void GraphRagSource_RequiredProperties()
    {
        var source = new GraphRagSource
        {
            DocumentId = "doc1",
            ChunkId = "chunk1",
            Repository = "repo1",
            FilePath = "path.md"
        };

        source.DocumentId.ShouldBe("doc1");
        source.RelevanceScore.ShouldBe(0);
    }
}
