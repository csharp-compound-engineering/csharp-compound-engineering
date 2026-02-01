# Phase 144: Integration Test Suite

> **Status**: NOT_STARTED
> **Effort Estimate**: 8-12 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 115 (Aspire Integration Fixture), Phase 119 (Unit Test Patterns)

---

## Spec References

This phase implements the integration testing patterns defined in:

- **spec/testing.md** - [Test Categories - Integration Tests](../spec/testing.md#integration-tests-compounddocsintegrationtests)
- **spec/testing.md** - [MCP Testing Patterns](../spec/testing.md#mcp-testing-patterns)
- **spec/testing/aspire-fixtures.md** - [Database Isolation Strategies](../spec/testing/aspire-fixtures.md#database-isolation-strategies)
- **spec/testing/test-independence.md** - [Core Principles](../spec/testing/test-independence.md)

---

## Objectives

1. Implement comprehensive database integration tests for vector storage and document repository
2. Create Ollama embedding service integration tests with real API calls
3. Implement file watcher integration tests with actual file system operations
4. Build full service integration tests combining multiple components
5. Establish reusable test data setup patterns and fixtures

---

## Acceptance Criteria

### Database Integration Tests

#### VectorStorageIntegrationTests

- [ ] Test class created at `tests/CompoundDocs.IntegrationTests/Database/VectorStorageIntegrationTests.cs`
- [ ] Uses `[Collection("Aspire")]` attribute for fixture sharing
- [ ] All tests have `[Trait("Category", "Integration")]` and `[Trait("Feature", "VectorStorage")]`
- [ ] Each test uses unique collection name via `$"test_{Guid.NewGuid():N}"` pattern

| Test Method | Description |
|-------------|-------------|
| `StoreVector_WithValidEmbedding_PersistsSuccessfully` | Stores 1024-dim vector, verifies via SELECT |
| `StoreVector_WithNullEmbedding_ThrowsArgumentException` | Validates null vector handling |
| `SearchVectors_WithSimilarVector_ReturnsOrderedByDistance` | Stores 3 vectors, searches, verifies cosine distance ordering |
| `SearchVectors_WithTopK_ReturnsRequestedCount` | Verifies topK limit is respected |
| `SearchVectors_WithEmptyCollection_ReturnsEmptyResults` | Handles empty collection gracefully |
| `DeleteCollection_WithExistingData_RemovesAllRecords` | Cleanup verification |
| `StoreVector_WithLargeMetadata_PersistsJsonB` | Tests JSONB metadata storage |

#### DocumentRepositoryIntegrationTests

- [ ] Test class created at `tests/CompoundDocs.IntegrationTests/Database/DocumentRepositoryIntegrationTests.cs`
- [ ] Uses `[Collection("Aspire")]` attribute
- [ ] All tests have `[Trait("Category", "Integration")]` and `[Trait("Feature", "DocumentRepository")]`

| Test Method | Description |
|-------------|-------------|
| `SaveDocument_WithValidDocument_PersistsAllFields` | Full document with path, content, chunks |
| `GetDocumentByPath_WhenExists_ReturnsDocument` | Retrieval by file path |
| `GetDocumentByPath_WhenNotExists_ReturnsNull` | Handles missing document |
| `UpdateDocument_WithExistingDocument_UpdatesContent` | Upsert behavior verification |
| `DeleteDocument_WithExistingDocument_RemovesFromDatabase` | Cascade delete verification |
| `ListDocuments_WithMultipleDocuments_ReturnsPaginated` | Pagination support |
| `SearchByContent_WithMatchingText_ReturnsRelevantDocuments` | Full-text search integration |

### Ollama Embedding Integration Tests

- [ ] Test class created at `tests/CompoundDocs.IntegrationTests/Embedding/OllamaEmbeddingIntegrationTests.cs`
- [ ] Uses `[Collection("Aspire")]` attribute for Ollama endpoint access
- [ ] All tests have `[Trait("Category", "Integration")]` and `[Trait("Feature", "Embedding")]`
- [ ] Uses 2-minute timeout for tests due to potential model download on first run

| Test Method | Description |
|-------------|-------------|
| `GenerateEmbedding_WithValidText_Returns1024DimVector` | Basic embedding generation |
| `GenerateEmbedding_WithEmptyText_ThrowsArgumentException` | Input validation |
| `GenerateEmbedding_WithLongText_ChunksAndEmbeds` | Handles text > context window |
| `GenerateEmbeddings_WithMultipleTexts_ReturnsBatchResults` | Batch embedding support |
| `GenerateEmbedding_WhenCancelled_ThrowsOperationCancelledException` | Cancellation propagation |
| `GenerateEmbedding_ConsistentResults_SameInputSameOutput` | Determinism verification (with tolerance) |

### File Watcher Integration Tests

- [ ] Test class created at `tests/CompoundDocs.IntegrationTests/FileWatcher/FileWatcherIntegrationTests.cs`
- [ ] Uses temporary directory for file operations (via `Path.GetTempPath()`)
- [ ] Uses `[Collection("Aspire")]` attribute
- [ ] All tests have `[Trait("Category", "Integration")]` and `[Trait("Feature", "FileWatcher")]`
- [ ] Implements `IAsyncLifetime` for temp directory setup/cleanup

| Test Method | Description |
|-------------|-------------|
| `StartWatching_WhenFileCreated_TriggersCreatedEvent` | FileSystemWatcher created event |
| `StartWatching_WhenFileModified_TriggersChangedEvent` | FileSystemWatcher changed event |
| `StartWatching_WhenFileDeleted_TriggersDeletedEvent` | FileSystemWatcher deleted event |
| `StartWatching_WhenFileRenamed_TriggersRenamedEvent` | FileSystemWatcher renamed event |
| `StartWatching_WithMarkdownFilter_IgnoresNonMarkdownFiles` | Filter pattern verification |
| `StartWatching_WithNestedDirectory_WatchesSubdirectories` | Recursive watching |
| `StopWatching_WhenDisposed_StopsReceivingEvents` | Cleanup verification |
| `StartWatching_WithRapidChanges_DebouncesProperly` | Debounce behavior (500ms default) |

### Full Service Integration Tests

- [ ] Test class created at `tests/CompoundDocs.IntegrationTests/Services/FullServiceIntegrationTests.cs`
- [ ] Uses `[Collection("Aspire")]` attribute
- [ ] All tests have `[Trait("Category", "Integration")]` and `[Trait("Feature", "FullService")]`
- [ ] Tests span multiple components working together

| Test Method | Description |
|-------------|-------------|
| `IndexDocument_ThroughEntireChain_PersistsWithEmbedding` | File read → Parse → Embed → Store |
| `SearchDocuments_ThroughEntireChain_ReturnsRankedResults` | Query → Embed → Vector search → Format |
| `DocumentUpdate_TriggersReindexing_UpdatesEmbedding` | Modification detection → Reprocess |
| `DocumentDelete_RemovesFromIndex_CleansUpEmbeddings` | Delete cascades through all storage |
| `ConcurrentIndexing_WithMultipleDocuments_HandlesParallelism` | Thread safety verification |
| `ServiceRecovery_AfterDatabaseReconnect_ContinuesOperation` | Resilience testing |

### MCP Tool Integration Tests

- [ ] Test class created at `tests/CompoundDocs.IntegrationTests/Mcp/McpToolIntegrationTests.cs`
- [ ] Uses `[Collection("Aspire")]` attribute for MCP client access
- [ ] All tests have `[Trait("Category", "Integration")]` and `[Trait("Feature", "McpTools")]`

| Test Method | Description |
|-------------|-------------|
| `ListTools_ReturnsAllRegisteredTools` | Tool discovery verification |
| `CallTool_IndexDocument_ExecutesSuccessfully` | Real tool execution |
| `CallTool_RagQuery_ReturnsFormattedResults` | End-to-end query |
| `CallTool_WithInvalidArgs_ReturnsError` | Error handling through MCP |
| `CallTool_WithMissingRequiredArg_ReturnsValidationError` | Argument validation |

### MCP Resource Integration Tests

- [ ] Test class created at `tests/CompoundDocs.IntegrationTests/Mcp/McpResourceIntegrationTests.cs`
- [ ] Uses `[Collection("Aspire")]` attribute
- [ ] All tests have `[Trait("Category", "Integration")]` and `[Trait("Feature", "McpResources")]`

| Test Method | Description |
|-------------|-------------|
| `ListResources_ReturnsAllRegisteredResources` | Resource discovery |
| `ReadResource_Document_ReturnsContent` | Document resource access |
| `ReadResource_Chunk_ReturnsChunkWithMetadata` | Chunk resource access |
| `ReadResource_NotFound_ReturnsError` | Missing resource handling |

### Test Data Setup Patterns

- [ ] Test data builder created at `tests/CompoundDocs.IntegrationTests/TestData/DocumentBuilder.cs`
- [ ] Test data builder created at `tests/CompoundDocs.IntegrationTests/TestData/EmbeddingBuilder.cs`
- [ ] Test data seeder created at `tests/CompoundDocs.IntegrationTests/TestData/TestDataSeeder.cs`
- [ ] Sample markdown files created at `tests/CompoundDocs.IntegrationTests/TestData/SampleDocs/`

---

## Implementation Notes

### VectorStorageIntegrationTests.cs

```csharp
using CompoundDocs.IntegrationTests.Fixtures;
using Npgsql;
using NpgsqlTypes;
using Pgvector;
using Shouldly;
using Xunit;

namespace CompoundDocs.IntegrationTests.Database;

[Collection("Aspire")]
[Trait("Category", "Integration")]
[Trait("Feature", "VectorStorage")]
public class VectorStorageIntegrationTests : IAsyncLifetime
{
    private readonly AspireIntegrationFixture _fixture;
    private readonly string _collection;
    private NpgsqlConnection _connection = null!;

    public VectorStorageIntegrationTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
        _collection = $"test_{Guid.NewGuid():N}";
    }

    public async Task InitializeAsync()
    {
        _connection = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await _connection.OpenAsync();
    }

    public async Task DisposeAsync()
    {
        // Cleanup test collection
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM embeddings WHERE collection = @collection";
        cmd.Parameters.AddWithValue("collection", _collection);
        await cmd.ExecuteNonQueryAsync();

        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task StoreVector_WithValidEmbedding_PersistsSuccessfully()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var embedding = CreateTestEmbedding(1024);
        var vectorStorage = new VectorStorage(_fixture.PostgresConnectionString);

        // Act
        await vectorStorage.StoreAsync(documentId, embedding, _collection);

        // Assert
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT embedding FROM embeddings WHERE document_id = @id AND collection = @collection";
        cmd.Parameters.AddWithValue("id", documentId);
        cmd.Parameters.AddWithValue("collection", _collection);

        var result = await cmd.ExecuteScalarAsync();
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task SearchVectors_WithSimilarVector_ReturnsOrderedByDistance()
    {
        // Arrange
        var vectorStorage = new VectorStorage(_fixture.PostgresConnectionString);
        var queryEmbedding = CreateTestEmbedding(1024, seed: 42);

        // Store vectors with varying similarity
        var doc1 = Guid.NewGuid();
        var doc2 = Guid.NewGuid();
        var doc3 = Guid.NewGuid();

        await vectorStorage.StoreAsync(doc1, CreateTestEmbedding(1024, seed: 42), _collection);  // Very similar
        await vectorStorage.StoreAsync(doc2, CreateTestEmbedding(1024, seed: 100), _collection); // Different
        await vectorStorage.StoreAsync(doc3, CreateTestEmbedding(1024, seed: 43), _collection);  // Somewhat similar

        // Act
        var results = await vectorStorage.SearchAsync(queryEmbedding, topK: 3, _collection);

        // Assert
        results.Count.ShouldBe(3);
        results[0].DocumentId.ShouldBe(doc1); // Most similar first
    }

    private static float[] CreateTestEmbedding(int dimensions, int seed = 0)
    {
        var random = new Random(seed);
        var embedding = new float[dimensions];
        for (int i = 0; i < dimensions; i++)
        {
            embedding[i] = (float)random.NextDouble();
        }
        return embedding;
    }
}
```

### OllamaEmbeddingIntegrationTests.cs

```csharp
using CompoundDocs.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace CompoundDocs.IntegrationTests.Embedding;

[Collection("Aspire")]
[Trait("Category", "Integration")]
[Trait("Feature", "Embedding")]
public class OllamaEmbeddingIntegrationTests
{
    private readonly AspireIntegrationFixture _fixture;

    public OllamaEmbeddingIntegrationTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Timeout = 120000)] // 2-minute timeout for potential model download
    public async Task GenerateEmbedding_WithValidText_Returns1024DimVector()
    {
        // Arrange
        var embeddingService = new OllamaEmbeddingService(_fixture.OllamaEndpoint);
        var text = "This is a sample document for testing embeddings.";

        // Act
        var embedding = await embeddingService.GenerateAsync(text, CancellationToken.None);

        // Assert
        embedding.ShouldNotBeNull();
        embedding.Length.ShouldBe(1024); // nomic-embed-text dimension
    }

    [Fact(Timeout = 120000)]
    public async Task GenerateEmbedding_ConsistentResults_SameInputSameOutput()
    {
        // Arrange
        var embeddingService = new OllamaEmbeddingService(_fixture.OllamaEndpoint);
        var text = "Consistent embedding test";

        // Act
        var embedding1 = await embeddingService.GenerateAsync(text, CancellationToken.None);
        var embedding2 = await embeddingService.GenerateAsync(text, CancellationToken.None);

        // Assert - embeddings should be identical (or very close due to floating point)
        for (int i = 0; i < embedding1.Length; i++)
        {
            embedding1[i].ShouldBe(embedding2[i], tolerance: 0.0001f);
        }
    }

    [Fact(Timeout = 120000)]
    public async Task GenerateEmbedding_WhenCancelled_ThrowsOperationCancelledException()
    {
        // Arrange
        var embeddingService = new OllamaEmbeddingService(_fixture.OllamaEndpoint);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        Func<Task> action = () => embeddingService.GenerateAsync("test", cts.Token);
        await action.ShouldThrowAsync<OperationCanceledException>();
    }
}
```

### FileWatcherIntegrationTests.cs

```csharp
using CompoundDocs.IntegrationTests.Fixtures;
using Shouldly;
using Xunit;

namespace CompoundDocs.IntegrationTests.FileWatcher;

[Collection("Aspire")]
[Trait("Category", "Integration")]
[Trait("Feature", "FileWatcher")]
public class FileWatcherIntegrationTests : IAsyncLifetime
{
    private readonly AspireIntegrationFixture _fixture;
    private string _testDirectory = null!;

    public FileWatcherIntegrationTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"filewatcher_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task StartWatching_WhenFileCreated_TriggersCreatedEvent()
    {
        // Arrange
        var createdFiles = new List<string>();
        var fileWatcher = new FileWatcherService();
        fileWatcher.OnFileCreated += (sender, path) => createdFiles.Add(path);

        // Act
        fileWatcher.StartWatching(_testDirectory, "*.md");
        var testFile = Path.Combine(_testDirectory, "test.md");
        await File.WriteAllTextAsync(testFile, "# Test");

        // Wait for event with timeout
        await WaitForConditionAsync(() => createdFiles.Count > 0, TimeSpan.FromSeconds(5));

        // Assert
        fileWatcher.StopWatching();
        createdFiles.ShouldContain(testFile);
    }

    [Fact]
    public async Task StartWatching_WithMarkdownFilter_IgnoresNonMarkdownFiles()
    {
        // Arrange
        var createdFiles = new List<string>();
        var fileWatcher = new FileWatcherService();
        fileWatcher.OnFileCreated += (sender, path) => createdFiles.Add(path);

        // Act
        fileWatcher.StartWatching(_testDirectory, "*.md");

        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "test.txt"), "Not markdown");
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "test.md"), "# Markdown");

        await WaitForConditionAsync(() => createdFiles.Count > 0, TimeSpan.FromSeconds(5));

        // Assert
        fileWatcher.StopWatching();
        createdFiles.ShouldHaveSingleItem();
        createdFiles[0].ShouldEndWith(".md");
    }

    [Fact]
    public async Task StartWatching_WithRapidChanges_DebouncesProperly()
    {
        // Arrange
        var changedFiles = new List<string>();
        var fileWatcher = new FileWatcherService(debounceMs: 500);
        fileWatcher.OnFileChanged += (sender, path) => changedFiles.Add(path);

        var testFile = Path.Combine(_testDirectory, "debounce.md");
        await File.WriteAllTextAsync(testFile, "Initial");

        // Act
        fileWatcher.StartWatching(_testDirectory, "*.md");

        // Rapid changes
        for (int i = 0; i < 5; i++)
        {
            await File.AppendAllTextAsync(testFile, $"\nChange {i}");
            await Task.Delay(50); // Faster than debounce
        }

        await Task.Delay(1000); // Wait for debounce to settle

        // Assert
        fileWatcher.StopWatching();
        changedFiles.Count.ShouldBeLessThan(5); // Should have debounced
    }

    private static async Task<bool> WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;
            await Task.Delay(100);
        }
        return false;
    }
}
```

### Test Data Builders

#### DocumentBuilder.cs

```csharp
namespace CompoundDocs.IntegrationTests.TestData;

public class DocumentBuilder
{
    private string _path = "/default/doc.md";
    private string _content = "# Default Content";
    private string _collection = "default";
    private Dictionary<string, object> _metadata = new();

    public static DocumentBuilder Create() => new();

    public DocumentBuilder WithPath(string path)
    {
        _path = path;
        return this;
    }

    public DocumentBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    public DocumentBuilder WithCollection(string collection)
    {
        _collection = collection;
        return this;
    }

    public DocumentBuilder WithMetadata(string key, object value)
    {
        _metadata[key] = value;
        return this;
    }

    public Document Build() => new Document
    {
        Id = Guid.NewGuid(),
        Path = _path,
        Content = _content,
        Collection = _collection,
        Metadata = _metadata,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };
}
```

#### TestDataSeeder.cs

```csharp
namespace CompoundDocs.IntegrationTests.TestData;

public class TestDataSeeder
{
    private readonly IDocumentRepository _repository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStorage _vectorStorage;
    private readonly string _collection;

    public TestDataSeeder(
        IDocumentRepository repository,
        IEmbeddingService embeddingService,
        IVectorStorage vectorStorage,
        string collection)
    {
        _repository = repository;
        _embeddingService = embeddingService;
        _vectorStorage = vectorStorage;
        _collection = collection;
    }

    public async Task<Document> SeedDocumentAsync(string content, CancellationToken cancellationToken = default)
    {
        var document = DocumentBuilder.Create()
            .WithContent(content)
            .WithCollection(_collection)
            .WithPath($"/test/{Guid.NewGuid():N}.md")
            .Build();

        await _repository.SaveAsync(document, cancellationToken);

        var embedding = await _embeddingService.GenerateAsync(content, cancellationToken);
        await _vectorStorage.StoreAsync(document.Id, embedding, _collection, cancellationToken);

        return document;
    }

    public async Task<IReadOnlyList<Document>> SeedMultipleDocumentsAsync(
        IEnumerable<string> contents,
        CancellationToken cancellationToken = default)
    {
        var documents = new List<Document>();
        foreach (var content in contents)
        {
            var doc = await SeedDocumentAsync(content, cancellationToken);
            documents.Add(doc);
        }
        return documents;
    }

    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        await _vectorStorage.DeleteCollectionAsync(_collection, cancellationToken);
        await _repository.DeleteByCollectionAsync(_collection, cancellationToken);
    }
}
```

### Sample Test Documents

Create `tests/CompoundDocs.IntegrationTests/TestData/SampleDocs/` with:

- `readme.md` - Standard README content
- `api-docs.md` - API documentation sample
- `tutorial.md` - Tutorial/guide content
- `changelog.md` - Changelog format
- `spec.md` - Specification document

---

## Dependencies

### Depends On

- **Phase 115**: Aspire Integration Fixture (provides `AspireIntegrationFixture`, PostgreSQL connection, Ollama endpoint, MCP client)
- **Phase 119**: Unit Test Patterns (provides testing conventions, naming patterns, Shouldly assertions)
- **Phase 111**: Test Dependencies (provides xUnit, Moq, Shouldly, Npgsql packages)
- **Phase 003**: PostgreSQL pgvector Setup (database schema)
- **Phase 004**: Ollama Service (embedding API)
- **Phase 006**: Vector Storage Implementation (VectorStorage class)
- **Phase 007**: Document Repository (DocumentRepository class)
- **Phase 008**: Embedding Service (EmbeddingService class)
- **Phase 013**: File Watcher Service (FileWatcherService class)

### Blocks

- **Phase 145**: E2E Test Suite (builds on integration test patterns)
- **Phase 146**: CI/CD Pipeline (requires integration tests to be defined)

---

## Verification Steps

After completing this phase, verify:

### Compilation Check

```bash
dotnet build tests/CompoundDocs.IntegrationTests
```

### Run Integration Tests

```bash
# Run all integration tests
dotnet test tests/CompoundDocs.IntegrationTests --filter "Category=Integration"

# Run by feature
dotnet test tests/CompoundDocs.IntegrationTests --filter "Feature=VectorStorage"
dotnet test tests/CompoundDocs.IntegrationTests --filter "Feature=Embedding"
dotnet test tests/CompoundDocs.IntegrationTests --filter "Feature=FileWatcher"
dotnet test tests/CompoundDocs.IntegrationTests --filter "Feature=FullService"
dotnet test tests/CompoundDocs.IntegrationTests --filter "Feature=McpTools"
```

### Infrastructure Verification

1. Verify Docker containers start for PostgreSQL and Ollama
2. Verify MCP server process spawns successfully
3. Verify tests create isolated data with unique collection names
4. Verify test cleanup removes all test data
5. Verify no test data leaks between test classes

### Test Independence Verification

```bash
# Run tests in reverse alphabetical order
dotnet test tests/CompoundDocs.IntegrationTests --filter "Category=Integration" -- xunit.order=reverse

# Run a single test in isolation
dotnet test tests/CompoundDocs.IntegrationTests --filter "FullyQualifiedName~VectorStorageIntegrationTests.StoreVector_WithValidEmbedding_PersistsSuccessfully"
```

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `tests/CompoundDocs.IntegrationTests/Database/VectorStorageIntegrationTests.cs` | Vector storage integration tests |
| `tests/CompoundDocs.IntegrationTests/Database/DocumentRepositoryIntegrationTests.cs` | Document repository integration tests |
| `tests/CompoundDocs.IntegrationTests/Embedding/OllamaEmbeddingIntegrationTests.cs` | Ollama embedding integration tests |
| `tests/CompoundDocs.IntegrationTests/FileWatcher/FileWatcherIntegrationTests.cs` | File watcher integration tests |
| `tests/CompoundDocs.IntegrationTests/Services/FullServiceIntegrationTests.cs` | Full service integration tests |
| `tests/CompoundDocs.IntegrationTests/Mcp/McpToolIntegrationTests.cs` | MCP tool integration tests |
| `tests/CompoundDocs.IntegrationTests/Mcp/McpResourceIntegrationTests.cs` | MCP resource integration tests |
| `tests/CompoundDocs.IntegrationTests/TestData/DocumentBuilder.cs` | Document test data builder |
| `tests/CompoundDocs.IntegrationTests/TestData/EmbeddingBuilder.cs` | Embedding test data builder |
| `tests/CompoundDocs.IntegrationTests/TestData/TestDataSeeder.cs` | Test data seeder helper |
| `tests/CompoundDocs.IntegrationTests/TestData/SampleDocs/*.md` | Sample markdown test files |

---

## Notes

- All integration tests must use the `[Collection("Aspire")]` attribute to share the Aspire fixture
- Each test class should implement `IAsyncLifetime` if it needs per-class setup/cleanup
- Use unique GUIDs for collection names to ensure test isolation: `$"test_{Guid.NewGuid():N}"`
- Ollama tests may be slow on first run due to model downloads; use 2-minute timeout
- File watcher tests use the system temp directory to avoid path conflicts
- Test data builders follow the fluent builder pattern for readability
- All cleanup should be performed in `DisposeAsync()` to handle test failures gracefully
- Database integration tests verify actual SQL behavior, not just mock returns
- The `TestDataSeeder` class provides a convenient way to set up realistic test scenarios
