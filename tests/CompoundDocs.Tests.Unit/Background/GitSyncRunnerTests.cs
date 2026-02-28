using CompoundDocs.Common.Configuration;
using CompoundDocs.GitSync;
using CompoundDocs.GitSync.Job;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace CompoundDocs.Tests.Unit.Background;

public sealed class GitSyncRunnerTests
{
    [Fact]
    public async Task RunAsync_UnknownRepo_Returns1AndNoGitCalls()
    {
        // Arrange
        var gitSyncMock = new Mock<IGitSyncService>();
        var ingestionMock = new Mock<IDocumentIngestionService>();
        var graphMock = new Mock<IGraphRepository>();
        var config = new CompoundDocsCloudConfig
        {
            Repositories = [new RepositoryConfig { Name = "other-repo" }]
        };

        graphMock
            .Setup(g => g.GetSyncStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        gitSyncMock
            .Setup(s => s.GetHeadCommitHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123");

        var runner = new GitSyncRunner(
            gitSyncMock.Object,
            ingestionMock.Object,
            graphMock.Object,
            Microsoft.Extensions.Options.Options.Create(config),
            NullLogger<GitSyncRunner>.Instance);

        // Act
        var result = await runner.RunAsync("nonexistent");

        // Assert
        result.ShouldBe(1);
        gitSyncMock.Verify(
            s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_ValidRepoNoFiles_Returns0()
    {
        // Arrange
        var gitSyncMock = new Mock<IGitSyncService>();
        var ingestionMock = new Mock<IDocumentIngestionService>();
        var graphMock = new Mock<IGraphRepository>();
        var config = new CompoundDocsCloudConfig
        {
            Repositories = [new RepositoryConfig { Name = "my-repo", Url = "https://example.com" }]
        };

        graphMock
            .Setup(g => g.GetSyncStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        gitSyncMock
            .Setup(s => s.GetHeadCommitHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123");
        gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var runner = new GitSyncRunner(
            gitSyncMock.Object,
            ingestionMock.Object,
            graphMock.Object,
            Microsoft.Extensions.Options.Options.Create(config),
            NullLogger<GitSyncRunner>.Instance);

        // Act
        var result = await runner.RunAsync("my-repo");

        // Assert
        result.ShouldBe(0);
        gitSyncMock.Verify(
            s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);
        gitSyncMock.Verify(
            s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_ChangedFiles_IngestsEachFile()
    {
        // Arrange
        var gitSyncMock = new Mock<IGitSyncService>();
        var ingestionMock = new Mock<IDocumentIngestionService>();
        var graphMock = new Mock<IGraphRepository>();
        var config = new CompoundDocsCloudConfig
        {
            Repositories = [new RepositoryConfig { Name = "my-repo", Url = "https://example.com" }]
        };

        graphMock
            .Setup(g => g.GetSyncStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        gitSyncMock
            .Setup(s => s.GetHeadCommitHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123");
        gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChangedFile { Path = "docs/a.md", ChangeType = ChangeType.Added },
                new ChangedFile { Path = "docs/b.md", ChangeType = ChangeType.Modified }
            ]);
        gitSyncMock
            .Setup(s => s.ReadFileContentAsync("/repos/my-repo", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("file content");

        var runner = new GitSyncRunner(
            gitSyncMock.Object,
            ingestionMock.Object,
            graphMock.Object,
            Microsoft.Extensions.Options.Options.Create(config),
            NullLogger<GitSyncRunner>.Instance);

        // Act
        var result = await runner.RunAsync("my-repo");

        // Assert
        result.ShouldBe(0);
        ingestionMock.Verify(
            s => s.IngestDocumentAsync(
                "file content",
                It.Is<DocumentIngestionMetadata>(m => m.DocumentId == "my-repo:docs/a.md"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        ingestionMock.Verify(
            s => s.IngestDocumentAsync(
                "file content",
                It.Is<DocumentIngestionMetadata>(m => m.DocumentId == "my-repo:docs/b.md"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_DeletedFiles_CallsDeleteDocumentAsync()
    {
        // Arrange
        var gitSyncMock = new Mock<IGitSyncService>();
        var ingestionMock = new Mock<IDocumentIngestionService>();
        var graphMock = new Mock<IGraphRepository>();
        var config = new CompoundDocsCloudConfig
        {
            Repositories = [new RepositoryConfig { Name = "my-repo", Url = "https://example.com" }]
        };

        graphMock
            .Setup(g => g.GetSyncStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        gitSyncMock
            .Setup(s => s.GetHeadCommitHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123");
        gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChangedFile { Path = "docs/removed.md", ChangeType = ChangeType.Deleted }
            ]);

        var runner = new GitSyncRunner(
            gitSyncMock.Object,
            ingestionMock.Object,
            graphMock.Object,
            Microsoft.Extensions.Options.Options.Create(config),
            NullLogger<GitSyncRunner>.Instance);

        // Act
        var result = await runner.RunAsync("my-repo");

        // Assert
        result.ShouldBe(0);
        ingestionMock.Verify(
            s => s.DeleteDocumentAsync("my-repo:docs/removed.md", It.IsAny<CancellationToken>()),
            Times.Once);
        gitSyncMock.Verify(
            s => s.ReadFileContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_DocumentId_IsLowercaseRepoColonPath()
    {
        // Arrange
        var gitSyncMock = new Mock<IGitSyncService>();
        var ingestionMock = new Mock<IDocumentIngestionService>();
        var graphMock = new Mock<IGraphRepository>();
        var config = new CompoundDocsCloudConfig
        {
            Repositories = [new RepositoryConfig { Name = "My-Repo", Url = "https://example.com" }]
        };

        graphMock
            .Setup(g => g.GetSyncStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        gitSyncMock
            .Setup(s => s.GetHeadCommitHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123");
        gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChangedFile { Path = "Docs/Guide.md", ChangeType = ChangeType.Added }
            ]);
        gitSyncMock
            .Setup(s => s.ReadFileContentAsync("/repos/my-repo", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("content");

        var runner = new GitSyncRunner(
            gitSyncMock.Object,
            ingestionMock.Object,
            graphMock.Object,
            Microsoft.Extensions.Options.Options.Create(config),
            NullLogger<GitSyncRunner>.Instance);

        // Act
        await runner.RunAsync("my-repo");

        // Assert
        ingestionMock.Verify(
            s => s.IngestDocumentAsync(
                It.IsAny<string>(),
                It.Is<DocumentIngestionMetadata>(m => m.DocumentId == "my-repo:docs/guide.md"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_MonitoredPaths_OnlyMatchingFilesProcessed()
    {
        // Arrange
        var gitSyncMock = new Mock<IGitSyncService>();
        var ingestionMock = new Mock<IDocumentIngestionService>();
        var graphMock = new Mock<IGraphRepository>();
        var config = new CompoundDocsCloudConfig
        {
            Repositories =
            [
                new RepositoryConfig
                {
                    Name = "my-repo",
                    Url = "https://example.com",
                    MonitoredPaths = ["docs/"]
                }
            ]
        };

        graphMock
            .Setup(g => g.GetSyncStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        gitSyncMock
            .Setup(s => s.GetHeadCommitHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123");
        gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChangedFile { Path = "docs/guide.md", ChangeType = ChangeType.Added },
                new ChangedFile { Path = "src/Program.cs", ChangeType = ChangeType.Added }
            ]);
        gitSyncMock
            .Setup(s => s.ReadFileContentAsync("/repos/my-repo", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("content");

        var runner = new GitSyncRunner(
            gitSyncMock.Object,
            ingestionMock.Object,
            graphMock.Object,
            Microsoft.Extensions.Options.Options.Create(config),
            NullLogger<GitSyncRunner>.Instance);

        // Act
        var result = await runner.RunAsync("my-repo");

        // Assert
        result.ShouldBe(0);
        ingestionMock.Verify(
            s => s.IngestDocumentAsync(
                It.IsAny<string>(),
                It.Is<DocumentIngestionMetadata>(m => m.DocumentId == "my-repo:docs/guide.md"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        ingestionMock.Verify(
            s => s.IngestDocumentAsync(
                It.IsAny<string>(),
                It.Is<DocumentIngestionMetadata>(m => m.FilePath.StartsWith("src/")),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_DeletedFileOutsideMonitoredPaths_SkipsDelete()
    {
        // Arrange
        var gitSyncMock = new Mock<IGitSyncService>();
        var ingestionMock = new Mock<IDocumentIngestionService>();
        var graphMock = new Mock<IGraphRepository>();
        var config = new CompoundDocsCloudConfig
        {
            Repositories =
            [
                new RepositoryConfig
                {
                    Name = "my-repo",
                    Url = "https://example.com",
                    MonitoredPaths = ["docs/"]
                }
            ]
        };

        graphMock
            .Setup(g => g.GetSyncStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        gitSyncMock
            .Setup(s => s.GetHeadCommitHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123");
        gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChangedFile { Path = "src/old.cs", ChangeType = ChangeType.Deleted }
            ]);

        var runner = new GitSyncRunner(
            gitSyncMock.Object,
            ingestionMock.Object,
            graphMock.Object,
            Microsoft.Extensions.Options.Options.Create(config),
            NullLogger<GitSyncRunner>.Instance);

        // Act
        await runner.RunAsync("my-repo");

        // Assert
        ingestionMock.Verify(
            s => s.DeleteDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_EmptyMonitoredPaths_AllFilesIncluded()
    {
        // Arrange
        var gitSyncMock = new Mock<IGitSyncService>();
        var ingestionMock = new Mock<IDocumentIngestionService>();
        var graphMock = new Mock<IGraphRepository>();
        var config = new CompoundDocsCloudConfig
        {
            Repositories =
            [
                new RepositoryConfig
                {
                    Name = "my-repo",
                    Url = "https://example.com",
                    MonitoredPaths = []
                }
            ]
        };

        graphMock
            .Setup(g => g.GetSyncStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        gitSyncMock
            .Setup(s => s.GetHeadCommitHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123");
        gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChangedFile { Path = "docs/guide.md", ChangeType = ChangeType.Added },
                new ChangedFile { Path = "src/Program.cs", ChangeType = ChangeType.Added }
            ]);
        gitSyncMock
            .Setup(s => s.ReadFileContentAsync("/repos/my-repo", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("content");

        var runner = new GitSyncRunner(
            gitSyncMock.Object,
            ingestionMock.Object,
            graphMock.Object,
            Microsoft.Extensions.Options.Options.Create(config),
            NullLogger<GitSyncRunner>.Instance);

        // Act
        var result = await runner.RunAsync("my-repo");

        // Assert
        result.ShouldBe(0);
        ingestionMock.Verify(
            s => s.IngestDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<DocumentIngestionMetadata>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RunAsync_CaseInsensitiveRepoNameMatch()
    {
        // Arrange
        var gitSyncMock = new Mock<IGitSyncService>();
        var ingestionMock = new Mock<IDocumentIngestionService>();
        var graphMock = new Mock<IGraphRepository>();
        var config = new CompoundDocsCloudConfig
        {
            Repositories = [new RepositoryConfig { Name = "My-Repo", Url = "https://example.com" }]
        };

        graphMock
            .Setup(g => g.GetSyncStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        gitSyncMock
            .Setup(s => s.GetHeadCommitHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123");
        gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var runner = new GitSyncRunner(
            gitSyncMock.Object,
            ingestionMock.Object,
            graphMock.Object,
            Microsoft.Extensions.Options.Options.Create(config),
            NullLogger<GitSyncRunner>.Instance);

        // Act
        var result = await runner.RunAsync("my-repo");

        // Assert
        result.ShouldBe(0);
        gitSyncMock.Verify(
            s => s.CloneOrUpdateAsync(
                It.Is<RepositoryConfig>(r => r.Name == "My-Repo"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_ReadsSyncStateBeforeGetChangedFiles()
    {
        // Arrange
        var gitSyncMock = new Mock<IGitSyncService>();
        var ingestionMock = new Mock<IDocumentIngestionService>();
        var graphMock = new Mock<IGraphRepository>();
        var config = new CompoundDocsCloudConfig
        {
            Repositories = [new RepositoryConfig { Name = "my-repo", Url = "https://example.com" }]
        };

        graphMock
            .Setup(g => g.GetSyncStateAsync("my-repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync("prev-hash-123");
        gitSyncMock
            .Setup(s => s.GetHeadCommitHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123");
        gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), "prev-hash-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var runner = new GitSyncRunner(
            gitSyncMock.Object,
            ingestionMock.Object,
            graphMock.Object,
            Microsoft.Extensions.Options.Options.Create(config),
            NullLogger<GitSyncRunner>.Instance);

        // Act
        await runner.RunAsync("my-repo");

        // Assert
        graphMock.Verify(
            g => g.GetSyncStateAsync("my-repo", It.IsAny<CancellationToken>()),
            Times.Once);
        gitSyncMock.Verify(
            s => s.GetChangedFilesAsync(
                It.IsAny<RepositoryConfig>(),
                "prev-hash-123",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WritesSyncStateAfterSync()
    {
        // Arrange
        var gitSyncMock = new Mock<IGitSyncService>();
        var ingestionMock = new Mock<IDocumentIngestionService>();
        var graphMock = new Mock<IGraphRepository>();
        var config = new CompoundDocsCloudConfig
        {
            Repositories = [new RepositoryConfig { Name = "my-repo", Url = "https://example.com" }]
        };

        graphMock
            .Setup(g => g.GetSyncStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        gitSyncMock
            .Setup(s => s.GetHeadCommitHashAsync("/repos/my-repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-head-abc");

        var runner = new GitSyncRunner(
            gitSyncMock.Object,
            ingestionMock.Object,
            graphMock.Object,
            Microsoft.Extensions.Options.Options.Create(config),
            NullLogger<GitSyncRunner>.Instance);

        // Act
        await runner.RunAsync("my-repo");

        // Assert
        graphMock.Verify(
            g => g.SetSyncStateAsync("my-repo", "new-head-abc", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData("docs/getting-started.md", "getting started")]
    [InlineData("architecture_overview.md", "architecture overview")]
    [InlineData("README.md", "README")]
    public void DeriveTitle_ConvertsFilenameCorrectly(string filePath, string expected)
    {
        GitSyncRunner.DeriveTitle(filePath).ShouldBe(expected);
    }

    [Fact]
    public async Task RunAsync_IngestMetadata_HasCorrectRepository()
    {
        // Arrange
        var gitSyncMock = new Mock<IGitSyncService>();
        var ingestionMock = new Mock<IDocumentIngestionService>();
        var graphMock = new Mock<IGraphRepository>();
        var config = new CompoundDocsCloudConfig
        {
            Repositories = [new RepositoryConfig { Name = "My-Repo", Url = "https://example.com" }]
        };

        graphMock
            .Setup(g => g.GetSyncStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        gitSyncMock
            .Setup(s => s.GetHeadCommitHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123");
        gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChangedFile { Path = "docs/guide.md", ChangeType = ChangeType.Added }
            ]);
        gitSyncMock
            .Setup(s => s.ReadFileContentAsync("/repos/my-repo", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("content");

        var runner = new GitSyncRunner(
            gitSyncMock.Object,
            ingestionMock.Object,
            graphMock.Object,
            Microsoft.Extensions.Options.Options.Create(config),
            NullLogger<GitSyncRunner>.Instance);

        // Act
        await runner.RunAsync("my-repo");

        // Assert
        ingestionMock.Verify(
            s => s.IngestDocumentAsync(
                It.IsAny<string>(),
                It.Is<DocumentIngestionMetadata>(m => m.Repository == "my-repo"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
