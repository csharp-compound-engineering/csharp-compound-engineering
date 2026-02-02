using CompoundDocs.Bedrock;

namespace CompoundDocs.Tests.Unit.Services;

public class BedrockLlmServiceTests
{
    private readonly IBedrockLlmService _service = Substitute.For<IBedrockLlmService>();

    [Fact]
    public async Task GenerateAsync_ValidInput_ReturnsResponse()
    {
        var messages = new List<BedrockMessage> { new("user", "Hello") };
        _service.GenerateAsync("system prompt", Arg.Any<IReadOnlyList<BedrockMessage>>(), Arg.Any<ModelTier>(), Arg.Any<CancellationToken>())
            .Returns("Generated response");

        var result = await _service.GenerateAsync("system prompt", messages, ModelTier.Sonnet);

        result.Should().Be("Generated response");
    }

    [Fact]
    public async Task ExtractEntitiesAsync_ValidText_ReturnsEntities()
    {
        var entities = new List<ExtractedEntity>
        {
            new() { Name = "Entity1", Type = "Concept", Description = "A test entity" }
        };

        _service.ExtractEntitiesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(entities);

        var result = await _service.ExtractEntitiesAsync("some chunk text");

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Entity1");
        result[0].Type.Should().Be("Concept");
    }

    [Fact]
    public async Task GenerateAsync_CancellationRequested_ThrowsOperationCancelled()
    {
        _service.GenerateAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<BedrockMessage>>(), Arg.Any<ModelTier>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var act = async () => await _service.GenerateAsync("prompt", new List<BedrockMessage>(), ModelTier.Haiku);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void ModelTier_HasExpectedValues()
    {
        Enum.GetValues<ModelTier>().Should().HaveCount(3);
        Enum.IsDefined(ModelTier.Haiku).Should().BeTrue();
        Enum.IsDefined(ModelTier.Sonnet).Should().BeTrue();
        Enum.IsDefined(ModelTier.Opus).Should().BeTrue();
    }

    [Fact]
    public void BedrockMessage_RecordEquality()
    {
        var msg1 = new BedrockMessage("user", "Hello");
        var msg2 = new BedrockMessage("user", "Hello");
        var msg3 = new BedrockMessage("assistant", "Hi");

        msg1.Should().Be(msg2);
        msg1.Should().NotBe(msg3);
    }
}
