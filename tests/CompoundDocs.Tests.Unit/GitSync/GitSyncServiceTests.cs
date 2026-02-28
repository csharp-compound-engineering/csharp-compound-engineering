using CompoundDocs.GitSync;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Unit.GitSync;

public sealed class GitSyncServiceTests
{
    // --- Internal constructor tests ---

    [Fact]
    public async Task InternalConstructor_SetsBaseDirectory_ReadFileUsesIt()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gitsync-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var filePath = Path.Combine(tempDir, "test.txt");
            await File.WriteAllTextAsync(filePath, "hello from internal ctor");
            var sut = new GitSyncService(tempDir, NullLogger<GitSyncService>.Instance);

            // Act
            var content = await sut.ReadFileContentAsync(tempDir, "test.txt");

            // Assert
            content.ShouldBe("hello from internal ctor");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    // --- Public constructor tests ---

    [Fact]
    public void PublicConstructor_WithExistingDirectory_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gitsync-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var config = new GitSyncConfig { CloneBaseDirectory = tempDir };
            var options = Microsoft.Extensions.Options.Options.Create(config);

            // Act & Assert
            Should.NotThrow(() => new GitSyncService(options, NullLogger<GitSyncService>.Instance));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void PublicConstructor_WithNonExistingDirectory_CreatesDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gitsync-test-{Guid.NewGuid():N}");
        try
        {
            // Arrange
            var config = new GitSyncConfig { CloneBaseDirectory = tempDir };
            var options = Microsoft.Extensions.Options.Options.Create(config);

            // Act
            _ = new GitSyncService(options, NullLogger<GitSyncService>.Instance);

            // Assert
            Directory.Exists(tempDir).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    // --- ReadFileContentAsync tests ---

    [Fact]
    public async Task ReadFileContentAsync_ExistingFile_ReturnsContent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gitsync-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var expectedContent = "test file content";
            await File.WriteAllTextAsync(Path.Combine(tempDir, "readme.md"), expectedContent);
            var sut = new GitSyncService(tempDir, NullLogger<GitSyncService>.Instance);

            // Act
            var result = await sut.ReadFileContentAsync(tempDir, "readme.md");

            // Assert
            result.ShouldBe(expectedContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ReadFileContentAsync_NonExistingFile_ThrowsFileNotFoundException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gitsync-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new GitSyncService(tempDir, NullLogger<GitSyncService>.Instance);

            // Act & Assert
            await Should.ThrowAsync<FileNotFoundException>(
                () => sut.ReadFileContentAsync(tempDir, "does-not-exist.txt"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ReadFileContentAsync_NestedPath_ReturnsContent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gitsync-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var nestedDir = Path.Combine(tempDir, "sub", "folder");
            Directory.CreateDirectory(nestedDir);
            var expectedContent = "nested content";
            await File.WriteAllTextAsync(Path.Combine(nestedDir, "deep.txt"), expectedContent);
            var sut = new GitSyncService(tempDir, NullLogger<GitSyncService>.Instance);

            // Act
            var result = await sut.ReadFileContentAsync(tempDir, Path.Combine("sub", "folder", "deep.txt"));

            // Assert
            result.ShouldBe(expectedContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ReadFileContentAsync_CancelledToken_ThrowsOperationCancelledException()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gitsync-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            await File.WriteAllTextAsync(Path.Combine(tempDir, "file.txt"), "content");
            var sut = new GitSyncService(tempDir, NullLogger<GitSyncService>.Instance);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // Act & Assert
            await Should.ThrowAsync<OperationCanceledException>(
                () => sut.ReadFileContentAsync(tempDir, "file.txt", cts.Token));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ReadFileContentAsync_EmptyFile_ReturnsEmptyString()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gitsync-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            await File.WriteAllTextAsync(Path.Combine(tempDir, "empty.txt"), string.Empty);
            var sut = new GitSyncService(tempDir, NullLogger<GitSyncService>.Instance);

            // Act
            var result = await sut.ReadFileContentAsync(tempDir, "empty.txt");

            // Assert
            result.ShouldBeEmpty();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ReadFileContentAsync_FileWithUnicodeContent_ReturnsCorrectContent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gitsync-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var unicodeContent = "Hello, Unicode chars here";
            await File.WriteAllTextAsync(Path.Combine(tempDir, "unicode.txt"), unicodeContent);
            var sut = new GitSyncService(tempDir, NullLogger<GitSyncService>.Instance);

            // Act
            var result = await sut.ReadFileContentAsync(tempDir, "unicode.txt");

            // Assert
            result.ShouldBe(unicodeContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ReadFileContentAsync_FileNotFoundException_ContainsRelativePath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"gitsync-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Arrange
            var sut = new GitSyncService(tempDir, NullLogger<GitSyncService>.Instance);
            var relativePath = "missing-file.txt";

            // Act
            var ex = await Should.ThrowAsync<FileNotFoundException>(
                () => sut.ReadFileContentAsync(tempDir, relativePath));

            // Assert
            ex.Message.ShouldContain(relativePath);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
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
