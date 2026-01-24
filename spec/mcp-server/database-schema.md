# Database Schema Specification

> **Status**: [DRAFT]
> **Parent**: [mcp-server.md](../mcp-server.md)

> **Background**: Comprehensive coverage of pgvector installation, indexing strategies (HNSW vs IVFFlat), distance functions, and .NET integration patterns. See [PostgreSQL with pgvector for RAG Applications](../../research/postgresql-pgvector-research.md).

---

## Multi-Tenant Architecture

Uses a **single schema** with tenant isolation via compound keys. This supports:
- Multiple projects
- Git worktrees (same project, different paths)
- Concurrent branches

---

## Schema Management Responsibilities

> **Background**: Analysis of when to use Liquibase vs Semantic Kernel's `EnsureCollectionExistsAsync()` for schema management, including trade-offs and multi-schema support patterns. See [Semantic Kernel PostgreSQL Schema Management](../../research/liquibase-semantic-kernel-schema-research.md).

Both schemas exist in the **same PostgreSQL database** but are managed differently:

| Schema | Managed By | Approach |
|--------|-----------|----------|
| `tenant_management` | Liquibase | DDL migrations via changelog files |
| `compounding` (vector collections) | Semantic Kernel | `EnsureCollectionExistsAsync()` auto-creates |

**Liquibase** handles the `tenant_management` schema because:
- It has stable, relational tables
- Requires explicit migration control
- Init script runs in Docker container startup

**Semantic Kernel** handles vector collections because:
- Collections are model-driven (C# classes define schema)
- Automatic table creation from attributes
- Handles pgvector-specific types transparently

### Embedding Dimensions

All vector columns use **1024 dimensions** to match `mxbai-embed-large` output.

> **Startup Validation**: The MCP server validates embedding dimensions at startup by:
> 1. Generating a test embedding via Ollama
> 2. Comparing `embedding.Length` against configured dimensions (1024)
> 3. Failing startup with clear error if dimensions mismatch
>
> This prevents silent failures from model changes or configuration errors.

---

## Tenant Management Schema

> **Background**: Docker Compose configuration, Liquibase changelog organization, and init script patterns for pgvector with multi-tenant schemas. See [Liquibase with PostgreSQL/pgvector in Docker](../../research/liquibase-pgvector-docker-init.md).

Use Liquibase and an init script in the postgres docker container for setup.

> **See also**: [liquibase-changelog.md](./liquibase-changelog.md) for detailed changelog format specification.

```sql
CREATE SCHEMA IF NOT EXISTS tenant_management;

-- Track encountered repo paths
CREATE TABLE IF NOT EXISTS tenant_management.repo_paths (
    path_hash VARCHAR(64) PRIMARY KEY,
    absolute_path TEXT NOT NULL,
    project_name TEXT NOT NULL,
    first_seen TIMESTAMPTZ DEFAULT NOW(),
    last_seen TIMESTAMPTZ DEFAULT NOW()
);

-- Track encountered branches
CREATE TABLE IF NOT EXISTS tenant_management.branches (
    id SERIAL PRIMARY KEY,
    project_name TEXT NOT NULL,
    branch_name TEXT NOT NULL,
    first_seen TIMESTAMPTZ DEFAULT NOW(),
    last_seen TIMESTAMPTZ DEFAULT NOW(),
    UNIQUE(project_name, branch_name)
);

CREATE INDEX IF NOT EXISTS idx_branches_project
ON tenant_management.branches (project_name);
```

---

## Documents Schema (Semantic Kernel Model)

> **Background**: Latest API changes for the PostgreSQL connector including package rename (`Microsoft.SemanticKernel.Connectors.PgVector`), class rename (`PostgresCollection<TKey, TRecord>`), and method updates. See [Semantic Kernel PostgreSQL Connector API Updates](../../research/semantic-kernel-pgvector-package-update.md).

Uses Semantic Kernel's model-first approach with filterable columns for tenant isolation:

```csharp
public class CompoundDocument
{
    [VectorStoreKey(StorageName = "id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Tenant isolation keys (filterable)
    [VectorStoreData(StorageName = "project_name", IsFilterable = true)]
    public string ProjectName { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "branch_name", IsFilterable = true)]
    public string BranchName { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "path_hash", IsFilterable = true)]
    public string PathHash { get; set; } = string.Empty;

    // Document metadata
    [VectorStoreData(StorageName = "relative_path")]
    public string RelativePath { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "title")]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "summary")]
    public string? Summary { get; set; }

    [VectorStoreData(StorageName = "doc_type", IsFilterable = true)]
    public string DocType { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "promotion_level", IsFilterable = true)]
    public string PromotionLevel { get; set; } = "standard";

    [VectorStoreData(StorageName = "content_hash")]
    public string ContentHash { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "char_count")]
    public int CharCount { get; set; }

    [VectorStoreData(StorageName = "frontmatter_json")]
    public string? FrontmatterJson { get; set; }

    // Vector embedding (mxbai-embed-large = 1024 dimensions)
    [VectorStoreVector(
        Dimensions: 1024,
        DistanceFunction.CosineDistance,
        IndexKind.Hnsw,
        StorageName = "embedding")]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}
```

---

## Document Chunks Schema (for large documents)

```csharp
public class DocumentChunk
{
    [VectorStoreKey(StorageName = "id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Parent document reference
    [VectorStoreData(StorageName = "document_id", IsFilterable = true)]
    public string DocumentId { get; set; } = string.Empty;

    // Tenant isolation (inherited from parent)
    [VectorStoreData(StorageName = "project_name", IsFilterable = true)]
    public string ProjectName { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "branch_name", IsFilterable = true)]
    public string BranchName { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "path_hash", IsFilterable = true)]
    public string PathHash { get; set; } = string.Empty;

    // Promotion level (inherited from parent document, updated atomically)
    [VectorStoreData(StorageName = "promotion_level", IsFilterable = true)]
    public string PromotionLevel { get; set; } = "standard";

    // Chunk metadata
    [VectorStoreData(StorageName = "chunk_index")]
    public int ChunkIndex { get; set; }

    [VectorStoreData(StorageName = "header_path")]
    public string HeaderPath { get; set; } = string.Empty;  // e.g., "## Section > ### Subsection"

    [VectorStoreData(StorageName = "content")]
    public string Content { get; set; } = string.Empty;

    // Vector embedding
    [VectorStoreVector(
        Dimensions: 1024,
        DistanceFunction.CosineDistance,
        IndexKind.Hnsw,
        StorageName = "embedding")]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}
```

---

## External Documents Schema (separate collection)

External project documentation is indexed in a separate collection:

```csharp
public class ExternalDocument
{
    [VectorStoreKey(StorageName = "id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Tenant isolation keys (filterable)
    [VectorStoreData(StorageName = "project_name", IsFilterable = true)]
    public string ProjectName { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "branch_name", IsFilterable = true)]
    public string BranchName { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "path_hash", IsFilterable = true)]
    public string PathHash { get; set; } = string.Empty;

    // Document metadata
    [VectorStoreData(StorageName = "relative_path")]
    public string RelativePath { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "title")]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "summary")]
    public string? Summary { get; set; }

    [VectorStoreData(StorageName = "content_hash")]
    public string ContentHash { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "char_count")]
    public int CharCount { get; set; }

    // Vector embedding (mxbai-embed-large = 1024 dimensions)
    [VectorStoreVector(
        Dimensions: 1024,
        DistanceFunction.CosineDistance,
        IndexKind.Hnsw,
        StorageName = "embedding")]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}
```

**Why a separate collection?**
- External docs are read-only reference material, not institutional knowledge
- Separate collection prevents external docs from appearing in RAG queries
- Different indexing/sync lifecycle (based on `external_docs` config)
- Clearer mental model for users

---

## External Document Chunks Schema

External documents >500 lines are chunked using the same pattern as compound documents. Chunks are stored in a separate collection:

```csharp
public class ExternalDocumentChunk
{
    [VectorStoreKey(StorageName = "id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Parent external document reference
    [VectorStoreData(StorageName = "external_document_id", IsFilterable = true)]
    public string ExternalDocumentId { get; set; } = string.Empty;

    // Tenant isolation (inherited from parent)
    [VectorStoreData(StorageName = "project_name", IsFilterable = true)]
    public string ProjectName { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "branch_name", IsFilterable = true)]
    public string BranchName { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "path_hash", IsFilterable = true)]
    public string PathHash { get; set; } = string.Empty;

    // Chunk metadata
    [VectorStoreData(StorageName = "chunk_index")]
    public int ChunkIndex { get; set; }

    [VectorStoreData(StorageName = "header_path")]
    public string HeaderPath { get; set; } = string.Empty;  // e.g., "## Section > ### Subsection"

    [VectorStoreData(StorageName = "content")]
    public string Content { get; set; } = string.Empty;

    // Vector embedding (mxbai-embed-large = 1024 dimensions)
    [VectorStoreVector(
        Dimensions: 1024,
        DistanceFunction.CosineDistance,
        IndexKind.Hnsw,
        StorageName = "embedding")]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}
```

**Chunking rules** (same as compound documents):
- Threshold: Documents >500 lines are chunked
- Split points: `##` (H2) and `###` (H3) markdown headers
- Storage: Database-only; source files on disk are never modified
- Header path: Stored as hierarchy string (e.g., `"## API Reference > ### Authentication"`)

---

## Semantic Kernel Collection Setup

```csharp
// Using SearchPath approach for single schema
var connectionString =
    "Host=localhost;Port=5433;Database=compounding_docs;" +
    "Username=compounding;Password=compounding;SearchPath=compounding";

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.UseVector();
var dataSource = dataSourceBuilder.Build();

// Documents collection (compounding docs)
var documentsCollection = new PostgresCollection<string, CompoundDocument>(
    dataSource,
    "documents",
    ownsDataSource: false
);

// Chunks collection (for large documents)
var chunksCollection = new PostgresCollection<string, DocumentChunk>(
    dataSource,
    "document_chunks",
    ownsDataSource: false
);

// External documents collection (separate from compounding docs)
var externalDocsCollection = new PostgresCollection<string, ExternalDocument>(
    dataSource,
    "external_documents",
    ownsDataSource: false
);

// External document chunks collection (for large external docs)
var externalChunksCollection = new PostgresCollection<string, ExternalDocumentChunk>(
    dataSource,
    "external_document_chunks",
    ownsDataSource: false
);

// Ensure collections exist (creates tables if needed)
await documentsCollection.EnsureCollectionExistsAsync();
await chunksCollection.EnsureCollectionExistsAsync();
await externalDocsCollection.EnsureCollectionExistsAsync();
await externalChunksCollection.EnsureCollectionExistsAsync();
```

---

## Query Filtering

Tenant isolation during search:

```csharp
// Search within current tenant context
var filter = new VectorSearchFilter()
    .EqualTo("project_name", activeProject.ProjectName)
    .EqualTo("branch_name", activeProject.BranchName)
    .EqualTo("path_hash", activeProject.PathHash);

var searchResults = documentsCollection.SearchAsync(
    queryEmbedding,
    top: config.Retrieval.MaxResults,
    filter: filter
);
```

---

## Path Hash Generation

```csharp
public static string ComputePathHash(string absolutePath)
{
    // Normalize path separators
    var normalizedPath = absolutePath.Replace('\\', '/').TrimEnd('/');

    // Compute SHA256
    using var sha256 = SHA256.Create();
    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalizedPath));

    // Return first 16 characters (64 bits) for brevity
    return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
}
```

---

## HNSW Index Configuration

> **Background**: Detailed HNSW parameter tuning guidance, performance benchmarks, and scaling recommendations for pgvector. See [PostgreSQL with pgvector for RAG Applications - Vector Indexing](../../research/postgresql-pgvector-research.md#3-vector-indexing-with-pgvector).

**Decision**: Use **Medium** configuration for expected data volumes (1,000-10,000 docs per project/branch tenant).

### Parameters

| Parameter | Value | Description |
|-----------|-------|-------------|
| `m` | 32 | Max connections per node (higher = better recall, more memory) |
| `ef_construction` | 128 | Search depth during index building (higher = better quality, slower build) |
| `ef_search` | 64 | Search depth during queries (higher = better recall, slower queries) |

### Liquibase Migration

```sql
-- Create HNSW index with Medium configuration
CREATE INDEX documents_embedding_idx ON documents
USING hnsw (embedding vector_cosine_ops)
WITH (m = 32, ef_construction = 128);
```

At query time, set `ef_search`:
```sql
SET hnsw.ef_search = 64;
```

### Scale Guidance

| Scale | Doc Count | Configuration |
|-------|-----------|---------------|
| Small | < 1,000 | pgvector defaults (m=16) |
| **Medium** | 1K-10K | **Current choice** (m=32) |
| Large | 10K-100K | m=48, ef_construction=200 |
| Very Large | 100K+ | Consider partitioning by tenant |

---

## Open Questions

1. ~~What's the optimal HNSW index configuration for expected data sizes?~~ **Resolved**: Use Medium configuration (m=32, ef_construction=128, ef_search=64)
2. ~~Should there be additional indexes for common query patterns?~~ **Resolved**: See [liquibase-changelog.md](./liquibase-changelog.md) changeset 004
3. ~~How to handle database migrations when schema changes?~~ **Resolved**: See [liquibase-changelog.md](./liquibase-changelog.md)

