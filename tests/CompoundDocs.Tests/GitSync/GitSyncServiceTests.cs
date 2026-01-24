using CompoundDocs.GitSync;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.GitSync;

public sealed class GitSyncServiceTests : IDisposable
{
    private readonly List<string> _tempDirs = [];
    private readonly NullLogger<GitSyncService> _logger = NullLogger<GitSyncService>.Instance;

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"gitsync-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    // --- Internal constructor tests ---

    [Fact]
    public async Task InternalConstructor_SetsBaseDirectory_ReadFileUsesIt()
    {
        // Arrange
        var tempDir = CreateTempDir();
        var filePath = Path.Combine(tempDir, "test.txt");
        await File.WriteAllTextAsync(filePath, "hello from internal ctor");
        var sut = new GitSyncService(tempDir, _logger);

        // Act
        var content = await sut.ReadFileContentAsync(tempDir, "test.txt");

        // Assert
        content.ShouldBe("hello from internal ctor");
    }

    // --- Public constructor tests ---

    [Fact]
    public void PublicConstructor_WithExistingDirectory_DoesNotThrow()
    {
        // Arrange
        var tempDir = CreateTempDir();
        var config = new GitSyncConfig { CloneBaseDirectory = tempDir };
        var options = Microsoft.Extensions.Options.Options.Create(config);

        // Act & Assert
        Should.NotThrow(() => new GitSyncService(options, _logger));
    }

    [Fact]
    public void PublicConstructor_WithNonExistingDirectory_CreatesDirectory()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"gitsync-test-{Guid.NewGuid():N}");
        _tempDirs.Add(tempDir); // register for cleanup even though it doesn't exist yet
        var config = new GitSyncConfig { CloneBaseDirectory = tempDir };
        var options = Microsoft.Extensions.Options.Options.Create(config);

        // Act
        _ = new GitSyncService(options, _logger);

        // Assert
        Directory.Exists(tempDir).ShouldBeTrue();
    }

    // --- ReadFileContentAsync tests ---

    [Fact]
    public async Task ReadFileContentAsync_ExistingFile_ReturnsContent()
    {
        // Arrange
        var tempDir = CreateTempDir();
        var expectedContent = "test file content";
        await File.WriteAllTextAsync(Path.Combine(tempDir, "readme.md"), expectedContent);
        var sut = new GitSyncService(tempDir, _logger);

        // Act
        var result = await sut.ReadFileContentAsync(tempDir, "readme.md");

        // Assert
        result.ShouldBe(expectedContent);
    }

    [Fact]
    public async Task ReadFileContentAsync_NonExistingFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var tempDir = CreateTempDir();
        var sut = new GitSyncService(tempDir, _logger);

        // Act & Assert
        await Should.ThrowAsync<FileNotFoundException>(
            () => sut.ReadFileContentAsync(tempDir, "does-not-exist.txt"));
    }

    [Fact]
    public async Task ReadFileContentAsync_NestedPath_ReturnsContent()
    {
        // Arrange
        var tempDir = CreateTempDir();
        var nestedDir = Path.Combine(tempDir, "sub", "folder");
        Directory.CreateDirectory(nestedDir);
        var expectedContent = "nested content";
        await File.WriteAllTextAsync(Path.Combine(nestedDir, "deep.txt"), expectedContent);
        var sut = new GitSyncService(tempDir, _logger);

        // Act
        var result = await sut.ReadFileContentAsync(tempDir, Path.Combine("sub", "folder", "deep.txt"));

        // Assert
        result.ShouldBe(expectedContent);
    }

    [Fact]
    public async Task ReadFileContentAsync_CancelledToken_ThrowsOperationCancelledException()
    {
        // Arrange
        var tempDir = CreateTempDir();
        await File.WriteAllTextAsync(Path.Combine(tempDir, "file.txt"), "content");
        var sut = new GitSyncService(tempDir, _logger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.ReadFileContentAsync(tempDir, "file.txt", cts.Token));
    }

    [Fact]
    public async Task ReadFileContentAsync_EmptyFile_ReturnsEmptyString()
    {
        // Arrange
        var tempDir = CreateTempDir();
        await File.WriteAllTextAsync(Path.Combine(tempDir, "empty.txt"), string.Empty);
        var sut = new GitSyncService(tempDir, _logger);

        // Act
        var result = await sut.ReadFileContentAsync(tempDir, "empty.txt");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReadFileContentAsync_FileWithUnicodeContent_ReturnsCorrectContent()
    {
        // Arrange
        var tempDir = CreateTempDir();
        var unicodeContent = "Hello, Unicode chars here";
        await File.WriteAllTextAsync(Path.Combine(tempDir, "unicode.txt"), unicodeContent);
        var sut = new GitSyncService(tempDir, _logger);

        // Act
        var result = await sut.ReadFileContentAsync(tempDir, "unicode.txt");

        // Assert
        result.ShouldBe(unicodeContent);
    }

    [Fact]
    public async Task ReadFileContentAsync_FileNotFoundException_ContainsRelativePath()
    {
        // Arrange
        var tempDir = CreateTempDir();
        var sut = new GitSyncService(tempDir, _logger);
        var relativePath = "missing-file.txt";

        // Act
        var ex = await Should.ThrowAsync<FileNotFoundException>(
            () => sut.ReadFileContentAsync(tempDir, relativePath));

        // Assert
        ex.Message.ShouldContain(relativePath);
    }

    // --- GitSyncConfig tests ---

    [Fact]
    public void GitSyncConfig_DefaultCloneBaseDirectory_IsInTempPath()
    {
        // Arrange & Act
        var config = new GitSyncConfig();

        // Assert
        config.CloneBaseDirectory.ShouldStartWith(Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar));
    }

    [Fact]
    public void GitSyncConfig_DefaultCloneBaseDirectory_EndsWithCompoundDocsRepos()
    {
        // Arrange & Act
        var config = new GitSyncConfig();

        // Assert
        config.CloneBaseDirectory.ShouldEndWith("compound-docs-repos");
    }

    [Fact]
    public void GitSyncConfig_CloneBaseDirectory_IsSettable()
    {
        // Arrange
        var config = new GitSyncConfig();

        // Act
        config.CloneBaseDirectory = "/my/custom/path";

        // Assert
        config.CloneBaseDirectory.ShouldBe("/my/custom/path");
    }
}
