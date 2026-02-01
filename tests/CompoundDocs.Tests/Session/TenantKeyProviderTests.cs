using CompoundDocs.McpServer.Session;

namespace CompoundDocs.Tests.Session;

/// <summary>
/// Unit tests for TenantKeyProvider.
/// </summary>
public sealed class TenantKeyProviderTests
{
    #region ComputePathHash Tests

    [Fact]
    public void ComputePathHash_WithValidPath_ReturnsHexString()
    {
        // Arrange
        var path = "/home/user/projects/my-project";

        // Act
        var hash = TenantKeyProvider.ComputePathHash(path);

        // Assert
        hash.ShouldNotBeNullOrEmpty();
        hash.Length.ShouldBe(TenantKeyProvider.PathHashLength);
        hash.ShouldMatch("^[a-f0-9]+$"); // Should be lowercase hex
    }

    [Fact]
    public void ComputePathHash_WithSamePath_ReturnsSameHash()
    {
        // Arrange
        var path = "/home/user/projects/my-project";

        // Act
        var hash1 = TenantKeyProvider.ComputePathHash(path);
        var hash2 = TenantKeyProvider.ComputePathHash(path);

        // Assert
        hash1.ShouldBe(hash2);
    }

    [Fact]
    public void ComputePathHash_WithDifferentPaths_ReturnsDifferentHashes()
    {
        // Arrange
        var path1 = "/home/user/projects/project-a";
        var path2 = "/home/user/projects/project-b";

        // Act
        var hash1 = TenantKeyProvider.ComputePathHash(path1);
        var hash2 = TenantKeyProvider.ComputePathHash(path2);

        // Assert
        hash1.ShouldNotBe(hash2);
    }

    [Fact]
    public void ComputePathHash_NormalizesPathSeparators()
    {
        // Arrange - Windows and Unix style paths
        var windowsPath = @"C:\Users\user\projects\my-project";
        var unixPath = "C:/Users/user/projects/my-project";

        // Act
        var hash1 = TenantKeyProvider.ComputePathHash(windowsPath);
        var hash2 = TenantKeyProvider.ComputePathHash(unixPath);

        // Assert
        hash1.ShouldBe(hash2);
    }

    [Fact]
    public void ComputePathHash_NormalizesTrailingSlashes()
    {
        // Arrange
        var pathWithSlash = "/home/user/projects/my-project/";
        var pathWithoutSlash = "/home/user/projects/my-project";

        // Act
        var hash1 = TenantKeyProvider.ComputePathHash(pathWithSlash);
        var hash2 = TenantKeyProvider.ComputePathHash(pathWithoutSlash);

        // Assert
        hash1.ShouldBe(hash2);
    }

    [Fact]
    public void ComputePathHash_WithNullPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => TenantKeyProvider.ComputePathHash(null!));
    }

    [Fact]
    public void ComputePathHash_WithEmptyPath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => TenantKeyProvider.ComputePathHash(string.Empty));
    }

    [Fact]
    public void ComputePathHash_WithWhitespacePath_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => TenantKeyProvider.ComputePathHash("   "));
    }

    #endregion

    #region GenerateTenantKey Tests

    [Fact]
    public void GenerateTenantKey_WithComponents_CombinesWithSeparator()
    {
        // Arrange
        var projectName = "my-project";
        var branchName = "main";
        var pathHash = "abc123def456789";

        // Act
        var tenantKey = TenantKeyProvider.GenerateTenantKey(projectName, branchName, pathHash);

        // Assert
        tenantKey.ShouldBe($"my-project{TenantKeyProvider.KeySeparator}main{TenantKeyProvider.KeySeparator}abc123def456789");
    }

    [Fact]
    public void GenerateTenantKey_FromPathAndBranch_ComputesHashAutomatically()
    {
        // Arrange
        var projectPath = "/home/user/projects/my-project";
        var branchName = "develop";

        // Act
        var tenantKey = TenantKeyProvider.GenerateTenantKey(projectPath, branchName);

        // Assert
        tenantKey.ShouldNotBeNullOrEmpty();
        tenantKey.ShouldContain("my-project");
        tenantKey.ShouldContain("develop");
        tenantKey.Split(TenantKeyProvider.KeySeparator).Length.ShouldBe(3);
    }

    [Fact]
    public void GenerateTenantKey_WithNullProjectName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            TenantKeyProvider.GenerateTenantKey(null!, "main", "hash123"));
    }

    [Fact]
    public void GenerateTenantKey_WithNullBranchName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            TenantKeyProvider.GenerateTenantKey("project", null!, "hash123"));
    }

    [Fact]
    public void GenerateTenantKey_WithNullPathHash_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            TenantKeyProvider.GenerateTenantKey("project", "main", null!));
    }

    [Fact]
    public void GenerateTenantKey_WithEmptyComponents_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            TenantKeyProvider.GenerateTenantKey("", "main", "hash123"));
        Should.Throw<ArgumentException>(() =>
            TenantKeyProvider.GenerateTenantKey("project", "", "hash123"));
        Should.Throw<ArgumentException>(() =>
            TenantKeyProvider.GenerateTenantKey("project", "main", ""));
    }

    #endregion

    #region ParseTenantKey Tests

    [Fact]
    public void ParseTenantKey_WithValidKey_ExtractsComponents()
    {
        // Arrange
        var tenantKey = "my-project:main:abc123def456789";

        // Act
        var (projectName, branchName, pathHash) = TenantKeyProvider.ParseTenantKey(tenantKey);

        // Assert
        projectName.ShouldBe("my-project");
        branchName.ShouldBe("main");
        pathHash.ShouldBe("abc123def456789");
    }

    [Fact]
    public void ParseTenantKey_RoundTripsWithGenerate()
    {
        // Arrange
        var originalProject = "test-project";
        var originalBranch = "feature/test";
        var originalHash = "0123456789abcdef";

        var tenantKey = TenantKeyProvider.GenerateTenantKey(originalProject, originalBranch, originalHash);

        // Act
        var (projectName, branchName, pathHash) = TenantKeyProvider.ParseTenantKey(tenantKey);

        // Assert
        projectName.ShouldBe(originalProject);
        branchName.ShouldBe(originalBranch);
        pathHash.ShouldBe(originalHash);
    }

    [Fact]
    public void ParseTenantKey_WithInvalidFormat_ThrowsFormatException()
    {
        // Arrange - Only two parts instead of three
        var invalidKey = "my-project:main";

        // Act & Assert
        Should.Throw<FormatException>(() => TenantKeyProvider.ParseTenantKey(invalidKey));
    }

    [Fact]
    public void ParseTenantKey_WithNullKey_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => TenantKeyProvider.ParseTenantKey(null!));
    }

    [Fact]
    public void ParseTenantKey_WithEmptyKey_ThrowsArgumentException()
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => TenantKeyProvider.ParseTenantKey(string.Empty));
    }

    [Fact]
    public void ParseTenantKey_WithTooManyParts_ThrowsFormatException()
    {
        // Arrange - Four parts instead of three
        var invalidKey = "project:branch:hash:extra";

        // Act & Assert
        Should.Throw<FormatException>(() => TenantKeyProvider.ParseTenantKey(invalidKey));
    }

    #endregion

    #region TryParseTenantKey Tests

    [Fact]
    public void TryParseTenantKey_WithValidKey_ReturnsTrueAndParsedResult()
    {
        // Arrange
        var tenantKey = "my-project:main:abc123def456789";

        // Act
        var success = TenantKeyProvider.TryParseTenantKey(tenantKey, out var result);

        // Assert
        success.ShouldBeTrue();
        result.ProjectName.ShouldBe("my-project");
        result.BranchName.ShouldBe("main");
        result.PathHash.ShouldBe("abc123def456789");
    }

    [Fact]
    public void TryParseTenantKey_WithInvalidKey_ReturnsFalse()
    {
        // Arrange
        var invalidKey = "invalid-key-format";

        // Act
        var success = TenantKeyProvider.TryParseTenantKey(invalidKey, out var result);

        // Assert
        success.ShouldBeFalse();
        result.ShouldBe(default);
    }

    [Fact]
    public void TryParseTenantKey_WithNullKey_ReturnsFalse()
    {
        // Act
        var success = TenantKeyProvider.TryParseTenantKey(null, out var result);

        // Assert
        success.ShouldBeFalse();
    }

    [Fact]
    public void TryParseTenantKey_WithEmptyParts_ReturnsFalse()
    {
        // Arrange - Empty middle part
        var invalidKey = "project::hash";

        // Act
        var success = TenantKeyProvider.TryParseTenantKey(invalidKey, out _);

        // Assert
        success.ShouldBeFalse();
    }

    #endregion

    #region IsValidTenantKey Tests

    [Fact]
    public void IsValidTenantKey_WithValidKey_ReturnsTrue()
    {
        // Arrange
        var validKey = "my-project:main:abc123def456789";

        // Act
        var isValid = TenantKeyProvider.IsValidTenantKey(validKey);

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void IsValidTenantKey_WithInvalidKey_ReturnsFalse()
    {
        // Arrange
        var invalidKey = "not-a-valid-key";

        // Act
        var isValid = TenantKeyProvider.IsValidTenantKey(invalidKey);

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValidTenantKey_WithNull_ReturnsFalse()
    {
        // Act
        var isValid = TenantKeyProvider.IsValidTenantKey(null);

        // Assert
        isValid.ShouldBeFalse();
    }

    #endregion

    #region ExtractProjectName Tests

    [Fact]
    public void ExtractProjectName_FromUnixPath_ReturnsLastComponent()
    {
        // Arrange
        var path = "/home/user/projects/my-awesome-project";

        // Act
        var projectName = TenantKeyProvider.ExtractProjectName(path);

        // Assert
        projectName.ShouldBe("my-awesome-project");
    }

    [Fact]
    public void ExtractProjectName_FromWindowsPath_ReturnsLastComponent()
    {
        // Arrange
        var path = @"C:\Users\user\projects\my-project";

        // Act
        var projectName = TenantKeyProvider.ExtractProjectName(path);

        // Assert
        projectName.ShouldBe("my-project");
    }

    [Fact]
    public void ExtractProjectName_WithTrailingSlash_HandlesCorrectly()
    {
        // Arrange
        var path = "/home/user/projects/my-project/";

        // Act
        var projectName = TenantKeyProvider.ExtractProjectName(path);

        // Assert
        projectName.ShouldBe("my-project");
    }

    [Fact]
    public void ExtractProjectName_WithSingleComponent_ReturnsComponent()
    {
        // Arrange
        var path = "my-project";

        // Act
        var projectName = TenantKeyProvider.ExtractProjectName(path);

        // Assert
        projectName.ShouldBe("my-project");
    }

    #endregion
}
