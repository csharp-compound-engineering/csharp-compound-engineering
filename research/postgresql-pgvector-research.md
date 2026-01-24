# PostgreSQL with pgvector for RAG Applications

## Comprehensive Research Report

**Date**: January 2026
**Purpose**: MCP Server for RAG using Semantic Kernel, Ollama, and PostgreSQL as the vector database

> **UPDATE (January 24, 2026):** The Semantic Kernel PostgreSQL connector package has been renamed from `Microsoft.SemanticKernel.Connectors.Postgres` to `Microsoft.SemanticKernel.Connectors.PgVector`. The namespace remains `Microsoft.SemanticKernel.Connectors.Postgres`. See [semantic-kernel-pgvector-package-update.md](./semantic-kernel-pgvector-package-update.md) for complete details on the breaking changes.

---

## Table of Contents

1. [PostgreSQL Overview](#1-postgresql-overview)
2. [pgvector Extension](#2-pgvector-extension)
3. [Vector Indexing with pgvector](#3-vector-indexing-with-pgvector)
4. [SQL Operations for Vectors](#4-sql-operations-for-vectors)
5. [PostgreSQL Docker Images](#5-postgresql-docker-images)
6. [Connection from .NET/C#](#6-connection-from-netc)
7. [Schema Design for RAG](#7-schema-design-for-rag)
8. [Performance Optimization](#8-performance-optimization)
9. [Complete Examples](#9-complete-examples)

---

## 1. PostgreSQL Overview

### Architecture Fundamentals

PostgreSQL uses a **client/server model** where a PostgreSQL session consists of cooperating processes:

- **Server process (postgres)**: Manages database files, accepts connections, and performs database actions on behalf of clients
- **Process-per-connection architecture**: Each client connection is handled by a dedicated operating system process (forked from the postmaster)
- **Shared memory**: Reserved for database caching and transaction log caching

#### Key Shared Memory Components

| Component | Purpose |
|-----------|---------|
| **Shared Buffers** | Caches frequently accessed data pages |
| **WAL Buffers** | Buffers write-ahead log entries before disk write |
| **CLOG Buffers** | Transaction commit status cache |

### PostgreSQL 18 Features (September 2025)

PostgreSQL 18 introduced significant improvements:

- **Asynchronous I/O (AIO) subsystem**: Improves performance of sequential scans, bitmap heap scans, and vacuums
- **Skip scan lookups**: Better utilization of multicolumn B-tree indexes
- **uuidv7() function**: Generates timestamp-ordered UUIDs (ideal for distributed systems)
- **Virtual generated columns**: Compute values during read operations (now the default)
- **OAuth authentication support**
- **Temporal constraints**: PRIMARY KEY, UNIQUE, and FOREIGN KEY constraints over ranges
- **Parallel GIN index creation**

### Data Types and Type System

PostgreSQL's extensible type system supports:

- **Primitive types**: integer, bigint, numeric, text, varchar, boolean, timestamp, etc.
- **Complex types**: arrays, JSON/JSONB, hstore, composite types
- **Geometric types**: point, line, circle, polygon
- **Network types**: inet, cidr, macaddr
- **Extension types**: vector (pgvector), geometry (PostGIS)

### Indexing Strategies

| Index Type | Best Use Cases | Characteristics |
|------------|---------------|-----------------|
| **B-tree** (default) | Equality and range queries (=, <, >, BETWEEN) | Self-balancing tree, logarithmic access time |
| **GIN** | JSONB, arrays, full-text search | Inverted index, slower writes but faster reads |
| **GiST** | Spatial data, range types, complex queries | Generalized search tree, supports containment |
| **BRIN** | Large tables with ordered data (time-series, logs) | Block range summaries, very small index size |
| **Hash** | Equality comparisons only | Not recommended for most use cases |
| **SP-GiST** | Quadtrees, k-d trees, radix trees | Non-balanced structures |

### Connection Management and Pooling

PostgreSQL handles connections through:

1. **Postmaster process**: Listens on configured port (default 5432)
2. **Backend processes**: One per connection, handles all queries for that session
3. **Connection limits**: Controlled by `max_connections` parameter

Connection pooling options:
- **Npgsql built-in pooling**: Application-level, zero latency
- **PgBouncer**: External pooler, connection sharing across applications

### Transaction Isolation Levels

| Level | Dirty Read | Non-repeatable Read | Phantom Read | Serialization Anomaly |
|-------|-----------|---------------------|--------------|----------------------|
| **Read Committed** (default) | No | Possible | Possible | Possible |
| **Repeatable Read** | No | No | No | Possible |
| **Serializable** | No | No | No | No |

Key characteristics:
- **Read Committed**: Sees only data committed before query began; adequate for most applications
- **Repeatable Read**: Snapshot as of transaction start; uses Snapshot Isolation
- **Serializable**: Uses Serializable Snapshot Isolation (SSI) with predicate locks

```sql
-- Set isolation level
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;

-- Or in BEGIN statement
BEGIN TRANSACTION ISOLATION LEVEL REPEATABLE READ;
```

---

## 2. pgvector Extension

### What is pgvector?

pgvector is an open-source PostgreSQL extension that enables:

- Storage of high-dimensional vector embeddings
- Exact and approximate nearest neighbor search
- Integration with traditional SQL operations (JOINs, WHERE clauses, transactions)
- ACID compliance and point-in-time recovery for vector data

### Installation Methods

#### Linux (RHEL/CentOS/Fedora)
```bash
sudo dnf install pgvector_18  # Replace 18 with your PostgreSQL version
```

#### Compiling from Source
```bash
cd /tmp
git clone --branch v0.8.1 https://github.com/pgvector/pgvector.git
cd pgvector
make
sudo make install
```

#### Docker (Recommended for Development)
```bash
docker run --name pgvector -p 5432:5432 \
  -e POSTGRES_PASSWORD=mysecretpassword \
  -d pgvector/pgvector:pg17
```

#### Enable the Extension
```sql
CREATE EXTENSION vector;
```

### Vector Data Types

| Type | Max Dimensions | Description |
|------|----------------|-------------|
| `vector(n)` | 2,000 | Single-precision floating point |
| `halfvec(n)` | 4,000 | Half-precision floating point (saves memory) |
| `bit(n)` | 64,000 | Binary vectors |
| `sparsevec` | 1,000 non-zero | Sparse vectors |

### Dimension Considerations by Model

| Embedding Model | Dimensions |
|-----------------|------------|
| OpenAI text-embedding-3-small | 1,536 |
| OpenAI text-embedding-3-large | 3,072 |
| Ollama nomic-embed-text | 768 |
| Ollama mxbai-embed-large | 1,024 |
| sentence-transformers/all-MiniLM-L6-v2 | 384 |

### Distance Functions

| Operator | Function | Description | Use Case |
|----------|----------|-------------|----------|
| `<->` | `l2_distance()` | L2/Euclidean distance | General purpose |
| `<#>` | `inner_product()` | Negative inner product | Normalized vectors |
| `<=>` | `cosine_distance()` | Cosine distance | Text embeddings |
| `<+>` | `l1_distance()` | L1/Manhattan distance | Sparse data |
| `<~>` | N/A | Hamming distance | Binary vectors |
| `<%>` | N/A | Jaccard distance | Binary vectors |

**Note**: `<#>` returns the *negative* inner product because PostgreSQL only supports ascending index scans.

### Creating Vector Columns

```sql
-- Create table with vector column
CREATE TABLE documents (
    id BIGSERIAL PRIMARY KEY,
    content TEXT NOT NULL,
    embedding vector(1536)  -- Adjust dimensions to your model
);

-- Add vector column to existing table
ALTER TABLE documents ADD COLUMN embedding vector(768);
```

---

## 3. Vector Indexing with pgvector

### Index Types Overview

| Index Type | Build Speed | Query Speed | Memory | Best For |
|------------|-------------|-------------|--------|----------|
| **Exact (no index)** | N/A | Slow | Low | Small datasets, perfect recall |
| **IVFFlat** | Fast | Moderate | Low | Batch workloads, smaller datasets |
| **HNSW** | Slow | Fast | High | Production queries, large datasets |

### IVFFlat Index (Inverted File with Flat Compression)

IVFFlat divides vectors into clusters (lists) using k-means and searches only the closest lists.

#### How It Works
1. **Training phase**: Clusters vectors into `lists` groups using k-means
2. **Query phase**: Finds closest cluster centroids, then searches vectors in those lists

#### Configuration Parameters

| Parameter | Default | Description | Recommendation |
|-----------|---------|-------------|----------------|
| `lists` | 100 | Number of clusters | `rows/1000` for <1M rows; `sqrt(rows)` for >1M rows |
| `ivfflat.probes` | 1 | Lists to search at query time | Start with `sqrt(lists)` |

#### Creating IVFFlat Index

```sql
-- Create index (after data is loaded)
CREATE INDEX idx_documents_embedding_ivfflat
ON documents USING ivfflat (embedding vector_cosine_ops)
WITH (lists = 100);

-- Set probes at query time
SET ivfflat.probes = 10;
```

#### When to Use IVFFlat
- Batch processing workloads
- Frequent data updates requiring index rebuilds
- Memory-constrained environments
- Datasets under 1 million vectors

### HNSW Index (Hierarchical Navigable Small World)

HNSW creates a multi-layer graph structure for efficient approximate nearest neighbor search.

#### How It Works
1. **Hierarchical layers**: Upper layers have fewer, more spread-out nodes
2. **Navigation**: Search starts at top layer, descends through layers
3. **Local search**: Each layer navigates to nearest neighbors

#### Configuration Parameters

| Parameter | Default | Description | Recommendation |
|-----------|---------|-------------|----------------|
| `m` | 16 | Maximum connections per node | 12-48 for most workloads |
| `ef_construction` | 64 | Candidate list size during build | At least 2x `m` |
| `hnsw.ef_search` | 40 | Candidate list size during query | Increase for better recall |

#### Creating HNSW Index

```sql
-- Create index with custom parameters
CREATE INDEX idx_documents_embedding_hnsw
ON documents USING hnsw (embedding vector_cosine_ops)
WITH (m = 16, ef_construction = 64);

-- Tune search at query time
SET hnsw.ef_search = 100;

-- For single query tuning within transaction
SET LOCAL hnsw.ef_search = 200;
```

#### When to Use HNSW
- Production workloads requiring low latency
- Large datasets (millions of vectors)
- When recall quality is critical
- Infrequent bulk data updates

### Index Operator Classes

| Distance Metric | vector | halfvec | sparsevec |
|-----------------|--------|---------|-----------|
| L2 (Euclidean) | `vector_l2_ops` | `halfvec_l2_ops` | `sparsevec_l2_ops` |
| Inner Product | `vector_ip_ops` | `halfvec_ip_ops` | `sparsevec_ip_ops` |
| Cosine | `vector_cosine_ops` | `halfvec_cosine_ops` | `sparsevec_cosine_ops` |
| L1 (Manhattan) | `vector_l1_ops` | `halfvec_l1_ops` | `sparsevec_l1_ops` |

### Performance Comparison

Based on benchmarks with ~1 million vectors:

| Metric | IVFFlat | HNSW |
|--------|---------|------|
| Build Time | ~128 seconds | ~4,065 seconds (32x slower) |
| Index Size | ~257 MB | ~729 MB (2.8x larger) |
| Query Throughput (0.998 recall) | 2.6 QPS | 40.5 QPS (15.5x faster) |

### Best Practices for Index Creation

```sql
-- Increase memory for faster index builds
SET maintenance_work_mem = '8GB';

-- Enable parallel index construction
SET max_parallel_maintenance_workers = 7;

-- Create index AFTER loading data
COPY documents FROM '/path/to/data.csv';
CREATE INDEX idx_embedding ON documents USING hnsw (embedding vector_cosine_ops);

-- Create index concurrently in production
CREATE INDEX CONCURRENTLY idx_embedding
ON documents USING hnsw (embedding vector_cosine_ops);

-- Monitor index build progress
SELECT phase, round(100.0 * blocks_done / nullif(blocks_total, 0), 1) AS "%"
FROM pg_stat_progress_create_index;
```

---

## 4. SQL Operations for Vectors

### Inserting Vectors

```sql
-- Single insert
INSERT INTO documents (content, embedding)
VALUES ('Hello world', '[0.1, 0.2, 0.3, ...]');

-- Batch insert
INSERT INTO documents (content, embedding) VALUES
    ('Document 1', '[0.1, 0.2, ...]'),
    ('Document 2', '[0.3, 0.4, ...]'),
    ('Document 3', '[0.5, 0.6, ...]');

-- Bulk load with COPY (fastest)
COPY documents (content, embedding) FROM '/path/to/vectors.csv' WITH CSV;

-- Upsert (insert or update)
INSERT INTO documents (id, content, embedding)
VALUES (1, 'Updated content', '[0.1, 0.2, ...]')
ON CONFLICT (id) DO UPDATE SET
    content = EXCLUDED.content,
    embedding = EXCLUDED.embedding;
```

### Querying Nearest Neighbors

#### Basic K-Nearest Neighbors

```sql
-- L2 (Euclidean) distance
SELECT id, content, embedding <-> '[0.1, 0.2, ...]' AS distance
FROM documents
ORDER BY embedding <-> '[0.1, 0.2, ...]'
LIMIT 10;

-- Cosine distance (most common for text embeddings)
SELECT id, content, embedding <=> '[0.1, 0.2, ...]' AS distance
FROM documents
ORDER BY embedding <=> '[0.1, 0.2, ...]'
LIMIT 10;

-- Inner product (for normalized vectors)
SELECT id, content, (embedding <#> '[0.1, 0.2, ...]') * -1 AS similarity
FROM documents
ORDER BY embedding <#> '[0.1, 0.2, ...]'
LIMIT 10;
```

#### Distance-Based Filtering

```sql
-- Find all vectors within a distance threshold
SELECT id, content
FROM documents
WHERE embedding <-> '[0.1, 0.2, ...]' < 0.5;

-- Combine with LIMIT for bounded results
SELECT id, content, embedding <=> '[0.1, 0.2, ...]' AS distance
FROM documents
WHERE embedding <=> '[0.1, 0.2, ...]' < 0.3
ORDER BY distance
LIMIT 20;
```

### Filtering with WHERE Clauses

```sql
-- Vector search with metadata filter
SELECT id, content, embedding <=> $1 AS distance
FROM documents
WHERE category = 'technology'
  AND created_at > '2024-01-01'
ORDER BY embedding <=> $1
LIMIT 10;

-- Hybrid search: vector + full-text
SELECT id, content,
       embedding <=> $1 AS vector_distance,
       ts_rank(to_tsvector('english', content), plainto_tsquery('machine learning')) AS text_rank
FROM documents
WHERE to_tsvector('english', content) @@ plainto_tsquery('machine learning')
ORDER BY embedding <=> $1
LIMIT 10;
```

### Combining Vector Search with Traditional SQL

```sql
-- JOIN with related tables
SELECT d.id, d.content, c.name AS category_name,
       d.embedding <=> $1 AS distance
FROM documents d
JOIN categories c ON d.category_id = c.id
WHERE c.active = true
ORDER BY d.embedding <=> $1
LIMIT 10;

-- Aggregations
SELECT category_id, COUNT(*) AS doc_count, AVG(embedding) AS centroid
FROM documents
GROUP BY category_id;

-- Subquery for similar to another document
SELECT d2.id, d2.content
FROM documents d1
JOIN documents d2 ON d1.id != d2.id
WHERE d1.id = 123
ORDER BY d1.embedding <=> d2.embedding
LIMIT 5;
```

### Iterative Index Scans (pgvector 0.8.0+)

For filtered queries that might not return enough results:

```sql
-- Strict ordering (exact distance order)
SET hnsw.iterative_scan = strict_order;

-- Relaxed ordering (better recall, slightly out of order)
SET hnsw.iterative_scan = relaxed_order;

-- Apply to filtered query
SELECT id, content
FROM documents
WHERE category = 'rare_category'
ORDER BY embedding <=> '[0.1, 0.2, ...]'
LIMIT 10;
```

### Vector Utility Functions

```sql
-- Get vector dimensions
SELECT vector_dims(embedding) FROM documents LIMIT 1;

-- Get vector norm (magnitude)
SELECT vector_norm(embedding) FROM documents LIMIT 1;

-- Calculate cosine similarity from cosine distance
SELECT 1 - (embedding <=> '[0.1, 0.2, ...]') AS cosine_similarity
FROM documents;

-- Average vectors (for centroid calculation)
SELECT AVG(embedding) AS centroid FROM documents WHERE category = 'tech';
```

---

## 5. PostgreSQL Docker Images

### Official pgvector/pgvector Image

The recommended Docker image is `pgvector/pgvector` which includes pgvector pre-installed.

#### Available Tags

| Tag Pattern | Example | Description |
|-------------|---------|-------------|
| `pg{version}` | `pg17` | Latest pgvector with specific PostgreSQL version |
| `{pgvector}-pg{postgres}` | `0.8.1-pg17` | Specific versions of both |
| `{pgvector}-pg{postgres}-{debian}` | `0.8.1-pg18-trixie` | Full version specification |

Current recommended tags:
- `pgvector/pgvector:pg17` - PostgreSQL 17 with latest pgvector
- `pgvector/pgvector:pg16` - PostgreSQL 16 with latest pgvector
- `pgvector/pgvector:0.8.1-pg17` - Pinned versions

### Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `POSTGRES_PASSWORD` | Yes | - | Superuser password |
| `POSTGRES_USER` | No | `postgres` | Superuser username |
| `POSTGRES_DB` | No | `$POSTGRES_USER` | Default database name |
| `PGDATA` | No | `/var/lib/postgresql/data` | Data directory location |

**Important**: For PostgreSQL 18+, `PGDATA` changed to `/var/lib/postgresql/18/docker`.

### Volume Mounting for Data Persistence

```yaml
# PostgreSQL 17 and below
volumes:
  - ./data:/var/lib/postgresql/data

# PostgreSQL 18 and above
volumes:
  - ./data:/var/lib/postgresql/18/docker
```

### Docker Compose Configuration

```yaml
version: '3.8'

services:
  postgres:
    image: pgvector/pgvector:pg17
    container_name: pgvector-db
    restart: unless-stopped
    environment:
      POSTGRES_USER: rag_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-changeme}
      POSTGRES_DB: rag_db
    ports:
      - "5432:5432"
    volumes:
      - pgvector_data:/var/lib/postgresql/data
      - ./init.sql:/docker-entrypoint-initdb.d/init.sql:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U rag_user -d rag_db"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    deploy:
      resources:
        limits:
          memory: 4G
        reservations:
          memory: 1G

volumes:
  pgvector_data:
```

### Health Checks

```yaml
healthcheck:
  test: ["CMD-SHELL", "pg_isready -U postgres"]
  interval: 10s
  timeout: 5s
  retries: 5
  start_period: 30s
```

Alternative health check with connection test:
```yaml
healthcheck:
  test: ["CMD-SHELL", "psql -U postgres -c 'SELECT 1' || exit 1"]
  interval: 10s
  timeout: 5s
  retries: 5
```

### Initialization Script

Create `init.sql` for automatic setup:

```sql
-- Enable pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

-- Create RAG schema
CREATE TABLE IF NOT EXISTS documents (
    id BIGSERIAL PRIMARY KEY,
    content TEXT NOT NULL,
    embedding vector(768),
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Create indexes
CREATE INDEX IF NOT EXISTS idx_documents_embedding
ON documents USING hnsw (embedding vector_cosine_ops);

CREATE INDEX IF NOT EXISTS idx_documents_metadata
ON documents USING gin (metadata);
```

---

## 6. Connection from .NET/C#

### Required NuGet Packages

```bash
dotnet add package Npgsql
dotnet add package Pgvector
dotnet add package Pgvector.EntityFrameworkCore  # If using EF Core
```

Package versions:
- `Pgvector` 0.3.2+ (requires Npgsql >= 8.0.5)
- `Pgvector.EntityFrameworkCore` 0.3.0+ (requires Npgsql.EntityFrameworkCore.PostgreSQL >= 9.0.1)

### NpgsqlDataSource and Connection Pooling

```csharp
using Npgsql;
using Pgvector;
using Pgvector.Npgsql;

// Create a data source (singleton - create once, reuse everywhere)
var connectionString = "Host=localhost;Database=rag_db;Username=rag_user;Password=changeme";
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.UseVector();  // Enable pgvector support
await using var dataSource = dataSourceBuilder.Build();

// Open connections from the data source
await using var connection = await dataSource.OpenConnectionAsync();
```

### Enabling pgvector Extension

```csharp
// Ensure extension is enabled (run once per database)
await using var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", connection);
await cmd.ExecuteNonQueryAsync();

// Reload types after enabling extension
await connection.ReloadTypesAsync();
```

### Parameterized Queries with Vectors

```csharp
// Insert a vector
var embedding = new Vector(new float[] { 0.1f, 0.2f, 0.3f, /* ... */ });
await using var insertCmd = new NpgsqlCommand(
    "INSERT INTO documents (content, embedding) VALUES (@content, @embedding)",
    connection);
insertCmd.Parameters.AddWithValue("content", "Document content here");
insertCmd.Parameters.AddWithValue("embedding", embedding);
await insertCmd.ExecuteNonQueryAsync();

// Query nearest neighbors
var queryVector = new Vector(new float[] { 0.1f, 0.2f, 0.3f, /* ... */ });
await using var selectCmd = new NpgsqlCommand(@"
    SELECT id, content, embedding <=> @query AS distance
    FROM documents
    ORDER BY embedding <=> @query
    LIMIT @limit", connection);
selectCmd.Parameters.AddWithValue("query", queryVector);
selectCmd.Parameters.AddWithValue("limit", 10);

await using var reader = await selectCmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    var id = reader.GetInt64(0);
    var content = reader.GetString(1);
    var distance = reader.GetDouble(2);
    Console.WriteLine($"ID: {id}, Distance: {distance:F4}");
}
```

### Async Operations

```csharp
public class VectorRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public VectorRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<long> InsertDocumentAsync(string content, float[] embedding)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO documents (content, embedding)
            VALUES (@content, @embedding)
            RETURNING id", connection);

        cmd.Parameters.AddWithValue("content", content);
        cmd.Parameters.AddWithValue("embedding", new Vector(embedding));

        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<List<SearchResult>> SearchAsync(float[] queryEmbedding, int limit = 10)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, content, embedding <=> @query AS distance
            FROM documents
            ORDER BY embedding <=> @query
            LIMIT @limit", connection);

        cmd.Parameters.AddWithValue("query", new Vector(queryEmbedding));
        cmd.Parameters.AddWithValue("limit", limit);

        var results = new List<SearchResult>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new SearchResult
            {
                Id = reader.GetInt64(0),
                Content = reader.GetString(1),
                Distance = reader.GetDouble(2)
            });
        }
        return results;
    }
}

public record SearchResult
{
    public long Id { get; init; }
    public string Content { get; init; } = "";
    public double Distance { get; init; }
}
```

### Transaction Handling

```csharp
await using var connection = await dataSource.OpenConnectionAsync();
await using var transaction = await connection.BeginTransactionAsync();

try
{
    // Insert document
    await using var insertCmd = new NpgsqlCommand(@"
        INSERT INTO documents (content, embedding) VALUES (@content, @embedding)",
        connection, transaction);
    insertCmd.Parameters.AddWithValue("content", "Document content");
    insertCmd.Parameters.AddWithValue("embedding", new Vector(embedding));
    await insertCmd.ExecuteNonQueryAsync();

    // Insert metadata
    await using var metaCmd = new NpgsqlCommand(@"
        INSERT INTO document_metadata (document_id, key, value) VALUES (@id, @key, @value)",
        connection, transaction);
    // ... add parameters and execute

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### Entity Framework Core Integration

```csharp
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

public class RagDbContext : DbContext
{
    public DbSet<Document> Documents => Set<Document>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=rag_db;Username=rag_user;Password=changeme",
            o => o.UseVector());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Document>()
            .HasIndex(d => d.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");
    }
}

public class Document
{
    public long Id { get; set; }
    public string Content { get; set; } = "";
    public Vector? Embedding { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Usage
using var context = new RagDbContext();
var queryVector = new Vector(new float[] { 0.1f, 0.2f, 0.3f });
var results = await context.Documents
    .OrderBy(d => d.Embedding!.CosineDistance(queryVector))
    .Take(10)
    .ToListAsync();
```

### Microsoft Semantic Kernel Integration

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Postgres;  // Note: Package renamed to Microsoft.SemanticKernel.Connectors.PgVector (namespace unchanged)

// Add PostgreSQL vector store
var connectionString = "Host=localhost;Database=rag_db;Username=rag_user;Password=changeme";

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddPostgresVectorStore(connectionString);

var kernel = kernelBuilder.Build();
var vectorStore = kernel.GetRequiredService<IVectorStore>();
```

---

## 7. Schema Design for RAG

### Basic Document Schema

```sql
CREATE TABLE documents (
    id BIGSERIAL PRIMARY KEY,
    content TEXT NOT NULL,
    embedding vector(768),  -- Adjust to your model's dimensions
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Index for vector search
CREATE INDEX idx_documents_embedding
ON documents USING hnsw (embedding vector_cosine_ops);
```

### Enhanced Schema with Metadata

```sql
CREATE TABLE documents (
    id BIGSERIAL PRIMARY KEY,

    -- Content
    title TEXT,
    content TEXT NOT NULL,
    content_hash BYTEA UNIQUE,  -- SHA256 hash for deduplication

    -- Source tracking
    source_url TEXT,
    source_type TEXT,  -- 'pdf', 'web', 'api', etc.
    filename TEXT,

    -- Chunking info
    chunk_index INTEGER DEFAULT 0,
    parent_document_id BIGINT REFERENCES documents(id),

    -- Vector embedding
    embedding vector(768),

    -- Flexible metadata
    metadata JSONB DEFAULT '{}',

    -- Timestamps
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    indexed_at TIMESTAMPTZ
);

-- Indexes
CREATE INDEX idx_documents_embedding
ON documents USING hnsw (embedding vector_cosine_ops);

CREATE INDEX idx_documents_metadata
ON documents USING gin (metadata);

CREATE INDEX idx_documents_source_type
ON documents (source_type);

CREATE INDEX idx_documents_created_at
ON documents (created_at DESC);

CREATE INDEX idx_documents_parent
ON documents (parent_document_id)
WHERE parent_document_id IS NOT NULL;
```

### Multi-Collection Schema

```sql
-- Collections table
CREATE TABLE collections (
    id BIGSERIAL PRIMARY KEY,
    name TEXT UNIQUE NOT NULL,
    description TEXT,
    embedding_model TEXT NOT NULL,
    embedding_dimensions INTEGER NOT NULL,
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Documents with collection reference
CREATE TABLE documents (
    id BIGSERIAL PRIMARY KEY,
    collection_id BIGINT REFERENCES collections(id) ON DELETE CASCADE,
    content TEXT NOT NULL,
    embedding vector(2000),  -- Max dimensions for flexibility
    metadata JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Partial indexes per collection (better performance)
CREATE INDEX idx_docs_collection_1_embedding
ON documents USING hnsw (embedding vector_cosine_ops)
WHERE collection_id = 1;
```

### Vector Column Sizing Guidelines

| Model | Dimensions | Storage per Vector |
|-------|------------|-------------------|
| all-MiniLM-L6-v2 | 384 | ~1.5 KB |
| nomic-embed-text | 768 | ~3 KB |
| mxbai-embed-large | 1,024 | ~4 KB |
| text-embedding-3-small | 1,536 | ~6 KB |
| text-embedding-3-large | 3,072 | ~12 KB |

### Partitioning for Large Datasets

```sql
-- Partition by date range
CREATE TABLE documents (
    id BIGSERIAL,
    content TEXT NOT NULL,
    embedding vector(768),
    created_at TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (id, created_at)
) PARTITION BY RANGE (created_at);

-- Create partitions
CREATE TABLE documents_2024_q1 PARTITION OF documents
    FOR VALUES FROM ('2024-01-01') TO ('2024-04-01');

CREATE TABLE documents_2024_q2 PARTITION OF documents
    FOR VALUES FROM ('2024-04-01') TO ('2024-07-01');

-- Each partition needs its own index
CREATE INDEX idx_docs_2024_q1_embedding
ON documents_2024_q1 USING hnsw (embedding vector_cosine_ops);
```

### Indexing Strategy for RAG

```sql
-- Primary vector index (HNSW for production)
CREATE INDEX idx_documents_embedding_hnsw
ON documents USING hnsw (embedding vector_cosine_ops)
WITH (m = 16, ef_construction = 100);

-- Metadata filtering support
CREATE INDEX idx_documents_metadata_gin
ON documents USING gin (metadata jsonb_path_ops);

-- Common filter columns as B-tree
CREATE INDEX idx_documents_source_type ON documents (source_type);
CREATE INDEX idx_documents_created ON documents (created_at DESC);

-- Composite index for filtered vector search
CREATE INDEX idx_documents_category_embedding
ON documents USING hnsw (embedding vector_cosine_ops)
WHERE (metadata->>'category') IS NOT NULL;
```

---

## 8. Performance Optimization

### Memory Settings

| Parameter | Recommended Value | Description |
|-----------|------------------|-------------|
| `shared_buffers` | 25-40% of RAM | Data page cache |
| `work_mem` | 64MB-256MB | Per-operation memory |
| `maintenance_work_mem` | 1GB-8GB | For index builds, VACUUM |
| `effective_cache_size` | 50-75% of RAM | Query planner hint |

```sql
-- Set in postgresql.conf or at runtime
ALTER SYSTEM SET shared_buffers = '4GB';
ALTER SYSTEM SET work_mem = '128MB';
ALTER SYSTEM SET maintenance_work_mem = '2GB';
ALTER SYSTEM SET effective_cache_size = '12GB';

-- For index builds specifically
SET maintenance_work_mem = '8GB';
```

### Index Tuning for Vector Search

```sql
-- HNSW: Increase ef_search for better recall
SET hnsw.ef_search = 100;  -- Default is 40

-- IVFFlat: Increase probes for better recall
SET ivfflat.probes = 20;  -- Default is 1

-- Enable iterative scans for filtered queries
SET hnsw.iterative_scan = relaxed_order;
SET ivfflat.iterative_scan = relaxed_order;
```

### Connection Pool Sizing

Formula: `connections = (cores * 2) + effective_spindle_count`

For SSD-based systems: `connections = cores * 2`

```csharp
// Npgsql connection string with pool settings
var connectionString = @"
    Host=localhost;
    Database=rag_db;
    Username=rag_user;
    Password=changeme;
    Pooling=true;
    MinPoolSize=5;
    MaxPoolSize=100;
    ConnectionIdleLifetime=300;
    ConnectionPruningInterval=10";
```

### Query Optimization

```sql
-- Use EXPLAIN ANALYZE to understand query plans
EXPLAIN ANALYZE
SELECT id, content, embedding <=> '[0.1, 0.2, ...]' AS distance
FROM documents
WHERE metadata->>'category' = 'technology'
ORDER BY embedding <=> '[0.1, 0.2, ...]'
LIMIT 10;

-- Check if index is being used
SELECT indexname, idx_scan, idx_tup_read, idx_tup_fetch
FROM pg_stat_user_indexes
WHERE tablename = 'documents';
```

### Vacuuming HNSW Indexes

```sql
-- Reindex before vacuum for faster operation
REINDEX INDEX CONCURRENTLY idx_documents_embedding_hnsw;

-- Then vacuum
VACUUM documents;

-- Or combined
VACUUM (INDEX_CLEANUP ON) documents;
```

### Monitoring and Diagnostics

```sql
-- Check table and index sizes
SELECT
    relname AS name,
    pg_size_pretty(pg_relation_size(oid)) AS size
FROM pg_class
WHERE relname LIKE 'documents%' OR relname LIKE 'idx_documents%'
ORDER BY pg_relation_size(oid) DESC;

-- Monitor active queries
SELECT pid, now() - query_start AS duration, query
FROM pg_stat_activity
WHERE state = 'active' AND query NOT LIKE '%pg_stat_activity%';

-- Check for slow queries
SELECT query, calls, total_exec_time/calls AS avg_time, rows
FROM pg_stat_statements
WHERE query LIKE '%embedding%'
ORDER BY total_exec_time DESC
LIMIT 10;
```

### Bulk Loading Best Practices

```sql
-- Disable autocommit for batch inserts
BEGIN;

-- Use COPY for fastest bulk loading
COPY documents (content, embedding, metadata)
FROM '/path/to/data.csv' WITH CSV;

-- Or use multi-value INSERT
INSERT INTO documents (content, embedding) VALUES
    ('doc1', '[0.1, ...]'),
    ('doc2', '[0.2, ...]'),
    -- ... up to 1000 rows per statement
    ('docN', '[0.N, ...]');

COMMIT;

-- Create indexes AFTER bulk loading
CREATE INDEX CONCURRENTLY idx_documents_embedding
ON documents USING hnsw (embedding vector_cosine_ops);

-- Analyze table after loading
ANALYZE documents;
```

---

## 9. Complete Examples

### Docker Compose Setup for Development

```yaml
# docker-compose.yml
version: '3.8'

services:
  postgres:
    image: pgvector/pgvector:pg17
    container_name: rag-postgres
    restart: unless-stopped
    environment:
      POSTGRES_USER: rag_user
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-development_password}
      POSTGRES_DB: rag_db
    ports:
      - "5432:5432"
    volumes:
      - pgvector_data:/var/lib/postgresql/data
      - ./init:/docker-entrypoint-initdb.d:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U rag_user -d rag_db"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s
    command:
      - "postgres"
      - "-c"
      - "shared_buffers=256MB"
      - "-c"
      - "work_mem=64MB"
      - "-c"
      - "maintenance_work_mem=512MB"
      - "-c"
      - "effective_cache_size=1GB"
      - "-c"
      - "max_connections=100"

volumes:
  pgvector_data:
    driver: local
```

### SQL Schema for RAG Application

```sql
-- init/01_schema.sql

-- Enable extensions
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS pg_trgm;  -- For text search

-- Main documents table
CREATE TABLE documents (
    id BIGSERIAL PRIMARY KEY,
    external_id TEXT UNIQUE,

    -- Content
    title TEXT,
    content TEXT NOT NULL,
    content_hash BYTEA,

    -- Chunking
    chunk_index INTEGER DEFAULT 0,
    chunk_count INTEGER DEFAULT 1,
    parent_id BIGINT REFERENCES documents(id) ON DELETE CASCADE,

    -- Vector embedding (adjust dimensions as needed)
    embedding vector(768),

    -- Source info
    source_type TEXT CHECK (source_type IN ('pdf', 'web', 'api', 'manual')),
    source_url TEXT,

    -- Flexible metadata
    metadata JSONB DEFAULT '{}',
    tags TEXT[] DEFAULT '{}',

    -- Timestamps
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    indexed_at TIMESTAMPTZ
);

-- Collections for multi-tenancy
CREATE TABLE collections (
    id BIGSERIAL PRIMARY KEY,
    name TEXT UNIQUE NOT NULL,
    description TEXT,
    embedding_model TEXT NOT NULL DEFAULT 'nomic-embed-text',
    embedding_dimensions INTEGER NOT NULL DEFAULT 768,
    settings JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- Add collection reference
ALTER TABLE documents ADD COLUMN collection_id BIGINT REFERENCES collections(id);

-- Create default collection
INSERT INTO collections (name, description)
VALUES ('default', 'Default document collection');

-- Indexes
CREATE INDEX idx_documents_embedding_hnsw
ON documents USING hnsw (embedding vector_cosine_ops)
WITH (m = 16, ef_construction = 64);

CREATE INDEX idx_documents_metadata
ON documents USING gin (metadata jsonb_path_ops);

CREATE INDEX idx_documents_tags
ON documents USING gin (tags);

CREATE INDEX idx_documents_content_hash
ON documents (content_hash);

CREATE INDEX idx_documents_collection
ON documents (collection_id);

CREATE INDEX idx_documents_created_at
ON documents (created_at DESC);

CREATE INDEX idx_documents_source_type
ON documents (source_type);

-- Updated timestamp trigger
CREATE OR REPLACE FUNCTION update_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER documents_updated_at
    BEFORE UPDATE ON documents
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at();

-- Helper function for similarity search
CREATE OR REPLACE FUNCTION search_documents(
    query_embedding vector(768),
    match_count INTEGER DEFAULT 10,
    filter_metadata JSONB DEFAULT NULL,
    filter_collection_id BIGINT DEFAULT NULL,
    similarity_threshold FLOAT DEFAULT 0.0
)
RETURNS TABLE (
    id BIGINT,
    content TEXT,
    metadata JSONB,
    similarity FLOAT
) AS $$
BEGIN
    RETURN QUERY
    SELECT
        d.id,
        d.content,
        d.metadata,
        1 - (d.embedding <=> query_embedding) AS similarity
    FROM documents d
    WHERE
        d.embedding IS NOT NULL
        AND (filter_collection_id IS NULL OR d.collection_id = filter_collection_id)
        AND (filter_metadata IS NULL OR d.metadata @> filter_metadata)
        AND 1 - (d.embedding <=> query_embedding) >= similarity_threshold
    ORDER BY d.embedding <=> query_embedding
    LIMIT match_count;
END;
$$ LANGUAGE plpgsql;
```

### C# Code for Vector Operations

```csharp
// VectorDbService.cs
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Pgvector;
using Pgvector.Npgsql;

namespace RagMcpServer.Services;

public interface IVectorDbService
{
    Task InitializeAsync();
    Task<long> InsertDocumentAsync(DocumentInsertRequest request);
    Task<List<SearchResult>> SearchAsync(SearchRequest request);
    Task<int> BulkInsertAsync(IEnumerable<DocumentInsertRequest> documents);
    Task<bool> DeleteDocumentAsync(long id);
}

public class VectorDbService : IVectorDbService, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<VectorDbService> _logger;

    public VectorDbService(string connectionString, ILogger<VectorDbService> logger)
    {
        _logger = logger;

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        _dataSource = dataSourceBuilder.Build();
    }

    public async Task InitializeAsync()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();

        // Ensure extension is enabled
        await using var cmd = new NpgsqlCommand(
            "CREATE EXTENSION IF NOT EXISTS vector", connection);
        await cmd.ExecuteNonQueryAsync();

        _logger.LogInformation("Vector database initialized successfully");
    }

    public async Task<long> InsertDocumentAsync(DocumentInsertRequest request)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();

        var contentHash = ComputeHash(request.Content);

        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO documents (content, embedding, metadata, content_hash, source_type, collection_id)
            VALUES (@content, @embedding, @metadata, @hash, @source, @collection)
            ON CONFLICT (content_hash) DO UPDATE SET
                embedding = EXCLUDED.embedding,
                metadata = EXCLUDED.metadata,
                updated_at = CURRENT_TIMESTAMP
            RETURNING id", connection);

        cmd.Parameters.AddWithValue("content", request.Content);
        cmd.Parameters.AddWithValue("embedding", new Vector(request.Embedding));
        cmd.Parameters.AddWithValue("metadata", NpgsqlTypes.NpgsqlDbType.Jsonb,
            request.Metadata ?? "{}");
        cmd.Parameters.AddWithValue("hash", contentHash);
        cmd.Parameters.AddWithValue("source", request.SourceType ?? "manual");
        cmd.Parameters.AddWithValue("collection", request.CollectionId ?? 1L);

        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task<List<SearchResult>> SearchAsync(SearchRequest request)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();

        // Set search parameters for this query
        if (request.EfSearch.HasValue)
        {
            await using var setCmd = new NpgsqlCommand(
                $"SET LOCAL hnsw.ef_search = {request.EfSearch.Value}", connection);
            await setCmd.ExecuteNonQueryAsync();
        }

        var sql = @"
            SELECT id, content, metadata,
                   1 - (embedding <=> @query) AS similarity
            FROM documents
            WHERE embedding IS NOT NULL";

        if (request.CollectionId.HasValue)
            sql += " AND collection_id = @collection";

        if (!string.IsNullOrEmpty(request.MetadataFilter))
            sql += " AND metadata @> @filter::jsonb";

        if (request.MinSimilarity.HasValue)
            sql += " AND 1 - (embedding <=> @query) >= @minSim";

        sql += @"
            ORDER BY embedding <=> @query
            LIMIT @limit";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("query", new Vector(request.QueryEmbedding));
        cmd.Parameters.AddWithValue("limit", request.Limit);

        if (request.CollectionId.HasValue)
            cmd.Parameters.AddWithValue("collection", request.CollectionId.Value);

        if (!string.IsNullOrEmpty(request.MetadataFilter))
            cmd.Parameters.AddWithValue("filter", request.MetadataFilter);

        if (request.MinSimilarity.HasValue)
            cmd.Parameters.AddWithValue("minSim", request.MinSimilarity.Value);

        var results = new List<SearchResult>();
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(new SearchResult
            {
                Id = reader.GetInt64(0),
                Content = reader.GetString(1),
                Metadata = reader.IsDBNull(2) ? null : reader.GetString(2),
                Similarity = reader.GetDouble(3)
            });
        }

        return results;
    }

    public async Task<int> BulkInsertAsync(IEnumerable<DocumentInsertRequest> documents)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var count = 0;

        try
        {
            foreach (var batch in documents.Chunk(100))
            {
                foreach (var doc in batch)
                {
                    await using var cmd = new NpgsqlCommand(@"
                        INSERT INTO documents (content, embedding, metadata, content_hash)
                        VALUES (@content, @embedding, @metadata, @hash)
                        ON CONFLICT (content_hash) DO NOTHING", connection, transaction);

                    cmd.Parameters.AddWithValue("content", doc.Content);
                    cmd.Parameters.AddWithValue("embedding", new Vector(doc.Embedding));
                    cmd.Parameters.AddWithValue("metadata", NpgsqlTypes.NpgsqlDbType.Jsonb,
                        doc.Metadata ?? "{}");
                    cmd.Parameters.AddWithValue("hash", ComputeHash(doc.Content));

                    count += await cmd.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();
            _logger.LogInformation("Bulk inserted {Count} documents", count);
            return count;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> DeleteDocumentAsync(long id)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM documents WHERE id = @id", connection);
        cmd.Parameters.AddWithValue("id", id);

        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    private static byte[] ComputeHash(string content)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(content));
    }

    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync();
    }
}

// Models
public record DocumentInsertRequest
{
    public required string Content { get; init; }
    public required float[] Embedding { get; init; }
    public string? Metadata { get; init; }
    public string? SourceType { get; init; }
    public long? CollectionId { get; init; }
}

public record SearchRequest
{
    public required float[] QueryEmbedding { get; init; }
    public int Limit { get; init; } = 10;
    public long? CollectionId { get; init; }
    public string? MetadataFilter { get; init; }
    public double? MinSimilarity { get; init; }
    public int? EfSearch { get; init; }
}

public record SearchResult
{
    public long Id { get; init; }
    public string Content { get; init; } = "";
    public string? Metadata { get; init; }
    public double Similarity { get; init; }
}
```

### Dependency Injection Setup

```csharp
// Program.cs or ServiceCollectionExtensions.cs
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVectorDb(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddSingleton<IVectorDbService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<VectorDbService>>();
            return new VectorDbService(connectionString, logger);
        });

        return services;
    }
}

// Usage in Program.cs
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PostgreSQL")
    ?? "Host=localhost;Database=rag_db;Username=rag_user;Password=changeme";

builder.Services.AddVectorDb(connectionString);

var app = builder.Build();

// Initialize database on startup
var vectorDb = app.Services.GetRequiredService<IVectorDbService>();
await vectorDb.InitializeAsync();
```

### Index Maintenance Script

```sql
-- maintenance.sql - Run periodically

-- Analyze statistics for query optimizer
ANALYZE documents;

-- Reindex if performance degrades
REINDEX INDEX CONCURRENTLY idx_documents_embedding_hnsw;

-- Vacuum to reclaim space and update visibility map
VACUUM (ANALYZE, INDEX_CLEANUP ON) documents;

-- Check index health
SELECT
    indexrelname,
    idx_scan,
    idx_tup_read,
    idx_tup_fetch,
    pg_size_pretty(pg_relation_size(indexrelid)) AS index_size
FROM pg_stat_user_indexes
WHERE schemaname = 'public'
ORDER BY idx_scan DESC;

-- Check for bloat
SELECT
    schemaname, tablename,
    pg_size_pretty(pg_total_relation_size(schemaname || '.' || tablename)) AS total_size,
    pg_size_pretty(pg_relation_size(schemaname || '.' || tablename)) AS table_size,
    pg_size_pretty(pg_indexes_size(schemaname || '.' || tablename)) AS index_size
FROM pg_tables
WHERE schemaname = 'public'
ORDER BY pg_total_relation_size(schemaname || '.' || tablename) DESC;
```

---

## Sources

### PostgreSQL Architecture
- [PostgreSQL Documentation: Architectural Fundamentals](https://www.postgresql.org/docs/current/tutorial-arch.html)
- [PostgreSQL: About](https://www.postgresql.org/about/)
- [PostgreSQL 18 Release Notes](https://www.postgresql.org/docs/current/release-18.html)

### pgvector
- [pgvector GitHub Repository](https://github.com/pgvector/pgvector)
- [pgvector Docker Hub](https://hub.docker.com/r/pgvector/pgvector)
- [Neon: pgvector Extension Documentation](https://neon.com/docs/extensions/pgvector)

### Indexing and Performance
- [AWS: Optimize Generative AI with pgvector Indexing](https://aws.amazon.com/blogs/database/optimize-generative-ai-applications-with-pgvector-indexing-a-deep-dive-into-ivfflat-and-hnsw-techniques/)
- [Google Cloud: Faster Similarity Search with pgvector](https://cloud.google.com/blog/products/databases/faster-similarity-search-performance-with-pgvector-indexes)
- [Crunchy Data: HNSW Indexes with Postgres and pgvector](https://www.crunchydata.com/blog/hnsw-indexes-with-postgres-and-pgvector)
- [Neon: Optimize pgvector Search](https://neon.com/docs/ai/ai-vector-search-optimization)

### .NET Integration
- [pgvector-dotnet GitHub](https://github.com/pgvector/pgvector-dotnet)
- [NuGet: Pgvector Package](https://www.nuget.org/packages/Pgvector/)
- [Microsoft: Semantic Kernel Postgres Connector](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/postgres-connector)
- [Npgsql Documentation](https://www.npgsql.org/doc/basic-usage.html)

### Performance Tuning
- [EDB: How to Tune PostgreSQL Memory](https://www.enterprisedb.com/postgres-tutorials/how-tune-postgresql-memory)
- [PostgreSQL Wiki: Tuning Your PostgreSQL Server](https://wiki.postgresql.org/wiki/Tuning_Your_PostgreSQL_Server)
- [Npgsql: Performance Guide](https://www.npgsql.org/doc/performance.html)

### Schema Design
- [pgEdge: Building a RAG Server with PostgreSQL](https://www.pgedge.com/blog/building-a-rag-server-with-postgresql-part-1-loading-your-content)
- [pgDash: RAG with PostgreSQL](https://pgdash.io/blog/rag-with-postgresql.html)

### Docker Configuration
- [Docker Hub: Official postgres Image](https://hub.docker.com/_/postgres)
- [Docker Blog: How to Use the Postgres Docker Official Image](https://www.docker.com/blog/how-to-use-the-postgres-docker-official-image/)

---

*This research report was compiled for the csharp-compound-engineering project to support building an MCP Server for RAG using Semantic Kernel, Ollama, and PostgreSQL with pgvector.*
