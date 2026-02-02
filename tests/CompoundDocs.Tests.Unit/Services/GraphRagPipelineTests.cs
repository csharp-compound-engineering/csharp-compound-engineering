using CompoundDocs.GraphRag;

namespace CompoundDocs.Tests.Unit.Services;

public class GraphRagPipelineTests
{
    private readonly IGraphRagPipeline _pipeline = Substitute.For<IGraphRagPipeline>();

    [Fact]
    public async Task QueryAsync_ValidQuery_ReturnsResult()
    {
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

        _pipeline.QueryAsync(Arg.Any<string>(), Arg.Any<GraphRagOptions>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _pipeline.QueryAsync("test query");

        result.Answer.Should().Be("Test answer");
        result.Sources.Should().HaveCount(1);
        result.RelatedConcepts.Should().HaveCount(2);
        result.Confidence.Should().BeApproximately(0.85, 0.01);
    }

    [Fact]
    public async Task QueryAsync_WithOptions_PassesOptions()
    {
        _pipeline.QueryAsync(Arg.Any<string>(), Arg.Any<GraphRagOptions>(), Arg.Any<CancellationToken>())
            .Returns(new GraphRagResult { Answer = "answer" });

        var options = new GraphRagOptions
        {
            MaxChunks = 15,
            MaxTraversalSteps = 3,
            MinRelevanceScore = 0.8,
            UseCrossRepoLinks = false,
            DocTypeFilter = "insight"
        };

        await _pipeline.QueryAsync("query", options);

        await _pipeline.Received(1).QueryAsync("query", options, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GraphRagOptions_DefaultValues()
    {
        var options = new GraphRagOptions();

        options.MaxChunks.Should().Be(10);
        options.MaxTraversalSteps.Should().Be(5);
        options.MinRelevanceScore.Should().Be(0.7);
        options.UseCrossRepoLinks.Should().BeTrue();
        options.RepositoryFilter.Should().BeNull();
        options.DocTypeFilter.Should().BeNull();
    }

    [Fact]
    public void GraphRagResult_DefaultCollections()
    {
        var result = new GraphRagResult { Answer = "test" };

        result.Sources.Should().BeEmpty();
        result.RelatedConcepts.Should().BeEmpty();
        result.Confidence.Should().Be(0);
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

        source.DocumentId.Should().Be("doc1");
        source.RelevanceScore.Should().Be(0);
    }
}
