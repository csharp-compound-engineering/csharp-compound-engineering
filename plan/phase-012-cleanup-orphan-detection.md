# Phase 012: Cleanup App Orphan Detection Logic

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Infrastructure Setup
> **Prerequisites**: Phase 011 (Cleanup Worker Scaffold)

---

## Spec References

This phase implements the orphan detection logic defined in:

- **spec/infrastructure/cleanup-app.md** - [Orphan Detection](../spec/infrastructure/cleanup-app.md#orphan-detection) section
- **spec/infrastructure/cleanup-app.md** - [Implementation](../spec/infrastructure/cleanup-app.md#implementation) section (`CleanOrphanedPathsAsync` and `CleanOrphanedBranchesAsync` methods)

### Research References

- **research/git-current-branch-detection.md** - For `git ls-remote --heads origin` patterns and error handling

---

## Objectives

1. Implement orphaned path detection (repo paths where directory no longer exists on disk)
2. Implement orphaned branch detection (branches that no longer exist on git remote)
3. Build database query patterns for identifying orphan records
4. Create comprehensive logging for detected orphans
5. Support dry-run mode for safe preview of deletions

---

## Acceptance Criteria

### Orphaned Path Detection

- [ ] `OrphanedPathDetector` class implements `IOrphanedPathDetector` interface
- [ ] Queries all paths from `tenant_management.repo_paths` table
- [ ] For each path, verifies directory existence using `Directory.Exists(absolute_path)`
- [ ] Returns list of orphaned path records with metadata (path_hash, absolute_path, last_seen)
- [ ] Handles path access exceptions gracefully (permissions, network drives)

### Orphaned Branch Detection

- [ ] `OrphanedBranchDetector` class implements `IOrphanedBranchDetector` interface
- [ ] Groups branches by `project_name` to minimize git remote calls
- [ ] Retrieves repo path from `tenant_management.repo_paths` for each project
- [ ] Executes `git ls-remote --heads origin` to get remote branch list
- [ ] Parses git output to extract branch names
- [ ] Compares stored branches against remote branches
- [ ] Returns list of orphaned branch records with metadata (project_name, branch_name, last_seen)
- [ ] Handles git command failures gracefully (network issues, invalid remotes)

### Database Query Patterns

- [ ] `IOrphanRepository` interface defines orphan detection queries
- [ ] `GetAllRepoPaths()` - retrieves all paths from `tenant_management.repo_paths`
- [ ] `GetBranchesByProject(string projectName)` - retrieves branches for a specific project
- [ ] `GetDistinctProjectNames()` - retrieves unique project names with branches
- [ ] `GetRepoPathForProject(string projectName)` - retrieves the repo path for a project

### Logging

- [ ] Logs total count of paths/branches scanned
- [ ] Logs each detected orphan with identifying information
- [ ] Distinguishes between dry-run and actual execution in log messages
- [ ] Logs timing information for detection operations
- [ ] Uses structured logging with correlation IDs

---

## Implementation Notes

### OrphanedPathDetector Implementation

```csharp
public interface IOrphanedPathDetector
{
    Task<IReadOnlyList<OrphanedPath>> DetectOrphanedPathsAsync(
        CancellationToken cancellationToken = default);
}

public record OrphanedPath(
    string PathHash,
    string AbsolutePath,
    DateTime? LastSeen,
    string? DetectionReason);

public class OrphanedPathDetector : IOrphanedPathDetector
{
    private readonly IOrphanRepository _repository;
    private readonly ILogger<OrphanedPathDetector> _logger;

    public async Task<IReadOnlyList<OrphanedPath>> DetectOrphanedPathsAsync(
        CancellationToken cancellationToken = default)
    {
        var orphanedPaths = new List<OrphanedPath>();
        var allPaths = await _repository.GetAllRepoPathsAsync(cancellationToken);

        _logger.LogInformation("Scanning {Count} repo paths for orphans", allPaths.Count);

        foreach (var path in allPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!Directory.Exists(path.AbsolutePath))
                {
                    orphanedPaths.Add(new OrphanedPath(
                        path.PathHash,
                        path.AbsolutePath,
                        path.LastSeen,
                        "Directory no longer exists"));

                    _logger.LogInformation(
                        "Detected orphaned path: {PathHash} -> {AbsolutePath}",
                        path.PathHash,
                        path.AbsolutePath);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                _logger.LogWarning(ex,
                    "Unable to verify path existence: {AbsolutePath}",
                    path.AbsolutePath);
            }
        }

        _logger.LogInformation(
            "Path scan complete. Found {OrphanCount} orphaned paths out of {TotalCount}",
            orphanedPaths.Count,
            allPaths.Count);

        return orphanedPaths;
    }
}
```

### OrphanedBranchDetector Implementation

```csharp
public interface IOrphanedBranchDetector
{
    Task<IReadOnlyList<OrphanedBranch>> DetectOrphanedBranchesAsync(
        CancellationToken cancellationToken = default);
}

public record OrphanedBranch(
    string ProjectName,
    string BranchName,
    DateTime? LastSeen,
    string? DetectionReason);

public class OrphanedBranchDetector : IOrphanedBranchDetector
{
    private readonly IOrphanRepository _repository;
    private readonly IGitRemoteService _gitRemoteService;
    private readonly ILogger<OrphanedBranchDetector> _logger;

    public async Task<IReadOnlyList<OrphanedBranch>> DetectOrphanedBranchesAsync(
        CancellationToken cancellationToken = default)
    {
        var orphanedBranches = new List<OrphanedBranch>();
        var projectNames = await _repository.GetDistinctProjectNamesAsync(cancellationToken);

        _logger.LogInformation("Scanning branches for {Count} projects", projectNames.Count);

        foreach (var projectName in projectNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var repoPath = await _repository.GetRepoPathForProjectAsync(
                projectName, cancellationToken);

            if (repoPath is null || !Directory.Exists(repoPath.AbsolutePath))
            {
                _logger.LogWarning(
                    "Skipping branch detection for project {ProjectName}: repo path not found or invalid",
                    projectName);
                continue;
            }

            try
            {
                var remoteBranches = await _gitRemoteService.GetRemoteBranchesAsync(
                    repoPath.AbsolutePath, cancellationToken);

                var storedBranches = await _repository.GetBranchesByProjectAsync(
                    projectName, cancellationToken);

                var remoteBranchSet = remoteBranches.ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var branch in storedBranches)
                {
                    if (!remoteBranchSet.Contains(branch.BranchName))
                    {
                        orphanedBranches.Add(new OrphanedBranch(
                            projectName,
                            branch.BranchName,
                            branch.LastSeen,
                            "Branch no longer exists on remote"));

                        _logger.LogInformation(
                            "Detected orphaned branch: {ProjectName}/{BranchName}",
                            projectName,
                            branch.BranchName);
                    }
                }
            }
            catch (GitCommandException ex)
            {
                _logger.LogWarning(ex,
                    "Failed to fetch remote branches for project {ProjectName}",
                    projectName);
            }
        }

        _logger.LogInformation(
            "Branch scan complete. Found {OrphanCount} orphaned branches",
            orphanedBranches.Count);

        return orphanedBranches;
    }
}
```

### Git Remote Service Implementation

```csharp
public interface IGitRemoteService
{
    Task<IReadOnlyList<string>> GetRemoteBranchesAsync(
        string repoPath,
        CancellationToken cancellationToken = default);
}

public class GitRemoteService : IGitRemoteService
{
    private readonly ILogger<GitRemoteService> _logger;

    public async Task<IReadOnlyList<string>> GetRemoteBranchesAsync(
        string repoPath,
        CancellationToken cancellationToken = default)
    {
        // Execute: git ls-remote --heads origin
        // Output format: <sha>\trefs/heads/<branch-name>

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "ls-remote --heads origin",
            WorkingDirectory = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new GitCommandException(
                $"git ls-remote failed with exit code {process.ExitCode}: {errorBuilder}");
        }

        // Parse output: extract branch names from refs/heads/<name>
        var branches = new List<string>();
        var lines = outputBuilder.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Format: <sha>\trefs/heads/<branch-name>
            var parts = line.Split('\t');
            if (parts.Length == 2 && parts[1].StartsWith("refs/heads/"))
            {
                var branchName = parts[1]["refs/heads/".Length..];
                branches.Add(branchName);
            }
        }

        _logger.LogDebug(
            "Found {Count} remote branches in {RepoPath}",
            branches.Count,
            repoPath);

        return branches;
    }
}

public class GitCommandException : Exception
{
    public GitCommandException(string message) : base(message) { }
    public GitCommandException(string message, Exception inner) : base(message, inner) { }
}
```

### Database Repository Implementation

```csharp
public interface IOrphanRepository
{
    Task<IReadOnlyList<RepoPathRecord>> GetAllRepoPathsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetDistinctProjectNamesAsync(
        CancellationToken cancellationToken = default);

    Task<RepoPathRecord?> GetRepoPathForProjectAsync(
        string projectName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BranchRecord>> GetBranchesByProjectAsync(
        string projectName,
        CancellationToken cancellationToken = default);
}

public record RepoPathRecord(
    string PathHash,
    string AbsolutePath,
    DateTime? LastSeen);

public record BranchRecord(
    string ProjectName,
    string BranchName,
    DateTime? LastSeen);

public class OrphanRepository : IOrphanRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public async Task<IReadOnlyList<RepoPathRecord>> GetAllRepoPathsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT path_hash, absolute_path, last_seen
            FROM tenant_management.repo_paths
            ORDER BY absolute_path
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<RepoPathRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new RepoPathRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetDateTime(2)));
        }

        return results;
    }

    public async Task<IReadOnlyList<string>> GetDistinctProjectNamesAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT DISTINCT project_name
            FROM tenant_management.branches
            ORDER BY project_name
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    public async Task<RepoPathRecord?> GetRepoPathForProjectAsync(
        string projectName,
        CancellationToken cancellationToken = default)
    {
        // Join branches to repo_paths to find the path for a project
        const string sql = """
            SELECT rp.path_hash, rp.absolute_path, rp.last_seen
            FROM tenant_management.repo_paths rp
            INNER JOIN tenant_management.branches b ON rp.path_hash = b.path_hash
            WHERE b.project_name = @projectName
            LIMIT 1
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("projectName", projectName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (await reader.ReadAsync(cancellationToken))
        {
            return new RepoPathRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetDateTime(2));
        }

        return null;
    }

    public async Task<IReadOnlyList<BranchRecord>> GetBranchesByProjectAsync(
        string projectName,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT project_name, branch_name, last_seen
            FROM tenant_management.branches
            WHERE project_name = @projectName
            ORDER BY branch_name
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("projectName", projectName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var results = new List<BranchRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new BranchRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetDateTime(2)));
        }

        return results;
    }
}
```

### Logging Patterns

```csharp
// Structured logging with consistent properties
_logger.LogInformation(
    "Orphan detection started. CorrelationId: {CorrelationId}",
    correlationId);

_logger.LogInformation(
    "Detected orphaned path. PathHash: {PathHash}, AbsolutePath: {AbsolutePath}, " +
    "LastSeen: {LastSeen}, Reason: {Reason}, DryRun: {DryRun}",
    orphan.PathHash,
    orphan.AbsolutePath,
    orphan.LastSeen,
    orphan.DetectionReason,
    isDryRun);

_logger.LogInformation(
    "Detected orphaned branch. ProjectName: {ProjectName}, BranchName: {BranchName}, " +
    "LastSeen: {LastSeen}, Reason: {Reason}, DryRun: {DryRun}",
    orphan.ProjectName,
    orphan.BranchName,
    orphan.LastSeen,
    orphan.DetectionReason,
    isDryRun);

_logger.LogInformation(
    "Orphan detection complete. PathsScanned: {PathsScanned}, OrphanedPaths: {OrphanedPaths}, " +
    "ProjectsScanned: {ProjectsScanned}, OrphanedBranches: {OrphanedBranches}, " +
    "Duration: {Duration}ms, CorrelationId: {CorrelationId}",
    pathsScanned,
    orphanedPaths,
    projectsScanned,
    orphanedBranches,
    duration.TotalMilliseconds,
    correlationId);
```

---

## Files to Create

| File | Purpose |
|------|---------|
| `src/CompoundDocs.Cleanup/Detection/IOrphanedPathDetector.cs` | Interface for path orphan detection |
| `src/CompoundDocs.Cleanup/Detection/OrphanedPathDetector.cs` | Path orphan detection implementation |
| `src/CompoundDocs.Cleanup/Detection/IOrphanedBranchDetector.cs` | Interface for branch orphan detection |
| `src/CompoundDocs.Cleanup/Detection/OrphanedBranchDetector.cs` | Branch orphan detection implementation |
| `src/CompoundDocs.Cleanup/Detection/OrphanedPath.cs` | Record type for detected orphaned paths |
| `src/CompoundDocs.Cleanup/Detection/OrphanedBranch.cs` | Record type for detected orphaned branches |
| `src/CompoundDocs.Cleanup/Git/IGitRemoteService.cs` | Interface for git remote operations |
| `src/CompoundDocs.Cleanup/Git/GitRemoteService.cs` | Git ls-remote implementation |
| `src/CompoundDocs.Cleanup/Git/GitCommandException.cs` | Exception for git command failures |
| `src/CompoundDocs.Cleanup/Data/IOrphanRepository.cs` | Interface for orphan-related DB queries |
| `src/CompoundDocs.Cleanup/Data/OrphanRepository.cs` | Database query implementation |
| `src/CompoundDocs.Cleanup/Data/RepoPathRecord.cs` | Record type for repo path data |
| `src/CompoundDocs.Cleanup/Data/BranchRecord.cs` | Record type for branch data |

---

## Dependencies

### Depends On

- **Phase 011**: Cleanup Worker Scaffold (CleanupWorker class, Program.cs, CleanupOptions)
- **Phase 007/008**: Database schema with `tenant_management.repo_paths` and `tenant_management.branches` tables

### Blocks

- **Phase 013**: Cleanup App Orphan Deletion Logic (requires detection to have orphans to delete)

---

## Verification Steps

After completing this phase, verify:

1. **Path Detection**:
   - Create a test repo path record pointing to a non-existent directory
   - Run detection and verify it is identified as orphaned
   - Verify logging output includes path details

2. **Branch Detection**:
   - Create a test branch record for a branch that doesn't exist on remote
   - Run detection and verify it is identified as orphaned
   - Verify logging output includes branch details

3. **Git Remote Integration**:
   - Verify `git ls-remote --heads origin` parsing works correctly
   - Test with repos having multiple branches
   - Test error handling when git command fails

4. **Database Queries**:
   - Verify all repository methods return expected data
   - Test with empty tables (no records)
   - Test with multiple projects/paths

5. **Dry Run Mode**:
   - Verify dry-run flag is respected in logging
   - No actual deletions occur (deletion is Phase 013)

---

## Notes

- Detection is separated from deletion to allow review before cleanup
- The `IGitRemoteService` abstraction allows for testing without actual git repos
- Consider caching remote branch results if the same repo has many projects
- Network timeouts for `git ls-remote` should be configurable
- The detector classes are designed to be stateless and injectable
