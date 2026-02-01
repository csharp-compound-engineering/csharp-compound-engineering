# Phase 072: semantic_search MCP Tool

> **Status**: PLANNED
> **Category**: MCP Tools
> **Estimated Effort**: M
> **Prerequisites**: Phase 025 (Tool Registration System), Phase 050 (Vector Search Service)

---

## Spec References

- [mcp-server/tools.md - Semantic Search Tool](../spec/mcp-server/tools.md#2-semantic-search-tool)
- [mcp-server/database-schema.md - Query Filtering](../spec/mcp-server/database-schema.md#query-filtering)
- [mcp-server/tools.md - Error Handling](../spec/mcp-server/tools.md#error-handling)

---

## Objectives

1. Register `semantic_search` tool with the MCP server using `[McpServerTool]` attribute
2. Define tool parameters: `query`, `doc_types`, `limit`, `min_relevance_score`, `promotion_levels`
3. Integrate with `IVectorSearchService` for vector similarity search execution
4. Return ranked results with relevance scores and document metadata
5. Support both chunk-level and full document results
6. Implement tenant context validation and error handling

---

## Acceptance Criteria

### Tool Registration

- [ ] `[McpServerTool(Name = "semantic_search")]` attribute applied to method
- [ ] Tool registered via `[McpServerToolType]` class in `SearchTools.cs`
- [ ] All parameters have `[Description]` attributes for LLM schema generation
- [ ] Tool discoverable via MCP `tools/list` protocol method

### Parameter Definition

- [ ] `query` (string, required): Natural language search query
- [ ] `doc_types` (string[]?, optional): Filter to specific doc-types (default: all)
- [ ] `limit` (int?, optional): Maximum results (default: 10)
- [ ] `min_relevance_score` (float?, optional): Minimum relevance threshold (default: 0.5)
- [ ] `promotion_levels` (string[]?, optional): Filter to specific levels (default: all)

### Vector Similarity Search

- [ ] Generate query embedding via `IEmbeddingService`
- [ ] Execute search via `IVectorSearchService.SearchDocumentsAsync()`
- [ ] Apply tenant isolation filter from active project context
- [ ] Apply doc_types filter when specified
- [ ] Apply promotion_levels filter when specified
- [ ] Filter results by min_relevance_score threshold

### Response Format

- [ ] Return `results` array with ranked documents
- [ ] Include `path`, `title`, `summary`, `char_count`, `relevance_score` per result
- [ ] Include `doc_type`, `date`, `promotion_level` per result
- [ ] Return `total_matches` count
- [ ] Results ordered by descending relevance score

### Chunk vs Full Document Results

- [ ] Search both documents and document_chunks collections
- [ ] Deduplicate results when chunk and parent document both match
- [ ] Include chunk metadata (`header_path`, `chunk_index`) when result is from chunk
- [ ] Prefer higher-scoring match when chunk and document both match

### Error Handling

- [ ] Return `PROJECT_NOT_ACTIVATED` if no active project
- [ ] Return `EMBEDDING_SERVICE_ERROR` if Ollama unavailable
- [ ] Return `DATABASE_ERROR` if PostgreSQL operation fails
- [ ] Return `INVALID_DOC_TYPE` if unknown doc-type specified
- [ ] Validate promotion_levels values against allowed set

---

## Implementation Notes

### Tool Class Registration

Add to `src/CompoundDocs.McpServer/Tools/SearchTools.cs`:

```csharp
using System.ComponentModel;
using System.Text.Json;
using CompoundDocs.Common.Search;
using CompoundDocs.Common.Services;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// MCP tools for semantic search operations against compounding docs.
/// </summary>
[McpServerToolType]
public class SearchTools
{
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IActiveProjectContext _projectContext;
    private readonly ILogger<SearchTools> _logger;

    public SearchTools(
        IVectorSearchService vectorSearchService,
        IActiveProjectContext projectContext,
        ILogger<SearchTools> logger)
    {
        _vectorSearchService = vectorSearchService ?? throw new ArgumentNullException(nameof(vectorSearchService));
        _projectContext = projectContext ?? throw new ArgumentNullException(nameof(projectContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [McpServerTool(Name = "semantic_search")]
    [Description("Search compounding docs by semantic similarity. Returns ranked documents without RAG synthesis.")]
    public async Task<string> SemanticSearch(
        [Description("Natural language search query")] string query,
        [Description("Filter to specific doc-types (default: all). Valid types: problem, insight, codebase, tool, style, or custom types.")]
        string[]? docTypes = null,
        [Description("Maximum results to return (default: 10, max: 50)")]
        int limit = 10,
        [Description("Minimum relevance score threshold 0.0-1.0 (default: 0.5)")]
        float minRelevanceScore = 0.5f,
        [Description("Filter to specific promotion levels: standard, important, critical (default: all)")]
        string[]? promotionLevels = null,
        CancellationToken cancellationToken = default)
    {
        // Validate project is activated
        if (!_projectContext.IsActivated)
        {
            _logger.LogWarning("semantic_search called without active project");
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: ToolErrorCodes.ProjectNotActivated,
                Message: "No project is currently activated. Call activate_project first.",
                Details: new { requiredTool = "activate_project" }));
        }

        // Validate and clamp parameters
        limit = Math.Clamp(limit, 1, 50);
        minRelevanceScore = Math.Clamp(minRelevanceScore, 0.0f, 1.0f);

        _logger.LogInformation(
            "semantic_search: query='{Query}' limit={Limit} minScore={MinScore}",
            query.Length > 100 ? query[..100] + "..." : query,
            limit,
            minRelevanceScore);

        try
        {
            // Build search filter from active project context
            var filter = VectorSearchFilter.FromProjectContext(_projectContext);

            // Apply doc_types filter if specified
            if (docTypes != null && docTypes.Length > 0)
            {
                // Validate doc types against available types
                var validDocTypes = await ValidateDocTypesAsync(docTypes, cancellationToken);
                if (validDocTypes.InvalidTypes.Count > 0)
                {
                    return JsonSerializer.Serialize(new ToolErrorResponse(
                        Error: true,
                        Code: ToolErrorCodes.InvalidDocType,
                        Message: $"Unknown doc-type(s): {string.Join(", ", validDocTypes.InvalidTypes)}",
                        Details: new {
                            invalidTypes = validDocTypes.InvalidTypes,
                            validTypes = validDocTypes.ValidTypes
                        }));
                }
                filter = filter.WithDocTypes(docTypes);
            }

            // Apply promotion_levels filter if specified
            if (promotionLevels != null && promotionLevels.Length > 0)
            {
                try
                {
                    filter = filter.WithPromotionLevels(promotionLevels);
                }
                catch (ArgumentException ex)
                {
                    return JsonSerializer.Serialize(new ToolErrorResponse(
                        Error: true,
                        Code: "INVALID_PROMOTION_LEVEL",
                        Message: ex.Message,
                        Details: new { validLevels = new[] { "standard", "important", "critical" } }));
                }
            }

            // Execute vector search across documents and chunks
            var documentResults = await _vectorSearchService.SearchDocumentsAsync(
                query,
                filter,
                limit,
                minRelevanceScore,
                cancellationToken);

            var chunkResults = await _vectorSearchService.SearchChunksAsync(
                query,
                filter,
                limit,
                minRelevanceScore,
                cancellationToken);

            // Merge and deduplicate results
            var mergedResults = MergeDocumentAndChunkResults(
                documentResults.Results,
                chunkResults.Results,
                limit);

            // Build response
            var response = new SemanticSearchResponse(
                Results: mergedResults.Select(r => new SemanticSearchResult(
                    Path: BuildDocumentPath(r.RelativePath),
                    Title: r.Title,
                    Summary: r.Summary,
                    CharCount: r.CharCount,
                    RelevanceScore: (float)r.RelevanceScore,
                    DocType: r.DocType,
                    Date: r.Date?.ToString("yyyy-MM-dd"),
                    PromotionLevel: r.PromotionLevel,
                    ChunkInfo: r.ParentDocumentId != null
                        ? new ChunkInfo(r.HeaderPath ?? "", r.ChunkIndex ?? 0)
                        : null
                )).ToList(),
                TotalMatches: mergedResults.Count);

            _logger.LogInformation(
                "semantic_search completed: {ResultCount} results found",
                response.Results.Count);

            return JsonSerializer.Serialize(response, ToolJsonOptions.Default);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("Ollama") || ex.Message.Contains("embedding"))
        {
            _logger.LogError(ex, "Embedding service error during semantic_search");
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: ToolErrorCodes.EmbeddingServiceError,
                Message: "Failed to generate query embedding. Ensure Ollama is running.",
                Details: new { innerMessage = ex.Message }));
        }
        catch (Npgsql.NpgsqlException ex)
        {
            _logger.LogError(ex, "Database error during semantic_search");
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: ToolErrorCodes.DatabaseError,
                Message: "Database operation failed during search.",
                Details: new { innerMessage = ex.Message }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during semantic_search");
            throw; // Let MCP SDK handle unexpected errors
        }
    }

    #region Private Helper Methods

    private async Task<(IReadOnlyList<string> ValidTypes, IReadOnlyList<string> InvalidTypes)> ValidateDocTypesAsync(
        string[] requestedTypes,
        CancellationToken cancellationToken)
    {
        // Get available doc types from project configuration
        var availableTypes = await _projectContext.GetAvailableDocTypesAsync(cancellationToken);
        var availableSet = new HashSet<string>(availableTypes, StringComparer.OrdinalIgnoreCase);

        var validTypes = requestedTypes.Where(t => availableSet.Contains(t)).ToList();
        var invalidTypes = requestedTypes.Where(t => !availableSet.Contains(t)).ToList();

        return (validTypes, invalidTypes);
    }

    private static List<VectorSearchResult> MergeDocumentAndChunkResults(
        IReadOnlyList<VectorSearchResult> documentResults,
        IReadOnlyList<VectorSearchResult> chunkResults,
        int limit)
    {
        // Combine all results
        var allResults = new List<VectorSearchResult>();
        allResults.AddRange(documentResults);

        // Track which parent documents we've already included
        var includedDocIds = documentResults.Select(d => d.Id).ToHashSet();

        // Add chunk results, preferring chunks over their parent if chunk scores higher
        foreach (var chunk in chunkResults)
        {
            if (chunk.ParentDocumentId != null)
            {
                // Check if parent document is already in results
                var parentInResults = documentResults.FirstOrDefault(d => d.Id == chunk.ParentDocumentId);

                if (parentInResults != null)
                {
                    // If chunk scores higher than parent, replace parent with chunk
                    if (chunk.RelevanceScore > parentInResults.RelevanceScore)
                    {
                        allResults.Remove(parentInResults);
                        allResults.Add(chunk);
                    }
                    // Otherwise keep parent, skip chunk (already represented)
                }
                else if (!includedDocIds.Contains(chunk.ParentDocumentId))
                {
                    // Parent not in results, add chunk
                    allResults.Add(chunk);
                    includedDocIds.Add(chunk.ParentDocumentId);
                }
            }
            else
            {
                // Chunk without parent (shouldn't happen, but handle gracefully)
                allResults.Add(chunk);
            }
        }

        // Sort by relevance and take top N
        return allResults
            .OrderByDescending(r => r.RelevanceScore)
            .Take(limit)
            .ToList();
    }

    private static string BuildDocumentPath(string relativePath)
    {
        // Ensure path starts with ./csharp-compounding-docs/
        if (string.IsNullOrEmpty(relativePath))
        {
            return relativePath;
        }

        if (!relativePath.StartsWith("./"))
        {
            return $"./csharp-compounding-docs/{relativePath}";
        }

        return relativePath;
    }

    #endregion
}
```

### Response DTOs

Add to `src/CompoundDocs.McpServer/Models/ToolResponses.cs`:

```csharp
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Response from semantic_search tool.
/// </summary>
public record SemanticSearchResponse(
    IReadOnlyList<SemanticSearchResult> Results,
    int TotalMatches);

/// <summary>
/// A single result from semantic search.
/// </summary>
public record SemanticSearchResult(
    string Path,
    string Title,
    string? Summary,
    int CharCount,
    float RelevanceScore,
    string DocType,
    string? Date,
    string PromotionLevel,
    ChunkInfo? ChunkInfo = null);

/// <summary>
/// Chunk-specific metadata when result is from a document chunk.
/// </summary>
public record ChunkInfo(
    string HeaderPath,
    int ChunkIndex);
```

### JSON Serialization Options

Create `src/CompoundDocs.McpServer/Models/ToolJsonOptions.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Shared JSON serialization options for tool responses.
/// </summary>
public static class ToolJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
```

### Example Response

Successful search response:

```json
{
  "results": [
    {
      "path": "./csharp-compounding-docs/problems/db-pool-exhaustion-20250115.md",
      "title": "Database Connection Pool Exhaustion",
      "summary": "Connection pool exhaustion caused by missing disposal in background jobs",
      "char_count": 2847,
      "relevance_score": 0.92,
      "doc_type": "problem",
      "date": "2025-01-15",
      "promotion_level": "critical"
    },
    {
      "path": "./csharp-compounding-docs/tools/npgsql-configuration-20250110.md",
      "title": "Npgsql Connection Configuration",
      "summary": "Best practices for configuring Npgsql connection pooling",
      "char_count": 1523,
      "relevance_score": 0.78,
      "doc_type": "tool",
      "date": "2025-01-10",
      "promotion_level": "standard"
    },
    {
      "path": "./csharp-compounding-docs/codebase/connection-management-20250108.md",
      "title": "Connection Management Architecture",
      "summary": null,
      "char_count": 3102,
      "relevance_score": 0.71,
      "doc_type": "codebase",
      "date": "2025-01-08",
      "promotion_level": "important",
      "chunk_info": {
        "header_path": "## Database Connections > ### Connection Pooling",
        "chunk_index": 2
      }
    }
  ],
  "total_matches": 3
}
```

Error response (project not activated):

```json
{
  "error": true,
  "code": "PROJECT_NOT_ACTIVATED",
  "message": "No project is currently activated. Call activate_project first.",
  "details": {
    "required_tool": "activate_project"
  }
}
```

### Tool Schema Generation

The tool parameters will generate the following JSON schema for the MCP `tools/list` response:

```json
{
  "name": "semantic_search",
  "description": "Search compounding docs by semantic similarity. Returns ranked documents without RAG synthesis.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "query": {
        "type": "string",
        "description": "Natural language search query"
      },
      "doc_types": {
        "type": "array",
        "items": { "type": "string" },
        "description": "Filter to specific doc-types (default: all). Valid types: problem, insight, codebase, tool, style, or custom types."
      },
      "limit": {
        "type": "integer",
        "description": "Maximum results to return (default: 10, max: 50)"
      },
      "min_relevance_score": {
        "type": "number",
        "description": "Minimum relevance score threshold 0.0-1.0 (default: 0.5)"
      },
      "promotion_levels": {
        "type": "array",
        "items": { "type": "string" },
        "description": "Filter to specific promotion levels: standard, important, critical (default: all)"
      }
    },
    "required": ["query"]
  }
}
```

---

## Dependencies

### Depends On

- **Phase 025**: Tool Registration System - MCP tool registration infrastructure
- **Phase 050**: Vector Search Service - `IVectorSearchService` for similarity search
- **Phase 029**: Embedding Service - `IEmbeddingService` for query embedding
- **Phase 038**: Tenant Context - `IActiveProjectContext` for tenant isolation

### Blocks

- **Phase 080+**: Integration testing of search tools
- End-to-end testing phases

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Tools/SearchToolsTests.cs
public class SearchToolsTests
{
    [Fact]
    public async Task SemanticSearch_WithActiveProject_ReturnsResults()
    {
        // Arrange
        var mockVectorSearch = new Mock<IVectorSearchService>();
        mockVectorSearch
            .Setup(s => s.SearchDocumentsAsync(
                It.IsAny<string>(),
                It.IsAny<VectorSearchFilter>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VectorSearchResults
            {
                Results = new[]
                {
                    new VectorSearchResult
                    {
                        Id = "doc1",
                        RelativePath = "problems/test.md",
                        Title = "Test Doc",
                        DocType = "problem",
                        PromotionLevel = "standard",
                        RelevanceScore = 0.85
                    }
                },
                TotalMatches = 1,
                Query = "test",
                MinRelevanceScore = 0.5,
                Limit = 10
            });

        var mockContext = new Mock<IActiveProjectContext>();
        mockContext.Setup(c => c.IsActivated).Returns(true);
        mockContext.Setup(c => c.ProjectName).Returns("test-project");
        mockContext.Setup(c => c.BranchName).Returns("main");
        mockContext.Setup(c => c.PathHash).Returns("abc123");
        mockContext
            .Setup(c => c.GetAvailableDocTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "problem", "insight", "codebase", "tool", "style" });

        var tools = new SearchTools(
            mockVectorSearch.Object,
            mockContext.Object,
            Mock.Of<ILogger<SearchTools>>());

        // Act
        var result = await tools.SemanticSearch("database connection");

        // Assert
        var response = JsonSerializer.Deserialize<SemanticSearchResponse>(result, ToolJsonOptions.Default);
        Assert.NotNull(response);
        Assert.Single(response.Results);
        Assert.Equal("Test Doc", response.Results[0].Title);
    }

    [Fact]
    public async Task SemanticSearch_WithoutActiveProject_ReturnsError()
    {
        // Arrange
        var mockContext = new Mock<IActiveProjectContext>();
        mockContext.Setup(c => c.IsActivated).Returns(false);

        var tools = new SearchTools(
            Mock.Of<IVectorSearchService>(),
            mockContext.Object,
            Mock.Of<ILogger<SearchTools>>());

        // Act
        var result = await tools.SemanticSearch("test query");

        // Assert
        var error = JsonSerializer.Deserialize<ToolErrorResponse>(result);
        Assert.NotNull(error);
        Assert.True(error.Error);
        Assert.Equal("PROJECT_NOT_ACTIVATED", error.Code);
    }

    [Fact]
    public async Task SemanticSearch_WithInvalidDocType_ReturnsError()
    {
        // Arrange
        var mockContext = new Mock<IActiveProjectContext>();
        mockContext.Setup(c => c.IsActivated).Returns(true);
        mockContext
            .Setup(c => c.GetAvailableDocTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "problem", "insight" });

        var tools = new SearchTools(
            Mock.Of<IVectorSearchService>(),
            mockContext.Object,
            Mock.Of<ILogger<SearchTools>>());

        // Act
        var result = await tools.SemanticSearch(
            "test query",
            docTypes: new[] { "invalid-type" });

        // Assert
        var error = JsonSerializer.Deserialize<ToolErrorResponse>(result);
        Assert.NotNull(error);
        Assert.True(error.Error);
        Assert.Equal("INVALID_DOC_TYPE", error.Code);
    }

    [Fact]
    public async Task SemanticSearch_WithPromotionLevelFilter_AppliesFilter()
    {
        // Arrange
        var mockVectorSearch = new Mock<IVectorSearchService>();
        mockVectorSearch
            .Setup(s => s.SearchDocumentsAsync(
                It.IsAny<string>(),
                It.Is<VectorSearchFilter>(f =>
                    f.PromotionLevels != null &&
                    f.PromotionLevels.Contains("critical")),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResults());

        var mockContext = CreateActivatedContext();

        var tools = new SearchTools(
            mockVectorSearch.Object,
            mockContext.Object,
            Mock.Of<ILogger<SearchTools>>());

        // Act
        await tools.SemanticSearch(
            "test query",
            promotionLevels: new[] { "critical" });

        // Assert
        mockVectorSearch.Verify(s => s.SearchDocumentsAsync(
            It.IsAny<string>(),
            It.Is<VectorSearchFilter>(f => f.PromotionLevels!.Contains("critical")),
            It.IsAny<int>(),
            It.IsAny<double>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SemanticSearch_ClampsLimitToMax50()
    {
        // Arrange
        var mockVectorSearch = new Mock<IVectorSearchService>();
        mockVectorSearch
            .Setup(s => s.SearchDocumentsAsync(
                It.IsAny<string>(),
                It.IsAny<VectorSearchFilter>(),
                50, // Should be clamped to 50
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEmptyResults());

        var mockContext = CreateActivatedContext();

        var tools = new SearchTools(
            mockVectorSearch.Object,
            mockContext.Object,
            Mock.Of<ILogger<SearchTools>>());

        // Act
        await tools.SemanticSearch("test", limit: 100);

        // Assert
        mockVectorSearch.Verify(s => s.SearchDocumentsAsync(
            It.IsAny<string>(),
            It.IsAny<VectorSearchFilter>(),
            50,
            It.IsAny<double>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SemanticSearch_MergesChunkAndDocumentResults()
    {
        // Test that chunk results are properly merged with document results
        // and deduplication works correctly
    }

    #region Test Helpers

    private static Mock<IActiveProjectContext> CreateActivatedContext()
    {
        var mock = new Mock<IActiveProjectContext>();
        mock.Setup(c => c.IsActivated).Returns(true);
        mock.Setup(c => c.ProjectName).Returns("test");
        mock.Setup(c => c.BranchName).Returns("main");
        mock.Setup(c => c.PathHash).Returns("abc123");
        mock.Setup(c => c.GetAvailableDocTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "problem", "insight", "codebase", "tool", "style" });
        return mock;
    }

    private static VectorSearchResults CreateEmptyResults()
    {
        return new VectorSearchResults
        {
            Results = Array.Empty<VectorSearchResult>(),
            TotalMatches = 0,
            Query = "test",
            MinRelevanceScore = 0.5,
            Limit = 10
        };
    }

    #endregion
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Tools/SearchToolsIntegrationTests.cs
[Trait("Category", "Integration")]
public class SearchToolsIntegrationTests : IClassFixture<McpServerFixture>
{
    private readonly McpServerFixture _fixture;

    public SearchToolsIntegrationTests(McpServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SemanticSearch_WithSeedData_ReturnsRelevantResults()
    {
        // Arrange
        await _fixture.ActivateTestProject();
        await _fixture.SeedTestDocuments();

        var tools = _fixture.GetService<SearchTools>();

        // Act
        var result = await tools.SemanticSearch("database connection pooling");

        // Assert
        var response = JsonSerializer.Deserialize<SemanticSearchResponse>(result, ToolJsonOptions.Default);
        Assert.NotNull(response);
        Assert.NotEmpty(response.Results);
        Assert.True(response.Results.All(r => r.RelevanceScore >= 0.5));
    }

    [Fact]
    public async Task SemanticSearch_ToolDiscoverable_ViaMcpProtocol()
    {
        // Arrange
        var mcpClient = _fixture.CreateMcpClient();

        // Act
        var tools = await mcpClient.ListToolsAsync();

        // Assert
        var semanticSearch = tools.FirstOrDefault(t => t.Name == "semantic_search");
        Assert.NotNull(semanticSearch);
        Assert.Contains("query", semanticSearch.InputSchema.GetProperty("required").EnumerateArray().Select(e => e.GetString()));
    }
}
```

### Manual Verification

```bash
# 1. Start MCP server
cd src/CompoundDocs.McpServer
dotnet run

# 2. Use MCP CLI to list tools
mcp-cli list-tools --stdio "dotnet run --project src/CompoundDocs.McpServer"

# Expected: semantic_search tool listed with schema

# 3. Test semantic_search via MCP CLI
mcp-cli call-tool semantic_search \
  --param query="database connection issues" \
  --param limit=5 \
  --stdio "dotnet run --project src/CompoundDocs.McpServer"

# 4. Test with filters
mcp-cli call-tool semantic_search \
  --param query="configuration" \
  --param doc_types='["tool", "codebase"]' \
  --param promotion_levels='["important", "critical"]' \
  --stdio "dotnet run --project src/CompoundDocs.McpServer"
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Tools/SearchTools.cs` | Create | Search tool class with `[McpServerToolType]` |
| `src/CompoundDocs.McpServer/Models/ToolResponses.cs` | Modify | Add `SemanticSearchResponse`, `SemanticSearchResult`, `ChunkInfo` |
| `src/CompoundDocs.McpServer/Models/ToolJsonOptions.cs` | Create | Shared JSON serialization options |
| `tests/CompoundDocs.Tests/Tools/SearchToolsTests.cs` | Create | Unit tests for search tools |
| `tests/CompoundDocs.IntegrationTests/Tools/SearchToolsIntegrationTests.cs` | Create | Integration tests |

---

## Configuration Reference

### Default Parameter Values

| Parameter | Default | Min | Max | Notes |
|-----------|---------|-----|-----|-------|
| `limit` | 10 | 1 | 50 | Clamped server-side |
| `min_relevance_score` | 0.5 | 0.0 | 1.0 | Lower than RAG (0.7) for broader discovery |

### Valid Promotion Levels

| Level | Description |
|-------|-------------|
| `standard` | Default level for all documents |
| `important` | High-value documents |
| `critical` | Must-include documents |

### Built-in Doc Types

| Type | Description |
|------|-------------|
| `problem` | Problems and solutions |
| `insight` | Key learnings and discoveries |
| `codebase` | Codebase architecture and patterns |
| `tool` | Tool configurations and usage |
| `style` | Coding style and conventions |

Custom doc types are supported via project configuration.

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Slow search on large document sets | HNSW index, limit parameter, server-side result capping |
| Missing tenant isolation | Filter built from required project context |
| Invalid doc types polluting results | Validation against available types before search |
| Chunk/document duplication | Merge algorithm deduplicates by parent document |
| Ollama unavailable | Clear error message with service name |
| Query embedding timeout | HttpClient timeout configured in embedding service |
| Empty results frustration | Lower default threshold (0.5) than RAG (0.7) |

---

## Success Criteria

1. `semantic_search` tool appears in MCP `tools/list` response
2. Tool executes successfully with just required `query` parameter
3. Optional filters (`doc_types`, `promotion_levels`) work correctly
4. Results are ordered by descending relevance score
5. Chunk results include `chunk_info` metadata
6. Error responses follow standard format
7. Tenant isolation prevents cross-project data leakage
8. Unit and integration tests pass
