using CompoundDocs.McpServer.Resilience;
using CompoundDocs.McpServer.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;

namespace CompoundDocs.Tests.Resilience;

/// <summary>
/// Unit tests for ResilientEmbeddingService.
/// </summary>
public sealed class ResilientEmbeddingServiceTests : IDisposable
{
    private readonly Mock<IEmbeddingService> _innerServiceMock;
    private readonly Mock<IResiliencePolicies> _resiliencePoliciesMock;
    private readonly EmbeddingCache _embeddingCache;
    private readonly ILogger<ResilientEmbeddingService> _logger;

    public ResilientEmbeddingServiceTests()
    {
        _innerServiceMock = new Mock<IEmbeddingService>(MockBehavior.Strict);
        _resiliencePoliciesMock = new Mock<IResiliencePolicies>(MockBehavior.Strict);
        _embeddingCache = new EmbeddingCache(
            Options.Create(new EmbeddingCacheOptions { Enabled = true, MaxCachedItems = 100 }),
            NullLogger<EmbeddingCache>.Instance);
        _logger = NullLogger<ResilientEmbeddingService>.Instance;

        // Setup default pipeline behavior - execute the operation directly
        _resiliencePoliciesMock
            .Setup(p => p.ExecuteWithOllamaResilienceAsync(
                It.IsAny<Func<CancellationToken, Task<ReadOnlyMemory<float>>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<ReadOnlyMemory<float>>>, CancellationToken>(
                (operation, ct) => operation(ct));

        _resiliencePoliciesMock
            .Setup(p => p.ExecuteWithOllamaResilienceAsync(
                It.IsAny<Func<CancellationToken, Task<IReadOnlyList<ReadOnlyMemory<float>>>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<IReadOnlyList<ReadOnlyMemory<float>>>>, CancellationToken>(
                (operation, ct) => operation(ct));
    }

    [Fact]
    public void Constructor_WithNullInnerService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ResilientEmbeddingService(
            null!,
            _resiliencePoliciesMock.Object,
            _embeddingCache,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullResiliencePolicies_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ResilientEmbeddingService(
            _innerServiceMock.Object,
            null!,
            _embeddingCache,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullCache_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ResilientEmbeddingService(
            _innerServiceMock.Object,
            _resiliencePoliciesMock.Object,
            null!,
            _logger));
    }

    [Fact]
    public void Dimensions_ReturnsInnerServiceDimensions()
    {
        // Arrange
        _innerServiceMock.Setup(s => s.Dimensions).Returns(1024);
        var service = CreateService();

        // Act
        var dimensions = service.Dimensions;

        // Assert
        dimensions.ShouldBe(1024);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_SuccessfulCall_ReturnsEmbeddingAndCaches()
    {
        // Arrange
        var content = "test content";
        var expectedEmbedding = CreateTestEmbedding(1024);

        _innerServiceMock
            .Setup(s => s.GenerateEmbeddingAsync(content, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding);

        var service = CreateService();

        // Act
        var result = await service.GenerateEmbeddingAsync(content);

        // Assert
        result.Length.ShouldBe(1024);
        _embeddingCache.TryGet(content, out var cached).ShouldBeTrue();
        cached.Length.ShouldBe(1024);
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
            await service.GenerateEmbeddingAsync(""));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WhenCircuitBreakerOpen_UsesCachedEmbedding()
    {
        // Arrange
        var content = "cached content";
        var cachedEmbedding = CreateTestEmbedding(1024);
        _embeddingCache.Set(content, cachedEmbedding);

        _resiliencePoliciesMock
            .Setup(p => p.ExecuteWithOllamaResilienceAsync(
                It.IsAny<Func<CancellationToken, Task<ReadOnlyMemory<float>>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BrokenCircuitException("Circuit is open"));

        var service = CreateService();

        // Act
        var result = await service.GenerateEmbeddingAsync(content);

        // Assert
        result.Length.ShouldBe(1024);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WhenCircuitBreakerOpenAndNoCache_ThrowsOllamaUnavailableException()
    {
        // Arrange
        var content = "uncached content";

        _resiliencePoliciesMock
            .Setup(p => p.ExecuteWithOllamaResilienceAsync(
                It.IsAny<Func<CancellationToken, Task<ReadOnlyMemory<float>>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new BrokenCircuitException("Circuit is open"));

        var service = CreateService();

        // Act & Assert
        var exception = await Should.ThrowAsync<OllamaUnavailableException>(async () =>
            await service.GenerateEmbeddingAsync(content));

        exception.Message.ShouldContain("unavailable");
        exception.Reason.ShouldContain("Circuit breaker");
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_WhenHttpRequestException_ThrowsOllamaUnavailableException()
    {
        // Arrange
        var content = "content";

        _resiliencePoliciesMock
            .Setup(p => p.ExecuteWithOllamaResilienceAsync(
                It.IsAny<Func<CancellationToken, Task<ReadOnlyMemory<float>>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var service = CreateService();

        // Act & Assert
        var exception = await Should.ThrowAsync<OllamaUnavailableException>(async () =>
            await service.GenerateEmbeddingAsync(content));

        exception.Reason.ShouldContain("Connection");
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_EmptyInput_ReturnsEmptyList()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GenerateEmbeddingsAsync(Array.Empty<string>());

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_AllCached_DoesNotCallInnerService()
    {
        // Arrange
        var contents = new[] { "content1", "content2" };
        _embeddingCache.Set("content1", CreateTestEmbedding(1024, seed: 1));
        _embeddingCache.Set("content2", CreateTestEmbedding(1024, seed: 2));

        var service = CreateService();

        // Act
        var result = await service.GenerateEmbeddingsAsync(contents);

        // Assert
        result.Count.ShouldBe(2);
        _innerServiceMock.Verify(
            s => s.GenerateEmbeddingsAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_PartialCache_OnlyFetchesUncached()
    {
        // Arrange
        var contents = new[] { "cached", "uncached" };
        var cachedEmbedding = CreateTestEmbedding(1024, seed: 1);
        var newEmbedding = CreateTestEmbedding(1024, seed: 2);
        _embeddingCache.Set("cached", cachedEmbedding);

        _innerServiceMock
            .Setup(s => s.GenerateEmbeddingsAsync(
                It.Is<IReadOnlyList<string>>(list => list.Count == 1 && list[0] == "uncached"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { newEmbedding });

        var service = CreateService();

        // Act
        var result = await service.GenerateEmbeddingsAsync(contents);

        // Assert
        result.Count.ShouldBe(2);
        _innerServiceMock.Verify(
            s => s.GenerateEmbeddingsAsync(
                It.Is<IReadOnlyList<string>>(list => list.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_SuccessfulCall_CachesNewEmbeddings()
    {
        // Arrange
        var contents = new[] { "new1", "new2" };
        var embeddings = new[] { CreateTestEmbedding(1024, seed: 1), CreateTestEmbedding(1024, seed: 2) };

        _innerServiceMock
            .Setup(s => s.GenerateEmbeddingsAsync(contents, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddings);

        var service = CreateService();

        // Act
        await service.GenerateEmbeddingsAsync(contents);

        // Assert
        _embeddingCache.TryGet("new1", out _).ShouldBeTrue();
        _embeddingCache.TryGet("new2", out _).ShouldBeTrue();
    }

    [Fact]
    public void IsOllamaAvailable_InitiallyTrue()
    {
        // Arrange
        var service = CreateService();

        // Assert
        service.IsOllamaAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_AfterHttpFailure_MarksOllamaUnavailable()
    {
        // Arrange
        var content = "content";
        _embeddingCache.Set(content, CreateTestEmbedding(1024)); // Ensure fallback works

        _resiliencePoliciesMock
            .Setup(p => p.ExecuteWithOllamaResilienceAsync(
                It.IsAny<Func<CancellationToken, Task<ReadOnlyMemory<float>>>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var service = CreateService();

        // Act
        await service.GenerateEmbeddingAsync(content);

        // Assert
        service.IsOllamaAvailable.ShouldBeFalse();
    }

    private ResilientEmbeddingService CreateService()
    {
        return new ResilientEmbeddingService(
            _innerServiceMock.Object,
            _resiliencePoliciesMock.Object,
            _embeddingCache,
            _logger);
    }

    private static ReadOnlyMemory<float> CreateTestEmbedding(int dimensions, int seed = 0)
    {
        var random = new Random(seed);
        var values = new float[dimensions];
        for (var i = 0; i < dimensions; i++)
        {
            values[i] = (float)random.NextDouble();
        }
        return new ReadOnlyMemory<float>(values);
    }

    public void Dispose()
    {
        _embeddingCache.Dispose();
    }
}
