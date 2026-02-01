# Phase 010: Project Configuration System

> **Category**: Infrastructure Setup
> **Prerequisites**: Phase 008 (Global Configuration System)
> **Estimated Effort**: Medium
> **Priority**: High (blocks project activation and doc-type operations)

---

## Overview

This phase implements the project-level configuration system that manages per-project settings stored in `.csharp-compounding-docs/config.json`. Unlike global configuration which handles infrastructure concerns, project configuration controls RAG retrieval behavior, semantic search thresholds, external documentation paths, and custom doc-type definitions.

---

## Goals

1. Implement the project configuration schema and validation
2. Create configuration loading with `IOptionsMonitor` for hot-reload support
3. Build the first-time initialization workflow
4. Support custom doc-type definitions with schema file references
5. Integrate external documentation path configuration with validation

---

## Spec References

- [spec/configuration.md](../spec/configuration.md) - Project Configuration section
- [spec/configuration/schema-files.md](../spec/configuration/schema-files.md) - Schema file specifications
- [structure/configuration.md](../structure/configuration.md) - Configuration summary

---

## Deliverables

### 1. Project Configuration Models

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/`

#### 1.1 ProjectConfig Record

```csharp
/// <summary>
/// Root configuration object for project-level settings.
/// Stored at: .csharp-compounding-docs/config.json
/// </summary>
public sealed record ProjectConfig
{
    /// <summary>
    /// Project identifier used as PostgreSQL schema name.
    /// Pattern: ^[a-z][a-z0-9-]*$
    /// </summary>
    [Required]
    [RegularExpression(@"^[a-z][a-z0-9-]*$")]
    public required string ProjectName { get; init; }

    /// <summary>
    /// RAG query settings for document retrieval.
    /// </summary>
    public RetrievalSettings Retrieval { get; init; } = new();

    /// <summary>
    /// Semantic search settings (separate from RAG).
    /// </summary>
    public SemanticSearchSettings SemanticSearch { get; init; } = new();

    /// <summary>
    /// Link resolution settings for following document references.
    /// </summary>
    public LinkResolutionSettings LinkResolution { get; init; } = new();

    /// <summary>
    /// Optional external documentation folder configuration.
    /// </summary>
    public ExternalDocsSettings? ExternalDocs { get; init; }

    /// <summary>
    /// Custom doc-type definitions for project-specific document types.
    /// </summary>
    public IReadOnlyList<CustomDocType> CustomDocTypes { get; init; } = [];
}
```

#### 1.2 RetrievalSettings Record

```csharp
/// <summary>
/// RAG retrieval parameters for document queries.
/// </summary>
public sealed record RetrievalSettings
{
    /// <summary>
    /// Minimum similarity score for RAG queries (0.0-1.0).
    /// Default: 0.7 (high relevance for synthesis)
    /// </summary>
    [Range(0.0, 1.0)]
    public double MinRelevanceScore { get; init; } = 0.7;

    /// <summary>
    /// Maximum documents returned (excluding linked docs).
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxResults { get; init; } = 3;

    /// <summary>
    /// Maximum linked documents to include in results.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int MaxLinkedDocs { get; init; } = 5;
}
```

#### 1.3 SemanticSearchSettings Record

```csharp
/// <summary>
/// Semantic search parameters (separate from RAG retrieval).
/// Uses lower threshold since search is exploratory.
/// </summary>
public sealed record SemanticSearchSettings
{
    /// <summary>
    /// Minimum similarity score for semantic search (0.0-1.0).
    /// Default: 0.5 (lower than RAG for broader results)
    /// </summary>
    [Range(0.0, 1.0)]
    public double MinRelevanceScore { get; init; } = 0.5;

    /// <summary>
    /// Default number of results to return.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int DefaultLimit { get; init; } = 10;
}
```

#### 1.4 LinkResolutionSettings Record

```csharp
/// <summary>
/// Controls how document links are followed during retrieval.
/// </summary>
public sealed record LinkResolutionSettings
{
    /// <summary>
    /// How many levels deep to follow document links.
    /// 0 = no link following, 1 = immediate links only, etc.
    /// </summary>
    [Range(0, 10)]
    public int MaxDepth { get; init; } = 2;
}
```

#### 1.5 ExternalDocsSettings Record

```csharp
/// <summary>
/// Configuration for external (read-only) documentation folder.
/// </summary>
public sealed record ExternalDocsSettings
{
    /// <summary>
    /// Path to external docs folder.
    /// Relative paths resolved from repository root.
    /// </summary>
    [Required]
    public required string Path { get; init; }

    /// <summary>
    /// Glob patterns for files to include.
    /// </summary>
    public IReadOnlyList<string> IncludePatterns { get; init; } = ["**/*.md"];

    /// <summary>
    /// Glob patterns for files to exclude.
    /// </summary>
    public IReadOnlyList<string> ExcludePatterns { get; init; } = ["**/node_modules/**"];
}
```

#### 1.6 CustomDocType Record

```csharp
/// <summary>
/// Definition for a project-specific custom document type.
/// </summary>
public sealed record CustomDocType
{
    /// <summary>
    /// Doc-type identifier used in skill name.
    /// Pattern: ^[a-z][a-z0-9-]*$
    /// </summary>
    [Required]
    [RegularExpression(@"^[a-z][a-z0-9-]*$")]
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable description of the doc-type.
    /// </summary>
    [Required]
    public required string Description { get; init; }

    /// <summary>
    /// Subfolder name in ./csharp-compounding-docs/
    /// </summary>
    [Required]
    public required string Folder { get; init; }

    /// <summary>
    /// Path to schema file (relative to ./csharp-compounding-docs/)
    /// </summary>
    [Required]
    public required string SchemaFile { get; init; }
}
```

---

### 2. JSON Schema Definition

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/Schemas/`

#### 2.1 Project Config Schema (project-config.schema.json)

Create the embedded JSON Schema resource for config.json validation:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://csharp-compounding-docs/schemas/project-config.json",
  "title": "Project Configuration",
  "description": "Configuration for csharp-compounding-docs project settings",
  "type": "object",
  "required": ["project_name"],
  "properties": {
    "project_name": {
      "type": "string",
      "pattern": "^[a-z][a-z0-9-]*$",
      "description": "Project identifier (used as PostgreSQL schema name)"
    },
    "retrieval": {
      "$ref": "#/$defs/retrievalSettings"
    },
    "semantic_search": {
      "$ref": "#/$defs/semanticSearchSettings"
    },
    "link_resolution": {
      "$ref": "#/$defs/linkResolutionSettings"
    },
    "external_docs": {
      "$ref": "#/$defs/externalDocsSettings"
    },
    "custom_doc_types": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/customDocType"
      },
      "default": []
    }
  },
  "$defs": {
    "retrievalSettings": {
      "type": "object",
      "properties": {
        "min_relevance_score": {
          "type": "number",
          "minimum": 0,
          "maximum": 1,
          "default": 0.7
        },
        "max_results": {
          "type": "integer",
          "minimum": 1,
          "default": 3
        },
        "max_linked_docs": {
          "type": "integer",
          "minimum": 0,
          "default": 5
        }
      }
    },
    "semanticSearchSettings": {
      "type": "object",
      "properties": {
        "min_relevance_score": {
          "type": "number",
          "minimum": 0,
          "maximum": 1,
          "default": 0.5
        },
        "default_limit": {
          "type": "integer",
          "minimum": 1,
          "default": 10
        }
      }
    },
    "linkResolutionSettings": {
      "type": "object",
      "properties": {
        "max_depth": {
          "type": "integer",
          "minimum": 0,
          "default": 2
        }
      }
    },
    "externalDocsSettings": {
      "type": "object",
      "required": ["path"],
      "properties": {
        "path": {
          "type": "string",
          "description": "Path to external docs folder"
        },
        "include_patterns": {
          "type": "array",
          "items": { "type": "string" },
          "default": ["**/*.md"]
        },
        "exclude_patterns": {
          "type": "array",
          "items": { "type": "string" },
          "default": ["**/node_modules/**"]
        }
      }
    },
    "customDocType": {
      "type": "object",
      "required": ["name", "description", "folder", "schema_file"],
      "properties": {
        "name": {
          "type": "string",
          "pattern": "^[a-z][a-z0-9-]*$"
        },
        "description": {
          "type": "string"
        },
        "folder": {
          "type": "string"
        },
        "schema_file": {
          "type": "string"
        }
      }
    }
  }
}
```

---

### 3. Configuration Services

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/Services/`

#### 3.1 IProjectConfigurationService Interface

```csharp
/// <summary>
/// Service for managing project-level configuration.
/// </summary>
public interface IProjectConfigurationService
{
    /// <summary>
    /// Loads configuration for the specified project path.
    /// </summary>
    /// <param name="projectPath">Path to project root</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Project configuration or null if not initialized</returns>
    Task<ProjectConfig?> LoadAsync(string projectPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves configuration for the specified project.
    /// </summary>
    Task SaveAsync(string projectPath, ProjectConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates configuration against JSON Schema.
    /// </summary>
    /// <returns>Validation result with any errors</returns>
    ValidationResult Validate(ProjectConfig config);

    /// <summary>
    /// Checks if a project has been initialized.
    /// </summary>
    bool IsInitialized(string projectPath);

    /// <summary>
    /// Gets the configuration file path for a project.
    /// </summary>
    string GetConfigPath(string projectPath);
}
```

#### 3.2 ProjectConfigurationService Implementation

```csharp
public sealed class ProjectConfigurationService : IProjectConfigurationService
{
    private const string ConfigFolder = ".csharp-compounding-docs";
    private const string ConfigFileName = "config.json";

    private readonly ISchemaValidationService _schemaValidator;
    private readonly ILogger<ProjectConfigurationService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ProjectConfigurationService(
        ISchemaValidationService schemaValidator,
        ILogger<ProjectConfigurationService> logger)
    {
        _schemaValidator = schemaValidator;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public string GetConfigPath(string projectPath) =>
        Path.Combine(projectPath, ConfigFolder, ConfigFileName);

    public bool IsInitialized(string projectPath) =>
        File.Exists(GetConfigPath(projectPath));

    public async Task<ProjectConfig?> LoadAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var configPath = GetConfigPath(projectPath);

        if (!File.Exists(configPath))
        {
            _logger.LogDebug("No config found at {Path}", configPath);
            return null;
        }

        var json = await File.ReadAllTextAsync(configPath, cancellationToken);
        var config = JsonSerializer.Deserialize<ProjectConfig>(json, _jsonOptions);

        if (config is null)
        {
            throw new ConfigurationException($"Failed to deserialize config at {configPath}");
        }

        var validationResult = Validate(config);
        if (!validationResult.IsValid)
        {
            throw new ConfigurationValidationException(configPath, validationResult.Errors);
        }

        return config;
    }

    public async Task SaveAsync(
        string projectPath,
        ProjectConfig config,
        CancellationToken cancellationToken = default)
    {
        var validationResult = Validate(config);
        if (!validationResult.IsValid)
        {
            throw new ConfigurationValidationException(
                GetConfigPath(projectPath),
                validationResult.Errors);
        }

        var configDir = Path.Combine(projectPath, ConfigFolder);
        Directory.CreateDirectory(configDir);

        var json = JsonSerializer.Serialize(config, _jsonOptions);
        await File.WriteAllTextAsync(GetConfigPath(projectPath), json, cancellationToken);
    }

    public ValidationResult Validate(ProjectConfig config)
    {
        // Use JsonSchema.Net for validation
        return _schemaValidator.ValidateProjectConfig(config);
    }
}
```

#### 3.3 ISchemaValidationService Interface

```csharp
/// <summary>
/// Service for validating configurations and documents against JSON Schema.
/// </summary>
public interface ISchemaValidationService
{
    /// <summary>
    /// Validates project configuration against embedded schema.
    /// </summary>
    ValidationResult ValidateProjectConfig(ProjectConfig config);

    /// <summary>
    /// Validates YAML frontmatter against a doc-type schema.
    /// </summary>
    /// <param name="frontmatter">Parsed YAML frontmatter as JsonNode</param>
    /// <param name="schemaPath">Path to schema file</param>
    ValidationResult ValidateFrontmatter(JsonNode frontmatter, string schemaPath);

    /// <summary>
    /// Loads and caches a schema from file.
    /// </summary>
    JsonSchema LoadSchema(string schemaPath);
}
```

#### 3.4 SchemaValidationService Implementation

```csharp
public sealed class SchemaValidationService : ISchemaValidationService
{
    private readonly ILogger<SchemaValidationService> _logger;
    private readonly ConcurrentDictionary<string, JsonSchema> _schemaCache = new();
    private readonly JsonSchema _projectConfigSchema;

    public SchemaValidationService(ILogger<SchemaValidationService> logger)
    {
        _logger = logger;
        _projectConfigSchema = LoadEmbeddedSchema("project-config.schema.json");
    }

    public ValidationResult ValidateProjectConfig(ProjectConfig config)
    {
        var jsonNode = JsonSerializer.SerializeToNode(config, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        return EvaluateSchema(_projectConfigSchema, jsonNode!);
    }

    public ValidationResult ValidateFrontmatter(JsonNode frontmatter, string schemaPath)
    {
        var schema = LoadSchema(schemaPath);
        return EvaluateSchema(schema, frontmatter);
    }

    public JsonSchema LoadSchema(string schemaPath)
    {
        return _schemaCache.GetOrAdd(schemaPath, path =>
        {
            var content = File.ReadAllText(path);

            // Determine format by extension
            if (path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            {
                // Convert YAML to JSON for schema parsing
                var yaml = new YamlStream();
                yaml.Load(new StringReader(content));
                var jsonNode = yaml.Documents[0].RootNode.ToJsonNode();
                content = jsonNode!.ToJsonString();
            }

            return JsonSchema.FromText(content);
        });
    }

    private ValidationResult EvaluateSchema(JsonSchema schema, JsonNode instance)
    {
        var result = schema.Evaluate(instance, new EvaluationOptions
        {
            OutputFormat = OutputFormat.List,
            ValidateAs = Draft.Draft202012
        });

        if (result.IsValid)
        {
            return ValidationResult.Success();
        }

        var errors = result.Details
            .Where(d => !d.IsValid && d.Errors is not null)
            .SelectMany(d => d.Errors!.Select(e =>
                new ValidationError(d.InstanceLocation.ToString(), e.Key, e.Value)))
            .ToList();

        return ValidationResult.Failed(errors);
    }

    private static JsonSchema LoadEmbeddedSchema(string resourceName)
    {
        var assembly = typeof(SchemaValidationService).Assembly;
        var fullName = $"CSharpCompoundingDocs.Core.Configuration.Schemas.{resourceName}";

        using var stream = assembly.GetManifestResourceStream(fullName)
            ?? throw new InvalidOperationException($"Embedded schema not found: {fullName}");

        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }
}
```

---

### 4. Project Initialization Service

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/Services/`

#### 4.1 IProjectInitializationService Interface

```csharp
/// <summary>
/// Service for initializing new projects with compounding docs structure.
/// </summary>
public interface IProjectInitializationService
{
    /// <summary>
    /// Initializes a new project with default configuration and folder structure.
    /// </summary>
    /// <param name="projectPath">Path to project root</param>
    /// <param name="projectName">Optional explicit project name (derived from folder if null)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created project configuration</returns>
    Task<ProjectConfig> InitializeAsync(
        string projectPath,
        string? projectName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds built-in doc-type folders to an existing project.
    /// </summary>
    Task EnsureBuiltInFoldersAsync(string projectPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates folders for custom doc-types.
    /// </summary>
    Task CreateCustomDocTypeFoldersAsync(
        string projectPath,
        IEnumerable<CustomDocType> customDocTypes,
        CancellationToken cancellationToken = default);
}
```

#### 4.2 ProjectInitializationService Implementation

```csharp
public sealed class ProjectInitializationService : IProjectInitializationService
{
    private static readonly string[] BuiltInFolders =
    [
        "problems",
        "insights",
        "codebase",
        "tools",
        "styles",
        "schemas"
    ];

    private const string ConfigFolder = ".csharp-compounding-docs";
    private const string DocsFolder = "csharp-compounding-docs";

    private readonly IProjectConfigurationService _configService;
    private readonly ILogger<ProjectInitializationService> _logger;

    public ProjectInitializationService(
        IProjectConfigurationService configService,
        ILogger<ProjectInitializationService> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public async Task<ProjectConfig> InitializeAsync(
        string projectPath,
        string? projectName = null,
        CancellationToken cancellationToken = default)
    {
        if (_configService.IsInitialized(projectPath))
        {
            throw new InvalidOperationException(
                $"Project already initialized at {projectPath}");
        }

        // Derive project name from folder if not provided
        projectName ??= DeriveProjectName(projectPath);

        _logger.LogInformation(
            "Initializing compounding docs for project '{ProjectName}' at {Path}",
            projectName, projectPath);

        // Create config folder (hidden)
        var configDir = Path.Combine(projectPath, ConfigFolder);
        Directory.CreateDirectory(configDir);

        // Create default config
        var config = new ProjectConfig
        {
            ProjectName = projectName,
            CustomDocTypes = []
        };

        await _configService.SaveAsync(projectPath, config, cancellationToken);

        // Create docs folder structure
        await EnsureBuiltInFoldersAsync(projectPath, cancellationToken);

        // Suggest .gitignore addition
        await SuggestGitIgnoreAsync(projectPath, cancellationToken);

        _logger.LogInformation("Project initialization complete");

        return config;
    }

    public Task EnsureBuiltInFoldersAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var docsPath = Path.Combine(projectPath, DocsFolder);

        foreach (var folder in BuiltInFolders)
        {
            var folderPath = Path.Combine(docsPath, folder);
            Directory.CreateDirectory(folderPath);
            _logger.LogDebug("Created folder: {Path}", folderPath);
        }

        return Task.CompletedTask;
    }

    public Task CreateCustomDocTypeFoldersAsync(
        string projectPath,
        IEnumerable<CustomDocType> customDocTypes,
        CancellationToken cancellationToken = default)
    {
        var docsPath = Path.Combine(projectPath, DocsFolder);

        foreach (var docType in customDocTypes)
        {
            var folderPath = Path.Combine(docsPath, docType.Folder);
            Directory.CreateDirectory(folderPath);
            _logger.LogDebug("Created custom doc-type folder: {Path}", folderPath);
        }

        return Task.CompletedTask;
    }

    private static string DeriveProjectName(string projectPath)
    {
        var folderName = new DirectoryInfo(projectPath).Name;

        // Convert to valid project name format: lowercase, alphanumeric with hyphens
        var normalized = folderName
            .ToLowerInvariant()
            .Replace('_', '-')
            .Replace(' ', '-');

        // Remove any invalid characters
        var valid = new string(normalized
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .ToArray());

        // Ensure starts with letter
        if (valid.Length == 0 || !char.IsLetter(valid[0]))
        {
            valid = "project-" + valid;
        }

        return valid;
    }

    private async Task SuggestGitIgnoreAsync(
        string projectPath,
        CancellationToken cancellationToken)
    {
        var gitIgnorePath = Path.Combine(projectPath, ".gitignore");

        if (!File.Exists(gitIgnorePath))
        {
            return;
        }

        var content = await File.ReadAllTextAsync(gitIgnorePath, cancellationToken);

        if (content.Contains(".csharp-compounding-docs"))
        {
            return;
        }

        _logger.LogInformation(
            "Consider adding to .gitignore:\n" +
            "# Compounding docs config (optional)\n" +
            "# .csharp-compounding-docs/");
    }
}
```

---

### 5. Switchable Configuration Provider for IOptionsMonitor

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/Providers/`

#### 5.1 SwitchableConfigurationProvider

Enables dynamic configuration reloading when projects are switched.

```csharp
/// <summary>
/// Configuration provider that supports switching between project configurations
/// at runtime with IOptionsMonitor change notifications.
/// </summary>
public sealed class SwitchableProjectConfigurationProvider : IDisposable
{
    private readonly IProjectConfigurationService _configService;
    private readonly ILogger<SwitchableProjectConfigurationProvider> _logger;

    private ProjectConfig? _currentConfig;
    private string? _currentProjectPath;
    private FileSystemWatcher? _watcher;
    private readonly object _lock = new();

    public event EventHandler<ProjectConfigChangedEventArgs>? ConfigChanged;

    public SwitchableProjectConfigurationProvider(
        IProjectConfigurationService configService,
        ILogger<SwitchableProjectConfigurationProvider> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public ProjectConfig? CurrentConfig => _currentConfig;
    public string? CurrentProjectPath => _currentProjectPath;

    /// <summary>
    /// Switches to a new project configuration.
    /// </summary>
    public async Task SwitchProjectAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _watcher?.Dispose();
            _watcher = null;
        }

        var config = await _configService.LoadAsync(projectPath, cancellationToken);

        lock (_lock)
        {
            var oldConfig = _currentConfig;
            _currentConfig = config;
            _currentProjectPath = projectPath;

            // Setup file watcher for hot reload
            SetupFileWatcher(projectPath);

            ConfigChanged?.Invoke(this, new ProjectConfigChangedEventArgs(oldConfig, config));
        }

        _logger.LogInformation(
            "Switched to project configuration: {ProjectName}",
            config?.ProjectName ?? "(uninitialized)");
    }

    /// <summary>
    /// Clears the current project configuration.
    /// </summary>
    public void ClearProject()
    {
        lock (_lock)
        {
            _watcher?.Dispose();
            _watcher = null;

            var oldConfig = _currentConfig;
            _currentConfig = null;
            _currentProjectPath = null;

            ConfigChanged?.Invoke(this, new ProjectConfigChangedEventArgs(oldConfig, null));
        }
    }

    private void SetupFileWatcher(string projectPath)
    {
        var configPath = _configService.GetConfigPath(projectPath);
        var directory = Path.GetDirectoryName(configPath)!;
        var fileName = Path.GetFileName(configPath);

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _watcher.Changed += OnConfigFileChanged;
        _watcher.EnableRaisingEvents = true;
    }

    private async void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            // Debounce - handled at a higher level typically
            await Task.Delay(100);

            if (_currentProjectPath is null) return;

            var newConfig = await _configService.LoadAsync(_currentProjectPath);

            lock (_lock)
            {
                var oldConfig = _currentConfig;
                _currentConfig = newConfig;
                ConfigChanged?.Invoke(this, new ProjectConfigChangedEventArgs(oldConfig, newConfig));
            }

            _logger.LogInformation("Project configuration reloaded due to file change");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload configuration after file change");
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}

public sealed record ProjectConfigChangedEventArgs(
    ProjectConfig? OldConfig,
    ProjectConfig? NewConfig);
```

---

### 6. External Docs Path Validation

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/Validation/`

#### 6.1 ExternalDocsPathValidator

```csharp
/// <summary>
/// Validates external documentation path configuration.
/// </summary>
public sealed class ExternalDocsPathValidator
{
    private readonly ILogger<ExternalDocsPathValidator> _logger;

    public ExternalDocsPathValidator(ILogger<ExternalDocsPathValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates the external docs path exists and is accessible.
    /// </summary>
    /// <param name="projectPath">Project root path</param>
    /// <param name="externalDocs">External docs settings</param>
    /// <returns>Validation result</returns>
    public ExternalDocsValidationResult Validate(
        string projectPath,
        ExternalDocsSettings externalDocs)
    {
        var resolvedPath = ResolvePath(projectPath, externalDocs.Path);

        if (!Directory.Exists(resolvedPath))
        {
            return ExternalDocsValidationResult.Failed(
                $"External docs path '{externalDocs.Path}' does not exist. " +
                $"Resolved to: {resolvedPath}");
        }

        // Check if any files match the include patterns
        var matchingFiles = CountMatchingFiles(resolvedPath, externalDocs);

        if (matchingFiles == 0)
        {
            _logger.LogWarning(
                "External docs path '{Path}' exists but contains no matching files",
                resolvedPath);
        }
        else
        {
            _logger.LogDebug(
                "External docs path '{Path}' contains {Count} matching files",
                resolvedPath, matchingFiles);
        }

        return ExternalDocsValidationResult.Success(resolvedPath, matchingFiles);
    }

    /// <summary>
    /// Resolves a relative path from the repository root.
    /// </summary>
    public string ResolvePath(string projectPath, string externalPath)
    {
        if (Path.IsPathRooted(externalPath))
        {
            return externalPath;
        }

        // Find repository root (directory containing .git)
        var repoRoot = FindRepositoryRoot(projectPath) ?? projectPath;

        return Path.GetFullPath(Path.Combine(repoRoot, externalPath));
    }

    private static string? FindRepositoryRoot(string startPath)
    {
        var current = new DirectoryInfo(startPath);

        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        return null;
    }

    private static int CountMatchingFiles(
        string basePath,
        ExternalDocsSettings settings)
    {
        // Use Microsoft.Extensions.FileSystemGlobbing for pattern matching
        var matcher = new Matcher();

        foreach (var include in settings.IncludePatterns)
        {
            matcher.AddInclude(include);
        }

        foreach (var exclude in settings.ExcludePatterns)
        {
            matcher.AddExclude(exclude);
        }

        var result = matcher.Execute(
            new DirectoryInfoWrapper(new DirectoryInfo(basePath)));

        return result.Files.Count();
    }
}

public sealed record ExternalDocsValidationResult
{
    public bool IsValid { get; init; }
    public string? Error { get; init; }
    public string? ResolvedPath { get; init; }
    public int MatchingFileCount { get; init; }

    public static ExternalDocsValidationResult Success(string resolvedPath, int fileCount) =>
        new() { IsValid = true, ResolvedPath = resolvedPath, MatchingFileCount = fileCount };

    public static ExternalDocsValidationResult Failed(string error) =>
        new() { IsValid = false, Error = error };
}
```

---

### 7. Dependency Injection Registration

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/ServiceCollectionExtensions.cs`

```csharp
public static class ConfigurationServiceCollectionExtensions
{
    public static IServiceCollection AddProjectConfiguration(
        this IServiceCollection services)
    {
        services.AddSingleton<ISchemaValidationService, SchemaValidationService>();
        services.AddSingleton<IProjectConfigurationService, ProjectConfigurationService>();
        services.AddSingleton<IProjectInitializationService, ProjectInitializationService>();
        services.AddSingleton<SwitchableProjectConfigurationProvider>();
        services.AddSingleton<ExternalDocsPathValidator>();

        return services;
    }
}
```

---

## Testing Requirements

### Unit Tests

1. **ProjectConfigurationServiceTests**
   - Load valid configuration from file
   - Handle missing configuration file (returns null)
   - Validate configuration against schema
   - Reject invalid project names (pattern validation)
   - Serialize/deserialize with snake_case JSON

2. **SchemaValidationServiceTests**
   - Validate project config against embedded schema
   - Load YAML schema files
   - Load JSON schema files
   - Cache schema files after first load
   - Return detailed validation errors

3. **ProjectInitializationServiceTests**
   - Create config folder and file
   - Create all built-in doc-type folders
   - Derive project name from folder name
   - Handle already initialized projects
   - Create custom doc-type folders

4. **SwitchableProjectConfigurationProviderTests**
   - Switch between projects
   - Fire ConfigChanged events
   - Handle file watcher notifications
   - Clear project state

5. **ExternalDocsPathValidatorTests**
   - Resolve relative paths from repo root
   - Detect missing directories
   - Count matching files with glob patterns
   - Handle absolute paths

### Integration Tests

1. **Configuration round-trip**: Save and reload configuration
2. **Schema validation end-to-end**: Validate against Draft 2020-12 features
3. **File watcher integration**: Config changes trigger reload

---

## Acceptance Criteria

- [ ] Project configuration loads from `.csharp-compounding-docs/config.json`
- [ ] JSON Schema validation using Draft 2020-12 with JsonSchema.Net
- [ ] All configuration properties have sensible defaults
- [ ] First-time initialization creates folder structure and config
- [ ] Project name derived from folder name follows pattern `^[a-z][a-z0-9-]*$`
- [ ] Custom doc-types can reference schema files in `./csharp-compounding-docs/schemas/`
- [ ] External docs path validated at activation time
- [ ] Configuration hot-reload via `IOptionsMonitor` pattern
- [ ] Snake_case JSON serialization matches spec schema
- [ ] Embedded JSON Schema resource for config validation
- [ ] Unit test coverage > 80% for all services

---

## Dependencies

### NuGet Packages

```xml
<PackageReference Include="JsonSchema.Net" Version="8.0.5" />
<PackageReference Include="Yaml2JsonNode" Version="2.4.0" />
<PackageReference Include="Microsoft.Extensions.FileSystemGlobbing" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Options" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
```

---

## Notes

- The switchable configuration provider enables the MCP server to handle multiple projects within a single process lifetime
- Schema caching is essential for performance since doc-type schemas are validated on every document operation
- The configuration precedence order (tool param > project config > default) is enforced at the MCP tool level, not in these services
