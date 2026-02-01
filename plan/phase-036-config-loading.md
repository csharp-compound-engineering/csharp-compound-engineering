# Phase 036: Configuration Loading Service

> **Category**: MCP Server Core
> **Prerequisites**: Phase 008 (Global Configuration Structure), Phase 010 (Project Configuration System)
> **Estimated Effort**: Medium (6-10 hours)
> **Priority**: High (blocks project activation and runtime configuration)

---

## Objective

Implement a unified configuration loading service that orchestrates loading from global config (`~/.claude/.csharp-compounding-docs/`), project config (`.csharp-compounding-docs/config.json`), and runtime tool parameters with proper precedence rules. The service leverages `IOptionsMonitor` for dynamic configuration reload and integrates with the switchable configuration provider from Phase 010.

---

## Spec References

- [spec/configuration.md](../spec/configuration.md) - Configuration precedence and schema definitions
- [research/dotnet-runtime-configuration-loading-research.md](../research/dotnet-runtime-configuration-loading-research.md) - Runtime configuration patterns
- [research/ioptions-monitor-dynamic-paths.md](../research/ioptions-monitor-dynamic-paths.md) - Dynamic path switching with IOptionsMonitor

---

## Background

### Configuration Precedence

The MCP server requires a three-tier configuration precedence:

1. **Tool parameter** (highest priority) - Explicitly provided in MCP tool call
2. **Project config** - From `.csharp-compounding-docs/config.json`
3. **Built-in default** (lowest priority) - Hardcoded in MCP server

Example: If `retrieval.min_relevance_score` is `0.8` in project config, but `rag_query` is called with `min_relevance_score: 0.6`, the tool uses `0.6`.

### Dynamic Configuration Challenge

The MCP server starts as an stdio process with no project context. The project configuration path is discovered at runtime when `activate_project()` is called. This requires a custom switchable configuration provider that can:

1. Start with empty/default configuration
2. Switch to project configuration when activated
3. Support hot-reload when config files change
4. Notify consumers via `IOptionsMonitor.OnChange()`

---

## Deliverables

### 1. Configuration Loading Service Interface

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/Services/`

#### 1.1 IConfigurationLoadingService Interface

```csharp
namespace CSharpCompoundingDocs.Configuration;

/// <summary>
/// Unified service for loading and managing configuration from all sources
/// with proper precedence handling.
/// </summary>
public interface IConfigurationLoadingService
{
    /// <summary>
    /// Gets the currently active project configuration, or null if no project is active.
    /// </summary>
    ProjectConfig? ActiveProjectConfig { get; }

    /// <summary>
    /// Gets the global configuration (always available).
    /// </summary>
    GlobalSettings GlobalSettings { get; }

    /// <summary>
    /// Gets the Ollama configuration (always available).
    /// </summary>
    OllamaConfig OllamaConfig { get; }

    /// <summary>
    /// Activates a project and loads its configuration.
    /// </summary>
    /// <param name="projectPath">Absolute path to project root</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Loaded project configuration</returns>
    /// <exception cref="ConfigurationException">If project is not initialized</exception>
    /// <exception cref="ConfigurationValidationException">If config validation fails</exception>
    Task<ProjectConfig> ActivateProjectAsync(
        string projectPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates the current project, reverting to default configuration.
    /// </summary>
    void DeactivateProject();

    /// <summary>
    /// Gets an effective configuration value with precedence handling.
    /// </summary>
    /// <typeparam name="T">Value type</typeparam>
    /// <param name="toolParameter">Value from tool call (highest priority)</param>
    /// <param name="projectConfigValue">Value from project config (middle priority)</param>
    /// <param name="defaultValue">Built-in default (lowest priority)</param>
    /// <returns>The effective value based on precedence</returns>
    T GetEffectiveValue<T>(T? toolParameter, T? projectConfigValue, T defaultValue)
        where T : struct;

    /// <summary>
    /// Gets an effective string configuration value with precedence handling.
    /// </summary>
    string? GetEffectiveValue(string? toolParameter, string? projectConfigValue, string? defaultValue);

    /// <summary>
    /// Validates the current configuration state.
    /// </summary>
    /// <returns>Validation result with any errors</returns>
    ConfigurationValidationResult ValidateCurrentState();

    /// <summary>
    /// Event raised when configuration changes (project switch or file reload).
    /// </summary>
    event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
}
```

#### 1.2 ConfigurationChangedEventArgs

```csharp
/// <summary>
/// Event arguments for configuration change notifications.
/// </summary>
public sealed record ConfigurationChangedEventArgs
{
    /// <summary>
    /// The type of change that occurred.
    /// </summary>
    public required ConfigurationChangeType ChangeType { get; init; }

    /// <summary>
    /// The previous project configuration (if any).
    /// </summary>
    public ProjectConfig? PreviousProjectConfig { get; init; }

    /// <summary>
    /// The new project configuration (if any).
    /// </summary>
    public ProjectConfig? NewProjectConfig { get; init; }

    /// <summary>
    /// The project path that was activated/deactivated (if applicable).
    /// </summary>
    public string? ProjectPath { get; init; }
}

/// <summary>
/// Types of configuration changes.
/// </summary>
public enum ConfigurationChangeType
{
    /// <summary>
    /// A project was activated.
    /// </summary>
    ProjectActivated,

    /// <summary>
    /// The active project was deactivated.
    /// </summary>
    ProjectDeactivated,

    /// <summary>
    /// The active project's configuration file was modified.
    /// </summary>
    ProjectConfigReloaded,

    /// <summary>
    /// Global configuration was modified.
    /// </summary>
    GlobalConfigReloaded
}
```

---

### 2. Configuration Loading Service Implementation

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/Services/`

#### 2.1 ConfigurationLoadingService

```csharp
namespace CSharpCompoundingDocs.Configuration;

/// <summary>
/// Implements unified configuration loading with precedence handling
/// and dynamic reload support via IOptionsMonitor.
/// </summary>
public sealed class ConfigurationLoadingService : IConfigurationLoadingService, IDisposable
{
    private readonly IGlobalConfigurationService _globalConfigService;
    private readonly IProjectConfigurationService _projectConfigService;
    private readonly ISwitchableConfigurationProvider _switchableProvider;
    private readonly IOptionsMonitor<ProjectConfig> _projectOptionsMonitor;
    private readonly IOptionsMonitorCache<ProjectConfig> _optionsCache;
    private readonly ExternalDocsPathValidator _externalDocsValidator;
    private readonly ILogger<ConfigurationLoadingService> _logger;

    private readonly object _lock = new();
    private string? _activeProjectPath;
    private ProjectConfig? _activeProjectConfig;
    private IDisposable? _configChangeSubscription;

    // Cached global configs (loaded once at startup)
    private GlobalSettings? _globalSettings;
    private OllamaConfig? _ollamaConfig;

    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    public ConfigurationLoadingService(
        IGlobalConfigurationService globalConfigService,
        IProjectConfigurationService projectConfigService,
        ISwitchableConfigurationProvider switchableProvider,
        IOptionsMonitor<ProjectConfig> projectOptionsMonitor,
        IOptionsMonitorCache<ProjectConfig> optionsCache,
        ExternalDocsPathValidator externalDocsValidator,
        ILogger<ConfigurationLoadingService> logger)
    {
        _globalConfigService = globalConfigService;
        _projectConfigService = projectConfigService;
        _switchableProvider = switchableProvider;
        _projectOptionsMonitor = projectOptionsMonitor;
        _optionsCache = optionsCache;
        _externalDocsValidator = externalDocsValidator;
        _logger = logger;

        // Subscribe to project config changes for hot-reload
        _configChangeSubscription = _projectOptionsMonitor.OnChange(OnProjectConfigChanged);
    }

    public ProjectConfig? ActiveProjectConfig
    {
        get
        {
            lock (_lock)
            {
                return _activeProjectConfig;
            }
        }
    }

    public GlobalSettings GlobalSettings
    {
        get
        {
            if (_globalSettings is null)
            {
                throw new InvalidOperationException(
                    "Configuration service not initialized. Call EnsureInitializedAsync first.");
            }
            return _globalSettings;
        }
    }

    public OllamaConfig OllamaConfig
    {
        get
        {
            if (_ollamaConfig is null)
            {
                throw new InvalidOperationException(
                    "Configuration service not initialized. Call EnsureInitializedAsync first.");
            }
            return _ollamaConfig;
        }
    }

    /// <summary>
    /// Initializes global configuration. Called during host startup.
    /// </summary>
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing configuration loading service");

        // Ensure global config directory exists with defaults
        await _globalConfigService.EnsureInitializedAsync(cancellationToken);

        // Load global configs
        _globalSettings = await _globalConfigService.GetSettingsAsync(cancellationToken);
        _ollamaConfig = await _globalConfigService.GetOllamaConfigAsync(cancellationToken);

        _logger.LogInformation(
            "Global configuration loaded. Generation model: {Model}, GPU enabled: {GpuEnabled}",
            _ollamaConfig.GenerationModel,
            _ollamaConfig.Gpu.Enabled);
    }

    public async Task<ProjectConfig> ActivateProjectAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        var absolutePath = Path.GetFullPath(projectPath);

        _logger.LogInformation("Activating project at: {ProjectPath}", absolutePath);

        // Validate project is initialized
        if (!_projectConfigService.IsInitialized(absolutePath))
        {
            throw new ConfigurationException(
                $"Project not initialized at '{absolutePath}'. " +
                "Run activate_project with a project path containing .csharp-compounding-docs/config.json");
        }

        // Load and validate project configuration
        var config = await _projectConfigService.LoadAsync(absolutePath, cancellationToken);

        if (config is null)
        {
            throw new ConfigurationException(
                $"Failed to load project configuration from '{absolutePath}'");
        }

        // Validate external docs path if configured
        if (config.ExternalDocs is not null)
        {
            var validation = _externalDocsValidator.Validate(absolutePath, config.ExternalDocs);
            if (!validation.IsValid)
            {
                throw new ConfigurationValidationException(
                    _projectConfigService.GetConfigPath(absolutePath),
                    [new ValidationError("external_docs.path", "path", validation.Error!)]);
            }

            _logger.LogInformation(
                "External docs path validated: {Path} ({FileCount} matching files)",
                validation.ResolvedPath,
                validation.MatchingFileCount);
        }

        // Switch the configuration provider to the new project
        var configPath = _projectConfigService.GetConfigPath(absolutePath);
        _switchableProvider.SetPath(configPath);

        // Clear options cache to force refresh
        _optionsCache.Clear();

        lock (_lock)
        {
            var previousConfig = _activeProjectConfig;
            _activeProjectPath = absolutePath;
            _activeProjectConfig = config;

            // Raise change event
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
            {
                ChangeType = ConfigurationChangeType.ProjectActivated,
                PreviousProjectConfig = previousConfig,
                NewProjectConfig = config,
                ProjectPath = absolutePath
            });
        }

        _logger.LogInformation(
            "Project activated: {ProjectName} at {ProjectPath}",
            config.ProjectName,
            absolutePath);

        return config;
    }

    public void DeactivateProject()
    {
        lock (_lock)
        {
            if (_activeProjectPath is null)
            {
                _logger.LogDebug("No active project to deactivate");
                return;
            }

            _logger.LogInformation("Deactivating project: {ProjectPath}", _activeProjectPath);

            var previousConfig = _activeProjectConfig;
            var previousPath = _activeProjectPath;

            // Clear the switchable provider
            _switchableProvider.ClearPath();

            // Clear options cache
            _optionsCache.Clear();

            _activeProjectPath = null;
            _activeProjectConfig = null;

            // Raise change event
            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
            {
                ChangeType = ConfigurationChangeType.ProjectDeactivated,
                PreviousProjectConfig = previousConfig,
                NewProjectConfig = null,
                ProjectPath = previousPath
            });

            _logger.LogInformation("Project deactivated");
        }
    }

    public T GetEffectiveValue<T>(T? toolParameter, T? projectConfigValue, T defaultValue)
        where T : struct
    {
        // Tool parameter takes highest priority
        if (toolParameter.HasValue)
        {
            return toolParameter.Value;
        }

        // Project config takes middle priority
        if (projectConfigValue.HasValue)
        {
            return projectConfigValue.Value;
        }

        // Fall back to built-in default
        return defaultValue;
    }

    public string? GetEffectiveValue(
        string? toolParameter,
        string? projectConfigValue,
        string? defaultValue)
    {
        // Tool parameter takes highest priority (if not null/whitespace)
        if (!string.IsNullOrWhiteSpace(toolParameter))
        {
            return toolParameter;
        }

        // Project config takes middle priority
        if (!string.IsNullOrWhiteSpace(projectConfigValue))
        {
            return projectConfigValue;
        }

        // Fall back to built-in default
        return defaultValue;
    }

    public ConfigurationValidationResult ValidateCurrentState()
    {
        var errors = new List<ValidationError>();

        // Validate global configs exist
        if (_globalSettings is null)
        {
            errors.Add(new ValidationError("global", "settings", "Global settings not loaded"));
        }

        if (_ollamaConfig is null)
        {
            errors.Add(new ValidationError("global", "ollama", "Ollama configuration not loaded"));
        }

        // Validate active project config if present
        if (_activeProjectConfig is not null && _activeProjectPath is not null)
        {
            var projectValidation = _projectConfigService.Validate(_activeProjectConfig);
            if (!projectValidation.IsValid)
            {
                errors.AddRange(projectValidation.Errors);
            }
        }

        return errors.Count == 0
            ? ConfigurationValidationResult.Success()
            : ConfigurationValidationResult.Failed(errors);
    }

    private void OnProjectConfigChanged(ProjectConfig newConfig, string? name)
    {
        lock (_lock)
        {
            if (_activeProjectPath is null)
            {
                return;
            }

            _logger.LogInformation(
                "Project configuration reloaded via file change: {ProjectName}",
                newConfig.ProjectName);

            var previousConfig = _activeProjectConfig;
            _activeProjectConfig = newConfig;

            ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
            {
                ChangeType = ConfigurationChangeType.ProjectConfigReloaded,
                PreviousProjectConfig = previousConfig,
                NewProjectConfig = newConfig,
                ProjectPath = _activeProjectPath
            });
        }
    }

    public void Dispose()
    {
        _configChangeSubscription?.Dispose();
    }
}
```

---

### 3. Switchable Configuration Provider Enhancement

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/Providers/`

Enhance the provider from Phase 010 with proper IOptions integration.

#### 3.1 Enhanced ISwitchableConfigurationProvider Interface

```csharp
/// <summary>
/// Configuration provider that supports switching between project configurations
/// at runtime with proper IOptionsMonitor integration.
/// </summary>
public interface ISwitchableConfigurationProvider
{
    /// <summary>
    /// Sets the path to the configuration file and triggers reload.
    /// </summary>
    /// <param name="filePath">Absolute path to config.json</param>
    void SetPath(string filePath);

    /// <summary>
    /// Clears the current path, reverting to empty/default configuration.
    /// </summary>
    void ClearPath();

    /// <summary>
    /// Gets the current configuration file path, or null if not set.
    /// </summary>
    string? CurrentPath { get; }

    /// <summary>
    /// Gets whether a configuration file is currently loaded.
    /// </summary>
    bool HasActiveConfiguration { get; }

    /// <summary>
    /// Event raised when the configuration source changes (path switch or file modification).
    /// </summary>
    event EventHandler<ConfigurationSourceChangedEventArgs>? SourceChanged;
}

/// <summary>
/// Event arguments for configuration source changes.
/// </summary>
public sealed record ConfigurationSourceChangedEventArgs
{
    public required string? PreviousPath { get; init; }
    public required string? NewPath { get; init; }
    public required ConfigurationSourceChangeReason Reason { get; init; }
}

/// <summary>
/// Reasons for configuration source changes.
/// </summary>
public enum ConfigurationSourceChangeReason
{
    PathSet,
    PathCleared,
    FileModified
}
```

#### 3.2 SwitchableJsonConfigurationProvider Implementation

```csharp
/// <summary>
/// JSON configuration provider that supports runtime path switching
/// with FileSystemWatcher for hot-reload.
/// </summary>
public sealed class SwitchableJsonConfigurationProvider :
    ConfigurationProvider,
    ISwitchableConfigurationProvider,
    IDisposable
{
    private readonly object _lock = new();
    private readonly bool _reloadOnChange;
    private readonly int _debounceMs;
    private readonly ILogger<SwitchableJsonConfigurationProvider> _logger;

    private string? _filePath;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounceCts;
    private bool _disposed;

    public event EventHandler<ConfigurationSourceChangedEventArgs>? SourceChanged;

    public SwitchableJsonConfigurationProvider(
        bool reloadOnChange = true,
        int debounceMs = 500,
        ILogger<SwitchableJsonConfigurationProvider>? logger = null)
    {
        _reloadOnChange = reloadOnChange;
        _debounceMs = debounceMs;
        _logger = logger ?? NullLogger<SwitchableJsonConfigurationProvider>.Instance;
    }

    public string? CurrentPath
    {
        get
        {
            lock (_lock)
            {
                return _filePath;
            }
        }
    }

    public bool HasActiveConfiguration
    {
        get
        {
            lock (_lock)
            {
                return _filePath is not null && Data.Count > 0;
            }
        }
    }

    public void SetPath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        lock (_lock)
        {
            var previousPath = _filePath;

            // Clean up previous watcher
            DisposeWatcher();

            _filePath = filePath;

            // Load the new configuration
            Load();

            // Set up file watching if enabled
            if (_reloadOnChange && File.Exists(filePath))
            {
                SetupWatcher(filePath);
            }

            // Signal that configuration has changed
            OnReload();

            // Raise source changed event
            SourceChanged?.Invoke(this, new ConfigurationSourceChangedEventArgs
            {
                PreviousPath = previousPath,
                NewPath = filePath,
                Reason = ConfigurationSourceChangeReason.PathSet
            });

            _logger.LogDebug("Configuration path set to: {Path}", filePath);
        }
    }

    public void ClearPath()
    {
        lock (_lock)
        {
            var previousPath = _filePath;

            DisposeWatcher();
            _filePath = null;
            Data.Clear();
            OnReload();

            SourceChanged?.Invoke(this, new ConfigurationSourceChangedEventArgs
            {
                PreviousPath = previousPath,
                NewPath = null,
                Reason = ConfigurationSourceChangeReason.PathCleared
            });

            _logger.LogDebug("Configuration path cleared");
        }
    }

    public override void Load()
    {
        Data.Clear();

        if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
        {
            _logger.LogDebug("No configuration file to load (path: {Path})", _filePath ?? "(null)");
            return;
        }

        try
        {
            using var stream = File.OpenRead(_filePath);
            using var doc = JsonDocument.Parse(stream);

            foreach (var kvp in FlattenJson(doc.RootElement, string.Empty))
            {
                Data[kvp.Key] = kvp.Value;
            }

            _logger.LogDebug(
                "Loaded {Count} configuration values from {Path}",
                Data.Count,
                _filePath);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parse error loading config from {Path}", _filePath);
            throw new ConfigurationException(
                $"Invalid JSON in configuration file: {_filePath}", ex);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error loading config from {Path}", _filePath);
            // Don't throw - leave empty config, file might be temporarily locked
        }
    }

    private void SetupWatcher(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);

        if (string.IsNullOrEmpty(directory))
        {
            _logger.LogWarning("Cannot set up file watcher - no directory for {Path}", filePath);
            return;
        }

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Size
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.EnableRaisingEvents = true;

        _logger.LogDebug("File watcher set up for: {Path}", filePath);
    }

    private async void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce file change events
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(_debounceMs, _debounceCts.Token);

            lock (_lock)
            {
                if (_filePath is null || _disposed)
                {
                    return;
                }

                _logger.LogInformation("Configuration file changed, reloading: {Path}", _filePath);

                Load();
                OnReload();

                SourceChanged?.Invoke(this, new ConfigurationSourceChangedEventArgs
                {
                    PreviousPath = _filePath,
                    NewPath = _filePath,
                    Reason = ConfigurationSourceChangeReason.FileModified
                });
            }
        }
        catch (TaskCanceledException)
        {
            // Debounce cancelled, another change came in
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload configuration after file change");
        }
    }

    private void DisposeWatcher()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileChanged;
            _watcher.Created -= OnFileChanged;
            _watcher.Dispose();
            _watcher = null;
        }

        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        _debounceCts = null;
    }

    private static IEnumerable<KeyValuePair<string, string?>> FlattenJson(
        JsonElement element,
        string prefix)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix)
                        ? property.Name
                        : $"{prefix}:{property.Name}";

                    foreach (var kvp in FlattenJson(property.Value, key))
                    {
                        yield return kvp;
                    }
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var key = $"{prefix}:{index}";
                    foreach (var kvp in FlattenJson(item, key))
                    {
                        yield return kvp;
                    }
                    index++;
                }
                break;

            case JsonValueKind.Null:
                yield return new KeyValuePair<string, string?>(prefix, null);
                break;

            default:
                yield return new KeyValuePair<string, string?>(prefix, element.ToString());
                break;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            DisposeWatcher();
            _disposed = true;
        }
    }
}
```

---

### 4. Configuration Source Registration

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/Providers/`

#### 4.1 SwitchableJsonConfigurationSource

```csharp
/// <summary>
/// Configuration source for the switchable JSON provider.
/// </summary>
public sealed class SwitchableJsonConfigurationSource : IConfigurationSource
{
    private readonly SwitchableJsonConfigurationProvider _provider;

    public SwitchableJsonConfigurationSource(SwitchableJsonConfigurationProvider provider)
    {
        _provider = provider;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return _provider;
    }
}
```

---

### 5. Effective Configuration Resolvers

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/Resolvers/`

#### 5.1 RetrievalConfigResolver

Provides resolved retrieval settings with precedence handling.

```csharp
/// <summary>
/// Resolves effective retrieval configuration from all sources.
/// </summary>
public interface IRetrievalConfigResolver
{
    /// <summary>
    /// Gets effective min_relevance_score with precedence handling.
    /// </summary>
    double GetMinRelevanceScore(double? toolParameter);

    /// <summary>
    /// Gets effective max_results with precedence handling.
    /// </summary>
    int GetMaxResults(int? toolParameter);

    /// <summary>
    /// Gets effective max_linked_docs with precedence handling.
    /// </summary>
    int GetMaxLinkedDocs(int? toolParameter);

    /// <summary>
    /// Gets effective link resolution max_depth with precedence handling.
    /// </summary>
    int GetLinkResolutionMaxDepth(int? toolParameter);
}

public sealed class RetrievalConfigResolver : IRetrievalConfigResolver
{
    // Built-in defaults (hardcoded in MCP server)
    private const double DefaultMinRelevanceScore = 0.7;
    private const int DefaultMaxResults = 3;
    private const int DefaultMaxLinkedDocs = 5;
    private const int DefaultMaxDepth = 2;

    private readonly IConfigurationLoadingService _configService;

    public RetrievalConfigResolver(IConfigurationLoadingService configService)
    {
        _configService = configService;
    }

    public double GetMinRelevanceScore(double? toolParameter)
    {
        var projectValue = _configService.ActiveProjectConfig?.Retrieval.MinRelevanceScore;
        return _configService.GetEffectiveValue(toolParameter, projectValue, DefaultMinRelevanceScore);
    }

    public int GetMaxResults(int? toolParameter)
    {
        var projectValue = _configService.ActiveProjectConfig?.Retrieval.MaxResults;
        return _configService.GetEffectiveValue(toolParameter, projectValue, DefaultMaxResults);
    }

    public int GetMaxLinkedDocs(int? toolParameter)
    {
        var projectValue = _configService.ActiveProjectConfig?.Retrieval.MaxLinkedDocs;
        return _configService.GetEffectiveValue(toolParameter, projectValue, DefaultMaxLinkedDocs);
    }

    public int GetLinkResolutionMaxDepth(int? toolParameter)
    {
        var projectValue = _configService.ActiveProjectConfig?.LinkResolution.MaxDepth;
        return _configService.GetEffectiveValue(toolParameter, projectValue, DefaultMaxDepth);
    }
}
```

#### 5.2 SemanticSearchConfigResolver

```csharp
/// <summary>
/// Resolves effective semantic search configuration from all sources.
/// </summary>
public interface ISemanticSearchConfigResolver
{
    /// <summary>
    /// Gets effective min_relevance_score for semantic search.
    /// </summary>
    double GetMinRelevanceScore(double? toolParameter);

    /// <summary>
    /// Gets effective result limit for semantic search.
    /// </summary>
    int GetDefaultLimit(int? toolParameter);
}

public sealed class SemanticSearchConfigResolver : ISemanticSearchConfigResolver
{
    // Built-in defaults
    private const double DefaultMinRelevanceScore = 0.5;
    private const int DefaultLimit = 10;

    private readonly IConfigurationLoadingService _configService;

    public SemanticSearchConfigResolver(IConfigurationLoadingService configService)
    {
        _configService = configService;
    }

    public double GetMinRelevanceScore(double? toolParameter)
    {
        var projectValue = _configService.ActiveProjectConfig?.SemanticSearch.MinRelevanceScore;
        return _configService.GetEffectiveValue(toolParameter, projectValue, DefaultMinRelevanceScore);
    }

    public int GetDefaultLimit(int? toolParameter)
    {
        var projectValue = _configService.ActiveProjectConfig?.SemanticSearch.DefaultLimit;
        return _configService.GetEffectiveValue(toolParameter, projectValue, DefaultLimit);
    }
}
```

---

### 6. Dependency Injection Registration

**Location**: `src/CSharpCompoundingDocs.Core/Configuration/ServiceCollectionExtensions.cs`

#### 6.1 Extension Method Updates

```csharp
public static class ConfigurationServiceCollectionExtensions
{
    /// <summary>
    /// Adds configuration loading services with switchable project configuration support.
    /// </summary>
    public static IServiceCollection AddConfigurationLoading(
        this IServiceCollection services,
        IConfigurationManager configuration)
    {
        // Create and register the switchable provider
        var switchableProvider = new SwitchableJsonConfigurationProvider(
            reloadOnChange: true,
            debounceMs: 500);

        // Add switchable provider to configuration system
        ((IConfigurationBuilder)configuration).Add(
            new SwitchableJsonConfigurationSource(switchableProvider));

        // Register provider as singleton for injection
        services.AddSingleton<ISwitchableConfigurationProvider>(switchableProvider);
        services.AddSingleton(switchableProvider);

        // Bind project configuration options (section name matches JSON structure)
        services.Configure<ProjectConfig>(configuration);

        // Register core configuration services (from Phase 008 and 010)
        services.AddSingleton<IGlobalConfigurationService, GlobalConfigurationService>();
        services.AddSingleton<IProjectConfigurationService, ProjectConfigurationService>();
        services.AddSingleton<ISchemaValidationService, SchemaValidationService>();
        services.AddSingleton<ExternalDocsPathValidator>();

        // Register the unified configuration loading service
        services.AddSingleton<IConfigurationLoadingService, ConfigurationLoadingService>();

        // Register configuration resolvers
        services.AddSingleton<IRetrievalConfigResolver, RetrievalConfigResolver>();
        services.AddSingleton<ISemanticSearchConfigResolver, SemanticSearchConfigResolver>();

        return services;
    }
}
```

---

### 7. Host Initialization Integration

**Location**: `src/CSharpCompoundingDocs.McpServer/Program.cs`

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Add configuration loading with switchable project support
builder.Services.AddConfigurationLoading(builder.Configuration);

// ... other service registrations ...

var host = builder.Build();

// Initialize configuration before starting the host
var configService = host.Services.GetRequiredService<IConfigurationLoadingService>();
await ((ConfigurationLoadingService)configService).EnsureInitializedAsync();

await host.RunAsync();
```

---

## Validation Requirements

### Configuration Validation on Load

Configuration is validated at multiple points:

1. **Global config on startup** - Via `GlobalConfigurationService.EnsureInitializedAsync()`
2. **Project config on activation** - Via `ProjectConfigurationService.LoadAsync()` with schema validation
3. **External docs path on activation** - Via `ExternalDocsPathValidator.Validate()`
4. **Runtime validation** - Via `ConfigurationLoadingService.ValidateCurrentState()`

### Validation Error Handling

```csharp
public sealed record ConfigurationValidationResult
{
    public bool IsValid { get; init; }
    public IReadOnlyList<ValidationError> Errors { get; init; } = [];

    public static ConfigurationValidationResult Success() =>
        new() { IsValid = true };

    public static ConfigurationValidationResult Failed(IEnumerable<ValidationError> errors) =>
        new() { IsValid = false, Errors = errors.ToList() };
}

public sealed record ValidationError(
    string Path,
    string Code,
    string Message);
```

---

## Testing Requirements

### Unit Tests

1. **ConfigurationLoadingServiceTests**
   - Initialize with global config
   - Activate project successfully
   - Handle missing project config
   - Handle invalid project config (validation errors)
   - Deactivate project clears state
   - Precedence: tool parameter > project config > default
   - Configuration change events fired correctly
   - Validate external docs path on activation

2. **SwitchableJsonConfigurationProviderTests**
   - Set path loads configuration
   - Clear path empties configuration
   - File modification triggers reload
   - Debounce multiple rapid file changes
   - Handle missing file gracefully
   - Handle invalid JSON with clear error

3. **RetrievalConfigResolverTests**
   - Tool parameter takes highest priority
   - Project config used when tool parameter is null
   - Default used when both are null
   - All retrieval settings resolved correctly

4. **SemanticSearchConfigResolverTests**
   - Tool parameter takes highest priority
   - Project config used when tool parameter is null
   - Default used when both are null

### Integration Tests

1. **Configuration flow end-to-end**
   - Start with no project
   - Activate project
   - Modify project config file
   - Verify hot-reload fires change event
   - Deactivate project

2. **Precedence verification**
   - Set project config value
   - Call tool with explicit parameter
   - Verify tool parameter wins

---

## File Structure

```
src/CSharpCompoundingDocs.Core/
├── Configuration/
│   ├── Providers/
│   │   ├── ISwitchableConfigurationProvider.cs
│   │   ├── SwitchableJsonConfigurationProvider.cs
│   │   └── SwitchableJsonConfigurationSource.cs
│   ├── Resolvers/
│   │   ├── IRetrievalConfigResolver.cs
│   │   ├── RetrievalConfigResolver.cs
│   │   ├── ISemanticSearchConfigResolver.cs
│   │   └── SemanticSearchConfigResolver.cs
│   ├── Services/
│   │   ├── IConfigurationLoadingService.cs
│   │   └── ConfigurationLoadingService.cs
│   ├── Events/
│   │   ├── ConfigurationChangedEventArgs.cs
│   │   └── ConfigurationSourceChangedEventArgs.cs
│   └── ServiceCollectionExtensions.cs
```

---

## Acceptance Criteria

- [ ] Global config loaded from `~/.claude/.csharp-compounding-docs/` on startup
- [ ] Project config loaded from `.csharp-compounding-docs/config.json` on activation
- [ ] Configuration precedence enforced: tool parameter > project > default
- [ ] `IOptionsMonitor<ProjectConfig>` receives change notifications on file modification
- [ ] File watcher debounces rapid changes (500ms default from global settings)
- [ ] Configuration validated on load with clear error messages
- [ ] External docs path validated at activation time
- [ ] Configuration change events raised for all change types
- [ ] Deactivation clears project config and reverts to defaults
- [ ] Resolvers provide typed access to effective configuration values
- [ ] Unit test coverage > 85% for all configuration services
- [ ] Integration tests verify hot-reload behavior

---

## Dependencies

### NuGet Packages

```xml
<PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
<PackageReference Include="JsonSchema.Net" Version="8.0.5" />
```

### Phase Dependencies

- **Phase 008**: Global configuration models (`OllamaConfig`, `GlobalSettings`)
- **Phase 010**: Project configuration models (`ProjectConfig`, `RetrievalSettings`, etc.)
- **Phase 017**: DI container setup (extension method patterns)

---

## Notes

- The switchable configuration provider is a singleton that persists across tool calls
- IOptionsMonitor automatically handles cache invalidation when `OnReload()` is called
- The debounce value is read from `GlobalSettings.FileWatcher.DebounceMs`
- Configuration resolvers should be used by MCP tool handlers, not direct config access
- Thread safety is critical as configuration can change during tool execution
