# Phase 046: HNSW Index Configuration

> **Category**: Database & Storage
> **Prerequisites**: Phase 041 (Semantic Kernel Vector Store Setup)
> **Estimated Effort**: Medium (4-6 hours)
> **Risk Level**: Low

---

## Overview

This phase establishes the HNSW (Hierarchical Navigable Small World) index configuration for vector similarity search in pgvector. The configuration is tuned for the expected data volume of 1,000-10,000 documents per tenant (project/branch combination), providing optimal balance between query performance, recall accuracy, and memory usage.

HNSW indexes create a multi-layer graph structure enabling fast approximate nearest neighbor (ANN) search, critical for RAG query performance.

---

## Objectives

1. Configure HNSW index parameters optimized for medium-scale deployments (m=32, ef_construction=128)
2. Implement query-time ef_search parameter setting (ef_search=64)
3. Establish index creation patterns via Semantic Kernel
4. Document performance rationale and scaling guidance
5. Configure index maintenance procedures

---

## Spec References

| Document | Section | Relevance |
|----------|---------|-----------|
| [spec/mcp-server/database-schema.md](/spec/mcp-server/database-schema.md) | HNSW Index Configuration | Parameter values and scale guidance |
| [research/postgresql-pgvector-research.md](/research/postgresql-pgvector-research.md) | Section 3: Vector Indexing with pgvector | HNSW theory and tuning guidance |
| [research/postgresql-pgvector-research.md](/research/postgresql-pgvector-research.md) | Section 8: Performance Optimization | Index tuning and maintenance |

---

## HNSW Parameter Configuration

### Selected Parameters (Medium Tier)

| Parameter | Value | Description | Rationale |
|-----------|-------|-------------|-----------|
| `m` | 32 | Max connections per node | Higher than default (16) for better recall with 1K-10K docs |
| `ef_construction` | 128 | Search depth during build | 4x `m` for high-quality graph construction |
| `ef_search` | 64 | Search depth during query | 2x `m` balances speed and recall |

### Parameter Effects

**`m` (Maximum Connections)**:
- Controls graph connectivity density
- Higher values improve recall but increase memory usage
- Memory impact: ~4 bytes per connection per vector
- For 10K vectors with m=32: ~1.3MB additional memory per index

**`ef_construction` (Build-time Search Depth)**:
- Determines quality of graph during index creation
- Higher values create better quality graphs but slower builds
- One-time cost during index creation or bulk inserts
- Recommended: 2x to 4x `m` value

**`ef_search` (Query-time Search Depth)**:
- Controls accuracy vs speed tradeoff at query time
- Higher values improve recall but increase latency
- Can be tuned per-query for different use cases
- Recommended: 1x to 2x `m` value for balanced performance

---

## Scale Guidance

### Configuration Tiers

| Scale | Document Count | m | ef_construction | ef_search | Use Case |
|-------|----------------|---|-----------------|-----------|----------|
| Small | < 1,000 | 16 | 64 | 40 | Single small project |
| **Medium** | 1K-10K | **32** | **128** | **64** | **Default (current)** |
| Large | 10K-100K | 48 | 200 | 100 | Large monorepo |
| Very Large | 100K+ | 64 | 256 | 128 | Consider partitioning |

### Why Medium Configuration?

The medium configuration (m=32, ef_construction=128, ef_search=64) is selected because:

1. **Expected Data Volume**: Most projects will have 1,000-10,000 compound documents
2. **Recall Requirements**: RAG queries need high recall (>95%) to surface relevant context
3. **Query Latency**: Sub-100ms queries expected with this configuration
4. **Memory Budget**: ~50-100MB index size acceptable for dedicated containers

### Scaling Considerations

- **Below 1K documents**: Consider using exact search (no index) for perfect recall
- **Above 10K documents per tenant**: Monitor query latency and adjust ef_search
- **Above 100K total documents**: Consider tenant-based partitioning strategies

---

## Implementation

### 1. Semantic Kernel Model Configuration

The HNSW index is specified in the model class via the `VectorStoreVector` attribute:

```csharp
// src/CompoundDocs.Common/Models/CompoundDocument.cs

[VectorStoreVector(
    Dimensions: 1024,
    DistanceFunction.CosineDistance,
    IndexKind.Hnsw,
    StorageName = "embedding")]
public ReadOnlyMemory<float>? Embedding { get; set; }
```

**Note**: Semantic Kernel's PostgreSQL connector uses default HNSW parameters. Custom parameters require post-creation index modification or direct SQL execution.

### 2. Custom Index Creation Service

Create a service to ensure HNSW indexes are created with optimal parameters:

```csharp
// src/CompoundDocs.McpServer/Services/HnswIndexService.cs

using Npgsql;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Service for managing HNSW index configuration and optimization.
/// </summary>
public interface IHnswIndexService
{
    /// <summary>
    /// Ensures HNSW indexes exist with optimal parameters for all vector collections.
    /// </summary>
    Task EnsureOptimalIndexesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the ef_search parameter for the current session.
    /// </summary>
    Task SetEfSearchAsync(NpgsqlConnection connection, int efSearch);

    /// <summary>
    /// Analyzes index health and reports statistics.
    /// </summary>
    Task<IndexHealthReport> GetIndexHealthAsync(CancellationToken cancellationToken = default);
}

public class HnswIndexService : IHnswIndexService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<HnswIndexService> _logger;

    // HNSW Configuration - Medium tier for 1K-10K documents
    private const int HnswM = 32;
    private const int HnswEfConstruction = 128;
    public const int DefaultEfSearch = 64;

    public HnswIndexService(NpgsqlDataSource dataSource, ILogger<HnswIndexService> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task EnsureOptimalIndexesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        // Check and recreate indexes for each collection
        var collections = new[] { "documents", "document_chunks", "external_documents", "external_document_chunks" };

        foreach (var collection in collections)
        {
            await EnsureCollectionIndexAsync(connection, collection, cancellationToken);
        }
    }

    private async Task EnsureCollectionIndexAsync(
        NpgsqlConnection connection,
        string collectionName,
        CancellationToken cancellationToken)
    {
        var indexName = $"{collectionName}_embedding_hnsw_idx";

        // Check if index exists with correct parameters
        var checkSql = @"
            SELECT
                i.relname as index_name,
                am.amname as index_type,
                array_to_string(array_agg(a.attname), ', ') as columns
            FROM pg_class t
            JOIN pg_index ix ON t.oid = ix.indrelid
            JOIN pg_class i ON i.oid = ix.indexrelid
            JOIN pg_am am ON i.relam = am.oid
            JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY(ix.indkey)
            WHERE t.relname = @collection
              AND am.amname = 'hnsw'
            GROUP BY i.relname, am.amname";

        await using var checkCmd = new NpgsqlCommand(checkSql, connection);
        checkCmd.Parameters.AddWithValue("collection", collectionName);

        var existingIndex = await checkCmd.ExecuteScalarAsync(cancellationToken);

        if (existingIndex != null)
        {
            _logger.LogDebug("HNSW index already exists for {Collection}", collectionName);
            return;
        }

        // Create HNSW index with optimal parameters
        _logger.LogInformation(
            "Creating HNSW index for {Collection} with m={M}, ef_construction={EfConstruction}",
            collectionName, HnswM, HnswEfConstruction);

        var createSql = $@"
            CREATE INDEX CONCURRENTLY IF NOT EXISTS {indexName}
            ON compounding.{collectionName}
            USING hnsw (embedding vector_cosine_ops)
            WITH (m = {HnswM}, ef_construction = {HnswEfConstruction})";

        await using var createCmd = new NpgsqlCommand(createSql, connection);
        createCmd.CommandTimeout = 600; // 10 minutes for large indexes

        try
        {
            await createCmd.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("HNSW index created for {Collection}", collectionName);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P07") // duplicate_object
        {
            _logger.LogDebug("HNSW index already exists for {Collection}", collectionName);
        }
    }

    public async Task SetEfSearchAsync(NpgsqlConnection connection, int efSearch)
    {
        // SET LOCAL affects only the current transaction
        await using var cmd = new NpgsqlCommand($"SET LOCAL hnsw.ef_search = {efSearch}", connection);
        await cmd.ExecuteNonQueryAsync();

        _logger.LogDebug("Set ef_search to {EfSearch} for current transaction", efSearch);
    }

    public async Task<IndexHealthReport> GetIndexHealthAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        var sql = @"
            SELECT
                t.relname as table_name,
                i.relname as index_name,
                pg_relation_size(i.oid) as index_size,
                idx_scan as scans,
                idx_tup_read as tuples_read,
                idx_tup_fetch as tuples_fetched
            FROM pg_class t
            JOIN pg_index ix ON t.oid = ix.indrelid
            JOIN pg_class i ON i.oid = ix.indexrelid
            JOIN pg_stat_user_indexes sui ON sui.indexrelid = i.oid
            JOIN pg_am am ON i.relam = am.oid
            WHERE am.amname = 'hnsw'
              AND t.relnamespace = (SELECT oid FROM pg_namespace WHERE nspname = 'compounding')
            ORDER BY t.relname";

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        var indexes = new List<IndexStats>();
        while (await reader.ReadAsync(cancellationToken))
        {
            indexes.Add(new IndexStats
            {
                TableName = reader.GetString(0),
                IndexName = reader.GetString(1),
                IndexSizeBytes = reader.GetInt64(2),
                ScanCount = reader.GetInt64(3),
                TuplesRead = reader.GetInt64(4),
                TuplesFetched = reader.GetInt64(5)
            });
        }

        return new IndexHealthReport { Indexes = indexes };
    }
}

public record IndexHealthReport
{
    public required List<IndexStats> Indexes { get; init; }

    public long TotalIndexSizeBytes => Indexes.Sum(i => i.IndexSizeBytes);
    public bool AllIndexesHealthy => Indexes.All(i => i.ScanCount > 0 || i.TuplesRead == 0);
}

public record IndexStats
{
    public required string TableName { get; init; }
    public required string IndexName { get; init; }
    public long IndexSizeBytes { get; init; }
    public long ScanCount { get; init; }
    public long TuplesRead { get; init; }
    public long TuplesFetched { get; init; }

    public string IndexSizePretty => IndexSizeBytes switch
    {
        < 1024 => $"{IndexSizeBytes} B",
        < 1024 * 1024 => $"{IndexSizeBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{IndexSizeBytes / (1024.0 * 1024):F1} MB",
        _ => $"{IndexSizeBytes / (1024.0 * 1024 * 1024):F2} GB"
    };
}
```

### 3. Query-Time ef_search Integration

Integrate ef_search setting into the vector search service:

```csharp
// Integration in VectorSearchService

public async Task<List<SearchResult>> SearchAsync(
    ReadOnlyMemory<float> queryEmbedding,
    string projectName,
    string branchName,
    string pathHash,
    SearchOptions? options = null,
    CancellationToken cancellationToken = default)
{
    options ??= SearchOptions.Default;

    await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
    await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

    try
    {
        // Set ef_search for this transaction
        await _hnswIndexService.SetEfSearchAsync(connection, options.EfSearch);

        // Execute vector search
        var results = await ExecuteSearchAsync(
            connection,
            queryEmbedding,
            projectName,
            branchName,
            pathHash,
            options,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return results;
    }
    catch
    {
        await transaction.RollbackAsync(cancellationToken);
        throw;
    }
}

public record SearchOptions
{
    public int MaxResults { get; init; } = 10;
    public int EfSearch { get; init; } = HnswIndexService.DefaultEfSearch;
    public double? MinSimilarity { get; init; }

    public static SearchOptions Default => new();

    /// <summary>
    /// High recall options for comprehensive searches.
    /// </summary>
    public static SearchOptions HighRecall => new() { EfSearch = 128, MaxResults = 20 };

    /// <summary>
    /// Fast options for quick lookups.
    /// </summary>
    public static SearchOptions Fast => new() { EfSearch = 32, MaxResults = 5 };
}
```

### 4. Dependency Injection Registration

```csharp
// src/CompoundDocs.McpServer/ServiceCollectionExtensions.cs

public static IServiceCollection AddHnswIndexServices(this IServiceCollection services)
{
    services.AddSingleton<IHnswIndexService, HnswIndexService>();
    return services;
}
```

### 5. Startup Index Verification

```csharp
// In Program.cs or startup sequence

public async Task InitializeVectorStoreAsync(IServiceProvider services)
{
    var hnswService = services.GetRequiredService<IHnswIndexService>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Verifying HNSW index configuration...");

    // Ensure optimal indexes exist
    await hnswService.EnsureOptimalIndexesAsync();

    // Report index health
    var health = await hnswService.GetIndexHealthAsync();
    logger.LogInformation(
        "HNSW indexes ready: {Count} indexes, {TotalSize} total",
        health.Indexes.Count,
        health.Indexes.Sum(i => i.IndexSizeBytes).ToString("N0") + " bytes");
}
```

---

## Index Maintenance

### Maintenance Procedures

HNSW indexes require periodic maintenance for optimal performance:

#### 1. VACUUM After Bulk Deletes

```sql
-- Run after significant document deletions
VACUUM (ANALYZE) compounding.documents;
VACUUM (ANALYZE) compounding.document_chunks;
```

#### 2. REINDEX for Degraded Performance

```sql
-- Rebuild index if performance degrades (run during maintenance window)
REINDEX INDEX CONCURRENTLY compounding.documents_embedding_hnsw_idx;
```

#### 3. Statistics Update

```sql
-- Update planner statistics after bulk operations
ANALYZE compounding.documents;
ANALYZE compounding.document_chunks;
```

### Maintenance Service

```csharp
// src/CompoundDocs.McpServer/Services/IndexMaintenanceService.cs

public interface IIndexMaintenanceService
{
    Task VacuumAnalyzeAsync(CancellationToken cancellationToken = default);
    Task ReindexIfNeededAsync(CancellationToken cancellationToken = default);
}

public class IndexMaintenanceService : IIndexMaintenanceService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<IndexMaintenanceService> _logger;

    public IndexMaintenanceService(NpgsqlDataSource dataSource, ILogger<IndexMaintenanceService> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task VacuumAnalyzeAsync(CancellationToken cancellationToken = default)
    {
        var tables = new[] { "documents", "document_chunks", "external_documents", "external_document_chunks" };

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        foreach (var table in tables)
        {
            _logger.LogInformation("Running VACUUM ANALYZE on {Table}", table);

            // VACUUM cannot run inside a transaction
            await using var cmd = new NpgsqlCommand($"VACUUM (ANALYZE) compounding.{table}", connection);
            cmd.CommandTimeout = 300; // 5 minutes
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        _logger.LogInformation("VACUUM ANALYZE completed for all tables");
    }

    public async Task ReindexIfNeededAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        // Check for bloated indexes (simple heuristic based on dead tuples)
        var checkSql = @"
            SELECT schemaname, relname, n_dead_tup, n_live_tup
            FROM pg_stat_user_tables
            WHERE schemaname = 'compounding'
              AND n_live_tup > 0
              AND n_dead_tup::float / n_live_tup > 0.2";

        await using var checkCmd = new NpgsqlCommand(checkSql, connection);
        await using var reader = await checkCmd.ExecuteReaderAsync(cancellationToken);

        var tablesToReindex = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            tablesToReindex.Add(reader.GetString(1));
        }

        await reader.CloseAsync();

        foreach (var table in tablesToReindex)
        {
            _logger.LogWarning("Table {Table} has >20% dead tuples, reindexing", table);

            var indexName = $"{table}_embedding_hnsw_idx";
            await using var reindexCmd = new NpgsqlCommand(
                $"REINDEX INDEX CONCURRENTLY compounding.{indexName}", connection);
            reindexCmd.CommandTimeout = 600; // 10 minutes

            try
            {
                await reindexCmd.ExecuteNonQueryAsync(cancellationToken);
                _logger.LogInformation("Reindexed {Index}", indexName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reindex {Index}", indexName);
            }
        }
    }
}
```

---

## Performance Benchmarks

### Expected Performance Characteristics

Based on pgvector benchmarks with HNSW (m=32, ef_construction=128):

| Metric | 1K Vectors | 10K Vectors | Notes |
|--------|-----------|-------------|-------|
| Index Build Time | ~2 sec | ~30 sec | One-time cost |
| Index Size | ~5 MB | ~50 MB | For 1024-dim vectors |
| Query Latency (p50) | <5 ms | <15 ms | With ef_search=64 |
| Query Latency (p99) | <20 ms | <50 ms | With ef_search=64 |
| Recall @ k=10 | >98% | >96% | Vs exact search |

### Recall vs Speed Tradeoff

| ef_search | Recall @ k=10 | Query Latency | Use Case |
|-----------|---------------|---------------|----------|
| 32 | ~92% | Fastest | Quick previews |
| 64 | ~96% | Balanced | **Default** |
| 128 | ~99% | Slower | Comprehensive search |
| 256 | ~99.5% | Slowest | Maximum recall |

---

## Acceptance Criteria

- [ ] HNSW index service implemented with m=32, ef_construction=128
- [ ] Query-time ef_search=64 applied to all vector searches
- [ ] Index health monitoring endpoint available
- [ ] VACUUM/ANALYZE maintenance procedures documented
- [ ] Performance benchmarks match expected characteristics
- [ ] Scale guidance documented for different data volumes
- [ ] Index creation uses CONCURRENTLY to avoid blocking

---

## Verification Steps

### 1. Index Parameter Verification

```sql
-- Verify HNSW index exists with correct parameters
SELECT
    indexname,
    indexdef
FROM pg_indexes
WHERE schemaname = 'compounding'
  AND indexdef LIKE '%hnsw%';
```

### 2. ef_search Verification

```sql
-- Check ef_search is being set
SHOW hnsw.ef_search;
-- Expected: 64
```

### 3. Query Performance Test

```sql
-- Test query with EXPLAIN ANALYZE
EXPLAIN ANALYZE
SELECT id, embedding <=> '[0.1, 0.2, ...]'::vector AS distance
FROM compounding.documents
WHERE project_name = 'test-project'
ORDER BY embedding <=> '[0.1, 0.2, ...]'::vector
LIMIT 10;
```

Expected: Index scan on HNSW index, execution time < 50ms

### 4. Index Health Check

```bash
# Via MCP server endpoint (once implemented)
curl http://localhost:5000/api/health/indexes
```

---

## Dependencies

### Upstream Dependencies
- Phase 041: Semantic Kernel Vector Store Setup (collections must exist)

### Downstream Dependencies
- Phase 047+: Vector search tools will use configured indexes
- All RAG query operations depend on HNSW index performance

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Index build blocks queries | Use CREATE INDEX CONCURRENTLY |
| Wrong parameters for data size | Document scale guidance, allow runtime tuning |
| Index bloat over time | Implement maintenance procedures |
| Query timeout on large datasets | Set appropriate CommandTimeout values |
| Memory pressure from large indexes | Monitor index size, document scaling thresholds |

---

## Success Criteria

1. HNSW indexes created on all vector collections with optimal parameters
2. Query latency < 50ms for 10K document collections
3. Recall > 95% for standard RAG queries
4. Index health monitoring operational
5. Maintenance procedures documented and tested
6. No blocking during index creation or maintenance
