using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.SemanticKernel;
using CompoundDocs.McpServer.Session;
using CompoundDocs.McpServer.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Tools;

/// <summary>
/// Unit tests for RagQueryTool.
/// </summary>
public sealed class RagQueryToolTests
{
    private readonly Mock<IDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<ISessionContext> _sessionContextMock;
    private readonly ILogger<RagQueryTool> _logger;
    private readonly RagQueryTool _tool;

    public RagQueryToolTests()
    {
        _documentRepositoryMock = new Mock<IDocumentRepository>(MockBehavior.Strict);
        _embeddingServiceMock = new Mock<IEmbeddingService>(MockBehavior.Strict);
        _sessionContextMock = new Mock<ISessionContext>(MockBehavior.Strict);
        _logger = NullLogger<RagQueryTool>.Instance;

        _tool = new RagQueryTool(
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
        Should.Throw<ArgumentNullException>(() => new RagQueryTool(
            null!,
            _embeddingServiceMock.Object,
            _sessionContextMock.Object,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullEmbeddingService_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new RagQueryTool(
            _documentRepositoryMock.Object,
            null!,
            _sessionContextMock.Object,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullSessionContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new RagQueryTool(
            _documentRepositoryMock.Object,
            _embeddingServiceMock.Object,
            null!,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new RagQueryTool(
            _documentRepositoryMock.Object,
            _embeddingServiceMock.Object,
            _sessionContextMock.Object,
            null!));
    }

    #endregion

    #region QueryAsync Tests

    [Fact]
    public async Task QueryAsync_NoActiveProject_ReturnsNoActiveProjectError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(false);

        // Act
        var result = await _tool.QueryAsync("test query");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NO_ACTIVE_PROJECT");
    }

    [Fact]
    public async Task QueryAsync_EmptyQuery_ReturnsEmptyQueryError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.QueryAsync("");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public async Task QueryAsync_WhitespaceQuery_ReturnsEmptyQueryError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.QueryAsync("   ");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public async Task QueryAsync_NullQuery_ReturnsEmptyQueryError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.QueryAsync(null!);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("EMPTY_QUERY");
    }

    [Fact]
    public async Task QueryAsync_NoResults_ReturnsEmptyResult()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f });
        _embeddingServiceMock
            .Setup(e => e.GenerateEmbeddingAsync("test query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _documentRepositoryMock
            .Setup(r => r.SearchChunksAsync(
                embedding, "test-tenant", It.IsAny<int>(), It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkSearchResult>());

        // Act
        var result = await _tool.QueryAsync("test query");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.Query.ShouldBe("test query");
        result.Data.Sources.ShouldBeEmpty();
        result.Data.ConfidenceScore.ShouldBe(0.0f);
    }

    [Fact]
    public async Task QueryAsync_WithCancellation_ReturnsOperationCancelledError()
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
        var result = await _tool.QueryAsync("test query", cancellationToken: cts.Token);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("OPERATION_CANCELLED");
    }

    [Fact]
    public async Task QueryAsync_MaxResultsNormalized_WhenLessThanOrEqualZero()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f });
        _embeddingServiceMock
            .Setup(e => e.GenerateEmbeddingAsync("test query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _documentRepositoryMock
            .Setup(r => r.SearchChunksAsync(
                embedding, "test-tenant", 10, It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkSearchResult>());

        // Act
        var result = await _tool.QueryAsync("test query", maxResults: -1);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryAsync_MaxResultsCapped_WhenGreaterThan20()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        var embedding = new ReadOnlyMemory<float>(new float[] { 0.1f, 0.2f });
        _embeddingServiceMock
            .Setup(e => e.GenerateEmbeddingAsync("test query", It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _documentRepositoryMock
            .Setup(r => r.SearchChunksAsync(
                embedding, "test-tenant", 40, It.IsAny<float>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkSearchResult>());

        // Act
        var result = await _tool.QueryAsync("test query", maxResults: 100);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task QueryAsync_EmbeddingFailure_ReturnsRagSynthesisFailedError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        _embeddingServiceMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Embedding service failed"));

        // Act
        var result = await _tool.QueryAsync("test query");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("RAG_SYNTHESIS_FAILED");
    }

    #endregion
}
