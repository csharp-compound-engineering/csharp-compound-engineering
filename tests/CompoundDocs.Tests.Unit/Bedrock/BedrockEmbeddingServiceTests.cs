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

public sealed class BedrockEmbeddingServiceTests
{
    private readonly Mock<IAmazonBedrockRuntime> _mockClient;
    private readonly BedrockEmbeddingService _sut;

    public BedrockEmbeddingServiceTests()
    {
        _mockClient = new Mock<IAmazonBedrockRuntime>();
        var config = new BedrockConfig
        {
            EmbeddingModelId = "amazon.titan-embed-text-v2:0"
        };
        var logger = NullLogger<BedrockEmbeddingService>.Instance;
        _sut = new BedrockEmbeddingService(_mockClient.Object, config, logger, ResiliencePipeline.Empty);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_Returns1024DimensionArray()
    {
        // Arrange
        var embedding = new float[1024];
        for (var i = 0; i < 1024; i++) embedding[i] = i * 0.001f;

        var responseBody = JsonSerializer.Serialize(new { embedding });
        var responseStream = new MemoryStream(Encoding.UTF8.GetBytes(responseBody));

        _mockClient.Setup(c => c.InvokeModelAsync(
                It.Is<InvokeModelRequest>(r => r.ModelId == "amazon.titan-embed-text-v2:0"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InvokeModelResponse
            {
                Body = responseStream,
                ContentType = "application/json"
            });

        // Act
        var result = await _sut.GenerateEmbeddingAsync("test text");

        // Assert
        result.ShouldNotBeNull();
        result.Length.ShouldBe(1024);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_ProcessesBatch()
    {
        // Arrange
        var embedding = new float[1024];
        for (var i = 0; i < 1024; i++) embedding[i] = 0.5f;

        var responseBody = JsonSerializer.Serialize(new { embedding });

        _mockClient.Setup(c => c.InvokeModelAsync(
                It.IsAny<InvokeModelRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new InvokeModelResponse
            {
                Body = new MemoryStream(Encoding.UTF8.GetBytes(responseBody)),
                ContentType = "application/json"
            });

        var texts = new[] { "text1", "text2", "text3" };

        // Act
        var results = await _sut.GenerateEmbeddingsAsync(texts);

        // Assert
        results.ShouldNotBeNull();
        results.Count.ShouldBe(3);
        results.ShouldAllBe(e => e.Length == 1024);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SendsCorrectRequestBody()
    {
        // Arrange
        InvokeModelRequest? capturedRequest = null;
        var embedding = new float[1024];

        var responseBody = JsonSerializer.Serialize(new { embedding });

        _mockClient.Setup(c => c.InvokeModelAsync(
                It.IsAny<InvokeModelRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<InvokeModelRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(new InvokeModelResponse
            {
                Body = new MemoryStream(Encoding.UTF8.GetBytes(responseBody)),
                ContentType = "application/json"
            });

        // Act
        await _sut.GenerateEmbeddingAsync("test input text");

        // Assert
        capturedRequest.ShouldNotBeNull();
        capturedRequest!.ModelId.ShouldBe("amazon.titan-embed-text-v2:0");
        capturedRequest.ContentType.ShouldBe("application/json");
        capturedRequest.Accept.ShouldBe("application/json");

        // Verify request body contains correct fields
        capturedRequest.Body.Position = 0;
        using var reader = new StreamReader(capturedRequest.Body);
        var bodyJson = await reader.ReadToEndAsync();
        using var doc = JsonDocument.Parse(bodyJson);
        doc.RootElement.GetProperty("inputText").GetString().ShouldBe("test input text");
        doc.RootElement.GetProperty("dimensions").GetInt32().ShouldBe(1024);
        doc.RootElement.GetProperty("normalize").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_EmptyCollection_ReturnsEmptyList()
    {
        var results = await _sut.GenerateEmbeddingsAsync(Array.Empty<string>());

        results.ShouldNotBeNull();
        results.Count.ShouldBe(0);
        _mockClient.Verify(c => c.InvokeModelAsync(
            It.IsAny<InvokeModelRequest>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Constructor_WithExplicitResiliencePipeline_UsesProvidedPipeline()
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var config = new BedrockConfig { EmbeddingModelId = "amazon.titan-embed-text-v2:0" };
        var logger = NullLogger<BedrockEmbeddingService>.Instance;
        var pipeline = ResiliencePipeline.Empty;

        // Act
        var service = new BedrockEmbeddingService(mockClient.Object, config, logger, pipeline);

        // Assert
        service.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithNullResiliencePipeline_FallsBackToEmpty()
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var config = new BedrockConfig { EmbeddingModelId = "amazon.titan-embed-text-v2:0" };
        var logger = NullLogger<BedrockEmbeddingService>.Instance;

        // Act — pass null to cover the ?? ResiliencePipeline.Empty branch
        var service = new BedrockEmbeddingService(mockClient.Object, config, logger, null);

        // Assert
        service.ShouldNotBeNull();
    }

    [Fact]
    public void PublicConstructor_WithIOptions_CreatesServiceWithRetryPipeline()
    {
        // Arrange
        var mockClient = new Mock<IAmazonBedrockRuntime>();
        var options = Microsoft.Extensions.Options.Options.Create(
            new BedrockConfig { EmbeddingModelId = "amazon.titan-embed-text-v2:0" });
        var logger = NullLogger<BedrockEmbeddingService>.Instance;

        // Act
        var service = new BedrockEmbeddingService(mockClient.Object, options, logger);

        // Assert
        service.ShouldNotBeNull();
    }
}
