using CompoundDocs.Common.Configuration;
using CompoundDocs.GitSync;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Background;

internal sealed partial class GitSyncBackgroundService : BackgroundService, IGitSyncStatus
{
    [LoggerMessage(EventId = 100, Level = LogLevel.Information,
        Message = "Git sync background service starting with interval {IntervalSeconds}s")]
    private partial void LogStarting(int intervalSeconds);

    [LoggerMessage(EventId = 101, Level = LogLevel.Information,
        Message = "Running git sync for repository '{RepoName}'")]
    private partial void LogSyncingRepo(string repoName);

    [LoggerMessage(EventId = 102, Level = LogLevel.Error,
        Message = "Git sync failed for repository '{RepoName}'")]
    private partial void LogSyncFailed(string repoName, Exception exception);

    [LoggerMessage(EventId = 103, Level = LogLevel.Information,
        Message = "Git sync cycle completed")]
    private partial void LogCycleCompleted();

    [LoggerMessage(EventId = 104, Level = LogLevel.Warning,
        Message = "No repositories configured for git sync")]
    private partial void LogNoRepositories();

    [LoggerMessage(EventId = 105, Level = LogLevel.Information,
        Message = "Git sync background service stopped")]
    private partial void LogStopped();

    private readonly GitSyncRunner _runner;
    private readonly CompoundDocsCloudConfig _cloudConfig;
    private readonly GitSyncConfig _gitSyncConfig;
    private readonly ILogger<GitSyncBackgroundService> _logger;

    private DateTimeOffset? _lastSuccessfulRun;
    private bool _lastRunFailed;

    public GitSyncBackgroundService(
        GitSyncRunner runner,
        IOptions<CompoundDocsCloudConfig> cloudConfig,
        IOptions<GitSyncConfig> gitSyncConfig,
        ILogger<GitSyncBackgroundService> logger)
    {
        _runner = runner;
        _cloudConfig = cloudConfig.Value;
        _gitSyncConfig = gitSyncConfig.Value;
        _logger = logger;
    }

    public DateTimeOffset? LastSuccessfulRun => _lastSuccessfulRun;
    public bool LastRunFailed => _lastRunFailed;
    public int IntervalSeconds => _gitSyncConfig.IntervalSeconds;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarting(_gitSyncConfig.IntervalSeconds);

        // Run an initial sync immediately on startup
        await RunSyncCycleAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_gitSyncConfig.IntervalSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunSyncCycleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Clean shutdown
        }

        LogStopped();
    }

    internal async Task RunSyncCycleAsync(CancellationToken ct)
    {
        var repos = _cloudConfig.Repositories;

        if (repos is null || repos.Count == 0)
        {
            LogNoRepositories();
            return;
        }

        var allSucceeded = true;

        foreach (var repo in repos)
        {
            try
            {
                LogSyncingRepo(repo.Name);
                await _runner.RunAsync(repo.Name, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogSyncFailed(repo.Name, ex);
                allSucceeded = false;
            }
        }

        if (allSucceeded)
        {
            _lastSuccessfulRun = DateTimeOffset.UtcNow;
        }

        _lastRunFailed = !allSucceeded;
        LogCycleCompleted();
    }
}
