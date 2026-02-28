using CompoundDocs.GraphRag;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using BedrockEntity = CompoundDocs.Bedrock.ExtractedEntity;
using BedrockLlm = CompoundDocs.Bedrock.IBedrockLlmService;

namespace CompoundDocs.Tests.Unit.GraphRag;

public sealed class BedrockEntityExtractorTests
{
    private readonly Mock<BedrockLlm> _llmMock = new();
    private readonly BedrockEntityExtractor _extractor;

    public BedrockEntityExtractorTests()
    {
        _extractor = new BedrockEntityExtractor(
            _llmMock.Object,
            NullLogger<BedrockEntityExtractor>.Instance);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_MapsEntitiesCorrectly()
    {
        _llmMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new BedrockEntity
                {
                    Name = "Amazon Neptune",
                    Type = "Service",
                    Description = "Graph database",
                    Aliases = ["Neptune", "AWS Neptune"]
                }
            ]);

        var result = await _extractor.ExtractEntitiesAsync("Some text about Neptune");

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Amazon Neptune");
        result[0].Type.ShouldBe("Service");
        result[0].Description.ShouldBe("Graph database");
        result[0].Aliases.ShouldBe(["Neptune", "AWS Neptune"]);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_EmptyResult_ReturnsEmptyList()
    {
        _llmMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _extractor.ExtractEntitiesAsync("No entities here");

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExtractEntitiesAsync_ExceptionPropagates()
    {
        _llmMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM failure"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => _extractor.ExtractEntitiesAsync("text"));
    }

    [Fact]
    public async Task ExtractEntitiesAsync_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _llmMock
            .Setup(s => s.ExtractEntitiesAsync("chunk text", token))
            .ReturnsAsync([]);

        await _extractor.ExtractEntitiesAsync("chunk text", token);

        _llmMock.Verify(
            s => s.ExtractEntitiesAsync("chunk text", token),
            Times.Once);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_NullDescriptionAndEmptyAliases_MappedCorrectly()
    {
        _llmMock
            .Setup(s => s.ExtractEntitiesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new BedrockEntity
                {
                    Name = "C#",
                    Type = "Language",
                    Description = null,
                    Aliases = []
                }
            ]);

        var result = await _extractor.ExtractEntitiesAsync("C# programming");

        result.Count.ShouldBe(1);
        result[0].Description.ShouldBeNull();
        result[0].Aliases.ShouldBeEmpty();
    }
}
