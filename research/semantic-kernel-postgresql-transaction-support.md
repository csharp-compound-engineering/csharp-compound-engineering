# Semantic Kernel PostgreSQL Transaction Support Research

> **Date**: 2025-01-24
> **Status**: Completed
> **Related Spec**: [mcp-server.md](../spec/mcp-server.md)

---

## Summary

**Semantic Kernel's PostgreSQL/pgvector connector does NOT support database transactions.** This has significant implications for atomic document + chunk indexing operations.

---

## Key Findings

### 1. Transaction Support: NOT AVAILABLE

The `PostgresCollection<TKey, TRecord>` and `PostgresVectorStore` do NOT support wrapping multiple operations in a database transaction. There is no API to:
- Pass a transaction object to the collection
- Begin/commit/rollback transactions through the collection API
- Specify transaction isolation levels

### 2. NpgsqlDataSource/NpgsqlConnection Transactions: NOT DIRECTLY SUPPORTED

The connector is designed around `NpgsqlDataSource`, not `NpgsqlConnection`. You cannot:
- Pass an `NpgsqlConnection` with an active transaction
- Access the internal connection used by the collection
- Control connection lifecycle for transactional purposes

### 3. Batch Operations: NOT ATOMIC

**GitHub Issue #10531** explicitly confirms that batch operations are NOT atomic:

> "to me this word [batch] suggests that all of the records will be added/deleted at once (in an atomic way), which is not true for some of the implementations like InMemoryVectorStore."

### 4. Unit of Work Pattern: NOT SUPPORTED

No unit of work abstraction exists in the Vector Store API design.

### 5. Manual Transaction Control: NOT POSSIBLE

The `IVectorStoreCollection` interface provides no transaction methods.

---

## Recommended Approach: Hybrid Architecture

Use raw Npgsql for transactional writes, Semantic Kernel for reads/search:

```csharp
public class DocumentRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly PostgresCollection<string, CompoundDocument> _searchCollection;

    // WRITES: Use raw Npgsql for transactional operations
    public async Task IndexDocumentAtomically(
        CompoundDocument document,
        IEnumerable<DocumentChunk> chunks,
        CancellationToken ct)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            // Upsert document
            await UpsertDocumentRaw(connection, document, ct);

            // Delete existing chunks for this document
            await DeleteChunksRaw(connection, document.Id, ct);

            // Insert new chunks
            foreach (var chunk in chunks)
            {
                await InsertChunkRaw(connection, chunk, ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    // READS: Use Semantic Kernel for vector search
    public async Task<IEnumerable<CompoundDocument>> SearchAsync(
        ReadOnlyMemory<float> embedding,
        int top,
        CancellationToken ct)
    {
        return await _searchCollection.VectorizedSearchAsync(embedding, new() { Top = top }, ct);
    }
}
```

---

## Alternative Workarounds

### Option A: TransactionScope (RISKY)

```csharp
using var scope = new TransactionScope(
    TransactionScopeOption.Required,
    new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
    TransactionScopeAsyncFlowOption.Enabled);

await documentsCollection.UpsertAsync(document, ct);
foreach (var chunk in chunks)
{
    await chunksCollection.UpsertAsync(chunk, ct);
}

scope.Complete();
```

**Warning**: Untested with SK; may create distributed transactions.

### Option B: Compensation Pattern

Accept eventual consistency:
1. Track indexing operations with status (pending/complete/failed)
2. Run background job to clean up orphaned data
3. Reconciliation on startup corrects any drift

---

## Impact on Spec

The spec's "Crash Recovery" section already handles this via reconciliation:
- Pending changes lost from in-memory queue on crash
- On next activation, reconciliation detects disk/DB drift and corrects it

However, the spec should be updated to:
1. Document that SK doesn't support transactions
2. Specify the hybrid approach (raw Npgsql for writes, SK for reads)
3. Note that partial writes are corrected by reconciliation

---

## Sources

- [Microsoft Learn - Postgres Vector Store Connector](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/postgres-connector)
- [GitHub Issue #10531 - IVectorStore API Design Feedback](https://github.com/microsoft/semantic-kernel/issues/10531)
- [ADR 0050 - Updated Vector Store Design](https://github.com/microsoft/semantic-kernel/blob/main/docs/decisions/0050-updated-vector-store-design.md)
- [Npgsql Basic Usage - Transaction Support](https://www.npgsql.org/doc/basic-usage.html)
