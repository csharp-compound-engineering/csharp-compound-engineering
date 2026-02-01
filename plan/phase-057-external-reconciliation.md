# Phase 057: External Docs Reconciliation

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: Document Processing
> **Prerequisites**: Phase 056 (Compounding Docs Reconciliation), Phase 049 (File Watcher Service)

---

## Spec References

This phase implements external documentation reconciliation defined in:

- **spec/mcp-server/file-watcher.md** - [External Docs](../spec/mcp-server/file-watcher.md#external-docs) (lines 147-167)
- **spec/configuration.md** - [External Documentation](../spec/configuration.md#external-documentation-optional) (lines 266-298)

---

## Objectives

1. Implement reconciliation logic for external documentation paths
2. Enforce read-only indexing (no modification of external files)
3. Validate external_docs path configuration at activation time
4. Maintain separate collection for external docs in database
5. Detect and handle external docs path changes in configuration
6. Apply include/exclude glob patterns for file filtering

---

## Acceptance Criteria

### Path Validation at Activation

- [ ] External docs path validated when project is activated
- [ ] Validation confirms path exists and is a directory
- [ ] Validation rejects non-existent paths with clear error message: `"External docs path '{path}' does not exist"`
- [ ] Relative paths resolved from repository root (directory containing `.git/`)
- [ ] Absolute paths supported without transformation
- [ ] Null/empty path gracefully skipped (external docs disabled)

### Read-Only Indexing

- [ ] No file modification operations on external docs
- [ ] No promotion operations available for external docs
- [ ] File watcher does not trigger write operations to external paths
- [ ] External docs flagged with `IsReadOnly = true` in database
- [ ] Tools that modify documents reject external doc paths with `EXTERNAL_DOC_READ_ONLY` error

### Separate Collection Sync

- [ ] External docs stored in separate database collection/table partition
- [ ] Collection identified by `ExternalDocs` collection type
- [ ] Reconciliation only compares against external docs collection (not compounding docs)
- [ ] Separate content hash tracking for external docs
- [ ] Collection cleared and rebuilt on external_docs path change

### Reconciliation Algorithm

- [ ] Same reconciliation logic as compounding docs (INDEX/UPDATE/DELETE)
- [ ] Full reconciliation on project activation if external_docs configured
- [ ] Content hash comparison for change detection
- [ ] Orphaned external doc records removed when files deleted
- [ ] Modified external files re-indexed with new embedding

### Glob Pattern Filtering

- [ ] `include_patterns` applied to select files (default: `["**/*.md"]`)
- [ ] `exclude_patterns` applied to filter out files (default: `["**/node_modules/**"]`)
- [ ] Exclude patterns take precedence over include patterns
- [ ] Patterns matched against relative path from external docs root
- [ ] Pattern changes trigger full re-reconciliation

### External Path Change Detection

- [ ] Detect when `external_docs.path` changes in config
- [ ] Clear external docs collection on path change
- [ ] Re-index all files from new path
- [ ] Detect when include/exclude patterns change
- [ ] Re-apply patterns and reconcile on pattern change

---

## Implementation Notes

### External Docs Repository Interface

```csharp
/// <summary>
/// Repository for external documentation with read-only constraints.
/// </summary>
/// <remarks>
/// Thread Safety: Implementations must be thread-safe for concurrent read operations.
/// External docs are indexed for search but never modified through this interface.
/// </remarks>
public interface IExternalDocsRepository
{
    /// <summary>
    /// Gets all external docs for comparison during reconciliation.
    /// </summary>
    Task<IReadOnlyList<ExternalDocRecord>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Indexes a new external document (read from disk, store embedding).
    /// </summary>
    Task IndexAsync(ExternalDocRecord record, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing external document's embedding.
    /// </summary>
    Task UpdateAsync(ExternalDocRecord record, CancellationToken ct = default);

    /// <summary>
    /// Removes an external document from the index.
    /// </summary>
    Task DeleteAsync(string relativePath, CancellationToken ct = default);

    /// <summary>
    /// Removes all external documents (for path change scenarios).
    /// </summary>
    Task ClearAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets external document by path for search results.
    /// </summary>
    Task<ExternalDocRecord?> GetByPathAsync(string relativePath, CancellationToken ct = default);
}

/// <summary>
/// Record representing an indexed external document.
/// </summary>
public record ExternalDocRecord
{
    public required string RelativePath { get; init; }
    public required string AbsolutePath { get; init; }
    public required string ContentHash { get; init; }
    public required ReadOnlyMemory<float> Embedding { get; init; }
    public required DateTime IndexedAt { get; init; }
    public bool IsReadOnly => true;  // Always true for external docs
}
```

### External Docs Reconciliation Service

```csharp
/// <summary>
/// Reconciles external documentation folder with database index.
/// </summary>
public class ExternalDocsReconciliationService
{
    private readonly IExternalDocsRepository _repository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IFileService _fileService;
    private readonly IGlobMatcher _globMatcher;
    private readonly ILogger<ExternalDocsReconciliationService> _logger;

    public ExternalDocsReconciliationService(
        IExternalDocsRepository repository,
        IEmbeddingService embeddingService,
        IFileService fileService,
        IGlobMatcher globMatcher,
        ILogger<ExternalDocsReconciliationService> logger)
    {
        _repository = repository;
        _embeddingService = embeddingService;
        _fileService = fileService;
        _globMatcher = globMatcher;
        _logger = logger;
    }

    /// <summary>
    /// Performs full reconciliation of external docs folder.
    /// </summary>
    public async Task<ReconciliationResult> ReconcileAsync(
        ExternalDocsOptions options,
        CancellationToken ct = default)
    {
        var result = new ReconciliationResult();

        // Get files on disk matching patterns
        var filesOnDisk = await GetMatchingFilesAsync(options, ct);
        var diskFileSet = filesOnDisk.ToDictionary(f => f.RelativePath, f => f);

        // Get current database records
        var dbRecords = await _repository.GetAllAsync(ct);
        var dbRecordSet = dbRecords.ToDictionary(r => r.RelativePath);

        // Process files on disk
        foreach (var (relativePath, fileInfo) in diskFileSet)
        {
            if (!dbRecordSet.TryGetValue(relativePath, out var dbRecord))
            {
                // New file - INDEX
                await IndexNewFileAsync(fileInfo, result, ct);
            }
            else if (dbRecord.ContentHash != fileInfo.ContentHash)
            {
                // Modified file - UPDATE
                await UpdateExistingFileAsync(fileInfo, result, ct);
            }
            else
            {
                // Unchanged - SKIP
                result.SkippedCount++;
            }
        }

        // Remove orphaned records
        foreach (var dbRecord in dbRecords)
        {
            if (!diskFileSet.ContainsKey(dbRecord.RelativePath))
            {
                // File deleted - DELETE
                await _repository.DeleteAsync(dbRecord.RelativePath, ct);
                result.DeletedCount++;
                _logger.LogInformation(
                    "Removed orphaned external doc: {Path}",
                    dbRecord.RelativePath);
            }
        }

        return result;
    }

    private async Task<IReadOnlyList<ExternalFileInfo>> GetMatchingFilesAsync(
        ExternalDocsOptions options,
        CancellationToken ct)
    {
        var allFiles = await _fileService.EnumerateFilesAsync(
            options.Path,
            recursive: true,
            ct);

        var matchingFiles = new List<ExternalFileInfo>();

        foreach (var file in allFiles)
        {
            var relativePath = Path.GetRelativePath(options.Path, file);

            // Check include patterns
            if (!_globMatcher.IsMatch(relativePath, options.IncludePatterns))
                continue;

            // Check exclude patterns
            if (_globMatcher.IsMatch(relativePath, options.ExcludePatterns))
                continue;

            var content = await _fileService.ReadFileAsync(file, ct);
            var contentHash = ComputeContentHash(content);

            matchingFiles.Add(new ExternalFileInfo(
                relativePath,
                file,
                contentHash,
                content));
        }

        return matchingFiles;
    }

    private async Task IndexNewFileAsync(
        ExternalFileInfo fileInfo,
        ReconciliationResult result,
        CancellationToken ct)
    {
        try
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                fileInfo.Content,
                ct);

            var record = new ExternalDocRecord
            {
                RelativePath = fileInfo.RelativePath,
                AbsolutePath = fileInfo.AbsolutePath,
                ContentHash = fileInfo.ContentHash,
                Embedding = embedding,
                IndexedAt = DateTime.UtcNow
            };

            await _repository.IndexAsync(record, ct);
            result.IndexedCount++;

            _logger.LogInformation(
                "Indexed external doc: {Path}",
                fileInfo.RelativePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index external doc: {Path}", fileInfo.RelativePath);
            result.ErrorCount++;
        }
    }

    private async Task UpdateExistingFileAsync(
        ExternalFileInfo fileInfo,
        ReconciliationResult result,
        CancellationToken ct)
    {
        try
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                fileInfo.Content,
                ct);

            var record = new ExternalDocRecord
            {
                RelativePath = fileInfo.RelativePath,
                AbsolutePath = fileInfo.AbsolutePath,
                ContentHash = fileInfo.ContentHash,
                Embedding = embedding,
                IndexedAt = DateTime.UtcNow
            };

            await _repository.UpdateAsync(record, ct);
            result.UpdatedCount++;

            _logger.LogInformation(
                "Updated external doc: {Path}",
                fileInfo.RelativePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update external doc: {Path}", fileInfo.RelativePath);
            result.ErrorCount++;
        }
    }

    private static string ComputeContentHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private record ExternalFileInfo(
        string RelativePath,
        string AbsolutePath,
        string ContentHash,
        string Content);
}
```

### Path Validation Service

```csharp
/// <summary>
/// Validates external documentation path configuration.
/// </summary>
public class ExternalDocsPathValidator
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ExternalDocsPathValidator> _logger;

    public ExternalDocsPathValidator(
        IFileSystem fileSystem,
        ILogger<ExternalDocsPathValidator> logger)
    {
        _fileSystem = fileSystem;
        _logger = logger;
    }

    /// <summary>
    /// Validates the external docs path and returns the resolved absolute path.
    /// </summary>
    /// <param name="path">The configured path (relative or absolute).</param>
    /// <param name="repositoryRoot">The repository root directory.</param>
    /// <returns>The resolved absolute path if valid.</returns>
    /// <exception cref="ExternalDocsPathException">Thrown if path is invalid.</exception>
    public string ValidateAndResolvePath(string? path, string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogDebug("External docs path not configured, skipping validation");
            throw new ExternalDocsPathException(
                ExternalDocsPathError.NotConfigured,
                "External docs path is not configured");
        }

        // Resolve relative paths from repository root
        var absolutePath = Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(repositoryRoot, path));

        _logger.LogDebug(
            "Resolved external docs path: {ConfiguredPath} -> {AbsolutePath}",
            path,
            absolutePath);

        // Validate path exists
        if (!_fileSystem.Directory.Exists(absolutePath))
        {
            throw new ExternalDocsPathException(
                ExternalDocsPathError.PathNotFound,
                $"External docs path '{path}' does not exist");
        }

        // Validate it's a directory (not a file)
        if (_fileSystem.File.Exists(absolutePath))
        {
            throw new ExternalDocsPathException(
                ExternalDocsPathError.NotADirectory,
                $"External docs path '{path}' is a file, not a directory");
        }

        return absolutePath;
    }
}

/// <summary>
/// Exception thrown when external docs path validation fails.
/// </summary>
public class ExternalDocsPathException : Exception
{
    public ExternalDocsPathError Error { get; }

    public ExternalDocsPathException(ExternalDocsPathError error, string message)
        : base(message)
    {
        Error = error;
    }
}

/// <summary>
/// Error codes for external docs path validation.
/// </summary>
public enum ExternalDocsPathError
{
    NotConfigured,
    PathNotFound,
    NotADirectory
}
```

### External Docs Options Model

```csharp
/// <summary>
/// Configuration options for external documentation indexing.
/// </summary>
public class ExternalDocsOptions
{
    /// <summary>
    /// The resolved absolute path to the external docs folder.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Glob patterns to include files for indexing.
    /// </summary>
    public IReadOnlyList<string> IncludePatterns { get; init; } = new[] { "**/*.md" };

    /// <summary>
    /// Glob patterns to exclude files from indexing.
    /// </summary>
    public IReadOnlyList<string> ExcludePatterns { get; init; } = new[] { "**/node_modules/**" };

    /// <summary>
    /// Computed hash of the configuration for change detection.
    /// </summary>
    public string ConfigHash => ComputeConfigHash();

    private string ComputeConfigHash()
    {
        var configString = $"{Path}|{string.Join(",", IncludePatterns)}|{string.Join(",", ExcludePatterns)}";
        var bytes = Encoding.UTF8.GetBytes(configString);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}
```

### Configuration Change Detection

```csharp
/// <summary>
/// Detects changes in external docs configuration.
/// </summary>
public class ExternalDocsConfigChangeDetector
{
    private readonly IProjectConfigStore _configStore;
    private readonly ILogger<ExternalDocsConfigChangeDetector> _logger;
    private string? _lastConfigHash;

    public ExternalDocsConfigChangeDetector(
        IProjectConfigStore configStore,
        ILogger<ExternalDocsConfigChangeDetector> logger)
    {
        _configStore = configStore;
        _logger = logger;
    }

    /// <summary>
    /// Checks if external docs configuration has changed since last check.
    /// </summary>
    /// <returns>True if configuration changed, false otherwise.</returns>
    public async Task<ConfigChangeResult> CheckForChangesAsync(
        string projectPath,
        CancellationToken ct = default)
    {
        var config = await _configStore.GetProjectConfigAsync(projectPath, ct);
        var externalDocs = config?.ExternalDocs;

        if (externalDocs?.Path == null)
        {
            // External docs not configured
            if (_lastConfigHash != null)
            {
                _lastConfigHash = null;
                return new ConfigChangeResult(Changed: true, Reason: "External docs disabled");
            }
            return new ConfigChangeResult(Changed: false, Reason: null);
        }

        var options = new ExternalDocsOptions
        {
            Path = externalDocs.Path,
            IncludePatterns = externalDocs.IncludePatterns ?? new[] { "**/*.md" },
            ExcludePatterns = externalDocs.ExcludePatterns ?? new[] { "**/node_modules/**" }
        };

        var currentHash = options.ConfigHash;

        if (_lastConfigHash == null)
        {
            _lastConfigHash = currentHash;
            return new ConfigChangeResult(Changed: true, Reason: "External docs newly configured");
        }

        if (_lastConfigHash != currentHash)
        {
            var previousHash = _lastConfigHash;
            _lastConfigHash = currentHash;

            _logger.LogInformation(
                "External docs config changed: {PreviousHash} -> {CurrentHash}",
                previousHash,
                currentHash);

            return new ConfigChangeResult(Changed: true, Reason: "Configuration modified");
        }

        return new ConfigChangeResult(Changed: false, Reason: null);
    }

    public record ConfigChangeResult(bool Changed, string? Reason);
}
```

### File Watcher Integration for External Docs

```csharp
/// <summary>
/// File watcher specifically for external documentation (read-only).
/// </summary>
public class ExternalDocsFileWatcher : IDisposable
{
    private readonly FileSystemWatcher? _watcher;
    private readonly ExternalDocsReconciliationService _reconciliationService;
    private readonly FileChangeDebouncer _debouncer;
    private readonly ExternalDocsOptions _options;
    private readonly ILogger<ExternalDocsFileWatcher> _logger;

    public ExternalDocsFileWatcher(
        ExternalDocsOptions options,
        ExternalDocsReconciliationService reconciliationService,
        ILogger<ExternalDocsFileWatcher> logger)
    {
        _options = options;
        _reconciliationService = reconciliationService;
        _logger = logger;

        // Create debouncer for external docs changes
        _debouncer = new FileChangeDebouncer(
            TimeSpan.FromMilliseconds(500),
            OnDebouncedChangeAsync,
            logger as ILogger<FileChangeDebouncer> ?? NullLogger<FileChangeDebouncer>.Instance);

        // Create file watcher
        _watcher = new FileSystemWatcher(options.Path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                         | NotifyFilters.LastWrite
                         | NotifyFilters.DirectoryName,
            EnableRaisingEvents = true
        };

        _watcher.Created += OnFileChanged;
        _watcher.Changed += OnFileChanged;
        _watcher.Deleted += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
        _watcher.Error += OnError;

        _logger.LogInformation(
            "Started watching external docs at: {Path}",
            options.Path);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!ShouldProcessFile(e.FullPath))
            return;

        var changeType = e.ChangeType switch
        {
            WatcherChangeTypes.Created => FileChangeType.Created,
            WatcherChangeTypes.Changed => FileChangeType.Modified,
            WatcherChangeTypes.Deleted => FileChangeType.Deleted,
            _ => FileChangeType.Modified
        };

        _debouncer.OnFileChanged(new FileChangeEvent(
            e.FullPath,
            changeType,
            DateTime.UtcNow));
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        // Treat rename as delete of old + create of new
        if (ShouldProcessFile(e.OldFullPath))
        {
            _debouncer.OnFileChanged(new FileChangeEvent(
                e.OldFullPath,
                FileChangeType.Deleted,
                DateTime.UtcNow));
        }

        if (ShouldProcessFile(e.FullPath))
        {
            _debouncer.OnFileChanged(new FileChangeEvent(
                e.FullPath,
                FileChangeType.Created,
                DateTime.UtcNow));
        }
    }

    private bool ShouldProcessFile(string absolutePath)
    {
        var relativePath = Path.GetRelativePath(_options.Path, absolutePath);

        // Check against glob patterns
        var globMatcher = new GlobMatcher();  // Assume available
        return globMatcher.IsMatch(relativePath, _options.IncludePatterns)
            && !globMatcher.IsMatch(relativePath, _options.ExcludePatterns);
    }

    private async Task OnDebouncedChangeAsync(FileChangeEvent evt, CancellationToken ct)
    {
        _logger.LogInformation(
            "Processing external doc change: {ChangeType} {Path}",
            evt.ChangeType,
            evt.FilePath);

        // Trigger incremental reconciliation for the changed file
        // Note: This is READ-ONLY - we only update the index, never the file
        await _reconciliationService.ReconcileAsync(_options, ct);
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(
            e.GetException(),
            "File watcher error for external docs");
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debouncer.Dispose();
    }
}
```

### Read-Only Enforcement

```csharp
/// <summary>
/// Guard service that prevents modification of external documents.
/// </summary>
public class ExternalDocGuard
{
    private readonly IExternalDocsRepository _repository;
    private readonly ILogger<ExternalDocGuard> _logger;

    public ExternalDocGuard(
        IExternalDocsRepository repository,
        ILogger<ExternalDocGuard> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Checks if a document path refers to an external document.
    /// </summary>
    public async Task<bool> IsExternalDocAsync(
        string documentPath,
        CancellationToken ct = default)
    {
        var record = await _repository.GetByPathAsync(documentPath, ct);
        return record != null;
    }

    /// <summary>
    /// Validates that a modification operation is allowed on a document.
    /// </summary>
    /// <exception cref="ExternalDocReadOnlyException">Thrown if document is external.</exception>
    public async Task ValidateModificationAllowedAsync(
        string documentPath,
        CancellationToken ct = default)
    {
        if (await IsExternalDocAsync(documentPath, ct))
        {
            _logger.LogWarning(
                "Attempted modification of external doc: {Path}",
                documentPath);

            throw new ExternalDocReadOnlyException(documentPath);
        }
    }
}

/// <summary>
/// Exception thrown when attempting to modify an external document.
/// </summary>
public class ExternalDocReadOnlyException : Exception
{
    public string DocumentPath { get; }

    public ExternalDocReadOnlyException(string documentPath)
        : base($"External document '{documentPath}' is read-only and cannot be modified")
    {
        DocumentPath = documentPath;
    }
}
```

### Error Response for External Doc Modification

```csharp
/// <summary>
/// Error response for external doc modification attempts.
/// </summary>
public static class ExternalDocErrors
{
    public static ErrorResponse ReadOnlyViolation(string documentPath) => new()
    {
        Error = true,
        Code = "EXTERNAL_DOC_READ_ONLY",
        Message = $"Cannot modify external document '{documentPath}'. External docs are indexed for search only.",
        Details = new Dictionary<string, object>
        {
            ["document_path"] = documentPath,
            ["is_external"] = true
        }
    };

    public static ErrorResponse PathNotFound(string path) => new()
    {
        Error = true,
        Code = "EXTERNAL_DOCS_PATH_NOT_FOUND",
        Message = $"External docs path '{path}' does not exist",
        Details = new Dictionary<string, object>
        {
            ["configured_path"] = path
        }
    };
}
```

### Service Registration

```csharp
public static class ExternalDocsServiceExtensions
{
    public static IServiceCollection AddExternalDocsServices(
        this IServiceCollection services)
    {
        services.AddScoped<IExternalDocsRepository, ExternalDocsRepository>();
        services.AddScoped<ExternalDocsReconciliationService>();
        services.AddScoped<ExternalDocsPathValidator>();
        services.AddScoped<ExternalDocsConfigChangeDetector>();
        services.AddScoped<ExternalDocGuard>();

        return services;
    }
}
```

---

## Test Cases

### Unit Tests

```csharp
[Fact]
public async Task PathValidator_RejectsNonExistentPath()
{
    // Arrange
    var fileSystem = new MockFileSystem();
    var validator = new ExternalDocsPathValidator(fileSystem, NullLogger<ExternalDocsPathValidator>.Instance);

    // Act & Assert
    var ex = Assert.Throws<ExternalDocsPathException>(
        () => validator.ValidateAndResolvePath("./nonexistent", "/repo/root"));

    Assert.Equal(ExternalDocsPathError.PathNotFound, ex.Error);
    Assert.Contains("does not exist", ex.Message);
}

[Fact]
public void PathValidator_ResolvesRelativePath()
{
    // Arrange
    var fileSystem = new MockFileSystem(new Dictionary<string, MockDirectoryData>
    {
        { "/repo/root/docs", new MockDirectoryData() }
    });
    var validator = new ExternalDocsPathValidator(fileSystem, NullLogger<ExternalDocsPathValidator>.Instance);

    // Act
    var result = validator.ValidateAndResolvePath("./docs", "/repo/root");

    // Assert
    Assert.Equal("/repo/root/docs", result);
}

[Fact]
public async Task Reconciliation_IndexesNewFiles()
{
    // Arrange
    var repository = new Mock<IExternalDocsRepository>();
    repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<ExternalDocRecord>());

    var service = CreateReconciliationService(repository.Object);
    var options = new ExternalDocsOptions { Path = "/external/docs" };

    // Act
    var result = await service.ReconcileAsync(options);

    // Assert
    Assert.True(result.IndexedCount > 0);
    repository.Verify(r => r.IndexAsync(It.IsAny<ExternalDocRecord>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
}

[Fact]
public async Task Reconciliation_RemovesOrphanedRecords()
{
    // Arrange
    var repository = new Mock<IExternalDocsRepository>();
    repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<ExternalDocRecord>
        {
            new() { RelativePath = "deleted.md", /* ... */ }
        });

    // File system returns empty (file was deleted)
    var service = CreateReconciliationService(repository.Object, emptyFileSystem: true);
    var options = new ExternalDocsOptions { Path = "/external/docs" };

    // Act
    var result = await service.ReconcileAsync(options);

    // Assert
    Assert.Equal(1, result.DeletedCount);
    repository.Verify(r => r.DeleteAsync("deleted.md", It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task ExternalDocGuard_PreventsModification()
{
    // Arrange
    var repository = new Mock<IExternalDocsRepository>();
    repository.Setup(r => r.GetByPathAsync("external.md", It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ExternalDocRecord { RelativePath = "external.md", /* ... */ });

    var guard = new ExternalDocGuard(repository.Object, NullLogger<ExternalDocGuard>.Instance);

    // Act & Assert
    await Assert.ThrowsAsync<ExternalDocReadOnlyException>(
        () => guard.ValidateModificationAllowedAsync("external.md"));
}

[Fact]
public async Task ConfigChangeDetector_DetectsPathChange()
{
    // Arrange
    var configStore = new Mock<IProjectConfigStore>();
    var detector = new ExternalDocsConfigChangeDetector(configStore.Object, NullLogger<ExternalDocsConfigChangeDetector>.Instance);

    // First call with path A
    configStore.Setup(c => c.GetProjectConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ProjectConfig { ExternalDocs = new() { Path = "/path/a" } });

    await detector.CheckForChangesAsync("/project");

    // Second call with path B
    configStore.Setup(c => c.GetProjectConfigAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new ProjectConfig { ExternalDocs = new() { Path = "/path/b" } });

    var result = await detector.CheckForChangesAsync("/project");

    // Assert
    Assert.True(result.Changed);
}

[Fact]
public void GlobPatterns_ExcludeTakesPrecedence()
{
    // Arrange
    var matcher = new GlobMatcher();
    var includePatterns = new[] { "**/*.md" };
    var excludePatterns = new[] { "**/node_modules/**" };

    // Act
    var shouldInclude = matcher.IsMatch("docs/readme.md", includePatterns);
    var shouldExcludeModules = matcher.IsMatch("node_modules/pkg/readme.md", excludePatterns);

    // Assert
    Assert.True(shouldInclude);
    Assert.True(shouldExcludeModules);
}
```

### Integration Tests

```csharp
[Fact]
public async Task EndToEnd_ExternalDocsReconciliation_WorksCorrectly()
{
    // Test full reconciliation flow with real file system and database
}

[Fact]
public async Task EndToEnd_FileWatcher_DetectsExternalDocChanges()
{
    // Test that file watcher correctly indexes new/modified external docs
}

[Fact]
public async Task EndToEnd_PathChange_ClearsAndRebuildsIndex()
{
    // Test that changing external_docs.path clears and rebuilds the index
}
```

---

## Dependencies

### Depends On

- Phase 056: Compounding Docs Reconciliation (shares reconciliation infrastructure)
- Phase 049: File Watcher Service (provides base file watcher infrastructure)
- Phase 029: Embedding Service (generates embeddings for external docs)
- Phase 040: Concurrency Model (debouncing, thread safety patterns)

### Blocks

- Phase 058+: Any phase requiring external docs search functionality
- Semantic search across external documentation
- RAG queries that include external docs collection

---

## Verification Steps

After completing this phase, verify:

1. **Path validation**: Invalid paths rejected with clear error message
2. **Relative path resolution**: Relative paths correctly resolved from repo root
3. **Read-only enforcement**: Modification attempts on external docs return EXTERNAL_DOC_READ_ONLY error
4. **Separate collection**: External docs stored in separate collection from compounding docs
5. **Reconciliation**: New/modified/deleted files correctly handled
6. **Glob patterns**: Include/exclude patterns correctly filter files
7. **Config change detection**: Path or pattern changes trigger full re-index

### Manual Verification

```bash
# Test 1: Path validation
# Configure invalid external_docs.path, activate project
# Should see: "External docs path 'invalid/path' does not exist"

# Test 2: Read-only enforcement
# Try to modify external doc via MCP tool
# Should see: EXTERNAL_DOC_READ_ONLY error

# Test 3: File watcher
# Create/modify/delete file in external docs folder
# Check logs for reconciliation activity

# Test 4: Pattern filtering
# Add file matching exclude pattern (e.g., node_modules)
# Should not be indexed
```

---

## Notes

- External docs are fundamentally different from compounding docs: they are read-only reference material maintained externally
- The separate collection ensures RAG queries for institutional knowledge don't get polluted with reference docs
- Path changes require full re-index because we can't determine which files moved vs. which are new
- Glob pattern matching uses Microsoft.Extensions.FileSystemGlobbing for consistency with .NET conventions
- The file watcher for external docs is optional and only starts if external_docs is configured
- Consider adding metrics for external docs indexing performance in future phases
