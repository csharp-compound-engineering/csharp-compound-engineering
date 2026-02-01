using CompoundDocs.McpServer.Session;

namespace CompoundDocs.Tests.Session;

/// <summary>
/// Unit tests for SessionContext.
/// </summary>
public sealed class SessionContextTests : IDisposable
{
    private SessionContext? _context;

    public void Dispose()
    {
        _context?.Dispose();
    }

    private SessionContext CreateContext()
    {
        _context = new SessionContext();
        return _context;
    }

    #region Initial State Tests

    [Fact]
    public void NewContext_HasNoActiveProject()
    {
        // Arrange & Act
        var context = CreateContext();

        // Assert
        context.IsProjectActive.ShouldBeFalse();
    }

    [Fact]
    public void NewContext_HasNullActiveProjectPath()
    {
        // Arrange & Act
        var context = CreateContext();

        // Assert
        context.ActiveProjectPath.ShouldBeNull();
    }

    [Fact]
    public void NewContext_HasNullActiveBranch()
    {
        // Arrange & Act
        var context = CreateContext();

        // Assert
        context.ActiveBranch.ShouldBeNull();
    }

    [Fact]
    public void NewContext_HasNullTenantKey()
    {
        // Arrange & Act
        var context = CreateContext();

        // Assert
        context.TenantKey.ShouldBeNull();
    }

    [Fact]
    public void NewContext_HasNullProjectName()
    {
        // Arrange & Act
        var context = CreateContext();

        // Assert
        context.ProjectName.ShouldBeNull();
    }

    [Fact]
    public void NewContext_HasNullPathHash()
    {
        // Arrange & Act
        var context = CreateContext();

        // Assert
        context.PathHash.ShouldBeNull();
    }

    #endregion

    #region ActivateProject Tests

    [Fact]
    public void ActivateProject_WithValidParameters_SetsActiveState()
    {
        // Arrange
        var context = CreateContext();
        var projectPath = "/home/user/projects/my-project";
        var branchName = "main";

        // Act
        context.ActivateProject(projectPath, branchName);

        // Assert
        context.IsProjectActive.ShouldBeTrue();
        context.ActiveProjectPath.ShouldBe(projectPath);
        context.ActiveBranch.ShouldBe(branchName);
    }

    [Fact]
    public void ActivateProject_SetsTenantKey()
    {
        // Arrange
        var context = CreateContext();
        var projectPath = "/home/user/projects/my-project";
        var branchName = "develop";

        // Act
        context.ActivateProject(projectPath, branchName);

        // Assert
        context.TenantKey.ShouldNotBeNull();
        context.TenantKey.ShouldContain("my-project");
        context.TenantKey.ShouldContain("develop");
    }

    [Fact]
    public void ActivateProject_SetsProjectName()
    {
        // Arrange
        var context = CreateContext();
        var projectPath = "/home/user/projects/awesome-project";
        var branchName = "main";

        // Act
        context.ActivateProject(projectPath, branchName);

        // Assert
        context.ProjectName.ShouldBe("awesome-project");
    }

    [Fact]
    public void ActivateProject_SetsPathHash()
    {
        // Arrange
        var context = CreateContext();
        var projectPath = "/home/user/projects/my-project";
        var branchName = "main";

        // Act
        context.ActivateProject(projectPath, branchName);

        // Assert
        context.PathHash.ShouldNotBeNull();
        context.PathHash.Length.ShouldBe(TenantKeyProvider.PathHashLength);
    }

    [Fact]
    public void ActivateProject_WithNullProjectPath_ThrowsArgumentNullException()
    {
        // Arrange
        var context = CreateContext();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            context.ActivateProject(null!, "main"));
    }

    [Fact]
    public void ActivateProject_WithEmptyProjectPath_ThrowsArgumentException()
    {
        // Arrange
        var context = CreateContext();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            context.ActivateProject(string.Empty, "main"));
    }

    [Fact]
    public void ActivateProject_WithWhitespaceProjectPath_ThrowsArgumentException()
    {
        // Arrange
        var context = CreateContext();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            context.ActivateProject("   ", "main"));
    }

    [Fact]
    public void ActivateProject_WithNullBranchName_ThrowsArgumentNullException()
    {
        // Arrange
        var context = CreateContext();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            context.ActivateProject("/path/to/project", null!));
    }

    [Fact]
    public void ActivateProject_WithEmptyBranchName_ThrowsArgumentException()
    {
        // Arrange
        var context = CreateContext();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            context.ActivateProject("/path/to/project", string.Empty));
    }

    [Fact]
    public void ActivateProject_WithWhitespaceBranchName_ThrowsArgumentException()
    {
        // Arrange
        var context = CreateContext();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            context.ActivateProject("/path/to/project", "   "));
    }

    [Fact]
    public void ActivateProject_CanActivateDifferentProject()
    {
        // Arrange
        var context = CreateContext();
        context.ActivateProject("/project1", "main");

        // Act
        context.DeactivateProject();
        context.ActivateProject("/project2", "develop");

        // Assert
        context.ActiveProjectPath.ShouldBe("/project2");
        context.ActiveBranch.ShouldBe("develop");
        context.ProjectName.ShouldBe("project2");
    }

    #endregion

    #region DeactivateProject Tests

    [Fact]
    public void DeactivateProject_ClearsAllState()
    {
        // Arrange
        var context = CreateContext();
        context.ActivateProject("/path/to/project", "main");

        // Act
        context.DeactivateProject();

        // Assert
        context.IsProjectActive.ShouldBeFalse();
        context.ActiveProjectPath.ShouldBeNull();
        context.ActiveBranch.ShouldBeNull();
        context.TenantKey.ShouldBeNull();
        context.ProjectName.ShouldBeNull();
        context.PathHash.ShouldBeNull();
    }

    [Fact]
    public void DeactivateProject_WhenNotActive_DoesNotThrow()
    {
        // Arrange
        var context = CreateContext();

        // Act & Assert (should not throw)
        Should.NotThrow(() => context.DeactivateProject());
    }

    [Fact]
    public void DeactivateProject_AfterDeactivate_AllowsReactivation()
    {
        // Arrange
        var context = CreateContext();
        context.ActivateProject("/project1", "main");
        context.DeactivateProject();

        // Act
        context.ActivateProject("/project2", "develop");

        // Assert
        context.IsProjectActive.ShouldBeTrue();
        context.ActiveProjectPath.ShouldBe("/project2");
    }

    #endregion

    #region GetConnectionString Tests

    [Fact]
    public void GetConnectionString_WhenProjectActive_ReturnsConnectionString()
    {
        // Arrange
        var context = CreateContext();
        context.ActivateProject("/path/to/project", "main");
        var baseConnectionString = "Host=localhost;Database=test";

        // Act
        var connectionString = context.GetConnectionString(baseConnectionString);

        // Assert
        connectionString.ShouldBe(baseConnectionString);
    }

    [Fact]
    public void GetConnectionString_WhenNoProjectActive_ThrowsInvalidOperationException()
    {
        // Arrange
        var context = CreateContext();
        var baseConnectionString = "Host=localhost;Database=test";

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            context.GetConnectionString(baseConnectionString));
        exception.Message.ShouldContain("no project is currently active");
    }

    [Fact]
    public void GetConnectionString_WithNullConnectionString_ThrowsArgumentNullException()
    {
        // Arrange
        var context = CreateContext();
        context.ActivateProject("/path/to/project", "main");

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            context.GetConnectionString(null!));
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_WhenInactive_ReturnsInactiveDescription()
    {
        // Arrange
        var context = CreateContext();

        // Act
        var result = context.ToString();

        // Assert
        result.ShouldBe("SessionContext[Inactive]");
    }

    [Fact]
    public void ToString_WhenActive_ReturnsFormattedDescription()
    {
        // Arrange
        var context = CreateContext();
        context.ActivateProject("/path/to/my-project", "feature-branch");

        // Act
        var result = context.ToString();

        // Assert
        result.ShouldStartWith("SessionContext[");
        result.ShouldContain("my-project");
        result.ShouldContain("feature-branch");
        result.ShouldEndWith("]");
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_PreventsSubsequentPropertyAccess()
    {
        // Arrange
        var context = new SessionContext();

        // Act
        context.Dispose();

        // Assert
        Should.Throw<ObjectDisposedException>(() => { _ = context.IsProjectActive; });
    }

    [Fact]
    public void Dispose_PreventsSubsequentActivateProject()
    {
        // Arrange
        var context = new SessionContext();

        // Act
        context.Dispose();

        // Assert
        Should.Throw<ObjectDisposedException>(() =>
            context.ActivateProject("/path", "main"));
    }

    [Fact]
    public void Dispose_PreventsSubsequentDeactivateProject()
    {
        // Arrange
        var context = new SessionContext();

        // Act
        context.Dispose();

        // Assert
        Should.Throw<ObjectDisposedException>(() =>
            context.DeactivateProject());
    }

    [Fact]
    public void Dispose_PreventsSubsequentGetConnectionString()
    {
        // Arrange
        var context = new SessionContext();

        // Act
        context.Dispose();

        // Assert
        Should.Throw<ObjectDisposedException>(() =>
            context.GetConnectionString("connection"));
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var context = new SessionContext();

        // Act & Assert (should not throw)
        Should.NotThrow(() =>
        {
            context.Dispose();
            context.Dispose();
            context.Dispose();
        });
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentReads_DoNotThrow()
    {
        // Arrange
        var context = CreateContext();
        context.ActivateProject("/path/to/project", "main");

        // Act & Assert
        var tasks = Enumerable.Range(0, 100).Select(n => Task.Run(() =>
        {
            var isActive = context.IsProjectActive;
            var path = context.ActiveProjectPath;
            var key = context.TenantKey;
            var name = context.ProjectName;
            var hash = context.PathHash;
        }));

        await Should.NotThrowAsync(async () => await Task.WhenAll(tasks));
    }

    [Fact]
    public async Task ConcurrentActivateDeactivate_DoesNotThrow()
    {
        // Arrange
        var context = CreateContext();

        // Act & Assert
        var tasks = Enumerable.Range(0, 50).Select(i => Task.Run(() =>
        {
            if (i % 2 == 0)
            {
                context.ActivateProject($"/project{i}", "main");
            }
            else
            {
                context.DeactivateProject();
            }
        }));

        await Should.NotThrowAsync(async () => await Task.WhenAll(tasks));
    }

    #endregion
}
