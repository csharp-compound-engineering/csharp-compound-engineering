namespace CompoundDocs.Common.Configuration;

/// <summary>
/// Global configuration stored in ~/.claude/.csharp-compounding-docs/
/// </summary>
public sealed class GlobalConfig
{
    /// <summary>
    /// Path to global config directory (default: ~/.claude/.csharp-compounding-docs/)
    /// </summary>
    public string ConfigDirectory { get; set; } = GetDefaultConfigDirectory();

    private static string GetDefaultConfigDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".claude", ".csharp-compounding-docs");
    }
}
