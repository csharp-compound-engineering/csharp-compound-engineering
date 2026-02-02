using CompoundDocs.Common.Configuration;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.GitSync;

public sealed class GitSyncService : IGitSyncService
{
    private readonly string _baseDirectory;
    private readonly ILogger<GitSyncService> _logger;

    public GitSyncService(
        IOptions<GitSyncConfig> options,
        ILogger<GitSyncService> logger)
    {
        _baseDirectory = options.Value.CloneBaseDirectory;
        _logger = logger;

        if (!Directory.Exists(_baseDirectory))
        {
            Directory.CreateDirectory(_baseDirectory);
        }
    }

    internal GitSyncService(string baseDirectory, ILogger<GitSyncService> logger)
    {
        _baseDirectory = baseDirectory;
        _logger = logger;
    }

    public Task<string> CloneOrUpdateAsync(RepositoryConfig repoConfig, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var repoPath = Path.Combine(_baseDirectory, repoConfig.Name);

            if (Repository.IsValid(repoPath))
            {
                _logger.LogDebug("Pulling updates for {RepoName}", repoConfig.Name);
                using var repo = new Repository(repoPath);
                var signature = new Signature("CompoundDocs", "compound@docs.local", DateTimeOffset.Now);
                Commands.Pull(repo, signature, new PullOptions());
            }
            else
            {
                _logger.LogInformation("Cloning repository {RepoName} from {Url}", repoConfig.Name, repoConfig.Url);
                Repository.Clone(repoConfig.Url, repoPath, new CloneOptions
                {
                    BranchName = repoConfig.Branch
                });
            }

            return repoPath;
        }, ct);
    }

    public Task<List<ChangedFile>> GetChangedFilesAsync(
        RepositoryConfig repoConfig,
        string? lastCommitHash,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var repoPath = Path.Combine(_baseDirectory, repoConfig.Name);
            var changedFiles = new List<ChangedFile>();

            if (!Repository.IsValid(repoPath))
            {
                _logger.LogWarning("Repository {RepoName} not found at {Path}", repoConfig.Name, repoPath);
                return changedFiles;
            }

            using var repo = new Repository(repoPath);

            if (string.IsNullOrEmpty(lastCommitHash))
            {
                // First sync - treat all tracked files as added
                var tree = repo.Head.Tip.Tree;
                AddTreeEntries(tree, string.Empty, changedFiles);
                return changedFiles;
            }

            var oldCommit = repo.Lookup<Commit>(lastCommitHash);
            var newCommit = repo.Head.Tip;

            if (oldCommit == null)
            {
                _logger.LogWarning("Commit {CommitHash} not found", lastCommitHash);
                return changedFiles;
            }

            var diff = repo.Diff.Compare<TreeChanges>(oldCommit.Tree, newCommit.Tree);

            foreach (var change in diff)
            {
                var changeType = change.Status switch
                {
                    ChangeKind.Added => ChangeType.Added,
                    ChangeKind.Deleted => ChangeType.Deleted,
                    _ => ChangeType.Modified
                };

                changedFiles.Add(new ChangedFile
                {
                    Path = change.Path,
                    ChangeType = changeType
                });
            }

            return changedFiles;
        }, ct);
    }

    public Task<string> ReadFileContentAsync(
        string repoPath,
        string relativePath,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var fullPath = Path.Combine(repoPath, relativePath);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: {relativePath}", fullPath);
            }

            return File.ReadAllText(fullPath);
        }, ct);
    }

    private static void AddTreeEntries(Tree tree, string prefix, List<ChangedFile> files)
    {
        foreach (var entry in tree)
        {
            var path = string.IsNullOrEmpty(prefix)
                ? entry.Name
                : $"{prefix}/{entry.Name}";

            if (entry.TargetType == TreeEntryTargetType.Blob)
            {
                files.Add(new ChangedFile
                {
                    Path = path,
                    ChangeType = ChangeType.Added
                });
            }
            else if (entry.TargetType == TreeEntryTargetType.Tree)
            {
                AddTreeEntries((Tree)entry.Target, path, files);
            }
        }
    }
}
