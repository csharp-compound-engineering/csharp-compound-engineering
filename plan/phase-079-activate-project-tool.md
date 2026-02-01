# Phase 079: activate_project MCP Tool

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: MCP Tools
> **Prerequisites**: Phase 025 (Tool Registration), Phase 035 (Session State), Phase 039 (Git Detection), Phase 053 (File Watcher Service)

---

## Spec References

This phase implements the `activate_project` MCP tool defined in:

- **spec/mcp-server/tools.md** - [Tool #9: activate_project](../spec/mcp-server/tools.md#9-activate-project-tool)
- **spec/mcp-server/file-watcher.md** - [Lifecycle](../spec/mcp-server/file-watcher.md#lifecycle) and [Sync on Activation](../spec/mcp-server/file-watcher.md#sync-on-activation-startup-reconciliation)
- **spec/mcp-server/database-schema.md** - [Tenant Management Schema](../spec/mcp-server/database-schema.md#tenant-management-schema)
- **research/ioptions-monitor-dynamic-paths.md** - Dynamic configuration loading patterns
- **research/git-current-branch-detection.md** - Git branch detection methods

---

## Objectives

1. Implement the `activate_project` MCP tool with attribute-based registration
2. Define and validate tool parameters (`config_path`, `branch_name`)
3. Compute repository root and path hash from config path
4. Update session state with active project context
5. Initialize file watcher on compounding docs directory
6. Trigger startup reconciliation between disk and database
7. Return activation response with project info and doc-type counts

---

## Acceptance Criteria

### Tool Registration

- [ ] Tool class annotated with `[McpServerToolType]`
- [ ] Tool method annotated with `[McpServerTool(Name = "activate_project")]`
- [ ] All parameters have `[Description]` attributes for LLM schema generation
- [ ] Tool registered and discoverable via `tools/list` MCP protocol

### Parameter Validation

- [ ] `config_path` parameter is required (absolute path to `.csharp-compounding-docs/config.json`)
- [ ] `branch_name` parameter is required (current git branch name)
- [ ] Config file existence validated before activation
- [ ] Path must end with `.csharp-compounding-docs/config.json`
- [ ] Returns `FILE_SYSTEM_ERROR` if config file does not exist

### Git Root and Path Hash Detection

- [ ] Repository root computed from config path (parent of `.csharp-compounding-docs/`)
- [ ] Path hash computed via SHA256 of normalized repo root path (first 16 hex chars)
- [ ] Git repository validation (warning logged if not a git repo, but continues)
- [ ] Detached HEAD state handled gracefully (uses provided `branch_name`)

### Session State Update

- [ ] Previous project deactivated before activating new project
- [ ] Active project info stored in `ISessionStateService`
- [ ] `TenantContext` created with `ProjectName`, `BranchName`, `PathHash`
- [ ] Switchable configuration provider updated with config path
- [ ] `IOptionsMonitorCache<ProjectOptions>` cleared to force config refresh

### Tenant Management Registration

- [ ] `repo_paths` table upserted with path_hash, absolute_path, project_name
- [ ] `branches` table upserted with project_name, branch_name
- [ ] `first_seen` set on initial activation
- [ ] `last_seen` updated on each activation

### File Watcher Initialization

- [ ] File watcher started on `{repo_root}/csharp-compounding-docs/` directory
- [ ] Previous file watcher stopped and disposed on new activation
- [ ] Recursive watching enabled for all subdirectories
- [ ] Debounce interval configurable (default 500ms)

### Startup Reconciliation Trigger

- [ ] Initial sync triggered after file watcher starts
- [ ] Disk-to-DB reconciliation performed per algorithm in file-watcher.md
- [ ] New files indexed, orphaned DB records deleted, changed files updated
- [ ] Reconciliation runs asynchronously (does not block activation response)

### Response Format

- [ ] Response includes `status: "activated"`
- [ ] Response includes `project_name`, `branch_name`, `path_hash`
- [ ] Response includes `doc_types` array with name and doc_count for each type
- [ ] Response includes `total_docs` count
- [ ] Custom doc types marked with `custom: true`

### Error Handling

- [ ] `FILE_SYSTEM_ERROR` for missing config file
- [ ] `DATABASE_ERROR` for tenant registration failures
- [ ] `EMBEDDING_SERVICE_ERROR` if reconciliation embedding fails
- [ ] Errors logged with correlation context

---

## Implementation Notes

### Tool Class Structure

Create `src/CompoundDocs.McpServer/Tools/ProjectTools.cs`:

```csharp
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Services;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// MCP tools for project lifecycle management.
/// </summary>
[McpServerToolType]
public class ProjectTools
{
    private readonly IProjectActivationService _activationService;
    private readonly IFileWatcherService _fileWatcherService;
    private readonly IReconciliationService _reconciliationService;
    private readonly IDocTypeService _docTypeService;
    private readonly ILogger<ProjectTools> _logger;

    public ProjectTools(
        IProjectActivationService activationService,
        IFileWatcherService fileWatcherService,
        IReconciliationService reconciliationService,
        IDocTypeService docTypeService,
        ILogger<ProjectTools> logger)
    {
        _activationService = activationService;
        _fileWatcherService = fileWatcherService;
        _reconciliationService = reconciliationService;
        _docTypeService = docTypeService;
        _logger = logger;
    }

    [McpServerTool(Name = "activate_project")]
    [Description("Activate a project for the session, establishing context for all other tools.")]
    public async Task<string> ActivateProject(
        [Description("Absolute path to .csharp-compounding-docs/config.json")]
        string config_path,
        [Description("Current git branch name")]
        string branch_name,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Activating project: config_path={ConfigPath}, branch={Branch}",
            config_path,
            branch_name);

        try
        {
            // Validate parameters
            var validationResult = ValidateParameters(config_path, branch_name);
            if (validationResult != null)
            {
                return validationResult;
            }

            // Activate the project
            var activationResult = await _activationService.ActivateProjectAsync(
                config_path,
                branch_name,
                cancellationToken);

            if (!activationResult.IsSuccess)
            {
                return JsonSerializer.Serialize(new ToolErrorResponse(
                    Error: true,
                    Code: "FILE_SYSTEM_ERROR",
                    Message: activationResult.ErrorMessage ?? "Failed to activate project"));
            }

            var tenantContext = activationResult.TenantContext!;
            var repoRoot = activationResult.RepoRoot!;

            // Start file watcher on compounding docs directory
            var compoundingDocsPath = Path.Combine(repoRoot, "csharp-compounding-docs");
            await _fileWatcherService.StartWatchingAsync(compoundingDocsPath, cancellationToken);

            // Trigger async reconciliation (non-blocking)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _reconciliationService.ReconcileAsync(
                        tenantContext,
                        compoundingDocsPath,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Background reconciliation failed for {TenantContext}",
                        tenantContext);
                }
            }, CancellationToken.None);

            // Get doc-type counts
            var docTypeCounts = await _docTypeService.GetDocTypeCountsAsync(
                tenantContext,
                cancellationToken);

            // Build response
            var response = new ActivateProjectResponse(
                Status: "activated",
                ProjectName: tenantContext.ProjectName,
                BranchName: tenantContext.BranchName,
                PathHash: tenantContext.PathHash,
                DocTypes: docTypeCounts.Select(dt => new DocTypeInfo(
                    Name: dt.Name,
                    DocCount: dt.Count,
                    Custom: dt.IsCustom)).ToList(),
                TotalDocs: docTypeCounts.Sum(dt => dt.Count));

            _logger.LogInformation(
                "Project activated successfully: {TenantContext}, total_docs={TotalDocs}",
                tenantContext,
                response.TotalDocs);

            return JsonSerializer.Serialize(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate project");
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: "DATABASE_ERROR",
                Message: "Failed to activate project: " + ex.Message));
        }
    }

    private string? ValidateParameters(string configPath, string branchName)
    {
        // Validate config_path is not empty
        if (string.IsNullOrWhiteSpace(configPath))
        {
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: "INVALID_PARAMETER",
                Message: "config_path is required"));
        }

        // Validate branch_name is not empty
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: "INVALID_PARAMETER",
                Message: "branch_name is required"));
        }

        // Validate config path ends with expected pattern
        if (!configPath.EndsWith(".csharp-compounding-docs/config.json") &&
            !configPath.EndsWith(".csharp-compounding-docs\\config.json"))
        {
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: "INVALID_PARAMETER",
                Message: "config_path must end with '.csharp-compounding-docs/config.json'"));
        }

        // Validate file exists
        if (!File.Exists(configPath))
        {
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: "FILE_SYSTEM_ERROR",
                Message: $"Config file not found: {configPath}"));
        }

        return null; // Valid
    }
}
```

### Response DTOs

Create `src/CompoundDocs.McpServer/Models/ActivateProjectResponse.cs`:

```csharp
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Response for the activate_project MCP tool.
/// </summary>
public record ActivateProjectResponse(
    string Status,
    string ProjectName,
    string BranchName,
    string PathHash,
    IReadOnlyList<DocTypeInfo> DocTypes,
    int TotalDocs
);

/// <summary>
/// Information about a document type in the activated project.
/// </summary>
public record DocTypeInfo(
    string Name,
    int DocCount,
    bool? Custom = null
);
```

### Project Activation Service Enhancement

Update `src/CompoundDocs.McpServer/Services/ProjectActivationService.cs` to return `RepoRoot`:

```csharp
public record ActivationResult
{
    public bool IsSuccess { get; init; }
    public TenantContext? TenantContext { get; init; }
    public string? RepoRoot { get; init; }
    public string? ErrorMessage { get; init; }

    public static ActivationResult Success(TenantContext tenant, string repoRoot) =>
        new() { IsSuccess = true, TenantContext = tenant, RepoRoot = repoRoot };

    public static ActivationResult Failed(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };
}
```

### Doc Type Service Interface

Create `src/CompoundDocs.McpServer/Services/IDocTypeService.cs`:

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Service for managing document types within a project.
/// </summary>
public interface IDocTypeService
{
    /// <summary>
    /// Gets document counts by type for the specified tenant context.
    /// </summary>
    Task<IReadOnlyList<DocTypeCount>> GetDocTypeCountsAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Document count for a specific doc-type.
/// </summary>
public record DocTypeCount(
    string Name,
    int Count,
    bool IsCustom
);
```

### Doc Type Service Implementation

Create `src/CompoundDocs.McpServer/Services/DocTypeService.cs`:

```csharp
namespace CompoundDocs.McpServer.Services;

public class DocTypeService : IDocTypeService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IOptionsMonitor<ProjectOptions> _projectOptions;
    private readonly ILogger<DocTypeService> _logger;

    // Built-in doc types (not custom)
    private static readonly HashSet<string> BuiltInDocTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "problem",
        "insight",
        "codebase",
        "tool",
        "style"
    };

    public DocTypeService(
        IDocumentRepository documentRepository,
        IOptionsMonitor<ProjectOptions> projectOptions,
        ILogger<DocTypeService> logger)
    {
        _documentRepository = documentRepository;
        _projectOptions = projectOptions;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DocTypeCount>> GetDocTypeCountsAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Getting doc type counts for {TenantContext}",
            tenantContext);

        var counts = await _documentRepository.GetDocTypeCountsAsync(
            tenantContext.ProjectName,
            tenantContext.BranchName,
            tenantContext.PathHash,
            cancellationToken);

        return counts
            .Select(kvp => new DocTypeCount(
                Name: kvp.Key,
                Count: kvp.Value,
                IsCustom: !BuiltInDocTypes.Contains(kvp.Key)))
            .OrderBy(dt => dt.IsCustom) // Built-in first
            .ThenBy(dt => dt.Name)
            .ToList();
    }
}
```

### File Watcher Service Interface

Ensure `IFileWatcherService` includes lifecycle methods:

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Service for watching file system changes in compounding docs directory.
/// </summary>
public interface IFileWatcherService
{
    /// <summary>
    /// Starts watching the specified directory for changes.
    /// Stops any existing watcher first.
    /// </summary>
    Task StartWatchingAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the current file watcher if active.
    /// </summary>
    Task StopWatchingAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether a file watcher is currently active.
    /// </summary>
    bool IsWatching { get; }

    /// <summary>
    /// Gets the path currently being watched, or null if not watching.
    /// </summary>
    string? CurrentWatchPath { get; }
}
```

### Reconciliation Service Interface

Create `src/CompoundDocs.McpServer/Services/IReconciliationService.cs`:

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Service for reconciling disk state with database state.
/// </summary>
public interface IReconciliationService
{
    /// <summary>
    /// Performs full reconciliation between disk and database for the specified tenant.
    /// </summary>
    /// <param name="tenantContext">The tenant context for isolation.</param>
    /// <param name="compoundingDocsPath">Path to the csharp-compounding-docs directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Reconciliation result with counts of added, updated, and deleted documents.</returns>
    Task<ReconciliationResult> ReconcileAsync(
        TenantContext tenantContext,
        string compoundingDocsPath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a reconciliation operation.
/// </summary>
public record ReconciliationResult(
    int FilesScanned,
    int DocumentsAdded,
    int DocumentsUpdated,
    int DocumentsDeleted,
    TimeSpan Duration
);
```

### Tenant Repository Enhancement

Ensure `ITenantRepository` has the upsert methods:

```csharp
public interface ITenantRepository
{
    /// <summary>
    /// Upserts a repository path record.
    /// </summary>
    Task UpsertRepoPathAsync(
        string pathHash,
        string absolutePath,
        string projectName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a branch record.
    /// </summary>
    Task UpsertBranchAsync(
        string projectName,
        string branchName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the last_seen timestamp for a path hash.
    /// </summary>
    Task UpdateLastSeenAsync(
        string pathHash,
        CancellationToken cancellationToken = default);
}
```

### Single Project Constraint

The MCP server runs as stdio (one per Claude Code instance), supporting only one active project at a time. Document this constraint:

```csharp
/// <summary>
/// Activates a project. Since the MCP server runs as stdio (one per Claude Code instance),
/// it supports only one active project at a time. Activating a new project deactivates
/// the previous one automatically.
/// </summary>
```

### DI Registration

Update `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs`:

```csharp
public static IServiceCollection AddProjectServices(this IServiceCollection services)
{
    services.AddSingleton<IProjectActivationService, ProjectActivationService>();
    services.AddSingleton<IFileWatcherService, FileWatcherService>();
    services.AddScoped<IReconciliationService, ReconciliationService>();
    services.AddScoped<IDocTypeService, DocTypeService>();
    services.AddScoped<ITenantRepository, TenantRepository>();

    return services;
}
```

---

## Dependencies

### NuGet Packages

```xml
<!-- Already included via Phase 025 -->
<PackageReference Include="ModelContextProtocol" Version="1.0.0" />
```

### Depends On

- **Phase 025**: Tool Registration System (provides `[McpServerToolType]`, `[McpServerTool]` attributes)
- **Phase 035**: Session State Management (provides `ISessionStateService`, `IProjectActivationService`)
- **Phase 039**: Git Detection Service (provides `IGitDetectionService` for validation)
- **Phase 053**: File Watcher Service (provides `IFileWatcherService` for document monitoring)

### Blocks

- **Phase 080+**: All other MCP tools that require active project context
- **Phase 085+**: RAG query operations
- **Phase 090+**: Document indexing operations

---

## Verification Steps

After completing this phase, verify:

1. **Tool Discovery**: Tool appears in `tools/list` with correct schema
2. **Parameter Validation**: Invalid parameters return appropriate error codes
3. **Session State**: Project context correctly stored after activation
4. **File Watcher**: Watcher starts on correct directory
5. **Reconciliation**: Initial sync triggered after activation
6. **Response Format**: Response matches spec exactly

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Tools/ProjectToolsTests.cs

[Fact]
public async Task ActivateProject_ValidParameters_ReturnsActivatedStatus()
{
    // Arrange
    var tools = CreateProjectTools();
    var configPath = CreateTempProjectConfig();

    try
    {
        // Act
        var result = await tools.ActivateProject(configPath, "main");
        var response = JsonSerializer.Deserialize<ActivateProjectResponse>(result);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("activated", response.Status);
        Assert.Equal("main", response.BranchName);
        Assert.NotNull(response.PathHash);
    }
    finally
    {
        CleanupTempProject(configPath);
    }
}

[Fact]
public async Task ActivateProject_MissingConfigFile_ReturnsFileSystemError()
{
    // Arrange
    var tools = CreateProjectTools();
    var nonExistentPath = "/path/to/.csharp-compounding-docs/config.json";

    // Act
    var result = await tools.ActivateProject(nonExistentPath, "main");
    var response = JsonSerializer.Deserialize<ToolErrorResponse>(result);

    // Assert
    Assert.NotNull(response);
    Assert.True(response.Error);
    Assert.Equal("FILE_SYSTEM_ERROR", response.Code);
}

[Fact]
public async Task ActivateProject_InvalidConfigPath_ReturnsInvalidParameterError()
{
    // Arrange
    var tools = CreateProjectTools();
    var invalidPath = "/path/to/some/other/config.json";

    // Act
    var result = await tools.ActivateProject(invalidPath, "main");
    var response = JsonSerializer.Deserialize<ToolErrorResponse>(result);

    // Assert
    Assert.NotNull(response);
    Assert.True(response.Error);
    Assert.Equal("INVALID_PARAMETER", response.Code);
    Assert.Contains(".csharp-compounding-docs/config.json", response.Message);
}

[Fact]
public async Task ActivateProject_EmptyBranchName_ReturnsInvalidParameterError()
{
    // Arrange
    var tools = CreateProjectTools();
    var configPath = CreateTempProjectConfig();

    try
    {
        // Act
        var result = await tools.ActivateProject(configPath, "");
        var response = JsonSerializer.Deserialize<ToolErrorResponse>(result);

        // Assert
        Assert.NotNull(response);
        Assert.True(response.Error);
        Assert.Equal("INVALID_PARAMETER", response.Code);
    }
    finally
    {
        CleanupTempProject(configPath);
    }
}

[Fact]
public async Task ActivateProject_SecondActivation_DeactivatesPrevious()
{
    // Arrange
    var tools = CreateProjectTools();
    var sessionState = GetMockedSessionStateService();
    var configPath1 = CreateTempProjectConfig("project1");
    var configPath2 = CreateTempProjectConfig("project2");

    try
    {
        // Act
        await tools.ActivateProject(configPath1, "main");
        await tools.ActivateProject(configPath2, "main");

        // Assert
        var activeProject = sessionState.ActiveProject;
        Assert.NotNull(activeProject);
        Assert.Contains("project2", activeProject.RepoRoot);
    }
    finally
    {
        CleanupTempProject(configPath1);
        CleanupTempProject(configPath2);
    }
}

[Fact]
public async Task ActivateProject_StartsFileWatcher()
{
    // Arrange
    var fileWatcherService = new Mock<IFileWatcherService>();
    var tools = CreateProjectTools(fileWatcherService: fileWatcherService.Object);
    var configPath = CreateTempProjectConfig();

    try
    {
        // Act
        await tools.ActivateProject(configPath, "main");

        // Assert
        fileWatcherService.Verify(
            s => s.StartWatchingAsync(
                It.Is<string>(p => p.EndsWith("csharp-compounding-docs")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    finally
    {
        CleanupTempProject(configPath);
    }
}

[Fact]
public async Task ActivateProject_TriggersReconciliation()
{
    // Arrange
    var reconciliationService = new Mock<IReconciliationService>();
    var tools = CreateProjectTools(reconciliationService: reconciliationService.Object);
    var configPath = CreateTempProjectConfig();

    try
    {
        // Act
        await tools.ActivateProject(configPath, "main");

        // Allow background task to start
        await Task.Delay(100);

        // Assert
        reconciliationService.Verify(
            s => s.ReconcileAsync(
                It.IsAny<TenantContext>(),
                It.Is<string>(p => p.EndsWith("csharp-compounding-docs")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
    finally
    {
        CleanupTempProject(configPath);
    }
}

[Fact]
public async Task ActivateProject_ReturnsDocTypeCounts()
{
    // Arrange
    var docTypeService = new Mock<IDocTypeService>();
    docTypeService
        .Setup(s => s.GetDocTypeCountsAsync(It.IsAny<TenantContext>(), default))
        .ReturnsAsync(new List<DocTypeCount>
        {
            new("problem", 12, false),
            new("insight", 5, false),
            new("api-contract", 4, true)
        });

    var tools = CreateProjectTools(docTypeService: docTypeService.Object);
    var configPath = CreateTempProjectConfig();

    try
    {
        // Act
        var result = await tools.ActivateProject(configPath, "main");
        var response = JsonSerializer.Deserialize<ActivateProjectResponse>(result);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(3, response.DocTypes.Count);
        Assert.Equal(21, response.TotalDocs);
        Assert.Contains(response.DocTypes, dt => dt.Name == "problem" && dt.DocCount == 12);
        Assert.Contains(response.DocTypes, dt => dt.Name == "api-contract" && dt.Custom == true);
    }
    finally
    {
        CleanupTempProject(configPath);
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Tools/ActivateProjectIntegrationTests.cs

[Trait("Category", "Integration")]
public class ActivateProjectIntegrationTests : IClassFixture<McpServerFixture>
{
    private readonly McpServerFixture _fixture;

    public ActivateProjectIntegrationTests(McpServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ActivateProject_FullWorkflow_Succeeds()
    {
        // Arrange
        var client = _fixture.CreateMcpClient();
        var projectPath = _fixture.CreateTestProject("integration-test");
        var configPath = Path.Combine(projectPath, ".csharp-compounding-docs", "config.json");

        try
        {
            // Act
            var response = await client.CallToolAsync("activate_project", new
            {
                config_path = configPath,
                branch_name = "main"
            });

            // Assert
            var result = JsonSerializer.Deserialize<ActivateProjectResponse>(response);
            Assert.NotNull(result);
            Assert.Equal("activated", result.Status);
            Assert.Equal("integration-test", result.ProjectName);
            Assert.Equal("main", result.BranchName);
            Assert.NotEmpty(result.PathHash);
        }
        finally
        {
            _fixture.CleanupTestProject(projectPath);
        }
    }

    [Fact]
    public async Task ActivateProject_RegistersTenantInDatabase()
    {
        // Arrange
        var client = _fixture.CreateMcpClient();
        var projectPath = _fixture.CreateTestProject("tenant-test");
        var configPath = Path.Combine(projectPath, ".csharp-compounding-docs", "config.json");

        try
        {
            // Act
            await client.CallToolAsync("activate_project", new
            {
                config_path = configPath,
                branch_name = "feature/test"
            });

            // Assert - Check database
            using var connection = _fixture.CreateDatabaseConnection();
            var repoPath = await connection.QuerySingleAsync<dynamic>(
                "SELECT * FROM tenant_management.repo_paths WHERE project_name = @Project",
                new { Project = "tenant-test" });

            Assert.NotNull(repoPath);
            Assert.Equal(projectPath, repoPath.absolute_path);

            var branch = await connection.QuerySingleAsync<dynamic>(
                "SELECT * FROM tenant_management.branches WHERE project_name = @Project AND branch_name = @Branch",
                new { Project = "tenant-test", Branch = "feature/test" });

            Assert.NotNull(branch);
        }
        finally
        {
            _fixture.CleanupTestProject(projectPath);
        }
    }
}
```

### Manual Verification

```bash
# 1. Start the MCP server
dotnet run --project src/CompoundDocs.McpServer

# 2. Use MCP client to list tools and verify activate_project is present
mcp-cli list-tools --stdio "dotnet run --project src/CompoundDocs.McpServer"

# 3. Create a test project structure
mkdir -p /tmp/test-project/.csharp-compounding-docs
echo '{"project_name": "test-project"}' > /tmp/test-project/.csharp-compounding-docs/config.json
mkdir -p /tmp/test-project/csharp-compounding-docs/problems

# 4. Call activate_project via MCP
mcp-cli call-tool activate_project \
  --param config_path=/tmp/test-project/.csharp-compounding-docs/config.json \
  --param branch_name=main

# Expected response:
# {
#   "status": "activated",
#   "project_name": "test-project",
#   "branch_name": "main",
#   "path_hash": "a1b2c3d4e5f6g7h8",
#   "doc_types": [
#     { "name": "problem", "doc_count": 0 },
#     { "name": "insight", "doc_count": 0 },
#     ...
#   ],
#   "total_docs": 0
# }

# 5. Verify tenant management tables
psql -h localhost -p 5433 -U compounding -d compounding_docs -c "
SELECT * FROM tenant_management.repo_paths WHERE project_name = 'test-project';"

psql -h localhost -p 5433 -U compounding -d compounding_docs -c "
SELECT * FROM tenant_management.branches WHERE project_name = 'test-project';"

# 6. Clean up
rm -rf /tmp/test-project
```

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `src/CompoundDocs.McpServer/Tools/ProjectTools.cs` | MCP tool class for activate_project |
| `src/CompoundDocs.McpServer/Models/ActivateProjectResponse.cs` | Response DTO |
| `src/CompoundDocs.McpServer/Models/DocTypeInfo.cs` | Doc type info DTO |
| `src/CompoundDocs.McpServer/Services/IDocTypeService.cs` | Doc type service interface |
| `src/CompoundDocs.McpServer/Services/DocTypeService.cs` | Doc type service implementation |
| `src/CompoundDocs.McpServer/Services/IReconciliationService.cs` | Reconciliation service interface |
| `src/CompoundDocs.McpServer/Services/ReconciliationService.cs` | Reconciliation service implementation |
| `tests/CompoundDocs.Tests/Tools/ProjectToolsTests.cs` | Unit tests |
| `tests/CompoundDocs.IntegrationTests/Tools/ActivateProjectIntegrationTests.cs` | Integration tests |

### Modified Files

| File | Changes |
|------|---------|
| `src/CompoundDocs.McpServer/Services/ProjectActivationService.cs` | Update ActivationResult to include RepoRoot |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Register new services |
| `src/CompoundDocs.McpServer/Program.cs` | Ensure tools are discovered via `.WithToolsFromAssembly()` |

---

## Error Response Examples

### Config File Not Found

```json
{
  "error": true,
  "code": "FILE_SYSTEM_ERROR",
  "message": "Config file not found: /path/to/.csharp-compounding-docs/config.json"
}
```

### Invalid Config Path

```json
{
  "error": true,
  "code": "INVALID_PARAMETER",
  "message": "config_path must end with '.csharp-compounding-docs/config.json'"
}
```

### Missing Branch Name

```json
{
  "error": true,
  "code": "INVALID_PARAMETER",
  "message": "branch_name is required"
}
```

### Database Error

```json
{
  "error": true,
  "code": "DATABASE_ERROR",
  "message": "Failed to activate project: Connection refused"
}
```

---

## Notes

- The `activate_project` tool is the gateway to all other tools - most tools require an active project
- Branch name is passed by the caller (Claude Code skill), not auto-detected, because Claude Code knows the branch context
- Reconciliation runs asynchronously to avoid blocking the activation response; the tool returns immediately after starting the background task
- Path hash ensures git worktrees (same project, different paths) are isolated in the database
- The file watcher must be stopped before starting a new one to prevent resource leaks
- Custom doc types are identified by comparing against the built-in list (problem, insight, codebase, tool, style)
- The response includes doc counts to give immediate feedback about the project state
