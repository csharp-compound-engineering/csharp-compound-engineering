using CompoundDocs.Bedrock;

namespace CompoundDocs.Tests.Unit.Services;

public class BedrockEmbeddingServiceTests
{
    private readonly IBedrockEmbeddingService _service = Substitute.For<IBedrockEmbeddingService>();

    [Fact]
    public async Task GenerateEmbeddingAsync_ValidText_ReturnsFloatArray()
    {
        var expected = new float[] { 0.1f, 0.2f, 0.3f };
        _service.GenerateEmbeddingAsync("test text", Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _service.GenerateEmbeddingAsync("test text");

        result.Should().BeEquivalentTo(expected);
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

        _service.GenerateEmbeddingsAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _service.GenerateEmbeddingsAsync(texts);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_CancellationRequested_ThrowsOperationCancelled()
    {
        _service.GenerateEmbeddingAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var act = async () => await _service.GenerateEmbeddingAsync("text", new CancellationToken(true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
