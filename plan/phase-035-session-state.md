# Phase 035: MCP Session State Management

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: MCP Server Core
> **Prerequisites**: Phase 023 (MCP Server Core)

---

## Spec References

This phase implements the session state management infrastructure defined in:

- **spec/mcp-server.md** - Server state management, single project constraint
- **spec/mcp-server/tools.md** - `activate_project` tool specification (Tool #9)
- **spec/mcp-server/database-schema.md** - TenantContext and tenant management tables
- **research/ioptions-monitor-dynamic-paths.md** - Dynamic configuration loading patterns
- **research/git-current-branch-detection.md** - Git branch detection methods

---

## Objectives

1. Implement active project tracking per MCP server session
2. Create git branch detection service using LibGit2Sharp
3. Build TenantContext management for project_name, branch_name, and path_hash
4. Integrate switchable configuration provider for runtime project activation
5. Implement session state persistence and cleanup mechanisms
6. Ensure thread-safe state access for concurrent tool calls

---

## Acceptance Criteria

### Session State Service

- [ ] `ISessionStateService` interface with session lifecycle methods
- [ ] `SessionStateService` implementation as singleton
- [ ] Properties for:
  - [ ] `ActiveProject` - currently activated project context (nullable)
  - [ ] `IsProjectActive` - boolean indicating if a project is activated
  - [ ] `CurrentTenantContext` - the active TenantContext record
- [ ] Thread-safe state access using `ReaderWriterLockSlim`

### TenantContext Management

- [ ] `TenantContext` record type with `ProjectName`, `BranchName`, `PathHash`
- [ ] `ITenantContextFactory` interface for creating tenant contexts
- [ ] `TenantContextFactory` implementation with:
  - [ ] Path hash computation (SHA256, first 16 hex chars)
  - [ ] Project name extraction from config
  - [ ] Branch name from git detection
- [ ] Registration in tenant_management database tables on activation

### Git Branch Detection Service

- [ ] `IGitBranchService` interface for branch operations
- [ ] `GitBranchService` implementation using LibGit2Sharp:
  - [ ] `GetCurrentBranchAsync(string repoPath)` - returns branch name
  - [ ] `IsDetachedHeadAsync(string repoPath)` - returns boolean
  - [ ] `IsGitRepositoryAsync(string path)` - validates git repo
- [ ] Fallback to `git branch --show-current` if LibGit2Sharp fails
- [ ] Handle detached HEAD state gracefully (use commit hash prefix)

### Switchable Configuration Integration

- [ ] `SwitchableJsonConfigurationProvider` from research implemented
- [ ] `ISwitchableConfigurationProvider` interface registered in DI
- [ ] `ProjectOptions` bound to switchable provider section
- [ ] `IOptionsMonitorCache<ProjectOptions>` cleared on project switch

### Project Activation Flow

- [ ] `IProjectActivationService` interface
- [ ] `ProjectActivationService` implementation with:
  - [ ] `ActivateProjectAsync(string configPath, string branchName)` method
  - [ ] `DeactivateProjectAsync()` method
  - [ ] `GetActiveProjectAsync()` method
- [ ] Activation workflow:
  1. Validate config file exists
  2. Compute repo_root from config path
  3. Generate path_hash from repo_root
  4. Extract project_name from config
  5. Store tenant context in session state
  6. Register in tenant_management tables
  7. Update switchable configuration provider
  8. Clear options cache
  9. Return activation result

### Session Cleanup

- [ ] `ISessionCleanupService` interface
- [ ] `SessionCleanupService` implementation
- [ ] Cleanup operations:
  - [ ] Stop active file watcher
  - [ ] Clear session state
  - [ ] Dispose switchable config provider watcher
  - [ ] Update last_seen timestamps in tenant tables
- [ ] Cleanup triggered on:
  - [ ] Explicit deactivation
  - [ ] New project activation (deactivates previous)
  - [ ] Server shutdown (graceful)

### State Persistence

- [ ] Update `tenant_management.repo_paths` on activation
- [ ] Update `tenant_management.branches` on activation
- [ ] Update `last_seen` timestamps
- [ ] Handle first-time vs returning project activation

---

## Implementation Notes

### Session State Service

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Manages active project state for the MCP server session.
/// Thread-safe for concurrent tool invocations.
/// </summary>
public interface ISessionStateService
{
    /// <summary>
    /// Gets the currently active tenant context, or null if no project is active.
    /// </summary>
    TenantContext? ActiveTenantContext { get; }

    /// <summary>
    /// Gets whether a project is currently active.
    /// </summary>
    bool IsProjectActive { get; }

    /// <summary>
    /// Gets the active project configuration, or null if no project is active.
    /// </summary>
    ActiveProjectInfo? ActiveProject { get; }

    /// <summary>
    /// Sets the active project state. Thread-safe.
    /// </summary>
    void SetActiveProject(ActiveProjectInfo project);

    /// <summary>
    /// Clears the active project state. Thread-safe.
    /// </summary>
    void ClearActiveProject();
}

/// <summary>
/// Represents information about the currently active project.
/// </summary>
public record ActiveProjectInfo(
    string ConfigPath,
    string RepoRoot,
    TenantContext TenantContext,
    DateTimeOffset ActivatedAt
);
```

### Session State Implementation

```csharp
public class SessionStateService : ISessionStateService
{
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private ActiveProjectInfo? _activeProject;

    public TenantContext? ActiveTenantContext
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _activeProject?.TenantContext;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    public bool IsProjectActive
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _activeProject != null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    public ActiveProjectInfo? ActiveProject
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _activeProject;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    public void SetActiveProject(ActiveProjectInfo project)
    {
        _lock.EnterWriteLock();
        try
        {
            _activeProject = project;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void ClearActiveProject()
    {
        _lock.EnterWriteLock();
        try
        {
            _activeProject = null;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}
```

### TenantContext Record

```csharp
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Represents the tenant isolation context for a project session.
/// Used as filter criteria for all database queries.
/// </summary>
public record TenantContext(
    string ProjectName,
    string BranchName,
    string PathHash
)
{
    /// <summary>
    /// Returns a string representation for logging.
    /// </summary>
    public override string ToString() =>
        $"[{ProjectName}:{BranchName}:{PathHash[..8]}]";
}
```

### Path Hash Computation

```csharp
public class TenantContextFactory : ITenantContextFactory
{
    public TenantContext Create(
        string projectName,
        string branchName,
        string repoRootPath)
    {
        var pathHash = ComputePathHash(repoRootPath);
        return new TenantContext(projectName, branchName, pathHash);
    }

    /// <summary>
    /// Computes a stable hash for the repository path.
    /// Normalized for cross-platform consistency.
    /// </summary>
    public static string ComputePathHash(string absolutePath)
    {
        // Normalize path separators and remove trailing slash
        var normalizedPath = absolutePath
            .Replace('\\', '/')
            .TrimEnd('/');

        // Compute SHA256
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath));

        // Return first 16 characters (64 bits) for brevity
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }
}
```

### Git Branch Detection Service

```csharp
using LibGit2Sharp;

public class GitBranchService : IGitBranchService
{
    private readonly ILogger<GitBranchService> _logger;

    public GitBranchService(ILogger<GitBranchService> logger)
    {
        _logger = logger;
    }

    public Task<string?> GetCurrentBranchAsync(string repoPath)
    {
        return Task.Run(() =>
        {
            try
            {
                using var repo = new Repository(repoPath);

                if (repo.Head.IsCurrentRepositoryHead && !repo.Info.IsHeadDetached)
                {
                    return repo.Head.FriendlyName;
                }

                // Detached HEAD - return short commit hash
                _logger.LogWarning(
                    "Repository at {Path} is in detached HEAD state",
                    repoPath);
                return $"detached-{repo.Head.Tip.Sha[..8]}";
            }
            catch (RepositoryNotFoundException)
            {
                _logger.LogWarning(
                    "Path {Path} is not a git repository",
                    repoPath);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to detect git branch at {Path}, falling back to CLI",
                    repoPath);
                return GetBranchViaCli(repoPath);
            }
        });
    }

    public Task<bool> IsGitRepositoryAsync(string path)
    {
        return Task.Run(() => Repository.IsValid(path));
    }

    public Task<bool> IsDetachedHeadAsync(string repoPath)
    {
        return Task.Run(() =>
        {
            try
            {
                using var repo = new Repository(repoPath);
                return repo.Info.IsHeadDetached;
            }
            catch
            {
                return false;
            }
        });
    }

    private string? GetBranchViaCli(string repoPath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "branch --show-current",
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);

            return string.IsNullOrEmpty(output) ? null : output;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Git CLI fallback also failed");
            return null;
        }
    }
}
```

### Project Activation Service

```csharp
public class ProjectActivationService : IProjectActivationService
{
    private readonly ISessionStateService _sessionState;
    private readonly ISwitchableConfigurationProvider _configProvider;
    private readonly IOptionsMonitorCache<ProjectOptions> _optionsCache;
    private readonly IGitBranchService _gitBranchService;
    private readonly ITenantContextFactory _tenantContextFactory;
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<ProjectActivationService> _logger;

    public async Task<ActivationResult> ActivateProjectAsync(
        string configPath,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Activating project from config: {ConfigPath}",
            configPath);

        // Validate config exists
        if (!File.Exists(configPath))
        {
            return ActivationResult.Failed($"Config file not found: {configPath}");
        }

        // Compute repo root (parent of .csharp-compounding-docs/)
        var configDir = Path.GetDirectoryName(configPath)!;
        var repoRoot = Path.GetDirectoryName(configDir)!;

        // Validate git repository
        if (!await _gitBranchService.IsGitRepositoryAsync(repoRoot))
        {
            _logger.LogWarning(
                "Repo root {RepoRoot} is not a git repository",
                repoRoot);
        }

        // Read project name from config
        var config = await LoadConfigAsync(configPath, cancellationToken);
        var projectName = config.ProjectName
            ?? Path.GetFileName(repoRoot)
            ?? "unknown";

        // Create tenant context
        var tenantContext = _tenantContextFactory.Create(
            projectName,
            branchName,
            repoRoot);

        // Deactivate previous project if any
        if (_sessionState.IsProjectActive)
        {
            await DeactivateProjectAsync(cancellationToken);
        }

        // Register in tenant management tables
        await _tenantRepository.UpsertRepoPathAsync(
            tenantContext.PathHash,
            repoRoot,
            projectName,
            cancellationToken);

        await _tenantRepository.UpsertBranchAsync(
            projectName,
            branchName,
            cancellationToken);

        // Update configuration provider
        _configProvider.SetPath(configPath);
        _optionsCache.Clear();

        // Set session state
        var activeProject = new ActiveProjectInfo(
            configPath,
            repoRoot,
            tenantContext,
            DateTimeOffset.UtcNow);

        _sessionState.SetActiveProject(activeProject);

        _logger.LogInformation(
            "Project activated: {TenantContext}",
            tenantContext);

        return ActivationResult.Success(tenantContext);
    }

    public async Task DeactivateProjectAsync(CancellationToken cancellationToken = default)
    {
        var activeProject = _sessionState.ActiveProject;
        if (activeProject == null)
        {
            _logger.LogDebug("No active project to deactivate");
            return;
        }

        _logger.LogInformation(
            "Deactivating project: {TenantContext}",
            activeProject.TenantContext);

        // Update last_seen timestamps
        await _tenantRepository.UpdateLastSeenAsync(
            activeProject.TenantContext.PathHash,
            cancellationToken);

        // Clear configuration
        _configProvider.ClearPath();
        _optionsCache.Clear();

        // Clear session state
        _sessionState.ClearActiveProject();

        _logger.LogInformation("Project deactivated");
    }
}
```

### DI Registration

```csharp
public static class SessionStateServiceCollectionExtensions
{
    public static IServiceCollection AddSessionState(
        this IServiceCollection services,
        IConfigurationManager configuration)
    {
        // Session state (singleton - one per MCP server process)
        services.AddSingleton<ISessionStateService, SessionStateService>();

        // Tenant context factory
        services.AddSingleton<ITenantContextFactory, TenantContextFactory>();

        // Git branch detection
        services.AddSingleton<IGitBranchService, GitBranchService>();

        // Switchable configuration provider
        var switchableProvider = new SwitchableJsonConfigurationProvider(
            reloadOnChange: true);
        ((IConfigurationBuilder)configuration).Add(
            new SwitchableJsonConfigurationSource(switchableProvider));
        services.AddSingleton<ISwitchableConfigurationProvider>(switchableProvider);
        services.AddSingleton(switchableProvider);

        // Project options bound to switchable provider
        services.Configure<ProjectOptions>(
            configuration.GetSection(ProjectOptions.SectionName));

        // Project activation service
        services.AddSingleton<IProjectActivationService, ProjectActivationService>();

        return services;
    }
}
```

### Activation Result Type

```csharp
public record ActivationResult
{
    public bool IsSuccess { get; init; }
    public TenantContext? TenantContext { get; init; }
    public string? ErrorMessage { get; init; }

    public static ActivationResult Success(TenantContext tenant) =>
        new() { IsSuccess = true, TenantContext = tenant };

    public static ActivationResult Failed(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };
}
```

---

## Dependencies

### NuGet Packages

```xml
<PackageReference Include="LibGit2Sharp" Version="0.31.0" />
<PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="9.0.0" />
```

### Depends On

- Phase 023: MCP Server Core (host infrastructure)
- Phase 017: Dependency Injection Container Setup
- Phase 007: Tenant Management Schema (database tables)

### Blocks

- Phase 036+: MCP tool implementations that require tenant context
- Phase 040+: File watcher integration (needs active project)
- Phase 045+: Document indexing (needs tenant context for isolation)

---

## Verification Steps

After completing this phase, verify:

1. **Session state thread safety**: Concurrent reads/writes don't corrupt state
2. **Branch detection**: LibGit2Sharp correctly detects branch in normal and detached states
3. **Path hash consistency**: Same path always produces same hash across platforms
4. **Config switching**: Activating a new project correctly switches configuration
5. **Cleanup**: Deactivation clears all state properly

### Unit Test Examples

```csharp
[Fact]
public void ComputePathHash_SamePath_ReturnsSameHash()
{
    var hash1 = TenantContextFactory.ComputePathHash("/path/to/repo");
    var hash2 = TenantContextFactory.ComputePathHash("/path/to/repo");

    Assert.Equal(hash1, hash2);
}

[Fact]
public void ComputePathHash_NormalizesPathSeparators()
{
    var unixHash = TenantContextFactory.ComputePathHash("/path/to/repo");
    var windowsHash = TenantContextFactory.ComputePathHash("\\path\\to\\repo");

    Assert.Equal(unixHash, windowsHash);
}

[Fact]
public void SessionState_ConcurrentAccess_IsThreadSafe()
{
    var state = new SessionStateService();
    var tasks = new List<Task>();

    // Concurrent writes
    for (int i = 0; i < 100; i++)
    {
        int index = i;
        tasks.Add(Task.Run(() =>
        {
            var project = new ActiveProjectInfo(
                $"/path/config-{index}.json",
                $"/path/repo-{index}",
                new TenantContext($"project-{index}", "main", "hash"),
                DateTimeOffset.UtcNow);
            state.SetActiveProject(project);
        }));
    }

    // Concurrent reads
    for (int i = 0; i < 100; i++)
    {
        tasks.Add(Task.Run(() =>
        {
            var _ = state.ActiveProject;
            var __ = state.IsProjectActive;
            var ___ = state.ActiveTenantContext;
        }));
    }

    // Should complete without deadlock or exception
    Assert.True(Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(10)));
}

[Fact]
public async Task GitBranchService_DetectsBranch_WhenOnBranch()
{
    // Arrange
    var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    Repository.Init(tempDir);

    try
    {
        var service = new GitBranchService(Mock.Of<ILogger<GitBranchService>>());

        // Act
        var branch = await service.GetCurrentBranchAsync(tempDir);

        // Assert (default branch is usually "main" or "master")
        Assert.NotNull(branch);
        Assert.True(branch == "main" || branch == "master");
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}
```

### Integration Test

```csharp
[Fact]
public async Task ProjectActivation_FullWorkflow_Succeeds()
{
    // Arrange
    var services = new ServiceCollection();
    var config = new ConfigurationManager();

    services.AddSessionState(config);
    services.AddLogging();

    using var provider = services.BuildServiceProvider();

    var activationService = provider.GetRequiredService<IProjectActivationService>();
    var sessionState = provider.GetRequiredService<ISessionStateService>();

    // Create temp project structure
    var tempDir = CreateTempProjectStructure();

    try
    {
        var configPath = Path.Combine(tempDir, ".csharp-compounding-docs", "config.json");

        // Act - Activate
        var result = await activationService.ActivateProjectAsync(configPath, "main");

        // Assert - Activated
        Assert.True(result.IsSuccess);
        Assert.True(sessionState.IsProjectActive);
        Assert.Equal("main", sessionState.ActiveTenantContext!.BranchName);

        // Act - Deactivate
        await activationService.DeactivateProjectAsync();

        // Assert - Deactivated
        Assert.False(sessionState.IsProjectActive);
        Assert.Null(sessionState.ActiveTenantContext);
    }
    finally
    {
        Directory.Delete(tempDir, recursive: true);
    }
}
```

---

## Configuration Files

### appsettings.json Session Defaults

```json
{
  "Session": {
    "MaxInactivityMinutes": 60,
    "EnableAutoDeactivation": false
  }
}
```

---

## Notes

- The MCP server runs as stdio (one per Claude Code instance), so there's only ever one active project at a time
- Session state is in-memory only; if the process restarts, state is lost (by design - Claude Code will re-activate)
- Path hash ensures git worktrees (same project, different paths) are isolated
- Branch name is passed by the caller (Claude Code skill), not auto-detected, because Claude Code knows the branch context
- LibGit2Sharp is used for programmatic git access; CLI fallback handles edge cases where native binaries fail
- Thread safety is critical because MCP tool calls may arrive concurrently from the protocol layer
