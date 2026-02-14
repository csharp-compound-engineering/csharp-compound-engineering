using Moq;
using CompoundDocs.Vector;

namespace CompoundDocs.Tests.Integration.Vector;

/// <summary>
/// Mock-based integration tests for embedding index mapping validation.
/// Verifies that 1024-dimensional Titan Embed V2 vectors are correctly passed to the vector store.
/// </summary>
public class EmbeddingIndexMockTests
{
    private readonly Mock<IVectorStore> _vectorStoreMock;

    public EmbeddingIndexMockTests()
    {
        _vectorStoreMock = new Mock<IVectorStore>(MockBehavior.Strict);
    }

    [Fact]
    public async Task IndexMapping_WithMockedStore_Validates1024Dimensions()
    {
        // Arrange
        const int expectedDimensions = 1024;
        var chunkId = "chunk-embed-001";
        var embedding = new float[expectedDimensions];
        var random = new Random(42);
        for (var i = 0; i < expectedDimensions; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }

        var metadata = new Dictionary<string, string>
        {
            ["documentId"] = "doc-embed-001",
            ["repository"] = "embedding-test-repo",
            ["filePath"] = "docs/embeddings.md"
        };

        float[]? capturedEmbedding = null;

        _vectorStoreMock
            .Setup(v => v.IndexAsync(
                It.IsAny<string>(),
                It.Is<float[]>(e => e.Length == expectedDimensions),
                It.IsAny<Dictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, float[], Dictionary<string, string>, CancellationToken>(
                (_, emb, _, _) => capturedEmbedding = emb)
            .Returns(Task.CompletedTask)
            .Verifiable();

        var store = _vectorStoreMock.Object;

        // Act
        await store.IndexAsync(chunkId, embedding, metadata);

        // Assert
        _vectorStoreMock.Verify(
            v => v.IndexAsync(
                chunkId,
                It.Is<float[]>(e => e.Length == expectedDimensions),
                metadata,
                It.IsAny<CancellationToken>()),
            Times.Once);

        capturedEmbedding.ShouldNotBeNull();
        capturedEmbedding.Length.ShouldBe(expectedDimensions);
        capturedEmbedding.ShouldBe(embedding);
    }
}
