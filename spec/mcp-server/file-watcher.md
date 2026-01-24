# File Watcher Service

> **Status**: [DRAFT]
> **Parent**: [../mcp-server.md](../mcp-server.md)

> **Background**: Comprehensive coverage of `System.IO.FileSystemWatcher` for RAG embedding synchronization, including debouncing patterns, event handling, recursive watching, initial synchronization algorithms, and cross-platform considerations. See [.NET FileSystemWatcher for RAG Embedding Synchronization](../../research/dotnet-file-watcher-embeddings-research.md).

---

## Purpose

The File Watcher Service maintains synchronization between the disk (`./csharp-compounding-docs/`) and the vector database. It ensures the database always reflects the current state of documentation files.

---

## Implementation

### Technology

- **Class**: `System.IO.FileSystemWatcher`
- **Target**: `./csharp-compounding-docs/` directory
- **Mode**: Recursive (watches all subdirectories)
- **Debounce**: Default 500ms (see [configuration.md](../configuration.md#file-watcher-settings) for details)

### Lifecycle

1. **Start**: Watcher is started when a project is activated via `activate_project` tool
2. **Stop**: Watcher is stopped when a new project is activated (previous project deactivated)
3. **Scope**: Watches only the `./csharp-compounding-docs/` directory for the active project

> **Background**: The file watcher service is implemented as a `BackgroundService` using .NET's hosted service patterns. See [IHostedService and BackgroundService Patterns in .NET](../../research/hosted-services-background-tasks.md).

---

## Events

| Event | Action |
|-------|--------|
| File Created | Generate embedding, insert to DB |
| File Modified | Re-generate embedding, upsert to DB |
| File Deleted | Remove from DB |
| File Renamed | Update path in DB, keep embedding if content unchanged |

### Event Processing

```
File Change Detected
        |
        v
   Debounce (500ms)
        |
        v
   Parse Markdown
        |
        v
   Validate Schema
        |
        v
   Generate Embedding (Ollama)
        |
        v
   Upsert to Database
        |
        v
   Update Chunks (if applicable)
```

> **Background**: Embedding generation uses Semantic Kernel with Ollama. See [Semantic Kernel + Ollama RAG Research](../../research/semantic-kernel-ollama-rag-research.md) for model selection, batch embedding patterns, and performance optimization.

### Debouncing

Rapid file changes are debounced to prevent excessive processing:

- **Default interval**: 500ms
- **Configurable**: Via [configuration.md](../configuration.md#file-watcher-settings)
- **Behavior**: Only the final state after debounce window is processed
- **Rationale**: Editors often write files multiple times in quick succession (auto-save, formatting)

---

## Sync on Activation (Startup Reconciliation)

When a project is activated, the server performs a full reconciliation between disk and database.

### Reconciliation Steps

1. List all `.md` files in `./csharp-compounding-docs/`
2. Compare with DB records (by path and content hash)
3. Add missing files (new on disk, not in DB)
4. Remove orphaned DB records (in DB, not on disk)
5. Update changed files (content hash mismatch)
6. Regenerate chunks for modified documents >500 lines

### Algorithm

```
For each file on disk:
    If not in DB:
        -> INDEX (new file)
    Else if content hash differs:
        -> UPDATE (modified file)
    Else:
        -> SKIP (unchanged)

For each record in DB:
    If file not on disk:
        -> DELETE (orphaned record)
```

### Performance Considerations

| Document Count | Expected Reconciliation Time |
|----------------|------------------------------|
| < 100 | < 1 second |
| 100-500 | 1-5 seconds |
| 500-1000 | 5-15 seconds |
| > 1000 | Consider batching |

**Note**: Reconciliation time depends heavily on how many documents need embedding regeneration. Hash comparisons are fast; Ollama embedding calls dominate processing time.

---

## Crash Recovery

The reconciliation approach eliminates the need for a persistent change queue.

### Crash Scenario

If the MCP server crashes while processing file watcher events:
- Pending changes are lost from the in-memory queue
- On next activation, reconciliation detects any disk/DB drift and corrects it
- The file system is always the source of truth

### Rationale

Startup reconciliation is simpler than persistent queuing and equally reliable since the file system is authoritative. The reconciliation cost on activation is acceptable given typical document counts (<1000 documents).

### Partial Write Recovery

If a crash occurs mid-write to the database:
- Document may be partially indexed (parent without chunks, or vice versa)
- Reconciliation detects content hash mismatch and re-indexes
- Transactions ensure atomic operations where possible (see [../mcp-server.md](../mcp-server.md#service-interfaces))

---

## External Docs

The same reconciliation applies to `external_docs` paths if configured.

### Differences from Compounding Docs

| Aspect | Compounding Docs | External Docs |
|--------|------------------|---------------|
| Directory | `./csharp-compounding-docs/` | Configurable via `external_docs` |
| Write Access | Read/Write | Read-only |
| Promotion | Supported | Not supported |
| File Watcher | Always active | Only if configured |

### External Docs Index Rebuild

The external docs index is rebuilt when:
- Project is activated
- `external_docs` path changes in config
- File watcher detects changes in external docs folder

---

## Concurrency Control

**Approach**: Last-write-wins (no file locking)

The MCP server uses a **last-write-wins** strategy for document modifications rather than optimistic locking.

### No OS-Level File Locks

The MCP server does **not** acquire file locks when reading or writing documents. File watchers monitor for changes and reconcile the database accordingly.

### Rationale

- **Chunking complexity**: Documents >500 lines are split into multiple chunks, each with its own database record and embedding. Optimistic locking would require coordinating hash checks across the parent document and all associated chunks, significantly increasing implementation complexity.
- **File system as source of truth**: The markdown files on disk are the authoritative source. The database is a derived index that can be rebuilt from disk at any time.
- **Single-user typical usage**: Compounding docs are typically edited by a single developer in a single Claude Code session. Concurrent edits are rare in practice.
- **File watcher reconciliation**: The file watcher service detects changes and re-syncs, ensuring the database eventually reflects the latest disk state.

### Implications

- If two processes modify the same document simultaneously, the last write to disk wins
- The file watcher will detect the final state and update the database accordingly
- Chunks are always regenerated from the current file content during sync

### Future Consideration

If concurrent editing becomes a common use case, optimistic locking could be added at the file level (not chunk level) using the document's `ContentHash`.

---

## Error Handling

### File System Errors

| Error | Handling |
|-------|----------|
| File not found (after event) | Log warning, skip (file may have been deleted) |
| Permission denied | Log error, retry once, then skip |
| Path too long | Log error, skip file |
| Invalid UTF-8 | Log warning, skip file |

### Processing Errors

| Error | Handling |
|-------|----------|
| Schema validation failure | Log warning, skip indexing |
| Embedding generation failure | Queue for retry (see [ollama-integration.md](./ollama-integration.md)) |
| Database write failure | Retry with backoff, then queue for reconciliation |

### Logging

File watcher events are logged at the following levels:

| Level | Events |
|-------|--------|
| Debug | All file events (before debounce) |
| Info | Index/update/delete operations |
| Warning | Schema validation failures, recoverable errors |
| Error | Unrecoverable errors (permission denied, etc.) |

---

## Related Files

- [tools.md](./tools.md) - MCP tools including `activate_project`
- [chunking.md](./chunking.md) - Document chunking for large files
- [ollama-integration.md](./ollama-integration.md) - Embedding generation
- [database-schema.md](./database-schema.md) - Database records managed by file watcher
- [../configuration.md](../configuration.md) - File watcher configuration options
