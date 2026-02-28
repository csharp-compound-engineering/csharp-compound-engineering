using CompoundDocs.Common.Configuration;
using CompoundDocs.GitSync;
using CompoundDocs.McpServer.Background;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompoundDocs.Tests.Unit.Background;

public sealed class GitSyncBackgroundServiceTests
{
    [Fact]
    public async Task RunSyncCycleAsync_NoRepositories_LogsWarningAndReturns()
    {
        // Arrange
        var runnerMock = new Mock<IGitSyncRunner>();
        var cloudConfig = new CompoundDocsCloudConfig { Repositories = [] };
        var sut = new GitSyncBackgroundService(
            runnerMock.Object,
            Microsoft.Extensions.Options.Options.Create(cloudConfig),
            Microsoft.Extensions.Options.Options.Create(new GitSyncConfig { IntervalSeconds = 1 }),
            NullLogger<GitSyncBackgroundService>.Instance);

        // Act
        await sut.RunSyncCycleAsync(CancellationToken.None);

        // Assert
        sut.LastSuccessfulRun.ShouldBeNull();
        sut.LastRunFailed.ShouldBeFalse();
    }

    [Fact]
    public async Task RunSyncCycleAsync_SingleRepo_CallsRunnerAndSetsLastSuccess()
    {
        // Arrange
        var runnerMock = new Mock<IGitSyncRunner>();
        var cloudConfig = new CompoundDocsCloudConfig
        {
            Repositories = [new RepositoryConfig { Name = "my-repo" }]
        };
        runnerMock
            .Setup(r => r.RunAsync("my-repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var sut = new GitSyncBackgroundService(
            runnerMock.Object,
            Microsoft.Extensions.Options.Options.Create(cloudConfig),
            Microsoft.Extensions.Options.Options.Create(new GitSyncConfig { IntervalSeconds = 1 }),
            NullLogger<GitSyncBackgroundService>.Instance);

        // Act
        await sut.RunSyncCycleAsync(CancellationToken.None);

        // Assert
        sut.LastSuccessfulRun.ShouldNotBeNull();
        sut.LastRunFailed.ShouldBeFalse();
        runnerMock.Verify(r => r.RunAsync("my-repo", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSyncCycleAsync_MultipleRepos_CallsRunnerForEach()
    {
        // Arrange
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

        var sut = new GitSyncBackgroundService(
            runnerMock.Object,
            Microsoft.Extensions.Options.Options.Create(cloudConfig),
            Microsoft.Extensions.Options.Options.Create(new GitSyncConfig { IntervalSeconds = 1 }),
            NullLogger<GitSyncBackgroundService>.Instance);

        // Act
        await sut.RunSyncCycleAsync(CancellationToken.None);

        // Assert
        runnerMock.Verify(r => r.RunAsync("repo-a", It.IsAny<CancellationToken>()), Times.Once);
        runnerMock.Verify(r => r.RunAsync("repo-b", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSyncCycleAsync_OneRepoFails_OtherRepoStillProcessed()
    {
        // Arrange
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

        var sut = new GitSyncBackgroundService(
            runnerMock.Object,
            Microsoft.Extensions.Options.Options.Create(cloudConfig),
            Microsoft.Extensions.Options.Options.Create(new GitSyncConfig { IntervalSeconds = 1 }),
            NullLogger<GitSyncBackgroundService>.Instance);

        // Act
        await sut.RunSyncCycleAsync(CancellationToken.None);

        // Assert
        sut.LastRunFailed.ShouldBeTrue();
        runnerMock.Verify(r => r.RunAsync("ok-repo", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunSyncCycleAsync_CancellationRequested_PropagatesCancellation()
    {
        // Arrange
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

        var sut = new GitSyncBackgroundService(
            runnerMock.Object,
            Microsoft.Extensions.Options.Options.Create(cloudConfig),
            Microsoft.Extensions.Options.Options.Create(new GitSyncConfig { IntervalSeconds = 1 }),
            NullLogger<GitSyncBackgroundService>.Instance);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.RunSyncCycleAsync(cts.Token));
    }

    [Fact]
    public void IntervalSeconds_ReturnsConfiguredValue()
    {
        // Arrange
        var runnerMock = new Mock<IGitSyncRunner>();
        var gitSyncConfig = new GitSyncConfig { IntervalSeconds = 3600 };
        var sut = new GitSyncBackgroundService(
            runnerMock.Object,
            Microsoft.Extensions.Options.Options.Create(new CompoundDocsCloudConfig()),
            Microsoft.Extensions.Options.Options.Create(gitSyncConfig),
            NullLogger<GitSyncBackgroundService>.Instance);

        // Assert
        sut.IntervalSeconds.ShouldBe(3600);
    }

    [Fact]
    public async Task ExecuteAsync_RunsInitialSyncThenStopsOnCancellation()
    {
        // Arrange
        var runnerMock = new Mock<IGitSyncRunner>();
        var cloudConfig = new CompoundDocsCloudConfig
        {
            Repositories = [new RepositoryConfig { Name = "my-repo" }]
        };
        runnerMock
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var sut = new GitSyncBackgroundService(
            runnerMock.Object,
            Microsoft.Extensions.Options.Options.Create(cloudConfig),
            Microsoft.Extensions.Options.Options.Create(new GitSyncConfig { IntervalSeconds = 1 }),
            NullLogger<GitSyncBackgroundService>.Instance);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        // Act
        await sut.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        await sut.StopAsync(CancellationToken.None);

        // Assert
        runnerMock.Verify(
            r => r.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}
