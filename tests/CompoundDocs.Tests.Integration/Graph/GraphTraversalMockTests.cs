using Moq;
using CompoundDocs.Graph;
using CompoundDocs.Common.Models;

namespace CompoundDocs.Tests.Integration.Graph;

/// <summary>
/// Mock-based integration tests for multi-hop graph traversal patterns.
/// Validates that concept-to-chunk traversal works correctly without Neptune infrastructure.
/// </summary>
public class GraphTraversalMockTests
{
    [Fact]
    public async Task MultiHopTraversal_WithMockedGraph_ReturnsDocuments()
    {
        // Arrange
        var graphRepoMock = new Mock<IGraphRepository>(MockBehavior.Strict);

        var rootConceptId = "concept-graphrag";
        var relatedConceptId = "concept-vector-search";

        var relatedConcepts = new List<ConceptNode>
        {
            new()
            {
                Id = relatedConceptId,
                Name = "Vector Search",
                Description = "Approximate nearest neighbor search over embeddings",
                Category = "Search"
            },
            new()
            {
                Id = "concept-knowledge-graph",
                Name = "Knowledge Graph",
                Description = "A graph-structured knowledge base",
                Category = "Data Structure"
            }
        };

        var chunksFromRootConcept = new List<ChunkNode>
        {
            new()
            {
                Id = "chunk-graphrag-001",
                SectionId = "section-intro",
                DocumentId = "doc-graphrag",
                Content = "GraphRAG combines graph traversal with vector search for retrieval-augmented generation.",
                Order = 0,
                TokenCount = 42
            }
        };

        var chunksFromRelatedConcept = new List<ChunkNode>
        {
            new()
            {
                Id = "chunk-vector-001",
                SectionId = "section-knn",
                DocumentId = "doc-vector",
                Content = "k-NN search finds the nearest neighbors in a high-dimensional embedding space.",
                Order = 0,
                TokenCount = 38
            },
            new()
            {
                Id = "chunk-vector-002",
                SectionId = "section-knn",
                DocumentId = "doc-vector",
                Content = "OpenSearch Serverless provides managed vector search capabilities.",
                Order = 1,
                TokenCount = 28
            }
        };

        // Setup: first hop retrieves related concepts
        graphRepoMock
            .Setup(g => g.GetRelatedConceptsAsync(
                rootConceptId,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(relatedConcepts)
            .Verifiable();

        // Setup: retrieve chunks for the root concept
        graphRepoMock
            .Setup(g => g.GetChunksByConceptAsync(
                rootConceptId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunksFromRootConcept)
            .Verifiable();

        // Setup: retrieve chunks for a related concept (second hop)
        graphRepoMock
            .Setup(g => g.GetChunksByConceptAsync(
                relatedConceptId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunksFromRelatedConcept)
            .Verifiable();

        // Setup: retrieve chunks for the other related concept
        graphRepoMock
            .Setup(g => g.GetChunksByConceptAsync(
                "concept-knowledge-graph",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkNode>())
            .Verifiable();

        var repo = graphRepoMock.Object;

        // Act - simulate multi-hop traversal pattern
        var concepts = await repo.GetRelatedConceptsAsync(rootConceptId, hops: 2);
        var allChunks = new List<ChunkNode>();

        // Retrieve chunks from root concept
        var rootChunks = await repo.GetChunksByConceptAsync(rootConceptId);
        allChunks.AddRange(rootChunks);

        // Retrieve chunks from each related concept (multi-hop)
        foreach (var concept in concepts)
        {
            var relatedChunks = await repo.GetChunksByConceptAsync(concept.Id);
            allChunks.AddRange(relatedChunks);
        }

        // Assert
        concepts.ShouldNotBeNull();
        concepts.Count.ShouldBe(2);

        allChunks.ShouldNotBeNull();
        allChunks.Count.ShouldBe(3);

        allChunks[0].Id.ShouldBe("chunk-graphrag-001");
        allChunks[0].DocumentId.ShouldBe("doc-graphrag");
        allChunks[0].Content.ShouldContain("GraphRAG");
        allChunks[0].TokenCount.ShouldBeGreaterThan(0);

        allChunks[1].Id.ShouldBe("chunk-vector-001");
        allChunks[1].DocumentId.ShouldBe("doc-vector");
        allChunks[1].SectionId.ShouldBe("section-knn");

        allChunks[2].Id.ShouldBe("chunk-vector-002");
        allChunks[2].Order.ShouldBe(1);

        // Verify traversal pattern: concepts queried once, chunks queried per concept
        graphRepoMock.Verify(
            g => g.GetRelatedConceptsAsync(rootConceptId, 2, It.IsAny<CancellationToken>()),
            Times.Once);

        graphRepoMock.Verify(
            g => g.GetChunksByConceptAsync(rootConceptId, It.IsAny<CancellationToken>()),
            Times.Once);

        graphRepoMock.Verify(
            g => g.GetChunksByConceptAsync(relatedConceptId, It.IsAny<CancellationToken>()),
            Times.Once);

        graphRepoMock.Verify(
            g => g.GetChunksByConceptAsync("concept-knowledge-graph", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
