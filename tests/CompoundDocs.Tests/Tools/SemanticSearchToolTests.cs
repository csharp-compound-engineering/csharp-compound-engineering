using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.SemanticKernel;
using CompoundDocs.McpServer.Session;
using CompoundDocs.McpServer.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Tools;

/// <summary>
/// Unit tests for SemanticSearchTool.
/// </summary>
public sealed class SemanticSearchToolTests
{
    private readonly Mock<IDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<ISessionContext> _sessionContextMock;
    private readonly ILogger<SemanticSearchTool> _logger;
    private readonly SemanticSearchTool _tool;

    public SemanticSearchToolTests()
    {
        _documentRepositoryMock = new Mock<IDocumentRepository>(MockBehavior.Strict);
        _embeddingServiceMock = new Mock<IEmbeddingService>(MockBehavior.Strict);
        _sessionContextMock = new Mock<ISessionContext>(MockBehavior.Strict);
        _logger = NullLogger<SemanticSearchTool>.Instance;

        _tool = new SemanticSearchTool(
            _documentRepositoryMock.Object,
            _embeddingServiceMock.Object,
            _sessionContextMock.Object,
            _logger);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullDocumentRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new SemanticSearchTool(
            null!,
            _embeddingServiceMock.Object,
            _sessionContextMock.Object,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullEmbeddingService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new SemanticSearchTool(
            _documentRepositoryMock.Object,
            null!,
            _sessionContextMock.Object,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullSessionContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new SemanticSearchTool(
            _documentRepositoryMock.Object,
            _embeddingServiceMock.Object,
            null!,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new SemanticSearchTool(
            _documentRepositoryMock.Object,
            _embeddingServiceMock.Object,
            _sessionContextMock.Object,
            null!));
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_NoActiveProject_ReturnsNoActiveProjectError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(false);

        // Act
        var result = await _tool.SearchAsync("test query");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NO_ACTIVE_PROJECT");
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmptyQueryError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.SearchAsync("");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public async Task SearchAsync_WhitespaceQuery_ReturnsEmptyQueryError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.SearchAsync("   ");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public async Task SearchAsync_NullQuery_ReturnsEmptyQueryError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.SearchAsync(null!);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public async Task SearchAsync_InvalidPromotionLevel_ReturnsInvalidPromotionLevelError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.SearchAsync("test query", promotionLevel: "invalid");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("INVALID_PROMOTION_LEVEL");
    }

    [Fact]
    public async Task SearchAsync_InvalidDocType_ReturnsInvalidDocTypeError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.SearchAsync("test query", docTypes: "invalid_type");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("INVALID_DOC_TYPE");
    }

    [Fact]
    public async Task SearchAsync_ValidQuery_ReturnsResults()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f });
        _embeddingServiceMock
            .Setup(e => e.GenerateEmbeddingAsync("test query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _documentRepositoryMock
            .Setup(r => r.SearchAsync(
                embedding, "test-tenant", It.IsAny<int>(), It.IsAny<float>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResult>());

        // Act
        var result = await _tool.SearchAsync("test query");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.Query.ShouldBe("test query");
    }

    [Fact]
    public async Task SearchAsync_LimitNormalized_WhenLessThanOrEqualZero()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f });
        _embeddingServiceMock
            .Setup(e => e.GenerateEmbeddingAsync("test query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _documentRepositoryMock
            .Setup(r => r.SearchAsync(
                embedding, "test-tenant", 10, It.IsAny<float>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResult>());

        // Act
        var result = await _tool.SearchAsync("test query", limit: -5);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task SearchAsync_LimitCapped_WhenGreaterThan100()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f });
        _embeddingServiceMock
            .Setup(e => e.GenerateEmbeddingAsync("test query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _documentRepositoryMock
            .Setup(r => r.SearchAsync(
                embedding, "test-tenant", 100, It.IsAny<float>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResult>());

        // Act
        var result = await _tool.SearchAsync("test query", limit: 500);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task SearchAsync_WithCancellation_ReturnsOperationCancelledError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _embeddingServiceMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _tool.SearchAsync("test query", cancellationToken: cts.Token);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("OPERATION_CANCELLED");
    }

    [Fact]
    public async Task SearchAsync_EmbeddingFailure_ReturnsSearchFailedError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        _embeddingServiceMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Embedding service failed"));

        // Act
        var result = await _tool.SearchAsync("test query");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("SEARCH_FAILED");
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("Standard")]
    [InlineData("important")]
    [InlineData("Important")]
    [InlineData("critical")]
    [InlineData("Critical")]
    public async Task SearchAsync_ValidPromotionLevels_AreAccepted(string promotionLevel)
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f });
        _embeddingServiceMock
            .Setup(e => e.GenerateEmbeddingAsync("test query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _documentRepositoryMock
            .Setup(r => r.SearchAsync(
                embedding, "test-tenant", It.IsAny<int>(), It.IsAny<float>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SearchResult>());

        // Act
        var result = await _tool.SearchAsync("test query", promotionLevel: promotionLevel);

        // Assert
        result.Success.ShouldBeTrue();
    }

    #endregion
}
