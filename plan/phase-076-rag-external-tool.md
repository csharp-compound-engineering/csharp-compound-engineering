# Phase 076: rag_query_external MCP Tool

> **Status**: PLANNED
> **Category**: MCP Tools
> **Estimated Effort**: M
> **Prerequisites**: Phase 025 (Tool Registration), Phase 049 (External Document Repository), Phase 032 (RAG Generation Service)

---

## Spec References

- [spec/mcp-server/tools.md - rag_query_external](../spec/mcp-server/tools.md#6-rag-query-external-docs-tool)
- [spec/mcp-server/ollama-integration.md - RAG Generation](../spec/mcp-server/ollama-integration.md#rag-generation-model)
- [spec/configuration.md - external_docs configuration](../spec/configuration.md#external-documentation-optional)

---

## Objectives

1. Implement the `rag_query_external` MCP tool for RAG queries against external documentation
2. Register the tool with proper parameter definitions and descriptions
3. Integrate with `IExternalDocumentRepository` for vector search against external docs collection
4. Use `IRagGenerationService` for response synthesis with external document context
5. Return synthesized answers with source attribution pointing to external doc paths
6. Enforce read-only constraint (no document modification capabilities)
7. Handle `external_docs` configuration validation gracefully

---

## Acceptance Criteria

### Tool Registration

- [ ] Tool registered as `rag_query_external` via `[McpServerTool]` attribute
- [ ] Tool class decorated with `[McpServerToolType]` for SDK discovery
- [ ] Tool description clearly indicates external documentation focus
- [ ] All parameters have `[Description]` attributes for LLM schema generation

### Parameter Definition

- [ ] `query` (string, required): Natural language question parameter
- [ ] `max_sources` (integer, optional): Maximum documents to use (default: 3)
- [ ] `min_relevance_score` (float, optional): Minimum relevance threshold (default: 0.7, overridden by project config)

### External Document Retrieval

- [ ] Queries the `external_documents` collection (separate from compounding docs)
- [ ] Respects tenant isolation (project_name, branch_name, path_hash)
- [ ] Applies `min_relevance_score` from project config `semantic_search.min_relevance_score` if set
- [ ] Retrieves up to `max_sources` documents ordered by relevance

### RAG Synthesis

- [ ] Passes retrieved external documents to `IRagGenerationService`
- [ ] Generates contextual answer using Ollama chat completion
- [ ] System prompt appropriate for external reference material (not institutional knowledge)
- [ ] Does NOT follow linked docs (external docs assumed to be standalone reference material)

### Source Attribution

- [ ] Response includes `sources` array with attribution to external document paths
- [ ] Each source includes: `path`, `title`, `char_count`, `relevance_score`
- [ ] Response includes `external_docs_path` indicating the configured external docs folder
- [ ] Paths are relative to the external docs folder, not absolute

### Error Handling

- [ ] Returns `PROJECT_NOT_ACTIVATED` error if no project is active
- [ ] Returns `EXTERNAL_DOCS_NOT_CONFIGURED` error if `external_docs` not in project config
- [ ] Returns `EMBEDDING_SERVICE_ERROR` on Ollama failures
- [ ] Returns `DATABASE_ERROR` on PostgreSQL failures
- [ ] Logs errors to stderr with correlation IDs

### Read-Only Constraint

- [ ] Tool performs only read operations (search + RAG synthesis)
- [ ] No document creation, modification, or deletion capabilities
- [ ] No promotion level operations (external docs don't participate in promotion)

---

## Implementation Notes

### 1. Tool Class Structure

Add the `rag_query_external` tool to the existing `RagTools` class:

```csharp
// src/CompoundDocs.McpServer/Tools/RagTools.cs
using System.ComponentModel;
using System.Text.Json;
using CompoundDocs.Common.Repositories;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Services;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

public partial class RagTools
{
    private readonly IExternalDocumentRepository _externalDocumentRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IRagGenerationService _ragGenerationService;
    private readonly IProjectContext _projectContext;
    private readonly ILogger<RagTools> _logger;

    public RagTools(
        IExternalDocumentRepository externalDocumentRepository,
        IEmbeddingService embeddingService,
        IRagGenerationService ragGenerationService,
        IProjectContext projectContext,
        ILogger<RagTools> logger)
    {
        _externalDocumentRepository = externalDocumentRepository;
        _embeddingService = embeddingService;
        _ragGenerationService = ragGenerationService;
        _projectContext = projectContext;
        _logger = logger;
    }

    [McpServerTool(Name = "rag_query_external")]
    [Description("Answer questions using external documentation context. Returns synthesized response with source attribution. Requires external_docs to be configured in project config.")]
    public async Task<string> RagQueryExternal(
        [Description("Natural language question")] string query,
        [Description("Maximum documents to use (default: 3)")] int maxSources = 3,
        [Description("Minimum relevance score (default: 0.7, overridden by project config)")] float minRelevanceScore = 0.7f,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "RAG query external: {Query} (maxSources={MaxSources}, minRelevance={MinRelevance})",
            query, maxSources, minRelevanceScore);

        // Validate project is activated
        if (!_projectContext.IsActivated)
        {
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: ToolErrorCodes.ProjectNotActivated,
                Message: "No project is currently activated. Call activate_project first.",
                Details: new { requiredTool = "activate_project" }));
        }

        // Validate external_docs is configured
        if (!_projectContext.HasExternalDocs)
        {
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: ToolErrorCodes.ExternalDocsNotConfigured,
                Message: "External documentation is not configured for this project. Add 'external_docs' section to your project config.",
                Details: new { configPath = _projectContext.ConfigPath }));
        }

        try
        {
            // Apply project config override for min_relevance_score if configured
            var effectiveMinRelevance = _projectContext.ExternalDocsMinRelevanceScore ?? minRelevanceScore;

            // Generate embedding for query
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

            // Search external documents
            var searchResults = await _externalDocumentRepository.SearchAsync(
                queryEmbedding,
                _projectContext.ProjectName,
                _projectContext.BranchName,
                _projectContext.PathHash,
                limit: maxSources,
                minRelevanceScore: effectiveMinRelevance,
                cancellationToken: cancellationToken);

            if (searchResults.Count == 0)
            {
                _logger.LogInformation("No relevant external documents found for query");

                return JsonSerializer.Serialize(new RagQueryExternalResponse(
                    Answer: "No relevant external documentation was found for this query. Try rephrasing your question or checking if the external_docs folder contains relevant content.",
                    Sources: Array.Empty<ExternalSourceAttribution>(),
                    ExternalDocsPath: _projectContext.ExternalDocsPath));
            }

            // Convert search results to retrieved documents for RAG
            var retrievedDocs = searchResults.Select(r => new RetrievedDocument(
                Path: r.Document.RelativePath,
                Title: r.Document.Title,
                Content: await GetDocumentContentAsync(r.Document, cancellationToken),
                CharCount: r.Document.CharCount,
                RelevanceScore: r.RelevanceScore,
                DocType: "external", // External docs don't have doc types
                PromotionLevel: "standard", // External docs don't have promotion levels
                Date: null
            )).ToList();

            // Prepare options for external docs (no linked doc following)
            var ragOptions = new RagGenerationOptions
            {
                IncludeLinkedDocs = false, // External docs are standalone
                SystemPromptOverride = ExternalDocsSystemPrompt
            };

            // Generate RAG response
            var ragResponse = await _ragGenerationService.GenerateResponseAsync(
                query,
                retrievedDocs,
                ragOptions,
                cancellationToken);

            // Build response with source attribution
            var response = new RagQueryExternalResponse(
                Answer: ragResponse.Answer,
                Sources: searchResults.Select(r => new ExternalSourceAttribution(
                    Path: r.Document.RelativePath,
                    Title: r.Document.Title,
                    CharCount: r.Document.CharCount,
                    RelevanceScore: r.RelevanceScore
                )).ToList(),
                ExternalDocsPath: _projectContext.ExternalDocsPath);

            _logger.LogInformation(
                "RAG query external completed with {SourceCount} sources in {Duration}ms",
                response.Sources.Count,
                ragResponse.ProcessingTime.TotalMilliseconds);

            return JsonSerializer.Serialize(response);
        }
        catch (EmbeddingServiceException ex)
        {
            _logger.LogError(ex, "Embedding service error during external RAG query");
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: ToolErrorCodes.EmbeddingServiceError,
                Message: "Failed to generate embeddings. Ensure Ollama is running.",
                Details: new { innerMessage = ex.Message }));
        }
        catch (Exception ex) when (ex.Message.Contains("Npgsql") || ex.Message.Contains("PostgreSQL"))
        {
            _logger.LogError(ex, "Database error during external RAG query");
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: ToolErrorCodes.DatabaseError,
                Message: "Database error occurred while searching external documents.",
                Details: new { innerMessage = ex.Message }));
        }
    }

    private async Task<string> GetDocumentContentAsync(
        ExternalDocument document,
        CancellationToken cancellationToken)
    {
        // Read content from file system based on external_docs path + relative path
        var fullPath = Path.Combine(_projectContext.ExternalDocsPath, document.RelativePath);

        if (!File.Exists(fullPath))
        {
            _logger.LogWarning(
                "External document file not found: {Path}. Using summary as fallback.",
                fullPath);
            return document.Summary ?? document.Title;
        }

        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    private const string ExternalDocsSystemPrompt = """
        You are a helpful assistant that answers questions based on external project documentation.

        Guidelines:
        - Always cite your sources by referencing the document paths when using information from them
        - This is external reference documentation, not internal institutional knowledge
        - If the documentation doesn't contain enough information to answer the question, say so clearly
        - Focus on accuracy over completeness - only state what is supported by the documentation
        - Use code examples from the documentation when relevant and helpful
        - If documents have different or conflicting information, acknowledge this

        Format your citations like: (source: path/to/document.md)
        """;
}
```

### 2. Response Models

Create response models for the external RAG query:

```csharp
// src/CompoundDocs.McpServer/Models/RagQueryExternalResponse.cs
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Response from rag_query_external tool.
/// </summary>
public record RagQueryExternalResponse(
    string Answer,
    IReadOnlyList<ExternalSourceAttribution> Sources,
    string ExternalDocsPath);

/// <summary>
/// Source attribution for an external document.
/// </summary>
public record ExternalSourceAttribution(
    string Path,
    string Title,
    int CharCount,
    float RelevanceScore);
```

### 3. Project Context Extensions

Ensure `IProjectContext` exposes external docs configuration:

```csharp
// src/CompoundDocs.McpServer/Services/IProjectContext.cs
public interface IProjectContext
{
    bool IsActivated { get; }
    string ProjectName { get; }
    string BranchName { get; }
    string PathHash { get; }
    string ConfigPath { get; }

    // External docs configuration
    bool HasExternalDocs { get; }
    string? ExternalDocsPath { get; }
    float? ExternalDocsMinRelevanceScore { get; }
}
```

### 4. Key Differences from rag_query

| Aspect | `rag_query` | `rag_query_external` |
|--------|-------------|----------------------|
| Collection | `documents` | `external_documents` |
| Linked docs | Yes, follows links | No (standalone reference material) |
| Promotion levels | Supports `min_promotion_level`, `include_critical` | Not applicable |
| Doc types | Supports `doc_types` filter | Not applicable |
| System prompt | Institutional knowledge focus | Reference documentation focus |
| Response format | Includes `linked_docs` | Only `sources` and `external_docs_path` |

### 5. JSON Response Format

Per the spec, the response format is:

```json
{
  "answer": "The API authentication uses JWT tokens with...",
  "sources": [
    {
      "path": "./docs/api/authentication.md",
      "title": "API Authentication Guide",
      "char_count": 3421,
      "relevance_score": 0.89
    },
    {
      "path": "./docs/security/jwt-setup.md",
      "title": "JWT Configuration",
      "char_count": 1876,
      "relevance_score": 0.76
    }
  ],
  "external_docs_path": "./docs"
}
```

### 6. Error Response Format

Standard error format per spec:

```json
{
  "error": true,
  "code": "EXTERNAL_DOCS_NOT_CONFIGURED",
  "message": "External documentation is not configured for this project. Add 'external_docs' section to your project config.",
  "details": {
    "configPath": "./.csharp-compounding-docs/config.json"
  }
}
```

---

## Dependencies

### Depends On

- **Phase 025**: Tool Registration System - Provides `[McpServerTool]` attribute infrastructure
- **Phase 049**: External Document Repository - Provides `IExternalDocumentRepository` for external doc queries
- **Phase 032**: RAG Generation Service - Provides `IRagGenerationService` for synthesis

### Blocks

- Integration testing phases for external docs workflow
- End-to-end testing with external documentation

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Tools/RagQueryExternalToolTests.cs
public class RagQueryExternalToolTests
{
    private readonly Mock<IExternalDocumentRepository> _mockExternalRepo;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IRagGenerationService> _mockRagService;
    private readonly Mock<IProjectContext> _mockProjectContext;
    private readonly RagTools _ragTools;

    public RagQueryExternalToolTests()
    {
        _mockExternalRepo = new Mock<IExternalDocumentRepository>();
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockRagService = new Mock<IRagGenerationService>();
        _mockProjectContext = new Mock<IProjectContext>();

        _ragTools = new RagTools(
            _mockExternalRepo.Object,
            _mockEmbeddingService.Object,
            _mockRagService.Object,
            _mockProjectContext.Object,
            Mock.Of<ILogger<RagTools>>());
    }

    [Fact]
    public async Task RagQueryExternal_WhenProjectNotActivated_ReturnsError()
    {
        // Arrange
        _mockProjectContext.Setup(p => p.IsActivated).Returns(false);

        // Act
        var result = await _ragTools.RagQueryExternal("What is the API?");
        var response = JsonSerializer.Deserialize<ToolErrorResponse>(result);

        // Assert
        Assert.True(response.Error);
        Assert.Equal("PROJECT_NOT_ACTIVATED", response.Code);
    }

    [Fact]
    public async Task RagQueryExternal_WhenExternalDocsNotConfigured_ReturnsError()
    {
        // Arrange
        _mockProjectContext.Setup(p => p.IsActivated).Returns(true);
        _mockProjectContext.Setup(p => p.HasExternalDocs).Returns(false);

        // Act
        var result = await _ragTools.RagQueryExternal("What is the API?");
        var response = JsonSerializer.Deserialize<ToolErrorResponse>(result);

        // Assert
        Assert.True(response.Error);
        Assert.Equal("EXTERNAL_DOCS_NOT_CONFIGURED", response.Code);
    }

    [Fact]
    public async Task RagQueryExternal_WithValidQuery_ReturnsRagResponse()
    {
        // Arrange
        SetupValidProjectContext();

        var queryEmbedding = new float[1024];
        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryEmbedding);

        var searchResults = new List<ExternalDocumentSearchResult>
        {
            new(CreateTestExternalDocument("api.md", "API Guide"), 0.92f)
        };
        _mockExternalRepo
            .Setup(r => r.SearchAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        var ragResponse = new RagResponse(
            Answer: "The API uses JWT authentication.",
            Sources: new List<SourceAttribution>(),
            LinkedDocs: new List<LinkedDocumentAttribution>(),
            ProcessingTime: TimeSpan.FromMilliseconds(100));
        _mockRagService
            .Setup(r => r.GenerateResponseAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RetrievedDocument>>(),
                It.IsAny<RagGenerationOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ragResponse);

        // Act
        var result = await _ragTools.RagQueryExternal("How does auth work?");
        var response = JsonSerializer.Deserialize<RagQueryExternalResponse>(result);

        // Assert
        Assert.Equal("The API uses JWT authentication.", response.Answer);
        Assert.Single(response.Sources);
        Assert.Equal("./docs", response.ExternalDocsPath);
    }

    [Fact]
    public async Task RagQueryExternal_WithNoResults_ReturnsHelpfulMessage()
    {
        // Arrange
        SetupValidProjectContext();

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1024]);

        _mockExternalRepo
            .Setup(r => r.SearchAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExternalDocumentSearchResult>());

        // Act
        var result = await _ragTools.RagQueryExternal("Unknown topic");
        var response = JsonSerializer.Deserialize<RagQueryExternalResponse>(result);

        // Assert
        Assert.Contains("No relevant external documentation", response.Answer);
        Assert.Empty(response.Sources);
    }

    [Fact]
    public async Task RagQueryExternal_UsesProjectConfigMinRelevanceScore()
    {
        // Arrange
        _mockProjectContext.Setup(p => p.IsActivated).Returns(true);
        _mockProjectContext.Setup(p => p.HasExternalDocs).Returns(true);
        _mockProjectContext.Setup(p => p.ExternalDocsMinRelevanceScore).Returns(0.85f);
        _mockProjectContext.Setup(p => p.ProjectName).Returns("test");
        _mockProjectContext.Setup(p => p.BranchName).Returns("main");
        _mockProjectContext.Setup(p => p.PathHash).Returns("abc123");
        _mockProjectContext.Setup(p => p.ExternalDocsPath).Returns("./docs");

        _mockEmbeddingService
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[1024]);

        _mockExternalRepo
            .Setup(r => r.SearchAsync(
                It.IsAny<ReadOnlyMemory<float>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                0.85f, // Should use project config value
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExternalDocumentSearchResult>());

        // Act
        await _ragTools.RagQueryExternal("Test query", minRelevanceScore: 0.5f);

        // Assert - verify the project config value was used, not the parameter
        _mockExternalRepo.Verify(r => r.SearchAsync(
            It.IsAny<ReadOnlyMemory<float>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            0.85f,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RagQueryExternal_DoesNotFollowLinkedDocs()
    {
        // Arrange
        SetupValidProjectContext();
        SetupSuccessfulSearch();

        RagGenerationOptions capturedOptions = null;
        _mockRagService
            .Setup(r => r.GenerateResponseAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RetrievedDocument>>(),
                It.IsAny<RagGenerationOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<RetrievedDocument>, RagGenerationOptions, CancellationToken>(
                (q, d, o, c) => capturedOptions = o)
            .ReturnsAsync(CreateTestRagResponse());

        // Act
        await _ragTools.RagQueryExternal("Test query");

        // Assert
        Assert.NotNull(capturedOptions);
        Assert.False(capturedOptions.IncludeLinkedDocs);
    }

    private void SetupValidProjectContext()
    {
        _mockProjectContext.Setup(p => p.IsActivated).Returns(true);
        _mockProjectContext.Setup(p => p.HasExternalDocs).Returns(true);
        _mockProjectContext.Setup(p => p.ProjectName).Returns("test-project");
        _mockProjectContext.Setup(p => p.BranchName).Returns("main");
        _mockProjectContext.Setup(p => p.PathHash).Returns("abc123");
        _mockProjectContext.Setup(p => p.ExternalDocsPath).Returns("./docs");
        _mockProjectContext.Setup(p => p.ExternalDocsMinRelevanceScore).Returns((float?)null);
    }

    private ExternalDocument CreateTestExternalDocument(string path, string title)
    {
        return new ExternalDocument
        {
            Id = Guid.NewGuid().ToString(),
            ProjectName = "test-project",
            BranchName = "main",
            PathHash = "abc123",
            RelativePath = path,
            Title = title,
            CharCount = 1000
        };
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Tools/RagQueryExternalIntegrationTests.cs
[Trait("Category", "Integration")]
public class RagQueryExternalIntegrationTests : IClassFixture<McpServerFixture>
{
    private readonly McpServerFixture _fixture;

    public RagQueryExternalIntegrationTests(McpServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RagQueryExternal_WithRealExternalDocs_ReturnsRelevantAnswer()
    {
        // Arrange
        await _fixture.ActivateProjectAsync("test-project", withExternalDocs: true);

        // Create test external docs
        await _fixture.CreateExternalDocAsync("api/auth.md", """
            # Authentication API

            The API uses JWT tokens for authentication.
            Include the token in the Authorization header as Bearer <token>.
            """);

        // Act
        var result = await _fixture.CallToolAsync("rag_query_external", new
        {
            query = "How do I authenticate with the API?"
        });

        // Assert
        Assert.Contains("JWT", result.Answer);
        Assert.Single(result.Sources);
        Assert.Equal("api/auth.md", result.Sources[0].Path);
    }

    [Fact]
    public async Task RagQueryExternal_RespectsMaxSources()
    {
        // Arrange
        await _fixture.ActivateProjectAsync("test-project", withExternalDocs: true);

        // Create multiple external docs
        for (int i = 1; i <= 10; i++)
        {
            await _fixture.CreateExternalDocAsync($"doc{i}.md", $"# Document {i}\n\nContent about topic.");
        }

        // Act
        var result = await _fixture.CallToolAsync("rag_query_external", new
        {
            query = "Tell me about the topic",
            max_sources = 3
        });

        // Assert
        Assert.True(result.Sources.Count <= 3);
    }
}
```

### Manual Verification

```bash
# 1. Start the MCP server
dotnet run --project src/CompoundDocs.McpServer/

# 2. Activate a project with external_docs configured
# (via MCP client or test harness)

# 3. Call rag_query_external
# Expected: Synthesized answer with source attribution

# 4. Verify error handling when external_docs not configured
# (activate project without external_docs)
# Expected: EXTERNAL_DOCS_NOT_CONFIGURED error
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Tools/RagTools.cs` | Modify | Add `rag_query_external` tool method |
| `src/CompoundDocs.McpServer/Models/RagQueryExternalResponse.cs` | Create | Response model for external RAG |
| `src/CompoundDocs.McpServer/Models/ExternalSourceAttribution.cs` | Create | Source attribution model |
| `src/CompoundDocs.McpServer/Services/IProjectContext.cs` | Modify | Add external docs properties |
| `tests/CompoundDocs.Tests/Tools/RagQueryExternalToolTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.IntegrationTests/Tools/RagQueryExternalIntegrationTests.cs` | Create | Integration tests |

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| No linked doc following | External docs are assumed to be standalone reference material, not interconnected knowledge |
| Separate system prompt | External docs are reference material, requiring different context framing than institutional knowledge |
| Project config override for min_relevance | Allows projects to tune relevance thresholds for their specific external docs quality |
| Read content from filesystem | External docs content not stored in DB embedding, retrieved fresh for RAG context |
| Relative paths in response | Paths relative to external_docs folder for clarity and portability |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| External docs folder moved | Validate path exists at query time, return clear error |
| Large external documents | Chunking handled by repository; RAG service manages context window |
| File read errors | Fallback to summary/title if file not readable |
| Slow embedding generation | Use same resilience patterns as rag_query (circuit breaker, timeout) |
| Confusion with rag_query | Clear tool descriptions and documentation distinguish the tools |

---

## Notes

- The `rag_query_external` tool intentionally has fewer parameters than `rag_query` because external docs don't have doc types, promotion levels, or linked doc traversal
- The system prompt emphasizes that this is external reference documentation to set appropriate expectations for the LLM
- Source attribution uses relative paths within the external_docs folder for clarity
- The tool is read-only by design - external docs can only be modified by changing files in the external_docs folder and re-syncing
