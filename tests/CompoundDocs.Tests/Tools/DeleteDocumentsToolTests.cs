using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Services.DocumentProcessing;
using CompoundDocs.McpServer.Session;
using CompoundDocs.McpServer.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Tools;

/// <summary>
/// Unit tests for DeleteDocumentsTool.
/// </summary>
public sealed class DeleteDocumentsToolTests
{
    private readonly Mock<IDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IDocumentIndexer> _documentIndexerMock;
    private readonly Mock<ISessionContext> _sessionContextMock;
    private readonly ILogger<DeleteDocumentsTool> _logger;
    private readonly DeleteDocumentsTool _tool;

    public DeleteDocumentsToolTests()
    {
        _documentRepositoryMock = new Mock<IDocumentRepository>(MockBehavior.Strict);
        _documentIndexerMock = new Mock<IDocumentIndexer>(MockBehavior.Strict);
        _sessionContextMock = new Mock<ISessionContext>(MockBehavior.Strict);
        _logger = NullLogger<DeleteDocumentsTool>.Instance;

        _tool = new DeleteDocumentsTool(
            _documentRepositoryMock.Object,
            _documentIndexerMock.Object,
            _sessionContextMock.Object,
            _logger);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullDocumentRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new DeleteDocumentsTool(
            null!,
            _documentIndexerMock.Object,
            _sessionContextMock.Object,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullDocumentIndexer_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new DeleteDocumentsTool(
            _documentRepositoryMock.Object,
            null!,
            _sessionContextMock.Object,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullSessionContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new DeleteDocumentsTool(
            _documentRepositoryMock.Object,
            _documentIndexerMock.Object,
            null!,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new DeleteDocumentsTool(
            _documentRepositoryMock.Object,
            _documentIndexerMock.Object,
            _sessionContextMock.Object,
            null!));
    }

    #endregion

    #region DeleteDocumentsAsync Tests

    [Fact]
    public async Task DeleteDocumentsAsync_NoActiveProject_ReturnsNoActiveProjectError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(false);

        // Act
        var result = await _tool.DeleteDocumentsAsync("test.md");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NO_ACTIVE_PROJECT");
    }

    [Fact]
    public async Task DeleteDocumentsAsync_EmptyFilePaths_ReturnsMissingParameterError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.DeleteDocumentsAsync("");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("MISSING_PARAMETER");
    }

    [Fact]
    public async Task DeleteDocumentsAsync_WhitespaceFilePaths_ReturnsMissingParameterError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.DeleteDocumentsAsync("   ");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("MISSING_PARAMETER");
    }

    [Fact]
    public async Task DeleteDocumentsAsync_NullFilePaths_ReturnsMissingParameterError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.DeleteDocumentsAsync(null!);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("MISSING_PARAMETER");
    }

    [Fact]
    public async Task DeleteDocumentsAsync_DocumentNotFound_ReturnsSuccessWithNotFoundInfo()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        _documentRepositoryMock
            .Setup(r => r.GetByTenantKeyAsync("test-tenant", "nonexistent.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument?)null);

        // Act
        var result = await _tool.DeleteDocumentsAsync("nonexistent.md");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.NotFoundCount.ShouldBe(1);
        result.Data.DeletedCount.ShouldBe(0);
    }

    [Fact]
    public async Task DeleteDocumentsAsync_DryRun_DoesNotDelete()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        var document = new CompoundDocument
        {
            Id = "doc-1",
            TenantKey = "test-tenant",
            FilePath = "test.md",
            Title = "Test Document",
            DocType = "doc",
            Content = "Test content",
            PromotionLevel = "standard"
        };

        _documentRepositoryMock
            .Setup(r => r.GetByTenantKeyAsync("test-tenant", "test.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act
        var result = await _tool.DeleteDocumentsAsync("test.md", dryRun: true);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.IsDryRun.ShouldBeTrue();
        result.Data.DeletedCount.ShouldBe(0);
        result.Data.Documents.ShouldContain(d => d.Reason!.Contains("dry run"));

        // Verify delete was not called
        _documentIndexerMock.Verify(
            i => i.DeleteDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteDocumentsAsync_ActualDelete_DeletesDocument()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        var document = new CompoundDocument
        {
            Id = "doc-1",
            TenantKey = "test-tenant",
            FilePath = "test.md",
            Title = "Test Document",
            DocType = "doc",
            Content = "Test content",
            PromotionLevel = "standard"
        };

        _documentRepositoryMock
            .Setup(r => r.GetByTenantKeyAsync("test-tenant", "test.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _documentIndexerMock
            .Setup(i => i.DeleteDocumentAsync("test-tenant", "test.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _tool.DeleteDocumentsAsync("test.md", dryRun: false);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.IsDryRun.ShouldBeFalse();
        result.Data.DeletedCount.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteDocumentsAsync_MultipleFiles_ProcessesAll()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        var document1 = new CompoundDocument
        {
            Id = "doc-1",
            TenantKey = "test-tenant",
            FilePath = "test1.md",
            Title = "Test Document 1",
            DocType = "doc",
            Content = "Test content 1",
            PromotionLevel = "standard"
        };

        _documentRepositoryMock
            .Setup(r => r.GetByTenantKeyAsync("test-tenant", "test1.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(document1);

        _documentRepositoryMock
            .Setup(r => r.GetByTenantKeyAsync("test-tenant", "test2.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument?)null);

        _documentIndexerMock
            .Setup(i => i.DeleteDocumentAsync("test-tenant", "test1.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _tool.DeleteDocumentsAsync("test1.md, test2.md", dryRun: false);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.TotalRequested.ShouldBe(2);
        result.Data.DeletedCount.ShouldBe(1);
        result.Data.NotFoundCount.ShouldBe(1);
    }

    [Fact]
    public async Task DeleteDocumentsAsync_WithCancellation_ReturnsOperationCancelledError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        var cts = new CancellationTokenSource();
        cts.Cancel();

        _documentRepositoryMock
            .Setup(r => r.GetByTenantKeyAsync("test-tenant", "test.md", cts.Token))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _tool.DeleteDocumentsAsync("test.md", cancellationToken: cts.Token);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("OPERATION_CANCELLED");
    }

    #endregion
}
