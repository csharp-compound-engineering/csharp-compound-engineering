using Moq;
using CompoundDocs.Bedrock;

namespace CompoundDocs.Tests.Integration.Bedrock;

/// <summary>
/// Mock-based integration tests for IBedrockLlmService.
/// Validates the Converse API contract for LLM generation without requiring AWS Bedrock infrastructure.
/// </summary>
public class BedrockLlmMockTests
{
    private readonly Mock<IBedrockLlmService> _llmServiceMock;

    public BedrockLlmMockTests()
    {
        _llmServiceMock = new Mock<IBedrockLlmService>(MockBehavior.Strict);
    }

    [Fact]
    public async Task ConverseApi_WithMockedBedrock_ReturnsResponse()
    {
        // Arrange
        var systemPrompt = "You are a technical documentation assistant. Answer questions based on the provided context. Cite your sources.";
        var messages = new List<BedrockMessage>
        {
            new("user", "What is dependency injection in .NET?")
        };
        var tier = ModelTier.Sonnet;

        var expectedResponse = "Dependency injection (DI) is a design pattern in .NET that achieves "
            + "Inversion of Control (IoC) between classes and their dependencies. The .NET framework "
            + "provides a built-in DI container through `Microsoft.Extensions.DependencyInjection`. "
            + "Services are registered in the `IServiceCollection` and resolved via `IServiceProvider`. "
            + "[Source: docs/architecture/di-overview.md]";

        string? capturedSystemPrompt = null;
        IReadOnlyList<BedrockMessage>? capturedMessages = null;
        ModelTier? capturedTier = null;

        _llmServiceMock
            .Setup(l => l.GenerateAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<BedrockMessage>>(),
                It.IsAny<ModelTier>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<BedrockMessage>, ModelTier, CancellationToken>(
                (sp, msgs, t, _) =>
                {
                    capturedSystemPrompt = sp;
                    capturedMessages = msgs;
                    capturedTier = t;
                })
            .ReturnsAsync(expectedResponse)
            .Verifiable();

        var service = _llmServiceMock.Object;

        // Act
        var result = await service.GenerateAsync(systemPrompt, messages, tier);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.ShouldBe(expectedResponse);
        result.ShouldContain("dependency injection", Case.Insensitive);
        result.ShouldContain("Source:");

        capturedSystemPrompt.ShouldBe(systemPrompt);
        capturedMessages.ShouldNotBeNull();
        capturedMessages!.Count.ShouldBe(1);
        capturedMessages[0].Role.ShouldBe("user");
        capturedMessages[0].Content.ShouldContain("dependency injection");
        capturedTier.ShouldBe(ModelTier.Sonnet);

        _llmServiceMock.Verify(
            l => l.GenerateAsync(
                systemPrompt,
                It.Is<IReadOnlyList<BedrockMessage>>(m => m.Count == 1 && m[0].Role == "user"),
                ModelTier.Sonnet,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
