using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using CompoundDocs.Common.Configuration;
using CompoundDocs.McpServer.Session;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// MCP tool for viewing and updating configuration settings.
/// Supports both global and project-level configuration management.
/// </summary>
[McpServerToolType]
public sealed class ConfigManagementTool
{
    private readonly ConfigurationLoader _configurationLoader;
    private readonly ISessionContext _sessionContext;
    private readonly ILogger<ConfigManagementTool> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Creates a new instance of ConfigManagementTool.
    /// </summary>
    public ConfigManagementTool(
        ConfigurationLoader configurationLoader,
        ISessionContext sessionContext,
        ILogger<ConfigManagementTool> logger)
    {
        _configurationLoader = configurationLoader ?? throw new ArgumentNullException(nameof(configurationLoader));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the current configuration settings.
    /// </summary>
    /// <param name="scope">The configuration scope: 'global', 'project', or 'all' (default: 'all').</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current configuration settings.</returns>
    [McpServerTool(Name = "get_config")]
    [Description("Get the current configuration settings. Returns global, project, or both configurations.")]
    public Task<ToolResponse<GetConfigResult>> GetConfigAsync(
        [Description("Configuration scope: 'global', 'project', or 'all' (default: 'all')")] string scope = "all",
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting configuration with scope: {Scope}", scope);

        try
        {
            var normalizedScope = scope.ToLowerInvariant().Trim();

            if (normalizedScope is not ("global" or "project" or "all"))
            {
                return Task.FromResult(ToolResponse<GetConfigResult>.Fail(
                    ToolErrors.InvalidParameter("scope", "Must be 'global', 'project', or 'all'")));
            }

            GlobalConfigInfo? globalConfig = null;
            ProjectConfigInfo? projectConfig = null;

            // Load global config
            if (normalizedScope is "global" or "all")
            {
                var global = _configurationLoader.LoadGlobalConfig();
                globalConfig = new GlobalConfigInfo
                {
                    ConfigDirectory = global.ConfigDirectory,
                    Postgres = new PostgresConfigInfo
                    {
                        Host = global.Postgres.Host,
                        Port = global.Postgres.Port,
                        Database = global.Postgres.Database,
                        Username = global.Postgres.Username,
                        PasswordSet = !string.IsNullOrEmpty(global.Postgres.Password)
                    },
                    Ollama = new OllamaConfigInfo
                    {
                        Host = global.Ollama.Host,
                        Port = global.Ollama.Port,
                        GenerationModel = global.Ollama.GenerationModel,
                        EmbeddingModel = OllamaSettings.EmbeddingModel,
                        EmbeddingDimensions = OllamaSettings.EmbeddingDimensions
                    }
                };
            }

            // Load project config
            if (normalizedScope is "project" or "all")
            {
                if (!_sessionContext.IsProjectActive)
                {
                    if (normalizedScope == "project")
                    {
                        return Task.FromResult(ToolResponse<GetConfigResult>.Fail(ToolErrors.NoActiveProject));
                    }
                    // For 'all' scope, just skip project config if no project is active
                }
                else
                {
                    var project = _configurationLoader.LoadProjectConfig(_sessionContext.ActiveProjectPath!);
                    projectConfig = new ProjectConfigInfo
                    {
                        ProjectName = project.ProjectName,
                        ProjectPath = _sessionContext.ActiveProjectPath!,
                        Rag = new RagConfigInfo
                        {
                            RelevanceThreshold = project.Rag.SimilarityThreshold,
                            MaxResults = project.Rag.MaxResults,
                            MaxLinkedDocs = project.Rag.LinkDepth
                        },
                        LinkResolution = new LinkResolutionConfigInfo
                        {
                            MaxDepth = project.LinkResolution.MaxDepth,
                            MaxLinkedDocs = project.LinkResolution.MaxLinkedDocs
                        },
                        FileWatcher = new FileWatcherConfigInfo
                        {
                            DebounceMs = project.FileWatcher.DebounceMs,
                            IncludePatterns = project.FileWatcher.IncludePatterns,
                            ExcludePatterns = project.FileWatcher.ExcludePatterns
                        },
                        ExternalDocsCount = project.ExternalDocs.Count,
                        CustomDocTypesCount = project.CustomDocTypes.Count
                    };
                }
            }

            _logger.LogDebug("Configuration retrieved successfully for scope: {Scope}", scope);

            return Task.FromResult(ToolResponse<GetConfigResult>.Ok(new GetConfigResult
            {
                Scope = normalizedScope,
                Global = globalConfig,
                Project = projectConfig,
                EnvironmentVariables = GetEnvironmentVariableStatus()
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error getting configuration");
            return Task.FromResult(ToolResponse<GetConfigResult>.Fail(
                ToolErrors.UnexpectedError(ex.Message)));
        }
    }

    /// <summary>
    /// Updates a configuration setting.
    /// </summary>
    /// <param name="scope">The configuration scope: 'global' or 'project'.</param>
    /// <param name="setting">The setting path (e.g., 'postgres.host', 'rag.maxResults').</param>
    /// <param name="value">The new value for the setting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the update operation.</returns>
    [McpServerTool(Name = "set_config")]
    [Description("Update a configuration setting. Use dot notation for nested settings (e.g., 'postgres.host', 'rag.maxResults').")]
    public async Task<ToolResponse<SetConfigResult>> SetConfigAsync(
        [Description("Configuration scope: 'global' or 'project'")] string scope,
        [Description("Setting path using dot notation (e.g., 'postgres.host', 'rag.maxResults')")] string setting,
        [Description("The new value for the setting")] string value,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return ToolResponse<SetConfigResult>.Fail(
                ToolErrors.MissingParameter("scope"));
        }

        if (string.IsNullOrWhiteSpace(setting))
        {
            return ToolResponse<SetConfigResult>.Fail(
                ToolErrors.MissingParameter("setting"));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return ToolResponse<SetConfigResult>.Fail(
                ToolErrors.MissingParameter("value"));
        }

        var normalizedScope = scope.ToLowerInvariant().Trim();
        var normalizedSetting = setting.ToLowerInvariant().Trim();

        _logger.LogInformation(
            "Setting configuration: scope={Scope}, setting={Setting}, value={Value}",
            normalizedScope,
            normalizedSetting,
            MaskSensitiveValue(normalizedSetting, value));

        try
        {
            if (normalizedScope == "global")
            {
                return await SetGlobalConfigAsync(normalizedSetting, value, cancellationToken);
            }
            else if (normalizedScope == "project")
            {
                return await SetProjectConfigAsync(normalizedSetting, value, cancellationToken);
            }
            else
            {
                return ToolResponse<SetConfigResult>.Fail(
                    ToolErrors.InvalidParameter("scope", "Must be 'global' or 'project'"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error setting configuration");
            return ToolResponse<SetConfigResult>.Fail(
                ToolErrors.UnexpectedError(ex.Message));
        }
    }

    /// <summary>
    /// Resets configuration to default values.
    /// </summary>
    /// <param name="scope">The configuration scope: 'global' or 'project'.</param>
    /// <param name="confirm">Must be 'yes' to confirm the reset operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the reset operation.</returns>
    [McpServerTool(Name = "reset_config")]
    [Description("Reset configuration to default values. Requires confirmation with 'yes'.")]
    public async Task<ToolResponse<ResetConfigResult>> ResetConfigAsync(
        [Description("Configuration scope: 'global' or 'project'")] string scope,
        [Description("Confirmation: must be 'yes' to proceed with reset")] string confirm,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return ToolResponse<ResetConfigResult>.Fail(
                ToolErrors.MissingParameter("scope"));
        }

        var normalizedScope = scope.ToLowerInvariant().Trim();
        var normalizedConfirm = confirm?.ToLowerInvariant().Trim();

        if (normalizedConfirm != "yes")
        {
            return ToolResponse<ResetConfigResult>.Fail(
                ToolErrors.InvalidParameter("confirm", "Must be 'yes' to confirm reset operation"));
        }

        _logger.LogInformation("Resetting configuration for scope: {Scope}", normalizedScope);

        try
        {
            if (normalizedScope == "global")
            {
                return await ResetGlobalConfigAsync(cancellationToken);
            }
            else if (normalizedScope == "project")
            {
                return await ResetProjectConfigAsync(cancellationToken);
            }
            else
            {
                return ToolResponse<ResetConfigResult>.Fail(
                    ToolErrors.InvalidParameter("scope", "Must be 'global' or 'project'"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error resetting configuration");
            return ToolResponse<ResetConfigResult>.Fail(
                ToolErrors.UnexpectedError(ex.Message));
        }
    }

    #region Private Methods - Global Config

    private Task<ToolResponse<SetConfigResult>> SetGlobalConfigAsync(
        string setting,
        string value,
        CancellationToken cancellationToken)
    {
        var config = _configurationLoader.LoadGlobalConfig();
        var previousValue = GetGlobalSettingValue(config, setting);

        // Validate and apply setting
        var validationError = ValidateAndApplyGlobalSetting(config, setting, value);
        if (validationError != null)
        {
            return Task.FromResult(ToolResponse<SetConfigResult>.Fail(validationError));
        }

        // Save the configuration
        SaveGlobalConfig(config);

        _logger.LogInformation(
            "Global configuration updated: {Setting} changed from {PreviousValue} to {NewValue}",
            setting,
            MaskSensitiveValue(setting, previousValue),
            MaskSensitiveValue(setting, value));

        return Task.FromResult(ToolResponse<SetConfigResult>.Ok(new SetConfigResult
        {
            Scope = "global",
            Setting = setting,
            PreviousValue = MaskSensitiveValue(setting, previousValue),
            NewValue = MaskSensitiveValue(setting, value),
            Message = $"Successfully updated global setting '{setting}'"
        }));
    }

    private ToolError? ValidateAndApplyGlobalSetting(GlobalConfig config, string setting, string value)
    {
        return setting switch
        {
            "postgres.host" => ApplySetting(() => config.Postgres.Host = value),
            "postgres.port" => ApplyIntSetting(value, 1, 65535, v => config.Postgres.Port = v, "Port must be between 1 and 65535"),
            "postgres.database" => ApplySetting(() => config.Postgres.Database = value),
            "postgres.username" => ApplySetting(() => config.Postgres.Username = value),
            "postgres.password" => ApplySetting(() => config.Postgres.Password = value),
            "ollama.host" => ApplySetting(() => config.Ollama.Host = value),
            "ollama.port" => ApplyIntSetting(value, 1, 65535, v => config.Ollama.Port = v, "Port must be between 1 and 65535"),
            "ollama.generationmodel" or "ollama.model" => ApplySetting(() => config.Ollama.GenerationModel = value),
            _ => ToolErrors.InvalidParameter("setting", $"Unknown global setting: '{setting}'. Valid settings: postgres.host, postgres.port, postgres.database, postgres.username, postgres.password, ollama.host, ollama.port, ollama.generationModel")
        };
    }

    private static string GetGlobalSettingValue(GlobalConfig config, string setting)
    {
        return setting switch
        {
            "postgres.host" => config.Postgres.Host,
            "postgres.port" => config.Postgres.Port.ToString(),
            "postgres.database" => config.Postgres.Database,
            "postgres.username" => config.Postgres.Username,
            "postgres.password" => config.Postgres.Password,
            "ollama.host" => config.Ollama.Host,
            "ollama.port" => config.Ollama.Port.ToString(),
            "ollama.generationmodel" or "ollama.model" => config.Ollama.GenerationModel,
            _ => "unknown"
        };
    }

    private void SaveGlobalConfig(GlobalConfig config)
    {
        var configFile = Path.Combine(config.ConfigDirectory, "global-config.json");
        Directory.CreateDirectory(config.ConfigDirectory);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(configFile, json);
    }

    private Task<ToolResponse<ResetConfigResult>> ResetGlobalConfigAsync(CancellationToken cancellationToken)
    {
        var defaultConfig = new GlobalConfig();
        SaveGlobalConfig(defaultConfig);

        _logger.LogInformation("Global configuration reset to defaults");

        return Task.FromResult(ToolResponse<ResetConfigResult>.Ok(new ResetConfigResult
        {
            Scope = "global",
            Message = "Global configuration has been reset to default values",
            ResetSettings =
            [
                "postgres.host",
                "postgres.port",
                "postgres.database",
                "postgres.username",
                "postgres.password",
                "ollama.host",
                "ollama.port",
                "ollama.generationModel"
            ]
        }));
    }

    #endregion

    #region Private Methods - Project Config

    private Task<ToolResponse<SetConfigResult>> SetProjectConfigAsync(
        string setting,
        string value,
        CancellationToken cancellationToken)
    {
        if (!_sessionContext.IsProjectActive)
        {
            return Task.FromResult(ToolResponse<SetConfigResult>.Fail(ToolErrors.NoActiveProject));
        }

        var projectPath = _sessionContext.ActiveProjectPath!;
        var config = _configurationLoader.LoadProjectConfig(projectPath);
        var previousValue = GetProjectSettingValue(config, setting);

        // Validate and apply setting
        var validationError = ValidateAndApplyProjectSetting(config, setting, value);
        if (validationError != null)
        {
            return Task.FromResult(ToolResponse<SetConfigResult>.Fail(validationError));
        }

        // Save the configuration
        _configurationLoader.SaveProjectConfig(projectPath, config);

        _logger.LogInformation(
            "Project configuration updated: {Setting} changed from {PreviousValue} to {NewValue}",
            setting,
            previousValue,
            value);

        return Task.FromResult(ToolResponse<SetConfigResult>.Ok(new SetConfigResult
        {
            Scope = "project",
            Setting = setting,
            PreviousValue = previousValue,
            NewValue = value,
            Message = $"Successfully updated project setting '{setting}'"
        }));
    }

    private ToolError? ValidateAndApplyProjectSetting(ProjectConfig config, string setting, string value)
    {
        return setting switch
        {
            "projectname" => ApplySetting(() => config.ProjectName = value),
            "rag.similaritythreshold" or "rag.relevancethreshold" => ApplyFloatSetting(value, 0.0f, 1.0f, v => config.Rag.SimilarityThreshold = v, "Similarity threshold must be between 0.0 and 1.0"),
            "rag.chunksize" => ApplyIntSetting(value, 100, 10000, v => config.Rag.ChunkSize = v, "Chunk size must be between 100 and 10000"),
            "rag.chunkoverlap" => ApplyIntSetting(value, 0, 1000, v => config.Rag.ChunkOverlap = v, "Chunk overlap must be between 0 and 1000"),
            "rag.maxresults" => ApplyIntSetting(value, 1, 100, v => config.Rag.MaxResults = v, "Max results must be between 1 and 100"),
            "rag.linkdepth" or "rag.maxlinkeddocs" => ApplyIntSetting(value, 0, 50, v => config.Rag.LinkDepth = v, "Link depth must be between 0 and 50"),
            "linkresolution.maxdepth" => ApplyIntSetting(value, 0, 10, v => config.LinkResolution.MaxDepth = v, "Max depth must be between 0 and 10"),
            "linkresolution.maxlinkeddocs" => ApplyIntSetting(value, 0, 50, v => config.LinkResolution.MaxLinkedDocs = v, "Max linked docs must be between 0 and 50"),
            "filewatcher.debouncems" => ApplyIntSetting(value, 100, 10000, v => config.FileWatcher.DebounceMs = v, "Debounce must be between 100 and 10000 milliseconds"),
            _ => ToolErrors.InvalidParameter("setting", $"Unknown project setting: '{setting}'. Valid settings: projectName, rag.chunkSize, rag.chunkOverlap, rag.maxResults, rag.similarityThreshold, rag.linkDepth, linkResolution.maxDepth, linkResolution.maxLinkedDocs, fileWatcher.debounceMs")
        };
    }

    private static string GetProjectSettingValue(ProjectConfig config, string setting)
    {
        return setting switch
        {
            "projectname" => config.ProjectName ?? "",
            "rag.similaritythreshold" or "rag.relevancethreshold" => config.Rag.SimilarityThreshold.ToString("F2"),
            "rag.chunksize" => config.Rag.ChunkSize.ToString(),
            "rag.chunkoverlap" => config.Rag.ChunkOverlap.ToString(),
            "rag.maxresults" => config.Rag.MaxResults.ToString(),
            "rag.linkdepth" or "rag.maxlinkeddocs" => config.Rag.LinkDepth.ToString(),
            "linkresolution.maxdepth" => config.LinkResolution.MaxDepth.ToString(),
            "linkresolution.maxlinkeddocs" => config.LinkResolution.MaxLinkedDocs.ToString(),
            "filewatcher.debouncems" => config.FileWatcher.DebounceMs.ToString(),
            _ => "unknown"
        };
    }

    private Task<ToolResponse<ResetConfigResult>> ResetProjectConfigAsync(CancellationToken cancellationToken)
    {
        if (!_sessionContext.IsProjectActive)
        {
            return Task.FromResult(ToolResponse<ResetConfigResult>.Fail(ToolErrors.NoActiveProject));
        }

        var projectPath = _sessionContext.ActiveProjectPath!;
        var defaultConfig = new ProjectConfig
        {
            ProjectName = Path.GetFileName(projectPath)
        };

        _configurationLoader.SaveProjectConfig(projectPath, defaultConfig);

        _logger.LogInformation("Project configuration reset to defaults for: {ProjectPath}", projectPath);

        return Task.FromResult(ToolResponse<ResetConfigResult>.Ok(new ResetConfigResult
        {
            Scope = "project",
            Message = "Project configuration has been reset to default values",
            ResetSettings =
            [
                "projectName",
                "rag.chunkSize",
                "rag.chunkOverlap",
                "rag.maxResults",
                "rag.similarityThreshold",
                "rag.linkDepth",
                "linkResolution.maxDepth",
                "linkResolution.maxLinkedDocs",
                "fileWatcher.debounceMs",
                "externalDocs",
                "customDocTypes"
            ]
        }));
    }

    #endregion

    #region Helper Methods

    private static ToolError? ApplySetting(Action apply)
    {
        apply();
        return null;
    }

    private static ToolError? ApplyIntSetting(string value, int min, int max, Action<int> apply, string errorMessage)
    {
        if (!int.TryParse(value, out var intValue))
        {
            return ToolErrors.InvalidParameter("value", "Must be a valid integer");
        }

        if (intValue < min || intValue > max)
        {
            return ToolErrors.InvalidParameter("value", errorMessage);
        }

        apply(intValue);
        return null;
    }

    private static ToolError? ApplyFloatSetting(string value, float min, float max, Action<float> apply, string errorMessage)
    {
        if (!float.TryParse(value, out var floatValue))
        {
            return ToolErrors.InvalidParameter("value", "Must be a valid decimal number");
        }

        if (floatValue < min || floatValue > max)
        {
            return ToolErrors.InvalidParameter("value", errorMessage);
        }

        apply(floatValue);
        return null;
    }

    private static string MaskSensitiveValue(string setting, string value)
    {
        if (setting.Contains("password", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(value))
        {
            return "****";
        }
        return value;
    }

    private static List<EnvironmentVariableInfo> GetEnvironmentVariableStatus()
    {
        return
        [
            new()
            {
                Name = "COMPOUNDING_POSTGRES_HOST",
                IsSet = Environment.GetEnvironmentVariable("COMPOUNDING_POSTGRES_HOST") != null,
                Description = "Override PostgreSQL host"
            },
            new()
            {
                Name = "COMPOUNDING_POSTGRES_PORT",
                IsSet = Environment.GetEnvironmentVariable("COMPOUNDING_POSTGRES_PORT") != null,
                Description = "Override PostgreSQL port"
            },
            new()
            {
                Name = "COMPOUNDING_POSTGRES_DATABASE",
                IsSet = Environment.GetEnvironmentVariable("COMPOUNDING_POSTGRES_DATABASE") != null,
                Description = "Override PostgreSQL database name"
            },
            new()
            {
                Name = "COMPOUNDING_POSTGRES_USERNAME",
                IsSet = Environment.GetEnvironmentVariable("COMPOUNDING_POSTGRES_USERNAME") != null,
                Description = "Override PostgreSQL username"
            },
            new()
            {
                Name = "COMPOUNDING_POSTGRES_PASSWORD",
                IsSet = Environment.GetEnvironmentVariable("COMPOUNDING_POSTGRES_PASSWORD") != null,
                Description = "Override PostgreSQL password"
            },
            new()
            {
                Name = "COMPOUNDING_OLLAMA_HOST",
                IsSet = Environment.GetEnvironmentVariable("COMPOUNDING_OLLAMA_HOST") != null,
                Description = "Override Ollama host"
            },
            new()
            {
                Name = "COMPOUNDING_OLLAMA_PORT",
                IsSet = Environment.GetEnvironmentVariable("COMPOUNDING_OLLAMA_PORT") != null,
                Description = "Override Ollama port"
            },
            new()
            {
                Name = "COMPOUNDING_OLLAMA_MODEL",
                IsSet = Environment.GetEnvironmentVariable("COMPOUNDING_OLLAMA_MODEL") != null,
                Description = "Override Ollama generation model"
            }
        ];
    }

    #endregion
}

#region Result Types

/// <summary>
/// Result data for getting configuration.
/// </summary>
public sealed class GetConfigResult
{
    /// <summary>
    /// The scope of configuration returned.
    /// </summary>
    [JsonPropertyName("scope")]
    public required string Scope { get; init; }

    /// <summary>
    /// Global configuration settings.
    /// </summary>
    [JsonPropertyName("global")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GlobalConfigInfo? Global { get; init; }

    /// <summary>
    /// Project configuration settings.
    /// </summary>
    [JsonPropertyName("project")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProjectConfigInfo? Project { get; init; }

    /// <summary>
    /// Environment variable override status.
    /// </summary>
    [JsonPropertyName("environment_variables")]
    public required List<EnvironmentVariableInfo> EnvironmentVariables { get; init; }
}

/// <summary>
/// Result data for setting configuration.
/// </summary>
public sealed class SetConfigResult
{
    /// <summary>
    /// The scope of configuration updated.
    /// </summary>
    [JsonPropertyName("scope")]
    public required string Scope { get; init; }

    /// <summary>
    /// The setting path that was updated.
    /// </summary>
    [JsonPropertyName("setting")]
    public required string Setting { get; init; }

    /// <summary>
    /// The previous value (masked if sensitive).
    /// </summary>
    [JsonPropertyName("previous_value")]
    public required string PreviousValue { get; init; }

    /// <summary>
    /// The new value (masked if sensitive).
    /// </summary>
    [JsonPropertyName("new_value")]
    public required string NewValue { get; init; }

    /// <summary>
    /// Human-readable result message.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

/// <summary>
/// Result data for resetting configuration.
/// </summary>
public sealed class ResetConfigResult
{
    /// <summary>
    /// The scope of configuration reset.
    /// </summary>
    [JsonPropertyName("scope")]
    public required string Scope { get; init; }

    /// <summary>
    /// Human-readable result message.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }

    /// <summary>
    /// List of settings that were reset.
    /// </summary>
    [JsonPropertyName("reset_settings")]
    public required List<string> ResetSettings { get; init; }
}

#endregion

#region Info Types

/// <summary>
/// Global configuration information.
/// </summary>
public sealed class GlobalConfigInfo
{
    /// <summary>
    /// The global configuration directory path.
    /// </summary>
    [JsonPropertyName("config_directory")]
    public required string ConfigDirectory { get; init; }

    /// <summary>
    /// PostgreSQL connection settings.
    /// </summary>
    [JsonPropertyName("postgres")]
    public required PostgresConfigInfo Postgres { get; init; }

    /// <summary>
    /// Ollama connection settings.
    /// </summary>
    [JsonPropertyName("ollama")]
    public required OllamaConfigInfo Ollama { get; init; }
}

/// <summary>
/// PostgreSQL configuration information.
/// </summary>
public sealed class PostgresConfigInfo
{
    [JsonPropertyName("host")]
    public required string Host { get; init; }

    [JsonPropertyName("port")]
    public required int Port { get; init; }

    [JsonPropertyName("database")]
    public required string Database { get; init; }

    [JsonPropertyName("username")]
    public required string Username { get; init; }

    [JsonPropertyName("password_set")]
    public required bool PasswordSet { get; init; }
}

/// <summary>
/// Ollama configuration information.
/// </summary>
public sealed class OllamaConfigInfo
{
    [JsonPropertyName("host")]
    public required string Host { get; init; }

    [JsonPropertyName("port")]
    public required int Port { get; init; }

    [JsonPropertyName("generation_model")]
    public required string GenerationModel { get; init; }

    [JsonPropertyName("embedding_model")]
    public required string EmbeddingModel { get; init; }

    [JsonPropertyName("embedding_dimensions")]
    public required int EmbeddingDimensions { get; init; }
}

/// <summary>
/// Project configuration information.
/// </summary>
public sealed class ProjectConfigInfo
{
    [JsonPropertyName("project_name")]
    public string? ProjectName { get; init; }

    [JsonPropertyName("project_path")]
    public required string ProjectPath { get; init; }

    [JsonPropertyName("rag")]
    public required RagConfigInfo Rag { get; init; }

    [JsonPropertyName("link_resolution")]
    public required LinkResolutionConfigInfo LinkResolution { get; init; }

    [JsonPropertyName("file_watcher")]
    public required FileWatcherConfigInfo FileWatcher { get; init; }

    [JsonPropertyName("external_docs_count")]
    public required int ExternalDocsCount { get; init; }

    [JsonPropertyName("custom_doc_types_count")]
    public required int CustomDocTypesCount { get; init; }
}

/// <summary>
/// RAG configuration information.
/// </summary>
public sealed class RagConfigInfo
{
    [JsonPropertyName("relevance_threshold")]
    public required float RelevanceThreshold { get; init; }

    [JsonPropertyName("max_results")]
    public required int MaxResults { get; init; }

    [JsonPropertyName("max_linked_docs")]
    public required int MaxLinkedDocs { get; init; }
}

/// <summary>
/// Link resolution configuration information.
/// </summary>
public sealed class LinkResolutionConfigInfo
{
    [JsonPropertyName("max_depth")]
    public required int MaxDepth { get; init; }

    [JsonPropertyName("max_linked_docs")]
    public required int MaxLinkedDocs { get; init; }
}

/// <summary>
/// File watcher configuration information.
/// </summary>
public sealed class FileWatcherConfigInfo
{
    [JsonPropertyName("debounce_ms")]
    public required int DebounceMs { get; init; }

    [JsonPropertyName("include_patterns")]
    public required List<string> IncludePatterns { get; init; }

    [JsonPropertyName("exclude_patterns")]
    public required List<string> ExcludePatterns { get; init; }
}

/// <summary>
/// Environment variable information.
/// </summary>
public sealed class EnvironmentVariableInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("is_set")]
    public required bool IsSet { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }
}

#endregion
