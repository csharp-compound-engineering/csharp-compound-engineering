# Phase 039: Git Branch Detection Service

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: MCP Server Core
> **Prerequisites**: Phase 035 (Process Execution Infrastructure)

---

## Spec References

This phase implements the Git detection infrastructure defined in:

- **spec/skills/meta-skills.md** - Git root and branch detection in `/cdocs:activate` skill
- **research/git-current-branch-detection.md** - Comprehensive Git branch detection methods guide

---

## Objectives

1. Implement Git repository root detection via `git rev-parse --show-toplevel`
2. Implement current branch name extraction with proper edge case handling
3. Support Git worktree environments
4. Handle detached HEAD state gracefully
5. Provide error handling for non-Git directories
6. Create a clean service interface for Git operations

---

## Acceptance Criteria

### Core Service Interface

- [ ] `IGitDetectionService` interface defined with:
  - [ ] `Task<string?> GetRepositoryRootAsync(string workingDirectory, CancellationToken cancellationToken)`
  - [ ] `Task<string?> GetCurrentBranchAsync(string workingDirectory, CancellationToken cancellationToken)`
  - [ ] `Task<bool> IsGitRepositoryAsync(string directory, CancellationToken cancellationToken)`
  - [ ] `Task<GitRepositoryInfo> GetRepositoryInfoAsync(string workingDirectory, CancellationToken cancellationToken)`

### Repository Root Detection

- [ ] Detect repository root using `git rev-parse --show-toplevel`
- [ ] Handle nested directories (returns root regardless of current depth)
- [ ] Return `null` for non-Git directories (no exception)
- [ ] Support paths with spaces and special characters

### Branch Name Extraction

- [ ] Primary method: `git branch --show-current` (Git 2.22+)
- [ ] Fallback method: `git symbolic-ref -q --short HEAD` for older Git versions
- [ ] Return `null` (not throw) for detached HEAD state
- [ ] Handle new repositories with no commits

### Worktree Support

- [ ] Detect worktree environment via `git rev-parse --git-common-dir`
- [ ] Return correct branch for the specific worktree
- [ ] Handle linked worktrees correctly

### Detached HEAD Handling

- [ ] Detect detached HEAD state reliably
- [ ] Return `null` for branch name when detached
- [ ] Optionally provide commit hash in `GitRepositoryInfo`
- [ ] Detect during-rebase state via `.git/rebase-merge` directory presence

### Error Handling

- [ ] Non-Git directory returns appropriate null/empty values (no exceptions)
- [ ] Git not installed throws `GitNotFoundException`
- [ ] Git command timeout handled with configurable duration
- [ ] Process execution errors logged with details

---

## Implementation Notes

### GitRepositoryInfo Model

Create `Models/GitRepositoryInfo.cs`:

```csharp
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Contains information about a Git repository's current state.
/// </summary>
public sealed record GitRepositoryInfo
{
    /// <summary>
    /// The absolute path to the repository root directory.
    /// Null if not a Git repository.
    /// </summary>
    public string? RepositoryRoot { get; init; }

    /// <summary>
    /// The current branch name.
    /// Null if in detached HEAD state or not a Git repository.
    /// </summary>
    public string? CurrentBranch { get; init; }

    /// <summary>
    /// True if the working directory is within a Git repository.
    /// </summary>
    public bool IsGitRepository => RepositoryRoot is not null;

    /// <summary>
    /// True if HEAD is detached (not pointing to a branch).
    /// </summary>
    public bool IsDetachedHead { get; init; }

    /// <summary>
    /// The current commit hash (short form) when in detached HEAD state.
    /// Null if on a branch or not a Git repository.
    /// </summary>
    public string? DetachedCommit { get; init; }

    /// <summary>
    /// True if this is a Git worktree (not the main repository).
    /// </summary>
    public bool IsWorktree { get; init; }

    /// <summary>
    /// True if a rebase operation is in progress.
    /// </summary>
    public bool IsRebaseInProgress { get; init; }

    /// <summary>
    /// True if a merge operation is in progress.
    /// </summary>
    public bool IsMergeInProgress { get; init; }

    /// <summary>
    /// Creates a result indicating the directory is not a Git repository.
    /// </summary>
    public static GitRepositoryInfo NotARepository => new()
    {
        RepositoryRoot = null,
        CurrentBranch = null,
        IsDetachedHead = false,
        DetachedCommit = null,
        IsWorktree = false,
        IsRebaseInProgress = false,
        IsMergeInProgress = false
    };
}
```

### Service Interface

Create `Services/IGitDetectionService.cs`:

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Service for detecting Git repository information.
/// </summary>
public interface IGitDetectionService
{
    /// <summary>
    /// Gets the root directory of the Git repository containing the specified path.
    /// </summary>
    /// <param name="workingDirectory">A path within the repository.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The repository root path, or null if not a Git repository.</returns>
    Task<string?> GetRepositoryRootAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current branch name for the repository containing the specified path.
    /// </summary>
    /// <param name="workingDirectory">A path within the repository.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The branch name, or null if detached HEAD or not a Git repository.</returns>
    Task<string?> GetCurrentBranchAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the specified directory is within a Git repository.
    /// </summary>
    /// <param name="directory">The directory to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if within a Git repository.</returns>
    Task<bool> IsGitRepositoryAsync(
        string directory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets comprehensive information about the Git repository.
    /// </summary>
    /// <param name="workingDirectory">A path within the repository.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Repository information, or NotARepository if not a Git repository.</returns>
    Task<GitRepositoryInfo> GetRepositoryInfoAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default);
}
```

### Service Implementation

Create `Services/GitDetectionService.cs`:

```csharp
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Implements Git repository detection using Git CLI commands.
/// </summary>
public sealed class GitDetectionService : IGitDetectionService
{
    private readonly ILogger<GitDetectionService> _logger;
    private readonly GitDetectionOptions _options;

    public GitDetectionService(
        ILogger<GitDetectionService> logger,
        IOptions<GitDetectionOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<string?> GetRepositoryRootAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteGitCommandAsync(
            workingDirectory,
            "rev-parse --show-toplevel",
            cancellationToken);

        return result.Success ? result.Output?.Trim() : null;
    }

    public async Task<string?> GetCurrentBranchAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        // Try modern approach first (Git 2.22+)
        var result = await ExecuteGitCommandAsync(
            workingDirectory,
            "branch --show-current",
            cancellationToken);

        if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
        {
            return result.Output.Trim();
        }

        // Fallback for older Git versions or empty result (detached HEAD returns empty)
        // Use symbolic-ref which fails on detached HEAD
        result = await ExecuteGitCommandAsync(
            workingDirectory,
            "symbolic-ref -q --short HEAD",
            cancellationToken);

        if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
        {
            return result.Output.Trim();
        }

        // Detached HEAD or not a repository
        return null;
    }

    public async Task<bool> IsGitRepositoryAsync(
        string directory,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteGitCommandAsync(
            directory,
            "rev-parse --git-dir",
            cancellationToken);

        return result.Success;
    }

    public async Task<GitRepositoryInfo> GetRepositoryInfoAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        // Check if it's a Git repository first
        var repoRoot = await GetRepositoryRootAsync(workingDirectory, cancellationToken);
        if (repoRoot is null)
        {
            return GitRepositoryInfo.NotARepository;
        }

        // Get branch name
        var branchName = await GetCurrentBranchAsync(workingDirectory, cancellationToken);

        // Determine if detached HEAD
        var isDetached = branchName is null;
        string? detachedCommit = null;

        if (isDetached)
        {
            var commitResult = await ExecuteGitCommandAsync(
                workingDirectory,
                "rev-parse --short HEAD",
                cancellationToken);

            if (commitResult.Success)
            {
                detachedCommit = commitResult.Output?.Trim();
            }
        }

        // Check if worktree
        var isWorktree = await IsWorktreeAsync(workingDirectory, cancellationToken);

        // Check for in-progress operations
        var gitDir = await GetGitDirAsync(workingDirectory, cancellationToken);
        var isRebaseInProgress = gitDir is not null &&
            (Directory.Exists(Path.Combine(gitDir, "rebase-merge")) ||
             Directory.Exists(Path.Combine(gitDir, "rebase-apply")));
        var isMergeInProgress = gitDir is not null &&
            File.Exists(Path.Combine(gitDir, "MERGE_HEAD"));

        return new GitRepositoryInfo
        {
            RepositoryRoot = repoRoot,
            CurrentBranch = branchName,
            IsDetachedHead = isDetached,
            DetachedCommit = detachedCommit,
            IsWorktree = isWorktree,
            IsRebaseInProgress = isRebaseInProgress,
            IsMergeInProgress = isMergeInProgress
        };
    }

    private async Task<bool> IsWorktreeAsync(
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        // Compare git-dir and git-common-dir to detect worktrees
        var gitDirResult = await ExecuteGitCommandAsync(
            workingDirectory,
            "rev-parse --git-dir",
            cancellationToken);

        var commonDirResult = await ExecuteGitCommandAsync(
            workingDirectory,
            "rev-parse --git-common-dir",
            cancellationToken);

        if (!gitDirResult.Success || !commonDirResult.Success)
        {
            return false;
        }

        var gitDir = Path.GetFullPath(
            Path.Combine(workingDirectory, gitDirResult.Output?.Trim() ?? ""));
        var commonDir = Path.GetFullPath(
            Path.Combine(workingDirectory, commonDirResult.Output?.Trim() ?? ""));

        // If they differ, this is a worktree
        return !string.Equals(gitDir, commonDir, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string?> GetGitDirAsync(
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteGitCommandAsync(
            workingDirectory,
            "rev-parse --git-dir",
            cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Output))
        {
            return null;
        }

        var gitDir = result.Output.Trim();
        if (!Path.IsPathRooted(gitDir))
        {
            gitDir = Path.GetFullPath(Path.Combine(workingDirectory, gitDir));
        }

        return gitDir;
    }

    private async Task<GitCommandResult> ExecuteGitCommandAsync(
        string workingDirectory,
        string arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _options.GitExecutable,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            _logger.LogDebug(
                "Executing git command: {Command} in {Directory}",
                arguments,
                workingDirectory);

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.CommandTimeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Git command timed out after {Timeout}ms: {Command}",
                    _options.CommandTimeout.TotalMilliseconds,
                    arguments);

                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Best effort cleanup
                }

                return new GitCommandResult(false, null, "Command timed out");
            }

            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                _logger.LogDebug(
                    "Git command returned non-zero exit code {ExitCode}: {Error}",
                    process.ExitCode,
                    error);

                return new GitCommandResult(false, null, error);
            }

            return new GitCommandResult(true, output, null);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            // File not found - Git is not installed
            _logger.LogError("Git executable not found at: {Path}", _options.GitExecutable);
            throw new GitNotFoundException(
                $"Git executable not found. Ensure Git is installed and '{_options.GitExecutable}' is accessible.",
                ex);
        }
        catch (Exception ex) when (ex is not GitNotFoundException)
        {
            _logger.LogError(
                ex,
                "Error executing git command: {Command}",
                arguments);

            return new GitCommandResult(false, null, ex.Message);
        }
    }

    private sealed record GitCommandResult(bool Success, string? Output, string? Error);
}
```

### Options Class

Create `Options/GitDetectionOptions.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace CompoundDocs.McpServer.Options;

/// <summary>
/// Configuration options for Git detection service.
/// </summary>
public sealed class GitDetectionOptions
{
    public const string SectionName = "GitDetection";

    /// <summary>
    /// Path to the Git executable. Defaults to "git" (uses PATH).
    /// </summary>
    public string GitExecutable { get; set; } = "git";

    /// <summary>
    /// Maximum time to wait for a Git command to complete.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
```

### Custom Exception

Create `Exceptions/GitNotFoundException.cs`:

```csharp
namespace CompoundDocs.McpServer.Exceptions;

/// <summary>
/// Exception thrown when Git is not installed or not found in the system PATH.
/// </summary>
public sealed class GitNotFoundException : Exception
{
    public GitNotFoundException(string message) : base(message)
    {
    }

    public GitNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

### Service Registration

Add to `Extensions/ServiceCollectionExtensions.cs`:

```csharp
/// <summary>
/// Registers Git detection services.
/// </summary>
public static IServiceCollection AddGitDetection(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddOptionsWithValidateOnStart<GitDetectionOptions>()
        .Bind(configuration.GetSection(GitDetectionOptions.SectionName))
        .ValidateDataAnnotations();

    services.AddSingleton<IGitDetectionService, GitDetectionService>();

    return services;
}
```

### Git Command Reference

| Operation | Command | Fallback | Notes |
|-----------|---------|----------|-------|
| Repository root | `git rev-parse --show-toplevel` | None | Returns absolute path |
| Current branch | `git branch --show-current` | `git symbolic-ref -q --short HEAD` | Empty on detached HEAD |
| Is repository | `git rev-parse --git-dir` | None | Returns `.git` or path |
| Is worktree | Compare `--git-dir` vs `--git-common-dir` | None | Different = worktree |
| Detached commit | `git rev-parse --short HEAD` | None | 7-char commit hash |
| Git directory | `git rev-parse --git-dir` | None | May be relative path |

### Edge Case Handling Matrix

| Scenario | Repository Root | Branch Name | Notes |
|----------|-----------------|-------------|-------|
| Normal checkout | `/path/to/repo` | `main` | Happy path |
| Detached HEAD | `/path/to/repo` | `null` | IsDetachedHead = true |
| New repo (no commits) | `/path/to/repo` | `main` | Branch exists but no commit |
| Worktree | `/path/to/worktree` | `feature-x` | IsWorktree = true |
| During rebase | `/path/to/repo` | `null` | IsRebaseInProgress = true |
| During merge | `/path/to/repo` | `main` | IsMergeInProgress = true |
| Not a repository | `null` | `null` | All flags false |
| Nested directory | `/path/to/repo` | `main` | Returns root regardless of depth |
| Bare repository | `/path/to/repo.git` | `null` | Special handling needed |

---

## Dependencies

### Depends On
- Phase 035: Process Execution Infrastructure (process spawning utilities)

### Blocks
- Phase 040+: Skills and MCP tools requiring repository context
- Activation flow (`/cdocs:activate`)
- Branch-aware document storage

---

## Verification Steps

After completing this phase, verify:

1. **Repository root detection**: Correctly finds root from nested directories
2. **Branch detection**: Returns correct branch name
3. **Detached HEAD**: Returns null for branch, correct commit hash
4. **Non-repository**: Returns appropriate null values without exceptions
5. **Worktree support**: Correctly identifies worktree environments

### Manual Verification

```bash
# Create test repository
mkdir -p /tmp/git-test && cd /tmp/git-test
git init
echo "test" > file.txt
git add . && git commit -m "Initial"

# Test from nested directory
mkdir -p deep/nested/path
cd deep/nested/path

# Run the service and verify:
# - GetRepositoryRootAsync returns "/tmp/git-test"
# - GetCurrentBranchAsync returns "main" (or "master")

# Test detached HEAD
cd /tmp/git-test
git checkout HEAD~0 --detach

# Verify:
# - GetCurrentBranchAsync returns null
# - GitRepositoryInfo.IsDetachedHead is true
# - GitRepositoryInfo.DetachedCommit has value

# Test non-repository
cd /tmp
# Verify all methods return null/false appropriately

# Cleanup
rm -rf /tmp/git-test
```

### Unit Tests

```csharp
[Fact]
public async Task GetRepositoryRootAsync_ReturnsRoot_WhenInRepository()
{
    // Arrange
    var service = CreateService();
    var repoPath = CreateTempGitRepository();

    try
    {
        // Act
        var result = await service.GetRepositoryRootAsync(repoPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(repoPath, result);
    }
    finally
    {
        Directory.Delete(repoPath, recursive: true);
    }
}

[Fact]
public async Task GetRepositoryRootAsync_ReturnsNull_WhenNotInRepository()
{
    // Arrange
    var service = CreateService();
    var tempPath = Path.GetTempPath();

    // Act
    var result = await service.GetRepositoryRootAsync(tempPath);

    // Assert
    Assert.Null(result);
}

[Fact]
public async Task GetCurrentBranchAsync_ReturnsBranchName_WhenOnBranch()
{
    // Arrange
    var service = CreateService();
    var repoPath = CreateTempGitRepository();

    try
    {
        // Act
        var result = await service.GetCurrentBranchAsync(repoPath);

        // Assert
        Assert.NotNull(result);
        Assert.True(result == "main" || result == "master");
    }
    finally
    {
        Directory.Delete(repoPath, recursive: true);
    }
}

[Fact]
public async Task GetCurrentBranchAsync_ReturnsNull_WhenDetachedHead()
{
    // Arrange
    var service = CreateService();
    var repoPath = CreateTempGitRepository();
    DetachHead(repoPath);

    try
    {
        // Act
        var result = await service.GetCurrentBranchAsync(repoPath);

        // Assert
        Assert.Null(result);
    }
    finally
    {
        Directory.Delete(repoPath, recursive: true);
    }
}

[Fact]
public async Task GetRepositoryInfoAsync_DetectsRebaseInProgress()
{
    // Arrange
    var service = CreateService();
    var repoPath = CreateTempGitRepository();
    SimulateRebaseInProgress(repoPath);

    try
    {
        // Act
        var result = await service.GetRepositoryInfoAsync(repoPath);

        // Assert
        Assert.True(result.IsGitRepository);
        Assert.True(result.IsRebaseInProgress);
    }
    finally
    {
        Directory.Delete(repoPath, recursive: true);
    }
}

private static string CreateTempGitRepository()
{
    var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    Directory.CreateDirectory(path);

    ExecuteGit(path, "init");
    ExecuteGit(path, "config user.email test@test.com");
    ExecuteGit(path, "config user.name Test");
    File.WriteAllText(Path.Combine(path, "file.txt"), "content");
    ExecuteGit(path, "add .");
    ExecuteGit(path, "commit -m Initial");

    return path;
}

private static void DetachHead(string repoPath)
{
    ExecuteGit(repoPath, "checkout HEAD~0 --detach");
}

private static void SimulateRebaseInProgress(string repoPath)
{
    var gitDir = Path.Combine(repoPath, ".git");
    Directory.CreateDirectory(Path.Combine(gitDir, "rebase-merge"));
}

private static void ExecuteGit(string workingDir, string arguments)
{
    var psi = new ProcessStartInfo("git", arguments)
    {
        WorkingDirectory = workingDir,
        UseShellExecute = false,
        CreateNoWindow = true
    };
    using var process = Process.Start(psi);
    process?.WaitForExit();
}
```

---

## Configuration Files

### appsettings.json Addition

```json
{
  "GitDetection": {
    "GitExecutable": "git",
    "CommandTimeout": "00:00:10"
  }
}
```

---

## Notes

- The service uses `git` CLI rather than LibGit2Sharp to avoid native dependencies and ensure consistent behavior with user's Git installation
- Commands are run with `UseShellExecute = false` to capture output and avoid shell injection
- The fallback from `git branch --show-current` to `git symbolic-ref` ensures compatibility with Git versions prior to 2.22 (June 2019)
- Worktree detection compares `--git-dir` and `--git-common-dir` - if they differ, it's a worktree
- The service is registered as singleton since it holds no state and Git operations are thread-safe
- Timeout defaults to 10 seconds which is generous for local Git operations but prevents hangs
- Path normalization uses `Path.GetFullPath` to handle relative paths from Git output
