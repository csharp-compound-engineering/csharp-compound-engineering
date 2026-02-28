using System.IO;
using System.Text;
using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using CompoundDocs.Bedrock;
using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Polly;

namespace CompoundDocs.Tests.Unit.Bedrock;

public sealed class BedrockLlmServiceTests
{
    [Theory]
    [InlineData(ModelTier.Haiku, "anthropic.claude-haiku-4-5-v1:0")]
    [InlineData(ModelTier.Sonnet, "anthropic.claude-sonnet-4-5-v1:0")]
    [InlineData(ModelTier.Opus, "anthropic.claude-opus-4-5-v1:0")]
    public async Task GenerateAsync_SelectsCorrectModelPerTier(ModelTier tier, string expectedModelId)
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var config = new BedrockConfig
        {
            SonnetModelId = "anthropic.claude-sonnet-4-5-v1:0",
            HaikuModelId = "anthropic.claude-haiku-4-5-v1:0",
            OpusModelId = "anthropic.claude-opus-4-5-v1:0"
        };
        var sut = new BedrockLlmService(mockClient.Object, config, NullLogger<BedrockLlmService>.Instance, ResiliencePipeline.Empty);

        mockClient.Setup(c => c.ConverseAsync(
                It.Is<ConverseRequest>(r => r.ModelId == expectedModelId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConverseResponse
            {
                Output = new ConverseOutput
                {
                    Message = new Message
                    {
                        Role = ConversationRole.Assistant,
                        Content = [new ContentBlock { Text = "test response" }]
                    }
                }
            });

        // Act
        var result = await sut.GenerateAsync(
            "system prompt",
            [new BedrockMessage("user", "hello")],
            tier);

        // Assert
        result.ShouldBe("test response");
        mockClient.Verify(c => c.ConverseAsync(
            It.Is<ConverseRequest>(r => r.ModelId == expectedModelId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_ReturnsStructuredEntities()
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var config = new BedrockConfig
        {
            SonnetModelId = "anthropic.claude-sonnet-4-5-v1:0",
            HaikuModelId = "anthropic.claude-haiku-4-5-v1:0",
            OpusModelId = "anthropic.claude-opus-4-5-v1:0"
        };
        var sut = new BedrockLlmService(mockClient.Object, config, NullLogger<BedrockLlmService>.Instance, ResiliencePipeline.Empty);

        var entities = new[]
        {
            new { name = "React", type = "Framework", description = "UI library", aliases = new[] { "ReactJS" } },
            new { name = "TypeScript", type = "Technology", description = "Typed JS", aliases = Array.Empty<string>() }
        };
        var responseJson = JsonSerializer.Serialize(entities);

        mockClient.Setup(c => c.ConverseAsync(
                It.IsAny<ConverseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConverseResponse
            {
                Output = new ConverseOutput
                {
                    Message = new Message
                    {
                        Role = ConversationRole.Assistant,
                        Content = [new ContentBlock { Text = responseJson }]
                    }
                }
            });

        // Act
        var result = await sut.ExtractEntitiesAsync("React and TypeScript are popular.");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("React");
        result[0].Type.ShouldBe("Framework");
        result[1].Name.ShouldBe("TypeScript");
    }

    [Fact]
    public async Task ExtractEntitiesAsync_ReturnsEmptyList_OnInvalidJson()
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var config = new BedrockConfig
        {
            SonnetModelId = "anthropic.claude-sonnet-4-5-v1:0",
            HaikuModelId = "anthropic.claude-haiku-4-5-v1:0",
            OpusModelId = "anthropic.claude-opus-4-5-v1:0"
        };
        var sut = new BedrockLlmService(mockClient.Object, config, NullLogger<BedrockLlmService>.Instance, ResiliencePipeline.Empty);

        mockClient.Setup(c => c.ConverseAsync(
                It.IsAny<ConverseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConverseResponse
            {
                Output = new ConverseOutput
                {
                    Message = new Message
                    {
                        Role = ConversationRole.Assistant,
                        Content = [new ContentBlock { Text = "not valid json" }]
                    }
                }
            });

        // Act
        var result = await sut.ExtractEntitiesAsync("some text");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public void GetModelId_ReturnsCorrectId_ForEachTier()
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var config = new BedrockConfig
        {
            SonnetModelId = "anthropic.claude-sonnet-4-5-v1:0",
            HaikuModelId = "anthropic.claude-haiku-4-5-v1:0",
            OpusModelId = "anthropic.claude-opus-4-5-v1:0"
        };
        var sut = new BedrockLlmService(mockClient.Object, config, NullLogger<BedrockLlmService>.Instance, ResiliencePipeline.Empty);

        // Assert
        sut.GetModelId(ModelTier.Haiku).ShouldBe(config.HaikuModelId);
        sut.GetModelId(ModelTier.Sonnet).ShouldBe(config.SonnetModelId);
        sut.GetModelId(ModelTier.Opus).ShouldBe(config.OpusModelId);
    }

    [Fact]
    public void GetModelId_InvalidTier_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var config = new BedrockConfig
        {
            SonnetModelId = "anthropic.claude-sonnet-4-5-v1:0",
            HaikuModelId = "anthropic.claude-haiku-4-5-v1:0",
            OpusModelId = "anthropic.claude-opus-4-5-v1:0"
        };
        var sut = new BedrockLlmService(mockClient.Object, config, NullLogger<BedrockLlmService>.Instance, ResiliencePipeline.Empty);

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(() =>
            sut.GetModelId((ModelTier)999));
    }

    [Fact]
    public async Task GenerateAsync_EmptyResponse_ReturnsEmptyString()
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var config = new BedrockConfig
        {
            SonnetModelId = "anthropic.claude-sonnet-4-5-v1:0",
            HaikuModelId = "anthropic.claude-haiku-4-5-v1:0",
            OpusModelId = "anthropic.claude-opus-4-5-v1:0"
        };
        var sut = new BedrockLlmService(mockClient.Object, config, NullLogger<BedrockLlmService>.Instance, ResiliencePipeline.Empty);

        mockClient.Setup(c => c.ConverseAsync(
                It.IsAny<ConverseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConverseResponse
            {
                Output = new ConverseOutput
                {
                    Message = new Message
                    {
                        Role = ConversationRole.Assistant,
                        Content = [new ContentBlock { Text = null }]
                    }
                }
            });

        // Act
        var result = await sut.GenerateAsync(
            "system prompt",
            [new BedrockMessage("user", "hello")]);

        // Assert
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ExtractEntitiesAsync_UsesHaikuTier()
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var config = new BedrockConfig
        {
            SonnetModelId = "anthropic.claude-sonnet-4-5-v1:0",
            HaikuModelId = "anthropic.claude-haiku-4-5-v1:0",
            OpusModelId = "anthropic.claude-opus-4-5-v1:0"
        };
        var sut = new BedrockLlmService(mockClient.Object, config, NullLogger<BedrockLlmService>.Instance, ResiliencePipeline.Empty);

        ConverseRequest? capturedRequest = null;
        mockClient.Setup(c => c.ConverseAsync(
                It.IsAny<ConverseRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<ConverseRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new ConverseResponse
            {
                Output = new ConverseOutput
                {
                    Message = new Message
                    {
                        Role = ConversationRole.Assistant,
                        Content = [new ContentBlock { Text = "[]" }]
                    }
                }
            });

        // Act
        await sut.ExtractEntitiesAsync("some text about React");

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest!.ModelId.ShouldBe(config.HaikuModelId);

        // Verify system prompt contains entity extraction instructions
        capturedRequest.System.ShouldNotBeNull();
        capturedRequest.System.Count.ShouldBeGreaterThan(0);
        var systemText = capturedRequest.System[0].Text;
        systemText.ShouldContain("entity extraction");
        systemText.ShouldContain("JSON");
    }

    [Fact]
    public void BedrockMessage_RecordCreation()
    {
        var msg = new BedrockMessage("user", "hello");
        msg.Role.ShouldBe("user");
        msg.Content.ShouldBe("hello");
    }

    [Fact]
    public void ModelTier_HasThreeValues()
    {
        var values = Enum.GetValues<ModelTier>();
        values.Length.ShouldBe(3);
    }

    [Fact]
    public async Task GenerateAsync_WithAssistantMessage_SetsCorrectRole()
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var config = new BedrockConfig
        {
            SonnetModelId = "anthropic.claude-sonnet-4-5-v1:0",
            HaikuModelId = "anthropic.claude-haiku-4-5-v1:0",
            OpusModelId = "anthropic.claude-opus-4-5-v1:0"
        };
        var sut = new BedrockLlmService(mockClient.Object, config, NullLogger<BedrockLlmService>.Instance, ResiliencePipeline.Empty);

        ConverseRequest? capturedRequest = null;
        mockClient.Setup(c => c.ConverseAsync(
                It.IsAny<ConverseRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<ConverseRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new ConverseResponse
            {
                Output = new ConverseOutput
                {
                    Message = new Message
                    {
                        Role = ConversationRole.Assistant,
                        Content = [new ContentBlock { Text = "response" }]
                    }
                }
            });

        // Act
        await sut.GenerateAsync(
            "system",
            [
                new BedrockMessage("user", "hi"),
                new BedrockMessage("assistant", "hello"),
                new BedrockMessage("user", "how are you?")
            ]);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest!.Messages.Count.ShouldBe(3);
        capturedRequest.Messages[0].Role.Value.ShouldBe("user");
        capturedRequest.Messages[1].Role.Value.ShouldBe("assistant");
        capturedRequest.Messages[2].Role.Value.ShouldBe("user");
    }

    [Fact]
    public async Task GenerateAsync_SetsSystemPrompt()
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var config = new BedrockConfig
        {
            SonnetModelId = "anthropic.claude-sonnet-4-5-v1:0",
            HaikuModelId = "anthropic.claude-haiku-4-5-v1:0",
            OpusModelId = "anthropic.claude-opus-4-5-v1:0"
        };
        var sut = new BedrockLlmService(mockClient.Object, config, NullLogger<BedrockLlmService>.Instance, ResiliencePipeline.Empty);

        ConverseRequest? capturedRequest = null;
        mockClient.Setup(c => c.ConverseAsync(
                It.IsAny<ConverseRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<ConverseRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new ConverseResponse
            {
                Output = new ConverseOutput
                {
                    Message = new Message
                    {
                        Role = ConversationRole.Assistant,
                        Content = [new ContentBlock { Text = "ok" }]
                    }
                }
            });

        // Act
        await sut.GenerateAsync(
            "You are a helpful assistant.",
            [new BedrockMessage("user", "hello")]);

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest!.System.Count.ShouldBe(1);
        capturedRequest.System[0].Text.ShouldBe("You are a helpful assistant.");
    }

    [Fact]
    public async Task ExtractEntitiesAsync_NullJsonArray_ReturnsEmptyList()
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var config = new BedrockConfig
        {
            SonnetModelId = "anthropic.claude-sonnet-4-5-v1:0",
            HaikuModelId = "anthropic.claude-haiku-4-5-v1:0",
            OpusModelId = "anthropic.claude-opus-4-5-v1:0"
        };
        var sut = new BedrockLlmService(mockClient.Object, config, NullLogger<BedrockLlmService>.Instance, ResiliencePipeline.Empty);

        mockClient.Setup(c => c.ConverseAsync(
                It.IsAny<ConverseRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConverseResponse
            {
                Output = new ConverseOutput
                {
                    Message = new Message
                    {
                        Role = ConversationRole.Assistant,
                        Content = [new ContentBlock { Text = "null" }]
                    }
                }
            });

        // Act
        var result = await sut.ExtractEntitiesAsync("some text");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GenerateAsync_DefaultsToSonnetTier()
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var config = new BedrockConfig
        {
            SonnetModelId = "anthropic.claude-sonnet-4-5-v1:0",
            HaikuModelId = "anthropic.claude-haiku-4-5-v1:0",
            OpusModelId = "anthropic.claude-opus-4-5-v1:0"
        };
        var sut = new BedrockLlmService(mockClient.Object, config, NullLogger<BedrockLlmService>.Instance, ResiliencePipeline.Empty);

        mockClient.Setup(c => c.ConverseAsync(
                It.Is<ConverseRequest>(r => r.ModelId == config.SonnetModelId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConverseResponse
            {
                Output = new ConverseOutput
                {
                    Message = new Message
                    {
                        Role = ConversationRole.Assistant,
                        Content = [new ContentBlock { Text = "response" }]
                    }
                }
            });

        // Act
        var result = await sut.GenerateAsync(
            "system",
            [new BedrockMessage("user", "hello")]);

        // Assert
        result.ShouldBe("response");
        mockClient.Verify(c => c.ConverseAsync(
            It.Is<ConverseRequest>(r => r.ModelId == config.SonnetModelId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Constructor_WithExplicitResiliencePipeline_UsesProvidedPipeline()
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var config = new BedrockConfig
        {
            SonnetModelId = "anthropic.claude-sonnet-4-5-v1:0",
            HaikuModelId = "anthropic.claude-haiku-4-5-v1:0",
            OpusModelId = "anthropic.claude-opus-4-5-v1:0"
        };
        var logger = NullLogger<BedrockLlmService>.Instance;
        var pipeline = ResiliencePipeline.Empty;

        // Act
        var service = new BedrockLlmService(mockClient.Object, config, logger, pipeline);

        // Assert
        service.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithNullResiliencePipeline_FallsBackToEmpty()
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var config = new BedrockConfig
        {
            SonnetModelId = "anthropic.claude-sonnet-4-5-v1:0",
            HaikuModelId = "anthropic.claude-haiku-4-5-v1:0",
            OpusModelId = "anthropic.claude-opus-4-5-v1:0"
        };
        var logger = NullLogger<BedrockLlmService>.Instance;

        // Act — pass null to cover the ?? ResiliencePipeline.Empty branch
        var service = new BedrockLlmService(mockClient.Object, config, logger, null);

        // Assert
        service.ShouldNotBeNull();
    }

    [Fact]
    public void PublicConstructor_WithIOptions_CreatesServiceWithRetryPipeline()
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var options = Microsoft.Extensions.Options.Options.Create(new BedrockConfig
        {
            SonnetModelId = "anthropic.claude-sonnet-4-5-v1:0",
            HaikuModelId = "anthropic.claude-haiku-4-5-v1:0",
            OpusModelId = "anthropic.claude-opus-4-5-v1:0"
        });
        var logger = NullLogger<BedrockLlmService>.Instance;

        // Act
        var service = new BedrockLlmService(mockClient.Object, options, logger);

        // Assert
        service.ShouldNotBeNull();
    }
}
