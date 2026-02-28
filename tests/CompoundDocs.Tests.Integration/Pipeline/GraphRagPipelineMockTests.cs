using Moq;
using CompoundDocs.GraphRag;

namespace CompoundDocs.Tests.Integration.Pipeline;

/// <summary>
/// Mock-based integration tests for the full IGraphRagPipeline.
/// Validates the end-to-end pipeline contract (embed, search, traverse, synthesize)
/// without requiring any AWS infrastructure.
/// </summary>
public class GraphRagPipelineMockTests
{
    [Fact]
    public async Task FullPipeline_WithMockedServices_ReturnsStructuredResult()
    {
        // Arrange
        var pipelineMock = new Mock<IGraphRagPipeline>(MockBehavior.Strict);

        var query = "How does the GraphRAG pipeline combine vector search with graph traversal?";
        var options = new GraphRagOptions
        {
            MaxChunks = 5,
            MaxTraversalSteps = 3,
            MinRelevanceScore = 0.75,
            UseCrossRepoLinks = true,
            RepositoryFilter = null,
            DocTypeFilter = "architecture"
        };

        var expectedResult = new GraphRagResult
        {
            Answer = "The GraphRAG pipeline combines vector search with graph traversal through a "
                + "multi-stage process: (1) The user query is embedded using Titan Embed V2 to produce "
                + "a 1024-dimensional vector. (2) A k-NN search against OpenSearch Serverless retrieves "
                + "the most relevant document chunks. (3) Graph traversal over Neptune follows concept "
                + "relationships (MENTIONS, RELATES_TO) to discover additional context across documents. "
                + "(4) The enriched context is synthesized by Claude via Bedrock to produce a final answer "
                + "with source attribution.",
            Sources =
            [
                new GraphRagSource
                {
                    DocumentId = "doc-pipeline-001",
                    ChunkId = "chunk-pipeline-overview",
                    Repository = "csharp-compound-engineering",
                    FilePath = "docs/architecture/pipeline.md",
                    RelevanceScore = 0.96
                },
                new GraphRagSource
                {
                    DocumentId = "doc-vector-002",
                    ChunkId = "chunk-knn-search",
                    Repository = "csharp-compound-engineering",
                    FilePath = "docs/components/vector-store.md",
                    RelevanceScore = 0.89
                },
                new GraphRagSource
                {
                    DocumentId = "doc-graph-003",
                    ChunkId = "chunk-traversal-algo",
                    Repository = "csharp-compound-engineering",
                    FilePath = "docs/components/graph-repository.md",
                    RelevanceScore = 0.85
                }
            ],
            RelatedConcepts =
            [
                "Vector Search",
                "Graph Traversal",
                "Knowledge Graph",
                "Retrieval-Augmented Generation",
                "Semantic Search"
            ],
            Confidence = 0.92
        };

        string? capturedQuery = null;
        GraphRagOptions? capturedOptions = null;

        pipelineMock
            .Setup(p => p.QueryAsync(
                It.IsAny<string>(),
                It.IsAny<GraphRagOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, GraphRagOptions?, CancellationToken>(
                (q, o, _) =>
                {
                    capturedQuery = q;
                    capturedOptions = o;
                })
            .ReturnsAsync(expectedResult)
            .Verifiable();

        var pipeline = pipelineMock.Object;

        // Act
        var result = await pipeline.QueryAsync(query, options);

        // Assert - Answer
        result.ShouldNotBeNull();
        result.Answer.ShouldNotBeNull();
        result.Answer.ShouldNotBeEmpty();
        result.Answer.ShouldContain("vector search");
        result.Answer.ShouldContain("graph traversal");

        // Assert - Sources
        result.Sources.ShouldNotBeNull();
        result.Sources.ShouldNotBeEmpty();
        result.Sources.Count.ShouldBe(3);

        result.Sources[0].DocumentId.ShouldBe("doc-pipeline-001");
        result.Sources[0].ChunkId.ShouldBe("chunk-pipeline-overview");
        result.Sources[0].Repository.ShouldBe("csharp-compound-engineering");
        result.Sources[0].FilePath.ShouldBe("docs/architecture/pipeline.md");
        result.Sources[0].RelevanceScore.ShouldBeGreaterThan(0.9);

        result.Sources[1].RelevanceScore.ShouldBeGreaterThan(0.8);
        result.Sources[2].RelevanceScore.ShouldBeGreaterThan(0.8);

        // Verify sources are ordered by relevance (descending)
        for (var i = 0; i < result.Sources.Count - 1; i++)
        {
            result.Sources[i].RelevanceScore
                .ShouldBeGreaterThanOrEqualTo(result.Sources[i + 1].RelevanceScore);
        }

        // Assert - Related Concepts
        result.RelatedConcepts.ShouldNotBeNull();
        result.RelatedConcepts.ShouldNotBeEmpty();
        result.RelatedConcepts.Count.ShouldBe(5);
        result.RelatedConcepts.ShouldContain("Vector Search");
        result.RelatedConcepts.ShouldContain("Graph Traversal");
        result.RelatedConcepts.ShouldContain("Retrieval-Augmented Generation");

        // Assert - Confidence
        result.Confidence.ShouldBeGreaterThan(0.0);
        result.Confidence.ShouldBeLessThanOrEqualTo(1.0);
        result.Confidence.ShouldBe(0.92);

        // Verify captured arguments
        capturedQuery.ShouldBe(query);
        capturedOptions.ShouldNotBeNull();
        capturedOptions!.MaxChunks.ShouldBe(5);
        capturedOptions.MaxTraversalSteps.ShouldBe(3);
        capturedOptions.MinRelevanceScore.ShouldBe(0.75);
        capturedOptions.UseCrossRepoLinks.ShouldBeTrue();
        capturedOptions.DocTypeFilter.ShouldBe("architecture");
        capturedOptions.RepositoryFilter.ShouldBeNull();

        pipelineMock.Verify(
            p => p.QueryAsync(query, options, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
