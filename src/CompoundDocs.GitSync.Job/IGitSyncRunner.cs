namespace CompoundDocs.GitSync.Job;

/// <summary>
/// Runs git sync operations for a specific repository.
/// </summary>
public interface IGitSyncRunner
{
    /// <summary>
    /// Runs a git sync cycle for the specified repository.
    /// </summary>
    Task<int> RunAsync(string repoName, CancellationToken ct = default);
}
