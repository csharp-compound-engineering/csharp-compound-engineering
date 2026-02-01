# Phase 078: update_promotion_level MCP Tool

> **Status**: PLANNED
> **Category**: MCP Tools
> **Estimated Effort**: M
> **Prerequisites**: Phase 025 (Tool Registration), Phase 048 (Document Repository), Phase 063 (Promotion Service)

---

## Spec References

- [mcp-server/tools.md - Tool 8: Update Promotion Level](../spec/mcp-server/tools.md#8-update-promotion-level-tool)
- [doc-types/promotion.md - Promotion Workflow](../spec/doc-types/promotion.md#promotion-workflow)
- [doc-types/promotion.md - Promotion Level Enum](../spec/doc-types/promotion.md#promotion-level-enum)
- [mcp-server/database-schema.md - Chunk Promotion](../spec/mcp-server/database-schema.md#document-chunks-schema-for-large-documents)

---

## Objectives

1. Implement `update_promotion_level` MCP tool for modifying document visibility levels
2. Create tool parameter validation for `document_path` and `promotion_level`
3. Implement level validation against allowed enum values (standard, important, critical)
4. Update document record in vector database with new promotion level
5. Atomically update all associated document chunks to inherit new promotion level
6. Return response with previous and new promotion levels for verification
7. Handle error cases including external docs and missing documents

---

## Acceptance Criteria

### Tool Registration

- [ ] Tool registered with `[McpServerTool(Name = "update_promotion_level")]` attribute
- [ ] Tool class placed in `DocumentTools.cs` alongside `delete_documents` tool
- [ ] Tool description matches spec: "Update the promotion level of a document (standard, important, critical)"
- [ ] Tool receives dependencies via constructor injection

### Parameter Definition

- [ ] `document_path` parameter (string, required):
  - [ ] `[Description("Relative path to document within ./csharp-compounding-docs/")]`
  - [ ] Non-null and non-empty validation
  - [ ] Path normalization (forward slashes, trimming)
- [ ] `promotion_level` parameter (string, required):
  - [ ] `[Description("New level: standard, important, or critical")]`
  - [ ] Enum validation against allowed values
  - [ ] Case-insensitive matching

### Level Validation

- [ ] Accepts `standard` as valid level (default, normal visibility)
- [ ] Accepts `important` as valid level (relevance boost)
- [ ] Accepts `critical` as valid level (required reading, always surfaced)
- [ ] Returns `INVALID_PARAMS` error for invalid levels with list of valid values
- [ ] Case-insensitive comparison (e.g., "CRITICAL" accepted as "critical")

### Database Record Update

- [ ] Locates document by relative path within current tenant context
- [ ] Validates document exists in `documents` collection
- [ ] Updates `promotion_level` field on document record
- [ ] Persists updated document via repository upsert
- [ ] Returns `DOCUMENT_NOT_FOUND` error if document doesn't exist

### Chunk Promotion Inheritance Update

- [ ] Queries all chunks with matching `document_id`
- [ ] Updates `promotion_level` field on all associated chunks
- [ ] Performs chunk updates atomically (single transaction)
- [ ] Logs number of chunks updated for observability

### Response Format

- [ ] Returns `status: "updated"` on success
- [ ] Includes `document_path` in response (normalized)
- [ ] Includes `previous_level` showing level before update
- [ ] Includes `new_level` showing updated level
- [ ] JSON response matches spec format exactly

### Error Handling

- [ ] `EXTERNAL_DOCS_NOT_PROMOTABLE` when path points to external docs
- [ ] `DOCUMENT_NOT_FOUND` when document doesn't exist
- [ ] `PROJECT_NOT_ACTIVATED` when no project is active
- [ ] `INVALID_PARAMS` for missing or invalid parameters
- [ ] `DATABASE_ERROR` for persistence failures

---

## Implementation Notes

### 1. Tool Class Implementation

Add to `DocumentTools.cs`:

```csharp
// src/CompoundDocs.McpServer/Tools/DocumentTools.cs
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// MCP tools for document management operations.
/// </summary>
[McpServerToolType]
public class DocumentTools
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentChunkRepository _chunkRepository;
    private readonly IProjectContext _projectContext;
    private readonly IPromotionService _promotionService;
    private readonly ILogger<DocumentTools> _logger;

    public DocumentTools(
        IDocumentRepository documentRepository,
        IDocumentChunkRepository chunkRepository,
        IProjectContext projectContext,
        IPromotionService promotionService,
        ILogger<DocumentTools> logger)
    {
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _projectContext = projectContext;
        _promotionService = promotionService;
        _logger = logger;
    }

    /// <summary>
    /// Update the promotion level of a document.
    /// Promotion levels control document visibility in RAG queries.
    /// </summary>
    [McpServerTool(Name = "update_promotion_level")]
    [Description("Update the promotion level of a document (standard, important, critical). " +
                 "Higher promotion levels surface documents more readily in RAG queries.")]
    public async Task<string> UpdatePromotionLevel(
        [Description("Relative path to document within ./csharp-compounding-docs/")]
        string document_path,
        [Description("New level: standard, important, or critical")]
        string promotion_level,
        CancellationToken cancellationToken = default)
    {
        // Validate project is activated
        if (!_projectContext.IsActivated)
        {
            return CreateErrorResponse(
                ErrorCodes.ProjectNotActivated,
                "No project is currently activated. Call activate_project first.");
        }

        // Validate and normalize parameters
        var validationResult = ValidateParameters(document_path, promotion_level);
        if (!validationResult.IsValid)
        {
            return CreateErrorResponse(
                ErrorCodes.InvalidParams,
                validationResult.ErrorMessage,
                validationResult.Details);
        }

        var normalizedPath = NormalizePath(document_path);
        var normalizedLevel = promotion_level.ToLowerInvariant();

        _logger.LogInformation(
            "Updating promotion level for {DocumentPath} to {NewLevel}",
            normalizedPath,
            normalizedLevel);

        try
        {
            // Check if this is an external doc path
            if (IsExternalDocPath(normalizedPath))
            {
                return CreateErrorResponse(
                    ErrorCodes.ExternalDocsNotPromotable,
                    "External documentation cannot be promoted. " +
                    "Only compounding docs support promotion levels.");
            }

            // Execute the promotion update
            var result = await _promotionService.UpdatePromotionLevelAsync(
                normalizedPath,
                normalizedLevel,
                cancellationToken);

            if (!result.Success)
            {
                return CreateErrorResponse(result.ErrorCode, result.ErrorMessage);
            }

            _logger.LogInformation(
                "Promotion level updated: {DocumentPath} from {PreviousLevel} to {NewLevel}, " +
                "{ChunkCount} chunks updated",
                normalizedPath,
                result.PreviousLevel,
                result.NewLevel,
                result.ChunksUpdated);

            return CreateSuccessResponse(new UpdatePromotionLevelResponse
            {
                Status = "updated",
                DocumentPath = normalizedPath,
                PreviousLevel = result.PreviousLevel,
                NewLevel = result.NewLevel
            });
        }
        catch (DocumentNotFoundException)
        {
            return CreateErrorResponse(
                ErrorCodes.DocumentNotFound,
                $"Document not found: {normalizedPath}",
                new { path = normalizedPath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to update promotion level for {DocumentPath}",
                normalizedPath);

            return CreateErrorResponse(
                ErrorCodes.DatabaseError,
                "Failed to update promotion level. See server logs for details.");
        }
    }

    private ValidationResult ValidateParameters(string documentPath, string promotionLevel)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return ValidationResult.Invalid(
                "document_path is required and cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(promotionLevel))
        {
            return ValidationResult.Invalid(
                "promotion_level is required and cannot be empty");
        }

        if (!PromotionLevels.IsValid(promotionLevel))
        {
            return ValidationResult.Invalid(
                $"Invalid promotion_level: {promotionLevel}. " +
                $"Valid values are: {string.Join(", ", PromotionLevels.All)}",
                new { invalidValue = promotionLevel, validValues = PromotionLevels.All });
        }

        return ValidationResult.Valid();
    }

    private static string NormalizePath(string path)
    {
        // Replace backslashes with forward slashes
        var normalized = path.Replace('\\', '/');

        // Remove leading ./csharp-compounding-docs/ prefix if present
        const string prefix = "./csharp-compounding-docs/";
        if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[prefix.Length..];
        }

        // Remove leading slashes
        normalized = normalized.TrimStart('/');

        return normalized;
    }

    private bool IsExternalDocPath(string path)
    {
        // External docs are in the external_docs configured path
        var externalDocsPath = _projectContext.Configuration?.ExternalDocsPath;
        if (string.IsNullOrEmpty(externalDocsPath))
        {
            return false;
        }

        // Check if path is within external docs directory
        return path.StartsWith(externalDocsPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateSuccessResponse<T>(T response)
    {
        return JsonSerializer.Serialize(response, JsonOptions.Default);
    }

    private static string CreateErrorResponse(
        string code,
        string message,
        object? details = null)
    {
        return JsonSerializer.Serialize(new
        {
            error = true,
            code,
            message,
            details
        }, JsonOptions.Default);
    }
}
```

### 2. Response DTOs

```csharp
// src/CompoundDocs.McpServer/Models/Responses/UpdatePromotionLevelResponse.cs
namespace CompoundDocs.McpServer.Models.Responses;

/// <summary>
/// Response returned by the update_promotion_level tool.
/// </summary>
public sealed record UpdatePromotionLevelResponse
{
    /// <summary>
    /// Status of the operation. Always "updated" on success.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Normalized relative path to the document.
    /// </summary>
    public required string DocumentPath { get; init; }

    /// <summary>
    /// The document's promotion level before the update.
    /// </summary>
    public required string PreviousLevel { get; init; }

    /// <summary>
    /// The document's new promotion level after the update.
    /// </summary>
    public required string NewLevel { get; init; }
}
```

### 3. Promotion Service Interface

```csharp
// src/CompoundDocs.McpServer/Services/IPromotionService.cs
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Service for managing document promotion levels.
/// </summary>
public interface IPromotionService
{
    /// <summary>
    /// Updates the promotion level of a document and all its chunks.
    /// </summary>
    /// <param name="relativePath">Relative path to the document.</param>
    /// <param name="newLevel">New promotion level (standard, important, critical).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing previous level, new level, and chunks updated count.</returns>
    Task<PromotionUpdateResult> UpdatePromotionLevelAsync(
        string relativePath,
        string newLevel,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a promotion level update operation.
/// </summary>
public sealed record PromotionUpdateResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string PreviousLevel { get; init; } = string.Empty;
    public string NewLevel { get; init; } = string.Empty;
    public int ChunksUpdated { get; init; }

    public static PromotionUpdateResult Succeeded(
        string previousLevel,
        string newLevel,
        int chunksUpdated) => new()
    {
        Success = true,
        PreviousLevel = previousLevel,
        NewLevel = newLevel,
        ChunksUpdated = chunksUpdated
    };

    public static PromotionUpdateResult Failed(string errorCode, string errorMessage) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}
```

### 4. Promotion Service Implementation

```csharp
// src/CompoundDocs.McpServer/Services/PromotionService.cs
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Service for managing document promotion levels.
/// Updates both document records and associated chunks atomically.
/// </summary>
public sealed class PromotionService : IPromotionService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentChunkRepository _chunkRepository;
    private readonly IProjectContext _projectContext;
    private readonly IFrontmatterService _frontmatterService;
    private readonly IFileService _fileService;
    private readonly ILogger<PromotionService> _logger;

    public PromotionService(
        IDocumentRepository documentRepository,
        IDocumentChunkRepository chunkRepository,
        IProjectContext projectContext,
        IFrontmatterService frontmatterService,
        IFileService fileService,
        ILogger<PromotionService> logger)
    {
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _projectContext = projectContext;
        _frontmatterService = frontmatterService;
        _fileService = fileService;
        _logger = logger;
    }

    public async Task<PromotionUpdateResult> UpdatePromotionLevelAsync(
        string relativePath,
        string newLevel,
        CancellationToken cancellationToken = default)
    {
        // Get current tenant context
        var tenantContext = _projectContext.CurrentTenant;

        // Find the document by path
        var document = await _documentRepository.GetByPathAsync(
            relativePath,
            tenantContext.ProjectName,
            tenantContext.BranchName,
            tenantContext.PathHash,
            cancellationToken);

        if (document is null)
        {
            throw new DocumentNotFoundException(relativePath);
        }

        var previousLevel = document.PromotionLevel;

        // Skip if already at requested level
        if (string.Equals(previousLevel, newLevel, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "Document {Path} already at promotion level {Level}, no update needed",
                relativePath,
                newLevel);

            return PromotionUpdateResult.Succeeded(previousLevel, newLevel, chunksUpdated: 0);
        }

        // Update the document's promotion level
        document.PromotionLevel = newLevel;

        // Update frontmatter in source file
        await UpdateFrontmatterAsync(relativePath, newLevel, cancellationToken);

        // Persist document update
        await _documentRepository.UpsertAsync(document, cancellationToken);

        // Update all associated chunks atomically
        var chunksUpdated = await UpdateChunkPromotionLevelsAsync(
            document.Id,
            newLevel,
            cancellationToken);

        return PromotionUpdateResult.Succeeded(previousLevel, newLevel, chunksUpdated);
    }

    private async Task UpdateFrontmatterAsync(
        string relativePath,
        string newLevel,
        CancellationToken cancellationToken)
    {
        var fullPath = _projectContext.GetCompoundingDocsPath(relativePath);

        // Read current file content
        var content = await _fileService.ReadAllTextAsync(fullPath, cancellationToken);

        // Update the promotion_level field in frontmatter
        var updatedContent = _frontmatterService.UpdateField(
            content,
            "promotion_level",
            newLevel);

        // Write back to file
        await _fileService.WriteAllTextAsync(fullPath, updatedContent, cancellationToken);

        _logger.LogDebug(
            "Updated frontmatter promotion_level to {Level} in {Path}",
            newLevel,
            relativePath);
    }

    private async Task<int> UpdateChunkPromotionLevelsAsync(
        string documentId,
        string newLevel,
        CancellationToken cancellationToken)
    {
        // Get all chunks for this document
        var chunks = await _chunkRepository.GetByDocumentIdAsync(
            documentId,
            cancellationToken);

        var chunkList = chunks.ToList();
        if (chunkList.Count == 0)
        {
            return 0;
        }

        // Update promotion level on all chunks
        foreach (var chunk in chunkList)
        {
            chunk.PromotionLevel = newLevel;
        }

        // Upsert all chunks in batch
        await _chunkRepository.UpsertBatchAsync(chunkList, cancellationToken);

        _logger.LogDebug(
            "Updated promotion level to {Level} on {Count} chunks for document {DocumentId}",
            newLevel,
            chunkList.Count,
            documentId);

        return chunkList.Count;
    }
}
```

### 5. Promotion Levels Constants

```csharp
// src/CompoundDocs.Common/Models/PromotionLevels.cs
namespace CompoundDocs.Common.Models;

/// <summary>
/// Defines valid promotion levels for compound documents.
/// </summary>
public static class PromotionLevels
{
    /// <summary>
    /// Standard level - default visibility, retrieved via normal RAG/search.
    /// </summary>
    public const string Standard = "standard";

    /// <summary>
    /// Important level - higher relevance boost, surfaces more readily in related queries.
    /// </summary>
    public const string Important = "important";

    /// <summary>
    /// Critical level - required reading, must be surfaced before code generation
    /// in related areas.
    /// </summary>
    public const string Critical = "critical";

    /// <summary>
    /// All valid promotion levels.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Standard,
        Important,
        Critical
    };

    /// <summary>
    /// Validates that a promotion level string is valid.
    /// Case-insensitive comparison.
    /// </summary>
    public static bool IsValid(string level) =>
        !string.IsNullOrWhiteSpace(level) &&
        All.Contains(level, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Normalizes a promotion level to lowercase.
    /// Throws ArgumentException if invalid.
    /// </summary>
    public static string Normalize(string level)
    {
        if (!IsValid(level))
        {
            throw new ArgumentException(
                $"Invalid promotion level: {level}. Valid values: {string.Join(", ", All)}",
                nameof(level));
        }

        return level.ToLowerInvariant();
    }
}
```

### 6. Validation Result Helper

```csharp
// src/CompoundDocs.McpServer/Tools/ValidationResult.cs
namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// Result of parameter validation for tool methods.
/// </summary>
public sealed record ValidationResult
{
    public bool IsValid { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public object? Details { get; init; }

    public static ValidationResult Valid() => new() { IsValid = true };

    public static ValidationResult Invalid(string errorMessage, object? details = null) =>
        new()
        {
            IsValid = false,
            ErrorMessage = errorMessage,
            Details = details
        };
}
```

### 7. Service Registration

```csharp
// In Program.cs or service registration
services.AddScoped<IPromotionService, PromotionService>();
```

---

## Response Format

### Success Response

```json
{
  "status": "updated",
  "document_path": "problems/n-plus-one-query-20250120.md",
  "previous_level": "standard",
  "new_level": "critical"
}
```

### Error Response - Document Not Found

```json
{
  "error": true,
  "code": "DOCUMENT_NOT_FOUND",
  "message": "Document not found: problems/nonexistent-doc.md",
  "details": {
    "path": "problems/nonexistent-doc.md"
  }
}
```

### Error Response - External Docs Not Promotable

```json
{
  "error": true,
  "code": "EXTERNAL_DOCS_NOT_PROMOTABLE",
  "message": "External documentation cannot be promoted. Only compounding docs support promotion levels.",
  "details": null
}
```

### Error Response - Invalid Promotion Level

```json
{
  "error": true,
  "code": "INVALID_PARAMS",
  "message": "Invalid promotion_level: ultra. Valid values are: standard, important, critical",
  "details": {
    "invalidValue": "ultra",
    "validValues": ["standard", "important", "critical"]
  }
}
```

---

## Dependencies

### Depends On

- **Phase 025**: Tool Registration Infrastructure - Tool class structure and attribute patterns
- **Phase 048**: Document Repository - CRUD operations for CompoundDocument records
- **Phase 063**: Promotion Service Infrastructure - Base promotion service setup

### Blocks

- **Phase 079+**: Promotion-related skills that invoke this tool
- **Phase 085+**: RAG query filtering by promotion level

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Tools/UpdatePromotionLevelToolTests.cs
namespace CompoundDocs.Tests.Tools;

public class UpdatePromotionLevelToolTests
{
    [Fact]
    public async Task UpdatePromotionLevel_ValidParameters_ReturnsSuccess()
    {
        // Arrange
        var mockContext = CreateMockProjectContext(isActivated: true);
        var mockRepo = new Mock<IDocumentRepository>();
        var mockChunkRepo = new Mock<IDocumentChunkRepository>();
        var mockPromotion = new Mock<IPromotionService>();

        mockPromotion.Setup(p => p.UpdatePromotionLevelAsync(
                "problems/test-doc.md", "critical", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PromotionUpdateResult.Succeeded("standard", "critical", 3));

        var tool = CreateTool(mockContext, mockRepo, mockChunkRepo, mockPromotion);

        // Act
        var result = await tool.UpdatePromotionLevel(
            "problems/test-doc.md",
            "critical");

        // Assert
        var response = JsonSerializer.Deserialize<UpdatePromotionLevelResponse>(result);
        Assert.NotNull(response);
        Assert.Equal("updated", response.Status);
        Assert.Equal("standard", response.PreviousLevel);
        Assert.Equal("critical", response.NewLevel);
    }

    [Fact]
    public async Task UpdatePromotionLevel_ProjectNotActivated_ReturnsError()
    {
        // Arrange
        var mockContext = CreateMockProjectContext(isActivated: false);
        var tool = CreateTool(mockContext);

        // Act
        var result = await tool.UpdatePromotionLevel(
            "problems/test-doc.md",
            "critical");

        // Assert
        var error = JsonSerializer.Deserialize<ErrorResponse>(result);
        Assert.True(error.Error);
        Assert.Equal("PROJECT_NOT_ACTIVATED", error.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task UpdatePromotionLevel_EmptyDocumentPath_ReturnsError(string path)
    {
        // Arrange
        var mockContext = CreateMockProjectContext(isActivated: true);
        var tool = CreateTool(mockContext);

        // Act
        var result = await tool.UpdatePromotionLevel(path, "critical");

        // Assert
        var error = JsonSerializer.Deserialize<ErrorResponse>(result);
        Assert.True(error.Error);
        Assert.Equal("INVALID_PARAMS", error.Code);
        Assert.Contains("document_path", error.Message);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("promoted")]
    [InlineData("ULTRA")]
    [InlineData("")]
    public async Task UpdatePromotionLevel_InvalidLevel_ReturnsError(string level)
    {
        // Arrange
        var mockContext = CreateMockProjectContext(isActivated: true);
        var tool = CreateTool(mockContext);

        // Act
        var result = await tool.UpdatePromotionLevel(
            "problems/test-doc.md",
            level);

        // Assert
        var error = JsonSerializer.Deserialize<ErrorResponse>(result);
        Assert.True(error.Error);
        Assert.Equal("INVALID_PARAMS", error.Code);
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("important")]
    [InlineData("critical")]
    [InlineData("STANDARD")]
    [InlineData("Important")]
    [InlineData("CRITICAL")]
    public async Task UpdatePromotionLevel_ValidLevels_AreAccepted(string level)
    {
        // Arrange
        var mockContext = CreateMockProjectContext(isActivated: true);
        var mockPromotion = new Mock<IPromotionService>();
        mockPromotion.Setup(p => p.UpdatePromotionLevelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PromotionUpdateResult.Succeeded("standard", level.ToLowerInvariant(), 0));

        var tool = CreateTool(mockContext, promotionService: mockPromotion);

        // Act
        var result = await tool.UpdatePromotionLevel("problems/test-doc.md", level);

        // Assert
        var response = JsonSerializer.Deserialize<UpdatePromotionLevelResponse>(result);
        Assert.Equal("updated", response.Status);
    }

    [Fact]
    public async Task UpdatePromotionLevel_DocumentNotFound_ReturnsError()
    {
        // Arrange
        var mockContext = CreateMockProjectContext(isActivated: true);
        var mockPromotion = new Mock<IPromotionService>();
        mockPromotion.Setup(p => p.UpdatePromotionLevelAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DocumentNotFoundException("problems/nonexistent.md"));

        var tool = CreateTool(mockContext, promotionService: mockPromotion);

        // Act
        var result = await tool.UpdatePromotionLevel(
            "problems/nonexistent.md",
            "critical");

        // Assert
        var error = JsonSerializer.Deserialize<ErrorResponse>(result);
        Assert.True(error.Error);
        Assert.Equal("DOCUMENT_NOT_FOUND", error.Code);
    }

    [Fact]
    public async Task UpdatePromotionLevel_ExternalDoc_ReturnsNotPromotableError()
    {
        // Arrange
        var mockContext = CreateMockProjectContext(
            isActivated: true,
            externalDocsPath: "docs/");
        var tool = CreateTool(mockContext);

        // Act
        var result = await tool.UpdatePromotionLevel(
            "docs/api/authentication.md",
            "critical");

        // Assert
        var error = JsonSerializer.Deserialize<ErrorResponse>(result);
        Assert.True(error.Error);
        Assert.Equal("EXTERNAL_DOCS_NOT_PROMOTABLE", error.Code);
    }
}
```

### Promotion Service Tests

```csharp
// tests/CompoundDocs.Tests/Services/PromotionServiceTests.cs
namespace CompoundDocs.Tests.Services;

public class PromotionServiceTests
{
    [Fact]
    public async Task UpdatePromotionLevelAsync_UpdatesDocumentAndChunks()
    {
        // Arrange
        var document = new CompoundDocument
        {
            Id = "doc-123",
            RelativePath = "problems/test.md",
            PromotionLevel = "standard"
        };

        var chunks = new List<DocumentChunk>
        {
            new() { Id = "chunk-1", DocumentId = "doc-123", PromotionLevel = "standard" },
            new() { Id = "chunk-2", DocumentId = "doc-123", PromotionLevel = "standard" },
            new() { Id = "chunk-3", DocumentId = "doc-123", PromotionLevel = "standard" }
        };

        var mockDocRepo = new Mock<IDocumentRepository>();
        mockDocRepo.Setup(r => r.GetByPathAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var mockChunkRepo = new Mock<IDocumentChunkRepository>();
        mockChunkRepo.Setup(r => r.GetByDocumentIdAsync(
                "doc-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        var service = CreateService(mockDocRepo, mockChunkRepo);

        // Act
        var result = await service.UpdatePromotionLevelAsync(
            "problems/test.md",
            "critical");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("standard", result.PreviousLevel);
        Assert.Equal("critical", result.NewLevel);
        Assert.Equal(3, result.ChunksUpdated);

        // Verify document was upserted
        mockDocRepo.Verify(r => r.UpsertAsync(
            It.Is<CompoundDocument>(d => d.PromotionLevel == "critical"),
            It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify chunks were upserted
        mockChunkRepo.Verify(r => r.UpsertBatchAsync(
            It.Is<IEnumerable<DocumentChunk>>(c =>
                c.All(chunk => chunk.PromotionLevel == "critical")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdatePromotionLevelAsync_SameLevel_SkipsUpdate()
    {
        // Arrange
        var document = new CompoundDocument
        {
            Id = "doc-123",
            RelativePath = "problems/test.md",
            PromotionLevel = "critical"
        };

        var mockDocRepo = new Mock<IDocumentRepository>();
        mockDocRepo.Setup(r => r.GetByPathAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        var service = CreateService(mockDocRepo);

        // Act
        var result = await service.UpdatePromotionLevelAsync(
            "problems/test.md",
            "critical");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("critical", result.PreviousLevel);
        Assert.Equal("critical", result.NewLevel);
        Assert.Equal(0, result.ChunksUpdated);

        // Verify no updates occurred
        mockDocRepo.Verify(r => r.UpsertAsync(
            It.IsAny<CompoundDocument>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdatePromotionLevelAsync_DocumentNotFound_ThrowsException()
    {
        // Arrange
        var mockDocRepo = new Mock<IDocumentRepository>();
        mockDocRepo.Setup(r => r.GetByPathAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument?)null);

        var service = CreateService(mockDocRepo);

        // Act & Assert
        await Assert.ThrowsAsync<DocumentNotFoundException>(() =>
            service.UpdatePromotionLevelAsync("nonexistent.md", "critical"));
    }
}
```

### Promotion Levels Tests

```csharp
// tests/CompoundDocs.Tests/Models/PromotionLevelsTests.cs
namespace CompoundDocs.Tests.Models;

public class PromotionLevelsTests
{
    [Theory]
    [InlineData("standard", true)]
    [InlineData("important", true)]
    [InlineData("critical", true)]
    [InlineData("Standard", true)]   // Case insensitive
    [InlineData("IMPORTANT", true)]
    [InlineData("Critical", true)]
    [InlineData("invalid", false)]
    [InlineData("promoted", false)]  // Not a valid level per spec
    [InlineData("pinned", false)]    // Not a valid level per spec
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_ReturnsExpectedResult(string level, bool expected)
    {
        // Act
        var result = PromotionLevels.IsValid(level);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("STANDARD", "standard")]
    [InlineData("Important", "important")]
    [InlineData("CRITICAL", "critical")]
    public void Normalize_ReturnsLowercaseValue(string input, string expected)
    {
        // Act
        var result = PromotionLevels.Normalize(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Normalize_InvalidLevel_ThrowsArgumentException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() =>
            PromotionLevels.Normalize("invalid"));

        Assert.Contains("standard, important, critical", ex.Message);
    }

    [Fact]
    public void All_ContainsExactlyThreeLevels()
    {
        // Assert
        Assert.Equal(3, PromotionLevels.All.Count);
        Assert.Contains(PromotionLevels.Standard, PromotionLevels.All);
        Assert.Contains(PromotionLevels.Important, PromotionLevels.All);
        Assert.Contains(PromotionLevels.Critical, PromotionLevels.All);
    }
}
```

### Integration Test

```csharp
// tests/CompoundDocs.Tests/Integration/UpdatePromotionLevelIntegrationTests.cs
namespace CompoundDocs.Tests.Integration;

public class UpdatePromotionLevelIntegrationTests : IClassFixture<McpServerFixture>
{
    private readonly McpServerFixture _fixture;

    public UpdatePromotionLevelIntegrationTests(McpServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpdatePromotionLevel_EndToEnd_UpdatesDocumentAndChunks()
    {
        // Arrange - Create and index a test document
        var testDocPath = "problems/integration-test-doc.md";
        await _fixture.CreateTestDocument(testDocPath, promotionLevel: "standard");
        await _fixture.IndexDocument(testDocPath);

        // Act - Update promotion level via MCP tool
        var result = await _fixture.InvokeTool("update_promotion_level", new
        {
            document_path = testDocPath,
            promotion_level = "critical"
        });

        // Assert - Verify response
        var response = JsonSerializer.Deserialize<UpdatePromotionLevelResponse>(result);
        Assert.Equal("updated", response.Status);
        Assert.Equal("standard", response.PreviousLevel);
        Assert.Equal("critical", response.NewLevel);

        // Verify document in database
        var document = await _fixture.GetDocument(testDocPath);
        Assert.Equal("critical", document.PromotionLevel);

        // Verify chunks in database
        var chunks = await _fixture.GetChunks(document.Id);
        Assert.All(chunks, c => Assert.Equal("critical", c.PromotionLevel));

        // Verify frontmatter in file
        var fileContent = await _fixture.ReadFile(testDocPath);
        Assert.Contains("promotion_level: critical", fileContent);
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Tools/DocumentTools.cs` | Modify | Add update_promotion_level tool method |
| `src/CompoundDocs.McpServer/Models/Responses/UpdatePromotionLevelResponse.cs` | Create | Response DTO |
| `src/CompoundDocs.McpServer/Services/IPromotionService.cs` | Create | Promotion service interface |
| `src/CompoundDocs.McpServer/Services/PromotionService.cs` | Create | Promotion service implementation |
| `src/CompoundDocs.Common/Models/PromotionLevels.cs` | Modify | Update to match spec (standard/important/critical) |
| `src/CompoundDocs.McpServer/Tools/ValidationResult.cs` | Create | Parameter validation helper |
| `tests/CompoundDocs.Tests/Tools/UpdatePromotionLevelToolTests.cs` | Create | Tool unit tests |
| `tests/CompoundDocs.Tests/Services/PromotionServiceTests.cs` | Create | Service unit tests |
| `tests/CompoundDocs.Tests/Models/PromotionLevelsTests.cs` | Modify | Update tests for spec levels |
| `tests/CompoundDocs.Tests/Integration/UpdatePromotionLevelIntegrationTests.cs` | Create | End-to-end tests |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Chunk update partial failure | Use batch upsert in single transaction |
| Frontmatter update fails after DB update | Transaction rollback; consider saga pattern |
| Invalid promotion level accepted | Strict validation before any processing |
| External docs incorrectly promoted | Path prefix check against external_docs config |
| Concurrent updates cause inconsistency | Document-level locking during update |
| File system write failure | Write to temp file first, then atomic rename |

---

## Notes

- The tool updates both the database record AND the source file frontmatter for consistency
- Chunk promotion inheritance is atomic - all chunks update together or none do
- The three-level promotion system (standard/important/critical) matches the spec exactly
- Case-insensitive matching allows "Critical", "CRITICAL", or "critical" as input
- External documentation cannot be promoted - this is enforced at the tool level
- The previous level is returned to allow skills to provide before/after context to users
