using CompoundDocs.McpServer.Options;
using CompoundDocs.McpServer.SemanticKernel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CompoundDocs.Tests.SemanticKernel;

/// <summary>
/// Unit tests for EmbeddingService.
/// </summary>
public sealed class EmbeddingServiceTests
{
    private readonly Mock<IEmbeddingGenerator<string, Embedding<float>>> _mockGenerator;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly IOptions<CompoundDocsServerOptions> _options;

    public EmbeddingServiceTests()
    {
        _mockGenerator = new Mock<IEmbeddingGenerator<string, Embedding<float>>>(MockBehavior.Loose);
        _logger = NullLogger<EmbeddingService>.Instance;
        _options = Options.Create(new CompoundDocsServerOptions());
    }

    private EmbeddingService CreateService() =>
        new(_mockGenerator.Object, _options, _logger);

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullGenerator_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new EmbeddingService(null!, _options, _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new EmbeddingService(_mockGenerator.Object, _options, null!));
    }

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Act
        var service = CreateService();

        // Assert
        service.ShouldNotBeNull();
    }

    #endregion

    #region Dimensions Property Tests

    [Fact]
    public void Dimensions_ReturnsExpectedValue()
    {
        // Arrange
        var service = CreateService();

        // Act
        var dimensions = service.Dimensions;

        // Assert
        dimensions.ShouldBe(OllamaConnectionOptions.EmbeddingDimensions);
        dimensions.ShouldBe(1024);
    }

    #endregion

    #region GenerateEmbeddingAsync Tests

    [Fact]
    public async Task GenerateEmbeddingAsync_WithValidContent_ReturnsEmbedding()
    {
        // Arrange
        var service = CreateService();
        var content = "This is test content for embedding";
        var expectedVector = CreateValidEmbedding();
        var embedding = new Embedding<float>(expectedVector);

        _mockGenerator
            .Setup(g => g.GenerateAsync(
                It.Is<IEnumerable<string>>(s => s.Single() == content),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([embedding]));

        // Act
        var result = await service.GenerateEmbeddingAsync(content);

        // Assert
        result.Length.ShouldBe(1024);
        result.ToArray().ShouldBe(expectedVector);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithNullContent_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await service.GenerateEmbeddingAsync(null!));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithEmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await service.GenerateEmbeddingAsync(string.Empty));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithWhitespaceContent_ThrowsArgumentException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await service.GenerateEmbeddingAsync("   "));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WithDimensionMismatch_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        var content = "Test content";
        var wrongSizeEmbedding = new float[512]; // Wrong size
        var embedding = new Embedding<float>(wrongSizeEmbedding);

        _mockGenerator
            .Setup(g => g.GenerateAsync(
                It.Is<IEnumerable<string>>(s => s.Single() == content),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([embedding]));

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.GenerateEmbeddingAsync(content));
        exception.Message.ShouldContain("dimension mismatch");
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WhenGeneratorThrows_PropagatesException()
    {
        // Arrange
        var service = CreateService();
        var content = "Test content";

        _mockGenerator
            .Setup(g => g.GenerateAsync(
                It.Is<IEnumerable<string>>(s => s.Single() == content),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(async () =>
            await service.GenerateEmbeddingAsync(content));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_RespectsCancellationToken()
    {
        // Arrange
        var service = CreateService();
        var content = "Test content";
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockGenerator
            .Setup(g => g.GenerateAsync(
                It.Is<IEnumerable<string>>(s => s.Single() == content),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await service.GenerateEmbeddingAsync(content, cts.Token));
    }

    #endregion

    #region GenerateEmbeddingsAsync Tests

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithValidContents_ReturnsEmbeddings()
    {
        // Arrange
        var service = CreateService();
        var contents = new List<string> { "Content 1", "Content 2", "Content 3" };
        var embeddings = contents.Select(_ => new Embedding<float>(CreateValidEmbedding())).ToList();

        _mockGenerator
            .Setup(g => g.GenerateAsync(
                It.Is<IEnumerable<string>>(s => s.SequenceEqual(contents)),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(embeddings));

        // Act
        var results = await service.GenerateEmbeddingsAsync(contents);

        // Assert
        results.Count.ShouldBe(3);
        results.All(e => e.Length == 1024).ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var service = CreateService();
        var contents = new List<string>();

        // Act
        var results = await service.GenerateEmbeddingsAsync(contents);

        // Assert
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithNullList_ThrowsArgumentNullException()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await service.GenerateEmbeddingsAsync(null!));
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithSingleItem_ReturnsOneEmbedding()
    {
        // Arrange
        var service = CreateService();
        var contents = new List<string> { "Single content" };
        var embedding = new Embedding<float>(CreateValidEmbedding());

        _mockGenerator
            .Setup(g => g.GenerateAsync(
                It.Is<IEnumerable<string>>(s => s.SequenceEqual(contents)),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>([embedding]));

        // Act
        var results = await service.GenerateEmbeddingsAsync(contents);

        // Assert
        results.Count.ShouldBe(1);
        results[0].Length.ShouldBe(1024);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WithDimensionMismatch_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        var contents = new List<string> { "Content 1", "Content 2" };
        var embeddings = new List<Embedding<float>>
        {
            new(CreateValidEmbedding()),
            new(new float[512]) // Wrong size
        };

        _mockGenerator
            .Setup(g => g.GenerateAsync(
                It.Is<IEnumerable<string>>(s => s.SequenceEqual(contents)),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratedEmbeddings<Embedding<float>>(embeddings));

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await service.GenerateEmbeddingsAsync(contents));
        exception.Message.ShouldContain("dimension mismatch");
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_WhenGeneratorThrows_PropagatesException()
    {
        // Arrange
        var service = CreateService();
        var contents = new List<string> { "Content 1", "Content 2" };

        _mockGenerator
            .Setup(g => g.GenerateAsync(
                It.Is<IEnumerable<string>>(s => s.SequenceEqual(contents)),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection failed"));

        // Act & Assert
        await Should.ThrowAsync<HttpRequestException>(async () =>
            await service.GenerateEmbeddingsAsync(contents));
    }

    #endregion

    #region Helper Methods

    private static float[] CreateValidEmbedding()
    {
        var embedding = new float[1024];
        for (int i = 0; i < 1024; i++)
        {
            embedding[i] = (float)(i * 0.001);
        }
        return embedding;
    }

    #endregion
}
