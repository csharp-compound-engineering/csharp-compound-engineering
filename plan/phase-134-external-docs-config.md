# Phase 134: External Documentation Configuration

> **Category**: Configuration System
> **Prerequisites**: Phase 010 (Project Configuration System)
> **Estimated Effort**: Small-Medium
> **Priority**: Medium (enables external doc search features)

---

## Overview

This phase extends the project configuration system to fully support external documentation folders. While Phase 010 defines the `ExternalDocsSettings` model, this phase implements the complete configuration workflow including path validation at activation time, schema isolation for external doc indexing, and the trigger mechanism for external path indexing.

---

## Goals

1. Implement comprehensive path validation for external documentation directories
2. Establish schema isolation between compounding docs and external docs
3. Create configuration examples and validation error messages
4. Integrate external docs configuration with the project activation workflow
5. Support include/exclude glob patterns for file selection

---

## Spec References

- [spec/configuration.md](../spec/configuration.md) - External Documentation (Optional) section
- [spec/mcp-server/tools.md](../spec/mcp-server/tools.md) - search_external_docs and rag_query_external tools

---

## Deliverables

### 1. External Docs Configuration Validator

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/Validation/`

#### 1.1 IExternalDocsConfigValidator Interface

```csharp
/// <summary>
/// Validates external documentation configuration settings.
/// Called during project activation to ensure external docs are accessible.
/// </summary>
public interface IExternalDocsConfigValidator
{
    /// <summary>
    /// Validates the external docs configuration for a project.
    /// </summary>
    /// <param name="projectPath">Project root path</param>
    /// <param name="externalDocs">External docs settings from config</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with resolved path and file count</returns>
    Task<ExternalDocsValidationResult> ValidateAsync(
        string projectPath,
        ExternalDocsSettings externalDocs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the external docs path relative to repository root.
    /// </summary>
    /// <param name="projectPath">Project root path</param>
    /// <param name="configuredPath">Path from config (relative or absolute)</param>
    /// <returns>Fully resolved absolute path</returns>
    string ResolveExternalPath(string projectPath, string configuredPath);

    /// <summary>
    /// Enumerates files matching the include/exclude patterns.
    /// </summary>
    /// <param name="resolvedPath">Resolved external docs path</param>
    /// <param name="settings">External docs settings with patterns</param>
    /// <returns>Enumerable of matching file paths</returns>
    IEnumerable<string> EnumerateMatchingFiles(
        string resolvedPath,
        ExternalDocsSettings settings);
}
```

#### 1.2 ExternalDocsConfigValidator Implementation

```csharp
/// <summary>
/// Validates external documentation configuration at activation time.
/// Per spec: "At activation time, the MCP server validates that external_docs.path
/// exists and is a directory."
/// </summary>
public sealed class ExternalDocsConfigValidator : IExternalDocsConfigValidator
{
    private readonly IGitRepositoryService _gitService;
    private readonly ILogger<ExternalDocsConfigValidator> _logger;

    public ExternalDocsConfigValidator(
        IGitRepositoryService gitService,
        ILogger<ExternalDocsConfigValidator> logger)
    {
        _gitService = gitService;
        _logger = logger;
    }

    public async Task<ExternalDocsValidationResult> ValidateAsync(
        string projectPath,
        ExternalDocsSettings externalDocs,
        CancellationToken cancellationToken = default)
    {
        // Resolve the path from repository root
        var resolvedPath = ResolveExternalPath(projectPath, externalDocs.Path);

        _logger.LogDebug(
            "Validating external docs path: configured='{Configured}', resolved='{Resolved}'",
            externalDocs.Path, resolvedPath);

        // Check directory exists
        if (!Directory.Exists(resolvedPath))
        {
            var errorMessage = $"External docs path '{externalDocs.Path}' does not exist. " +
                              $"Resolved path: {resolvedPath}";

            _logger.LogError(errorMessage);

            return ExternalDocsValidationResult.Failed(
                ExternalDocsValidationError.PathNotFound,
                errorMessage);
        }

        // Check if path is a directory (not a file)
        if (File.Exists(resolvedPath))
        {
            return ExternalDocsValidationResult.Failed(
                ExternalDocsValidationError.PathIsFile,
                $"External docs path '{externalDocs.Path}' is a file, not a directory");
        }

        // Enumerate matching files
        var matchingFiles = EnumerateMatchingFiles(resolvedPath, externalDocs).ToList();

        if (matchingFiles.Count == 0)
        {
            _logger.LogWarning(
                "External docs path '{Path}' exists but contains no files matching patterns. " +
                "Include patterns: [{Include}], Exclude patterns: [{Exclude}]",
                resolvedPath,
                string.Join(", ", externalDocs.IncludePatterns),
                string.Join(", ", externalDocs.ExcludePatterns));
        }
        else
        {
            _logger.LogInformation(
                "External docs path '{Path}' validated: {Count} matching files found",
                resolvedPath, matchingFiles.Count);
        }

        return ExternalDocsValidationResult.Success(
            resolvedPath,
            matchingFiles.Count,
            matchingFiles);
    }

    public string ResolveExternalPath(string projectPath, string configuredPath)
    {
        // Absolute paths are used as-is
        if (Path.IsPathRooted(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        // Per spec: "Relative paths are resolved from the repository root
        // (the directory containing .git/)"
        var repoRoot = _gitService.FindRepositoryRoot(projectPath);

        if (repoRoot is null)
        {
            // Fallback to project path if not in a git repo
            _logger.LogWarning(
                "No git repository found. Resolving external docs path from project path: {Path}",
                projectPath);
            repoRoot = projectPath;
        }

        return Path.GetFullPath(Path.Combine(repoRoot, configuredPath));
    }

    public IEnumerable<string> EnumerateMatchingFiles(
        string resolvedPath,
        ExternalDocsSettings settings)
    {
        var matcher = new Matcher();

        // Add include patterns (default: ["**/*.md"])
        foreach (var pattern in settings.IncludePatterns)
        {
            matcher.AddInclude(pattern);
        }

        // Add exclude patterns (default: ["**/node_modules/**"])
        foreach (var pattern in settings.ExcludePatterns)
        {
            matcher.AddExclude(pattern);
        }

        var directoryInfo = new DirectoryInfo(resolvedPath);
        var result = matcher.Execute(new DirectoryInfoWrapper(directoryInfo));

        return result.Files
            .Select(match => Path.Combine(resolvedPath, match.Path));
    }
}
```

#### 1.3 ExternalDocsValidationResult Record

```csharp
/// <summary>
/// Result of external docs configuration validation.
/// </summary>
public sealed record ExternalDocsValidationResult
{
    /// <summary>
    /// Whether validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Error code if validation failed.
    /// </summary>
    public ExternalDocsValidationError? ErrorCode { get; init; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Fully resolved absolute path to external docs.
    /// </summary>
    public string? ResolvedPath { get; init; }

    /// <summary>
    /// Count of files matching include/exclude patterns.
    /// </summary>
    public int MatchingFileCount { get; init; }

    /// <summary>
    /// List of matching file paths (for initial indexing).
    /// </summary>
    public IReadOnlyList<string>? MatchingFiles { get; init; }

    public static ExternalDocsValidationResult Success(
        string resolvedPath,
        int fileCount,
        IReadOnlyList<string> files) => new()
    {
        IsValid = true,
        ResolvedPath = resolvedPath,
        MatchingFileCount = fileCount,
        MatchingFiles = files
    };

    public static ExternalDocsValidationResult Failed(
        ExternalDocsValidationError errorCode,
        string errorMessage) => new()
    {
        IsValid = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// Error codes for external docs validation failures.
/// </summary>
public enum ExternalDocsValidationError
{
    /// <summary>
    /// The configured path does not exist.
    /// </summary>
    PathNotFound,

    /// <summary>
    /// The configured path is a file, not a directory.
    /// </summary>
    PathIsFile,

    /// <summary>
    /// The path exists but is not accessible.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// Include patterns are invalid.
    /// </summary>
    InvalidIncludePattern,

    /// <summary>
    /// Exclude patterns are invalid.
    /// </summary>
    InvalidExcludePattern
}
```

---

### 2. Schema Isolation Configuration

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/`

#### 2.1 ExternalDocsSchemaConfig Record

Per spec: "External docs are indexed in a separate collection from compounding docs."

```csharp
/// <summary>
/// Configuration for external docs schema isolation.
/// Ensures external docs don't pollute RAG queries for institutional knowledge.
/// </summary>
public static class ExternalDocsSchemaConfig
{
    /// <summary>
    /// Collection name suffix for external documents.
    /// Full name: {project_name}_external_documents
    /// </summary>
    public const string ExternalCollectionSuffix = "_external_documents";

    /// <summary>
    /// Collection name suffix for external document chunks.
    /// Full name: {project_name}_external_chunks
    /// </summary>
    public const string ExternalChunksSuffix = "_external_chunks";

    /// <summary>
    /// Gets the external documents collection name for a project.
    /// </summary>
    public static string GetExternalDocsCollectionName(string projectName) =>
        $"{projectName}{ExternalCollectionSuffix}";

    /// <summary>
    /// Gets the external chunks collection name for a project.
    /// </summary>
    public static string GetExternalChunksCollectionName(string projectName) =>
        $"{projectName}{ExternalChunksSuffix}";

    /// <summary>
    /// Determines if a collection name represents external docs.
    /// </summary>
    public static bool IsExternalCollection(string collectionName) =>
        collectionName.EndsWith(ExternalCollectionSuffix, StringComparison.OrdinalIgnoreCase) ||
        collectionName.EndsWith(ExternalChunksSuffix, StringComparison.OrdinalIgnoreCase);
}
```

---

### 3. External Docs Activation Integration

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/Services/`

#### 3.1 IExternalDocsActivationService Interface

```csharp
/// <summary>
/// Service for activating external docs configuration during project activation.
/// </summary>
public interface IExternalDocsActivationService
{
    /// <summary>
    /// Activates external docs for a project, triggering initial indexing if needed.
    /// </summary>
    /// <param name="projectPath">Project root path</param>
    /// <param name="config">Project configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Activation result with indexing status</returns>
    Task<ExternalDocsActivationResult> ActivateAsync(
        string projectPath,
        ProjectConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates external docs for a project (cleanup).
    /// </summary>
    Task DeactivateAsync(string projectName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if external docs are configured and active for current project.
    /// </summary>
    bool IsExternalDocsActive { get; }

    /// <summary>
    /// Gets the resolved external docs path for current project.
    /// </summary>
    string? ActiveExternalDocsPath { get; }
}
```

#### 3.2 ExternalDocsActivationService Implementation

```csharp
/// <summary>
/// Manages external docs activation during project lifecycle.
/// </summary>
public sealed class ExternalDocsActivationService : IExternalDocsActivationService
{
    private readonly IExternalDocsConfigValidator _validator;
    private readonly IExternalDocIndexingService _indexingService;
    private readonly IFileWatcherService _fileWatcherService;
    private readonly ILogger<ExternalDocsActivationService> _logger;

    private string? _activeExternalDocsPath;
    private string? _activeProjectName;

    public ExternalDocsActivationService(
        IExternalDocsConfigValidator validator,
        IExternalDocIndexingService indexingService,
        IFileWatcherService fileWatcherService,
        ILogger<ExternalDocsActivationService> logger)
    {
        _validator = validator;
        _indexingService = indexingService;
        _fileWatcherService = fileWatcherService;
        _logger = logger;
    }

    public bool IsExternalDocsActive => _activeExternalDocsPath is not null;
    public string? ActiveExternalDocsPath => _activeExternalDocsPath;

    public async Task<ExternalDocsActivationResult> ActivateAsync(
        string projectPath,
        ProjectConfig config,
        CancellationToken cancellationToken = default)
    {
        // If no external docs configured, return early
        if (config.ExternalDocs is null)
        {
            _logger.LogDebug(
                "Project '{ProjectName}' has no external docs configured",
                config.ProjectName);

            return ExternalDocsActivationResult.NotConfigured();
        }

        // Validate the external docs path
        var validationResult = await _validator.ValidateAsync(
            projectPath,
            config.ExternalDocs,
            cancellationToken);

        if (!validationResult.IsValid)
        {
            _logger.LogError(
                "External docs validation failed for project '{ProjectName}': {Error}",
                config.ProjectName, validationResult.ErrorMessage);

            return ExternalDocsActivationResult.ValidationFailed(
                validationResult.ErrorCode!.Value,
                validationResult.ErrorMessage!);
        }

        // Store active state
        _activeExternalDocsPath = validationResult.ResolvedPath;
        _activeProjectName = config.ProjectName;

        // Start file watcher for external docs
        _fileWatcherService.StartWatching(
            validationResult.ResolvedPath!,
            config.ExternalDocs.IncludePatterns,
            config.ExternalDocs.ExcludePatterns,
            ExternalDocsSchemaConfig.GetExternalDocsCollectionName(config.ProjectName));

        _logger.LogInformation(
            "Starting external docs indexing for project '{ProjectName}': {Count} files",
            config.ProjectName, validationResult.MatchingFileCount);

        // Trigger initial indexing
        var indexingResult = await _indexingService.IndexExternalDocsAsync(
            config.ProjectName,
            validationResult.ResolvedPath!,
            validationResult.MatchingFiles!,
            cancellationToken);

        return ExternalDocsActivationResult.Activated(
            validationResult.ResolvedPath!,
            validationResult.MatchingFileCount,
            indexingResult.IndexedCount,
            indexingResult.SkippedCount,
            indexingResult.ErrorCount);
    }

    public async Task DeactivateAsync(
        string projectName,
        CancellationToken cancellationToken = default)
    {
        if (_activeProjectName != projectName)
        {
            return;
        }

        // Stop file watcher
        if (_activeExternalDocsPath is not null)
        {
            _fileWatcherService.StopWatching(_activeExternalDocsPath);
        }

        _activeExternalDocsPath = null;
        _activeProjectName = null;

        _logger.LogDebug(
            "Deactivated external docs for project '{ProjectName}'",
            projectName);

        await Task.CompletedTask;
    }
}
```

#### 3.3 ExternalDocsActivationResult Record

```csharp
/// <summary>
/// Result of external docs activation during project activation.
/// </summary>
public sealed record ExternalDocsActivationResult
{
    /// <summary>
    /// Status of the activation.
    /// </summary>
    public ExternalDocsActivationStatus Status { get; init; }

    /// <summary>
    /// Error code if activation failed.
    /// </summary>
    public ExternalDocsValidationError? ErrorCode { get; init; }

    /// <summary>
    /// Error message if activation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Resolved path to external docs.
    /// </summary>
    public string? ResolvedPath { get; init; }

    /// <summary>
    /// Total files matching patterns.
    /// </summary>
    public int TotalFiles { get; init; }

    /// <summary>
    /// Files successfully indexed.
    /// </summary>
    public int IndexedFiles { get; init; }

    /// <summary>
    /// Files skipped (already indexed, unchanged).
    /// </summary>
    public int SkippedFiles { get; init; }

    /// <summary>
    /// Files that failed to index.
    /// </summary>
    public int ErrorFiles { get; init; }

    public static ExternalDocsActivationResult NotConfigured() => new()
    {
        Status = ExternalDocsActivationStatus.NotConfigured
    };

    public static ExternalDocsActivationResult ValidationFailed(
        ExternalDocsValidationError errorCode,
        string errorMessage) => new()
    {
        Status = ExternalDocsActivationStatus.ValidationFailed,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    public static ExternalDocsActivationResult Activated(
        string resolvedPath,
        int totalFiles,
        int indexedFiles,
        int skippedFiles,
        int errorFiles) => new()
    {
        Status = ExternalDocsActivationStatus.Activated,
        ResolvedPath = resolvedPath,
        TotalFiles = totalFiles,
        IndexedFiles = indexedFiles,
        SkippedFiles = skippedFiles,
        ErrorFiles = errorFiles
    };
}

/// <summary>
/// Status codes for external docs activation.
/// </summary>
public enum ExternalDocsActivationStatus
{
    /// <summary>
    /// External docs not configured in project config.
    /// </summary>
    NotConfigured,

    /// <summary>
    /// Validation failed (path not found, etc.).
    /// </summary>
    ValidationFailed,

    /// <summary>
    /// External docs activated and indexing triggered.
    /// </summary>
    Activated
}
```

---

### 4. Configuration Examples

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/Examples/`

Create embedded resource files with configuration examples:

#### 4.1 external-docs-basic.json

```json
{
  "project_name": "my-app",
  "external_docs": {
    "path": "./docs",
    "include_patterns": ["**/*.md"],
    "exclude_patterns": ["**/node_modules/**"]
  }
}
```

#### 4.2 external-docs-advanced.json

```json
{
  "project_name": "enterprise-app",
  "external_docs": {
    "path": "./documentation",
    "include_patterns": [
      "**/*.md",
      "**/*.rst",
      "**/README*"
    ],
    "exclude_patterns": [
      "**/node_modules/**",
      "**/drafts/**",
      "**/.git/**",
      "**/vendor/**"
    ]
  },
  "semantic_search": {
    "min_relevance_score": 0.6,
    "default_limit": 15
  }
}
```

#### 4.3 external-docs-absolute-path.json

```json
{
  "project_name": "shared-docs-project",
  "external_docs": {
    "path": "/shared/documentation/api-docs",
    "include_patterns": ["**/*.md"],
    "exclude_patterns": []
  }
}
```

---

### 5. Error Messages

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/Resources/`

#### 5.1 ExternalDocsErrorMessages.cs

```csharp
/// <summary>
/// Standardized error messages for external docs configuration.
/// </summary>
public static class ExternalDocsErrorMessages
{
    public static string PathNotFound(string configuredPath, string resolvedPath) =>
        $"External docs path '{configuredPath}' does not exist. " +
        $"Resolved to: {resolvedPath}. " +
        "Verify the path in your config.json or create the directory.";

    public static string PathIsFile(string configuredPath) =>
        $"External docs path '{configuredPath}' is a file, not a directory. " +
        "The external_docs.path must point to a directory containing documentation files.";

    public static string AccessDenied(string resolvedPath) =>
        $"Cannot access external docs path '{resolvedPath}'. " +
        "Check file system permissions.";

    public static string NoMatchingFiles(
        string resolvedPath,
        IEnumerable<string> includePatterns,
        IEnumerable<string> excludePatterns) =>
        $"External docs path '{resolvedPath}' exists but contains no matching files. " +
        $"Include patterns: [{string.Join(", ", includePatterns)}]. " +
        $"Exclude patterns: [{string.Join(", ", excludePatterns)}]. " +
        "Verify your include/exclude patterns in config.json.";

    public static string NotConfigured() =>
        "External docs are not configured for this project. " +
        "Add an 'external_docs' section to your .csharp-compounding-docs/config.json: " +
        "{ \"external_docs\": { \"path\": \"./docs\" } }";
}
```

---

### 6. Dependency Injection Registration

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/ServiceCollectionExtensions.cs`

Add to existing registration:

```csharp
public static class ConfigurationServiceCollectionExtensions
{
    public static IServiceCollection AddProjectConfiguration(
        this IServiceCollection services)
    {
        // Existing registrations...
        services.AddSingleton<ISchemaValidationService, SchemaValidationService>();
        services.AddSingleton<IProjectConfigurationService, ProjectConfigurationService>();
        services.AddSingleton<IProjectInitializationService, ProjectInitializationService>();
        services.AddSingleton<SwitchableProjectConfigurationProvider>();
        services.AddSingleton<ExternalDocsPathValidator>();

        // External docs configuration (Phase 134)
        services.AddSingleton<IExternalDocsConfigValidator, ExternalDocsConfigValidator>();
        services.AddSingleton<IExternalDocsActivationService, ExternalDocsActivationService>();

        return services;
    }
}
```

---

## Testing Requirements

### Unit Tests

1. **ExternalDocsConfigValidatorTests**
   - Validate existing directory path
   - Reject non-existent directory path
   - Reject file path (not directory)
   - Resolve relative path from repository root
   - Resolve absolute path unchanged
   - Enumerate files with include patterns
   - Exclude files with exclude patterns
   - Handle empty include patterns (match nothing)
   - Handle empty exclude patterns (exclude nothing)
   - Multiple include patterns (OR logic)
   - Multiple exclude patterns (OR logic)
   - Fallback to project path when no git repo

2. **ExternalDocsActivationServiceTests**
   - Activate with valid external docs config
   - Return NotConfigured when external_docs is null
   - Return ValidationFailed for invalid path
   - Start file watcher on activation
   - Stop file watcher on deactivation
   - Trigger indexing on activation
   - Track active external docs path
   - Handle reactivation (deactivate then activate)

3. **ExternalDocsSchemaConfigTests**
   - Generate correct collection names
   - Identify external collections by suffix
   - Distinguish from compounding docs collections

4. **ExternalDocsErrorMessagesTests**
   - Format PathNotFound message correctly
   - Include pattern list in NoMatchingFiles message
   - Include instructions in NotConfigured message

### Integration Tests

1. **External docs end-to-end**: Configure, validate, activate, verify indexing trigger
2. **Path resolution with real git repo**: Test relative path resolution
3. **Glob pattern matching**: Test with real file structure

---

## Acceptance Criteria

- [ ] `external_docs.path` validated at project activation time
- [ ] Relative paths resolved from repository root (`.git/` parent)
- [ ] Absolute paths used as-is without modification
- [ ] Clear error message when path does not exist
- [ ] Clear error message when path is a file (not directory)
- [ ] Include patterns default to `["**/*.md"]`
- [ ] Exclude patterns default to `["**/node_modules/**"]`
- [ ] Multiple include patterns work with OR logic
- [ ] Multiple exclude patterns work with OR logic
- [ ] External docs indexed to separate collection (`{project}_external_documents`)
- [ ] External chunks indexed to separate collection (`{project}_external_chunks`)
- [ ] File watcher started for external docs path on activation
- [ ] File watcher stopped on deactivation
- [ ] Configuration examples embedded as resources
- [ ] Error messages include remediation instructions
- [ ] Unit test coverage > 80% for all new code

---

## Dependencies

### NuGet Packages

```xml
<PackageReference Include="Microsoft.Extensions.FileSystemGlobbing" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
```

### Internal Dependencies

- Phase 010: Project Configuration System (prerequisite)
- Phase 039: Git Detection (for repository root resolution)
- Phase 053: File Watcher Service (for external docs watching)
- Phase 044: External Document Model (for indexing)
- Phase 049: External Repository (for storage)

---

## Configuration Examples Summary

### Basic External Docs

```json
{
  "project_name": "my-app",
  "external_docs": {
    "path": "./docs"
  }
}
```

### With Custom Patterns

```json
{
  "project_name": "my-app",
  "external_docs": {
    "path": "./documentation",
    "include_patterns": ["**/*.md", "**/*.rst"],
    "exclude_patterns": ["**/drafts/**", "**/node_modules/**"]
  }
}
```

### Absolute Path

```json
{
  "project_name": "my-app",
  "external_docs": {
    "path": "/shared/team-docs"
  }
}
```

---

## Notes

- External docs are **read-only** - no skills modify these documents
- Schema isolation ensures external docs don't affect RAG queries for institutional knowledge
- The `semantic_search.min_relevance_score` config value overrides the tool default for external doc searches
- External docs indexing is triggered on project activation and when the file watcher detects changes
- Per spec, external docs do not support link following (assumed standalone reference material)
