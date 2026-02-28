using CompoundDocs.Common.Configuration;
using CompoundDocs.GitSync;
using CompoundDocs.Graph;
using CompoundDocs.GraphRag;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Background;

internal sealed partial class GitSyncRunner
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Repository config not found for '{RepoName}'")]
    private partial void LogRepoNotFound(string repoName);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Cloned/updated repository to {RepoPath}")]
    private partial void LogRepoReady(string repoPath);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Found {FileCount} changed files")]
    private partial void LogChangedFileCount(int fileCount);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "Read {RelativePath} ({ContentLength} chars)")]
    private partial void LogFileRead(string relativePath, int contentLength);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information,
        Message = "Deleting document {DocumentId}")]
    private partial void LogDeletingDocument(string documentId);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug,
        Message = "Skipping {RelativePath} — not in monitored paths")]
    private partial void LogSkippingUnmonitored(string relativePath);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information,
        Message = "Git sync completed for '{RepoName}': {ProcessedCount} files processed")]
    private partial void LogSyncCompleted(string repoName, int processedCount);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information,
        Message = "Ingesting document {DocumentId}")]
    private partial void LogIngestingDocument(string documentId);

    private readonly IGitSyncService _gitSyncService;
    private readonly IDocumentIngestionService _ingestionService;
    private readonly IGraphRepository _graphRepository;
    private readonly CompoundDocsCloudConfig _config;
    private readonly ILogger<GitSyncRunner> _logger;

    public GitSyncRunner(
        IGitSyncService gitSyncService,
        IDocumentIngestionService ingestionService,
        IGraphRepository graphRepository,
        IOptions<CompoundDocsCloudConfig> config,
        ILogger<GitSyncRunner> logger)
    {
        _gitSyncService = gitSyncService;
        _ingestionService = ingestionService;
        _graphRepository = graphRepository;
        _config = config.Value;
        _logger = logger;
    }

    public async Task<int> RunAsync(string repoName, CancellationToken ct = default)
    {
        var repoConfig = _config.Repositories?
            .FirstOrDefault(r => string.Equals(r.Name, repoName, StringComparison.OrdinalIgnoreCase));

        if (repoConfig is null)
        {
            LogRepoNotFound(repoName);
            return 1;
        }

        var repoPath = await _gitSyncService.CloneOrUpdateAsync(repoConfig, ct);
        LogRepoReady(repoPath);

        var lastCommitHash = await _graphRepository.GetSyncStateAsync(repoName, ct);
        var changedFiles = await _gitSyncService.GetChangedFilesAsync(repoConfig, lastCommitHash, ct);
        LogChangedFileCount(changedFiles.Count);

        var monitoredPaths = repoConfig.MonitoredPaths;
        var processedCount = 0;

        foreach (var file in changedFiles)
        {
            // MonitoredPaths filter applies to all change types (including deletes)
            if (monitoredPaths.Length > 0 &&
                !monitoredPaths.Any(mp => file.Path.StartsWith(mp, StringComparison.Ordinal)))
            {
                LogSkippingUnmonitored(file.Path);
                continue;
            }

            var documentId = $"{repoConfig.Name.ToLowerInvariant()}:{file.Path.ToLowerInvariant().Replace('\\', '/')}";

            if (file.ChangeType == ChangeType.Deleted)
            {
                LogDeletingDocument(documentId);
                await _ingestionService.DeleteDocumentAsync(documentId, ct);
                processedCount++;
                continue;
            }

            var content = await _gitSyncService.ReadFileContentAsync(repoPath, file.Path, ct);
            LogFileRead(file.Path, content.Length);

            LogIngestingDocument(documentId);
            var metadata = new DocumentIngestionMetadata
            {
                DocumentId = documentId,
                Repository = repoConfig.Name.ToLowerInvariant(),
                FilePath = file.Path.ToLowerInvariant().Replace('\\', '/'),
                Title = DeriveTitle(file.Path)
            };

            await _ingestionService.IngestDocumentAsync(content, metadata, ct);
            processedCount++;
        }

        var headCommitHash = await _gitSyncService.GetHeadCommitHashAsync(repoPath, ct);
        await _graphRepository.SetSyncStateAsync(repoName, headCommitHash, ct);

        LogSyncCompleted(repoName, processedCount);
        return 0;
    }

    internal static string DeriveTitle(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return fileName.Replace('-', ' ').Replace('_', ' ');
    }
}
