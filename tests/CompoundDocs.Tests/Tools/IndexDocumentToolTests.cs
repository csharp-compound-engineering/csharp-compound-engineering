using CompoundDocs.McpServer.Services.DocumentProcessing;
using CompoundDocs.McpServer.Session;
using CompoundDocs.McpServer.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Tools;

/// <summary>
/// Unit tests for IndexDocumentTool.
/// </summary>
public sealed class IndexDocumentToolTests
{
    private readonly Mock<IDocumentIndexer> _documentIndexerMock;
    private readonly Mock<ISessionContext> _sessionContextMock;
    private readonly ILogger<IndexDocumentTool> _logger;
    private readonly IndexDocumentTool _tool;

    public IndexDocumentToolTests()
    {
        _documentIndexerMock = new Mock<IDocumentIndexer>(MockBehavior.Strict);
        _sessionContextMock = new Mock<ISessionContext>(MockBehavior.Strict);
        _logger = NullLogger<IndexDocumentTool>.Instance;

        _tool = new IndexDocumentTool(
            _documentIndexerMock.Object,
            _sessionContextMock.Object,
            _logger);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullDocumentIndexer_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new IndexDocumentTool(
            null!,
            _sessionContextMock.Object,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullSessionContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new IndexDocumentTool(
            _documentIndexerMock.Object,
            null!,
            _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new IndexDocumentTool(
            _documentIndexerMock.Object,
            _sessionContextMock.Object,
            null!));
    }

    #endregion

    #region IndexDocumentAsync Tests

    [Fact]
    public async Task IndexDocumentAsync_NoActiveProject_ReturnsNoActiveProjectError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(false);

        // Act
        var result = await _tool.IndexDocumentAsync("test.md");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("NO_ACTIVE_PROJECT");
    }

    [Fact]
    public async Task IndexDocumentAsync_EmptyFilePath_ReturnsMissingParameterError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.IndexDocumentAsync("");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("MISSING_PARAMETER");
    }

    [Fact]
    public async Task IndexDocumentAsync_WhitespaceFilePath_ReturnsMissingParameterError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.IndexDocumentAsync("   ");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("MISSING_PARAMETER");
    }

    [Fact]
    public async Task IndexDocumentAsync_NullFilePath_ReturnsMissingParameterError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);

        // Act
        var result = await _tool.IndexDocumentAsync(null!);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("MISSING_PARAMETER");
    }

    [Fact]
    public async Task IndexDocumentAsync_FileNotFound_ReturnsFileNotFoundError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.ActiveProjectPath).Returns("/tmp/test-project");
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        // Act
        var result = await _tool.IndexDocumentAsync("nonexistent.md");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("FILE_NOT_FOUND");
    }

    [Fact]
    public async Task IndexDocumentAsync_WithCancellation_ReturnsOperationCancelledError()
    {
        // Arrange
        _sessionContextMock.Setup(s => s.IsProjectActive).Returns(true);
        _sessionContextMock.Setup(s => s.ActiveProjectPath).Returns(Path.GetTempPath());
        _sessionContextMock.Setup(s => s.TenantKey).Returns("test-tenant");

        // Create a temporary file
        var tempFile = Path.Combine(Path.GetTempPath(), "test-cancel.md");
        await File.WriteAllTextAsync(tempFile, "# Test\nContent");

        try
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act
            var result = await _tool.IndexDocumentAsync("test-cancel.md", cts.Token);

            // Assert
            result.Success.ShouldBeFalse();
            result.ErrorCode.ShouldBe("OPERATION_CANCELLED");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion
}
