# Phase 098: /cdocs:promote Utility Skill

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: Skills System
> **Prerequisites**: Phase 081 (Semantic Search Tool), Phase 078 (Update Document Tool)

---

## Spec References

This phase implements the `/cdocs:promote` skill defined in:

- **spec/skills/utility-skills.md** - [/cdocs:promote](../spec/skills/utility-skills.md#cdocspromote) - Skill behavior and MCP tool invocation
- **spec/doc-types/promotion.md** - [Document Promotion Levels](../spec/doc-types/promotion.md) - Promotion workflow and level definitions

---

## Objectives

1. Create SKILL.md content for the `/cdocs:promote` utility skill
2. Implement promotion level changes via MCP `update_promotion_level` tool
3. Update both YAML frontmatter in the file and database record atomically
4. Support all three promotion levels: standard, important, critical
5. Restrict promotion to local docs only (external docs are read-only)
6. Provide decision menu after successful promotion

---

## Acceptance Criteria

### SKILL.md Structure

- [ ] SKILL.md created at `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-promote/SKILL.md`
- [ ] YAML frontmatter with skill metadata:
  - [ ] `name: cdocs:promote`
  - [ ] `description: Promote or demote a document's visibility level`
  - [ ] `invocation: manual`
  - [ ] `mcp_tools: ["update_promotion_level"]`
- [ ] Clear instructions for promotion workflow
- [ ] Decision menu templates included
- [ ] Error handling guidance for external docs

### Skill Invocation Workflow

- [ ] Step 1: If document path not provided, prompt user to search or provide path
- [ ] Step 2: Display current document metadata including current promotion level
- [ ] Step 3: Present promotion options (standard, important, critical)
- [ ] Step 4: Confirm the change with user
- [ ] Step 5: Call MCP `update_promotion_level` tool
- [ ] Step 6: Report success with decision menu

### MCP Tool: update_promotion_level

- [ ] Tool registered with `[McpServerTool(Name = "update_promotion_level")]` attribute
- [ ] Tool class `PromotionTools` marked with `[McpServerToolType]` attribute
- [ ] Parameters validated per spec:

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `document_path` | string | Yes | Relative path to document |
| `promotion_level` | enum | Yes | `standard`, `important`, or `critical` |

### Promotion Level Enum

- [ ] `PromotionLevel` enum defined with three values:
  - [ ] `Standard` - Default visibility, retrieved via normal RAG/search
  - [ ] `Important` - Higher relevance boost, surfaces more readily
  - [ ] `Critical` - Required reading, must be surfaced before code generation

### YAML Frontmatter Update

- [ ] Read existing document file
- [ ] Parse YAML frontmatter using established frontmatter parser
- [ ] Update or add `promotion_level` field
- [ ] Add promotion audit comment: `# Promoted to {level} on {date}: {reason}`
- [ ] Preserve all other frontmatter fields
- [ ] Write updated content back to file
- [ ] Preserve file encoding (UTF-8)

### Database Record Update

- [ ] Update `promotion_level` in `CompoundDocument` record
- [ ] Update `promotion_level` in all associated `DocumentChunk` records
- [ ] Both updates occur in same transaction
- [ ] Content hash not changed (only metadata update)

### External Document Restriction

- [ ] Detect if document is external (from `ExternalDocument` table)
- [ ] Return error for external documents: `EXTERNAL_DOC_READONLY`
- [ ] Clear error message: "External documents cannot be promoted (read-only)"
- [ ] Only documents in `./csharp-compounding-docs/` can be promoted

### Response Format

- [ ] Success response matches spec:
```json
{
  "status": "updated",
  "document_path": "problems/n-plus-one-query-20250120.md",
  "previous_level": "standard",
  "new_level": "critical"
}
```

- [ ] Error response for external docs:
```json
{
  "error": true,
  "code": "EXTERNAL_DOC_READONLY",
  "message": "External documents cannot be promoted (read-only)",
  "details": {
    "document_path": "external/some-doc.md",
    "source": "external"
  }
}
```

### Decision Menu After Promotion

- [ ] SKILL.md includes decision menu template:
```
Document promoted to [level]

File: ./csharp-compounding-docs/[path]

What's next?
1. Continue workflow
2. Promote another document
3. View document
4. Other
```

### Error Handling

- [ ] `PROJECT_NOT_ACTIVATED` - No active project
- [ ] `DOCUMENT_NOT_FOUND` - Document path does not exist
- [ ] `EXTERNAL_DOC_READONLY` - Cannot promote external documents
- [ ] `INVALID_PROMOTION_LEVEL` - Level not one of standard/important/critical
- [ ] `FILE_WRITE_FAILED` - Could not update frontmatter in file
- [ ] `DATABASE_UPDATE_FAILED` - Could not update database record

---

## Implementation Notes

### SKILL.md Content

```markdown
---
name: cdocs:promote
description: Promote or demote a document's visibility level
invocation: manual
mcp_tools:
  - update_promotion_level
---

# /cdocs:promote

Promote or demote a document's visibility level to control how readily it surfaces in RAG queries and search results.

## Promotion Levels

| Level | Description |
|-------|-------------|
| **standard** | Default visibility. Retrieved via normal RAG/search |
| **important** | Higher relevance boost. Surfaces more readily in related queries |
| **critical** | Required reading. Must be surfaced before code generation in related areas |

## Workflow

1. If document path not provided, ask user to search or provide path
2. Display current document metadata:
   - Title
   - Doc-type
   - Current promotion level
   - Last modified date
3. Present promotion options:
   - Standard (default visibility)
   - Important (higher relevance boost)
   - Critical (required reading)
4. Confirm the change
5. Call MCP tool: `update_promotion_level`
6. Report success with decision menu

## MCP Tool Invocation

```json
{
  "tool": "update_promotion_level",
  "arguments": {
    "document_path": "problems/n-plus-one-query-20250120.md",
    "promotion_level": "critical"
  }
}
```

## Restrictions

- **External docs cannot be promoted** - They are read-only
- Only documents in `./csharp-compounding-docs/` can be promoted

## Decision Menu

After successful promotion:

```
Document promoted to [level]

File: ./csharp-compounding-docs/[path]

What's next?
1. Continue workflow
2. Promote another document
3. View document
4. Other
```

## When to Promote

### Promote to Important

- The knowledge prevents common mistakes
- The information is frequently referenced
- Understanding this is necessary for working in a specific area
- The solution saved significant debugging time

### Promote to Critical

- The same mistake has been made 3+ times across different contexts
- The solution is non-obvious but must be followed every time
- Ignoring this knowledge leads to significant rework
- This represents a foundational pattern for the codebase
- Getting this wrong could cause production issues

### Do NOT Over-Promote

**If everything is critical, nothing is.** Reserve critical for truly essential patterns.
```

### Tool Implementation

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
public class PromotionTools
{
    private readonly IProjectContext _projectContext;
    private readonly IDocumentRepository _documentRepository;
    private readonly IExternalDocumentRepository _externalRepository;
    private readonly IFrontmatterService _frontmatterService;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<PromotionTools> _logger;

    public PromotionTools(
        IProjectContext projectContext,
        IDocumentRepository documentRepository,
        IExternalDocumentRepository externalRepository,
        IFrontmatterService frontmatterService,
        IFileSystem fileSystem,
        ILogger<PromotionTools> logger)
    {
        _projectContext = projectContext;
        _documentRepository = documentRepository;
        _externalRepository = externalRepository;
        _frontmatterService = frontmatterService;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    [McpServerTool(Name = "update_promotion_level")]
    [Description("Update the promotion level of a compound document. Promotion controls visibility in RAG queries. Only local docs can be promoted (not external).")]
    public async Task<string> UpdatePromotionLevel(
        [Description("Relative path to the document within csharp-compounding-docs folder")]
        string document_path,
        [Description("Target promotion level: 'standard', 'important', or 'critical'")]
        string promotion_level,
        CancellationToken cancellationToken = default)
    {
        // Validate project activation
        if (!_projectContext.IsActivated)
        {
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: ToolErrorCodes.ProjectNotActivated,
                Message: "No project is currently activated. Call activate_project first."));
        }

        // Validate promotion level
        if (!Enum.TryParse<PromotionLevel>(promotion_level, ignoreCase: true, out var newLevel))
        {
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: "INVALID_PROMOTION_LEVEL",
                Message: $"Invalid promotion level: '{promotion_level}'. Must be 'standard', 'important', or 'critical'."));
        }

        // Check if document is external (read-only)
        var isExternal = await _externalRepository.ExistsAsync(
            _projectContext.PathHash,
            document_path,
            cancellationToken);

        if (isExternal)
        {
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: "EXTERNAL_DOC_READONLY",
                Message: "External documents cannot be promoted (read-only)",
                Details: new { document_path, source = "external" }));
        }

        // Get current document
        var document = await _documentRepository.GetByPathAsync(
            _projectContext.PathHash,
            _projectContext.BranchName,
            document_path,
            cancellationToken);

        if (document == null)
        {
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: "DOCUMENT_NOT_FOUND",
                Message: $"Document not found: {document_path}"));
        }

        var previousLevel = document.PromotionLevel;

        // Update file frontmatter
        var fullPath = Path.Combine(
            _projectContext.CompoundDocsPath,
            document_path);

        try
        {
            await UpdateFileFrontmatterAsync(fullPath, newLevel, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update frontmatter for {Path}", document_path);
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: "FILE_WRITE_FAILED",
                Message: $"Could not update frontmatter in file: {ex.Message}"));
        }

        // Update database record (document and chunks)
        try
        {
            await _documentRepository.UpdatePromotionLevelAsync(
                document.Id,
                newLevel,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update database for {Path}", document_path);
            // Attempt to rollback file change
            await UpdateFileFrontmatterAsync(fullPath, previousLevel, cancellationToken);
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: "DATABASE_UPDATE_FAILED",
                Message: $"Could not update database record: {ex.Message}"));
        }

        _logger.LogInformation(
            "Promoted document {Path} from {Previous} to {New}",
            document_path, previousLevel, newLevel);

        return JsonSerializer.Serialize(new UpdatePromotionLevelResponse(
            Status: "updated",
            DocumentPath: document_path,
            PreviousLevel: previousLevel.ToString().ToLowerInvariant(),
            NewLevel: newLevel.ToString().ToLowerInvariant()));
    }

    private async Task UpdateFileFrontmatterAsync(
        string fullPath,
        PromotionLevel level,
        CancellationToken cancellationToken)
    {
        var content = await _fileSystem.ReadAllTextAsync(fullPath, cancellationToken);
        var (frontmatter, body) = _frontmatterService.Parse(content);

        // Update promotion level
        frontmatter["promotion_level"] = level.ToString().ToLowerInvariant();

        // Add audit comment
        var auditComment = $"# Promoted to {level.ToString().ToLowerInvariant()} on {DateTime.UtcNow:yyyy-MM-dd}";

        var updatedContent = _frontmatterService.Serialize(frontmatter, body, auditComment);
        await _fileSystem.WriteAllTextAsync(fullPath, updatedContent, cancellationToken);
    }
}
```

### Promotion Level Enum

```csharp
public enum PromotionLevel
{
    /// <summary>
    /// Default visibility. Retrieved via normal RAG/search.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// Higher relevance boost. Surfaces more readily in related queries.
    /// </summary>
    Important = 1,

    /// <summary>
    /// Required reading. Must be surfaced before code generation in related areas.
    /// </summary>
    Critical = 2
}
```

### Response DTOs

```csharp
public record UpdatePromotionLevelResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("document_path")] string DocumentPath,
    [property: JsonPropertyName("previous_level")] string PreviousLevel,
    [property: JsonPropertyName("new_level")] string NewLevel);
```

### Repository Extension

```csharp
public interface IDocumentRepository
{
    // Existing methods...

    /// <summary>
    /// Updates the promotion level for a document and all its chunks.
    /// </summary>
    Task UpdatePromotionLevelAsync(
        Guid documentId,
        PromotionLevel level,
        CancellationToken cancellationToken = default);
}

// Implementation
public async Task UpdatePromotionLevelAsync(
    Guid documentId,
    PromotionLevel level,
    CancellationToken cancellationToken = default)
{
    await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
    await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

    try
    {
        // Update document
        const string updateDocSql = @"
            UPDATE documents
            SET promotion_level = @Level, updated_at = @UpdatedAt
            WHERE id = @DocumentId";

        await connection.ExecuteAsync(updateDocSql, new
        {
            DocumentId = documentId,
            Level = level.ToString().ToLowerInvariant(),
            UpdatedAt = DateTime.UtcNow
        }, transaction);

        // Update all chunks for this document
        const string updateChunksSql = @"
            UPDATE document_chunks
            SET promotion_level = @Level
            WHERE document_id = @DocumentId";

        await connection.ExecuteAsync(updateChunksSql, new
        {
            DocumentId = documentId,
            Level = level.ToString().ToLowerInvariant()
        }, transaction);

        await transaction.CommitAsync(cancellationToken);
    }
    catch
    {
        await transaction.RollbackAsync(cancellationToken);
        throw;
    }
}
```

### Frontmatter Service Interface

```csharp
public interface IFrontmatterService
{
    /// <summary>
    /// Parses YAML frontmatter from markdown content.
    /// </summary>
    (Dictionary<string, object> Frontmatter, string Body) Parse(string content);

    /// <summary>
    /// Serializes frontmatter and body back to markdown content.
    /// </summary>
    /// <param name="frontmatter">The frontmatter dictionary</param>
    /// <param name="body">The markdown body</param>
    /// <param name="auditComment">Optional comment to add after frontmatter</param>
    string Serialize(
        Dictionary<string, object> frontmatter,
        string body,
        string? auditComment = null);
}
```

---

## Dependencies

### Depends On
- Phase 081: Semantic Search Tool (search for documents to promote)
- Phase 078: Update Document Tool (document update patterns)
- Phase 042: Compound Document Model (document records)
- Phase 043: Document Chunk Model (chunk promotion level sync)
- Phase 044: External Document Model (external doc detection)
- Phase 059: Frontmatter Parsing (YAML parsing)
- Phase 038: Tenant Context (project activation)

### Blocks
- Phase 099+: Skills that reference promotion state

---

## Verification Steps

After completing this phase, verify:

1. **Skill File Exists**: SKILL.md at correct path with proper frontmatter
2. **Tool Discovery**: `update_promotion_level` appears in MCP `tools/list` response
3. **Parameter Validation**: Invalid promotion level returns appropriate error
4. **External Doc Rejection**: External documents return `EXTERNAL_DOC_READONLY` error
5. **File Update**: YAML frontmatter updated with new promotion level
6. **Database Update**: Both document and chunks updated in transaction
7. **Audit Trail**: Promotion comment added to frontmatter
8. **Response Format**: JSON structure matches spec exactly

### Manual Verification

```bash
# Start MCP server and promote a document
mcp-cli call update_promotion_level \
  --param document_path="problems/database-timeout-20250115.md" \
  --param promotion_level="critical" \
  --stdio "dotnet run --project src/CompoundDocs.McpServer"
```

Expected output:
```json
{
  "status": "updated",
  "document_path": "problems/database-timeout-20250115.md",
  "previous_level": "standard",
  "new_level": "critical"
}
```

### Verify File Update

After promotion, the document should have updated frontmatter:
```yaml
---
doc_type: problem
title: Database connection timeout
promotion_level: critical
# Promoted to critical on 2025-01-24
---
```

### Unit Tests

```csharp
[Fact]
public async Task UpdatePromotionLevel_UpdatesDocumentAndChunks()
{
    // Arrange
    var document = CreateTestDocument(promotionLevel: PromotionLevel.Standard);
    var repository = CreateRepositoryWithDocument(document);
    var tool = CreateTool(repository);

    // Act
    var result = await tool.UpdatePromotionLevel(
        document_path: "problems/test.md",
        promotion_level: "critical");
    var response = JsonSerializer.Deserialize<UpdatePromotionLevelResponse>(result);

    // Assert
    Assert.Equal("updated", response.Status);
    Assert.Equal("standard", response.PreviousLevel);
    Assert.Equal("critical", response.NewLevel);

    var updatedDoc = await repository.GetByIdAsync(document.Id);
    Assert.Equal(PromotionLevel.Critical, updatedDoc.PromotionLevel);
}

[Fact]
public async Task UpdatePromotionLevel_RejectsExternalDocuments()
{
    // Arrange
    var externalRepo = CreateExternalRepoWithDocument("external/doc.md");
    var tool = CreateTool(externalRepository: externalRepo);

    // Act
    var result = await tool.UpdatePromotionLevel(
        document_path: "external/doc.md",
        promotion_level: "important");
    var error = JsonSerializer.Deserialize<ToolErrorResponse>(result);

    // Assert
    Assert.True(error.Error);
    Assert.Equal("EXTERNAL_DOC_READONLY", error.Code);
}

[Fact]
public async Task UpdatePromotionLevel_RejectsInvalidLevel()
{
    // Arrange
    var tool = CreateTool();

    // Act
    var result = await tool.UpdatePromotionLevel(
        document_path: "problems/test.md",
        promotion_level: "super-important");
    var error = JsonSerializer.Deserialize<ToolErrorResponse>(result);

    // Assert
    Assert.True(error.Error);
    Assert.Equal("INVALID_PROMOTION_LEVEL", error.Code);
}

[Fact]
public async Task UpdatePromotionLevel_UpdatesFrontmatter()
{
    // Arrange
    var fileSystem = new MockFileSystem();
    fileSystem.AddFile("problems/test.md", @"---
doc_type: problem
title: Test
promotion_level: standard
---
# Content");
    var tool = CreateTool(fileSystem: fileSystem);

    // Act
    await tool.UpdatePromotionLevel(
        document_path: "problems/test.md",
        promotion_level: "critical");

    // Assert
    var content = fileSystem.GetFile("problems/test.md");
    Assert.Contains("promotion_level: critical", content);
    Assert.Contains("# Promoted to critical on", content);
}

[Fact]
public async Task UpdatePromotionLevel_RequiresProjectActivation()
{
    // Arrange
    var projectContext = CreateInactiveContext();
    var tool = CreateTool(projectContext: projectContext);

    // Act
    var result = await tool.UpdatePromotionLevel(
        document_path: "problems/test.md",
        promotion_level: "important");
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
| `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-promote/SKILL.md` | Skill definition and workflow |
| `src/CompoundDocs.McpServer/Tools/PromotionTools.cs` | update_promotion_level tool |
| `src/CompoundDocs.Core/Models/PromotionLevel.cs` | Promotion level enum |
| `src/CompoundDocs.McpServer/Models/Responses/UpdatePromotionLevelResponse.cs` | Response DTO |
| `tests/CompoundDocs.Tests/Tools/PromotionToolsTests.cs` | Unit tests |

### Modified Files

| File | Changes |
|------|---------|
| `src/CompoundDocs.Core/Repositories/IDocumentRepository.cs` | Add `UpdatePromotionLevelAsync` method |
| `src/CompoundDocs.Infrastructure/Repositories/DocumentRepository.cs` | Implement `UpdatePromotionLevelAsync` |
| `src/CompoundDocs.Core/Services/IFrontmatterService.cs` | Add `Serialize` method with audit comment support |
| `src/CompoundDocs.Infrastructure/Services/FrontmatterService.cs` | Implement frontmatter serialization |
| `src/CompoundDocs.McpServer/Program.cs` | Register `PromotionTools` in DI |

---

## Notes

- Promotion changes require updating both file and database to maintain consistency
- The audit comment in frontmatter provides git-trackable history of promotions
- External documents are detected by checking the `ExternalDocument` table
- Transaction ensures document and all chunks are updated atomically
- If database update fails after file update, the implementation attempts to rollback the file change
- The skill workflow in SKILL.md guides Claude through the interactive promotion process
- Demotion follows the same workflow (e.g., critical -> standard)
