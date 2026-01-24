# Semantic Kernel PostgreSQL Connector API Updates (January 2026)

**Research Date:** January 24, 2026
**Purpose:** Document the package rename and API changes for the Semantic Kernel PostgreSQL/pgvector connector

---

## Package Rename

The Semantic Kernel PostgreSQL connector package has been renamed:

| Status | Package Name |
|--------|--------------|
| **Old (Deprecated)** | `Microsoft.SemanticKernel.Connectors.Postgres` |
| **New (Current)** | `Microsoft.SemanticKernel.Connectors.PgVector` |

**Important:** The old package name is deprecated. Always use the new package name for new projects.

---

## Package Details

| Property | Value |
|----------|-------|
| **Package** | `Microsoft.SemanticKernel.Connectors.PgVector` |
| **Latest Version** | `1.70.0-preview` (January 23, 2026) |
| **Status** | Preview/Experimental |
| **Namespace** | `Microsoft.SemanticKernel.Connectors.Postgres` (unchanged) |

### Installation

```bash
dotnet add package Microsoft.SemanticKernel.Connectors.PgVector --prerelease
```

**Note:** The `--prerelease` flag is required as this package is still in preview status.

---

## Class Name Changes

| Old Name | New Name |
|----------|----------|
| `PostgresVectorStoreRecordCollection` | `PostgresCollection<TKey, TRecord>` |
| `PostgresVectorStoreRecordCollectionOptions` | `PostgresCollectionOptions` |

---

## Constructor Overloads

The `PostgresCollection<TKey, TRecord>` class supports two constructor patterns:

### With NpgsqlDataSource (Recommended)

```csharp
var dataSourceBuilder = new NpgsqlDataSourceBuilder("<Connection String>");
dataSourceBuilder.UseVector();  // CRITICAL - Required for pgvector support
var dataSource = dataSourceBuilder.Build();

var collection = new PostgresCollection<string, Hotel>(
    dataSource,           // NpgsqlDataSource
    "skhotels",          // string collectionName
    ownsDataSource: true // bool ownsDataSource
);
```

### With Connection String

```csharp
var collection = new PostgresCollection<string, Hotel>(
    "<Connection String>",  // string connectionString
    "skhotels"              // string collectionName
);
```

---

## Breaking API Changes (May 2025)

These changes were introduced in the May 2025 release and are documented in the [Vector Store changes - May 2025](https://learn.microsoft.com/en-us/semantic-kernel/support/migration/vectorstore-may-2025) migration guide.

| Old API | New API | Notes |
|---------|---------|-------|
| `CreateCollectionIfNotExistsAsync` | `EnsureCollectionExistsAsync` | Method renamed |
| `DeleteAsync` | `EnsureCollectionDeletedAsync` | Method renamed for clarity |
| `IVectorStoreRecordCollection` | `VectorStoreCollection` (abstract class) | Interface replaced with abstract class |
| `VectorStoreOperationException` | `VectorStoreException` | Exception type renamed |

### Example Migration

**Before (Old API):**
```csharp
await collection.CreateCollectionIfNotExistsAsync();
await collection.DeleteAsync();
```

**After (New API):**
```csharp
await collection.EnsureCollectionExistsAsync();
await collection.EnsureCollectionDeletedAsync();
```

---

## Critical Configuration: UseVector()

When creating an `NpgsqlDataSource`, you **must** call `UseVector()` on the builder to enable pgvector support:

```csharp
NpgsqlDataSourceBuilder dataSourceBuilder = new("<Connection String>");
dataSourceBuilder.UseVector();  // CRITICAL - Required for pgvector support
NpgsqlDataSource dataSource = dataSourceBuilder.Build();
```

**Warning:** Failing to call `UseVector()` will result in errors when working with vector columns.

---

## Complete Example

```csharp
#pragma warning disable SKEXP0020

using Microsoft.SemanticKernel.Connectors.Postgres;
using Microsoft.Extensions.VectorData;
using Npgsql;

// Data model
public class Hotel
{
    [VectorStoreKey(StorageName = "hotel_id")]
    public string HotelId { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "hotel_name")]
    public string HotelName { get; set; } = string.Empty;

    [VectorStoreVector(
        Dimensions: 1024,
        DistanceFunction.CosineDistance,
        IndexKind.Hnsw,
        StorageName = "description_embedding")]
    public ReadOnlyMemory<float>? DescriptionEmbedding { get; set; }
}

// Setup
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.UseVector();  // CRITICAL
var dataSource = dataSourceBuilder.Build();

var collection = new PostgresCollection<string, Hotel>(
    dataSource,
    "hotels",
    ownsDataSource: true
);

// Create collection (idempotent)
await collection.EnsureCollectionExistsAsync();

// Use the collection
await collection.UpsertAsync(new Hotel
{
    HotelId = "hotel-001",
    HotelName = "Grand Hotel",
    DescriptionEmbedding = embedding
});
```

---

## Experimental Warning Pragmas

The PostgreSQL connector is still in preview. You may need to suppress experimental warnings:

```csharp
#pragma warning disable SKEXP0020  // PostgreSQL connector
#pragma warning disable SKEXP0070  // Ollama connector (if used together)
```

---

## Sources

- [PostgreSQL Vector Store Connector Documentation](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/postgres-connector)
- [Microsoft.SemanticKernel.Connectors.PgVector on NuGet](https://www.nuget.org/packages/Microsoft.SemanticKernel.Connectors.PgVector)
- [Vector Store changes - May 2025 Migration Guide](https://learn.microsoft.com/en-us/semantic-kernel/support/migration/vectorstore-may-2025)

---

*This research document was created for the csharp-compound-engineering project to track the latest API changes in the Semantic Kernel PostgreSQL connector.*
