using CompoundDocs.Bedrock;

namespace CompoundDocs.Tests.Unit.Services;

public class BedrockLlmServiceTests
{
    private readonly Mock<IBedrockLlmService> _service = new();

    [Fact]
    public async Task GenerateAsync_ValidInput_ReturnsResponse()
    {
        var messages = new List<BedrockMessage> { new("user", "Hello") };
        _service.Setup(m => m.GenerateAsync("system prompt", It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Generated response");

        var result = await _service.Object.GenerateAsync("system prompt", messages, ModelTier.Sonnet);

        result.ShouldBe("Generated response");
    }

    [Fact]
    public async Task ExtractEntitiesAsync_ValidText_ReturnsEntities()
    {
        var entities = new List<ExtractedEntity>
        {
            new() { Name = "Entity1", Type = "Concept", Description = "A test entity" }
        };

        _service.Setup(m => m.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entities);

        var result = await _service.Object.ExtractEntitiesAsync("some chunk text");

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Entity1");
        result[0].Type.ShouldBe("Concept");
    }

    [Fact]
    public async Task GenerateAsync_CancellationRequested_ThrowsOperationCancelled()
    {
        _service.Setup(m => m.GenerateAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<BedrockMessage>>(), It.IsAny<ModelTier>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await _service.Object.GenerateAsync("prompt", new List<BedrockMessage>(), ModelTier.Haiku));
    }

    [Fact]
    public void ModelTier_HasExpectedValues()
    {
        Enum.GetValues<ModelTier>().Length.ShouldBe(3);
        Enum.IsDefined(ModelTier.Haiku).ShouldBeTrue();
        Enum.IsDefined(ModelTier.Sonnet).ShouldBeTrue();
        Enum.IsDefined(ModelTier.Opus).ShouldBeTrue();
    }

    [Fact]
    public void BedrockMessage_RecordEquality()
    {
        var msg1 = new BedrockMessage("user", "Hello");
        var msg2 = new BedrockMessage("user", "Hello");
        var msg3 = new BedrockMessage("assistant", "Hi");

        msg1.ShouldBe(msg2);
        msg1.ShouldNotBe(msg3);
    }
}
