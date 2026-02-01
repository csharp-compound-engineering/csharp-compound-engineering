# Phase 041: Semantic Kernel PostgreSQL Vector Store Connector

> **Status**: PLANNED
> **Category**: Database & Storage
> **Estimated Effort**: M
> **Prerequisites**: Phase 028 (Semantic Kernel Integration), Phase 003 (PostgreSQL with pgvector Setup)

---

## Spec References

- [mcp-server/database-schema.md - Schema Management Responsibilities](../spec/mcp-server/database-schema.md#schema-management-responsibilities)
- [mcp-server/database-schema.md - Semantic Kernel Collection Setup](../spec/mcp-server/database-schema.md#semantic-kernel-collection-setup)
- [mcp-server/database-schema.md - Documents Schema](../spec/mcp-server/database-schema.md#documents-schema-semantic-kernel-model)
- [research/semantic-kernel-pgvector-package-update.md](../research/semantic-kernel-pgvector-package-update.md)
- [research/postgresql-pgvector-research.md - Connection from .NET/C#](../research/postgresql-pgvector-research.md#6-connection-from-netc)

---

## Objectives

1. Add `Microsoft.SemanticKernel.Connectors.PgVector` NuGet package to the MCP Server project
2. Configure `NpgsqlDataSource` with pgvector support via `UseVector()` extension
3. Create `PostgresCollection<TKey, TRecord>` instances for each vector collection
4. Implement connection string configuration with `SearchPath=compounding` schema targeting
5. Call `EnsureCollectionExistsAsync()` at startup to auto-create vector tables
6. Configure HNSW index parameters (m=32, ef_construction=128) via model attributes
7. Register vector store services in dependency injection container

---

## Acceptance Criteria

- [ ] `Microsoft.SemanticKernel.Connectors.PgVector` package version 1.70.0-preview or later added
- [ ] `NpgsqlDataSource` configured with `UseVector()` for pgvector type support
- [ ] Connection string includes `SearchPath=compounding` for schema isolation
- [ ] `PostgresCollection<string, CompoundDocument>` configured for documents collection
- [ ] `PostgresCollection<string, DocumentChunk>` configured for chunks collection
- [ ] `PostgresCollection<string, ExternalDocument>` configured for external documents
- [ ] `PostgresCollection<string, ExternalDocumentChunk>` configured for external document chunks
- [ ] `EnsureCollectionExistsAsync()` called for all collections at startup
- [ ] `#pragma warning disable SKEXP0020` added for experimental PostgreSQL connector
- [ ] Unit tests verify collection configuration
- [ ] Integration test verifies table creation in PostgreSQL

---

## Implementation Notes

### 1. NuGet Package Installation

Add the Semantic Kernel PostgreSQL connector package (package renamed from `Connectors.Postgres`):

```bash
cd src/CompoundDocs.McpServer
dotnet add package Microsoft.SemanticKernel.Connectors.PgVector --prerelease
```

**Important**: The `--prerelease` flag is required as this package is still in preview status.

### 2. Data Model Classes

The model classes are defined according to the spec. Place these in `CompoundDocs.Common`:

```csharp
// src/CompoundDocs.Common/Models/CompoundDocument.cs
#pragma warning disable SKEXP0020 // PostgreSQL connector is experimental

using Microsoft.Extensions.VectorData;

namespace CompoundDocs.Common.Models;

/// <summary>
/// Represents a compound document stored in the vector database.
/// Schema created automatically by Semantic Kernel EnsureCollectionExistsAsync().
/// </summary>
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

```csharp
// src/CompoundDocs.Common/Models/DocumentChunk.cs
#pragma warning disable SKEXP0020

using Microsoft.Extensions.VectorData;

namespace CompoundDocs.Common.Models;

/// <summary>
/// Represents a chunk of a large document (>500 lines) for semantic search.
/// Parent document reference maintained for context retrieval.
/// </summary>
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
    public string HeaderPath { get; set; } = string.Empty; // e.g., "## Section > ### Subsection"

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

```csharp
// src/CompoundDocs.Common/Models/ExternalDocument.cs
#pragma warning disable SKEXP0020

using Microsoft.Extensions.VectorData;

namespace CompoundDocs.Common.Models;

/// <summary>
/// External project documentation indexed separately from compound documents.
/// Read-only reference material that does not appear in primary RAG queries.
/// </summary>
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

```csharp
// src/CompoundDocs.Common/Models/ExternalDocumentChunk.cs
#pragma warning disable SKEXP0020

using Microsoft.Extensions.VectorData;

namespace CompoundDocs.Common.Models;

/// <summary>
/// Chunks for external documents >500 lines, following same pattern as DocumentChunk.
/// </summary>
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
    public string HeaderPath { get; set; } = string.Empty;

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

### 3. NpgsqlDataSource Configuration

Configure the data source with pgvector support:

```csharp
// src/CompoundDocs.McpServer/Infrastructure/PostgresDataSourceFactory.cs
#pragma warning disable SKEXP0020

using Npgsql;

namespace CompoundDocs.McpServer.Infrastructure;

/// <summary>
/// Factory for creating NpgsqlDataSource configured for pgvector support.
/// Uses SearchPath to target the compounding schema.
/// </summary>
public sealed class PostgresDataSourceFactory : IDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private bool _disposed;

    /// <summary>
    /// Creates a new factory with the specified connection string.
    /// The connection string should include SearchPath=compounding for schema targeting.
    /// </summary>
    public PostgresDataSourceFactory(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);

        // CRITICAL: Required for pgvector support
        builder.UseVector();

        _dataSource = builder.Build();
    }

    /// <summary>
    /// Gets the configured NpgsqlDataSource for use with PostgresCollection.
    /// </summary>
    public NpgsqlDataSource DataSource => _dataSource;

    public void Dispose()
    {
        if (!_disposed)
        {
            _dataSource.Dispose();
            _disposed = true;
        }
    }
}
```

### 4. Vector Store Collections Service

Create a service to manage all vector collections:

```csharp
// src/CompoundDocs.McpServer/Services/VectorStoreCollections.cs
#pragma warning disable SKEXP0020

using CompoundDocs.Common.Models;
using Microsoft.SemanticKernel.Connectors.Postgres;
using Npgsql;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Provides access to all Semantic Kernel vector store collections.
/// Collections are auto-created via EnsureCollectionExistsAsync() at startup.
/// </summary>
public interface IVectorStoreCollections
{
    /// <summary>Compound documents collection.</summary>
    PostgresCollection<string, CompoundDocument> Documents { get; }

    /// <summary>Document chunks collection (for large documents).</summary>
    PostgresCollection<string, DocumentChunk> DocumentChunks { get; }

    /// <summary>External documents collection.</summary>
    PostgresCollection<string, ExternalDocument> ExternalDocuments { get; }

    /// <summary>External document chunks collection.</summary>
    PostgresCollection<string, ExternalDocumentChunk> ExternalDocumentChunks { get; }

    /// <summary>
    /// Ensures all collections exist in the database.
    /// Creates tables if they do not exist.
    /// </summary>
    Task EnsureCollectionsExistAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Production implementation of IVectorStoreCollections using PostgresCollection.
/// </summary>
public sealed class VectorStoreCollections : IVectorStoreCollections, IDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private bool _disposed;

    /// <summary>Collection name for compound documents.</summary>
    public const string DocumentsCollectionName = "documents";

    /// <summary>Collection name for document chunks.</summary>
    public const string DocumentChunksCollectionName = "document_chunks";

    /// <summary>Collection name for external documents.</summary>
    public const string ExternalDocumentsCollectionName = "external_documents";

    /// <summary>Collection name for external document chunks.</summary>
    public const string ExternalDocumentChunksCollectionName = "external_document_chunks";

    public PostgresCollection<string, CompoundDocument> Documents { get; }
    public PostgresCollection<string, DocumentChunk> DocumentChunks { get; }
    public PostgresCollection<string, ExternalDocument> ExternalDocuments { get; }
    public PostgresCollection<string, ExternalDocumentChunk> ExternalDocumentChunks { get; }

    public VectorStoreCollections(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));

        // Create collections with shared data source (ownsDataSource: false)
        // The data source lifecycle is managed by this class
        Documents = new PostgresCollection<string, CompoundDocument>(
            _dataSource,
            DocumentsCollectionName,
            ownsDataSource: false
        );

        DocumentChunks = new PostgresCollection<string, DocumentChunk>(
            _dataSource,
            DocumentChunksCollectionName,
            ownsDataSource: false
        );

        ExternalDocuments = new PostgresCollection<string, ExternalDocument>(
            _dataSource,
            ExternalDocumentsCollectionName,
            ownsDataSource: false
        );

        ExternalDocumentChunks = new PostgresCollection<string, ExternalDocumentChunk>(
            _dataSource,
            ExternalDocumentChunksCollectionName,
            ownsDataSource: false
        );
    }

    public async Task EnsureCollectionsExistAsync(CancellationToken cancellationToken = default)
    {
        // Create all tables if they don't exist
        // This is idempotent - safe to call multiple times
        await Documents.EnsureCollectionExistsAsync(cancellationToken);
        await DocumentChunks.EnsureCollectionExistsAsync(cancellationToken);
        await ExternalDocuments.EnsureCollectionExistsAsync(cancellationToken);
        await ExternalDocumentChunks.EnsureCollectionExistsAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Collections don't own the data source, no disposal needed
            // Data source disposed by PostgresDataSourceFactory
            _disposed = true;
        }
    }
}
```

### 5. Connection String Configuration

Configure the connection string with schema targeting:

```csharp
// Connection string format with SearchPath for schema isolation
// This ensures Semantic Kernel creates tables in the 'compounding' schema
public static class PostgresConnectionStrings
{
    /// <summary>
    /// Builds a connection string with SearchPath for schema targeting.
    /// </summary>
    public static string Build(
        string host = "localhost",
        int port = 5433,
        string database = "compounding_docs",
        string username = "compounding",
        string password = "compounding",
        string schema = "compounding")
    {
        return $"Host={host};Port={port};Database={database};" +
               $"Username={username};Password={password};SearchPath={schema}";
    }
}
```

### 6. Startup Collection Initialization

Create a hosted service to ensure collections exist at startup:

```csharp
// src/CompoundDocs.McpServer/Services/VectorStoreInitializer.cs
#pragma warning disable SKEXP0020

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Hosted service that ensures all vector store collections exist at startup.
/// This creates the tables in PostgreSQL if they don't already exist.
/// </summary>
public sealed class VectorStoreInitializer : IHostedService
{
    private readonly IVectorStoreCollections _collections;
    private readonly ILogger<VectorStoreInitializer> _logger;

    public VectorStoreInitializer(
        IVectorStoreCollections collections,
        ILogger<VectorStoreInitializer> logger)
    {
        _collections = collections;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing vector store collections...");

        try
        {
            await _collections.EnsureCollectionsExistAsync(cancellationToken);

            _logger.LogInformation(
                "Vector store collections initialized: {Collections}",
                string.Join(", ", new[]
                {
                    VectorStoreCollections.DocumentsCollectionName,
                    VectorStoreCollections.DocumentChunksCollectionName,
                    VectorStoreCollections.ExternalDocumentsCollectionName,
                    VectorStoreCollections.ExternalDocumentChunksCollectionName
                }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to initialize vector store collections. " +
                "Ensure PostgreSQL is running and the compounding schema exists.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

### 7. Compounding Schema Creation

The compounding schema must exist before Semantic Kernel creates tables. Add to Liquibase:

```sql
-- docker/postgres/changelog/changes/005-create-compounding-schema/change.sql
CREATE SCHEMA IF NOT EXISTS compounding;

COMMENT ON SCHEMA compounding IS
    'Vector collections managed by Semantic Kernel - documents, chunks, external docs';
```

**Note**: This schema is empty initially. Semantic Kernel's `EnsureCollectionExistsAsync()` creates the actual tables based on the C# model attributes.

### 8. Service Registration

Register all PostgreSQL vector store services:

```csharp
// In ServiceCollectionExtensions.cs
public static IServiceCollection AddPostgresVectorStore(
    this IServiceCollection services,
    string connectionString)
{
    // Data source factory (singleton - manages connection pool)
    services.AddSingleton<PostgresDataSourceFactory>(sp =>
        new PostgresDataSourceFactory(connectionString));

    // Extract NpgsqlDataSource from factory
    services.AddSingleton(sp =>
        sp.GetRequiredService<PostgresDataSourceFactory>().DataSource);

    // Vector store collections
    services.AddSingleton<IVectorStoreCollections, VectorStoreCollections>();

    // Startup initialization
    services.AddHostedService<VectorStoreInitializer>();

    return services;
}
```

### 9. HNSW Index Configuration

The HNSW index parameters are specified via model attributes:

```csharp
// The VectorStoreVector attribute configures:
// - Dimensions: 1024 (matches mxbai-embed-large output)
// - DistanceFunction.CosineDistance (optimal for text embeddings)
// - IndexKind.Hnsw (fast approximate nearest neighbor search)
[VectorStoreVector(
    Dimensions: 1024,
    DistanceFunction.CosineDistance,
    IndexKind.Hnsw,
    StorageName = "embedding")]
public ReadOnlyMemory<float>? Embedding { get; set; }
```

For advanced HNSW tuning (m=32, ef_construction=128), set at query time:

```csharp
// Set ef_search for better recall at query time
await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
await using var cmd = new NpgsqlCommand("SET hnsw.ef_search = 64", connection);
await cmd.ExecuteNonQueryAsync(cancellationToken);
```

---

## Dependencies

### Depends On

- **Phase 003**: PostgreSQL with pgvector Setup - Database must be running with pgvector extension
- **Phase 028**: Semantic Kernel Integration - Kernel builder must be configured

### Blocks

- **Phase 042+**: Document Indexing Service - Needs vector collections to store embeddings
- **Phase 043+**: RAG Query Service - Needs vector collections for similarity search

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Services/VectorStoreCollectionsTests.cs
public class VectorStoreCollectionsTests
{
    [Fact]
    public void Constructor_CreatesAllCollections()
    {
        // Arrange
        var mockDataSource = CreateMockDataSource();

        // Act
        using var collections = new VectorStoreCollections(mockDataSource);

        // Assert
        Assert.NotNull(collections.Documents);
        Assert.NotNull(collections.DocumentChunks);
        Assert.NotNull(collections.ExternalDocuments);
        Assert.NotNull(collections.ExternalDocumentChunks);
    }

    [Fact]
    public void CollectionNames_MatchExpectedValues()
    {
        Assert.Equal("documents", VectorStoreCollections.DocumentsCollectionName);
        Assert.Equal("document_chunks", VectorStoreCollections.DocumentChunksCollectionName);
        Assert.Equal("external_documents", VectorStoreCollections.ExternalDocumentsCollectionName);
        Assert.Equal("external_document_chunks", VectorStoreCollections.ExternalDocumentChunksCollectionName);
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Services/VectorStoreIntegrationTests.cs
[Trait("Category", "Integration")]
public class VectorStoreIntegrationTests : IClassFixture<PostgresFixture>
{
    private readonly IVectorStoreCollections _collections;

    public VectorStoreIntegrationTests(PostgresFixture fixture)
    {
        _collections = fixture.GetService<IVectorStoreCollections>();
    }

    [Fact]
    public async Task EnsureCollectionsExistAsync_CreatesTablesInDatabase()
    {
        // Act
        await _collections.EnsureCollectionsExistAsync();

        // Assert - verify tables exist by attempting operations
        var doc = new CompoundDocument
        {
            ProjectName = "test-project",
            BranchName = "main",
            PathHash = "abc123",
            RelativePath = "test.md",
            Title = "Test Document"
        };

        await _collections.Documents.UpsertAsync(doc);
        var retrieved = await _collections.Documents.GetAsync(doc.Id);

        Assert.NotNull(retrieved);
        Assert.Equal("test-project", retrieved.ProjectName);

        // Cleanup
        await _collections.Documents.DeleteAsync(doc.Id);
    }

    [Fact]
    public async Task VectorSearch_FindsSimilarDocuments()
    {
        // Arrange - create test documents with embeddings
        var embedding1 = GenerateTestEmbedding(1.0f);
        var embedding2 = GenerateTestEmbedding(0.9f); // Similar
        var embedding3 = GenerateTestEmbedding(0.1f); // Dissimilar

        var doc1 = CreateTestDocument("doc1", embedding1);
        var doc2 = CreateTestDocument("doc2", embedding2);
        var doc3 = CreateTestDocument("doc3", embedding3);

        await _collections.Documents.UpsertAsync(doc1);
        await _collections.Documents.UpsertAsync(doc2);
        await _collections.Documents.UpsertAsync(doc3);

        try
        {
            // Act - search for documents similar to embedding1
            var searchResults = await _collections.Documents.SearchAsync(
                embedding1,
                top: 2);

            var results = await searchResults.ToListAsync();

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.Record.Id == doc1.Id);
            Assert.Contains(results, r => r.Record.Id == doc2.Id);
        }
        finally
        {
            // Cleanup
            await _collections.Documents.DeleteAsync(doc1.Id);
            await _collections.Documents.DeleteAsync(doc2.Id);
            await _collections.Documents.DeleteAsync(doc3.Id);
        }
    }

    private static ReadOnlyMemory<float> GenerateTestEmbedding(float seed)
    {
        var embedding = new float[1024];
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = seed * (i / 1024f);
        }
        return new ReadOnlyMemory<float>(embedding);
    }

    private static CompoundDocument CreateTestDocument(string id, ReadOnlyMemory<float> embedding)
    {
        return new CompoundDocument
        {
            Id = id,
            ProjectName = "integration-test",
            BranchName = "main",
            PathHash = "testhash",
            RelativePath = $"{id}.md",
            Title = $"Test {id}",
            Embedding = embedding
        };
    }
}
```

### Manual Verification

```bash
# 1. Ensure PostgreSQL is running
docker compose -p csharp-compounding-docs ps

# 2. Connect to database and verify compounding schema exists
docker exec csharp-compounding-docs-postgres psql -U compounding -d compounding_docs -c "\dn"

# 3. After running the MCP server, verify tables were created
docker exec csharp-compounding-docs-postgres psql -U compounding -d compounding_docs -c "\dt compounding.*"

# Expected tables:
# - compounding.documents
# - compounding.document_chunks
# - compounding.external_documents
# - compounding.external_document_chunks

# 4. Verify vector columns exist
docker exec csharp-compounding-docs-postgres psql -U compounding -d compounding_docs -c "\d compounding.documents"

# 5. Verify HNSW indexes were created
docker exec csharp-compounding-docs-postgres psql -U compounding -d compounding_docs -c "
SELECT indexname, indexdef
FROM pg_indexes
WHERE schemaname = 'compounding'
AND indexdef LIKE '%hnsw%';
"
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj` | Modify | Add Microsoft.SemanticKernel.Connectors.PgVector package |
| `src/CompoundDocs.Common/Models/CompoundDocument.cs` | Create | Document model with vector attributes |
| `src/CompoundDocs.Common/Models/DocumentChunk.cs` | Create | Chunk model with vector attributes |
| `src/CompoundDocs.Common/Models/ExternalDocument.cs` | Create | External document model |
| `src/CompoundDocs.Common/Models/ExternalDocumentChunk.cs` | Create | External document chunk model |
| `src/CompoundDocs.McpServer/Infrastructure/PostgresDataSourceFactory.cs` | Create | NpgsqlDataSource factory with UseVector() |
| `src/CompoundDocs.McpServer/Services/VectorStoreCollections.cs` | Create | PostgresCollection instances |
| `src/CompoundDocs.McpServer/Services/VectorStoreInitializer.cs` | Create | Startup collection creation |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Add AddPostgresVectorStore |
| `docker/postgres/changelog/changes/005-create-compounding-schema/change.xml` | Create | Liquibase changeset for schema |
| `docker/postgres/changelog/changes/005-create-compounding-schema/change.sql` | Create | SQL for compounding schema |
| `tests/CompoundDocs.Tests/Services/VectorStoreCollectionsTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.IntegrationTests/Services/VectorStoreIntegrationTests.cs` | Create | Integration tests |

---

## Configuration Reference

### Connection String Format

```
Host=localhost;Port=5433;Database=compounding_docs;Username=compounding;Password=compounding;SearchPath=compounding
```

| Parameter | Default | Description |
|-----------|---------|-------------|
| Host | localhost | PostgreSQL server hostname |
| Port | 5433 | Non-default port to avoid conflicts |
| Database | compounding_docs | Database name |
| Username | compounding | Database user |
| Password | compounding | Database password |
| SearchPath | compounding | Schema for vector collections |

### Vector Configuration

| Setting | Value | Source |
|---------|-------|--------|
| Embedding Dimensions | 1024 | Model attribute, matches mxbai-embed-large |
| Distance Function | CosineDistance | Optimal for text embeddings |
| Index Kind | HNSW | Fast approximate nearest neighbor |
| ef_search | 64 | Set at query time for recall tuning |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| PostgreSQL not running | VectorStoreInitializer logs clear error with connection details |
| pgvector extension not enabled | Phase 003 ensures extension is enabled before application starts |
| Schema doesn't exist | Liquibase changeset 005 creates compounding schema before SK runs |
| Dimension mismatch | Model attributes enforce 1024 dimensions; embedding service validates |
| Table already exists | EnsureCollectionExistsAsync() is idempotent |
| Connection pool exhaustion | NpgsqlDataSource manages pooling efficiently |

---

## Success Criteria

1. `Microsoft.SemanticKernel.Connectors.PgVector` package installed successfully
2. MCP server starts without connection errors
3. All four vector tables created in `compounding` schema
4. HNSW indexes present on embedding columns
5. Documents can be upserted and retrieved via PostgresCollection
6. Vector similarity search returns ranked results
7. Integration tests pass with real PostgreSQL instance
