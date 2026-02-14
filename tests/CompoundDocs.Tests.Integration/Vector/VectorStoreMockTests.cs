using Moq;
using CompoundDocs.Vector;

namespace CompoundDocs.Tests.Integration.Vector;

/// <summary>
/// Mock-based integration tests for IVectorStore index and search operations.
/// Validates the contract without requiring AWS OpenSearch infrastructure.
/// </summary>
public class VectorStoreMockTests
{
    private readonly Mock<IVectorStore> _vectorStoreMock;

    public VectorStoreMockTests()
    {
        _vectorStoreMock = new Mock<IVectorStore>(MockBehavior.Strict);
    }

    [Fact]
    public async Task IndexAndSearch_WithMockedOpenSearch_ReturnsResults()
    {
        // Arrange
        var chunkId = "chunk-001";
        var embedding = new float[1024];
        Array.Fill(embedding, 0.5f);
        var metadata = new Dictionary<string, string>
        {
            ["documentId"] = "doc-001",
            ["repository"] = "test-repo",
            ["filePath"] = "docs/architecture.md"
        };

        var expectedResults = new List<VectorSearchResult>
        {
            new()
            {
                ChunkId = chunkId,
                Score = 0.95,
                Metadata = metadata
            },
            new()
            {
                ChunkId = "chunk-002",
                Score = 0.82,
                Metadata = new Dictionary<string, string>
                {
                    ["documentId"] = "doc-002",
                    ["repository"] = "test-repo",
                    ["filePath"] = "docs/design.md"
                }
            }
        };

        _vectorStoreMock
            .Setup(v => v.IndexAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        _vectorStoreMock
            .Setup(v => v.SearchAsync(
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults)
            .Verifiable();

        var store = _vectorStoreMock.Object;

        // Act
        await store.IndexAsync(chunkId, embedding, metadata);
        var results = await store.SearchAsync(embedding, topK: 5);

        // Assert
        results.ShouldNotBeNull();
        results.ShouldNotBeEmpty();
        results.Count.ShouldBe(2);

        results[0].ChunkId.ShouldBe(chunkId);
        results[0].Score.ShouldBeGreaterThan(0.9);
        results[0].Metadata.ShouldContainKey("documentId");
        results[0].Metadata["documentId"].ShouldBe("doc-001");

        results[1].ChunkId.ShouldBe("chunk-002");
        results[1].Score.ShouldBeGreaterThan(0.8);

        _vectorStoreMock.Verify(
            v => v.IndexAsync(chunkId, embedding, metadata, It.IsAny<CancellationToken>()),
            Times.Once);

        _vectorStoreMock.Verify(
            v => v.SearchAsync(embedding, 5, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
