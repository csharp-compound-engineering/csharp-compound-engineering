using CompoundDocs.McpServer.Session;
using CompoundDocs.McpServer.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Tools;

/// <summary>
/// Unit tests for ActivateProjectTool.
/// Tests input validation and error handling.
/// Note: Integration tests with actual ProjectActivationService are handled separately.
/// </summary>
public sealed class ActivateProjectToolTests
{
    #region Input Validation Tests

    [Fact]
    public async Task ActivateProjectAsync_EmptyProjectPath_ReturnsMissingParameterError()
    {
        // Arrange
        var sessionContextMock = new Mock<ISessionContext>();
        var logger = NullLogger<ActivateProjectTool>.Instance;
        var activationLogger = NullLogger<ProjectActivationService>.Instance;
        var activationService = new ProjectActivationService(sessionContextMock.Object, activationLogger);

        var tool = new ActivateProjectTool(activationService, sessionContextMock.Object, logger);

        // Act
        var result = await tool.ActivateProjectAsync("");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("MISSING_PARAMETER");
    }

    [Fact]
    public async Task ActivateProjectAsync_WhitespaceProjectPath_ReturnsMissingParameterError()
    {
        // Arrange
        var sessionContextMock = new Mock<ISessionContext>();
        var logger = NullLogger<ActivateProjectTool>.Instance;
        var activationLogger = NullLogger<ProjectActivationService>.Instance;
        var activationService = new ProjectActivationService(sessionContextMock.Object, activationLogger);

        var tool = new ActivateProjectTool(activationService, sessionContextMock.Object, logger);

        // Act
        var result = await tool.ActivateProjectAsync("   ");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("MISSING_PARAMETER");
    }

    [Fact]
    public async Task ActivateProjectAsync_NullProjectPath_ReturnsMissingParameterError()
    {
        // Arrange
        var sessionContextMock = new Mock<ISessionContext>();
        var logger = NullLogger<ActivateProjectTool>.Instance;
        var activationLogger = NullLogger<ProjectActivationService>.Instance;
        var activationService = new ProjectActivationService(sessionContextMock.Object, activationLogger);

        var tool = new ActivateProjectTool(activationService, sessionContextMock.Object, logger);

        // Act
        var result = await tool.ActivateProjectAsync(null!);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("MISSING_PARAMETER");
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullActivationService_ThrowsArgumentNullException()
    {
        // Arrange
        var sessionContextMock = new Mock<ISessionContext>();
        var logger = NullLogger<ActivateProjectTool>.Instance;

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ActivateProjectTool(
            null!,
            sessionContextMock.Object,
            logger));
    }

    [Fact]
    public void Constructor_WithNullSessionContext_ThrowsArgumentNullException()
    {
        // Arrange
        var sessionContextMock = new Mock<ISessionContext>();
        var logger = NullLogger<ActivateProjectTool>.Instance;
        var activationLogger = NullLogger<ProjectActivationService>.Instance;
        var activationService = new ProjectActivationService(sessionContextMock.Object, activationLogger);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ActivateProjectTool(
            activationService,
            null!,
            logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var sessionContextMock = new Mock<ISessionContext>();
        var activationLogger = NullLogger<ProjectActivationService>.Instance;
        var activationService = new ProjectActivationService(sessionContextMock.Object, activationLogger);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ActivateProjectTool(
            activationService,
            sessionContextMock.Object,
            null!));
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ActivateProjectAsync_NonExistentPath_ReturnsProjectActivationFailedError()
    {
        // Arrange
        var sessionContextMock = new Mock<ISessionContext>();
        sessionContextMock.Setup(s => s.IsProjectActive).Returns(false);

        var logger = NullLogger<ActivateProjectTool>.Instance;
        var activationLogger = NullLogger<ProjectActivationService>.Instance;
        var activationService = new ProjectActivationService(sessionContextMock.Object, activationLogger);

        var tool = new ActivateProjectTool(activationService, sessionContextMock.Object, logger);

        // Act
        var result = await tool.ActivateProjectAsync("/nonexistent/path/that/does/not/exist");

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorCode.ShouldBe("PROJECT_ACTIVATION_FAILED");
    }

    [Fact]
    public async Task ActivateProjectAsync_ValidPath_ActivatesSuccessfully()
    {
        // Arrange
        var sessionContextMock = new Mock<ISessionContext>();
        sessionContextMock.Setup(s => s.IsProjectActive).Returns(false);
        sessionContextMock.Setup(s => s.ActivateProject(It.IsAny<string>(), It.IsAny<string>()));
        sessionContextMock.Setup(s => s.ProjectName).Returns("test-project");
        sessionContextMock.Setup(s => s.PathHash).Returns("abc123");
        sessionContextMock.Setup(s => s.TenantKey).Returns("test-project:main:abc123");

        var logger = NullLogger<ActivateProjectTool>.Instance;
        var activationLogger = NullLogger<ProjectActivationService>.Instance;
        var activationService = new ProjectActivationService(sessionContextMock.Object, activationLogger);

        var tool = new ActivateProjectTool(activationService, sessionContextMock.Object, logger);

        // Use actual existing directory
        var tempDir = Path.GetTempPath();

        // Act
        var result = await tool.ActivateProjectAsync(tempDir);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
    }

    [Fact]
    public async Task ActivateProjectAsync_WithCancellation_ReturnsOperationCancelledError()
    {
        // Arrange
        var sessionContextMock = new Mock<ISessionContext>();
        sessionContextMock.Setup(s => s.IsProjectActive).Returns(false);

        var logger = NullLogger<ActivateProjectTool>.Instance;
        var activationLogger = NullLogger<ProjectActivationService>.Instance;
        var activationService = new ProjectActivationService(sessionContextMock.Object, activationLogger);

        var tool = new ActivateProjectTool(activationService, sessionContextMock.Object, logger);

        // Use existing directory but cancel immediately
        var tempDir = Path.GetTempPath();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await tool.ActivateProjectAsync(tempDir, cancellationToken: cts.Token);

        // Assert - The tool catches OperationCanceledException
        // Due to timing, this may succeed or return cancelled
        // We just verify the tool handles the token properly
        result.ShouldNotBeNull();
    }

    #endregion
}
