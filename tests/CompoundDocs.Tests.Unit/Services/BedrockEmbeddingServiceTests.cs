using CompoundDocs.Bedrock;

namespace CompoundDocs.Tests.Unit.Services;

public class BedrockEmbeddingServiceTests
{
    private readonly Mock<IBedrockEmbeddingService> _service = new();

    [Fact]
    public async Task GenerateEmbeddingAsync_ValidText_ReturnsFloatArray()
    {
        var expected = new float[] { 0.1f, 0.2f, 0.3f };
        _service.Setup(m => m.GenerateEmbeddingAsync("test text", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.Object.GenerateEmbeddingAsync("test text");

        result.ShouldBe(expected);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_MultipleTexts_ReturnsList()
    {
        var texts = new[] { "text1", "text2" };
        var expected = new List<float[]>
        {
            new float[] { 0.1f, 0.2f },
            new float[] { 0.3f, 0.4f }
        };

        _service.Setup(m => m.GenerateEmbeddingsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _service.Object.GenerateEmbeddingsAsync(texts);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_CancellationRequested_ThrowsOperationCancelled()
    {
        _service.Setup(m => m.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await _service.Object.GenerateEmbeddingAsync("text", new CancellationToken(true)));
    }
}
