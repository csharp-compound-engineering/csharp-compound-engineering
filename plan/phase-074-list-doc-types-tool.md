# Phase 074: list_doc_types MCP Tool

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: MCP Tools
> **Prerequisites**: Phase 025 (Tool Registration System)

---

## Spec References

This phase implements the `list_doc_types` tool defined in:

- **spec/mcp-server/tools.md** - [4. List Doc-Types Tool](../spec/mcp-server/tools.md#4-list-doc-types-tool) - Tool parameters and response format
- **spec/doc-types.md** - [Overview](../spec/doc-types.md) - Doc-type architecture and common fields
- **spec/doc-types/built-in-types.md** - 5 built-in doc-types with complete schemas
- **spec/doc-types/custom-types.md** - Custom doc-type definition structure

---

## Objectives

1. Implement the `list_doc_types` MCP tool in the `DocTypeTools` class
2. Enumerate all 5 built-in doc-types (problem, insight, codebase, tool, style)
3. Load custom doc-types from project configuration
4. Return schema summary for each doc-type
5. Include document counts per type from the database
6. Ensure project activation is required before tool invocation

---

## Acceptance Criteria

### Tool Registration

- [ ] `list_doc_types` tool registered with `[McpServerTool(Name = "list_doc_types")]` attribute
- [ ] Tool method has `[Description]` attribute for LLM schema documentation
- [ ] Tool class `DocTypeTools` marked with `[McpServerToolType]` attribute
- [ ] Tool discoverable via MCP `tools/list` protocol method

### Parameters

- [ ] Tool accepts no required parameters (parameterless invocation)
- [ ] Tool returns error if no project is activated (`PROJECT_NOT_ACTIVATED`)

### Built-in Doc-Types Enumeration

- [ ] All 5 built-in doc-types enumerated:
  - [ ] `problem` - Problems and solutions (folder: `problems`)
  - [ ] `insight` - Product/project insights (folder: `insights`)
  - [ ] `codebase` - Architecture decisions and code patterns (folder: `codebase`)
  - [ ] `tool` - Library gotchas and dependency knowledge (folder: `tools`)
  - [ ] `style` - Coding conventions and preferences (folder: `styles`)
- [ ] Built-in schemas loaded from plugin directory: `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-{name}/schema.yaml`
- [ ] Each built-in type marked with `schema: "built-in"` in response

### Custom Doc-Types Loading

- [ ] Custom doc-types loaded from `config.json` `custom_doc_types` array
- [ ] Each custom type includes:
  - [ ] `name` - Doc-type identifier (kebab-case)
  - [ ] `description` - Human-readable description
  - [ ] `folder` - Storage folder name
  - [ ] `schema_file` - Path to schema file
- [ ] Custom types marked with `custom: true` in response
- [ ] Schema file path included in response

### Schema Summary

- [ ] Each doc-type response includes:
  - [ ] `name` - Doc-type identifier
  - [ ] `description` - Human-readable description
  - [ ] `folder` - Storage folder relative to `./csharp-compounding-docs/`
  - [ ] `schema` - Either `"built-in"` or path to schema file
  - [ ] `doc_count` - Number of documents of this type in database
  - [ ] `custom` (optional) - `true` if custom type, omitted for built-in

### Response Format

- [ ] Response matches spec format:
```json
{
  "doc_types": [
    {
      "name": "problem",
      "description": "Problems and solutions",
      "folder": "problems",
      "schema": "built-in",
      "doc_count": 12
    },
    {
      "name": "api-contract",
      "description": "API design decisions",
      "folder": "api-contracts",
      "schema": "./schemas/api-contract.schema.yaml",
      "doc_count": 4,
      "custom": true
    }
  ]
}
```

### Error Handling

- [ ] Returns `PROJECT_NOT_ACTIVATED` error if no active project
- [ ] Returns graceful response with `doc_count: 0` if database query fails
- [ ] Logs warning if custom schema file not found
- [ ] Continues enumeration even if individual type fails

---

## Implementation Notes

### Tool Implementation

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
public class DocTypeTools
{
    private readonly IProjectContext _projectContext;
    private readonly IDocTypeService _docTypeService;
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<DocTypeTools> _logger;

    public DocTypeTools(
        IProjectContext projectContext,
        IDocTypeService docTypeService,
        IDocumentRepository documentRepository,
        ILogger<DocTypeTools> logger)
    {
        _projectContext = projectContext;
        _docTypeService = docTypeService;
        _documentRepository = documentRepository;
        _logger = logger;
    }

    [McpServerTool(Name = "list_doc_types")]
    [Description("Return available doc-types for the active project, including both built-in and custom types with their schemas and document counts.")]
    public async Task<string> ListDocTypes(CancellationToken cancellationToken = default)
    {
        // Check project activation
        if (!_projectContext.IsActivated)
        {
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: ToolErrorCodes.ProjectNotActivated,
                Message: "No project is currently activated. Call activate_project first.",
                Details: new { requiredTool = "activate_project" }));
        }

        _logger.LogInformation("Listing doc-types for project: {ProjectName}",
            _projectContext.ProjectName);

        var docTypes = new List<DocTypeInfo>();

        // Enumerate built-in doc-types
        foreach (var builtIn in BuiltInDocTypes.All)
        {
            var docCount = await _documentRepository.CountByDocTypeAsync(
                _projectContext.PathHash,
                _projectContext.BranchName,
                builtIn.Name,
                cancellationToken);

            docTypes.Add(new DocTypeInfo(
                Name: builtIn.Name,
                Description: builtIn.Description,
                Folder: builtIn.Folder,
                Schema: "built-in",
                DocCount: docCount,
                Custom: null));
        }

        // Load custom doc-types from config
        var customTypes = _docTypeService.GetCustomDocTypes();
        foreach (var custom in customTypes)
        {
            var docCount = await _documentRepository.CountByDocTypeAsync(
                _projectContext.PathHash,
                _projectContext.BranchName,
                custom.Name,
                cancellationToken);

            docTypes.Add(new DocTypeInfo(
                Name: custom.Name,
                Description: custom.Description,
                Folder: custom.Folder,
                Schema: custom.SchemaFile,
                DocCount: docCount,
                Custom: true));
        }

        return JsonSerializer.Serialize(new ListDocTypesResponse(docTypes));
    }
}
```

### Built-in Doc-Types Registry

```csharp
public static class BuiltInDocTypes
{
    public static readonly DocTypeDefinition Problem = new(
        Name: "problem",
        Description: "Solved problems with symptoms, root cause, and solution",
        Folder: "problems");

    public static readonly DocTypeDefinition Insight = new(
        Name: "insight",
        Description: "Product, business, or domain learnings",
        Folder: "insights");

    public static readonly DocTypeDefinition Codebase = new(
        Name: "codebase",
        Description: "Architecture decisions, code patterns, and structural knowledge",
        Folder: "codebase");

    public static readonly DocTypeDefinition Tool = new(
        Name: "tool",
        Description: "Library gotchas, configuration nuances, and dependency knowledge",
        Folder: "tools");

    public static readonly DocTypeDefinition Style = new(
        Name: "style",
        Description: "Coding conventions, preferences, and team standards",
        Folder: "styles");

    public static readonly IReadOnlyList<DocTypeDefinition> All = new[]
    {
        Problem, Insight, Codebase, Tool, Style
    };
}

public record DocTypeDefinition(
    string Name,
    string Description,
    string Folder);
```

### Response DTOs

```csharp
public record ListDocTypesResponse(
    IReadOnlyList<DocTypeInfo> DocTypes);

public record DocTypeInfo(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("folder")] string Folder,
    [property: JsonPropertyName("schema")] string Schema,
    [property: JsonPropertyName("doc_count")] int DocCount,
    [property: JsonPropertyName("custom")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? Custom);
```

### Doc-Type Service Interface

```csharp
public interface IDocTypeService
{
    /// <summary>
    /// Gets all built-in doc-type definitions.
    /// </summary>
    IReadOnlyList<DocTypeDefinition> GetBuiltInDocTypes();

    /// <summary>
    /// Gets custom doc-types from the active project configuration.
    /// </summary>
    IReadOnlyList<CustomDocTypeDefinition> GetCustomDocTypes();

    /// <summary>
    /// Validates that a doc-type name is valid (built-in or custom).
    /// </summary>
    bool IsValidDocType(string docTypeName);
}

public record CustomDocTypeDefinition(
    string Name,
    string Description,
    string Folder,
    string SchemaFile,
    string SkillName);
```

### Document Repository Extension

```csharp
public interface IDocumentRepository
{
    // Existing methods...

    /// <summary>
    /// Counts documents by doc-type for a specific tenant context.
    /// </summary>
    Task<int> CountByDocTypeAsync(
        string pathHash,
        string branchName,
        string docType,
        CancellationToken cancellationToken = default);
}

// Implementation
public async Task<int> CountByDocTypeAsync(
    string pathHash,
    string branchName,
    string docType,
    CancellationToken cancellationToken = default)
{
    const string sql = @"
        SELECT COUNT(*)
        FROM documents
        WHERE path_hash = @PathHash
          AND branch_name = @BranchName
          AND doc_type = @DocType";

    using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
    return await connection.ExecuteScalarAsync<int>(sql, new
    {
        PathHash = pathHash,
        BranchName = branchName,
        DocType = docType
    });
}
```

### Custom Doc-Types from Config

Custom doc-types are loaded from the project configuration:

```json
{
  "project_name": "my-project",
  "custom_doc_types": [
    {
      "name": "api-contract",
      "description": "API design decisions and contract specifications",
      "folder": "api-contracts",
      "schema_file": "./schemas/api-contract.schema.yaml",
      "skill_name": "cdocs:api-contract"
    }
  ]
}
```

### JSON Serialization Options

Configure JSON serialization for snake_case property names:

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
};
```

---

## Dependencies

### Depends On
- Phase 025: Tool Registration System (tool infrastructure)
- Phase 010: Project Configuration (config loading)
- Phase 038: Tenant Context (project activation state)
- Phase 042: Compound Document Model (document counting)

### Blocks
- Phase 075+: Tools that need to validate doc-type parameters

---

## Verification Steps

After completing this phase, verify:

1. **Tool Discovery**: `list_doc_types` appears in MCP `tools/list` response
2. **No Parameters**: Tool can be invoked without any parameters
3. **Project Activation Required**: Returns error when no project activated
4. **Built-in Types**: All 5 built-in types included with correct metadata
5. **Custom Types**: Custom types from config included with `custom: true`
6. **Document Counts**: Counts reflect actual documents in database
7. **Response Format**: JSON structure matches spec exactly

### Manual Verification

```bash
# Start MCP server and call list_doc_types
mcp-cli call list_doc_types --stdio "dotnet run --project src/CompoundDocs.McpServer"
```

Expected output:
```json
{
  "doc_types": [
    { "name": "problem", "description": "Solved problems with symptoms, root cause, and solution", "folder": "problems", "schema": "built-in", "doc_count": 0 },
    { "name": "insight", "description": "Product, business, or domain learnings", "folder": "insights", "schema": "built-in", "doc_count": 0 },
    { "name": "codebase", "description": "Architecture decisions, code patterns, and structural knowledge", "folder": "codebase", "schema": "built-in", "doc_count": 0 },
    { "name": "tool", "description": "Library gotchas, configuration nuances, and dependency knowledge", "folder": "tools", "schema": "built-in", "doc_count": 0 },
    { "name": "style", "description": "Coding conventions, preferences, and team standards", "folder": "styles", "schema": "built-in", "doc_count": 0 }
  ]
}
```

### Unit Tests

```csharp
[Fact]
public async Task ListDocTypes_ReturnsAllBuiltInTypes()
{
    // Arrange
    var projectContext = CreateActivatedContext();
    var docTypeService = new DocTypeService(projectContext);
    var documentRepository = CreateMockRepository(docCounts: new()
    {
        ["problem"] = 5,
        ["insight"] = 3
    });
    var tool = new DocTypeTools(projectContext, docTypeService, documentRepository, NullLogger<DocTypeTools>.Instance);

    // Act
    var result = await tool.ListDocTypes();
    var response = JsonSerializer.Deserialize<ListDocTypesResponse>(result);

    // Assert
    Assert.NotNull(response);
    Assert.Equal(5, response.DocTypes.Count);
    Assert.Contains(response.DocTypes, dt => dt.Name == "problem" && dt.DocCount == 5);
    Assert.Contains(response.DocTypes, dt => dt.Name == "insight" && dt.DocCount == 3);
    Assert.All(response.DocTypes.Where(dt => dt.Custom == null),
        dt => Assert.Equal("built-in", dt.Schema));
}

[Fact]
public async Task ListDocTypes_IncludesCustomTypes()
{
    // Arrange
    var config = CreateConfigWithCustomTypes(new[]
    {
        new CustomDocTypeDefinition(
            Name: "api-contract",
            Description: "API design decisions",
            Folder: "api-contracts",
            SchemaFile: "./schemas/api-contract.schema.yaml",
            SkillName: "cdocs:api-contract")
    });
    var projectContext = CreateActivatedContext(config);
    var tool = CreateTool(projectContext);

    // Act
    var result = await tool.ListDocTypes();
    var response = JsonSerializer.Deserialize<ListDocTypesResponse>(result);

    // Assert
    Assert.Equal(6, response.DocTypes.Count); // 5 built-in + 1 custom
    var customType = response.DocTypes.Single(dt => dt.Name == "api-contract");
    Assert.True(customType.Custom);
    Assert.Equal("./schemas/api-contract.schema.yaml", customType.Schema);
}

[Fact]
public async Task ListDocTypes_ReturnsError_WhenProjectNotActivated()
{
    // Arrange
    var projectContext = CreateInactiveContext();
    var tool = CreateTool(projectContext);

    // Act
    var result = await tool.ListDocTypes();
    var error = JsonSerializer.Deserialize<ToolErrorResponse>(result);

    // Assert
    Assert.True(error.Error);
    Assert.Equal("PROJECT_NOT_ACTIVATED", error.Code);
}
```

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `src/CompoundDocs.McpServer/Tools/DocTypeTools.cs` | list_doc_types tool implementation |
| `src/CompoundDocs.McpServer/Services/IDocTypeService.cs` | Doc-type service interface |
| `src/CompoundDocs.McpServer/Services/DocTypeService.cs` | Doc-type service implementation |
| `src/CompoundDocs.McpServer/Models/BuiltInDocTypes.cs` | Built-in doc-type definitions |
| `src/CompoundDocs.McpServer/Models/Responses/ListDocTypesResponse.cs` | Response DTOs |
| `tests/CompoundDocs.Tests/Tools/DocTypeToolsTests.cs` | Unit tests |

### Modified Files

| File | Changes |
|------|---------|
| `src/CompoundDocs.Core/Repositories/IDocumentRepository.cs` | Add `CountByDocTypeAsync` method |
| `src/CompoundDocs.Infrastructure/Repositories/DocumentRepository.cs` | Implement `CountByDocTypeAsync` |
| `src/CompoundDocs.McpServer/Program.cs` | Register `IDocTypeService` in DI |

---

## Notes

- The tool is parameterless by design to provide a simple discovery mechanism
- Document counts are fetched live from the database, not cached
- Custom doc-types require the project to be activated to load config
- Built-in doc-types are always available regardless of project state
- The `custom` property is only included when `true` to keep response compact
- Schema validation for custom types is not performed by this tool (handled elsewhere)
