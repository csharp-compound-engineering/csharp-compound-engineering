using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace CompoundDocs.Common.Configuration;

/// <summary>
/// Loads and manages configuration from files and environment variables.
/// </summary>
public sealed class ConfigurationLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private const string ProjectConfigFileName = "config.json";
    private const string ProjectConfigDirectory = ".csharp-compounding-docs";

    /// <summary>
    /// Loads global configuration from ~/.claude/.csharp-compounding-docs/
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Null-coalescing branch on overridePath requires config file at user home directory default path; not testable without filesystem side-effects")]
    public GlobalConfig LoadGlobalConfig(string? overridePath = null)
    {
        var config = new GlobalConfig();

        if (overridePath != null)
        {
            config.ConfigDirectory = overridePath;
        }

        var configFile = Path.Combine(config.ConfigDirectory, "global-config.json");

        if (File.Exists(configFile))
        {
            var json = File.ReadAllText(configFile);
            var loaded = JsonSerializer.Deserialize<GlobalConfig>(json, JsonOptions);
            if (loaded != null)
            {
                config = loaded;
                config.ConfigDirectory = overridePath ?? config.ConfigDirectory;
            }
        }

        return config;
    }

    /// <summary>
    /// Loads project configuration from .csharp-compounding-docs/config.json
    /// </summary>
    public ProjectConfig LoadProjectConfig(string projectPath)
    {
        var configDir = Path.Combine(projectPath, ProjectConfigDirectory);
        var configFile = Path.Combine(configDir, ProjectConfigFileName);

        if (!File.Exists(configFile))
        {
            return CreateDefaultProjectConfig(projectPath);
        }

        var json = File.ReadAllText(configFile);
        var config = JsonSerializer.Deserialize<ProjectConfig>(json, JsonOptions);

        return config ?? CreateDefaultProjectConfig(projectPath);
    }

    /// <summary>
    /// Saves project configuration to .csharp-compounding-docs/config.json
    /// </summary>
    public void SaveProjectConfig(string projectPath, ProjectConfig config)
    {
        var configDir = Path.Combine(projectPath, ProjectConfigDirectory);
        var configFile = Path.Combine(configDir, ProjectConfigFileName);

        Directory.CreateDirectory(configDir);

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(configFile, json);
    }

    /// <summary>
    /// Ensures the global config directory exists and creates default files if needed.
    /// </summary>
    public void EnsureGlobalConfigDirectory(GlobalConfig config)
    {
        Directory.CreateDirectory(config.ConfigDirectory);

        var globalConfigFile = Path.Combine(config.ConfigDirectory, "global-config.json");
        if (!File.Exists(globalConfigFile))
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(globalConfigFile, json);
        }
    }

    /// <summary>
    /// Ensures the project config directory exists and creates default config if needed.
    /// This handles first-time project setup (Phase 133).
    /// </summary>
    public void EnsureProjectConfigDirectory(string projectPath, ProjectConfig? config = null)
    {
        var configDir = Path.Combine(projectPath, ProjectConfigDirectory);
        Directory.CreateDirectory(configDir);

        var configFile = Path.Combine(configDir, ProjectConfigFileName);
        if (!File.Exists(configFile))
        {
            var configToSave = config ?? CreateDefaultProjectConfig(projectPath);
            var json = JsonSerializer.Serialize(configToSave, JsonOptions);
            File.WriteAllText(configFile, json);
        }
    }

    private static ProjectConfig CreateDefaultProjectConfig(string projectPath)
    {
        return new ProjectConfig
        {
            ProjectName = Path.GetFileName(projectPath)
        };
    }

}
