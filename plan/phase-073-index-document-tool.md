# Phase 073: index_document MCP Tool

> **Status**: PLANNED
> **Category**: MCP Tools
> **Estimated Effort**: S
> **Prerequisites**: Phase 025 (Tool Registration), Phase 068 (Indexing Service)

---

## Spec References

- [mcp-server/tools.md - Index Document Tool](../spec/mcp-server/tools.md#3-index-document-tool) - Tool specification and response format
- [mcp-server/file-watcher.md](../spec/mcp-server/file-watcher.md) - File watcher context (this tool is a fallback)
- [mcp-server/tools.md - Error Handling](../spec/mcp-server/tools.md#error-handling) - Standard error codes

---

## Objectives

1. Implement the `index_document` MCP tool for manual indexing of documents
2. Define tool parameters with proper validation and description attributes
3. Implement path validation and security checks
4. Integrate with the indexing service for embedding generation and storage
5. Return standardized success/error responses
6. Ensure idempotent behavior (re-indexing produces same result)

---

## Acceptance Criteria

### Tool Registration

- [ ] `index_document` tool registered with `[McpServerTool]` attribute
- [ ] Tool has `Name = "index_document"` explicitly set
- [ ] `[Description]` attribute provides clear purpose: "Manually trigger indexing of a specific document"
- [ ] Tool method signature follows async pattern with `CancellationToken`

### Parameter Definition

- [ ] `path` parameter defined as required string
- [ ] `path` parameter has `[Description]` attribute: "Relative path to document"
- [ ] Parameter validation rejects null or empty paths

### Path Validation and Security

- [ ] Path must be relative (not absolute)
- [ ] Path must be within `./csharp-compounding-docs/` directory
- [ ] Path traversal attacks prevented (no `..` segments escaping root)
- [ ] Path must end with `.md` extension
- [ ] File must exist at the specified path
- [ ] Return `DOCUMENT_NOT_FOUND` if file doesn't exist
- [ ] Return `FILE_SYSTEM_ERROR` for invalid or malicious paths

### Indexing Service Invocation

- [ ] Tool invokes `IIndexingService.IndexDocumentAsync()` method
- [ ] Active project context is validated before indexing
- [ ] Return `PROJECT_NOT_ACTIVATED` if no project is active
- [ ] Indexing includes markdown parsing, schema validation, and embedding generation
- [ ] Chunks are generated for documents >500 lines

### Success Response

- [ ] Response follows spec format:
  ```json
  {
    "status": "indexed",
    "path": "./csharp-compounding-docs/problems/new-issue-20250122.md",
    "embedding_dimensions": 1024
  }
  ```
- [ ] `status` field is always `"indexed"` on success
- [ ] `path` field reflects the normalized document path
- [ ] `embedding_dimensions` field confirms 1024-dimensional embedding

### Error Response

- [ ] Errors follow standard format: `{ error: true, code, message, details }`
- [ ] Appropriate error codes used:
  - `PROJECT_NOT_ACTIVATED` - No active project
  - `DOCUMENT_NOT_FOUND` - File doesn't exist
  - `FILE_SYSTEM_ERROR` - Path validation failure or I/O error
  - `SCHEMA_VALIDATION_FAILED` - Invalid frontmatter
  - `EMBEDDING_SERVICE_ERROR` - Ollama unavailable or failed

### Idempotent Behavior

- [ ] Re-indexing same document produces identical result
- [ ] Existing database record is updated (upsert), not duplicated
- [ ] Content hash comparison determines if re-embedding is needed
- [ ] Chunks are regenerated only if content changed

---

## Implementation Notes

### 1. Tool Class Location

Add to `IndexTools.cs` as defined in Phase 025:

```csharp
// src/CompoundDocs.McpServer/Tools/IndexTools.cs
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

[McpServerToolType]
public class IndexTools
{
    private readonly IIndexingService _indexingService;
    private readonly IProjectContext _projectContext;
    private readonly IPathValidator _pathValidator;
    private readonly ILogger<IndexTools> _logger;

    public IndexTools(
        IIndexingService indexingService,
        IProjectContext projectContext,
        IPathValidator pathValidator,
        ILogger<IndexTools> logger)
    {
        _indexingService = indexingService;
        _projectContext = projectContext;
        _pathValidator = pathValidator;
        _logger = logger;
    }

    [McpServerTool(Name = "index_document")]
    [Description("Manually trigger indexing of a specific document (used by skills after creating docs). This is a fallback; file watcher handles most cases.")]
    public async Task<string> IndexDocument(
        [Description("Relative path to document within ./csharp-compounding-docs/")] string path,
        CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}
```

### 2. Tool Implementation

```csharp
[McpServerTool(Name = "index_document")]
[Description("Manually trigger indexing of a specific document (used by skills after creating docs). This is a fallback; file watcher handles most cases.")]
public async Task<string> IndexDocument(
    [Description("Relative path to document within ./csharp-compounding-docs/")] string path,
    CancellationToken cancellationToken = default)
{
    _logger.LogInformation("Manual index requested for document: {Path}", path);

    // 1. Validate project is activated
    if (!_projectContext.IsActivated)
    {
        _logger.LogWarning("Index document failed: no project activated");
        return JsonSerializer.Serialize(new ToolErrorResponse(
            Error: true,
            Code: ToolErrorCodes.ProjectNotActivated,
            Message: "No project is currently activated. Call activate_project first.",
            Details: new { requiredTool = "activate_project" }));
    }

    // 2. Validate and normalize path
    var pathValidation = _pathValidator.ValidateDocumentPath(path);
    if (!pathValidation.IsValid)
    {
        _logger.LogWarning("Invalid document path: {Path} - {Error}", path, pathValidation.Error);
        return JsonSerializer.Serialize(new ToolErrorResponse(
            Error: true,
            Code: pathValidation.ErrorCode,
            Message: pathValidation.Error,
            Details: new { path }));
    }

    var normalizedPath = pathValidation.NormalizedPath;
    var fullPath = Path.Combine(_projectContext.CompoundingDocsRoot, normalizedPath);

    // 3. Check file exists
    if (!File.Exists(fullPath))
    {
        _logger.LogWarning("Document not found: {FullPath}", fullPath);
        return JsonSerializer.Serialize(new ToolErrorResponse(
            Error: true,
            Code: ToolErrorCodes.DocumentNotFound,
            Message: $"Document not found: {normalizedPath}",
            Details: new { path = normalizedPath }));
    }

    try
    {
        // 4. Invoke indexing service
        var result = await _indexingService.IndexDocumentAsync(
            fullPath,
            cancellationToken);

        _logger.LogInformation(
            "Document indexed successfully: {Path}, Dimensions: {Dimensions}",
            normalizedPath,
            result.EmbeddingDimensions);

        // 5. Return success response
        return JsonSerializer.Serialize(new IndexDocumentResponse(
            Status: "indexed",
            Path: $"./csharp-compounding-docs/{normalizedPath}",
            EmbeddingDimensions: result.EmbeddingDimensions));
    }
    catch (SchemaValidationException ex)
    {
        _logger.LogWarning(ex, "Schema validation failed for {Path}", normalizedPath);
        return JsonSerializer.Serialize(new ToolErrorResponse(
            Error: true,
            Code: ToolErrorCodes.SchemaValidationFailed,
            Message: "Document frontmatter validation failed",
            Details: new { path = normalizedPath, errors = ex.ValidationErrors }));
    }
    catch (EmbeddingServiceException ex)
    {
        _logger.LogError(ex, "Embedding service error for {Path}", normalizedPath);
        return JsonSerializer.Serialize(new ToolErrorResponse(
            Error: true,
            Code: ToolErrorCodes.EmbeddingServiceError,
            Message: "Failed to generate embeddings. Ensure Ollama is running.",
            Details: new { path = normalizedPath, innerMessage = ex.Message }));
    }
    catch (IOException ex)
    {
        _logger.LogError(ex, "File system error for {Path}", normalizedPath);
        return JsonSerializer.Serialize(new ToolErrorResponse(
            Error: true,
            Code: ToolErrorCodes.FileSystemError,
            Message: $"File system error: {ex.Message}",
            Details: new { path = normalizedPath }));
    }
}
```

### 3. Response DTOs

```csharp
// src/CompoundDocs.McpServer/Models/IndexDocumentResponse.cs
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Response from index_document tool on success.
/// </summary>
public record IndexDocumentResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("embedding_dimensions")] int EmbeddingDimensions);
```

### 4. Path Validator Interface

```csharp
// src/CompoundDocs.McpServer/Services/IPathValidator.cs
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Validates and normalizes document paths for security.
/// </summary>
public interface IPathValidator
{
    /// <summary>
    /// Validates a document path is safe and within allowed boundaries.
    /// </summary>
    PathValidationResult ValidateDocumentPath(string path);
}

public record PathValidationResult
{
    public bool IsValid { get; init; }
    public string? NormalizedPath { get; init; }
    public string? Error { get; init; }
    public string ErrorCode { get; init; } = ToolErrorCodes.FileSystemError;

    public static PathValidationResult Success(string normalizedPath) =>
        new() { IsValid = true, NormalizedPath = normalizedPath };

    public static PathValidationResult Failure(string error, string errorCode = "FILE_SYSTEM_ERROR") =>
        new() { IsValid = false, Error = error, ErrorCode = errorCode };
}
```

### 5. Path Validator Implementation

```csharp
// src/CompoundDocs.McpServer/Services/PathValidator.cs
namespace CompoundDocs.McpServer.Services;

public class PathValidator : IPathValidator
{
    private readonly ILogger<PathValidator> _logger;

    public PathValidator(ILogger<PathValidator> logger)
    {
        _logger = logger;
    }

    public PathValidationResult ValidateDocumentPath(string path)
    {
        // Null/empty check
        if (string.IsNullOrWhiteSpace(path))
        {
            return PathValidationResult.Failure("Path cannot be empty");
        }

        // Normalize path separators
        var normalizedPath = path.Replace('\\', '/').Trim();

        // Remove leading ./csharp-compounding-docs/ if present
        if (normalizedPath.StartsWith("./csharp-compounding-docs/"))
        {
            normalizedPath = normalizedPath["./csharp-compounding-docs/".Length..];
        }
        else if (normalizedPath.StartsWith("csharp-compounding-docs/"))
        {
            normalizedPath = normalizedPath["csharp-compounding-docs/".Length..];
        }

        // Remove leading slashes
        normalizedPath = normalizedPath.TrimStart('/');

        // Check for absolute path
        if (Path.IsPathRooted(normalizedPath))
        {
            return PathValidationResult.Failure("Absolute paths are not allowed. Use relative paths.");
        }

        // Check for path traversal attempts
        if (normalizedPath.Contains(".."))
        {
            _logger.LogWarning("Path traversal attempt detected: {Path}", path);
            return PathValidationResult.Failure("Path traversal (.. segments) not allowed");
        }

        // Validate .md extension
        if (!normalizedPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return PathValidationResult.Failure("Only .md files can be indexed");
        }

        // Check for invalid characters
        var invalidChars = Path.GetInvalidPathChars();
        if (normalizedPath.Any(c => invalidChars.Contains(c)))
        {
            return PathValidationResult.Failure("Path contains invalid characters");
        }

        // Validate path doesn't escape (after normalization)
        var fullPath = Path.GetFullPath(Path.Combine("/root", normalizedPath));
        if (!fullPath.StartsWith("/root/"))
        {
            return PathValidationResult.Failure("Path attempts to escape document root");
        }

        return PathValidationResult.Success(normalizedPath);
    }
}
```

### 6. Indexing Service Interface (Reference)

The tool depends on `IIndexingService` from Phase 068:

```csharp
// Expected interface from Phase 068
public interface IIndexingService
{
    /// <summary>
    /// Indexes a single document, generating embeddings and storing in the database.
    /// Idempotent - re-indexing updates existing record.
    /// </summary>
    Task<IndexingResult> IndexDocumentAsync(
        string fullPath,
        CancellationToken cancellationToken = default);
}

public record IndexingResult(
    string Path,
    int EmbeddingDimensions,
    bool WasUpdated,
    int ChunksCreated);
```

### 7. Service Registration

```csharp
// In Program.cs or service extensions
services.AddSingleton<IPathValidator, PathValidator>();
```

---

## Dependencies

### Depends On

- **Phase 025**: Tool Registration Infrastructure - MCP tool registration patterns
- **Phase 068**: Indexing Service - `IIndexingService` for document indexing operations
- **Phase 027**: Error Responses - Standard error response format and codes
- **Phase 038**: Tenant Context - `IProjectContext` for active project validation
- **Phase 029**: Embedding Service - Underlying embedding generation

### Blocks

- Skills that create documents and need immediate indexing confirmation
- Integration tests for manual document indexing workflow

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Tools/IndexToolsTests.cs
public class IndexToolsTests
{
    [Fact]
    public async Task IndexDocument_WithValidPath_ReturnsIndexedStatus()
    {
        // Arrange
        var mockIndexingService = new Mock<IIndexingService>();
        mockIndexingService
            .Setup(s => s.IndexDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexingResult("test.md", 1024, false, 0));

        var mockProjectContext = new Mock<IProjectContext>();
        mockProjectContext.Setup(c => c.IsActivated).Returns(true);
        mockProjectContext.Setup(c => c.CompoundingDocsRoot).Returns("/tmp/docs");

        var mockPathValidator = new Mock<IPathValidator>();
        mockPathValidator
            .Setup(v => v.ValidateDocumentPath("problems/test.md"))
            .Returns(PathValidationResult.Success("problems/test.md"));

        // Create test file
        Directory.CreateDirectory("/tmp/docs/problems");
        File.WriteAllText("/tmp/docs/problems/test.md", "# Test");

        var tool = new IndexTools(
            mockIndexingService.Object,
            mockProjectContext.Object,
            mockPathValidator.Object,
            Mock.Of<ILogger<IndexTools>>());

        // Act
        var result = await tool.IndexDocument("problems/test.md");

        // Assert
        var response = JsonSerializer.Deserialize<IndexDocumentResponse>(result);
        Assert.Equal("indexed", response.Status);
        Assert.Equal(1024, response.EmbeddingDimensions);
    }

    [Fact]
    public async Task IndexDocument_WithoutActiveProject_ReturnsProjectNotActivated()
    {
        // Arrange
        var mockProjectContext = new Mock<IProjectContext>();
        mockProjectContext.Setup(c => c.IsActivated).Returns(false);

        var tool = new IndexTools(
            Mock.Of<IIndexingService>(),
            mockProjectContext.Object,
            Mock.Of<IPathValidator>(),
            Mock.Of<ILogger<IndexTools>>());

        // Act
        var result = await tool.IndexDocument("test.md");

        // Assert
        var response = JsonSerializer.Deserialize<ToolErrorResponse>(result);
        Assert.True(response.Error);
        Assert.Equal("PROJECT_NOT_ACTIVATED", response.Code);
    }

    [Fact]
    public async Task IndexDocument_WithPathTraversal_ReturnsFileSystemError()
    {
        // Arrange
        var mockProjectContext = new Mock<IProjectContext>();
        mockProjectContext.Setup(c => c.IsActivated).Returns(true);

        var pathValidator = new PathValidator(Mock.Of<ILogger<PathValidator>>());

        var tool = new IndexTools(
            Mock.Of<IIndexingService>(),
            mockProjectContext.Object,
            pathValidator,
            Mock.Of<ILogger<IndexTools>>());

        // Act
        var result = await tool.IndexDocument("../../../etc/passwd.md");

        // Assert
        var response = JsonSerializer.Deserialize<ToolErrorResponse>(result);
        Assert.True(response.Error);
        Assert.Equal("FILE_SYSTEM_ERROR", response.Code);
        Assert.Contains("traversal", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IndexDocument_WithNonExistentFile_ReturnsDocumentNotFound()
    {
        // Arrange
        var mockProjectContext = new Mock<IProjectContext>();
        mockProjectContext.Setup(c => c.IsActivated).Returns(true);
        mockProjectContext.Setup(c => c.CompoundingDocsRoot).Returns("/tmp/docs");

        var mockPathValidator = new Mock<IPathValidator>();
        mockPathValidator
            .Setup(v => v.ValidateDocumentPath("missing.md"))
            .Returns(PathValidationResult.Success("missing.md"));

        var tool = new IndexTools(
            Mock.Of<IIndexingService>(),
            mockProjectContext.Object,
            mockPathValidator.Object,
            Mock.Of<ILogger<IndexTools>>());

        // Act
        var result = await tool.IndexDocument("missing.md");

        // Assert
        var response = JsonSerializer.Deserialize<ToolErrorResponse>(result);
        Assert.True(response.Error);
        Assert.Equal("DOCUMENT_NOT_FOUND", response.Code);
    }

    [Fact]
    public async Task IndexDocument_WhenReindexing_IsIdempotent()
    {
        // Arrange
        var indexingResults = new List<IndexingResult>();
        var mockIndexingService = new Mock<IIndexingService>();
        mockIndexingService
            .Setup(s => s.IndexDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexingResult("test.md", 1024, true, 0))
            .Callback<string, CancellationToken>((p, _) =>
                indexingResults.Add(new IndexingResult(p, 1024, true, 0)));

        var mockProjectContext = new Mock<IProjectContext>();
        mockProjectContext.Setup(c => c.IsActivated).Returns(true);
        mockProjectContext.Setup(c => c.CompoundingDocsRoot).Returns("/tmp/docs");

        var mockPathValidator = new Mock<IPathValidator>();
        mockPathValidator
            .Setup(v => v.ValidateDocumentPath(It.IsAny<string>()))
            .Returns(PathValidationResult.Success("test.md"));

        // Create test file
        Directory.CreateDirectory("/tmp/docs");
        File.WriteAllText("/tmp/docs/test.md", "# Test");

        var tool = new IndexTools(
            mockIndexingService.Object,
            mockProjectContext.Object,
            mockPathValidator.Object,
            Mock.Of<ILogger<IndexTools>>());

        // Act - index twice
        var result1 = await tool.IndexDocument("test.md");
        var result2 = await tool.IndexDocument("test.md");

        // Assert - both succeed with same result
        var response1 = JsonSerializer.Deserialize<IndexDocumentResponse>(result1);
        var response2 = JsonSerializer.Deserialize<IndexDocumentResponse>(result2);
        Assert.Equal(response1.Status, response2.Status);
        Assert.Equal(response1.EmbeddingDimensions, response2.EmbeddingDimensions);
    }
}
```

### Path Validator Tests

```csharp
// tests/CompoundDocs.Tests/Services/PathValidatorTests.cs
public class PathValidatorTests
{
    private readonly PathValidator _validator;

    public PathValidatorTests()
    {
        _validator = new PathValidator(Mock.Of<ILogger<PathValidator>>());
    }

    [Theory]
    [InlineData("problems/test.md", "problems/test.md")]
    [InlineData("./csharp-compounding-docs/problems/test.md", "problems/test.md")]
    [InlineData("csharp-compounding-docs/insights/test.md", "insights/test.md")]
    [InlineData("/problems/test.md", "problems/test.md")]  // Leading slash stripped
    public void ValidateDocumentPath_ValidPaths_ReturnsSuccess(string input, string expected)
    {
        var result = _validator.ValidateDocumentPath(input);

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.NormalizedPath);
    }

    [Theory]
    [InlineData("../secret.md")]
    [InlineData("problems/../../../etc/passwd.md")]
    [InlineData("foo/../../bar.md")]
    public void ValidateDocumentPath_PathTraversal_ReturnsFailure(string input)
    {
        var result = _validator.ValidateDocumentPath(input);

        Assert.False(result.IsValid);
        Assert.Contains("traversal", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void ValidateDocumentPath_EmptyPath_ReturnsFailure(string input)
    {
        var result = _validator.ValidateDocumentPath(input);

        Assert.False(result.IsValid);
        Assert.Contains("empty", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("test.txt")]
    [InlineData("test.json")]
    [InlineData("test")]
    public void ValidateDocumentPath_NonMarkdown_ReturnsFailure(string input)
    {
        var result = _validator.ValidateDocumentPath(input);

        Assert.False(result.IsValid);
        Assert.Contains(".md", result.Error);
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Tools/IndexDocumentIntegrationTests.cs
[Trait("Category", "Integration")]
public class IndexDocumentIntegrationTests : IClassFixture<McpServerFixture>
{
    private readonly McpServerFixture _fixture;

    public IndexDocumentIntegrationTests(McpServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task IndexDocument_WithRealOllama_IndexesSuccessfully()
    {
        // Arrange
        await _fixture.ActivateProjectAsync();
        var testDoc = await _fixture.CreateTestDocumentAsync("problems/integration-test.md", @"---
title: Integration Test Document
date: 2025-01-24
tags: [test]
---

# Integration Test

This document tests the index_document tool with real Ollama embeddings.
");

        try
        {
            // Act
            var result = await _fixture.InvokeToolAsync("index_document", new
            {
                path = "problems/integration-test.md"
            });

            // Assert
            Assert.Equal("indexed", result.Status);
            Assert.Equal(1024, result.EmbeddingDimensions);
        }
        finally
        {
            await _fixture.DeleteTestDocumentAsync(testDoc);
        }
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Tools/IndexTools.cs` | Create | Tool class with `index_document` method |
| `src/CompoundDocs.McpServer/Models/IndexDocumentResponse.cs` | Create | Success response DTO |
| `src/CompoundDocs.McpServer/Services/IPathValidator.cs` | Create | Path validation interface |
| `src/CompoundDocs.McpServer/Services/PathValidator.cs` | Create | Path validation implementation |
| `src/CompoundDocs.McpServer/Program.cs` | Modify | Register PathValidator in DI |
| `tests/CompoundDocs.Tests/Tools/IndexToolsTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.Tests/Services/PathValidatorTests.cs` | Create | Path validator unit tests |
| `tests/CompoundDocs.IntegrationTests/Tools/IndexDocumentIntegrationTests.cs` | Create | Integration tests |

---

## Security Considerations

| Threat | Mitigation |
|--------|------------|
| Path traversal attacks (`../`) | Path validator rejects any `..` segments |
| Absolute path injection | Rejects paths that are rooted or contain drive letters |
| Non-document indexing | Validates `.md` extension required |
| Access outside docs folder | Path normalization and containment check |
| Symlink attacks | Use `Path.GetFullPath` to resolve symlinks before validation |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Ollama unavailable | Return clear error message; tool is fallback anyway |
| Large document timeout | Inherit timeout from embedding service (5 min) |
| Concurrent indexing of same doc | Indexing service handles upsert semantics |
| Invalid frontmatter | Schema validation with clear error messages |
| File locked by editor | Retry with backoff; log warning |

---

## Notes

- This tool is explicitly a **fallback** mechanism as noted in the spec: "File watcher should handle most cases, but skills may call this for immediate confirmation"
- The tool is commonly called by skills after creating new documents to ensure they are immediately searchable
- Idempotent design means calling the tool multiple times on the same document is safe and produces consistent results
- Path validation is critical for security; the validator rejects any attempt to access files outside the compounding docs directory
- The response includes `embedding_dimensions` (always 1024) to confirm the embedding model is correctly configured
