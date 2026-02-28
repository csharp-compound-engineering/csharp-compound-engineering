namespace CompoundDocs.Common.Configuration;

/// <summary>
/// Loads and manages configuration from files and environment variables.
/// </summary>
public interface IConfigurationLoader
{
    /// <summary>
    /// Loads global configuration from ~/.claude/.csharp-compounding-docs/
    /// </summary>
    GlobalConfig LoadGlobalConfig(string? overridePath = null);

    /// <summary>
    /// Loads project configuration from .csharp-compounding-docs/config.json
    /// </summary>
    ProjectConfig LoadProjectConfig(string projectPath);

    /// <summary>
    /// Saves project configuration to .csharp-compounding-docs/config.json
    /// </summary>
    void SaveProjectConfig(string projectPath, ProjectConfig config);

    /// <summary>
    /// Ensures the global config directory exists and creates default files if needed.
    /// </summary>
    void EnsureGlobalConfigDirectory(GlobalConfig config);

    /// <summary>
    /// Ensures the project config directory exists and creates default config if needed.
    /// </summary>
    void EnsureProjectConfigDirectory(string projectPath, ProjectConfig? config = null);
}
