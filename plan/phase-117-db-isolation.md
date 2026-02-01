# Phase 117: Database Isolation Strategies

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 115 (Aspire Integration Fixture)

---

## Spec References

This phase implements the database isolation strategies defined in:

- **spec/testing/aspire-fixtures.md** - [Database Isolation Strategies](../spec/testing/aspire-fixtures.md#database-isolation-strategies) - Three isolation approaches with code examples
- **spec/testing/test-independence.md** - [Database Isolation Strategies](../spec/testing/test-independence.md#database-isolation-strategies) - Strategy selection criteria and recommendations

---

## Objectives

1. Implement unique collection names per test strategy (recommended default)
2. Implement TRUNCATE between tests strategy for shared state scenarios
3. Implement schema-per-test-class strategy for complete isolation
4. Implement GUID-based data partitioning patterns
5. Create cleanup validation utilities
6. Document strategy selection guidelines

---

## Acceptance Criteria

### Strategy 1: Unique Collection Names (Recommended)

- [ ] `UniqueCollectionTestBase` abstract class created
- [ ] Generates unique collection name per test instance: `test_{Guid.NewGuid():N}`
- [ ] Implements `IAsyncLifetime` for proper setup/teardown
- [ ] `InitializeAsync()` seeds test-specific data to unique collection
- [ ] `DisposeAsync()` cleans up collection via MCP `delete_collection` tool
- [ ] Works with Aspire fixture via `[Collection("Aspire")]` attribute
- [ ] Example test class demonstrates usage pattern
- [ ] Documented as default/recommended strategy

### Strategy 2: TRUNCATE Between Tests

- [ ] `DatabaseCleaner` static utility class created
- [ ] `CleanAsync(NpgsqlConnection connection)` method truncates all test tables:
  - [ ] `TRUNCATE TABLE documents CASCADE;`
  - [ ] `TRUNCATE TABLE embeddings CASCADE;`
  - [ ] `TRUNCATE TABLE chunks CASCADE;`
- [ ] Method is idempotent (safe to call multiple times)
- [ ] Handles connection state appropriately
- [ ] `TruncateCleanupTestBase` abstract class for tests using this strategy
- [ ] Calls `DatabaseCleaner.CleanAsync()` in `InitializeAsync()`
- [ ] Example test class demonstrates usage pattern

### Strategy 3: Schema Per Test Class

- [ ] `SchemaIsolatedFixture` class created implementing `IAsyncLifetime`
- [ ] Generates unique schema name: `test_{Guid.NewGuid():N}`
- [ ] `SchemaName` property exposed for test access
- [ ] `InitializeAsync()` creates schema and sets search path:
  - [ ] `CREATE SCHEMA {schemaName};`
  - [ ] `SET search_path TO {schemaName};`
  - [ ] Runs migrations to create tables in new schema
- [ ] `DisposeAsync()` drops schema with CASCADE:
  - [ ] `DROP SCHEMA {schemaName} CASCADE;`
- [ ] Provides connection string with schema in search path
- [ ] Example test class demonstrates usage pattern

### Strategy 4: GUID-Based Data Partitioning

- [ ] `TestDataGenerator` utility class created
- [ ] Generates unique test identifiers:
  - [ ] `TestId` property returns unique GUID per test instance
  - [ ] `CreateUniqueFilePath()` generates test file paths with GUID
  - [ ] `CreateUniqueDocumentName()` generates document names with prefix
- [ ] All generated data includes test identifier for filtering
- [ ] Cleanup queries can target specific test's data by GUID prefix
- [ ] Example test class demonstrates partitioning pattern

### Cleanup Validation

- [ ] `CleanupValidator` utility class created
- [ ] `ValidateCollectionDeletedAsync(string collection)` verifies collection removal
- [ ] `ValidateSchemaDeletedAsync(string schemaName)` verifies schema removal
- [ ] `ValidateNoTestDataRemainsAsync(string testPrefix)` verifies GUID-partitioned cleanup
- [ ] Validation methods throw descriptive exceptions on failure
- [ ] Validation integrated into `DisposeAsync()` for debug builds only

### Test Organization

- [ ] Tests using same isolation strategy share configuration via base class
- [ ] Test classes explicitly document which strategy they use via comment or trait
- [ ] `[Trait("IsolationStrategy", "UniqueCollection")]` attribute pattern established
- [ ] No cross-strategy state leakage (each strategy is self-contained)

---

## Implementation Notes

### UniqueCollectionTestBase

```csharp
using Aspire.Hosting.Testing;
using Xunit;

namespace CompoundDocs.IntegrationTests.Base;

[Collection("Aspire")]
public abstract class UniqueCollectionTestBase : IAsyncLifetime
{
    protected readonly AspireIntegrationFixture Fixture;
    protected readonly string Collection;

    protected UniqueCollectionTestBase(AspireIntegrationFixture fixture)
    {
        Fixture = fixture;
        Collection = $"test_{Guid.NewGuid():N}";
    }

    public virtual Task InitializeAsync()
    {
        // Override in derived classes to seed test data
        return Task.CompletedTask;
    }

    public virtual async Task DisposeAsync()
    {
        // Cleanup test collection
        try
        {
            await Fixture.McpClient!.CallToolAsync(
                "delete_collection",
                new Dictionary<string, object?> { ["collection"] = Collection });
        }
        catch (Exception ex)
        {
            // Log but don't fail tests on cleanup errors
            Console.WriteLine($"Warning: Failed to cleanup collection {Collection}: {ex.Message}");
        }

#if DEBUG
        // Validate cleanup in debug builds
        await ValidateCollectionDeletedAsync();
#endif
    }

    private async Task ValidateCollectionDeletedAsync()
    {
        // Verify collection no longer exists
        // Implementation depends on available query methods
    }
}
```

### DatabaseCleaner Utility

```csharp
using Npgsql;

namespace CompoundDocs.IntegrationTests.Utilities;

public static class DatabaseCleaner
{
    /// <summary>
    /// Truncates all test-related tables. Use for tests that require
    /// shared schema but clean slate between tests.
    /// </summary>
    public static async Task CleanAsync(NpgsqlConnection connection)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            TRUNCATE TABLE documents CASCADE;
            TRUNCATE TABLE embeddings CASCADE;
            TRUNCATE TABLE chunks CASCADE;";
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Truncates specific tables only.
    /// </summary>
    public static async Task CleanTablesAsync(
        NpgsqlConnection connection,
        params string[] tableNames)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        foreach (var table in tableNames)
        {
            await using var cmd = connection.CreateCommand();
            // Use parameterized table name safely (whitelist approach)
            var safeName = ValidateTableName(table);
            cmd.CommandText = $"TRUNCATE TABLE {safeName} CASCADE;";
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static string ValidateTableName(string tableName)
    {
        // Only allow known table names to prevent SQL injection
        var allowedTables = new HashSet<string>
        {
            "documents", "embeddings", "chunks",
            "external_documents", "external_chunks"
        };

        if (!allowedTables.Contains(tableName.ToLowerInvariant()))
        {
            throw new ArgumentException($"Unknown table name: {tableName}");
        }

        return tableName.ToLowerInvariant();
    }
}
```

### SchemaIsolatedFixture

```csharp
using Npgsql;

namespace CompoundDocs.IntegrationTests.Fixtures;

public class SchemaIsolatedFixture : IAsyncLifetime
{
    private readonly string _baseConnectionString;
    private readonly string _schemaName;

    public string SchemaName => _schemaName;
    public string ConnectionString => $"{_baseConnectionString};SearchPath={_schemaName}";

    public SchemaIsolatedFixture()
    {
        _baseConnectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")
            ?? "Host=localhost;Database=test_compounddocs;Username=postgres;Password=postgres";
        _schemaName = $"test_{Guid.NewGuid():N}";
    }

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(_baseConnectionString);
        await connection.OpenAsync();

        // Create isolated schema
        await using var createCmd = connection.CreateCommand();
        createCmd.CommandText = $@"
            CREATE SCHEMA IF NOT EXISTS {_schemaName};
            SET search_path TO {_schemaName};";
        await createCmd.ExecuteNonQueryAsync();

        // Run migrations in the new schema
        await RunMigrationsAsync(connection);
    }

    private async Task RunMigrationsAsync(NpgsqlConnection connection)
    {
        // Create tables in the test schema
        // This should mirror production schema structure
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            SET search_path TO {_schemaName};

            CREATE EXTENSION IF NOT EXISTS vector;

            CREATE TABLE IF NOT EXISTS documents (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                project_name VARCHAR(255) NOT NULL,
                branch_name VARCHAR(255) NOT NULL,
                path_hash VARCHAR(64) NOT NULL,
                file_path TEXT NOT NULL,
                content_hash VARCHAR(64),
                created_at TIMESTAMPTZ DEFAULT NOW(),
                updated_at TIMESTAMPTZ DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS chunks (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                document_id UUID REFERENCES documents(id) ON DELETE CASCADE,
                chunk_index INTEGER NOT NULL,
                content TEXT NOT NULL,
                embedding vector(768),
                created_at TIMESTAMPTZ DEFAULT NOW()
            );

            CREATE TABLE IF NOT EXISTS embeddings (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                chunk_id UUID REFERENCES chunks(id) ON DELETE CASCADE,
                model_name VARCHAR(255) NOT NULL,
                embedding vector(768) NOT NULL,
                created_at TIMESTAMPTZ DEFAULT NOW()
            );";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(_baseConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"DROP SCHEMA IF EXISTS {_schemaName} CASCADE;";
        await cmd.ExecuteNonQueryAsync();

#if DEBUG
        // Verify schema was dropped
        await using var verifyCmd = connection.CreateCommand();
        verifyCmd.CommandText = @"
            SELECT COUNT(*) FROM information_schema.schemata
            WHERE schema_name = @schemaName";
        verifyCmd.Parameters.AddWithValue("schemaName", _schemaName);
        var count = (long)(await verifyCmd.ExecuteScalarAsync() ?? 0);
        if (count > 0)
        {
            throw new InvalidOperationException(
                $"Schema {_schemaName} was not properly dropped during cleanup");
        }
#endif
    }
}
```

### TestDataGenerator Utility

```csharp
namespace CompoundDocs.IntegrationTests.Utilities;

public class TestDataGenerator
{
    private readonly string _testId;
    private readonly string _testPrefix;

    public string TestId => _testId;
    public string TestPrefix => _testPrefix;

    public TestDataGenerator()
    {
        _testId = Guid.NewGuid().ToString("N");
        _testPrefix = $"test_{_testId[..8]}";
    }

    /// <summary>
    /// Creates a unique temporary file path for this test.
    /// </summary>
    public string CreateUniqueFilePath(string extension = ".md")
    {
        return Path.Combine(
            Path.GetTempPath(),
            $"{_testPrefix}_{Guid.NewGuid():N}{extension}");
    }

    /// <summary>
    /// Creates a unique document name with test prefix.
    /// </summary>
    public string CreateUniqueDocumentName(string baseName = "doc")
    {
        return $"{_testPrefix}_{baseName}_{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Creates a unique collection name for this test.
    /// </summary>
    public string CreateUniqueCollection()
    {
        return $"coll_{_testPrefix}";
    }

    /// <summary>
    /// Creates test content with embedded test identifier for tracing.
    /// </summary>
    public string CreateTestContent(string content)
    {
        return $"<!-- TestId: {_testId} -->\n{content}";
    }
}
```

### CleanupValidator Utility

```csharp
using Npgsql;

namespace CompoundDocs.IntegrationTests.Utilities;

public static class CleanupValidator
{
    /// <summary>
    /// Validates that no documents with the given test prefix remain in the database.
    /// </summary>
    public static async Task ValidateNoTestDataRemainsAsync(
        NpgsqlConnection connection,
        string testPrefix)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM documents
            WHERE file_path LIKE @prefix";
        cmd.Parameters.AddWithValue("prefix", $"%{testPrefix}%");

        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0);
        if (count > 0)
        {
            throw new InvalidOperationException(
                $"Cleanup validation failed: {count} documents with prefix '{testPrefix}' still exist");
        }
    }

    /// <summary>
    /// Validates that a schema has been properly dropped.
    /// </summary>
    public static async Task ValidateSchemaDeletedAsync(
        NpgsqlConnection connection,
        string schemaName)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM information_schema.schemata
            WHERE schema_name = @schemaName";
        cmd.Parameters.AddWithValue("schemaName", schemaName);

        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0);
        if (count > 0)
        {
            throw new InvalidOperationException(
                $"Cleanup validation failed: schema '{schemaName}' still exists");
        }
    }

    /// <summary>
    /// Validates that specified tables are empty.
    /// </summary>
    public static async Task ValidateTablesEmptyAsync(
        NpgsqlConnection connection,
        params string[] tableNames)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        foreach (var table in tableNames)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {table}";
            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0);
            if (count > 0)
            {
                throw new InvalidOperationException(
                    $"Cleanup validation failed: table '{table}' has {count} rows");
            }
        }
    }
}
```

### Strategy Selection Guidelines

| Strategy | Use Case | Isolation Level | Performance |
|----------|----------|-----------------|-------------|
| **Unique Collection Names** | Vector storage tests, most integration tests | Per-test | Fast |
| **TRUNCATE Between Tests** | Tests requiring shared schema, batch operations | Per-test-class | Medium |
| **Schema Per Test Class** | DDL tests, migration tests, complete isolation | Per-test-class | Slow |
| **GUID Partitioning** | Mixed data scenarios, debugging aid | Per-test | Fast |

**Decision Tree**:
1. Does the test modify database schema (DDL)? -> Schema Per Test Class
2. Does the test need to verify behavior across multiple collections? -> TRUNCATE or GUID
3. Is the test a typical CRUD/query test? -> Unique Collection Names (default)
4. Does the test need data visible for debugging after failure? -> GUID Partitioning

---

## Dependencies

### Depends On
- **Phase 115**: Aspire Integration Fixture (provides `AspireIntegrationFixture` class)
- **Phase 110**: xUnit Test Framework Configuration (testing framework setup)
- **Phase 003**: PostgreSQL/pgvector (database infrastructure)
- **Phase 077**: Delete Documents Tool (for collection cleanup via MCP)

### Blocks
- **Phase 118+**: Integration test implementation (tests need isolation strategies)
- **Phase 120+**: E2E test implementation (tests need isolation strategies)
- All integration tests depend on proper isolation patterns

---

## Verification Steps

After completing this phase, verify:

1. **UniqueCollectionTestBase works**:
   ```csharp
   [Collection("Aspire")]
   public class SampleUniqueCollectionTest : UniqueCollectionTestBase
   {
       public SampleUniqueCollectionTest(AspireIntegrationFixture fixture)
           : base(fixture) { }

       [Fact]
       public async Task Collection_Is_Isolated()
       {
           // Verify collection name is unique
           Collection.ShouldStartWith("test_");
           Collection.Length.ShouldBe(37); // "test_" + 32 char GUID
       }
   }
   ```

2. **DatabaseCleaner truncates correctly**:
   ```csharp
   [Fact]
   public async Task Cleaner_Truncates_All_Tables()
   {
       // Insert test data
       await InsertTestDocument();

       // Clean
       await DatabaseCleaner.CleanAsync(connection);

       // Verify empty
       var count = await GetDocumentCount();
       count.ShouldBe(0);
   }
   ```

3. **SchemaIsolatedFixture creates isolated schema**:
   ```csharp
   public class SchemaIsolationTest : IClassFixture<SchemaIsolatedFixture>
   {
       private readonly SchemaIsolatedFixture _fixture;

       [Fact]
       public void Schema_Name_Is_Unique()
       {
           _fixture.SchemaName.ShouldStartWith("test_");
       }

       [Fact]
       public async Task Tables_Exist_In_Schema()
       {
           // Verify tables were created in isolated schema
           await using var conn = new NpgsqlConnection(_fixture.ConnectionString);
           await conn.OpenAsync();
           // Query information_schema for tables
       }
   }
   ```

4. **Cleanup validation detects leaks**:
   ```csharp
   [Fact]
   public async Task Validator_Detects_Remaining_Data()
   {
       // Insert data with known prefix
       await InsertDocumentWithPrefix("test_abc123");

       // Validation should throw
       await Should.ThrowAsync<InvalidOperationException>(
           () => CleanupValidator.ValidateNoTestDataRemainsAsync(
               connection, "test_abc123"));
   }
   ```

5. **Tests run independently**:
   ```bash
   # Run single test in isolation
   dotnet test --filter "FullyQualifiedName~SampleUniqueCollectionTest"

   # Run multiple times to verify no state leakage
   for i in {1..5}; do dotnet test --filter "Category=Integration"; done
   ```

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `tests/CompoundDocs.IntegrationTests/Base/UniqueCollectionTestBase.cs` | Abstract base for unique collection isolation |
| `tests/CompoundDocs.IntegrationTests/Utilities/DatabaseCleaner.cs` | TRUNCATE utility for shared schema cleanup |
| `tests/CompoundDocs.IntegrationTests/Fixtures/SchemaIsolatedFixture.cs` | Schema-per-test-class fixture |
| `tests/CompoundDocs.IntegrationTests/Utilities/TestDataGenerator.cs` | GUID-based data generation utility |
| `tests/CompoundDocs.IntegrationTests/Utilities/CleanupValidator.cs` | Cleanup verification utilities |

### Modified Files

| File | Changes |
|------|---------|
| `tests/CompoundDocs.IntegrationTests/GlobalUsings.cs` | Add using statements for utilities |
| `tests/Directory.Build.props` | Ensure DEBUG constant defined for validation |

---

## Notes

- Unique collection names is the recommended default strategy per spec
- Schema isolation is slowest but provides strongest guarantees for DDL tests
- GUID partitioning aids debugging since data remains visible after test failure
- Cleanup validation runs only in DEBUG builds to avoid production test overhead
- All strategies should be used with `[Collection("Aspire")]` to share infrastructure
- Consider adding telemetry to track which tests leave orphaned data in CI
- The `DatabaseCleaner` whitelist approach prevents SQL injection via table names
- Schema isolation requires running migrations, which adds overhead but ensures schema consistency
