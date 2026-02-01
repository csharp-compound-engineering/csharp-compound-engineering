using System;

namespace CompoundDocs.McpServer.Services.FileWatcher;

/// <summary>
/// Represents the type of file change that occurred.
/// </summary>
public enum FileChangeType
{
    /// <summary>
    /// A new file was created.
    /// </summary>
    Created,

    /// <summary>
    /// An existing file was modified.
    /// </summary>
    Modified,

    /// <summary>
    /// A file was deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// A file was renamed.
    /// </summary>
    Renamed
}

/// <summary>
/// Represents a file change event detected by the file watcher.
/// </summary>
/// <param name="FilePath">The absolute path to the file that changed.</param>
/// <param name="ChangeType">The type of change that occurred.</param>
/// <param name="OldPath">The previous path for rename operations. Null for other change types.</param>
/// <param name="Timestamp">The timestamp when the change was detected.</param>
public sealed record FileChangeEvent(
    string FilePath,
    FileChangeType ChangeType,
    string? OldPath = null)
{
    /// <summary>
    /// Gets the timestamp when the change was detected.
    /// Defaults to the current UTC time if not specified.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the relative path from the project root.
    /// </summary>
    /// <param name="projectRoot">The project root path.</param>
    /// <returns>The relative file path.</returns>
    public string GetRelativePath(string projectRoot)
    {
        ArgumentNullException.ThrowIfNull(projectRoot);

        if (FilePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
        {
            var relative = FilePath[(projectRoot.Length)..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative;
        }

        return FilePath;
    }

    /// <summary>
    /// Gets the file name without the directory path.
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// Creates a copy of this event with a new change type.
    /// Useful for converting renames to delete+create pairs.
    /// </summary>
    public FileChangeEvent WithChangeType(FileChangeType newType) =>
        this with { ChangeType = newType };
}
