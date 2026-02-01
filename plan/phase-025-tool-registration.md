# Phase 025: MCP Tool Registration System

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: MCP Server Core
> **Prerequisites**: Phase 021 (MCP Server Host Setup)

---

## Spec References

This phase implements the MCP tool registration infrastructure defined in:

- **spec/mcp-server.md** - [Tool Endpoints](../spec/mcp-server.md#tool-endpoints) (9 MCP tools overview)
- **spec/mcp-server/tools.md** - Complete tool specifications with parameters and responses
- **research/mcp-csharp-sdk-research.md** - Tool registration patterns and SDK usage

---

## Objectives

1. Establish the attribute-based tool registration pattern using `[McpServerToolType]` and `[McpServerTool]`
2. Define the base tool interface pattern for dependency injection and testability
3. Implement tool metadata conventions (name, description, parameters)
4. Create parameter schema definition patterns with validation
5. Set up tool discovery and enumeration infrastructure
6. Implement standard error response handling for tools

---

## Acceptance Criteria

### Tool Registration Infrastructure

- [ ] `[McpServerToolType]` attribute applied to tool container classes
- [ ] `[McpServerTool]` attribute applied to each tool method
- [ ] Tool methods use `[Description]` attributes for schema documentation
- [ ] All 9 tools are registered with the MCP server builder:
  - [ ] `rag_query`
  - [ ] `semantic_search`
  - [ ] `index_document`
  - [ ] `list_doc_types`
  - [ ] `search_external_docs`
  - [ ] `rag_query_external`
  - [ ] `delete_documents`
  - [ ] `update_promotion_level`
  - [ ] `activate_project`

### Tool Interface Pattern

- [ ] Base `ITool` interface defines common tool contract (if needed beyond SDK attributes)
- [ ] Tool classes receive dependencies via constructor injection
- [ ] `CancellationToken` parameter included in all async tool methods
- [ ] Tool methods return strongly-typed result DTOs or JSON-serialized strings

### Parameter Schema Definition

- [ ] Each tool parameter has `[Description]` attribute for LLM schema
- [ ] Optional parameters use nullable types or default values
- [ ] Complex parameters (arrays, enums) are properly typed for JSON schema generation
- [ ] Parameter validation occurs within tool methods before processing

### Tool Discovery

- [ ] `.WithToolsFromAssembly()` discovers all `[McpServerToolType]` classes
- [ ] Alternative: `.WithTools<T>()` registers specific tool classes
- [ ] Tools are enumerable via MCP `tools/list` protocol method
- [ ] Tool schemas are correctly generated from method signatures

### Error Handling

- [ ] Standard error response format implemented per spec
- [ ] `McpProtocolException` thrown for validation errors with appropriate codes
- [ ] Tool-specific error codes defined (e.g., `PROJECT_NOT_ACTIVATED`, `DOCUMENT_NOT_FOUND`)
- [ ] Internal exceptions logged but not exposed to clients

---

## Implementation Notes

### Tool Class Organization

Organize tools into logical groupings by functionality:

```
src/CompoundDocs.McpServer/
├── Tools/
│   ├── RagTools.cs              # rag_query, rag_query_external
│   ├── SearchTools.cs           # semantic_search, search_external_docs
│   ├── IndexTools.cs            # index_document
│   ├── DocTypeTools.cs          # list_doc_types
│   ├── DocumentTools.cs         # delete_documents, update_promotion_level
│   └── ProjectTools.cs          # activate_project
```

### Tool Registration Pattern

Use attribute-based registration with the ModelContextProtocol SDK:

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
public class RagTools
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IRagService _ragService;
    private readonly ILogger<RagTools> _logger;

    public RagTools(
        IDocumentRepository documentRepository,
        IEmbeddingService embeddingService,
        IRagService ragService,
        ILogger<RagTools> logger)
    {
        _documentRepository = documentRepository;
        _embeddingService = embeddingService;
        _ragService = ragService;
        _logger = logger;
    }

    [McpServerTool(Name = "rag_query")]
    [Description("Answer questions using RAG with compounding docs. Returns synthesized response with source metadata.")]
    public async Task<string> RagQuery(
        [Description("Natural language question")] string query,
        [Description("Filter to specific doc-types (default: all)")] string[]? docTypes = null,
        [Description("Maximum documents to use (default: 3)")] int maxSources = 3,
        [Description("Minimum relevance score (default: 0.7)")] float minRelevanceScore = 0.7f,
        [Description("Only return docs at or above this level: standard, important, critical (default: standard)")]
        string minPromotionLevel = "standard",
        [Description("Prepend critical docs to context regardless of relevance (default: true)")]
        bool includeCritical = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("RAG query: {Query}", query);

        // Implementation in later phases
        var result = await _ragService.QueryAsync(
            query,
            docTypes,
            maxSources,
            minRelevanceScore,
            minPromotionLevel,
            includeCritical,
            cancellationToken);

        return JsonSerializer.Serialize(result);
    }
}
```

### Parameter Schema Generation

The SDK automatically generates JSON schemas from method signatures:

| C# Type | JSON Schema Type | Notes |
|---------|------------------|-------|
| `string` | `string` | Required unless nullable |
| `string?` | `string` | Optional |
| `int` | `integer` | Required |
| `int?` | `integer` | Optional |
| `float` | `number` | Required |
| `bool` | `boolean` | Required |
| `string[]` | `array` of `string` | Required unless nullable |
| `enum` | `string` with enum | Use string for LLM compatibility |

### Tool Name Convention

Tool names follow snake_case convention per MCP spec:

```csharp
[McpServerTool(Name = "rag_query")]           // Explicit name
[McpServerTool(Name = "semantic_search")]
[McpServerTool(Name = "index_document")]
[McpServerTool(Name = "list_doc_types")]
[McpServerTool(Name = "search_external_docs")]
[McpServerTool(Name = "rag_query_external")]
[McpServerTool(Name = "delete_documents")]
[McpServerTool(Name = "update_promotion_level")]
[McpServerTool(Name = "activate_project")]
```

### Dependency Injection for Tools

Tool classes receive services via constructor injection. The SDK creates tool instances per-request using the DI container:

```csharp
// In Program.cs or startup
builder.Services.AddScoped<IDocumentRepository, DocumentRepository>();
builder.Services.AddScoped<IEmbeddingService, SemanticKernelEmbeddingService>();
builder.Services.AddScoped<IRagService, OllamaRagService>();
builder.Services.AddScoped<IProjectContext, ProjectContext>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();  // Auto-discovers [McpServerToolType] classes
```

### Standard Error Response Pattern

Implement consistent error handling across all tools:

```csharp
public record ToolErrorResponse(
    bool Error,
    string Code,
    string Message,
    object? Details = null);

// In tool method:
[McpServerTool(Name = "rag_query")]
public async Task<string> RagQuery(string query, CancellationToken ct)
{
    // Check preconditions
    if (!_projectContext.IsActivated)
    {
        return JsonSerializer.Serialize(new ToolErrorResponse(
            Error: true,
            Code: "PROJECT_NOT_ACTIVATED",
            Message: "No project is currently activated. Call activate_project first.",
            Details: new { requiredTool = "activate_project" }));
    }

    try
    {
        // Tool logic...
    }
    catch (EmbeddingServiceException ex)
    {
        _logger.LogError(ex, "Embedding service error during RAG query");
        return JsonSerializer.Serialize(new ToolErrorResponse(
            Error: true,
            Code: "EMBEDDING_SERVICE_ERROR",
            Message: "Failed to generate embeddings. Ensure Ollama is running.",
            Details: new { innerMessage = ex.Message }));
    }
}
```

### Standard Error Codes

Define error codes as constants per spec/mcp-server/tools.md:

```csharp
public static class ToolErrorCodes
{
    public const string ProjectNotActivated = "PROJECT_NOT_ACTIVATED";
    public const string ExternalDocsNotConfigured = "EXTERNAL_DOCS_NOT_CONFIGURED";
    public const string ExternalDocsNotPromotable = "EXTERNAL_DOCS_NOT_PROMOTABLE";
    public const string DocumentNotFound = "DOCUMENT_NOT_FOUND";
    public const string InvalidDocType = "INVALID_DOC_TYPE";
    public const string SchemaValidationFailed = "SCHEMA_VALIDATION_FAILED";
    public const string EmbeddingServiceError = "EMBEDDING_SERVICE_ERROR";
    public const string DatabaseError = "DATABASE_ERROR";
    public const string FileSystemError = "FILE_SYSTEM_ERROR";
}
```

### Tool Response DTOs

Define strongly-typed response objects for each tool:

```csharp
// RagQueryResponse.cs
public record RagQueryResponse(
    string Answer,
    IReadOnlyList<SourceDocument> Sources,
    IReadOnlyList<LinkedDocument>? LinkedDocs = null);

public record SourceDocument(
    string Path,
    string Title,
    int CharCount,
    float RelevanceScore);

public record LinkedDocument(
    string Path,
    string Title,
    int CharCount,
    string LinkedFrom);

// SemanticSearchResponse.cs
public record SemanticSearchResponse(
    IReadOnlyList<SearchResult> Results,
    int TotalMatches);

public record SearchResult(
    string Path,
    string Title,
    string? Summary,
    int CharCount,
    float RelevanceScore,
    string DocType,
    DateOnly Date,
    string PromotionLevel);
```

### Tool Discovery Verification

Verify tools are correctly discovered and registered:

```csharp
// Integration test
[Fact]
public async Task AllToolsAreDiscovered()
{
    var expectedTools = new[]
    {
        "rag_query",
        "semantic_search",
        "index_document",
        "list_doc_types",
        "search_external_docs",
        "rag_query_external",
        "delete_documents",
        "update_promotion_level",
        "activate_project"
    };

    var host = CreateTestHost();
    var mcpServer = host.Services.GetRequiredService<McpServer>();

    var tools = await mcpServer.ListToolsAsync();
    var toolNames = tools.Select(t => t.Name).ToHashSet();

    foreach (var expected in expectedTools)
    {
        Assert.Contains(expected, toolNames);
    }
}
```

---

## Dependencies

### Depends On
- Phase 021: MCP Server Host Setup (provides the hosted service and DI container)

### Blocks
- Phase 026: RAG Query Tool Implementation
- Phase 027: Semantic Search Tool Implementation
- Phase 028: Document Management Tools Implementation
- Phase 029: Project Activation Tool Implementation
- All tool-specific implementation phases

---

## Verification Steps

After completing this phase, verify:

1. **Tool Discovery**: Run the MCP server and verify all 9 tools appear in `tools/list` response
2. **Schema Generation**: Verify tool parameters generate correct JSON schemas
3. **DI Integration**: Verify tool classes receive injected dependencies
4. **Error Handling**: Verify error responses follow the standard format
5. **Logging**: Verify tool invocations are logged to stderr

### Manual Verification

Start the MCP server and use an MCP client to list tools:

```bash
# Using mcp-cli or similar tool
mcp-cli list-tools --stdio "dotnet run --project src/CompoundDocs.McpServer"
```

Expected output should include all 9 tools with their descriptions and parameter schemas.

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `src/CompoundDocs.McpServer/Tools/RagTools.cs` | RAG query tools |
| `src/CompoundDocs.McpServer/Tools/SearchTools.cs` | Semantic search tools |
| `src/CompoundDocs.McpServer/Tools/IndexTools.cs` | Index document tool |
| `src/CompoundDocs.McpServer/Tools/DocTypeTools.cs` | List doc-types tool |
| `src/CompoundDocs.McpServer/Tools/DocumentTools.cs` | Delete and promotion tools |
| `src/CompoundDocs.McpServer/Tools/ProjectTools.cs` | Project activation tool |
| `src/CompoundDocs.McpServer/Models/ToolResponses.cs` | Response DTOs |
| `src/CompoundDocs.McpServer/Models/ToolErrorCodes.cs` | Error code constants |
| `tests/CompoundDocs.Tests/Tools/ToolDiscoveryTests.cs` | Tool registration tests |

### Modified Files

| File | Changes |
|------|---------|
| `src/CompoundDocs.McpServer/Program.cs` | Add `.WithToolsFromAssembly()` to MCP builder |

---

## Notes

- Tool method bodies will be stubbed in this phase; actual implementation occurs in subsequent phases
- The SDK handles JSON-RPC protocol details; tools only need to return results or throw exceptions
- Use `string` for enum parameters instead of C# enums for better LLM compatibility
- All tool methods should be async even if currently synchronous to allow future async operations
- Tool classes can be either static (for stateless tools) or instance-based (for tools needing DI)
