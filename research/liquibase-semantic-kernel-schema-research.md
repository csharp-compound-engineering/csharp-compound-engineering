# Semantic Kernel PostgreSQL Schema Management

**Research Date:** January 22, 2026
**Purpose:** Using Microsoft Semantic Kernel's built-in schema management (`EnsureCollectionExistsAsync()`) for PostgreSQL with pgvector, eliminating the need for Liquibase. Includes multi-schema support for multiple vector stores serving different RAG endpoints.

> **UPDATE (January 24, 2026):** This document has been updated to reflect the latest API changes. The PostgreSQL connector package has been renamed from `Microsoft.SemanticKernel.Connectors.Postgres` (deprecated) to `Microsoft.SemanticKernel.Connectors.PgVector`. The class `PostgresVectorStoreRecordCollection` is now `PostgresCollection<TKey, TRecord>`. See [semantic-kernel-pgvector-package-update.md](./semantic-kernel-pgvector-package-update.md) for complete details.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [EnsureCollectionExistsAsync() Capabilities](#2-ensurecollectionexistsasync-capabilities)
3. [PostgresCollection Configuration Options](#3-postgrescollection-configuration-options)
4. [Multiple Schemas for Multiple Vector Stores](#4-multiple-schemas-for-multiple-vector-stores)
5. [Multi-Collection Architecture](#5-multi-collection-architecture)
6. [Schema Creation Without Liquibase](#6-schema-creation-without-liquibase)
7. [Simplified Docker Compose Setup](#7-simplified-docker-compose-setup)
8. [Complete Implementation Examples](#8-complete-implementation-examples)
9. [Best Practices and Considerations](#9-best-practices-and-considerations)
10. [Sources](#10-sources)

---

## 1. Executive Summary

### Why Use Semantic Kernel's Built-in Schema Management?

Microsoft Semantic Kernel provides a **model-first approach** where your C# data models define the database schema. The `EnsureCollectionExistsAsync()` method handles table creation, eliminating the need for external migration tools like Liquibase.

### Key Benefits

| Benefit | Description |
|---------|-------------|
| **Single Source of Truth** | C# models define both application logic and database schema |
| **Zero Additional Tooling** | No Liquibase, Flyway, or other migration tools needed |
| **Automatic Index Creation** | HNSW indexes created when collection is created |
| **Idempotent Operations** | Safe to call multiple times in production |
| **Simplified Docker Compose** | Just PostgreSQL with pgvector, no migration container |

### Trade-offs

| Consideration | Impact |
|---------------|--------|
| No Rollback | Cannot roll back schema changes automatically |
| No Migration History | No tracking of when changes were applied |
| Limited Control | Schema derived from model, less fine-grained control |
| pgvector Prerequisite | Extension must still be created separately |

**Recommendation**: For most RAG applications, Semantic Kernel's built-in schema management is sufficient and significantly simpler than using Liquibase.

---

## 2. EnsureCollectionExistsAsync() Capabilities

### What It Creates

When you call `EnsureCollectionExistsAsync()`, Semantic Kernel:

1. **Creates the Table** with schema derived from your C# data model
2. **Sets Up Primary Key** from property marked with `[VectorStoreKey]`
3. **Creates Data Columns** from properties marked with `[VectorStoreData]`
4. **Creates Vector Columns** with `VECTOR(n)` type from `[VectorStoreVector]`
5. **Creates HNSW Index** automatically when `IndexKind.Hnsw` is specified

### Generated SQL Example

For this data model:

```csharp
public class KnowledgeDocument
{
    [VectorStoreKey(StorageName = "id")]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "content")]
    public string Content { get; set; } = string.Empty;

    [VectorStoreVector(
        Dimensions: 1024,
        DistanceFunction.CosineDistance,
        IndexKind.Hnsw,
        StorageName = "content_embedding")]
    public ReadOnlyMemory<float>? ContentEmbedding { get; set; }
}
```

Semantic Kernel generates approximately:

```sql
CREATE TABLE IF NOT EXISTS public."knowledge_base" (
    "id" VARCHAR(255) PRIMARY KEY NOT NULL,
    "content" TEXT,
    "content_embedding" VECTOR(1024)
);

CREATE INDEX IF NOT EXISTS idx_knowledge_base_content_embedding_hnsw
ON public."knowledge_base" USING hnsw (content_embedding vector_cosine_ops);
```

### Idempotency Behavior

| Scenario | Behavior |
|----------|----------|
| Table doesn't exist | Creates table with full schema and indexes |
| Table exists with matching schema | Does nothing (no-op) |
| Table exists with different schema | **Does not modify** - uses existing table |

**Important**: `EnsureCollectionExistsAsync()` will NOT modify an existing table. If your model changes, you must either:
- Drop and recreate the table manually
- Create a new collection with a different name
- Handle schema migrations separately

### What It Does NOT Create

| Item | Status |
|------|--------|
| pgvector Extension | **NOT created** - must be pre-installed |
| Custom Indexes (non-HNSW) | NOT created automatically |
| Stored Procedures/Functions | NOT created |
| PostgreSQL Schemas | NOT created - uses default or connection's search_path |
| Additional Constraints | NOT created beyond PRIMARY KEY |

### Usage Example

```csharp
using Microsoft.SemanticKernel.Connectors.PgVector;
using Npgsql;

// Setup data source with vector support
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.UseVector();  // CRITICAL: Must call this
var dataSource = dataSourceBuilder.Build();

// Create collection
var collection = new PostgresCollection<string, KnowledgeDocument>(
    dataSource,
    "knowledge_base",
    ownsDataSource: true
);

// Ensure table exists (idempotent - safe to call multiple times)
await collection.EnsureCollectionExistsAsync();

// Ready to use
await collection.UpsertAsync(new KnowledgeDocument { ... });
```

---

## 3. PostgresCollection Configuration Options

### PostgresCollectionOptions

As of May 2025, the options class was renamed from `PostgresVectorStoreRecordCollectionOptions` to `PostgresCollectionOptions`.

```csharp
var options = new PostgresCollectionOptions
{
    // Vector store record definition (optional - uses model attributes by default)
    Definition = new VectorStoreRecordDefinition
    {
        Properties = new List<VectorStoreRecordProperty>
        {
            new VectorStoreRecordKeyProperty("Id", typeof(string)),
            new VectorStoreRecordDataProperty("Content", typeof(string)),
            new VectorStoreRecordVectorProperty("ContentEmbedding", typeof(float[]))
            {
                Dimensions = 1024,
                DistanceFunction = DistanceFunction.CosineDistance,
                IndexKind = IndexKind.Hnsw
            }
        }
    }
};

var collection = new PostgresCollection<string, KnowledgeDocument>(
    dataSource,
    "knowledge_base",
    options
);
```

### Data Model Attributes

| Attribute | Purpose | Options |
|-----------|---------|---------|
| `[VectorStoreKey]` | Marks primary key | `StorageName` |
| `[VectorStoreData]` | Data columns | `StorageName`, `IsFilterable` |
| `[VectorStoreVector]` | Vector columns | `Dimensions`, `DistanceFunction`, `IndexKind`, `StorageName` |

### StorageName for Column Mapping

Use `StorageName` to map C# property names to database column names:

```csharp
[VectorStoreKey(StorageName = "document_id")]
public string Id { get; set; }  // Maps to "document_id" column

[VectorStoreData(StorageName = "document_content")]
public string Content { get; set; }  // Maps to "document_content" column
```

### Supported Vector Types

| C# Type | PostgreSQL Type |
|---------|-----------------|
| `ReadOnlyMemory<float>` | `VECTOR(n)` |
| `Embedding<float>` | `VECTOR(n)` |
| `float[]` | `VECTOR(n)` |
| `ReadOnlyMemory<Half>` | `VECTOR(n)` (half precision) |
| `BitArray` | Bit vector |
| `Pgvector.SparseVector` | Sparse vector |

### Distance Functions

| DistanceFunction | PostgreSQL Operator |
|------------------|---------------------|
| `CosineDistance` | `vector_cosine_ops` |
| `CosineSimilarity` | `vector_cosine_ops` |
| `DotProductSimilarity` | `vector_ip_ops` |
| `EuclideanDistance` | `vector_l2_ops` |
| `ManhattanDistance` | `vector_l1_ops` |

---

## 4. Multiple Schemas for Multiple Vector Stores

### PostgreSQL Schema Basics

PostgreSQL schemas are namespaces that contain database objects (tables, indexes, etc.). By default, objects are created in the `public` schema.

### Approach 1: Connection String SearchPath (Recommended)

Use different connection strings with different `SearchPath` values:

```csharp
// Schema for document embeddings
var docConnectionString =
    "Host=localhost;Port=5432;Database=rag_db;Username=rag_user;Password=secret;" +
    "SearchPath=rag_documents";

// Schema for code embeddings
var codeConnectionString =
    "Host=localhost;Port=5432;Database=rag_db;Username=rag_user;Password=secret;" +
    "SearchPath=rag_code";

// Schema for conversation embeddings
var chatConnectionString =
    "Host=localhost;Port=5432;Database=rag_db;Username=rag_user;Password=secret;" +
    "SearchPath=rag_conversations";
```

### Approach 2: Separate NpgsqlDataSource Per Schema

```csharp
public class MultiSchemaVectorStoreFactory
{
    private readonly string _baseConnectionString;

    public MultiSchemaVectorStoreFactory(string baseConnectionString)
    {
        _baseConnectionString = baseConnectionString;
    }

    public PostgresCollection<TKey, TRecord> CreateCollection<TKey, TRecord>(
        string schemaName,
        string collectionName)
        where TKey : notnull
    {
        // Build connection string with schema
        var connectionString = $"{_baseConnectionString};SearchPath={schemaName}";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        return new PostgresCollection<TKey, TRecord>(
            dataSource,
            collectionName,
            ownsDataSource: true  // Collection will dispose the data source
        );
    }
}
```

### Approach 3: Single DataSource with Schema in Table Name

While not ideal, you can prefix table names with schema:

```csharp
// Note: This may have limitations with Semantic Kernel
var collection = new PostgresCollection<string, Document>(
    dataSource,
    "rag_documents.knowledge_base"  // schema.table format
);
```

**Warning**: This approach may not work with all Semantic Kernel operations. The `SearchPath` approach is more reliable.

### Creating Schemas

Schemas must be created before use. Add to your init script:

```sql
-- Create schemas for different RAG purposes
CREATE SCHEMA IF NOT EXISTS rag_documents;
CREATE SCHEMA IF NOT EXISTS rag_code;
CREATE SCHEMA IF NOT EXISTS rag_conversations;

-- Grant permissions
GRANT ALL ON SCHEMA rag_documents TO rag_user;
GRANT ALL ON SCHEMA rag_code TO rag_user;
GRANT ALL ON SCHEMA rag_conversations TO rag_user;
```

### Multi-Schema Architecture Example

```
PostgreSQL Database: rag_db
├── public (default schema)
│   └── (Semantic Kernel doesn't use if SearchPath is set)
│
├── rag_documents
│   └── knowledge_base (table created by EnsureCollectionExistsAsync)
│       ├── id (PRIMARY KEY)
│       ├── title
│       ├── content
│       └── content_embedding VECTOR(1024)
│
├── rag_code
│   └── code_snippets (table created by EnsureCollectionExistsAsync)
│       ├── id (PRIMARY KEY)
│       ├── language
│       ├── code
│       └── code_embedding VECTOR(1024)
│
└── rag_conversations
    └── chat_history (table created by EnsureCollectionExistsAsync)
        ├── id (PRIMARY KEY)
        ├── message
        ├── role
        └── message_embedding VECTOR(1024)
```

---

## 5. Multi-Collection Architecture

### Different Record Types for Different RAG Use Cases

```csharp
// Document RAG
public class DocumentRecord
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData]
    public string Content { get; set; } = string.Empty;

    [VectorStoreData]
    public string? Source { get; set; }

    [VectorStoreVector(Dimensions: 1024, DistanceFunction.CosineDistance, IndexKind.Hnsw)]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}

// Code RAG
public class CodeRecord
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData]
    public string Language { get; set; } = string.Empty;

    [VectorStoreData]
    public string FilePath { get; set; } = string.Empty;

    [VectorStoreData]
    public string Code { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 1024, DistanceFunction.CosineDistance, IndexKind.Hnsw)]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}

// Conversation RAG
public class ConversationRecord
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData]
    public string SessionId { get; set; } = string.Empty;

    [VectorStoreData]
    public string Role { get; set; } = string.Empty;

    [VectorStoreData]
    public string Message { get; set; } = string.Empty;

    [VectorStoreData]
    public DateTime Timestamp { get; set; }

    [VectorStoreVector(Dimensions: 1024, DistanceFunction.CosineDistance, IndexKind.Hnsw)]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}
```

### Registering Multiple Collections in Dependency Injection

```csharp
public static class VectorStoreServiceExtensions
{
    public static IServiceCollection AddMultiSchemaVectorStores(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var baseConnectionString = configuration.GetConnectionString("PostgreSQL")!;

        // Document collection (rag_documents schema)
        services.AddSingleton(sp =>
        {
            var connString = $"{baseConnectionString};SearchPath=rag_documents";
            var builder = new NpgsqlDataSourceBuilder(connString);
            builder.UseVector();
            return new PostgresCollection<string, DocumentRecord>(
                builder.Build(),
                "knowledge_base",
                ownsDataSource: true
            );
        });

        // Code collection (rag_code schema)
        services.AddSingleton(sp =>
        {
            var connString = $"{baseConnectionString};SearchPath=rag_code";
            var builder = new NpgsqlDataSourceBuilder(connString);
            builder.UseVector();
            return new PostgresCollection<string, CodeRecord>(
                builder.Build(),
                "code_snippets",
                ownsDataSource: true
            );
        });

        // Conversation collection (rag_conversations schema)
        services.AddSingleton(sp =>
        {
            var connString = $"{baseConnectionString};SearchPath=rag_conversations";
            var builder = new NpgsqlDataSourceBuilder(connString);
            builder.UseVector();
            return new PostgresCollection<string, ConversationRecord>(
                builder.Build(),
                "chat_history",
                ownsDataSource: true
            );
        });

        return services;
    }
}
```

### Named Collections Pattern with Keyed Services (.NET 8+)

```csharp
public static class VectorStoreServiceExtensions
{
    public static IServiceCollection AddKeyedVectorStores(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var baseConnectionString = configuration.GetConnectionString("PostgreSQL")!;

        // Register with keyed services
        services.AddKeyedSingleton<PostgresCollection<string, DocumentRecord>>(
            "documents",
            (sp, key) => CreateCollection<DocumentRecord>(baseConnectionString, "rag_documents", "knowledge_base")
        );

        services.AddKeyedSingleton<PostgresCollection<string, CodeRecord>>(
            "code",
            (sp, key) => CreateCollection<CodeRecord>(baseConnectionString, "rag_code", "code_snippets")
        );

        services.AddKeyedSingleton<PostgresCollection<string, ConversationRecord>>(
            "conversations",
            (sp, key) => CreateCollection<ConversationRecord>(baseConnectionString, "rag_conversations", "chat_history")
        );

        return services;
    }

    private static PostgresCollection<string, TRecord> CreateCollection<TRecord>(
        string baseConnectionString,
        string schema,
        string collectionName)
    {
        var connString = $"{baseConnectionString};SearchPath={schema}";
        var builder = new NpgsqlDataSourceBuilder(connString);
        builder.UseVector();
        return new PostgresCollection<string, TRecord>(
            builder.Build(),
            collectionName,
            ownsDataSource: true
        );
    }
}

// Usage in a service
public class RagService
{
    private readonly PostgresCollection<string, DocumentRecord> _documentCollection;
    private readonly PostgresCollection<string, CodeRecord> _codeCollection;

    public RagService(
        [FromKeyedServices("documents")] PostgresCollection<string, DocumentRecord> documentCollection,
        [FromKeyedServices("code")] PostgresCollection<string, CodeRecord> codeCollection)
    {
        _documentCollection = documentCollection;
        _codeCollection = codeCollection;
    }
}
```

### Initialization at Startup

```csharp
public class VectorStoreInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VectorStoreInitializer> _logger;

    public VectorStoreInitializer(
        IServiceProvider serviceProvider,
        ILogger<VectorStoreInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing vector store collections...");

        // Initialize all collections
        var documentCollection = _serviceProvider
            .GetRequiredService<PostgresCollection<string, DocumentRecord>>();
        var codeCollection = _serviceProvider
            .GetRequiredService<PostgresCollection<string, CodeRecord>>();
        var conversationCollection = _serviceProvider
            .GetRequiredService<PostgresCollection<string, ConversationRecord>>();

        await Task.WhenAll(
            documentCollection.EnsureCollectionExistsAsync(cancellationToken),
            codeCollection.EnsureCollectionExistsAsync(cancellationToken),
            conversationCollection.EnsureCollectionExistsAsync(cancellationToken)
        );

        _logger.LogInformation("Vector store collections initialized successfully");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

---

## 6. Schema Creation Without Liquibase

### What's Still Needed

Even without Liquibase, you need a minimal PostgreSQL initialization:

1. **pgvector Extension** - Must be created before Semantic Kernel can create tables
2. **Custom Schemas** - If using multi-schema approach, schemas must exist
3. **User Permissions** - Appropriate GRANT statements

### Minimal Init Script (init.sql)

```sql
-- Enable pgvector extension (required before any vector operations)
CREATE EXTENSION IF NOT EXISTS vector;

-- Create schemas for multi-schema RAG architecture (optional)
CREATE SCHEMA IF NOT EXISTS rag_documents;
CREATE SCHEMA IF NOT EXISTS rag_code;
CREATE SCHEMA IF NOT EXISTS rag_conversations;

-- Grant schema permissions to application user
-- (Replace 'rag_user' with your actual username)
GRANT ALL ON SCHEMA rag_documents TO rag_user;
GRANT ALL ON SCHEMA rag_code TO rag_user;
GRANT ALL ON SCHEMA rag_conversations TO rag_user;

-- Grant default privileges for future objects
ALTER DEFAULT PRIVILEGES IN SCHEMA rag_documents GRANT ALL ON TABLES TO rag_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA rag_code GRANT ALL ON TABLES TO rag_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA rag_conversations GRANT ALL ON TABLES TO rag_user;
```

### Single-Schema Minimal Init (Simplest)

If using only the `public` schema:

```sql
-- That's it! Just enable pgvector
CREATE EXTENSION IF NOT EXISTS vector;
```

### Why pgvector Must Be Pre-Created

Semantic Kernel's `EnsureCollectionExistsAsync()` does **not** create the pgvector extension. The extension must exist before:
- The `VECTOR(n)` column type can be used
- Vector indexes can be created
- Any vector operations can be performed

Attempting to create a collection without pgvector results in an error like:
```
ERROR: type "vector" does not exist
```

---

## 7. Simplified Docker Compose Setup

### docker-compose.yml (Minimal)

```yaml
# docker-compose.yml
services:
  postgres:
    image: pgvector/pgvector:pg17
    container_name: rag-postgres
    restart: unless-stopped
    environment:
      POSTGRES_USER: ${POSTGRES_USER:-rag_user}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?POSTGRES_PASSWORD is required}
      POSTGRES_DB: ${POSTGRES_DB:-rag_db}
    ports:
      - "127.0.0.1:5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
      # Mount init script for pgvector extension
      - ./init.sql:/docker-entrypoint-initdb.d/init.sql:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER:-rag_user} -d ${POSTGRES_DB:-rag_db}"]
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

  # Optional: Ollama for local embeddings
  ollama:
    image: ollama/ollama:latest
    container_name: rag-ollama
    restart: unless-stopped
    ports:
      - "127.0.0.1:11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    profiles:
      - with-ollama

volumes:
  pgdata:
    driver: local
  ollama_data:
    driver: local
```

### Project Structure (Simplified)

```
project-root/
├── docker-compose.yml
├── .env
├── init.sql                    # Single SQL file for pgvector + schemas
└── src/
    └── RagMcpServer/
        ├── Program.cs
        └── Models/
            ├── DocumentRecord.cs
            ├── CodeRecord.cs
            └── ConversationRecord.cs
```

### Compare: Before (with Liquibase) vs After

**Before (Complex)**:
```
project-root/
├── docker-compose.yml          # ~90 lines with Liquibase service
├── .env
├── liquibase/
│   └── changelog/
│       ├── db.changelog-master.yaml
│       └── changes/
│           ├── 001-enable-pgvector.yaml
│           ├── 002-create-tables.yaml
│           └── 003-create-indexes.yaml
└── src/
    └── ...
```

**After (Simplified)**:
```
project-root/
├── docker-compose.yml          # ~40 lines, no Liquibase
├── .env
├── init.sql                    # 3-10 lines
└── src/
    └── ...
```

### Environment File (.env)

```bash
# .env
POSTGRES_USER=rag_user
POSTGRES_PASSWORD=your_secure_password_here
POSTGRES_DB=rag_db
```

---

## 8. Complete Implementation Examples

### Full Program.cs with Multi-Schema Support

```csharp
// Program.cs
#pragma warning disable SKEXP0070

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Connectors.PgVector;
using Microsoft.Extensions.VectorData;
using Npgsql;

// ============= Data Models =============

public class DocumentRecord
{
    [VectorStoreKey(StorageName = "id")]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "title")]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "content")]
    public string Content { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "source")]
    public string? Source { get; set; }

    [VectorStoreVector(
        Dimensions: 1024,
        DistanceFunction.CosineDistance,
        IndexKind.Hnsw,
        StorageName = "embedding")]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}

public class CodeRecord
{
    [VectorStoreKey(StorageName = "id")]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "language")]
    public string Language { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "file_path")]
    public string FilePath { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "code")]
    public string Code { get; set; } = string.Empty;

    [VectorStoreVector(
        Dimensions: 1024,
        DistanceFunction.CosineDistance,
        IndexKind.Hnsw,
        StorageName = "embedding")]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}

// ============= Vector Store Factory =============

public class VectorStoreFactory : IAsyncDisposable
{
    private readonly string _baseConnectionString;
    private readonly List<NpgsqlDataSource> _dataSources = new();

    public VectorStoreFactory(string baseConnectionString)
    {
        _baseConnectionString = baseConnectionString;
    }

    public PostgresCollection<string, TRecord> CreateCollection<TRecord>(
        string schemaName,
        string collectionName)
    {
        var connectionString = $"{_baseConnectionString};SearchPath={schemaName}";
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.UseVector();
        var dataSource = builder.Build();
        _dataSources.Add(dataSource);

        return new PostgresCollection<string, TRecord>(
            dataSource,
            collectionName,
            ownsDataSource: false  // Factory owns the data sources
        );
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var dataSource in _dataSources)
        {
            await dataSource.DisposeAsync();
        }
    }
}

// ============= Main Program =============

public class Program
{
    public static async Task Main(string[] args)
    {
        // Configuration
        const string baseConnectionString =
            "Host=localhost;Port=5432;Database=rag_db;Username=rag_user;Password=your_password";
        const string ollamaEndpoint = "http://localhost:11434";
        const string embeddingModel = "mxbai-embed-large";

        // Build Kernel with Ollama embedding service
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddOllamaTextEmbeddingGeneration(
            modelId: embeddingModel,
            endpoint: new Uri(ollamaEndpoint)
        );
        var kernel = kernelBuilder.Build();
        var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

        // Create vector store factory
        await using var factory = new VectorStoreFactory(baseConnectionString);

        // Create collections for different RAG purposes
        var documentCollection = factory.CreateCollection<DocumentRecord>("rag_documents", "knowledge_base");
        var codeCollection = factory.CreateCollection<CodeRecord>("rag_code", "code_snippets");

        // Initialize collections (creates tables if they don't exist)
        Console.WriteLine("Initializing vector store collections...");
        await Task.WhenAll(
            documentCollection.EnsureCollectionExistsAsync(),
            codeCollection.EnsureCollectionExistsAsync()
        );
        Console.WriteLine("Collections initialized successfully!");

        // Example: Index a document
        var docContent = "Semantic Kernel is Microsoft's SDK for building AI applications.";
        var docEmbedding = await embeddingService.GenerateEmbeddingAsync(docContent);

        await documentCollection.UpsertAsync(new DocumentRecord
        {
            Id = "doc-001",
            Title = "Introduction to Semantic Kernel",
            Content = docContent,
            Source = "microsoft.com",
            Embedding = docEmbedding
        });
        Console.WriteLine("Document indexed successfully!");

        // Example: Index code
        var codeContent = "public class HelloWorld { public static void Main() { } }";
        var codeEmbedding = await embeddingService.GenerateEmbeddingAsync(codeContent);

        await codeCollection.UpsertAsync(new CodeRecord
        {
            Id = "code-001",
            Language = "csharp",
            FilePath = "/src/HelloWorld.cs",
            Code = codeContent,
            Embedding = codeEmbedding
        });
        Console.WriteLine("Code indexed successfully!");

        // Example: Search documents
        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync("What is Semantic Kernel?");
        var searchResults = documentCollection.SearchAsync(queryEmbedding, top: 3);

        Console.WriteLine("\nSearch Results:");
        await foreach (var result in searchResults)
        {
            Console.WriteLine($"  - {result.Record.Title} (Score: {result.Score:F4})");
        }
    }
}
```

### ASP.NET Core Integration with DI

```csharp
// Program.cs for ASP.NET Core
var builder = WebApplication.CreateBuilder(args);

// Add vector stores
builder.Services.AddMultiSchemaVectorStores(builder.Configuration);

// Add hosted service to initialize collections at startup
builder.Services.AddHostedService<VectorStoreInitializer>();

// Add Semantic Kernel with Ollama
builder.Services.AddKernel()
    .AddOllamaTextEmbeddingGeneration(
        modelId: "mxbai-embed-large",
        endpoint: new Uri("http://localhost:11434")
    );

var app = builder.Build();

// ... rest of app configuration
```

### appsettings.json

```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Port=5432;Database=rag_db;Username=rag_user;Password=your_password"
  },
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "EmbeddingModel": "mxbai-embed-large"
  },
  "VectorStore": {
    "Schemas": {
      "Documents": "rag_documents",
      "Code": "rag_code",
      "Conversations": "rag_conversations"
    },
    "EmbeddingDimensions": 1024
  }
}
```

---

## 9. Best Practices and Considerations

### When to Use EnsureCollectionExistsAsync()

| Use Case | Recommendation |
|----------|----------------|
| Prototypes/POCs | Excellent - quick setup |
| Single-developer projects | Excellent - no coordination needed |
| Simple schemas | Excellent - model-first works well |
| Production with stable schema | Good - once schema is finalized |
| Frequently changing schemas | Consider alternatives |
| Complex migrations | Consider Liquibase or EF Migrations |
| Audit requirements | Consider Liquibase for tracking |

### Schema Change Handling

Since `EnsureCollectionExistsAsync()` doesn't modify existing tables:

```csharp
// Option 1: Version collection names
var collection = new PostgresCollection<string, DocumentRecordV2>(
    dataSource,
    "knowledge_base_v2"  // New version
);

// Option 2: Manual migration (one-time script)
// Run via psql or admin tool:
// ALTER TABLE knowledge_base ADD COLUMN new_field TEXT;

// Option 3: Drop and recreate (dev only!)
await collection.DeleteCollectionAsync();
await collection.EnsureCollectionExistsAsync();
```

### Performance Considerations

| Consideration | Recommendation |
|---------------|----------------|
| HNSW Index Tuning | Create indexes manually with custom `m` and `ef_construction` |
| Connection Pooling | Use single NpgsqlDataSource per schema |
| Bulk Inserts | Use batch upsert operations |
| Index Creation | HNSW works best with some data; consider delayed indexing |

### Custom HNSW Index Tuning

If you need custom HNSW parameters, create indexes manually:

```sql
-- Drop auto-created index
DROP INDEX IF EXISTS idx_knowledge_base_embedding_hnsw;

-- Create with custom parameters
CREATE INDEX idx_knowledge_base_embedding_hnsw
ON rag_documents.knowledge_base USING hnsw (embedding vector_cosine_ops)
WITH (m = 24, ef_construction = 100);
```

### Error Handling

```csharp
try
{
    await collection.EnsureCollectionExistsAsync();
}
catch (NpgsqlException ex) when (ex.Message.Contains("vector"))
{
    throw new InvalidOperationException(
        "pgvector extension is not installed. Run: CREATE EXTENSION IF NOT EXISTS vector;",
        ex);
}
catch (NpgsqlException ex) when (ex.Message.Contains("schema"))
{
    throw new InvalidOperationException(
        $"Schema does not exist. Ensure init.sql creates required schemas.",
        ex);
}
```

---

## 10. Sources

### Microsoft Semantic Kernel Documentation
- [Using the Semantic Kernel Postgres Vector Store connector (Preview)](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/postgres-connector)
- [Vector Store changes - May 2025](https://learn.microsoft.com/en-us/semantic-kernel/support/migration/vectorstore-may-2025)
- [What are Semantic Kernel Vector Stores?](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/)
- [Defining Your Data Model](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/defining-your-data-model)
- [Generating embeddings for Vector Store connectors](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/embedding-generation)

### Npgsql Documentation
- [Connection String Parameters](https://www.npgsql.org/doc/connection-string-parameters.html)
- [NpgsqlConnectionStringBuilder Class](https://www.npgsql.org/doc/api/Npgsql.NpgsqlConnectionStringBuilder.html)

### pgvector Resources
- [pgvector GitHub Repository](https://github.com/pgvector/pgvector)
- [pgvector Docker Image](https://hub.docker.com/r/pgvector/pgvector)
- [pgvector-dotnet GitHub](https://github.com/pgvector/pgvector-dotnet)
- [HNSW Indexes with Postgres and pgvector](https://www.crunchydata.com/blog/hnsw-indexes-with-postgres-and-pgvector)

### Sample Repositories and Tutorials
- [Azure-Samples/postgres-semantic-kernel-examples](https://github.com/Azure-Samples/postgres-semantic-kernel-examples)
- [Semantic Kernel with Dapper, PostgreSQL, and PgVector](https://github.com/davek-dev/semantic-kernel-demo)
- [Beginner's Guide to Semantic Kernel with Dapper, PostgreSQL](https://davek.dev/beginners-guide-to-semantic-kernel-with-dapper-postgresql-and-pgvector-in-c)
- [Setup PostgreSQL with pgvector in Docker](https://dev.to/ninjasoards/setup-postgresql-w-pgvector-in-a-docker-container-4ghe)
- [Part 1: Setup with PostgreSQL and pgvector](https://dev.to/yukaty/setting-up-postgresql-with-pgvector-using-docker-hcl)

### NuGet Packages
- [Microsoft.SemanticKernel.Connectors.PgVector](https://www.nuget.org/packages/Microsoft.SemanticKernel.Connectors.PgVector)
- [Microsoft.SemanticKernel.Connectors.PgVector](https://www.nuget.org/packages/Microsoft.SemanticKernel.Connectors.PgVector)

### Related Resources
- [Kernel Memory PostgreSQL Extension](https://microsoft.github.io/kernel-memory/extensions/memory-db/postgres)
- [Semantic Kernel - Neon Docs](https://neon.com/docs/ai/semantic-kernel)
- [PostgreSQL as Vector Database Complete Guide](https://dbadataverse.com/tech/postgresql/2025/12/pgvector-postgresql-vector-database-guide)

---

*This research report was updated for the csharp-compound-engineering project to document using Microsoft Semantic Kernel's built-in schema management for PostgreSQL with pgvector, replacing the previous Liquibase-based approach.*
