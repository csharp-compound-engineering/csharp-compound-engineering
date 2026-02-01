# Phase 075: search_external_docs MCP Tool

> **Status**: PLANNED
> **Category**: MCP Tools
> **Estimated Effort**: S-M
> **Prerequisites**: Phase 025 (Tool Registration System), Phase 049 (External Document Repository)

---

## Spec References

- [mcp-server/tools.md - search_external_docs](../spec/mcp-server/tools.md#5-search-external-docs-tool)
- [mcp-server/tools.md - Error Handling](../spec/mcp-server/tools.md#error-handling)
- [configuration.md - external_docs configuration](../spec/configuration.md#external-documentation-optional)

---

## Objectives

1. Implement the `search_external_docs` MCP tool for searching external project documentation
2. Define parameter schema with query, limit, and min_relevance_score
3. Integrate with `IExternalDocumentRepository` for semantic search
4. Enforce read-only constraint (no document modification)
5. Return results with source path attribution and external_docs_path
6. Handle `EXTERNAL_DOCS_NOT_CONFIGURED` error when external_docs is not set up

---

## Acceptance Criteria

### Tool Registration

- [ ] Tool registered with name `search_external_docs`
- [ ] Tool description: "Search external project documentation (read-only). Requires `external_docs` to be configured in project config."
- [ ] Tool accessible via MCP `tools/list` protocol method

### Parameter Definition

- [ ] `query` parameter: string, required - Search query
- [ ] `limit` parameter: integer, optional - Maximum results (default: 10)
- [ ] `min_relevance_score` parameter: float, optional - Minimum relevance threshold (default: 0.7, overridden by `semantic_search.min_relevance_score` in project config)
- [ ] All parameters have `[Description]` attributes for LLM schema generation

### External Document Search

- [ ] Generate embedding for query using `IEmbeddingService`
- [ ] Perform vector similarity search via `IExternalDocumentRepository.SearchAsync()`
- [ ] Filter results by tenant context (project_name, branch_name, path_hash)
- [ ] Apply min_relevance_score filtering
- [ ] Return up to `limit` results

### Read-Only Constraint Enforcement

- [ ] Tool performs only read operations (no create/update/delete)
- [ ] Repository interface enforces read-only semantics for search operations
- [ ] No side effects on external document collection

### Results with Source Path Attribution

- [ ] Each result includes `path` (relative path within external_docs)
- [ ] Each result includes `title` (extracted from document)
- [ ] Each result includes `summary` (first paragraph or extracted summary)
- [ ] Each result includes `char_count` (document character count)
- [ ] Each result includes `relevance_score` (vector similarity score)
- [ ] Response includes `total_matches` count
- [ ] Response includes `external_docs_path` (configured path from project config)

### External Docs Configuration Requirement

- [ ] Check if `external_docs` is configured in project config before executing search
- [ ] Return `EXTERNAL_DOCS_NOT_CONFIGURED` error if not configured
- [ ] Error message includes instructions for configuration
- [ ] Check project is activated before checking external_docs config

---

## Implementation Notes

### Tool Implementation

```csharp
// src/CompoundDocs.McpServer/Tools/SearchTools.cs
using System.ComponentModel;
using System.Text.Json;
using CompoundDocs.Common.Models;
using CompoundDocs.Common.Repositories;
using CompoundDocs.Common.Services;
using CompoundDocs.McpServer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

[McpServerToolType]
public partial class SearchTools
{
    private readonly IExternalDocumentRepository _externalDocRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IProjectContext _projectContext;
    private readonly ILogger<SearchTools> _logger;

    public SearchTools(
        IExternalDocumentRepository externalDocRepository,
        IEmbeddingService embeddingService,
        IProjectContext projectContext,
        ILogger<SearchTools> logger)
    {
        _externalDocRepository = externalDocRepository;
        _embeddingService = embeddingService;
        _projectContext = projectContext;
        _logger = logger;
    }

    [McpServerTool(Name = "search_external_docs")]
    [Description("Search external project documentation (read-only). Requires external_docs to be configured in project config.")]
    public async Task<string> SearchExternalDocs(
        [Description("Search query")] string query,
        [Description("Maximum results (default: 10)")] int limit = 10,
        [Description("Minimum relevance threshold (default: 0.7)")] float minRelevanceScore = 0.7f,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "search_external_docs called with query: {Query}, limit: {Limit}, minScore: {MinScore}",
            query, limit, minRelevanceScore);

        // Check project is activated
        if (!_projectContext.IsActivated)
        {
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: ToolErrorCodes.ProjectNotActivated,
                Message: "No project is currently activated. Call activate_project first.",
                Details: new { requiredTool = "activate_project" }));
        }

        // Check external_docs is configured
        if (!_projectContext.HasExternalDocs)
        {
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: ToolErrorCodes.ExternalDocsNotConfigured,
                Message: "external_docs is not configured in project config. " +
                         "Add 'external_docs.path' to your .csharp-compounding-docs/config.json file.",
                Details: new
                {
                    configFile = ".csharp-compounding-docs/config.json",
                    exampleConfig = new
                    {
                        external_docs = new
                        {
                            path = "./docs",
                            include_patterns = new[] { "**/*.md" },
                            exclude_patterns = new[] { "**/node_modules/**" }
                        }
                    }
                }));
        }

        try
        {
            // Apply config override for min_relevance_score if present
            var effectiveMinScore = _projectContext.SemanticSearchMinRelevanceScore ?? minRelevanceScore;

            // Generate embedding for query
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

            // Perform semantic search against external docs
            var searchResults = await _externalDocRepository.SearchAsync(
                queryEmbedding,
                _projectContext.ProjectName!,
                _projectContext.BranchName!,
                _projectContext.PathHash!,
                limit,
                effectiveMinScore,
                cancellationToken);

            // Build response
            var response = new SearchExternalDocsResponse(
                Results: searchResults.Select(r => new ExternalSearchResult(
                    Path: r.Document.RelativePath,
                    Title: r.Document.Title,
                    Summary: r.Document.Summary,
                    CharCount: r.Document.CharCount,
                    RelevanceScore: r.RelevanceScore)).ToList(),
                TotalMatches: searchResults.Count,
                ExternalDocsPath: _projectContext.ExternalDocsPath!);

            _logger.LogInformation(
                "search_external_docs returning {Count} results for query: {Query}",
                searchResults.Count, query);

            return JsonSerializer.Serialize(response, JsonOptions.Default);
        }
        catch (EmbeddingServiceException ex)
        {
            _logger.LogError(ex, "Embedding service error during external docs search");
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: ToolErrorCodes.EmbeddingServiceError,
                Message: "Failed to generate embeddings. Ensure Ollama is running.",
                Details: new { innerMessage = ex.Message }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error during external docs search");
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: ToolErrorCodes.DatabaseError,
                Message: "Failed to search external documents.",
                Details: new { innerMessage = ex.Message }));
        }
    }
}
```

### Response DTOs

```csharp
// src/CompoundDocs.McpServer/Models/SearchExternalDocsResponse.cs
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Response from search_external_docs tool.
/// </summary>
public record SearchExternalDocsResponse(
    IReadOnlyList<ExternalSearchResult> Results,
    int TotalMatches,
    string ExternalDocsPath);

/// <summary>
/// A single result from external document search.
/// </summary>
public record ExternalSearchResult(
    string Path,
    string Title,
    string? Summary,
    int CharCount,
    float RelevanceScore);
```

### IProjectContext Interface Extension

Ensure `IProjectContext` includes external docs properties:

```csharp
// In src/CompoundDocs.Common/Services/IProjectContext.cs
public interface IProjectContext
{
    // ... existing properties ...

    /// <summary>
    /// Whether external_docs is configured in project config.
    /// </summary>
    bool HasExternalDocs { get; }

    /// <summary>
    /// The configured external_docs.path, or null if not configured.
    /// </summary>
    string? ExternalDocsPath { get; }

    /// <summary>
    /// Optional override for semantic search min_relevance_score from project config.
    /// </summary>
    float? SemanticSearchMinRelevanceScore { get; }
}
```

### JSON Serialization Options

```csharp
// src/CompoundDocs.McpServer/Models/JsonOptions.cs
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CompoundDocs.McpServer.Models;

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}
```

### Example Tool Response

Success response:
```json
{
  "results": [
    {
      "path": "./docs/architecture/database-design.md",
      "title": "Database Design Guide",
      "summary": "Overview of database schema and design decisions",
      "char_count": 4521,
      "relevance_score": 0.85
    },
    {
      "path": "./docs/api/authentication.md",
      "title": "API Authentication",
      "summary": "How to authenticate with the API using JWT tokens",
      "char_count": 2134,
      "relevance_score": 0.78
    }
  ],
  "total_matches": 2,
  "external_docs_path": "./docs"
}
```

Error response (external_docs not configured):
```json
{
  "error": true,
  "code": "EXTERNAL_DOCS_NOT_CONFIGURED",
  "message": "external_docs is not configured in project config. Add 'external_docs.path' to your .csharp-compounding-docs/config.json file.",
  "details": {
    "configFile": ".csharp-compounding-docs/config.json",
    "exampleConfig": {
      "external_docs": {
        "path": "./docs",
        "include_patterns": ["**/*.md"],
        "exclude_patterns": ["**/node_modules/**"]
      }
    }
  }
}
```

---

## Dependencies

### Depends On

- **Phase 025**: Tool Registration System - Tool registration infrastructure and patterns
- **Phase 049**: External Document Repository - `IExternalDocumentRepository` for search operations
- **Phase 029**: Embedding Service - For generating query embeddings
- **Phase 035**: Session State - `IProjectContext` for tenant context and external_docs config
- **Phase 027**: Error Responses - Standard error response format and codes

### Blocks

- None (end-user tool)

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Tools/SearchExternalDocsToolTests.cs
public class SearchExternalDocsToolTests
{
    [Fact]
    public async Task SearchExternalDocs_ReturnsError_WhenProjectNotActivated()
    {
        // Arrange
        var mockProjectContext = new Mock<IProjectContext>();
        mockProjectContext.Setup(p => p.IsActivated).Returns(false);

        var tool = CreateSearchTools(projectContext: mockProjectContext.Object);

        // Act
        var result = await tool.SearchExternalDocs("test query");

        // Assert
        var response = JsonSerializer.Deserialize<ToolErrorResponse>(result);
        Assert.True(response?.Error);
        Assert.Equal("PROJECT_NOT_ACTIVATED", response?.Code);
    }

    [Fact]
    public async Task SearchExternalDocs_ReturnsError_WhenExternalDocsNotConfigured()
    {
        // Arrange
        var mockProjectContext = new Mock<IProjectContext>();
        mockProjectContext.Setup(p => p.IsActivated).Returns(true);
        mockProjectContext.Setup(p => p.HasExternalDocs).Returns(false);

        var tool = CreateSearchTools(projectContext: mockProjectContext.Object);

        // Act
        var result = await tool.SearchExternalDocs("test query");

        // Assert
        var response = JsonSerializer.Deserialize<ToolErrorResponse>(result);
        Assert.True(response?.Error);
        Assert.Equal("EXTERNAL_DOCS_NOT_CONFIGURED", response?.Code);
    }

    [Fact]
    public async Task SearchExternalDocs_ReturnsResults_WhenSearchSucceeds()
    {
        // Arrange
        var mockProjectContext = CreateActivatedProjectContext(hasExternalDocs: true);
        var mockEmbeddingService = new Mock<IEmbeddingService>();
        mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1024]);

        var mockRepository = new Mock<IExternalDocumentRepository>();
        mockRepository
            .Setup(r => r.SearchAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExternalDocumentSearchResult>
            {
                new(CreateTestExternalDocument("./docs/test.md", "Test Doc"), 0.85f)
            });

        var tool = CreateSearchTools(
            projectContext: mockProjectContext.Object,
            embeddingService: mockEmbeddingService.Object,
            externalDocRepository: mockRepository.Object);

        // Act
        var result = await tool.SearchExternalDocs("test query");

        // Assert
        var response = JsonSerializer.Deserialize<SearchExternalDocsResponse>(result);
        Assert.NotNull(response);
        Assert.Single(response.Results);
        Assert.Equal("./docs/test.md", response.Results[0].Path);
        Assert.Equal(0.85f, response.Results[0].RelevanceScore);
        Assert.Equal("./docs", response.ExternalDocsPath);
    }

    [Fact]
    public async Task SearchExternalDocs_AppliesLimitParameter()
    {
        // Arrange
        var mockRepository = new Mock<IExternalDocumentRepository>();

        var tool = CreateSearchTools(externalDocRepository: mockRepository.Object);

        // Act
        await tool.SearchExternalDocs("test query", limit: 5);

        // Assert
        mockRepository.Verify(r => r.SearchAsync(
            It.IsAny<ReadOnlyMemory<float>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            5, // Verify limit passed correctly
            It.IsAny<float>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchExternalDocs_AppliesMinRelevanceScore()
    {
        // Arrange
        var mockRepository = new Mock<IExternalDocumentRepository>();

        var tool = CreateSearchTools(externalDocRepository: mockRepository.Object);

        // Act
        await tool.SearchExternalDocs("test query", minRelevanceScore: 0.9f);

        // Assert
        mockRepository.Verify(r => r.SearchAsync(
            It.IsAny<ReadOnlyMemory<float>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            0.9f, // Verify min score passed correctly
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchExternalDocs_UsesConfigOverrideForMinRelevanceScore()
    {
        // Arrange
        var mockProjectContext = CreateActivatedProjectContext(
            hasExternalDocs: true,
            semanticSearchMinRelevanceScore: 0.8f);

        var mockRepository = new Mock<IExternalDocumentRepository>();

        var tool = CreateSearchTools(
            projectContext: mockProjectContext.Object,
            externalDocRepository: mockRepository.Object);

        // Act - parameter says 0.7, but config override is 0.8
        await tool.SearchExternalDocs("test query", minRelevanceScore: 0.7f);

        // Assert - should use config override 0.8
        mockRepository.Verify(r => r.SearchAsync(
            It.IsAny<ReadOnlyMemory<float>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            0.8f,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchExternalDocs_ReturnsError_WhenEmbeddingServiceFails()
    {
        // Arrange
        var mockEmbeddingService = new Mock<IEmbeddingService>();
        mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EmbeddingServiceException("Ollama unavailable"));

        var tool = CreateSearchTools(embeddingService: mockEmbeddingService.Object);

        // Act
        var result = await tool.SearchExternalDocs("test query");

        // Assert
        var response = JsonSerializer.Deserialize<ToolErrorResponse>(result);
        Assert.True(response?.Error);
        Assert.Equal("EMBEDDING_SERVICE_ERROR", response?.Code);
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Tools/SearchExternalDocsToolIntegrationTests.cs
[Trait("Category", "Integration")]
public class SearchExternalDocsToolIntegrationTests : IClassFixture<McpServerFixture>
{
    private readonly McpServerFixture _fixture;

    public SearchExternalDocsToolIntegrationTests(McpServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SearchExternalDocs_WithRealDatabase_ReturnsResults()
    {
        // Arrange - activate project and sync external docs
        await _fixture.ActivateProjectAsync(withExternalDocs: true);

        var tool = _fixture.GetService<SearchTools>();

        // Act
        var result = await tool.SearchExternalDocs("database design");

        // Assert
        var response = JsonSerializer.Deserialize<SearchExternalDocsResponse>(result);
        Assert.NotNull(response);
        Assert.NotEmpty(response.Results);
        Assert.Equal("./docs", response.ExternalDocsPath);
    }
}
```

### Manual Verification

```bash
# 1. Configure external_docs in project config
cat > .csharp-compounding-docs/config.json << 'EOF'
{
  "project_name": "test-project",
  "external_docs": {
    "path": "./docs",
    "include_patterns": ["**/*.md"],
    "exclude_patterns": ["**/node_modules/**"]
  }
}
EOF

# 2. Create test external docs
mkdir -p docs
echo "# API Guide\n\nHow to use our REST API." > docs/api-guide.md
echo "# Setup Guide\n\nHow to set up the project." > docs/setup.md

# 3. Activate project (syncs external docs)
# (via MCP client or test harness)

# 4. Test search_external_docs tool
mcp-cli call search_external_docs \
  --query "REST API" \
  --limit 5 \
  --stdio "dotnet run --project src/CompoundDocs.McpServer"

# Expected output:
# {
#   "results": [
#     {
#       "path": "./docs/api-guide.md",
#       "title": "API Guide",
#       "summary": "How to use our REST API.",
#       "char_count": 42,
#       "relevance_score": 0.89
#     }
#   ],
#   "total_matches": 1,
#   "external_docs_path": "./docs"
# }
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Tools/SearchTools.cs` | Modify | Add `search_external_docs` tool method |
| `src/CompoundDocs.McpServer/Models/SearchExternalDocsResponse.cs` | Create | Response DTO for external search |
| `src/CompoundDocs.McpServer/Models/ExternalSearchResult.cs` | Create | Result DTO for external search |
| `src/CompoundDocs.Common/Services/IProjectContext.cs` | Modify | Add `HasExternalDocs`, `ExternalDocsPath`, `SemanticSearchMinRelevanceScore` |
| `tests/CompoundDocs.Tests/Tools/SearchExternalDocsToolTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.IntegrationTests/Tools/SearchExternalDocsToolIntegrationTests.cs` | Create | Integration tests |

---

## Key Design Decisions

### 1. Read-Only Enforcement

The tool only performs read operations:
- Calls `IExternalDocumentRepository.SearchAsync()` which is inherently read-only
- No create/update/delete operations available through this tool
- External docs modification requires file system changes and re-sync

### 2. Tenant Context Filtering

All searches are scoped to the active tenant:
- `project_name` from `IProjectContext`
- `branch_name` from `IProjectContext`
- `path_hash` from `IProjectContext`

This ensures external doc searches are isolated per project/branch.

### 3. Config Override for min_relevance_score

The `min_relevance_score` parameter can be overridden by project config:
- Parameter default: 0.7
- If `semantic_search.min_relevance_score` is set in project config, use that instead
- Allows project-specific tuning of search sensitivity

### 4. External Docs Path in Response

Including `external_docs_path` in the response:
- Provides context for where results came from
- Helps users understand the document location relative to project root
- Matches spec requirement for source path attribution

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| External docs not synced | Tool depends on sync during project activation; error if empty results |
| Large result sets | `limit` parameter caps results (default: 10) |
| Slow embedding generation | Embedding service has circuit breaker and retry policies |
| Missing external_docs config | Clear error message with configuration instructions |
| Stale external docs index | File watcher (Phase 051+) will keep index updated |
