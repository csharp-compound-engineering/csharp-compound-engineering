# Phase 013: Cleanup App Deletion Operations

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Infrastructure Setup
> **Prerequisites**: Phase 012

---

## Spec References

This phase implements the deletion and safety features defined in:

- **spec/infrastructure/cleanup-app.md** - [Safety Features](../spec/infrastructure/cleanup-app.md#safety-features)
- **spec/infrastructure/cleanup-app.md** - [Implementation](../spec/infrastructure/cleanup-app.md#implementation) (cascade deletion logic)
- **spec/infrastructure/cleanup-app.md** - [Configuration](../spec/infrastructure/cleanup-app.md#configuration) (DryRun option)

---

## Objectives

1. Implement safe deletion workflow with dry-run preview capability
2. Implement database cascade deletion for orphaned paths and branches
3. Create deletion logging and audit trail system
4. Add grace period implementation for newly orphaned items
5. Implement error handling and rollback for deletion operations

---

## Acceptance Criteria

### Dry-Run Preview

- [ ] `--dry-run` command-line flag is supported
- [ ] `DryRun` configuration option in appsettings.json is supported
- [ ] Dry-run mode logs all items that would be deleted without actually deleting
- [ ] Dry-run output clearly distinguishes paths vs branches vs documents vs chunks
- [ ] Dry-run summary shows total counts of items that would be deleted

### Database Cascade Deletion

- [ ] `DeleteOrphanedPathAsync` method implements proper deletion order:
  - [ ] 1. Delete all chunks with matching `path_hash`
  - [ ] 2. Delete all documents with matching `path_hash`
  - [ ] 3. Delete the path record from `tenant_management.repo_paths`
- [ ] `DeleteOrphanedBranchAsync` method implements proper deletion order:
  - [ ] 1. Delete all chunks with matching `(project_name, branch_name)`
  - [ ] 2. Delete all documents with matching `(project_name, branch_name)`
  - [ ] 3. Delete the branch record from `tenant_management.branches`
- [ ] All deletions within a single orphan use a database transaction
- [ ] Deletion methods use parameterized queries to prevent SQL injection

### Deletion Logging and Audit Trail

- [ ] `ICleanupAuditLogger` interface defined with logging methods
- [ ] `CleanupAuditLogger` implementation logs to structured logging (Serilog)
- [ ] Each deletion logs:
  - [ ] Timestamp (UTC)
  - [ ] Deletion type (Path, Branch, Document, Chunk)
  - [ ] Entity identifier (path, branch name, document ID, chunk ID)
  - [ ] Associated project name
  - [ ] Reason for deletion (e.g., "Directory not found", "Branch not on remote")
- [ ] Summary log entry at end of each cleanup cycle with counts
- [ ] Log level is `Information` for deletions, `Warning` for errors

### Grace Period Implementation

- [ ] `GracePeriodMinutes` configuration option (default: 0 = no grace period)
- [ ] `orphan_detected_at` tracking column or in-memory tracking
- [ ] Items only deleted after grace period elapsed since first detection
- [ ] Grace period resets if item becomes valid again (path exists, branch returns)
- [ ] Grace period state persists across cleanup cycles (consider database tracking)

### Error Handling and Rollback

- [ ] Each orphan deletion wrapped in try-catch
- [ ] Failed deletion transaction is rolled back
- [ ] Error logged with full exception details
- [ ] Cleanup continues with next item after failure (no cascade failures)
- [ ] Partial failures don't prevent reporting of successful deletions
- [ ] Final summary includes success and failure counts

---

## Implementation Notes

### Deletion Service Interface

```csharp
// Services/ICleanupDeletionService.cs
public interface ICleanupDeletionService
{
    Task<DeletionResult> DeleteOrphanedPathAsync(
        OrphanedPath path,
        bool dryRun,
        CancellationToken ct);

    Task<DeletionResult> DeleteOrphanedBranchAsync(
        OrphanedBranch branch,
        bool dryRun,
        CancellationToken ct);
}

public record DeletionResult(
    bool Success,
    int DocumentsDeleted,
    int ChunksDeleted,
    string? ErrorMessage);
```

### Cascade Deletion Implementation

```csharp
// Services/CleanupDeletionService.cs
public class CleanupDeletionService : ICleanupDeletionService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ICleanupAuditLogger _auditLogger;

    public async Task<DeletionResult> DeleteOrphanedPathAsync(
        OrphanedPath path,
        bool dryRun,
        CancellationToken ct)
    {
        if (dryRun)
        {
            var counts = await GetDeletionCountsForPathAsync(path.PathHash, ct);
            _auditLogger.LogDryRunPath(path, counts);
            return new DeletionResult(true, counts.Documents, counts.Chunks, null);
        }

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            // Delete chunks first (they reference documents)
            var chunksDeleted = await DeleteChunksByPathHashAsync(
                connection, transaction, path.PathHash, ct);

            // Delete documents (they reference paths)
            var docsDeleted = await DeleteDocumentsByPathHashAsync(
                connection, transaction, path.PathHash, ct);

            // Delete the path record
            await DeletePathRecordAsync(
                connection, transaction, path.Id, ct);

            await transaction.CommitAsync(ct);

            _auditLogger.LogDeletedPath(path, docsDeleted, chunksDeleted);

            return new DeletionResult(true, docsDeleted, chunksDeleted, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _auditLogger.LogDeletionError(path, ex);
            return new DeletionResult(false, 0, 0, ex.Message);
        }
    }

    private async Task<int> DeleteChunksByPathHashAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        string pathHash,
        CancellationToken ct)
    {
        const string sql = @"
            DELETE FROM chunks
            WHERE path_hash = @pathHash";

        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("pathHash", pathHash);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    // Similar implementations for documents, branches, etc.
}
```

### Audit Logger Implementation

```csharp
// Services/CleanupAuditLogger.cs
public interface ICleanupAuditLogger
{
    void LogDryRunPath(OrphanedPath path, DeletionCounts counts);
    void LogDryRunBranch(OrphanedBranch branch, DeletionCounts counts);
    void LogDeletedPath(OrphanedPath path, int docsDeleted, int chunksDeleted);
    void LogDeletedBranch(OrphanedBranch branch, int docsDeleted, int chunksDeleted);
    void LogDeletionError(object orphan, Exception ex);
    void LogCleanupSummary(CleanupSummary summary);
}

public class CleanupAuditLogger : ICleanupAuditLogger
{
    private readonly ILogger<CleanupAuditLogger> _logger;

    public void LogDeletedPath(OrphanedPath path, int docsDeleted, int chunksDeleted)
    {
        _logger.LogInformation(
            "Deleted orphaned path: {PathId} ({AbsolutePath}). " +
            "Removed {DocumentCount} documents and {ChunkCount} chunks. " +
            "Reason: {Reason}",
            path.Id,
            path.AbsolutePath,
            docsDeleted,
            chunksDeleted,
            "Directory not found on disk");
    }

    public void LogCleanupSummary(CleanupSummary summary)
    {
        _logger.LogInformation(
            "Cleanup cycle completed. " +
            "Paths: {PathsDeleted} deleted, {PathsFailed} failed. " +
            "Branches: {BranchesDeleted} deleted, {BranchesFailed} failed. " +
            "Total documents: {DocsDeleted}, chunks: {ChunksDeleted}",
            summary.PathsDeleted,
            summary.PathsFailed,
            summary.BranchesDeleted,
            summary.BranchesFailed,
            summary.TotalDocumentsDeleted,
            summary.TotalChunksDeleted);
    }
}
```

### Grace Period Tracking

```csharp
// Models/GracePeriodTracker.cs
public class GracePeriodTracker
{
    private readonly Dictionary<string, DateTime> _firstDetectedTimes = new();
    private readonly TimeSpan _gracePeriod;

    public GracePeriodTracker(TimeSpan gracePeriod)
    {
        _gracePeriod = gracePeriod;
    }

    public bool IsReadyForDeletion(string orphanKey)
    {
        if (_gracePeriod == TimeSpan.Zero)
            return true;

        if (!_firstDetectedTimes.TryGetValue(orphanKey, out var firstDetected))
        {
            _firstDetectedTimes[orphanKey] = DateTime.UtcNow;
            return false;
        }

        return DateTime.UtcNow - firstDetected >= _gracePeriod;
    }

    public void MarkAsValid(string orphanKey)
    {
        _firstDetectedTimes.Remove(orphanKey);
    }
}
```

### Configuration Options

```csharp
// Configuration/CleanupOptions.cs (additions)
public class CleanupOptions
{
    public int IntervalMinutes { get; set; } = 60;
    public bool CheckRemoteBranches { get; set; } = true;
    public bool DryRun { get; set; } = false;
    public int GracePeriodMinutes { get; set; } = 0;
}
```

### Command-Line Arguments

```csharp
// Program.cs additions for command-line parsing
var dryRunFromArgs = args.Contains("--dry-run");

// Override configuration with command-line flag
services.PostConfigure<CleanupOptions>(options =>
{
    if (dryRunFromArgs)
        options.DryRun = true;
});
```

### Summary Models

```csharp
// Models/CleanupSummary.cs
public record CleanupSummary(
    int PathsDeleted,
    int PathsFailed,
    int BranchesDeleted,
    int BranchesFailed,
    int TotalDocumentsDeleted,
    int TotalChunksDeleted,
    TimeSpan Duration);

public record DeletionCounts(int Documents, int Chunks);
```

---

## Dependencies

### Depends On

- **Phase 012**: Cleanup App Orphan Detection (provides orphan detection logic and models)

### Blocks

- **Phase 014**: Cleanup App Integration and Testing (requires deletion operations)

---

## Verification Steps

After completing this phase, verify:

1. **Dry-run mode**: Run `./CompoundDocs.Cleanup --once --dry-run` and verify:
   - No actual deletions occur
   - Output shows what would be deleted
   - Counts are accurate

2. **Cascade deletion**: Create test orphaned path with documents/chunks:
   - Verify chunks deleted first
   - Verify documents deleted second
   - Verify path record deleted last
   - Verify single transaction (all-or-nothing)

3. **Audit logging**: Check logs after deletion:
   - Each deletion has a log entry
   - Summary appears at end of cycle
   - Timestamps and identifiers are correct

4. **Grace period**: Configure 5-minute grace period:
   - Create orphaned path
   - Run cleanup - item should NOT be deleted
   - Wait 5 minutes, run again - item SHOULD be deleted

5. **Error handling**: Simulate database error:
   - Verify transaction is rolled back
   - Verify error is logged
   - Verify cleanup continues with other items
   - Verify summary includes failure count

6. **Rollback test**: Interrupt deletion mid-transaction:
   - Verify no partial deletions remain
   - Verify data integrity

---

## Notes

- Grace period tracking uses in-memory storage by default; for multi-instance deployments, consider database-backed tracking
- The deletion order (chunks -> documents -> path/branch) is critical due to foreign key relationships
- Consider adding a `--force` flag to bypass grace period for manual cleanup operations
- For production, consider adding metrics/telemetry for monitoring deletion rates and failures
- The dry-run mode is essential for operators to preview impact before enabling automatic cleanup
