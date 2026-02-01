using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Session;
using CompoundDocs.McpServer.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Tools;

/// <summary>
/// Unit tests for UpdatePromotionLevelTool.
/// </summary>
public sealed class UpdatePromotionLevelToolTests
{
    private readonly Mock<IDocumentRepository> _documentRepositoryMock;
    private readonly Mock<ISessionContext> _sessionContextMock;
    private readonly ILogger<UpdatePromotionLevelTool> _logger;
    private readonly UpdatePromotionLevelTool _tool;

    public UpdatePromotionLevelToolTests()
    {
        _documentRepositoryMock = new Mock<IDocumentRepository>(MockBehavior.Strict);
        _sessionContextMock = new Mock<ISessionContext>(MockBehavior.Strict);
        _logger = NullLogger<UpdatePromotionLevelTool>.Instance;

        _tool = new UpdatePromotionLevelTool(
            _documentRepositoryMock.Object,
            _sessionContextMock.Object,
            _logger);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullDocumentRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new UpdatePromotionLevelTool(
            null!,
            _sessionContextMock.Object,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullSessionContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new UpdatePromotionLevelTool(
            _documentRepositoryMock.Object,
            null!,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new UpdatePromotionLevelTool(
            _documentRepositoryMock.Object,
            _sessionContextMock.Object,
            null!));
    }

    #endregion

    #region UpdatePromotionLevelAsync Tests

    [Fact]
    public async Task UpdatePromotionLevelAsync_NoActiveProject_ReturnsNoActiveProjectError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(false);

        // Act
        var result = await _tool.UpdatePromotionLevelAsync("test.md", "promoted");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NO_ACTIVE_PROJECT");
    }

    [Fact]
    public async Task UpdatePromotionLevelAsync_EmptyFilePath_ReturnsMissingParameterError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.UpdatePromotionLevelAsync("", "promoted");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("MISSING_PARAMETER");
    }

    [Fact]
    public async Task UpdatePromotionLevelAsync_WhitespaceFilePath_ReturnsMissingParameterError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.UpdatePromotionLevelAsync("   ", "promoted");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("MISSING_PARAMETER");
    }

    [Fact]
    public async Task UpdatePromotionLevelAsync_NullFilePath_ReturnsMissingParameterError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.UpdatePromotionLevelAsync(null!, "promoted");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("MISSING_PARAMETER");
    }

    [Fact]
    public async Task UpdatePromotionLevelAsync_EmptyPromotionLevel_ReturnsMissingParameterError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.UpdatePromotionLevelAsync("test.md", "");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("MISSING_PARAMETER");
    }

    [Fact]
    public async Task UpdatePromotionLevelAsync_WhitespacePromotionLevel_ReturnsMissingParameterError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.UpdatePromotionLevelAsync("test.md", "   ");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("MISSING_PARAMETER");
    }

    [Fact]
    public async Task UpdatePromotionLevelAsync_InvalidPromotionLevel_ReturnsInvalidPromotionLevelError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.UpdatePromotionLevelAsync("test.md", "invalid");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("INVALID_PROMOTION_LEVEL");
    }

    [Fact]
    public async Task UpdatePromotionLevelAsync_DocumentNotFound_ReturnsDocumentNotFoundError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        _documentRepositoryMock
            .Setup(r => r.GetByTenantKeyAsync("test-tenant", "nonexistent.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument?)null);

        // Act
        var result = await _tool.UpdatePromotionLevelAsync("nonexistent.md", "promoted");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("DOCUMENT_NOT_FOUND");
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("Standard")]
    [InlineData("STANDARD")]
    public async Task UpdatePromotionLevelAsync_StandardLevel_UpdatesSuccessfully(string level)
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

        _documentRepositoryMock
            .Setup(r => r.UpdatePromotionLevelAsync("doc-1", "standard", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _tool.UpdatePromotionLevelAsync("test.md", level);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.NewLevel.ShouldBe("standard");
    }

    [Theory]
    [InlineData("promoted")]
    [InlineData("important")]
    [InlineData("Promoted")]
    [InlineData("Important")]
    public async Task UpdatePromotionLevelAsync_PromotedLevel_UpdatesSuccessfully(string level)
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

        _documentRepositoryMock
            .Setup(r => r.UpdatePromotionLevelAsync("doc-1", "promoted", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _tool.UpdatePromotionLevelAsync("test.md", level);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.NewLevel.ShouldBe("promoted");
    }

    [Theory]
    [InlineData("pinned")]
    [InlineData("critical")]
    [InlineData("Pinned")]
    [InlineData("Critical")]
    public async Task UpdatePromotionLevelAsync_PinnedLevel_UpdatesSuccessfully(string level)
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

        _documentRepositoryMock
            .Setup(r => r.UpdatePromotionLevelAsync("doc-1", "pinned", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _tool.UpdatePromotionLevelAsync("test.md", level);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.NewLevel.ShouldBe("pinned");
    }

    [Fact]
    public async Task UpdatePromotionLevelAsync_UpdateFails_ReturnsUnexpectedError()
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

        _documentRepositoryMock
            .Setup(r => r.UpdatePromotionLevelAsync("doc-1", "promoted", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _tool.UpdatePromotionLevelAsync("test.md", "promoted");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("UNEXPECTED_ERROR");
    }

    [Fact]
    public async Task UpdatePromotionLevelAsync_Success_ReturnsPreviousAndNewLevels()
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

        _documentRepositoryMock
            .Setup(r => r.UpdatePromotionLevelAsync("doc-1", "pinned", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _tool.UpdatePromotionLevelAsync("test.md", "pinned");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.PreviousLevel.ShouldBe("standard");
        result.Data.NewLevel.ShouldBe("pinned");
        result.Data.BoostFactor.ShouldBe(2.0f);
    }

    [Fact]
    public async Task UpdatePromotionLevelAsync_WithCancellation_ReturnsOperationCancelledError()
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
        var result = await _tool.UpdatePromotionLevelAsync("test.md", "promoted", cts.Token);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("OPERATION_CANCELLED");
    }

    #endregion
}
