using CompoundDocs.Common.Configuration;
using CompoundDocs.GitSync;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using CompoundDocs.McpServer.Background;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace CompoundDocs.Tests.Unit.Background;

public sealed class GitSyncBackgroundServiceTests
{
    private readonly Mock<IGitSyncService> _gitSyncMock = new();
    private readonly Mock<IDocumentIngestionService> _ingestionMock = new();
    private readonly Mock<IGraphRepository> _graphMock = new();
    private readonly CompoundDocsCloudConfig _cloudConfig = new();
    private readonly GitSyncConfig _gitSyncConfig = new() { IntervalSeconds = 1 };

    public GitSyncBackgroundServiceTests()
    {
        _graphMock
            .Setup(g => g.GetSyncStateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _gitSyncMock
            .Setup(s => s.GetHeadCommitHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("abc123");
    }

    private GitSyncRunner CreateRunner() =>
        new(
            _gitSyncMock.Object,
            _ingestionMock.Object,
            _graphMock.Object,
            Microsoft.Extensions.Options.Options.Create(_cloudConfig),
            NullLogger<GitSyncRunner>.Instance);

    private GitSyncBackgroundService CreateService() =>
        new(
            CreateRunner(),
            Microsoft.Extensions.Options.Options.Create(_cloudConfig),
            Microsoft.Extensions.Options.Options.Create(_gitSyncConfig),
            NullLogger<GitSyncBackgroundService>.Instance);

    [Fact]
    public async Task RunSyncCycleAsync_NoRepositories_LogsWarningAndReturns()
    {
        _cloudConfig.Repositories = [];
        var sut = CreateService();

        await sut.RunSyncCycleAsync(CancellationToken.None);

        sut.LastSuccessfulRun.ShouldBeNull();
        sut.LastRunFailed.ShouldBeFalse();
    }

    [Fact]
    public async Task RunSyncCycleAsync_SingleRepo_CallsRunnerAndSetsLastSuccess()
    {
        _cloudConfig.Repositories = [new RepositoryConfig { Name = "my-repo" }];
        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        _gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = CreateService();
        await sut.RunSyncCycleAsync(CancellationToken.None);

        sut.LastSuccessfulRun.ShouldNotBeNull();
        sut.LastRunFailed.ShouldBeFalse();
    }

    [Fact]
    public async Task RunSyncCycleAsync_MultipleRepos_CallsRunnerForEach()
    {
        _cloudConfig.Repositories =
        [
            new RepositoryConfig { Name = "repo-a" },
            new RepositoryConfig { Name = "repo-b" }
        ];
        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/default");
        _gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = CreateService();
        await sut.RunSyncCycleAsync(CancellationToken.None);

        _gitSyncMock.Verify(
            s => s.CloneOrUpdateAsync(
                It.Is<RepositoryConfig>(r => r.Name == "repo-a"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _gitSyncMock.Verify(
            s => s.CloneOrUpdateAsync(
                It.Is<RepositoryConfig>(r => r.Name == "repo-b"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunSyncCycleAsync_OneRepoFails_OtherRepoStillProcessed()
    {
        _cloudConfig.Repositories =
        [
            new RepositoryConfig { Name = "fail-repo" },
            new RepositoryConfig { Name = "ok-repo" }
        ];
        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(
                It.Is<RepositoryConfig>(r => r.Name == "fail-repo"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("clone failed"));
        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(
                It.Is<RepositoryConfig>(r => r.Name == "ok-repo"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/ok-repo");
        _gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = CreateService();
        await sut.RunSyncCycleAsync(CancellationToken.None);

        sut.LastRunFailed.ShouldBeTrue();
        _gitSyncMock.Verify(
            s => s.CloneOrUpdateAsync(
                It.Is<RepositoryConfig>(r => r.Name == "ok-repo"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunSyncCycleAsync_CancellationRequested_PropagatesCancellation()
    {
        _cloudConfig.Repositories = [new RepositoryConfig { Name = "my-repo" }];
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var sut = CreateService();

        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.RunSyncCycleAsync(cts.Token));
    }

    [Fact]
    public void IntervalSeconds_ReturnsConfiguredValue()
    {
        _gitSyncConfig.IntervalSeconds = 3600;
        var sut = CreateService();

        sut.IntervalSeconds.ShouldBe(3600);
    }

    [Fact]
    public async Task ExecuteAsync_RunsInitialSyncThenStopsOnCancellation()
    {
        _cloudConfig.Repositories = [new RepositoryConfig { Name = "my-repo" }];
        _gitSyncMock
            .Setup(s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/repos/my-repo");
        _gitSyncMock
            .Setup(s => s.GetChangedFilesAsync(It.IsAny<RepositoryConfig>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var sut = CreateService();

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        await sut.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        await sut.StopAsync(CancellationToken.None);

        _gitSyncMock.Verify(
            s => s.CloneOrUpdateAsync(It.IsAny<RepositoryConfig>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}
