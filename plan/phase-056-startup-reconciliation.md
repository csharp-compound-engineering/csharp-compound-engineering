# Phase 056: Startup Reconciliation

> **Status**: [PLANNED]
> **Category**: Document Processing
> **Estimated Effort**: L
> **Prerequisites**: Phase 053

---

## Spec References

- [spec/mcp-server/file-watcher.md - Sync on Activation](../spec/mcp-server/file-watcher.md#sync-on-activation-startup-reconciliation)
- [spec/mcp-server/file-watcher.md - Crash Recovery](../spec/mcp-server/file-watcher.md#crash-recovery)
- [spec/mcp-server/database-schema.md](../spec/mcp-server/database-schema.md)
- [spec/mcp-server/chunking.md](../spec/mcp-server/chunking.md)

---

## Objectives

1. Implement full disk-to-database reconciliation on project activation
2. Detect and index files on disk that are not in the database (new files)
3. Detect and remove database records where the disk file no longer exists (orphaned records)
4. Detect and re-index modified files using content hash comparison
5. Regenerate chunks for modified documents exceeding 500 lines
6. Provide progress reporting for large repository reconciliation
7. Support crash recovery by ensuring disk state is always source of truth
8. Handle external docs reconciliation when configured

---

## Acceptance Criteria

- [ ] Reconciliation service lists all `.md` files in `./csharp-compounding-docs/` directory
- [ ] Service compares disk files with database records by path and content hash
- [ ] New files on disk (not in DB) are queued for indexing
- [ ] Orphaned DB records (file not on disk) are deleted with their chunks
- [ ] Modified files (content hash mismatch) are queued for re-indexing
- [ ] Chunks are regenerated for modified documents >500 lines
- [ ] Progress events report total files, processed count, and current operation
- [ ] Reconciliation handles partial write recovery (incomplete previous indexing)
- [ ] External docs paths are reconciled when `external_docs` is configured
- [ ] Performance meets spec targets (<1s for <100 docs, <15s for 500-1000 docs)
- [ ] All operations are idempotent (re-running reconciliation produces same result)
- [ ] Unit tests verify reconciliation algorithm logic
- [ ] Integration tests verify disk/database synchronization

---

## Implementation Notes

### 1. Reconciliation Service Interface

```csharp
/// <summary>
/// Performs startup reconciliation between disk and database.
/// Ensures the vector database reflects the current state of documentation files.
/// </summary>
public interface IReconciliationService
{
    /// <summary>
    /// Performs full reconciliation for the active tenant context.
    /// Called when a project is activated via activate_project tool.
    /// </summary>
    /// <param name="context">The active tenant context (project, branch, path)</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Summary of reconciliation operations performed</returns>
    Task<ReconciliationResult> ReconcileAsync(
        TenantContext context,
        IProgress<ReconciliationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Summary of reconciliation operations performed.
/// </summary>
public record ReconciliationResult
{
    public int FilesScanned { get; init; }
    public int FilesIndexed { get; init; }
    public int FilesUpdated { get; init; }
    public int FilesDeleted { get; init; }
    public int ChunksCreated { get; init; }
    public int ChunksDeleted { get; init; }
    public TimeSpan Duration { get; init; }
    public IReadOnlyList<ReconciliationError> Errors { get; init; } = Array.Empty<ReconciliationError>();
}

/// <summary>
/// Progress information for reconciliation operations.
/// </summary>
public record ReconciliationProgress
{
    public ReconciliationPhase Phase { get; init; }
    public int TotalFiles { get; init; }
    public int ProcessedFiles { get; init; }
    public string? CurrentFile { get; init; }
    public string? CurrentOperation { get; init; }
}

public enum ReconciliationPhase
{
    Scanning,
    Comparing,
    Indexing,
    Updating,
    Deleting,
    Completed
}

/// <summary>
/// Error encountered during reconciliation (non-fatal).
/// </summary>
public record ReconciliationError
{
    public string FilePath { get; init; } = string.Empty;
    public string ErrorType { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
```

### 2. Reconciliation Algorithm Implementation

```csharp
public class ReconciliationService : IReconciliationService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IChunkRepository _chunkRepository;
    private readonly IDocumentIndexer _documentIndexer;
    private readonly IContentHasher _contentHasher;
    private readonly IFileSystemService _fileSystem;
    private readonly ILogger<ReconciliationService> _logger;

    public async Task<ReconciliationResult> ReconcileAsync(
        TenantContext context,
        IProgress<ReconciliationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new ReconciliationResultBuilder();
        var errors = new List<ReconciliationError>();

        try
        {
            // Phase 1: Scan disk for markdown files
            progress?.Report(new ReconciliationProgress
            {
                Phase = ReconciliationPhase.Scanning,
                CurrentOperation = "Scanning disk for markdown files"
            });

            var diskFiles = await ScanDiskFilesAsync(context, cancellationToken);
            result.FilesScanned = diskFiles.Count;

            _logger.LogInformation(
                "Reconciliation scanning complete. Found {FileCount} markdown files",
                diskFiles.Count);

            // Phase 2: Load existing database records
            progress?.Report(new ReconciliationProgress
            {
                Phase = ReconciliationPhase.Comparing,
                TotalFiles = diskFiles.Count,
                CurrentOperation = "Loading database records"
            });

            var dbRecords = await _documentRepository.GetAllByTenantAsync(context, cancellationToken);
            var dbRecordsByPath = dbRecords.ToDictionary(d => d.RelativePath, d => d);

            // Phase 3: Compare and categorize
            var toIndex = new List<DiskFile>();      // On disk, not in DB
            var toUpdate = new List<DiskFile>();     // On disk, in DB, hash differs
            var toDelete = new List<string>();       // In DB, not on disk
            var unchanged = new List<DiskFile>();    // On disk, in DB, hash matches

            foreach (var diskFile in diskFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!dbRecordsByPath.TryGetValue(diskFile.RelativePath, out var dbRecord))
                {
                    // File on disk but not in database -> index
                    toIndex.Add(diskFile);
                }
                else if (diskFile.ContentHash != dbRecord.ContentHash)
                {
                    // File on disk with different content -> update
                    toUpdate.Add(diskFile);
                }
                else
                {
                    // File unchanged
                    unchanged.Add(diskFile);
                }
            }

            // Find orphaned database records (in DB but not on disk)
            var diskPaths = diskFiles.Select(f => f.RelativePath).ToHashSet();
            foreach (var dbRecord in dbRecords)
            {
                if (!diskPaths.Contains(dbRecord.RelativePath))
                {
                    toDelete.Add(dbRecord.Id);
                }
            }

            _logger.LogInformation(
                "Reconciliation comparison: {ToIndex} to index, {ToUpdate} to update, " +
                "{ToDelete} to delete, {Unchanged} unchanged",
                toIndex.Count, toUpdate.Count, toDelete.Count, unchanged.Count);

            // Phase 4: Process indexes (new files)
            await ProcessIndexOperationsAsync(
                toIndex, context, progress, result, errors, cancellationToken);

            // Phase 5: Process updates (modified files)
            await ProcessUpdateOperationsAsync(
                toUpdate, context, progress, result, errors, cancellationToken);

            // Phase 6: Process deletions (orphaned records)
            await ProcessDeleteOperationsAsync(
                toDelete, context, progress, result, errors, cancellationToken);

            stopwatch.Stop();
            return result.Build(stopwatch.Elapsed, errors);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Reconciliation cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reconciliation failed");
            throw;
        }
    }

    private async Task<List<DiskFile>> ScanDiskFilesAsync(
        TenantContext context,
        CancellationToken cancellationToken)
    {
        var docsPath = Path.Combine(context.AbsolutePath, "csharp-compounding-docs");
        var files = new List<DiskFile>();

        if (!_fileSystem.DirectoryExists(docsPath))
        {
            _logger.LogWarning(
                "Compounding docs directory does not exist: {Path}",
                docsPath);
            return files;
        }

        var markdownFiles = _fileSystem.EnumerateFiles(docsPath, "*.md", SearchOption.AllDirectories);

        foreach (var absolutePath in markdownFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var relativePath = Path.GetRelativePath(docsPath, absolutePath)
                    .Replace('\\', '/');
                var content = await _fileSystem.ReadAllTextAsync(absolutePath, cancellationToken);
                var contentHash = _contentHasher.ComputeHash(content);
                var lineCount = content.Split('\n').Length;

                files.Add(new DiskFile
                {
                    AbsolutePath = absolutePath,
                    RelativePath = relativePath,
                    Content = content,
                    ContentHash = contentHash,
                    LineCount = lineCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to read file during reconciliation scan: {Path}",
                    absolutePath);
            }
        }

        return files;
    }

    private async Task ProcessIndexOperationsAsync(
        List<DiskFile> toIndex,
        TenantContext context,
        IProgress<ReconciliationProgress>? progress,
        ReconciliationResultBuilder result,
        List<ReconciliationError> errors,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < toIndex.Count; i++)
        {
            var file = toIndex[i];
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new ReconciliationProgress
            {
                Phase = ReconciliationPhase.Indexing,
                TotalFiles = toIndex.Count,
                ProcessedFiles = i,
                CurrentFile = file.RelativePath,
                CurrentOperation = "Indexing new file"
            });

            try
            {
                var indexResult = await _documentIndexer.IndexDocumentAsync(
                    file, context, cancellationToken);

                result.FilesIndexed++;
                result.ChunksCreated += indexResult.ChunksCreated;

                _logger.LogDebug(
                    "Indexed new file: {Path} (chunks: {ChunkCount})",
                    file.RelativePath, indexResult.ChunksCreated);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to index file: {Path}",
                    file.RelativePath);

                errors.Add(new ReconciliationError
                {
                    FilePath = file.RelativePath,
                    ErrorType = "IndexError",
                    Message = ex.Message
                });
            }
        }
    }

    private async Task ProcessUpdateOperationsAsync(
        List<DiskFile> toUpdate,
        TenantContext context,
        IProgress<ReconciliationProgress>? progress,
        ReconciliationResultBuilder result,
        List<ReconciliationError> errors,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < toUpdate.Count; i++)
        {
            var file = toUpdate[i];
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new ReconciliationProgress
            {
                Phase = ReconciliationPhase.Updating,
                TotalFiles = toUpdate.Count,
                ProcessedFiles = i,
                CurrentFile = file.RelativePath,
                CurrentOperation = "Re-indexing modified file"
            });

            try
            {
                // Delete existing chunks first
                var deletedChunks = await _chunkRepository.DeleteByDocumentPathAsync(
                    file.RelativePath, context, cancellationToken);
                result.ChunksDeleted += deletedChunks;

                // Re-index with new content
                var indexResult = await _documentIndexer.UpdateDocumentAsync(
                    file, context, cancellationToken);

                result.FilesUpdated++;
                result.ChunksCreated += indexResult.ChunksCreated;

                _logger.LogDebug(
                    "Updated file: {Path} (old chunks deleted: {Deleted}, new chunks: {Created})",
                    file.RelativePath, deletedChunks, indexResult.ChunksCreated);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to update file: {Path}",
                    file.RelativePath);

                errors.Add(new ReconciliationError
                {
                    FilePath = file.RelativePath,
                    ErrorType = "UpdateError",
                    Message = ex.Message
                });
            }
        }
    }

    private async Task ProcessDeleteOperationsAsync(
        List<string> toDelete,
        TenantContext context,
        IProgress<ReconciliationProgress>? progress,
        ReconciliationResultBuilder result,
        List<ReconciliationError> errors,
        CancellationToken cancellationToken)
    {
        progress?.Report(new ReconciliationProgress
        {
            Phase = ReconciliationPhase.Deleting,
            TotalFiles = toDelete.Count,
            CurrentOperation = "Removing orphaned database records"
        });

        for (int i = 0; i < toDelete.Count; i++)
        {
            var documentId = toDelete[i];
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Delete chunks first (referential integrity)
                var deletedChunks = await _chunkRepository.DeleteByDocumentIdAsync(
                    documentId, cancellationToken);
                result.ChunksDeleted += deletedChunks;

                // Delete parent document
                await _documentRepository.DeleteAsync(documentId, cancellationToken);
                result.FilesDeleted++;

                _logger.LogDebug(
                    "Deleted orphaned document: {DocumentId} (chunks: {ChunkCount})",
                    documentId, deletedChunks);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to delete orphaned document: {DocumentId}",
                    documentId);

                errors.Add(new ReconciliationError
                {
                    FilePath = documentId,
                    ErrorType = "DeleteError",
                    Message = ex.Message
                });
            }
        }
    }
}
```

### 3. Content Hash Service

```csharp
/// <summary>
/// Computes content hashes for change detection.
/// </summary>
public interface IContentHasher
{
    /// <summary>
    /// Computes SHA256 hash of content for change detection.
    /// </summary>
    string ComputeHash(string content);
}

public class Sha256ContentHasher : IContentHasher
{
    public string ComputeHash(string content)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
```

### 4. External Docs Reconciliation

External docs are reconciled with the same algorithm but stored in separate collections:

```csharp
public class ExternalDocsReconciliationService : IExternalDocsReconciliationService
{
    private readonly IExternalDocumentRepository _externalDocRepository;
    private readonly IExternalChunkRepository _externalChunkRepository;
    private readonly IDocumentIndexer _documentIndexer;
    private readonly IContentHasher _contentHasher;
    private readonly IProjectConfigService _configService;
    private readonly ILogger<ExternalDocsReconciliationService> _logger;

    public async Task<ReconciliationResult> ReconcileExternalDocsAsync(
        TenantContext context,
        IProgress<ReconciliationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var config = await _configService.GetProjectConfigAsync(context, cancellationToken);

        if (string.IsNullOrEmpty(config.ExternalDocsPath))
        {
            _logger.LogDebug("No external_docs configured, skipping external reconciliation");
            return ReconciliationResult.Empty;
        }

        // Same algorithm as compounding docs reconciliation
        // but uses external document collections
        // ...
    }
}
```

### 5. Crash Recovery Support

The reconciliation approach inherently handles crash recovery:

```csharp
/// <summary>
/// Reconciliation handles various crash scenarios by treating disk as source of truth.
/// </summary>
public class CrashRecoveryBehavior
{
    /*
     * Scenario 1: Crash during file watcher event processing
     * - In-memory queue is lost
     * - On next activation, reconciliation detects any drift
     * - Disk state is re-indexed as needed
     *
     * Scenario 2: Crash during document indexing (partial write)
     * - Document may exist without embedding or chunks
     * - Content hash comparison detects mismatch
     * - Document is re-indexed completely
     *
     * Scenario 3: Crash during chunk generation
     * - Parent document may have partial or no chunks
     * - Re-indexing regenerates all chunks
     * - Orphaned chunks are cleaned up
     */
}
```

### 6. Progress Reporting Integration

```csharp
/// <summary>
/// MCP tool handler integration for progress reporting.
/// </summary>
public class ActivateProjectToolHandler
{
    private readonly IReconciliationService _reconciliationService;
    private readonly IProgressNotifier _progressNotifier;

    public async Task<ActivateProjectResult> HandleAsync(
        ActivateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var context = BuildTenantContext(request);

        // Report reconciliation progress via MCP notifications
        var progress = new Progress<ReconciliationProgress>(p =>
        {
            _progressNotifier.NotifyProgress(new
            {
                operation = "reconciliation",
                phase = p.Phase.ToString(),
                totalFiles = p.TotalFiles,
                processedFiles = p.ProcessedFiles,
                currentFile = p.CurrentFile,
                message = p.CurrentOperation
            });
        });

        var result = await _reconciliationService.ReconcileAsync(
            context, progress, cancellationToken);

        return new ActivateProjectResult
        {
            Success = true,
            Reconciliation = new ReconciliationSummary
            {
                FilesScanned = result.FilesScanned,
                FilesIndexed = result.FilesIndexed,
                FilesUpdated = result.FilesUpdated,
                FilesDeleted = result.FilesDeleted,
                Duration = result.Duration,
                Errors = result.Errors.Count
            }
        };
    }
}
```

### 7. Performance Optimization

```csharp
/// <summary>
/// Performance optimizations for large repository reconciliation.
/// </summary>
public class ReconciliationPerformanceOptions
{
    /// <summary>
    /// Maximum number of concurrent embedding generations.
    /// </summary>
    public int MaxConcurrentEmbeddings { get; set; } = 4;

    /// <summary>
    /// Batch size for database operations.
    /// </summary>
    public int DatabaseBatchSize { get; set; } = 50;

    /// <summary>
    /// Enable parallel file scanning.
    /// </summary>
    public bool ParallelScanning { get; set; } = true;
}

// Performance targets from spec:
// | Document Count | Expected Reconciliation Time |
// |----------------|------------------------------|
// | < 100          | < 1 second                   |
// | 100-500        | 1-5 seconds                  |
// | 500-1000       | 5-15 seconds                 |
// | > 1000         | Consider batching            |
```

### 8. DI Registration

```csharp
public static class ReconciliationServiceExtensions
{
    public static IServiceCollection AddReconciliationServices(
        this IServiceCollection services)
    {
        services.AddSingleton<IContentHasher, Sha256ContentHasher>();
        services.AddScoped<IReconciliationService, ReconciliationService>();
        services.AddScoped<IExternalDocsReconciliationService, ExternalDocsReconciliationService>();

        services.Configure<ReconciliationPerformanceOptions>(options =>
        {
            options.MaxConcurrentEmbeddings = 4;
            options.DatabaseBatchSize = 50;
            options.ParallelScanning = true;
        });

        return services;
    }
}
```

---

## Dependencies

### Depends On

- **Phase 053**: File Watcher Service - Provides the file system monitoring infrastructure that triggers reconciliation on activation

### Blocks

- **Phase 057+**: RAG query tools - Need properly indexed documents
- **Document indexing phases**: Reconciliation coordinates indexing operations
- **Cleanup tool phases**: May leverage reconciliation for orphan detection

---

## Testing Strategy

### Unit Tests

```csharp
public class ReconciliationServiceTests
{
    [Fact]
    public async Task ReconcileAsync_NewFileOnDisk_IndexesFile()
    {
        // Arrange
        var mockFileSystem = new Mock<IFileSystemService>();
        mockFileSystem.Setup(fs => fs.EnumerateFiles(It.IsAny<string>(), "*.md", SearchOption.AllDirectories))
            .Returns(new[] { "/project/csharp-compounding-docs/new-doc.md" });
        mockFileSystem.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# New Document\nContent here");

        var mockDocRepo = new Mock<IDocumentRepository>();
        mockDocRepo.Setup(r => r.GetAllByTenantAsync(It.IsAny<TenantContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CompoundDocument>()); // Empty DB

        var mockIndexer = new Mock<IDocumentIndexer>();
        mockIndexer.Setup(i => i.IndexDocumentAsync(It.IsAny<DiskFile>(), It.IsAny<TenantContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexResult { ChunksCreated = 0 });

        var service = new ReconciliationService(
            mockDocRepo.Object,
            new Mock<IChunkRepository>().Object,
            mockIndexer.Object,
            new Sha256ContentHasher(),
            mockFileSystem.Object,
            Mock.Of<ILogger<ReconciliationService>>());

        var context = new TenantContext { AbsolutePath = "/project" };

        // Act
        var result = await service.ReconcileAsync(context);

        // Assert
        Assert.Equal(1, result.FilesScanned);
        Assert.Equal(1, result.FilesIndexed);
        mockIndexer.Verify(i => i.IndexDocumentAsync(
            It.Is<DiskFile>(f => f.RelativePath == "new-doc.md"),
            It.IsAny<TenantContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReconcileAsync_OrphanedDbRecord_DeletesRecord()
    {
        // Arrange
        var mockFileSystem = new Mock<IFileSystemService>();
        mockFileSystem.Setup(fs => fs.EnumerateFiles(It.IsAny<string>(), "*.md", SearchOption.AllDirectories))
            .Returns(Array.Empty<string>()); // No files on disk

        var orphanedDoc = new CompoundDocument
        {
            Id = "doc-123",
            RelativePath = "deleted-doc.md",
            ContentHash = "abc123"
        };

        var mockDocRepo = new Mock<IDocumentRepository>();
        mockDocRepo.Setup(r => r.GetAllByTenantAsync(It.IsAny<TenantContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CompoundDocument> { orphanedDoc });

        var mockChunkRepo = new Mock<IChunkRepository>();
        mockChunkRepo.Setup(r => r.DeleteByDocumentIdAsync("doc-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(3); // 3 chunks deleted

        var service = new ReconciliationService(
            mockDocRepo.Object,
            mockChunkRepo.Object,
            new Mock<IDocumentIndexer>().Object,
            new Sha256ContentHasher(),
            mockFileSystem.Object,
            Mock.Of<ILogger<ReconciliationService>>());

        var context = new TenantContext { AbsolutePath = "/project" };

        // Act
        var result = await service.ReconcileAsync(context);

        // Assert
        Assert.Equal(1, result.FilesDeleted);
        Assert.Equal(3, result.ChunksDeleted);
        mockDocRepo.Verify(r => r.DeleteAsync("doc-123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReconcileAsync_ModifiedFile_UpdatesDocument()
    {
        // Arrange
        var existingDoc = new CompoundDocument
        {
            Id = "doc-123",
            RelativePath = "modified-doc.md",
            ContentHash = "old-hash-abc"
        };

        var mockFileSystem = new Mock<IFileSystemService>();
        mockFileSystem.Setup(fs => fs.EnumerateFiles(It.IsAny<string>(), "*.md", SearchOption.AllDirectories))
            .Returns(new[] { "/project/csharp-compounding-docs/modified-doc.md" });
        mockFileSystem.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("# Modified Document\nNew content here that produces different hash");

        var mockDocRepo = new Mock<IDocumentRepository>();
        mockDocRepo.Setup(r => r.GetAllByTenantAsync(It.IsAny<TenantContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CompoundDocument> { existingDoc });

        var mockChunkRepo = new Mock<IChunkRepository>();
        mockChunkRepo.Setup(r => r.DeleteByDocumentPathAsync("modified-doc.md", It.IsAny<TenantContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var mockIndexer = new Mock<IDocumentIndexer>();
        mockIndexer.Setup(i => i.UpdateDocumentAsync(It.IsAny<DiskFile>(), It.IsAny<TenantContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexResult { ChunksCreated = 3 });

        var service = new ReconciliationService(
            mockDocRepo.Object,
            mockChunkRepo.Object,
            mockIndexer.Object,
            new Sha256ContentHasher(),
            mockFileSystem.Object,
            Mock.Of<ILogger<ReconciliationService>>());

        var context = new TenantContext { AbsolutePath = "/project" };

        // Act
        var result = await service.ReconcileAsync(context);

        // Assert
        Assert.Equal(1, result.FilesUpdated);
        Assert.Equal(2, result.ChunksDeleted);
        Assert.Equal(3, result.ChunksCreated);
    }

    [Fact]
    public async Task ReconcileAsync_UnchangedFile_SkipsProcessing()
    {
        // Arrange
        var content = "# Unchanged Document\nSame content";
        var contentHash = new Sha256ContentHasher().ComputeHash(content);

        var existingDoc = new CompoundDocument
        {
            Id = "doc-123",
            RelativePath = "unchanged-doc.md",
            ContentHash = contentHash
        };

        var mockFileSystem = new Mock<IFileSystemService>();
        mockFileSystem.Setup(fs => fs.EnumerateFiles(It.IsAny<string>(), "*.md", SearchOption.AllDirectories))
            .Returns(new[] { "/project/csharp-compounding-docs/unchanged-doc.md" });
        mockFileSystem.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(content);

        var mockDocRepo = new Mock<IDocumentRepository>();
        mockDocRepo.Setup(r => r.GetAllByTenantAsync(It.IsAny<TenantContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CompoundDocument> { existingDoc });

        var mockIndexer = new Mock<IDocumentIndexer>();

        var service = new ReconciliationService(
            mockDocRepo.Object,
            new Mock<IChunkRepository>().Object,
            mockIndexer.Object,
            new Sha256ContentHasher(),
            mockFileSystem.Object,
            Mock.Of<ILogger<ReconciliationService>>());

        var context = new TenantContext { AbsolutePath = "/project" };

        // Act
        var result = await service.ReconcileAsync(context);

        // Assert
        Assert.Equal(1, result.FilesScanned);
        Assert.Equal(0, result.FilesIndexed);
        Assert.Equal(0, result.FilesUpdated);
        Assert.Equal(0, result.FilesDeleted);
        mockIndexer.Verify(i => i.IndexDocumentAsync(It.IsAny<DiskFile>(), It.IsAny<TenantContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Sha256ContentHasher_ProducesDeterministicHash()
    {
        var hasher = new Sha256ContentHasher();
        var content = "Test content for hashing";

        var hash1 = hasher.ComputeHash(content);
        var hash2 = hasher.ComputeHash(content);

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length); // SHA256 produces 64 hex characters
    }
}
```

### Integration Tests

```csharp
[Trait("Category", "Integration")]
public class ReconciliationIntegrationTests
{
    [Fact]
    public async Task ReconcileAsync_RealFileSystem_ProcessesDocuments()
    {
        // Arrange - Create test directory with markdown files
        using var tempDir = new TempDirectory();
        var docsDir = Path.Combine(tempDir.Path, "csharp-compounding-docs");
        Directory.CreateDirectory(docsDir);
        File.WriteAllText(Path.Combine(docsDir, "test-doc.md"), "# Test\nContent");

        // Set up real database connection
        var services = new ServiceCollection();
        services.AddReconciliationServices();
        services.AddTestDatabaseContext(); // Test helper
        var provider = services.BuildServiceProvider();

        var service = provider.GetRequiredService<IReconciliationService>();
        var context = new TenantContext
        {
            ProjectName = "test-project",
            BranchName = "main",
            AbsolutePath = tempDir.Path
        };

        // Act
        var result = await service.ReconcileAsync(context);

        // Assert
        Assert.Equal(1, result.FilesScanned);
        Assert.Equal(1, result.FilesIndexed);
    }

    [Fact]
    public async Task ReconcileAsync_LargeRepository_MeetsPerformanceTarget()
    {
        // Arrange - Create 100+ files
        using var tempDir = new TempDirectory();
        var docsDir = Path.Combine(tempDir.Path, "csharp-compounding-docs");
        Directory.CreateDirectory(docsDir);

        for (int i = 0; i < 100; i++)
        {
            File.WriteAllText(
                Path.Combine(docsDir, $"doc-{i:D3}.md"),
                $"# Document {i}\nSome content here");
        }

        // ... setup service ...

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await service.ReconcileAsync(context);
        stopwatch.Stop();

        // Assert - Should complete within 1 second for <100 docs
        // Note: Actual timing depends on whether Ollama embeddings are mocked
        Assert.Equal(100, result.FilesScanned);
        // Performance assertion (if embeddings mocked):
        // Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.Core/Reconciliation/IReconciliationService.cs` | Create | Reconciliation service interface |
| `src/CompoundDocs.Core/Reconciliation/ReconciliationService.cs` | Create | Main reconciliation implementation |
| `src/CompoundDocs.Core/Reconciliation/ReconciliationResult.cs` | Create | Result/progress DTOs |
| `src/CompoundDocs.Core/Reconciliation/IExternalDocsReconciliationService.cs` | Create | External docs reconciliation interface |
| `src/CompoundDocs.Core/Reconciliation/ExternalDocsReconciliationService.cs` | Create | External docs reconciliation |
| `src/CompoundDocs.Core/Hashing/IContentHasher.cs` | Create | Content hasher interface |
| `src/CompoundDocs.Core/Hashing/Sha256ContentHasher.cs` | Create | SHA256 implementation |
| `src/CompoundDocs.Core/Models/DiskFile.cs` | Create | Disk file DTO |
| `src/CompoundDocs.Core/Extensions/ReconciliationServiceExtensions.cs` | Create | DI registration |
| `src/CompoundDocs.McpServer/Tools/ActivateProjectToolHandler.cs` | Modify | Add reconciliation integration |
| `tests/CompoundDocs.Core.Tests/Reconciliation/ReconciliationServiceTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.Integration.Tests/Reconciliation/ReconciliationIntegrationTests.cs` | Create | Integration tests |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Slow reconciliation for large repos | Performance optimization options, progress reporting, batch operations |
| Embedding generation failures | Continue reconciliation with errors list, don't block on individual failures |
| Race condition with file watcher | File watcher should be paused during reconciliation |
| Memory pressure from large file reads | Stream large files, process in batches |
| Database connection exhaustion | Connection pooling, batch operations |
| External docs path changes mid-reconciliation | Use snapshot of config at start of reconciliation |

---

## Verification Checklist

Before marking this phase complete:

1. [ ] `IReconciliationService` interface implemented
2. [ ] Disk scanning discovers all `.md` files in compounding docs directory
3. [ ] New files (on disk, not in DB) are indexed with embeddings and chunks
4. [ ] Orphaned records (in DB, not on disk) are deleted with their chunks
5. [ ] Modified files (hash mismatch) are re-indexed with chunk regeneration
6. [ ] Progress reporting works via `IProgress<ReconciliationProgress>`
7. [ ] `ReconciliationResult` contains accurate summary statistics
8. [ ] External docs reconciliation works when `external_docs` configured
9. [ ] Error handling continues reconciliation despite individual file failures
10. [ ] Performance meets spec targets for typical document counts
11. [ ] Unit tests achieve 80%+ code coverage
12. [ ] Integration tests verify end-to-end reconciliation
13. [ ] Logging provides appropriate visibility into reconciliation operations
