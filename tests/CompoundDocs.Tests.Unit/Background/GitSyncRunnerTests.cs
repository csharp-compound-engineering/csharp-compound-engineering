using CompoundDocs.Common.Configuration;
using CompoundDocs.GitSync;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using CompoundDocs.McpServer.Background;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace CompoundDocs.Tests.Unit.Background;

public sealed class GitSyncRunnerTests
{
    private readonly Mock<IGitSyncService> _gitSyncMock = new();
    private readonly Mock<IDocumentIngestionService> _ingestionMock = new();
    private readonly Mock<IGraphRepository> _graphMock = new();
    private readonly CompoundDocsCloudConfig _config = new();
    private readonly GitSyncRunner _runner;

    public GitSyncRunnerTests()
    {
        var options = Microsoft.Extensions.Options.Options.Create(_config);

        _graphMock
            .Setup(g => g.GetSyncStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _gitSyncMock
            .Setup(s => s.GetHeadCommitHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123");

        _runner = new GitSyncRunner(
            _gitSyncMock.Object,
            _ingestionMock.Object,
            _graphMock.Object,
            options,
            NullLogger<GitSyncRunner>.Instance);
    }

    [Fact]
    public async Task RunAsync_UnknownRepo_Returns1AndNoGitCalls()
    {
        _config.Repositories = [new RepositoryConfig { Name = "other-repo" }];

        var result = await _runner.RunAsync("nonexistent");

        result.ShouldBe(1);
        _gitSyncMock.Verify(
            s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_ValidRepoNoFiles_Returns0()
    {
        _config.Repositories = [new RepositoryConfig { Name = "my-repo", Url = "https://example.com" }];
        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        _gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _runner.RunAsync("my-repo");

        result.ShouldBe(0);
        _gitSyncMock.Verify(
            s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _gitSyncMock.Verify(
            s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_ChangedFiles_IngestsEachFile()
    {
        _config.Repositories = [new RepositoryConfig { Name = "my-repo", Url = "https://example.com" }];
        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        _gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChangedFile { Path = "docs/a.md", ChangeType = ChangeType.Added },
                new ChangedFile { Path = "docs/b.md", ChangeType = ChangeType.Modified }
            ]);
        _gitSyncMock
            .Setup(s => s.ReadFileContentAsync("/repos/my-repo", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("file content");

        var result = await _runner.RunAsync("my-repo");

        result.ShouldBe(0);
        _ingestionMock.Verify(
            s => s.IngestDocumentAsync(
                "file content",
                It.Is<DocumentIngestionMetadata>(m => m.DocumentId == "my-repo:docs/a.md"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _ingestionMock.Verify(
            s => s.IngestDocumentAsync(
                "file content",
                It.Is<DocumentIngestionMetadata>(m => m.DocumentId == "my-repo:docs/b.md"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_DeletedFiles_CallsDeleteDocumentAsync()
    {
        _config.Repositories = [new RepositoryConfig { Name = "my-repo", Url = "https://example.com" }];
        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        _gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChangedFile { Path = "docs/removed.md", ChangeType = ChangeType.Deleted }
            ]);

        var result = await _runner.RunAsync("my-repo");

        result.ShouldBe(0);
        _ingestionMock.Verify(
            s => s.DeleteDocumentAsync("my-repo:docs/removed.md", It.IsAny<CancellationToken>()),
            Times.Once);
        _gitSyncMock.Verify(
            s => s.ReadFileContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_DocumentId_IsLowercaseRepoColonPath()
    {
        _config.Repositories = [new RepositoryConfig { Name = "My-Repo", Url = "https://example.com" }];
        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        _gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChangedFile { Path = "Docs/Guide.md", ChangeType = ChangeType.Added }
            ]);
        _gitSyncMock
            .Setup(s => s.ReadFileContentAsync("/repos/my-repo", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("content");

        await _runner.RunAsync("my-repo");

        _ingestionMock.Verify(
            s => s.IngestDocumentAsync(
                It.IsAny<string>(),
                It.Is<DocumentIngestionMetadata>(m => m.DocumentId == "my-repo:docs/guide.md"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_MonitoredPaths_OnlyMatchingFilesProcessed()
    {
        _config.Repositories =
        [
            new RepositoryConfig
            {
                Name = "my-repo",
                Url = "https://example.com",
                MonitoredPaths = ["docs/"]
            }
        ];
        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        _gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChangedFile { Path = "docs/guide.md", ChangeType = ChangeType.Added },
                new ChangedFile { Path = "src/Program.cs", ChangeType = ChangeType.Added }
            ]);
        _gitSyncMock
            .Setup(s => s.ReadFileContentAsync("/repos/my-repo", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("content");

        var result = await _runner.RunAsync("my-repo");

        result.ShouldBe(0);
        _ingestionMock.Verify(
            s => s.IngestDocumentAsync(
                It.IsAny<string>(),
                It.Is<DocumentIngestionMetadata>(m => m.DocumentId == "my-repo:docs/guide.md"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _ingestionMock.Verify(
            s => s.IngestDocumentAsync(
                It.IsAny<string>(),
                It.Is<DocumentIngestionMetadata>(m => m.FilePath.StartsWith("src/")),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_DeletedFileOutsideMonitoredPaths_SkipsDelete()
    {
        _config.Repositories =
        [
            new RepositoryConfig
            {
                Name = "my-repo",
                Url = "https://example.com",
                MonitoredPaths = ["docs/"]
            }
        ];
        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        _gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChangedFile { Path = "src/old.cs", ChangeType = ChangeType.Deleted }
            ]);

        await _runner.RunAsync("my-repo");

        _ingestionMock.Verify(
            s => s.DeleteDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunAsync_EmptyMonitoredPaths_AllFilesIncluded()
    {
        _config.Repositories =
        [
            new RepositoryConfig
            {
                Name = "my-repo",
                Url = "https://example.com",
                MonitoredPaths = []
            }
        ];
        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        _gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChangedFile { Path = "docs/guide.md", ChangeType = ChangeType.Added },
                new ChangedFile { Path = "src/Program.cs", ChangeType = ChangeType.Added }
            ]);
        _gitSyncMock
            .Setup(s => s.ReadFileContentAsync("/repos/my-repo", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("content");

        var result = await _runner.RunAsync("my-repo");

        result.ShouldBe(0);
        _ingestionMock.Verify(
            s => s.IngestDocumentAsync(
                It.IsAny<string>(),
                It.IsAny<DocumentIngestionMetadata>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RunAsync_CaseInsensitiveRepoNameMatch()
    {
        _config.Repositories = [new RepositoryConfig { Name = "My-Repo", Url = "https://example.com" }];
        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        _gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _runner.RunAsync("my-repo");

        result.ShouldBe(0);
        _gitSyncMock.Verify(
            s => s.CloneOrUpdateAsync(
                It.Is<RepositoryConfig>(r => r.Name == "My-Repo"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_ReadsSyncStateBeforeGetChangedFiles()
    {
        _config.Repositories = [new RepositoryConfig { Name = "my-repo", Url = "https://example.com" }];

        _graphMock
            .Setup(g => g.GetSyncStateAsync("my-repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync("prev-hash-123");
        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        _gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), "prev-hash-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await _runner.RunAsync("my-repo");

        _graphMock.Verify(
            g => g.GetSyncStateAsync("my-repo", It.IsAny<CancellationToken>()),
            Times.Once);
        _gitSyncMock.Verify(
            s => s.GetChangedFilesAsync(
                It.IsAny<RepositoryConfig>(),
                "prev-hash-123",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WritesSyncStateAfterSync()
    {
        _config.Repositories = [new RepositoryConfig { Name = "my-repo", Url = "https://example.com" }];
        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        _gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _gitSyncMock
            .Setup(s => s.GetHeadCommitHashAsync("/repos/my-repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-head-abc");

        await _runner.RunAsync("my-repo");

        _graphMock.Verify(
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
        _config.Repositories = [new RepositoryConfig { Name = "My-Repo", Url = "https://example.com" }];
        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        _gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ChangedFile { Path = "docs/guide.md", ChangeType = ChangeType.Added }
            ]);
        _gitSyncMock
            .Setup(s => s.ReadFileContentAsync("/repos/my-repo", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("content");

        await _runner.RunAsync("my-repo");

        _ingestionMock.Verify(
            s => s.IngestDocumentAsync(
                It.IsAny<string>(),
                It.Is<DocumentIngestionMetadata>(m => m.Repository == "my-repo"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
