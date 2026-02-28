using CompoundDocs.Common.Configuration;

namespace CompoundDocs.GitSync;

public interface IGitSyncService
{
    Task<string> CloneOrUpdateAsync(RepositoryConfig repoConfig, CancellationToken ct = default);

    Task<List<ChangedFile>> GetChangedFilesAsync(
        RepositoryConfig repoConfig,
        string? lastCommitHash,
        CancellationToken ct = default);

    Task<string> ReadFileContentAsync(
        string repoPath,
        string relativePath,
        CancellationToken ct = default);

    Task<string> GetHeadCommitHashAsync(string repoPath, CancellationToken ct = default);
}
