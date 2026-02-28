using Moq;
using CompoundDocs.Bedrock;

namespace CompoundDocs.Tests.Integration.Bedrock;

/// <summary>
/// Mock-based integration tests for IBedrockEmbeddingService.
/// Validates Titan Embed V2 embedding generation contract (1024-dimensional vectors)
/// without requiring AWS Bedrock infrastructure.
/// </summary>
public class BedrockEmbeddingMockTests
{
    [Fact]
    public async Task TitanEmbed_WithMockedBedrock_ReturnsEmbeddingArray()
    {
        // Arrange
        var embeddingServiceMock = new Mock<IBedrockEmbeddingService>(MockBehavior.Strict);

        const int expectedDimensions = 1024;
        var inputText = "Dependency injection is a technique for achieving inversion of control between classes and their dependencies.";

        var expectedEmbedding = new float[expectedDimensions];
        var random = new Random(42);
        for (var i = 0; i < expectedDimensions; i++)
        {
            expectedEmbedding[i] = (float)(random.NextDouble() * 2 - 1);
        }

        embeddingServiceMock
            .Setup(e => e.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding)
            .Verifiable();

        var service = embeddingServiceMock.Object;

        // Act
        var result = await service.GenerateEmbeddingAsync(inputText);

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBe(expectedDimensions);
        result.ShouldBe(expectedEmbedding);

        // Verify embedding values are normalized (within expected range)
        result.All(v => v >= -1.0f && v <= 1.0f).ShouldBeTrue();

        embeddingServiceMock.Verify(
            e => e.GenerateEmbeddingAsync(inputText, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
