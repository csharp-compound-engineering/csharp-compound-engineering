using System;
using System.Collections.Generic;

namespace CompoundDocs.McpServer.Services.FileWatcher;

/// <summary>
/// Represents the action needed for a file during reconciliation.
/// </summary>
public enum ReconciliationAction
{
    /// <summary>
    /// No action needed; file is in sync.
    /// </summary>
    None,

    /// <summary>
    /// File needs to be indexed (new file).
    /// </summary>
    Index,

    /// <summary>
    /// File needs to be re-indexed (modified).
    /// </summary>
    Reindex,

    /// <summary>
    /// File needs to be removed from index (deleted from disk).
    /// </summary>
    Remove
}

/// <summary>
/// Represents a file that needs reconciliation action.
/// </summary>
/// <param name="FilePath">The absolute path to the file.</param>
/// <param name="Action">The action needed for this file.</param>
/// <param name="Reason">The reason for the action.</param>
public sealed record ReconciliationItem(
    string FilePath,
    ReconciliationAction Action,
    string Reason);

/// <summary>
/// Represents the result of a file reconciliation operation.
/// </summary>
public sealed class ReconciliationResult
{
    /// <summary>
    /// Gets or sets the project path that was reconciled.
    /// </summary>
    public required string ProjectPath { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when reconciliation was performed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the files that need to be indexed (new files on disk).
    /// </summary>
    public IReadOnlyList<ReconciliationItem> NewFiles { get; init; } = [];

    /// <summary>
    /// Gets or sets the files that need to be re-indexed (modified on disk).
    /// </summary>
    public IReadOnlyList<ReconciliationItem> ModifiedFiles { get; init; } = [];

    /// <summary>
    /// Gets or sets the files that need to be removed from the index (deleted from disk).
    /// </summary>
    public IReadOnlyList<ReconciliationItem> DeletedFiles { get; init; } = [];

    /// <summary>
    /// Gets the total number of files found on disk.
    /// </summary>
    public int TotalFilesOnDisk { get; init; }

    /// <summary>
    /// Gets the total number of files in the database.
    /// </summary>
    public int TotalFilesInDatabase { get; init; }

    /// <summary>
    /// Gets whether any actions are needed.
    /// </summary>
    public bool HasChanges =>
        NewFiles.Count > 0 || ModifiedFiles.Count > 0 || DeletedFiles.Count > 0;

    /// <summary>
    /// Gets the total number of actions needed.
    /// </summary>
    public int TotalActions => NewFiles.Count + ModifiedFiles.Count + DeletedFiles.Count;

    /// <summary>
    /// Gets all items that need action.
    /// </summary>
    public IEnumerable<ReconciliationItem> AllItems
    {
        get
        {
            foreach (var item in NewFiles)
                yield return item;
            foreach (var item in ModifiedFiles)
                yield return item;
            foreach (var item in DeletedFiles)
                yield return item;
        }
    }

    /// <summary>
    /// Creates a result indicating no changes are needed.
    /// </summary>
    public static ReconciliationResult NoChanges(string projectPath, int filesOnDisk, int filesInDatabase) =>
        new()
        {
            ProjectPath = projectPath,
            TotalFilesOnDisk = filesOnDisk,
            TotalFilesInDatabase = filesInDatabase
        };
}
