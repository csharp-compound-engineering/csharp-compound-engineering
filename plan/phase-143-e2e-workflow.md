# Phase 143: End-to-End Workflow Tests

> **Status**: NOT_STARTED
> **Effort Estimate**: 8-10 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 120 (MCP Client Test Patterns)

---

## Spec References

This phase implements the E2E workflow tests defined in:

- **spec/testing.md** - [E2E Tests (CompoundDocs.E2ETests)](../spec/testing.md#test-categories) - Test characteristics, timeout configuration (2 minutes per test), workflow trait patterns
- **spec/testing.md** - [E2E Testing via MCP Client](../spec/testing.md#e2e-testing-via-mcp-client) - Complete MCP tool invocation patterns
- **SPEC.md** - [Complete Workflow Example](../SPEC.md#complete-workflow-example) - Day 1 (capture) and Day 30 (retrieval) user scenarios
- **spec/testing/aspire-fixtures.md** - [Database Isolation Strategies](../spec/testing/aspire-fixtures.md#database-isolation-strategies) - Unique collection names per test

---

## Objectives

1. Implement Day 1 workflow test (knowledge capture scenario)
2. Implement Day 30 workflow test (knowledge retrieval scenario)
3. Create full skill invocation tests via MCP client
4. Test complete MCP tool chains (activate -> index -> query)
5. Validate project activation to query flow end-to-end
6. Implement file watcher integration tests
7. Test multi-document linking and retrieval
8. Verify promotion level impact on search results

---

## Acceptance Criteria

### Test Project Structure

- [ ] `tests/CompoundDocs.E2ETests/Workflows/` directory exists
- [ ] `DocumentIndexingWorkflowTests.cs` created
- [ ] `RagQueryWorkflowTests.cs` created
- [ ] `FileWatcherWorkflowTests.cs` created
- [ ] `FullLifecycleWorkflowTests.cs` created
- [ ] All test classes use `[Collection("Aspire")]` attribute
- [ ] All test methods use `[Trait("Category", "E2E")]` attribute
- [ ] All test methods use `[Fact(Timeout = 120000)]` for 2-minute timeout

### Day 1 Workflow Test (Knowledge Capture)

- [ ] Test creates a markdown file with proper frontmatter
- [ ] Test invokes file watcher or explicit index tool
- [ ] Test verifies document is indexed in vector store
- [ ] Test verifies embedding is generated correctly
- [ ] Test confirms document is retrievable via semantic search
- [ ] Test uses unique collection name per test run (GUID-based)
- [ ] Test cleans up collection data in disposal

### Day 30 Workflow Test (Knowledge Retrieval)

- [ ] Test pre-seeds documents with known content
- [ ] Test invokes `rag_query` tool with relevant query
- [ ] Test verifies RAG response contains expected information
- [ ] Test validates source documents are returned with response
- [ ] Test confirms similarity score meets threshold (0.7)
- [ ] Test validates linked document resolution

### MCP Tool Chain Tests

- [ ] `activate_project` -> `index_document` -> `rag_query` chain tested
- [ ] `activate_project` -> `index_document` -> `semantic_search` chain tested
- [ ] `activate_project` -> `list_doc_types` -> `index_document` chain tested
- [ ] `index_document` -> `update_promotion` -> `rag_query` chain tested
- [ ] `index_document` -> `delete_documents` -> `rag_query` (empty) chain tested

### Project Activation Flow Tests

- [ ] Test `activate_project` tool with valid project path
- [ ] Test verifies tenant context is established (project + branch)
- [ ] Test validates configuration is loaded from project
- [ ] Test confirms subsequent tools use correct tenant isolation
- [ ] Test verifies multiple activations switch context correctly

### Full Skill Invocation Tests

- [ ] Test `/cdocs:query` skill invocation via MCP
- [ ] Test `/cdocs:search` skill invocation via MCP
- [ ] Test `/cdocs:activate` auto-invoke behavior
- [ ] Test multi-document query returns synthesized answer
- [ ] Test skill response format matches specification

### File Watcher Integration Tests

- [ ] Test file creation triggers automatic indexing
- [ ] Test file modification triggers re-indexing
- [ ] Test file deletion triggers document removal
- [ ] Test debouncing prevents rapid successive indexing (500ms)
- [ ] Test startup reconciliation indexes existing documents

### Multi-Document and Linking Tests

- [ ] Test documents with markdown links are properly indexed
- [ ] Test linked documents are resolved during query
- [ ] Test circular reference handling (no infinite loops)
- [ ] Test link depth configuration is respected
- [ ] Test cross-document context is included in RAG response

### Promotion Level Tests

- [ ] Test `standard` promotion level (default boost 1.0x)
- [ ] Test `starred` promotion level (boost 1.5x)
- [ ] Test `pinned` promotion level (boost 2.0x)
- [ ] Test promotion boost affects ranking order
- [ ] Test `update_promotion` tool changes document ranking

---

## Implementation Notes

### DocumentIndexingWorkflowTests.cs

```csharp
using CompoundDocs.IntegrationTests.Fixtures;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace CompoundDocs.E2ETests.Workflows;

[Collection("Aspire")]
[Trait("Category", "E2E")]
public class DocumentIndexingWorkflowTests : IAsyncLifetime
{
    private readonly AspireIntegrationFixture _fixture;
    private readonly string _testCollection;
    private readonly string _testProjectPath;

    public DocumentIndexingWorkflowTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
        _testCollection = $"test_{Guid.NewGuid():N}";
        _testProjectPath = Path.Combine(Path.GetTempPath(), $"cdocs_test_{Guid.NewGuid():N}");
    }

    public async Task InitializeAsync()
    {
        // Create test project directory structure
        Directory.CreateDirectory(_testProjectPath);
        Directory.CreateDirectory(Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems"));

        // Activate project
        await _fixture.McpClient!.CallToolAsync(
            "activate_project",
            new Dictionary<string, object?>
            {
                ["project_path"] = _testProjectPath,
                ["branch"] = "test-branch"
            });
    }

    [Fact(Timeout = 120000)]
    [Trait("Workflow", "DocumentIndexing")]
    public async Task Day1Workflow_CaptureKnowledge_IndexesAndRetrievesDocument()
    {
        // Arrange - Create document mimicking Day 1 capture
        var documentContent = """
            ---
            title: Database Connection Pool Exhaustion
            date: 2025-01-23
            summary: Background jobs not disposing SqlConnection caused pool exhaustion
            doc_type: problem
            problem_type: bug
            severity: high
            symptoms:
              - connection pool exhaustion
              - background job failures
            root_cause: SqlConnection not disposed in background jobs
            solution: Added using statements to ensure disposal
            promotion_level: standard
            ---

            # Database Connection Pool Exhaustion

            ## Symptoms
            - Connection pool exhaustion errors in logs
            - Background jobs failing intermittently

            ## Root Cause
            The `ProcessDataJob` class was creating `SqlConnection` objects without
            `using` statements, causing connections to leak.

            ## Solution
            Added `using` statements to all database operations in background jobs.
            """;

        var docPath = Path.Combine(
            _testProjectPath,
            "csharp-compounding-docs",
            "problems",
            "db-pool-exhaustion-20250123.md");
        await File.WriteAllTextAsync(docPath, documentContent);

        // Act - Index document via MCP tool
        var indexResult = await _fixture.McpClient!.CallToolAsync(
            "index_document",
            new Dictionary<string, object?>
            {
                ["path"] = docPath,
                ["collection"] = _testCollection
            });

        // Assert - Document indexed successfully
        indexResult.IsError.ShouldBeFalse("Index operation should succeed");

        // Verify - Query returns the document
        var queryResult = await _fixture.McpClient!.CallToolAsync(
            "rag_query",
            new Dictionary<string, object?>
            {
                ["query"] = "connection pool exhaustion background jobs",
                ["collection"] = _testCollection
            });

        queryResult.IsError.ShouldBeFalse("Query should succeed");
        var response = queryResult.Content.OfType<TextContentBlock>().First().Text;
        response.ShouldContain("SqlConnection", Case.Insensitive);
        response.ShouldContain("using", Case.Insensitive);
    }

    [Fact(Timeout = 120000)]
    [Trait("Workflow", "DocumentIndexing")]
    public async Task IndexDocument_WithValidMarkdown_GeneratesEmbeddingAndStores()
    {
        // Arrange
        var docContent = """
            ---
            title: Test Document
            date: 2025-01-24
            doc_type: insight
            summary: Test insight for indexing
            ---

            # Test Document
            This is a test document for verifying indexing workflow.
            """;

        var docPath = Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems", "test-doc.md");
        await File.WriteAllTextAsync(docPath, docContent);

        // Act
        var result = await _fixture.McpClient!.CallToolAsync(
            "index_document",
            new Dictionary<string, object?>
            {
                ["path"] = docPath,
                ["collection"] = _testCollection
            });

        // Assert
        result.IsError.ShouldBeFalse();

        // Verify via semantic search
        var searchResult = await _fixture.McpClient!.CallToolAsync(
            "semantic_search",
            new Dictionary<string, object?>
            {
                ["query"] = "test document verifying",
                ["collection"] = _testCollection,
                ["limit"] = 5
            });

        searchResult.IsError.ShouldBeFalse();
        var searchResponse = searchResult.Content.OfType<TextContentBlock>().First().Text;
        searchResponse.ShouldContain("test-doc.md");
    }

    public async Task DisposeAsync()
    {
        // Cleanup test collection
        try
        {
            await _fixture.McpClient!.CallToolAsync(
                "delete_documents",
                new Dictionary<string, object?>
                {
                    ["collection"] = _testCollection
                });
        }
        catch
        {
            // Ignore cleanup failures
        }

        // Cleanup test directory
        if (Directory.Exists(_testProjectPath))
        {
            Directory.Delete(_testProjectPath, recursive: true);
        }
    }
}
```

### RagQueryWorkflowTests.cs

```csharp
using CompoundDocs.IntegrationTests.Fixtures;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace CompoundDocs.E2ETests.Workflows;

[Collection("Aspire")]
[Trait("Category", "E2E")]
public class RagQueryWorkflowTests : IAsyncLifetime
{
    private readonly AspireIntegrationFixture _fixture;
    private readonly string _testCollection;
    private readonly string _testProjectPath;

    public RagQueryWorkflowTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
        _testCollection = $"test_{Guid.NewGuid():N}";
        _testProjectPath = Path.Combine(Path.GetTempPath(), $"cdocs_test_{Guid.NewGuid():N}");
    }

    public async Task InitializeAsync()
    {
        // Create test project and seed documents
        Directory.CreateDirectory(_testProjectPath);
        Directory.CreateDirectory(Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems"));

        // Activate project
        await _fixture.McpClient!.CallToolAsync(
            "activate_project",
            new Dictionary<string, object?>
            {
                ["project_path"] = _testProjectPath,
                ["branch"] = "test-branch"
            });

        // Seed known documents for Day 30 scenario
        await SeedTestDocumentsAsync();
    }

    private async Task SeedTestDocumentsAsync()
    {
        // Document 1: Pool exhaustion issue
        var doc1 = """
            ---
            title: Database Connection Pool Exhaustion
            date: 2025-01-23
            doc_type: problem
            problem_type: bug
            severity: high
            solution: Added using statements to ensure SqlConnection disposal
            ---

            # Database Connection Pool Exhaustion
            Background jobs were not disposing SqlConnection objects.
            """;

        // Document 2: Related memory issue
        var doc2 = """
            ---
            title: Memory Leak in Job Scheduler
            date: 2025-01-20
            doc_type: problem
            problem_type: bug
            severity: medium
            solution: Implemented IDisposable pattern correctly
            ---

            # Memory Leak in Job Scheduler
            Job scheduler was holding references to completed jobs.
            """;

        var docPath1 = Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems", "pool-exhaustion.md");
        var docPath2 = Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems", "memory-leak.md");

        await File.WriteAllTextAsync(docPath1, doc1);
        await File.WriteAllTextAsync(docPath2, doc2);

        // Index both documents
        await _fixture.McpClient!.CallToolAsync(
            "index_document",
            new Dictionary<string, object?> { ["path"] = docPath1, ["collection"] = _testCollection });
        await _fixture.McpClient!.CallToolAsync(
            "index_document",
            new Dictionary<string, object?> { ["path"] = docPath2, ["collection"] = _testCollection });
    }

    [Fact(Timeout = 120000)]
    [Trait("Workflow", "RagQuery")]
    public async Task Day30Workflow_QueryForConnectionPoolIssue_ReturnsRelevantDocumentation()
    {
        // Arrange - Query mimicking Day 30 developer encountering similar issue
        var query = "We're seeing connection pool errors in our job scheduler";

        // Act
        var result = await _fixture.McpClient!.CallToolAsync(
            "rag_query",
            new Dictionary<string, object?>
            {
                ["query"] = query,
                ["collection"] = _testCollection
            });

        // Assert
        result.IsError.ShouldBeFalse("RAG query should succeed");

        var response = result.Content.OfType<TextContentBlock>().First().Text;

        // Should mention the solution from Day 1 capture
        response.ShouldContain("using", Case.Insensitive,
            "Response should mention the using statements solution");
        response.ShouldContain("SqlConnection", Case.Insensitive,
            "Response should reference SqlConnection disposal");
    }

    [Fact(Timeout = 120000)]
    [Trait("Workflow", "RagQuery")]
    public async Task RagQuery_WithSourcesRequested_ReturnsSourceDocuments()
    {
        // Act
        var result = await _fixture.McpClient!.CallToolAsync(
            "rag_query",
            new Dictionary<string, object?>
            {
                ["query"] = "database connection issues",
                ["collection"] = _testCollection,
                ["include_sources"] = true
            });

        // Assert
        result.IsError.ShouldBeFalse();
        var response = result.Content.OfType<TextContentBlock>().First().Text;

        // Should include source references
        response.ShouldContain("Sources:", Case.Insensitive);
        response.ShouldContain("pool-exhaustion.md");
    }

    [Fact(Timeout = 120000)]
    [Trait("Workflow", "RagQuery")]
    public async Task RagQuery_WithNoRelevantDocuments_ReturnsEmptyResult()
    {
        // Act - Query for something not in our test documents
        var result = await _fixture.McpClient!.CallToolAsync(
            "rag_query",
            new Dictionary<string, object?>
            {
                ["query"] = "kubernetes deployment configuration yaml manifests",
                ["collection"] = _testCollection
            });

        // Assert
        result.IsError.ShouldBeFalse();
        var response = result.Content.OfType<TextContentBlock>().First().Text;

        // Should indicate no relevant documents found
        response.ShouldContain("no relevant", Case.Insensitive);
    }

    public async Task DisposeAsync()
    {
        try
        {
            await _fixture.McpClient!.CallToolAsync(
                "delete_documents",
                new Dictionary<string, object?> { ["collection"] = _testCollection });
        }
        catch { }

        if (Directory.Exists(_testProjectPath))
        {
            Directory.Delete(_testProjectPath, recursive: true);
        }
    }
}
```

### FullLifecycleWorkflowTests.cs

```csharp
using CompoundDocs.IntegrationTests.Fixtures;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace CompoundDocs.E2ETests.Workflows;

[Collection("Aspire")]
[Trait("Category", "E2E")]
public class FullLifecycleWorkflowTests : IAsyncLifetime
{
    private readonly AspireIntegrationFixture _fixture;
    private readonly string _testCollection;
    private readonly string _testProjectPath;

    public FullLifecycleWorkflowTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
        _testCollection = $"test_{Guid.NewGuid():N}";
        _testProjectPath = Path.Combine(Path.GetTempPath(), $"cdocs_test_{Guid.NewGuid():N}");
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_testProjectPath);
        Directory.CreateDirectory(Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems"));
    }

    [Fact(Timeout = 120000)]
    [Trait("Workflow", "FullLifecycle")]
    public async Task CompleteToolChain_ActivateIndexQuery_ReturnsAccurateResponse()
    {
        // Step 1: Activate project
        var activateResult = await _fixture.McpClient!.CallToolAsync(
            "activate_project",
            new Dictionary<string, object?>
            {
                ["project_path"] = _testProjectPath,
                ["branch"] = "main"
            });
        activateResult.IsError.ShouldBeFalse("Project activation should succeed");

        // Step 2: Create and index document
        var docContent = """
            ---
            title: API Rate Limiting Implementation
            date: 2025-01-24
            doc_type: problem
            solution: Implemented sliding window rate limiter with Redis backend
            ---

            # API Rate Limiting Implementation
            Used a sliding window algorithm with Redis for distributed rate limiting.
            """;

        var docPath = Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems", "rate-limiting.md");
        await File.WriteAllTextAsync(docPath, docContent);

        var indexResult = await _fixture.McpClient!.CallToolAsync(
            "index_document",
            new Dictionary<string, object?>
            {
                ["path"] = docPath,
                ["collection"] = _testCollection
            });
        indexResult.IsError.ShouldBeFalse("Document indexing should succeed");

        // Step 3: Query the indexed document
        var queryResult = await _fixture.McpClient!.CallToolAsync(
            "rag_query",
            new Dictionary<string, object?>
            {
                ["query"] = "how to implement rate limiting",
                ["collection"] = _testCollection
            });

        queryResult.IsError.ShouldBeFalse("RAG query should succeed");
        var response = queryResult.Content.OfType<TextContentBlock>().First().Text;
        response.ShouldContain("sliding window", Case.Insensitive);
        response.ShouldContain("Redis", Case.Insensitive);
    }

    [Fact(Timeout = 120000)]
    [Trait("Workflow", "FullLifecycle")]
    public async Task ToolChain_IndexUpdatePromotionQuery_PromotedDocumentRanksHigher()
    {
        // Activate project
        await _fixture.McpClient!.CallToolAsync(
            "activate_project",
            new Dictionary<string, object?>
            {
                ["project_path"] = _testProjectPath,
                ["branch"] = "main"
            });

        // Create two similar documents
        var doc1 = """
            ---
            title: Caching Strategy Overview
            date: 2025-01-20
            doc_type: insight
            promotion_level: standard
            ---
            # Caching Strategy Overview
            Basic caching patterns for web applications.
            """;

        var doc2 = """
            ---
            title: Advanced Caching with Redis
            date: 2025-01-24
            doc_type: insight
            promotion_level: standard
            ---
            # Advanced Caching with Redis
            Detailed caching implementation using Redis distributed cache.
            """;

        var docPath1 = Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems", "caching-basic.md");
        var docPath2 = Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems", "caching-redis.md");

        await File.WriteAllTextAsync(docPath1, doc1);
        await File.WriteAllTextAsync(docPath2, doc2);

        // Index both documents
        await _fixture.McpClient!.CallToolAsync(
            "index_document",
            new Dictionary<string, object?> { ["path"] = docPath1, ["collection"] = _testCollection });
        await _fixture.McpClient!.CallToolAsync(
            "index_document",
            new Dictionary<string, object?> { ["path"] = docPath2, ["collection"] = _testCollection });

        // Promote doc1 to starred
        var promoteResult = await _fixture.McpClient!.CallToolAsync(
            "update_promotion",
            new Dictionary<string, object?>
            {
                ["path"] = docPath1,
                ["collection"] = _testCollection,
                ["promotion_level"] = "starred"
            });
        promoteResult.IsError.ShouldBeFalse("Promotion should succeed");

        // Query for caching - promoted doc should rank first
        var searchResult = await _fixture.McpClient!.CallToolAsync(
            "semantic_search",
            new Dictionary<string, object?>
            {
                ["query"] = "caching strategy",
                ["collection"] = _testCollection,
                ["limit"] = 2
            });

        searchResult.IsError.ShouldBeFalse();
        var response = searchResult.Content.OfType<TextContentBlock>().First().Text;

        // First result should be the promoted document
        var basicIndex = response.IndexOf("caching-basic.md", StringComparison.OrdinalIgnoreCase);
        var redisIndex = response.IndexOf("caching-redis.md", StringComparison.OrdinalIgnoreCase);
        basicIndex.ShouldBeLessThan(redisIndex, "Promoted document should appear first");
    }

    [Fact(Timeout = 120000)]
    [Trait("Workflow", "FullLifecycle")]
    public async Task ToolChain_IndexDeleteQuery_DeletedDocumentNotReturned()
    {
        // Activate project
        await _fixture.McpClient!.CallToolAsync(
            "activate_project",
            new Dictionary<string, object?>
            {
                ["project_path"] = _testProjectPath,
                ["branch"] = "main"
            });

        // Create and index document
        var docContent = """
            ---
            title: Temporary Test Document
            date: 2025-01-24
            doc_type: problem
            ---
            # Temporary Test Document
            This document will be deleted.
            """;

        var docPath = Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems", "temp-doc.md");
        await File.WriteAllTextAsync(docPath, docContent);

        await _fixture.McpClient!.CallToolAsync(
            "index_document",
            new Dictionary<string, object?> { ["path"] = docPath, ["collection"] = _testCollection });

        // Verify document is searchable
        var searchBefore = await _fixture.McpClient!.CallToolAsync(
            "semantic_search",
            new Dictionary<string, object?>
            {
                ["query"] = "temporary test document deleted",
                ["collection"] = _testCollection
            });
        searchBefore.Content.OfType<TextContentBlock>().First().Text
            .ShouldContain("temp-doc.md");

        // Delete the document
        var deleteResult = await _fixture.McpClient!.CallToolAsync(
            "delete_documents",
            new Dictionary<string, object?>
            {
                ["path"] = docPath,
                ["collection"] = _testCollection
            });
        deleteResult.IsError.ShouldBeFalse("Delete should succeed");

        // Verify document is no longer searchable
        var searchAfter = await _fixture.McpClient!.CallToolAsync(
            "semantic_search",
            new Dictionary<string, object?>
            {
                ["query"] = "temporary test document deleted",
                ["collection"] = _testCollection
            });
        searchAfter.Content.OfType<TextContentBlock>().First().Text
            .ShouldNotContain("temp-doc.md");
    }

    public async Task DisposeAsync()
    {
        try
        {
            await _fixture.McpClient!.CallToolAsync(
                "delete_documents",
                new Dictionary<string, object?> { ["collection"] = _testCollection });
        }
        catch { }

        if (Directory.Exists(_testProjectPath))
        {
            Directory.Delete(_testProjectPath, recursive: true);
        }
    }
}
```

### FileWatcherWorkflowTests.cs

```csharp
using CompoundDocs.IntegrationTests.Fixtures;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Shouldly;
using Xunit;

namespace CompoundDocs.E2ETests.Workflows;

[Collection("Aspire")]
[Trait("Category", "E2E")]
public class FileWatcherWorkflowTests : IAsyncLifetime
{
    private readonly AspireIntegrationFixture _fixture;
    private readonly string _testCollection;
    private readonly string _testProjectPath;

    public FileWatcherWorkflowTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
        _testCollection = $"test_{Guid.NewGuid():N}";
        _testProjectPath = Path.Combine(Path.GetTempPath(), $"cdocs_test_{Guid.NewGuid():N}");
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_testProjectPath);
        Directory.CreateDirectory(Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems"));

        await _fixture.McpClient!.CallToolAsync(
            "activate_project",
            new Dictionary<string, object?>
            {
                ["project_path"] = _testProjectPath,
                ["branch"] = "main"
            });
    }

    [Fact(Timeout = 120000)]
    [Trait("Workflow", "FileWatcher")]
    public async Task FileCreation_TriggersAutomaticIndexing()
    {
        // Arrange - Create document file
        var docContent = """
            ---
            title: File Watcher Test Document
            date: 2025-01-24
            doc_type: insight
            ---
            # File Watcher Test
            Testing automatic indexing via file system events.
            """;

        var docPath = Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems", "watcher-test.md");

        // Act - Write file (should trigger watcher)
        await File.WriteAllTextAsync(docPath, docContent);

        // Wait for debounce (500ms) + processing time
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert - Document should be searchable
        var searchResult = await _fixture.McpClient!.CallToolAsync(
            "semantic_search",
            new Dictionary<string, object?>
            {
                ["query"] = "file watcher automatic indexing",
                ["collection"] = _testCollection
            });

        searchResult.IsError.ShouldBeFalse();
        searchResult.Content.OfType<TextContentBlock>().First().Text
            .ShouldContain("watcher-test.md");
    }

    [Fact(Timeout = 120000)]
    [Trait("Workflow", "FileWatcher")]
    public async Task FileModification_TriggersReIndexing()
    {
        // Arrange - Create and index initial document
        var initialContent = """
            ---
            title: Document for Modification
            date: 2025-01-24
            doc_type: problem
            ---
            # Original Content
            This is the original version.
            """;

        var docPath = Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems", "modify-test.md");
        await File.WriteAllTextAsync(docPath, initialContent);

        // Index explicitly first
        await _fixture.McpClient!.CallToolAsync(
            "index_document",
            new Dictionary<string, object?> { ["path"] = docPath, ["collection"] = _testCollection });

        // Act - Modify the file
        var modifiedContent = """
            ---
            title: Document for Modification
            date: 2025-01-24
            doc_type: problem
            ---
            # Updated Content
            This version includes UPDATED_MARKER for verification.
            """;

        await File.WriteAllTextAsync(docPath, modifiedContent);

        // Wait for watcher debounce + re-indexing
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert - Updated content should be searchable
        var searchResult = await _fixture.McpClient!.CallToolAsync(
            "semantic_search",
            new Dictionary<string, object?>
            {
                ["query"] = "UPDATED_MARKER verification",
                ["collection"] = _testCollection
            });

        searchResult.IsError.ShouldBeFalse();
        searchResult.Content.OfType<TextContentBlock>().First().Text
            .ShouldContain("modify-test.md");
    }

    [Fact(Timeout = 120000)]
    [Trait("Workflow", "FileWatcher")]
    public async Task FileDeletion_TriggersDocumentRemoval()
    {
        // Arrange - Create and index document
        var docContent = """
            ---
            title: Document to Delete
            date: 2025-01-24
            doc_type: problem
            ---
            # Delete Me
            This document will be deleted via file system.
            """;

        var docPath = Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems", "delete-test.md");
        await File.WriteAllTextAsync(docPath, docContent);

        await _fixture.McpClient!.CallToolAsync(
            "index_document",
            new Dictionary<string, object?> { ["path"] = docPath, ["collection"] = _testCollection });

        // Verify indexed
        var searchBefore = await _fixture.McpClient!.CallToolAsync(
            "semantic_search",
            new Dictionary<string, object?> { ["query"] = "document to delete", ["collection"] = _testCollection });
        searchBefore.Content.OfType<TextContentBlock>().First().Text
            .ShouldContain("delete-test.md");

        // Act - Delete the file
        File.Delete(docPath);

        // Wait for watcher to process deletion
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert - Document should no longer be searchable
        var searchAfter = await _fixture.McpClient!.CallToolAsync(
            "semantic_search",
            new Dictionary<string, object?> { ["query"] = "document to delete", ["collection"] = _testCollection });
        searchAfter.Content.OfType<TextContentBlock>().First().Text
            .ShouldNotContain("delete-test.md");
    }

    [Fact(Timeout = 120000)]
    [Trait("Workflow", "FileWatcher")]
    public async Task RapidFileChanges_DebouncedToSingleIndexOperation()
    {
        // Arrange
        var docPath = Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems", "debounce-test.md");

        // Act - Rapid writes (should debounce to single index)
        for (int i = 1; i <= 5; i++)
        {
            var content = $"""
                ---
                title: Debounce Test Iteration {i}
                date: 2025-01-24
                doc_type: insight
                ---
                # Iteration {i}
                Final iteration marker: DEBOUNCE_FINAL_{i}
                """;
            await File.WriteAllTextAsync(docPath, content);
            await Task.Delay(100); // 100ms between writes
        }

        // Wait for debounce window (500ms) + processing
        await Task.Delay(TimeSpan.FromSeconds(2));

        // Assert - Only final version should be indexed
        var searchResult = await _fixture.McpClient!.CallToolAsync(
            "semantic_search",
            new Dictionary<string, object?> { ["query"] = "DEBOUNCE_FINAL", ["collection"] = _testCollection });

        searchResult.IsError.ShouldBeFalse();
        var response = searchResult.Content.OfType<TextContentBlock>().First().Text;

        // Should contain the final iteration
        response.ShouldContain("DEBOUNCE_FINAL_5");
        // Earlier iterations should not appear (they were debounced)
        response.ShouldNotContain("DEBOUNCE_FINAL_1");
    }

    public async Task DisposeAsync()
    {
        try
        {
            await _fixture.McpClient!.CallToolAsync(
                "delete_documents",
                new Dictionary<string, object?> { ["collection"] = _testCollection });
        }
        catch { }

        if (Directory.Exists(_testProjectPath))
        {
            Directory.Delete(_testProjectPath, recursive: true);
        }
    }
}
```

### Multi-Document Linking Tests

```csharp
// Add to FullLifecycleWorkflowTests.cs or create separate LinkedDocumentWorkflowTests.cs

[Fact(Timeout = 120000)]
[Trait("Workflow", "Linking")]
public async Task DocumentsWithLinks_QueryIncludesLinkedContent()
{
    // Create main document with link
    var mainDoc = """
        ---
        title: Authentication Overview
        date: 2025-01-24
        doc_type: codebase
        ---
        # Authentication Overview
        See [JWT Implementation](./jwt-details.md) for token handling.
        """;

    // Create linked document
    var linkedDoc = """
        ---
        title: JWT Implementation Details
        date: 2025-01-24
        doc_type: codebase
        ---
        # JWT Implementation
        Tokens use RS256 signing algorithm with 1-hour expiry.
        """;

    var mainPath = Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems", "auth-overview.md");
    var linkedPath = Path.Combine(_testProjectPath, "csharp-compounding-docs", "problems", "jwt-details.md");

    await File.WriteAllTextAsync(mainPath, mainDoc);
    await File.WriteAllTextAsync(linkedPath, linkedDoc);

    // Index both
    await _fixture.McpClient!.CallToolAsync(
        "index_document",
        new Dictionary<string, object?> { ["path"] = mainPath, ["collection"] = _testCollection });
    await _fixture.McpClient!.CallToolAsync(
        "index_document",
        new Dictionary<string, object?> { ["path"] = linkedPath, ["collection"] = _testCollection });

    // Query should resolve links and include linked content
    var queryResult = await _fixture.McpClient!.CallToolAsync(
        "rag_query",
        new Dictionary<string, object?>
        {
            ["query"] = "authentication token expiry",
            ["collection"] = _testCollection,
            ["follow_links"] = true
        });

    queryResult.IsError.ShouldBeFalse();
    var response = queryResult.Content.OfType<TextContentBlock>().First().Text;

    // Should include content from linked document
    response.ShouldContain("RS256", Case.Insensitive);
    response.ShouldContain("1-hour", Case.Insensitive);
}
```

---

## Test Data Patterns

### Unique Collection Names

Every test class generates a unique collection name to ensure complete isolation:

```csharp
private readonly string _testCollection = $"test_{Guid.NewGuid():N}";
```

### Test Document Templates

Use consistent frontmatter structures that match the spec:

```yaml
---
title: [Descriptive Title]
date: YYYY-MM-DD
doc_type: problem|insight|codebase|tool|style
promotion_level: standard|starred|pinned
summary: Brief description
---
```

### Cleanup Pattern

All test classes implement `IAsyncLifetime.DisposeAsync()` to clean up:

1. Delete test collection from vector store
2. Delete temporary project directory

---

## Dependencies

### Depends On

- **Phase 120**: MCP Client Test Patterns (provides MCP client testing infrastructure)
- **Phase 115**: Aspire Integration Fixture (provides `AspireIntegrationFixture`)
- **Phase 117**: Database Isolation (provides isolation strategies)
- **Phase 071**: RAG Query Tool (provides `rag_query` implementation)
- **Phase 072**: Semantic Search Tool (provides `semantic_search` implementation)
- **Phase 079**: Activate Project Tool (provides `activate_project` implementation)
- **Phase 053**: File Watcher Service (provides file system event handling)

### Blocks

- **Phase 144**: Performance Benchmarks (may reference E2E tests for baseline)
- **Phase 145**: CI Workflow Updates (adds E2E test stage)

---

## Verification Steps

After completing this phase, verify:

1. **All test files compile**:
   ```bash
   dotnet build tests/CompoundDocs.E2ETests
   ```

2. **E2E tests pass** (requires Docker and infrastructure):
   ```bash
   dotnet test tests/CompoundDocs.E2ETests --filter "Category=E2E"
   ```

3. **Day 1 and Day 30 workflows complete successfully**:
   ```bash
   dotnet test tests/CompoundDocs.E2ETests --filter "Workflow=DocumentIndexing"
   dotnet test tests/CompoundDocs.E2ETests --filter "Workflow=RagQuery"
   ```

4. **File watcher tests demonstrate automatic indexing**:
   ```bash
   dotnet test tests/CompoundDocs.E2ETests --filter "Workflow=FileWatcher"
   ```

5. **Tool chain tests validate complete flows**:
   ```bash
   dotnet test tests/CompoundDocs.E2ETests --filter "Workflow=FullLifecycle"
   ```

6. **Test isolation verified** (run tests in parallel if possible):
   ```bash
   dotnet test tests/CompoundDocs.E2ETests --parallel
   ```

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `tests/CompoundDocs.E2ETests/Workflows/DocumentIndexingWorkflowTests.cs` | Day 1 capture workflow tests |
| `tests/CompoundDocs.E2ETests/Workflows/RagQueryWorkflowTests.cs` | Day 30 retrieval workflow tests |
| `tests/CompoundDocs.E2ETests/Workflows/FullLifecycleWorkflowTests.cs` | Complete tool chain tests |
| `tests/CompoundDocs.E2ETests/Workflows/FileWatcherWorkflowTests.cs` | File watcher integration tests |
| `tests/CompoundDocs.E2ETests/Workflows/LinkedDocumentWorkflowTests.cs` | Multi-document linking tests |

### Modified Files

| File | Changes |
|------|---------|
| `tests/CompoundDocs.E2ETests/CompoundDocs.E2ETests.csproj` | Add any missing package references |

---

## Notes

- E2E tests require full infrastructure (Docker, PostgreSQL, Ollama)
- First-run tests may be slow due to Ollama model downloads
- Tests use 2-minute timeout per spec to account for embedding generation latency
- Collection cleanup is critical to prevent test pollution
- File watcher tests include delays to account for debounce windows
- Consider running E2E tests in CI with pre-warmed Ollama models (see Phase 121)
- Promotion boost tests validate the scoring multiplier effect on search results
