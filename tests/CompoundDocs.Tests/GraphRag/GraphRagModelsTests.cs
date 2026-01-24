using CompoundDocs.GraphRag;

namespace CompoundDocs.Tests.GraphRag;

public sealed class GraphRagModelsTests
{
    [Fact]
    public void GraphRagResult_RequiredPropertiesAndDefaults()
    {
        var result = new GraphRagResult
        {
            Answer = "Test answer"
        };

        result.Answer.ShouldBe("Test answer");
        result.Sources.ShouldNotBeNull();
        result.Sources.ShouldBeEmpty();
        result.RelatedConcepts.ShouldNotBeNull();
        result.RelatedConcepts.ShouldBeEmpty();
        result.Confidence.ShouldBe(0.0);
    }

    [Fact]
    public void GraphRagSource_RequiredProperties()
    {
        var source = new GraphRagSource
        {
            DocumentId = "doc-1",
            ChunkId = "chunk-1",
            Repository = "repo",
            FilePath = "docs/test.md"
        };

        source.DocumentId.ShouldBe("doc-1");
        source.ChunkId.ShouldBe("chunk-1");
        source.Repository.ShouldBe("repo");
        source.FilePath.ShouldBe("docs/test.md");
        source.RelevanceScore.ShouldBe(0.0);
    }

    [Fact]
    public void GraphRagOptions_Defaults()
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
    public void GraphRagOptions_CustomValues()
    {
        var options = new GraphRagOptions
        {
            MaxChunks = 20,
            MaxTraversalSteps = 3,
            MinRelevanceScore = 0.5,
            UseCrossRepoLinks = false,
            RepositoryFilter = "my-repo",
            DocTypeFilter = "spec"
        };

        options.MaxChunks.ShouldBe(20);
        options.MaxTraversalSteps.ShouldBe(3);
        options.MinRelevanceScore.ShouldBe(0.5);
        options.UseCrossRepoLinks.ShouldBeFalse();
        options.RepositoryFilter.ShouldBe("my-repo");
        options.DocTypeFilter.ShouldBe("spec");
    }
}
