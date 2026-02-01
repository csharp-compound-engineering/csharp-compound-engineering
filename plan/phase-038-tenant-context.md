# Phase 038: Multi-Tenant Context Service

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: MCP Server Core
> **Prerequisites**: Phase 035 (Tenant Management Repository)

---

## Spec References

This phase implements the tenant context service defined in:

- **spec/mcp-server/database-schema.md** - Multi-tenant architecture with compound keys
- **structure/mcp-server.md** - Tenant isolation patterns

---

## Objectives

1. Define the `ITenantContext` interface for tenant isolation
2. Implement the compound key structure (project_name, branch_name, path_hash)
3. Create a scoped `TenantContext` implementation for per-request isolation
4. Implement tenant context injection into services
5. Add tenant validation with clear error messages
6. Create a `TenantContextAccessor` for ambient context access in non-DI scenarios

---

## Acceptance Criteria

### ITenantContext Interface

- [ ] `ITenantContext` interface exists in `CompoundDocs.McpServer.Abstractions`
- [ ] Interface exposes `ProjectName` property (string, required)
- [ ] Interface exposes `BranchName` property (string, required)
- [ ] Interface exposes `PathHash` property (string, required)
- [ ] Interface exposes `AbsolutePath` property (string, required)
- [ ] Interface exposes `IsInitialized` property (bool, read-only)
- [ ] Interface exposes `Initialize(string projectName, string branchName, string absolutePath)` method
- [ ] Interface exposes `CompoundKey` property returning composite key for query filtering

### TenantContext Implementation

- [ ] `TenantContext` class implements `ITenantContext`
- [ ] Constructor allows uninitialized state (for scoped registration)
- [ ] `Initialize()` method validates all required parameters
- [ ] `Initialize()` throws `ArgumentException` for null/empty values
- [ ] `Initialize()` computes `PathHash` using SHA256 (first 16 hex characters)
- [ ] Properties throw `InvalidOperationException` if accessed before initialization
- [ ] `CompoundKey` returns `TenantCompoundKey` record with all three components

### TenantCompoundKey Record

- [ ] `TenantCompoundKey` record exists with `ProjectName`, `BranchName`, `PathHash` properties
- [ ] Record implements `IEquatable<TenantCompoundKey>` (implicit via record)
- [ ] Record includes `ToString()` override for logging/debugging
- [ ] Record includes static `FromTenantContext(ITenantContext)` factory method

### Path Hash Generation

- [ ] `PathHashGenerator` static class exists with `ComputePathHash(string absolutePath)` method
- [ ] Normalizes path separators (`\` to `/`)
- [ ] Trims trailing slashes before hashing
- [ ] Uses SHA256 for hash computation
- [ ] Returns first 16 characters of hex-encoded hash (lowercase)
- [ ] Handles edge cases (empty path, whitespace-only, null)

### Tenant Context Accessor

- [ ] `ITenantContextAccessor` interface for ambient context access
- [ ] `TenantContextAccessor` implementation using `AsyncLocal<ITenantContext>`
- [ ] Registered as singleton in DI container
- [ ] Allows background services to access current tenant context

### Tenant Validation

- [ ] `TenantValidator` class with validation methods
- [ ] `ValidateProjectName(string)` - non-empty, valid characters
- [ ] `ValidateBranchName(string)` - non-empty, valid git branch characters
- [ ] `ValidateAbsolutePath(string)` - non-empty, path exists check (optional)
- [ ] Returns `ValidationResult` with error messages for clear diagnostics

### Service Registration

- [ ] `ITenantContext` registered as Scoped in DI container
- [ ] `ITenantContextAccessor` registered as Singleton in DI container
- [ ] `TenantValidator` registered as Singleton (stateless)
- [ ] Extension method `AddTenantServices(this IServiceCollection)` for organized registration

---

## Implementation Notes

### ITenantContext Interface

```csharp
namespace CompoundDocs.McpServer.Abstractions;

/// <summary>
/// Represents the current tenant context for multi-tenant isolation.
/// All database queries filter by the compound key (project_name, branch_name, path_hash).
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// The project name (e.g., "my-app", "company-api").
    /// </summary>
    string ProjectName { get; }

    /// <summary>
    /// The git branch name (e.g., "main", "feature/new-docs").
    /// </summary>
    string BranchName { get; }

    /// <summary>
    /// SHA256 hash of the normalized absolute path (first 16 hex characters).
    /// Distinguishes same project in different worktrees.
    /// </summary>
    string PathHash { get; }

    /// <summary>
    /// The absolute path to the repository root.
    /// </summary>
    string AbsolutePath { get; }

    /// <summary>
    /// Indicates whether the tenant context has been initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Gets the compound key for filtering database queries.
    /// </summary>
    TenantCompoundKey CompoundKey { get; }

    /// <summary>
    /// Initializes the tenant context. Must be called once per scope.
    /// </summary>
    /// <param name="projectName">The project name.</param>
    /// <param name="branchName">The git branch name.</param>
    /// <param name="absolutePath">The absolute path to the repository.</param>
    /// <exception cref="InvalidOperationException">If already initialized.</exception>
    /// <exception cref="ArgumentException">If any parameter is invalid.</exception>
    void Initialize(string projectName, string branchName, string absolutePath);
}
```

### TenantCompoundKey Record

```csharp
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Composite key for tenant isolation in multi-tenant queries.
/// </summary>
/// <param name="ProjectName">The project name.</param>
/// <param name="BranchName">The git branch name.</param>
/// <param name="PathHash">The SHA256 path hash (16 chars).</param>
public sealed record TenantCompoundKey(
    string ProjectName,
    string BranchName,
    string PathHash)
{
    /// <summary>
    /// Creates a compound key from a tenant context.
    /// </summary>
    public static TenantCompoundKey FromTenantContext(ITenantContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!context.IsInitialized)
        {
            throw new InvalidOperationException("Tenant context is not initialized.");
        }

        return new TenantCompoundKey(
            context.ProjectName,
            context.BranchName,
            context.PathHash);
    }

    public override string ToString() =>
        $"[{ProjectName}:{BranchName}:{PathHash}]";
}
```

### TenantContext Implementation

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Scoped implementation of ITenantContext.
/// Initialized once per request/operation via Initialize().
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private string? _projectName;
    private string? _branchName;
    private string? _pathHash;
    private string? _absolutePath;
    private bool _isInitialized;

    public string ProjectName =>
        _isInitialized
            ? _projectName!
            : throw new InvalidOperationException("Tenant context not initialized. Call Initialize() first.");

    public string BranchName =>
        _isInitialized
            ? _branchName!
            : throw new InvalidOperationException("Tenant context not initialized. Call Initialize() first.");

    public string PathHash =>
        _isInitialized
            ? _pathHash!
            : throw new InvalidOperationException("Tenant context not initialized. Call Initialize() first.");

    public string AbsolutePath =>
        _isInitialized
            ? _absolutePath!
            : throw new InvalidOperationException("Tenant context not initialized. Call Initialize() first.");

    public bool IsInitialized => _isInitialized;

    public TenantCompoundKey CompoundKey =>
        _isInitialized
            ? new TenantCompoundKey(ProjectName, BranchName, PathHash)
            : throw new InvalidOperationException("Tenant context not initialized. Call Initialize() first.");

    public void Initialize(string projectName, string branchName, string absolutePath)
    {
        if (_isInitialized)
        {
            throw new InvalidOperationException(
                "Tenant context already initialized. Create a new scope for different tenant.");
        }

        // Validate inputs
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName, nameof(projectName));
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName, nameof(branchName));
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath, nameof(absolutePath));

        _projectName = projectName;
        _branchName = branchName;
        _absolutePath = absolutePath;
        _pathHash = PathHashGenerator.ComputePathHash(absolutePath);
        _isInitialized = true;
    }
}
```

### PathHashGenerator

```csharp
namespace CompoundDocs.McpServer.Services;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Generates SHA256-based path hashes for tenant isolation.
/// </summary>
public static class PathHashGenerator
{
    /// <summary>
    /// Computes a 16-character hash of the normalized absolute path.
    /// </summary>
    /// <param name="absolutePath">The absolute path to hash.</param>
    /// <returns>First 16 characters of the SHA256 hex hash (lowercase).</returns>
    public static string ComputePathHash(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath, nameof(absolutePath));

        // Normalize path separators and trim trailing slashes
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

### TenantContextAccessor for Background Services

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Provides ambient access to the current tenant context.
/// Used by background services that cannot receive ITenantContext via DI.
/// </summary>
public interface ITenantContextAccessor
{
    /// <summary>
    /// Gets or sets the current tenant context.
    /// </summary>
    ITenantContext? TenantContext { get; set; }
}

/// <summary>
/// AsyncLocal-based implementation of ITenantContextAccessor.
/// </summary>
public sealed class TenantContextAccessor : ITenantContextAccessor
{
    private static readonly AsyncLocal<TenantContextHolder> _tenantContextCurrent = new();

    public ITenantContext? TenantContext
    {
        get => _tenantContextCurrent.Value?.Context;
        set
        {
            var holder = _tenantContextCurrent.Value;
            if (holder != null)
            {
                // Clear current context trapped in the AsyncLocals, as its done.
                holder.Context = null;
            }

            if (value != null)
            {
                // Use a holder to prevent the context from flowing back
                _tenantContextCurrent.Value = new TenantContextHolder { Context = value };
            }
        }
    }

    private sealed class TenantContextHolder
    {
        public ITenantContext? Context;
    }
}
```

### TenantValidator

```csharp
namespace CompoundDocs.McpServer.Services;

using System.Text.RegularExpressions;

/// <summary>
/// Validates tenant context parameters.
/// </summary>
public sealed partial class TenantValidator
{
    // Git branch name pattern: alphanumeric, dash, underscore, slash, dot (no consecutive dots, no leading/trailing special chars)
    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9/_.-]*[a-zA-Z0-9]$|^[a-zA-Z0-9]$", RegexOptions.Compiled)]
    private static partial Regex GitBranchPattern();

    // Project name pattern: alphanumeric, dash, underscore (no leading dash)
    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9_-]*$", RegexOptions.Compiled)]
    private static partial Regex ProjectNamePattern();

    public ValidationResult ValidateProjectName(string? projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            return ValidationResult.Failure("Project name cannot be null or empty.");
        }

        if (projectName.Length > 128)
        {
            return ValidationResult.Failure("Project name cannot exceed 128 characters.");
        }

        if (!ProjectNamePattern().IsMatch(projectName))
        {
            return ValidationResult.Failure(
                "Project name must start with alphanumeric and contain only alphanumeric, dash, or underscore characters.");
        }

        return ValidationResult.Success();
    }

    public ValidationResult ValidateBranchName(string? branchName)
    {
        if (string.IsNullOrWhiteSpace(branchName))
        {
            return ValidationResult.Failure("Branch name cannot be null or empty.");
        }

        if (branchName.Length > 256)
        {
            return ValidationResult.Failure("Branch name cannot exceed 256 characters.");
        }

        if (!GitBranchPattern().IsMatch(branchName))
        {
            return ValidationResult.Failure(
                "Branch name contains invalid characters. Must follow git branch naming rules.");
        }

        return ValidationResult.Success();
    }

    public ValidationResult ValidateAbsolutePath(string? absolutePath, bool checkExists = false)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return ValidationResult.Failure("Absolute path cannot be null or empty.");
        }

        if (!Path.IsPathFullyQualified(absolutePath))
        {
            return ValidationResult.Failure("Path must be fully qualified (absolute).");
        }

        if (checkExists && !Directory.Exists(absolutePath))
        {
            return ValidationResult.Failure($"Directory does not exist: {absolutePath}");
        }

        return ValidationResult.Success();
    }

    public ValidationResult ValidateTenantContext(
        string? projectName,
        string? branchName,
        string? absolutePath,
        bool checkPathExists = false)
    {
        var errors = new List<string>();

        var projectResult = ValidateProjectName(projectName);
        if (!projectResult.IsValid)
        {
            errors.Add($"ProjectName: {projectResult.ErrorMessage}");
        }

        var branchResult = ValidateBranchName(branchName);
        if (!branchResult.IsValid)
        {
            errors.Add($"BranchName: {branchResult.ErrorMessage}");
        }

        var pathResult = ValidateAbsolutePath(absolutePath, checkPathExists);
        if (!pathResult.IsValid)
        {
            errors.Add($"AbsolutePath: {pathResult.ErrorMessage}");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(string.Join("; ", errors));
    }
}

/// <summary>
/// Result of a validation operation.
/// </summary>
public readonly record struct ValidationResult(bool IsValid, string? ErrorMessage)
{
    public static ValidationResult Success() => new(true, null);
    public static ValidationResult Failure(string errorMessage) => new(false, errorMessage);
}
```

### Service Registration Extension

```csharp
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering tenant services.
/// </summary>
public static class TenantServiceCollectionExtensions
{
    /// <summary>
    /// Adds tenant context and related services to the service collection.
    /// </summary>
    public static IServiceCollection AddTenantServices(this IServiceCollection services)
    {
        // Scoped - per-request tenant isolation
        services.AddScoped<ITenantContext, TenantContext>();

        // Singleton - ambient context accessor for background services
        services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();

        // Singleton - stateless validator
        services.AddSingleton<TenantValidator>();

        return services;
    }
}
```

### Usage in MCP Tool Handlers

Example of how tenant context is initialized and used:

```csharp
public class ActivateProjectHandler
{
    private readonly ITenantContext _tenantContext;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly TenantValidator _validator;
    private readonly ILogger<ActivateProjectHandler> _logger;

    public ActivateProjectHandler(
        ITenantContext tenantContext,
        ITenantContextAccessor tenantContextAccessor,
        TenantValidator validator,
        ILogger<ActivateProjectHandler> logger)
    {
        _tenantContext = tenantContext;
        _tenantContextAccessor = tenantContextAccessor;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ActivateProjectResult> HandleAsync(ActivateProjectRequest request)
    {
        // Validate inputs
        var validationResult = _validator.ValidateTenantContext(
            request.ProjectName,
            request.BranchName,
            request.AbsolutePath,
            checkPathExists: true);

        if (!validationResult.IsValid)
        {
            return ActivateProjectResult.Error(validationResult.ErrorMessage!);
        }

        // Initialize scoped tenant context
        _tenantContext.Initialize(
            request.ProjectName,
            request.BranchName,
            request.AbsolutePath);

        // Set accessor for background services
        _tenantContextAccessor.TenantContext = _tenantContext;

        _logger.LogInformation(
            "Project activated: {CompoundKey}",
            _tenantContext.CompoundKey);

        return ActivateProjectResult.Success(_tenantContext.CompoundKey);
    }
}
```

### Query Filtering with Tenant Context

Integration with Semantic Kernel vector store:

```csharp
public class DocumentRepository : IDocumentRepository
{
    private readonly ITenantContext _tenantContext;
    private readonly IVectorStoreCollection<string, CompoundDocument> _collection;

    public DocumentRepository(
        ITenantContext tenantContext,
        IVectorStoreCollection<string, CompoundDocument> collection)
    {
        _tenantContext = tenantContext;
        _collection = collection;
    }

    public async Task<IEnumerable<CompoundDocument>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        var key = _tenantContext.CompoundKey;

        // Filter by tenant compound key
        var filter = new VectorSearchFilter()
            .EqualTo("project_name", key.ProjectName)
            .EqualTo("branch_name", key.BranchName)
            .EqualTo("path_hash", key.PathHash);

        var results = await _collection.SearchAsync(
            queryEmbedding,
            top: maxResults,
            filter: filter,
            cancellationToken: cancellationToken);

        return results.Select(r => r.Record);
    }
}
```

---

## Dependencies

### Depends On

- **Phase 035**: Tenant Management Repository (repo_paths and branches tables must be accessible)
- **Phase 017**: Dependency Injection Container Setup (IServiceCollection patterns)

### Blocks

- **Phase 039+**: Any phase requiring tenant-aware database queries
- **MCP Tool Handlers**: All tools require tenant context for data isolation
- **File Watcher Service**: Needs tenant context for reconciliation
- **Document Repository**: Needs tenant context for filtered queries

---

## Files to Create

| File Path | Description |
|-----------|-------------|
| `src/CompoundDocs.McpServer/Abstractions/ITenantContext.cs` | Tenant context interface |
| `src/CompoundDocs.McpServer/Abstractions/ITenantContextAccessor.cs` | Accessor interface |
| `src/CompoundDocs.McpServer/Models/TenantCompoundKey.cs` | Compound key record |
| `src/CompoundDocs.McpServer/Models/ValidationResult.cs` | Validation result record |
| `src/CompoundDocs.McpServer/Services/TenantContext.cs` | Scoped implementation |
| `src/CompoundDocs.McpServer/Services/TenantContextAccessor.cs` | AsyncLocal accessor |
| `src/CompoundDocs.McpServer/Services/TenantValidator.cs` | Validation service |
| `src/CompoundDocs.McpServer/Services/PathHashGenerator.cs` | Path hash utility |
| `src/CompoundDocs.McpServer/Extensions/TenantServiceCollectionExtensions.cs` | DI registration |
| `tests/CompoundDocs.McpServer.Tests/Services/TenantContextTests.cs` | Unit tests |
| `tests/CompoundDocs.McpServer.Tests/Services/PathHashGeneratorTests.cs` | Hash tests |
| `tests/CompoundDocs.McpServer.Tests/Services/TenantValidatorTests.cs` | Validation tests |

**Total files**: 12

---

## Verification Steps

After completing this phase, verify:

1. **Interface resolution**: `ITenantContext` and `ITenantContextAccessor` resolve from DI
2. **Scoped isolation**: Different scopes get independent tenant contexts
3. **Path hash consistency**: Same path always produces same hash
4. **Path hash uniqueness**: Different paths produce different hashes
5. **Validation coverage**: Invalid inputs are rejected with clear messages
6. **Initialization guard**: Accessing properties before Initialize() throws

### Unit Test Examples

```csharp
[Fact]
public void TenantContext_WhenNotInitialized_ThrowsOnPropertyAccess()
{
    var context = new TenantContext();

    Assert.False(context.IsInitialized);
    Assert.Throws<InvalidOperationException>(() => context.ProjectName);
    Assert.Throws<InvalidOperationException>(() => context.BranchName);
    Assert.Throws<InvalidOperationException>(() => context.PathHash);
}

[Fact]
public void TenantContext_Initialize_SetsAllProperties()
{
    var context = new TenantContext();

    context.Initialize("my-project", "main", "/home/user/repos/my-project");

    Assert.True(context.IsInitialized);
    Assert.Equal("my-project", context.ProjectName);
    Assert.Equal("main", context.BranchName);
    Assert.Equal("/home/user/repos/my-project", context.AbsolutePath);
    Assert.NotNull(context.PathHash);
    Assert.Equal(16, context.PathHash.Length);
}

[Fact]
public void TenantContext_Initialize_Twice_Throws()
{
    var context = new TenantContext();
    context.Initialize("project", "branch", "/path");

    Assert.Throws<InvalidOperationException>(
        () => context.Initialize("other", "other", "/other"));
}

[Fact]
public void PathHashGenerator_SameInput_SameOutput()
{
    var path = "/home/user/repos/my-project";

    var hash1 = PathHashGenerator.ComputePathHash(path);
    var hash2 = PathHashGenerator.ComputePathHash(path);

    Assert.Equal(hash1, hash2);
}

[Fact]
public void PathHashGenerator_NormalizesPathSeparators()
{
    var unixPath = "/home/user/repos/my-project";
    var windowsPath = "\\home\\user\\repos\\my-project";

    var unixHash = PathHashGenerator.ComputePathHash(unixPath);
    var windowsHash = PathHashGenerator.ComputePathHash(windowsPath);

    Assert.Equal(unixHash, windowsHash);
}

[Fact]
public void TenantValidator_ValidProjectName_Succeeds()
{
    var validator = new TenantValidator();

    var result = validator.ValidateProjectName("my-project_123");

    Assert.True(result.IsValid);
    Assert.Null(result.ErrorMessage);
}

[Fact]
public void TenantValidator_InvalidProjectName_FailsWithMessage()
{
    var validator = new TenantValidator();

    var result = validator.ValidateProjectName("-invalid");

    Assert.False(result.IsValid);
    Assert.Contains("must start with alphanumeric", result.ErrorMessage);
}
```

---

## Notes

- The `TenantContext` is scoped and must be initialized once per request/operation via the `activate_project` MCP tool
- Background services (singletons) must use `ITenantContextAccessor` with `AsyncLocal` for ambient context
- Path hash uses SHA256 truncated to 16 characters (64 bits) to balance uniqueness and readability
- The compound key (project_name, branch_name, path_hash) uniquely identifies a tenant even across git worktrees
- All database queries must filter by the compound key to ensure tenant isolation
- Consider adding telemetry/logging for tenant context initialization to aid debugging multi-tenant issues
