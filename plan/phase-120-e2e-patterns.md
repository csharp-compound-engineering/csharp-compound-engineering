# Phase 120: E2E Test Patterns via MCP Client

> **Status**: NOT_STARTED
> **Effort Estimate**: 8-12 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 115 (Aspire Integration Fixtures), Phase 119 (Integration Test Patterns)

---

## Spec References

This phase implements the E2E testing patterns defined in:

- **spec/testing.md** - [E2E Tests](../spec/testing.md#e2e-tests-compounddocse2etests) (lines 150-171)
- **spec/testing.md** - [MCP Testing Patterns - E2E Testing via MCP Client](../spec/testing.md#e2e-testing-via-mcp-client) (lines 351-397)
- **spec/testing/aspire-fixtures.md** - [E2E Fixture Patterns](../spec/testing/aspire-fixtures.md)
- **research/aspire-testing-mcp-client.md** - [E2E Test Patterns](../research/aspire-testing-mcp-client.md#5-e2e-test-patterns), [MCP Client for Testing](../research/aspire-testing-mcp-client.md#2-mcp-client-for-testing)

---

## Objectives

1. Implement E2E test fixture with full MCP client setup via stdio transport
2. Create reusable test workflow patterns for complete user scenarios
3. Establish tool invocation patterns via `McpClient.CallToolAsync`
4. Implement response validation patterns for MCP tool results
5. Configure real database and Ollama integration (no mocking)
6. Implement test data setup and cleanup patterns
7. Create test helpers for async polling and condition waiting
8. Establish timeout configuration for Ollama embedding latency

---

## Acceptance Criteria

### E2E Fixture Implementation

- [ ] `E2EFixture` class created in `tests/CompoundDocs.E2ETests/Fixtures/E2EFixture.cs`
- [ ] Fixture implements `IAsyncLifetime` for async setup/teardown
- [ ] Fixture starts full Aspire application with all resources:
  - [ ] PostgreSQL with pgvector extension
  - [ ] Ollama with required models
  - [ ] MCP server via stdio transport
- [ ] Fixture exposes `McpClient` property for test access
- [ ] Fixture exposes `PostgresConnectionString` for direct DB verification
- [ ] Fixture handles 5-minute startup timeout for resource initialization
- [ ] Fixture properly disposes all resources in `DisposeAsync`

### MCP Client Configuration

- [ ] `StdioClientTransport` configured with correct options:
  - [ ] `Name`: "CompoundDocs"
  - [ ] `Command`: "dotnet"
  - [ ] `Arguments`: `["run", "--project", <path>, "--no-build"]`
  - [ ] `EnvironmentVariables`: PostgreSQL connection, Ollama endpoint
- [ ] `McpClientOptions` configured:
  - [ ] `ClientInfo`: Test client identification
  - [ ] `InitializationTimeout`: 60 seconds
- [ ] Client initialization verified via `ServerInfo` properties

### Tool Invocation Patterns

- [ ] Helper method for calling MCP tools with typed responses:
  ```csharp
  Task<TResponse> CallToolAsync<TResponse>(string toolName, Dictionary<string, object?> args)
  ```
- [ ] `CallToolAsync` handles:
  - [ ] `TextContentBlock` response extraction
  - [ ] JSON deserialization to typed response
  - [ ] Error content handling
  - [ ] Cancellation token propagation
- [ ] Tool invocation patterns documented for:
  - [ ] `index_document` - Document indexing
  - [ ] `rag_query` - RAG query execution
  - [ ] `semantic_search` - Vector search
  - [ ] `delete_documents` - Data cleanup
  - [ ] `list_doc_types` - Document type enumeration

### Response Validation Patterns

- [ ] Shouldly assertions for MCP responses:
  - [ ] `result.ShouldNotBeNull()`
  - [ ] `result.Content.ShouldNotBeEmpty()`
  - [ ] Response text content validation
- [ ] Pattern for extracting and validating text content:
  ```csharp
  var textContent = result.Content.OfType<TextContentBlock>().First();
  textContent.Text.ShouldContain(expected);
  ```
- [ ] Pattern for validating structured responses via JSON
- [ ] Error response validation patterns

### Full Workflow Tests

- [ ] `DocumentIndexingWorkflowTests` class created:
  - [ ] Test: Create document, index, verify in DB
  - [ ] Test: Index multiple documents, query all
  - [ ] Test: Update document, re-index, verify changes
  - [ ] Test: Delete document, verify removal
- [ ] `RagQueryWorkflowTests` class created:
  - [ ] Test: Index document, RAG query returns relevant context
  - [ ] Test: Query with no matching documents returns empty
  - [ ] Test: Multi-document RAG with source attribution
  - [ ] Test: Query with `top_k` parameter respected
- [ ] `FileWatcherWorkflowTests` class created:
  - [ ] Test: File created in watched directory auto-indexed
  - [ ] Test: File modified triggers re-indexing
  - [ ] Test: File deleted triggers cleanup

### Test Data Setup and Cleanup

- [ ] Test document creation helper:
  ```csharp
  async Task<string> CreateTestDocumentAsync(string content, string? filename = null)
  ```
- [ ] Unique collection name per test via `$"test_{Guid.NewGuid():N}"`
- [ ] Cleanup via `delete_documents` tool in test teardown
- [ ] File cleanup in `finally` blocks or `DisposeAsync`
- [ ] Database state verification helper for direct DB queries

### Real Infrastructure Integration

- [ ] Tests use real PostgreSQL (no in-memory substitutes)
- [ ] Tests use real Ollama for embeddings (no mocked embeddings)
- [ ] Tests verify actual vector similarity search results
- [ ] Tests handle Ollama model download latency (first run)

### Timeout Configuration

- [ ] Individual E2E tests use `[Fact(Timeout = 120000)]` (2 minutes)
- [ ] Fixture initialization has 5-minute timeout
- [ ] MCP client initialization has 60-second timeout
- [ ] Polling helpers respect configurable timeouts

### Test Helpers

- [ ] `WaitForConditionAsync` helper for async polling:
  ```csharp
  Task<bool> WaitForConditionAsync(
      Func<Task<bool>> condition,
      TimeSpan timeout,
      TimeSpan pollInterval)
  ```
- [ ] `RetryAsync` helper for flaky operation handling
- [ ] `GetWatchedDirectoryAsync` for file watcher tests
- [ ] `VerifyDocumentInDatabaseAsync` for DB state verification

---

## Implementation Notes

### E2E Test Fixture

```csharp
using Aspire.Hosting.Testing;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Npgsql;
using Xunit;

namespace CompoundDocs.E2ETests.Fixtures;

public class E2EFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public DistributedApplication App => _app
        ?? throw new InvalidOperationException("App not initialized");

    public string PostgresConnectionString { get; private set; } = string.Empty;
    public string OllamaEndpoint { get; private set; } = string.Empty;
    public McpClient? McpClient { get; private set; }

    public async Task InitializeAsync()
    {
        // Create the test host from AppHost project
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.CompoundDocs_AppHost>();

        // Configure logging for debugging
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Warning);
        });

        // Build and start
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // Wait for resources with 5-minute timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        // Wait for PostgreSQL to be healthy
        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("postgres", cts.Token);
        PostgresConnectionString = await _app
            .GetConnectionStringAsync("postgres", cts.Token)
            ?? throw new InvalidOperationException("PostgreSQL connection string not available");

        // Wait for Ollama
        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("ollama", cts.Token);

        // Get Ollama endpoint
        OllamaEndpoint = await GetOllamaEndpointAsync(cts.Token);

        // Initialize MCP Client
        await InitializeMcpClientAsync(cts.Token);
    }

    private async Task<string> GetOllamaEndpointAsync(CancellationToken cancellationToken)
    {
        var ollamaResource = App.Resources.Single(r => r.Name == "ollama");

        if (ollamaResource is ContainerResource container)
        {
            var endpoint = container.GetEndpoint("http");
            return endpoint?.Url
                ?? throw new InvalidOperationException("Ollama endpoint not available");
        }

        throw new NotSupportedException($"Resource type {ollamaResource.GetType()} not supported");
    }

    private async Task InitializeMcpClientAsync(CancellationToken cancellationToken)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "CompoundDocs",
            Command = "dotnet",
            Arguments = ["run", "--project", GetMcpServerProjectPath(), "--no-build"],
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["POSTGRES_CONNECTION"] = PostgresConnectionString,
                ["OLLAMA_ENDPOINT"] = OllamaEndpoint
            }
        });

        McpClient = await McpClient.CreateAsync(
            transport,
            clientOptions: new McpClientOptions
            {
                ClientInfo = new Implementation { Name = "E2ETests", Version = "1.0.0" },
                InitializationTimeout = TimeSpan.FromSeconds(60)
            },
            cancellationToken: cancellationToken);
    }

    private static string GetMcpServerProjectPath()
    {
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(
            baseDir, "..", "..", "..", "..",
            "src", "CompoundDocs.McpServer", "CompoundDocs.McpServer.csproj"));
    }

    public async Task DisposeAsync()
    {
        if (McpClient != null)
        {
            await McpClient.DisposeAsync();
        }

        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }
}
```

### E2E Collection Definition

```csharp
[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<E2EFixture>
{
    // Marker class for xUnit collection fixture
}
```

### Document Indexing Workflow Test

```csharp
[Collection("E2E")]
[Trait("Category", "E2E")]
public class DocumentIndexingWorkflowTests : IAsyncLifetime
{
    private readonly E2EFixture _fixture;
    private readonly string _testCollection;
    private readonly List<string> _testFiles = new();

    public DocumentIndexingWorkflowTests(E2EFixture fixture)
    {
        _fixture = fixture;
        _testCollection = $"test_{Guid.NewGuid():N}";
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Clean up test files
        foreach (var file in _testFiles)
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        // Clean up test collection
        try
        {
            await _fixture.McpClient!.CallToolAsync(
                "delete_documents",
                new Dictionary<string, object?>
                {
                    ["project_name"] = "e2e-test",
                    ["collection"] = _testCollection
                });
        }
        catch { /* Ignore cleanup errors */ }
    }

    [Fact(Timeout = 120000)]
    public async Task IndexDocument_ThenQuery_ReturnsRelevantResults()
    {
        var mcpClient = _fixture.McpClient!;

        // Step 1: Create test document
        var testFile = await CreateTestDocumentAsync(@"
# System Architecture

## Overview
The system uses a microservices architecture with three main components:
1. API Gateway - handles authentication and routing
2. Document Service - manages document storage and retrieval
3. Vector Database - stores embeddings for semantic search

## Technology Stack
- PostgreSQL with pgvector for vector storage
- Ollama for local LLM inference
- .NET 9 for all services
");

        // Step 2: Index document via MCP tool
        var indexResult = await mcpClient.CallToolAsync(
            "index_document",
            new Dictionary<string, object?>
            {
                ["path"] = testFile,
                ["collection"] = _testCollection
            },
            cancellationToken: CancellationToken.None);

        var indexResponse = indexResult.Content.OfType<TextContentBlock>().First().Text;
        indexResponse.ShouldContain("success", Case.Insensitive);

        // Step 3: Query via RAG tool
        var queryResult = await mcpClient.CallToolAsync(
            "rag_query",
            new Dictionary<string, object?>
            {
                ["query"] = "What are the main components of the system?",
                ["collection"] = _testCollection,
                ["top_k"] = 3
            },
            cancellationToken: CancellationToken.None);

        var queryResponse = queryResult.Content.OfType<TextContentBlock>().First().Text;

        // Step 4: Verify response contains relevant content
        queryResponse.ShouldContain("API Gateway", Case.Insensitive);
        queryResponse.ShouldContain("Document Service", Case.Insensitive);
    }

    private async Task<string> CreateTestDocumentAsync(string content, string? filename = null)
    {
        var testDir = Path.Combine(Path.GetTempPath(), "e2e-test-docs", _testCollection);
        Directory.CreateDirectory(testDir);

        var fileName = filename ?? $"test-{Guid.NewGuid():N}.md";
        var filePath = Path.Combine(testDir, fileName);

        await File.WriteAllTextAsync(filePath, content);
        _testFiles.Add(filePath);

        return filePath;
    }
}
```

### RAG Query Workflow Test

```csharp
[Collection("E2E")]
[Trait("Category", "E2E")]
public class RagQueryWorkflowTests : IAsyncLifetime
{
    private readonly E2EFixture _fixture;
    private readonly string _testCollection;
    private readonly List<string> _testFiles = new();

    public RagQueryWorkflowTests(E2EFixture fixture)
    {
        _fixture = fixture;
        _testCollection = $"rag_{Guid.NewGuid():N}";
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Cleanup test files
        foreach (var file in _testFiles)
        {
            if (File.Exists(file))
                File.Delete(file);
        }

        // Cleanup collection
        try
        {
            await _fixture.McpClient!.CallToolAsync(
                "delete_documents",
                new Dictionary<string, object?>
                {
                    ["project_name"] = "e2e-test",
                    ["collection"] = _testCollection
                });
        }
        catch { /* Ignore */ }
    }

    [Fact(Timeout = 120000)]
    public async Task RagQuery_WithNoMatchingDocuments_ReturnsEmptyContext()
    {
        var mcpClient = _fixture.McpClient!;

        // Query non-existent collection
        var emptyCollection = $"empty_{Guid.NewGuid():N}";
        var result = await mcpClient.CallToolAsync(
            "rag_query",
            new Dictionary<string, object?>
            {
                ["query"] = "What is quantum computing?",
                ["collection"] = emptyCollection
            });

        var response = result.Content.OfType<TextContentBlock>().First().Text;

        // Response should indicate no relevant documents
        response.ShouldContain("no", Case.Insensitive);
    }

    [Fact(Timeout = 120000)]
    public async Task RagQuery_MultiDocument_ReturnsSourceAttribution()
    {
        var mcpClient = _fixture.McpClient!;

        // Index multiple documents
        var doc1 = await CreateTestDocumentAsync(
            "# Authentication\nJWT tokens are used for authentication.",
            "auth.md");
        var doc2 = await CreateTestDocumentAsync(
            "# Authorization\nRBAC is used for authorization policies.",
            "authz.md");

        await mcpClient.CallToolAsync("index_document",
            new Dictionary<string, object?> { ["path"] = doc1, ["collection"] = _testCollection });
        await mcpClient.CallToolAsync("index_document",
            new Dictionary<string, object?> { ["path"] = doc2, ["collection"] = _testCollection });

        // Query across both documents
        var result = await mcpClient.CallToolAsync(
            "rag_query",
            new Dictionary<string, object?>
            {
                ["query"] = "How does the system handle authentication and authorization?",
                ["collection"] = _testCollection,
                ["top_k"] = 5
            });

        var response = result.Content.OfType<TextContentBlock>().First().Text;

        // Should mention both topics
        response.ShouldContain("JWT", Case.Insensitive);
        response.ShouldContain("RBAC", Case.Insensitive);
    }

    private async Task<string> CreateTestDocumentAsync(string content, string filename)
    {
        var testDir = Path.Combine(Path.GetTempPath(), "rag-test-docs", _testCollection);
        Directory.CreateDirectory(testDir);

        var filePath = Path.Combine(testDir, filename);
        await File.WriteAllTextAsync(filePath, content);
        _testFiles.Add(filePath);

        return filePath;
    }
}
```

### File Watcher Workflow Test

```csharp
[Collection("E2E")]
[Trait("Category", "E2E")]
public class FileWatcherWorkflowTests : IAsyncLifetime
{
    private readonly E2EFixture _fixture;
    private string _watchedDirectory = string.Empty;

    public FileWatcherWorkflowTests(E2EFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _watchedDirectory = await GetWatchedDirectoryAsync();
        Directory.CreateDirectory(_watchedDirectory);
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_watchedDirectory))
        {
            Directory.Delete(_watchedDirectory, recursive: true);
        }
        return Task.CompletedTask;
    }

    [Fact(Timeout = 120000)]
    public async Task FileCreated_InWatchedDirectory_AutomaticallyIndexed()
    {
        var mcpClient = _fixture.McpClient!;
        var testFile = Path.Combine(_watchedDirectory, $"auto-index-{Guid.NewGuid()}.md");

        // Create file in watched directory
        await File.WriteAllTextAsync(testFile,
            "# Auto-Indexed Document\n\nThis document should be indexed automatically by the file watcher.");

        // Wait for auto-indexing with polling
        var indexed = await E2ETestHelpers.WaitForConditionAsync(
            async () =>
            {
                var result = await mcpClient.CallToolAsync(
                    "semantic_search",
                    new Dictionary<string, object?>
                    {
                        ["query"] = "auto-indexed automatically",
                        ["top_k"] = 5
                    });
                var response = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text ?? "";
                return response.Contains(Path.GetFileName(testFile), StringComparison.OrdinalIgnoreCase);
            },
            timeout: TimeSpan.FromSeconds(30),
            pollInterval: TimeSpan.FromSeconds(2));

        indexed.ShouldBeTrue("Document should be auto-indexed within timeout");
    }

    private Task<string> GetWatchedDirectoryAsync()
    {
        // This would be obtained from MCP server configuration or resource
        var baseDir = Path.Combine(Path.GetTempPath(), "e2e-watched", Guid.NewGuid().ToString("N"));
        return Task.FromResult(baseDir);
    }
}
```

### E2E Test Helpers

```csharp
namespace CompoundDocs.E2ETests;

public static class E2ETestHelpers
{
    public static async Task<bool> WaitForConditionAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (await condition())
                    return true;
            }
            catch
            {
                // Continue polling on exceptions
            }

            await Task.Delay(pollInterval);
        }
        return false;
    }

    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> operation,
        Func<T, bool> successCondition,
        int maxAttempts = 5,
        TimeSpan? delayBetweenAttempts = null)
    {
        var delay = delayBetweenAttempts ?? TimeSpan.FromSeconds(1);

        for (int i = 0; i < maxAttempts; i++)
        {
            var result = await operation();
            if (successCondition(result))
                return result;

            if (i < maxAttempts - 1)
                await Task.Delay(delay);
        }

        throw new TimeoutException($"Operation did not succeed after {maxAttempts} attempts");
    }

    public static async Task VerifyDocumentInDatabaseAsync(
        string connectionString,
        string documentPath,
        string collection)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM documents
            WHERE path = @path AND collection = @collection";
        cmd.Parameters.AddWithValue("path", documentPath);
        cmd.Parameters.AddWithValue("collection", collection);

        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0);
        count.ShouldBeGreaterThan(0, $"Document {documentPath} should exist in database");
    }
}
```

### xunit.runner.json (E2E Project)

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeAssembly": false,
  "parallelizeTestCollections": false,
  "maxParallelThreads": 1,
  "diagnosticMessages": true,
  "longRunningTestSeconds": 60,
  "methodDisplay": "classAndMethod",
  "methodDisplayOptions": "replacePeriodWithComma"
}
```

---

## Test Data Management

### Test Document Creation Pattern

All E2E tests should create documents with unique identifiers to prevent interference:

```csharp
private string _testCollection = $"test_{Guid.NewGuid():N}";

private async Task<string> CreateTestDocumentAsync(string content)
{
    var testDir = Path.Combine(Path.GetTempPath(), "e2e-docs", _testCollection);
    Directory.CreateDirectory(testDir);

    var filePath = Path.Combine(testDir, $"{Guid.NewGuid():N}.md");
    await File.WriteAllTextAsync(filePath, content);

    return filePath;
}
```

### Cleanup Pattern

Cleanup should happen in `DisposeAsync` and/or `finally` blocks:

1. **File cleanup**: Delete test documents from filesystem
2. **Database cleanup**: Call `delete_documents` MCP tool
3. **Collection cleanup**: Use unique collection names to ensure isolation

---

## Timeout Guidelines

| Operation | Timeout | Rationale |
|-----------|---------|-----------|
| Individual E2E test | 2 minutes | Accounts for Ollama embedding generation |
| Fixture initialization | 5 minutes | Container startup + model downloads |
| MCP client initialization | 60 seconds | Server startup via stdio |
| Polling for async operations | 30 seconds | File watcher detection |
| CI/CD E2E step | 15 minutes | Total E2E test suite |
| CI/CD E2E job | 30 minutes | Includes startup/teardown |

---

## Dependencies

### Depends On

- **Phase 115**: Aspire Integration Fixtures (base fixture infrastructure)
- **Phase 119**: Integration Test Patterns (shared patterns for Aspire tests)
- **Phase 109**: Test Project Structure (E2E test project must exist)
- **Phase 110**: xUnit Configuration (xunit.runner.json patterns)
- **Phase 021**: MCP Server Project (server to test against)

### Blocks

- **Phase 121+**: Specific E2E test implementations
- **Phase 122**: CI/CD Pipeline Integration (E2E test execution)

---

## Verification Steps

After completing this phase, verify:

1. **E2E fixture initializes successfully**:
   ```bash
   dotnet test tests/CompoundDocs.E2ETests/ --filter "FullyQualifiedName~E2EFixture"
   ```

2. **MCP client connects to server**:
   - Verify `McpClient.ServerInfo` is populated
   - Verify `ListToolsAsync()` returns expected tools

3. **Document indexing workflow completes**:
   ```bash
   dotnet test tests/CompoundDocs.E2ETests/ --filter "Category=E2E"
   ```

4. **RAG query returns results**:
   - Index a known document
   - Query with related terms
   - Verify response contains expected content

5. **Cleanup removes test data**:
   - Run tests twice
   - Verify no test data accumulation

6. **Timeouts are respected**:
   - Tests fail fast on timeout, not hang
   - 2-minute per-test timeout enforced

---

## Notes

- E2E tests are inherently slower due to real infrastructure; run them separately from unit tests
- First test run may be slower due to Ollama model downloads (persisted via data volume)
- Use `[Fact(Skip = "reason")]` to temporarily disable flaky tests during development
- Consider adding retry logic for tests affected by container startup timing
- The stdio transport for MCP client means the server runs as a child process, consuming additional resources
- For CI/CD, pre-pull Docker images and pre-download Ollama models to reduce test execution time
- E2E tests validate user-facing behavior; if unit and integration tests pass but E2E fails, investigate the integration points

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `tests/CompoundDocs.E2ETests/Fixtures/E2EFixture.cs` | Main E2E test fixture |
| `tests/CompoundDocs.E2ETests/Fixtures/E2ECollection.cs` | xUnit collection definition |
| `tests/CompoundDocs.E2ETests/E2ETestHelpers.cs` | Shared test utilities |
| `tests/CompoundDocs.E2ETests/Workflows/DocumentIndexingWorkflowTests.cs` | Document indexing E2E tests |
| `tests/CompoundDocs.E2ETests/Workflows/RagQueryWorkflowTests.cs` | RAG query E2E tests |
| `tests/CompoundDocs.E2ETests/Workflows/FileWatcherWorkflowTests.cs` | File watcher E2E tests |

### Modified Files

| File | Changes |
|------|---------|
| `tests/CompoundDocs.E2ETests/CompoundDocs.E2ETests.csproj` | Add MCP client, Aspire.Hosting.Testing packages |
| `tests/CompoundDocs.E2ETests/xunit.runner.json` | Configure serial execution, timeouts |
