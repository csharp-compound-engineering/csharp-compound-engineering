using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CompoundDocs.Cleanup;

public sealed class CleanupWorker : BackgroundService
{
    private readonly ILogger<CleanupWorker> _logger;
    private readonly IOptions<CleanupOptions> _options;
    private readonly NpgsqlDataSource _dataSource;
    private readonly IHostApplicationLifetime _lifetime;

    public CleanupWorker(
        ILogger<CleanupWorker> logger,
        IOptions<CleanupOptions> options,
        NpgsqlDataSource dataSource,
        IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _options = options;
        _dataSource = dataSource;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Cleanup worker starting. DryRun={DryRun}, RunOnce={RunOnce}",
            _options.Value.DryRun, _options.Value.RunOnce);

        try
        {
            do
            {
                await RunCleanupCycleAsync(stoppingToken);

                if (_options.Value.RunOnce)
                {
                    _logger.LogInformation("Single cleanup cycle completed. Stopping.");
                    _lifetime.StopApplication();
                    break;
                }

                var delay = TimeSpan.FromMinutes(_options.Value.IntervalMinutes);
                _logger.LogInformation("Next cleanup in {Minutes} minutes", _options.Value.IntervalMinutes);
                await Task.Delay(delay, stoppingToken);
            }
            while (!stoppingToken.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Cleanup worker stopping due to cancellation");
        }
    }

    private async Task RunCleanupCycleAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Starting cleanup cycle");

        var orphanedPaths = await CleanOrphanedPathsAsync(ct);
        var orphanedBranches = await CleanOrphanedBranchesAsync(ct);

        sw.Stop();
        _logger.LogInformation(
            "Cleanup cycle completed in {ElapsedMs}ms. Removed {Paths} paths, {Branches} branches",
            sw.ElapsedMilliseconds, orphanedPaths, orphanedBranches);
    }

    private async Task<int> CleanOrphanedPathsAsync(CancellationToken ct)
    {
        var orphanedCount = 0;

        await using var connection = await _dataSource.OpenConnectionAsync(ct);

        // Query all repo paths
        await using var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = """
            SELECT id, absolute_path, project_name
            FROM tenant_management.repo_paths
            """;

        var pathsToDelete = new List<(Guid Id, string Path, string Project)>();

        await using (var reader = await selectCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0);
                var absolutePath = reader.GetString(1);
                var projectName = reader.GetString(2);

                if (!Directory.Exists(absolutePath))
                {
                    pathsToDelete.Add((id, absolutePath, projectName));
                    _logger.LogInformation(
                        "Found orphaned path: {Project} at {Path}", projectName, absolutePath);
                }
            }
        }

        if (_options.Value.DryRun)
        {
            _logger.LogInformation("[DRY RUN] Would delete {Count} orphaned paths", pathsToDelete.Count);
            return pathsToDelete.Count;
        }

        foreach (var (id, path, project) in pathsToDelete)
        {
            await using var deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = """
                DELETE FROM tenant_management.repo_paths WHERE id = @id
                """;
            deleteCmd.Parameters.AddWithValue("id", id);

            var deleted = await deleteCmd.ExecuteNonQueryAsync(ct);
            if (deleted > 0)
            {
                orphanedCount++;
                _logger.LogInformation("Deleted orphaned path: {Project} at {Path}", project, path);
            }
        }

        return orphanedCount;
    }

    private async Task<int> CleanOrphanedBranchesAsync(CancellationToken ct)
    {
        var orphanedCount = 0;

        await using var connection = await _dataSource.OpenConnectionAsync(ct);

        // Get distinct repo paths with their branches
        await using var selectCmd = connection.CreateCommand();
        selectCmd.CommandText = """
            SELECT DISTINCT rp.id, rp.absolute_path, b.id, b.branch_name
            FROM tenant_management.repo_paths rp
            JOIN tenant_management.branches b ON b.repo_path_id = rp.id
            ORDER BY rp.id
            """;

        var branchesToCheck = new List<(Guid RepoId, string RepoPath, Guid BranchId, string BranchName)>();

        await using (var reader = await selectCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                branchesToCheck.Add((
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetGuid(2),
                    reader.GetString(3)
                ));
            }
        }

        // Group by repo and check remote branches
        foreach (var repoGroup in branchesToCheck.GroupBy(x => (x.RepoId, x.RepoPath)))
        {
            var (_, repoPath) = repoGroup.Key;

            if (!Directory.Exists(repoPath))
            {
                // Repo doesn't exist, path cleanup will handle this
                continue;
            }

            var remoteBranches = await GetRemoteBranchesAsync(repoPath, ct);

            foreach (var (_, _, branchId, branchName) in repoGroup)
            {
                if (!remoteBranches.Contains(branchName))
                {
                    _logger.LogInformation(
                        "Found orphaned branch: {Branch} in {Path}", branchName, repoPath);

                    if (_options.Value.DryRun)
                    {
                        orphanedCount++;
                        continue;
                    }

                    await using var deleteCmd = connection.CreateCommand();
                    deleteCmd.CommandText = """
                        DELETE FROM tenant_management.branches WHERE id = @id
                        """;
                    deleteCmd.Parameters.AddWithValue("id", branchId);

                    var deleted = await deleteCmd.ExecuteNonQueryAsync(ct);
                    if (deleted > 0)
                    {
                        orphanedCount++;
                        _logger.LogInformation("Deleted orphaned branch: {Branch}", branchName);
                    }
                }
            }
        }

        if (_options.Value.DryRun)
        {
            _logger.LogInformation("[DRY RUN] Would delete {Count} orphaned branches", orphanedCount);
        }

        return orphanedCount;
    }

    private async Task<HashSet<string>> GetRemoteBranchesAsync(string repoPath, CancellationToken ct)
    {
        var branches = new HashSet<string>();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "ls-remote --heads origin",
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return branches;

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            // Parse output: each line is "{sha}\trefs/heads/{branchname}"
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('\t');
                if (parts.Length >= 2 && parts[1].StartsWith("refs/heads/"))
                {
                    var branchName = parts[1]["refs/heads/".Length..];
                    branches.Add(branchName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get remote branches for {Path}", repoPath);
        }

        return branches;
    }
}
