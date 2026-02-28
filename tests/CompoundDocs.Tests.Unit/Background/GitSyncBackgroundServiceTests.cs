using CompoundDocs.Common.Configuration;
using CompoundDocs.GitSync;
using CompoundDocs.McpServer.Background;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Unit.Background;

public sealed class GitSyncBackgroundServiceTests
{
    private static GitSyncBackgroundService CreateService(
        Mock<IGitSyncRunner> runnerMock,
        CompoundDocsCloudConfig? cloudConfig = null,
        GitSyncConfig? gitSyncConfig = null) =>
        new(
            runnerMock.Object,
            Microsoft.Extensions.Options.Options.Create(cloudConfig ?? new CompoundDocsCloudConfig()),
            Microsoft.Extensions.Options.Options.Create(gitSyncConfig ?? new GitSyncConfig { IntervalSeconds = 1 }),
            NullLogger<GitSyncBackgroundService>.Instance);

    [Fact]
    public async Task RunSyncCycleAsync_NoRepositories_LogsWarningAndReturns()
    {
        var runnerMock = new Mock<IGitSyncRunner>();
        var cloudConfig = new CompoundDocsCloudConfig { Repositories = [] };
        var sut = CreateService(runnerMock, cloudConfig);

        await sut.RunSyncCycleAsync(CancellationToken.None);

        sut.LastSuccessfulRun.ShouldBeNull();
        sut.LastRunFailed.ShouldBeFalse();
    }

    [Fact]
    public async Task RunSyncCycleAsync_SingleRepo_CallsRunnerAndSetsLastSuccess()
    {
        var runnerMock = new Mock<IGitSyncRunner>();
        var cloudConfig = new CompoundDocsCloudConfig
        {
            Repositories = [new RepositoryConfig { Name = "my-repo" }]
        };
        runnerMock
            .Setup(r => r.RunAsync("my-repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var sut = CreateService(runnerMock, cloudConfig);
        await sut.RunSyncCycleAsync(CancellationToken.None);

        sut.LastSuccessfulRun.ShouldNotBeNull();
        sut.LastRunFailed.ShouldBeFalse();
        runnerMock.Verify(r => r.RunAsync("my-repo", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSyncCycleAsync_MultipleRepos_CallsRunnerForEach()
    {
        var runnerMock = new Mock<IGitSyncRunner>();
        var cloudConfig = new CompoundDocsCloudConfig
        {
            Repositories =
            [
                new RepositoryConfig { Name = "repo-a" },
                new RepositoryConfig { Name = "repo-b" }
            ]
        };
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var sut = CreateService(runnerMock, cloudConfig);
        await sut.RunSyncCycleAsync(CancellationToken.None);

        runnerMock.Verify(r => r.RunAsync("repo-a", It.IsAny<CancellationToken>()), Times.Once);
        runnerMock.Verify(r => r.RunAsync("repo-b", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSyncCycleAsync_OneRepoFails_OtherRepoStillProcessed()
    {
        var runnerMock = new Mock<IGitSyncRunner>();
        var cloudConfig = new CompoundDocsCloudConfig
        {
            Repositories =
            [
                new RepositoryConfig { Name = "fail-repo" },
                new RepositoryConfig { Name = "ok-repo" }
            ]
        };
        runnerMock
            .Setup(r => r.RunAsync("fail-repo", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("clone failed"));
        runnerMock
            .Setup(r => r.RunAsync("ok-repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var sut = CreateService(runnerMock, cloudConfig);
        await sut.RunSyncCycleAsync(CancellationToken.None);

        sut.LastRunFailed.ShouldBeTrue();
        runnerMock.Verify(r => r.RunAsync("ok-repo", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSyncCycleAsync_CancellationRequested_PropagatesCancellation()
    {
        var runnerMock = new Mock<IGitSyncRunner>();
        var cloudConfig = new CompoundDocsCloudConfig
        {
            Repositories = [new RepositoryConfig { Name = "my-repo" }]
        };
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var sut = CreateService(runnerMock, cloudConfig);

        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.RunSyncCycleAsync(cts.Token));
    }

    [Fact]
    public void IntervalSeconds_ReturnsConfiguredValue()
    {
        var runnerMock = new Mock<IGitSyncRunner>();
        var gitSyncConfig = new GitSyncConfig { IntervalSeconds = 3600 };
        var sut = CreateService(runnerMock, gitSyncConfig: gitSyncConfig);

        sut.IntervalSeconds.ShouldBe(3600);
    }

    [Fact]
    public async Task ExecuteAsync_RunsInitialSyncThenStopsOnCancellation()
    {
        var runnerMock = new Mock<IGitSyncRunner>();
        var cloudConfig = new CompoundDocsCloudConfig
        {
            Repositories = [new RepositoryConfig { Name = "my-repo" }]
        };
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var sut = CreateService(runnerMock, cloudConfig);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        await sut.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        await sut.StopAsync(CancellationToken.None);

        runnerMock.Verify(
            r => r.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}
