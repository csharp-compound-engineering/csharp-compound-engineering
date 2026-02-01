# Phase 077: delete_documents MCP Tool

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: MCP Tools
> **Prerequisites**: Phase 025 (MCP Tool Registration System), Phase 038 (Multi-Tenant Context Service)

---

## Spec References

This phase implements the `delete_documents` tool defined in:

- **spec/mcp-server/tools.md** - [Delete Documents Tool](../spec/mcp-server/tools.md#7-delete-documents-tool) (parameters, response, behavior)
- **spec/mcp-server/tools.md** - [Error Handling](../spec/mcp-server/tools.md#error-handling) (standard error response format)
- **spec/infrastructure/cleanup-app.md** - Cascade deletion patterns (chunks with parent)

---

## Objectives

1. Implement the `delete_documents` MCP tool with attribute-based registration
2. Support deletion scopes: project, branch, and path_hash filtering
3. Implement dry-run preview mode for safe operation
4. Enable cascade deletion of document chunks with parent documents
5. Add tenant context filtering to ensure multi-tenant isolation
6. Implement confirmation workflow via dry-run first pattern
7. Create comprehensive deletion logging and audit trail

---

## Acceptance Criteria

### Tool Registration

- [ ] `[McpServerTool(Name = "delete_documents")]` attribute applied to tool method
- [ ] Tool method in `DocumentTools.cs` class (per Phase 025 organization)
- [ ] `[Description]` attributes on method and all parameters
- [ ] Tool appears in `tools/list` MCP response with correct schema
- [ ] All parameters are properly typed for JSON schema generation

### Parameter Definition

- [ ] `project_name` parameter (string, required) - Project identifier
- [ ] `branch_name` parameter (string, optional) - Branch name filter
- [ ] `path_hash` parameter (string, optional) - Path hash filter
- [ ] `dry_run` parameter (bool, optional, default: false) - Preview mode flag

### Tenant Context Filtering

- [ ] Tool validates project context is active
- [ ] Deletion queries filter by compound key (project_name, branch_name, path_hash)
- [ ] Users can only delete documents within their tenant context
- [ ] Cross-tenant deletion is prevented with appropriate error

### Dry-Run Preview Mode

- [ ] When `dry_run=true`, returns counts without performing deletion
- [ ] Response includes `status: "preview"` for dry-run results
- [ ] Preview shows document count and chunk count that would be deleted
- [ ] No database modifications occur in dry-run mode

### Cascade Deletion

- [ ] Documents are deleted from the `documents` collection
- [ ] Associated chunks are deleted from `document_chunks` collection
- [ ] Chunks are deleted before documents (proper cascade order)
- [ ] All deletions within a request occur in a single transaction
- [ ] Transaction is rolled back on any failure

### Confirmation Workflow

- [ ] Tool design supports "dry_run first, then confirm" pattern
- [ ] Response includes information needed for user confirmation
- [ ] Clear distinction between preview and actual deletion results

### Deletion Logging

- [ ] Each deletion operation is logged with structured logging
- [ ] Log entries include: timestamp, project, branch, path_hash, counts
- [ ] Warning level for actual deletions, Information for dry-run
- [ ] Errors are logged with full exception details

---

## Implementation Notes

### Tool Method Implementation

```csharp
// src/CompoundDocs.McpServer/Tools/DocumentTools.cs
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

[McpServerToolType]
public partial class DocumentTools
{
    private readonly IDocumentDeletionService _deletionService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DocumentTools> _logger;

    public DocumentTools(
        IDocumentDeletionService deletionService,
        ITenantContext tenantContext,
        ILogger<DocumentTools> logger)
    {
        _deletionService = deletionService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    [McpServerTool(Name = "delete_documents")]
    [Description("Delete compounding docs from the database by project, branch, or path. Use dry_run=true to preview counts before deleting.")]
    public async Task<string> DeleteDocuments(
        [Description("Project identifier (required)")] string project_name,
        [Description("Branch name - if omitted, deletes all branches")] string? branch_name = null,
        [Description("Path hash - if omitted, deletes all paths")] string? path_hash = null,
        [Description("If true, return counts without deleting (default: false)")] bool dry_run = false,
        CancellationToken cancellationToken = default)
    {
        // Validate project context is active
        if (!_tenantContext.IsInitialized)
        {
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: ToolErrorCodes.ProjectNotActivated,
                Message: "No project is currently activated. Call activate_project first.",
                Details: new { requiredTool = "activate_project" }));
        }

        // Validate project_name matches active context
        if (!string.Equals(project_name, _tenantContext.ProjectName, StringComparison.Ordinal))
        {
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: "INVALID_PROJECT",
                Message: $"Cannot delete from project '{project_name}'. Active project is '{_tenantContext.ProjectName}'.",
                Details: new { activeProject = _tenantContext.ProjectName, requestedProject = project_name }));
        }

        try
        {
            var request = new DeleteDocumentsRequest(
                ProjectName: project_name,
                BranchName: branch_name,
                PathHash: path_hash,
                DryRun: dry_run);

            var result = await _deletionService.DeleteDocumentsAsync(request, cancellationToken);

            if (dry_run)
            {
                _logger.LogInformation(
                    "Dry-run deletion preview for project={Project}, branch={Branch}, path_hash={PathHash}: " +
                    "{DocumentCount} documents, {ChunkCount} chunks would be deleted",
                    project_name, branch_name ?? "(all)", path_hash ?? "(all)",
                    result.DeletedCount, result.DeletedChunks);
            }
            else
            {
                _logger.LogWarning(
                    "Deleted documents from project={Project}, branch={Branch}, path_hash={PathHash}: " +
                    "{DocumentCount} documents, {ChunkCount} chunks deleted",
                    project_name, branch_name ?? "(all)", path_hash ?? "(all)",
                    result.DeletedCount, result.DeletedChunks);
            }

            return JsonSerializer.Serialize(new DeleteDocumentsResponse(
                Status: dry_run ? "preview" : "deleted",
                DeletedCount: result.DeletedCount,
                DeletedChunks: result.DeletedChunks,
                ProjectName: project_name,
                BranchName: branch_name,
                PathHash: path_hash,
                DryRun: dry_run));
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error during document deletion for project={Project}", project_name);
            return JsonSerializer.Serialize(new ToolErrorResponse(
                Error: true,
                Code: ToolErrorCodes.DatabaseError,
                Message: "Failed to delete documents due to database error.",
                Details: new { innerMessage = ex.Message }));
        }
    }
}
```

### Response DTOs

```csharp
// src/CompoundDocs.McpServer/Models/DeleteDocumentsResponse.cs
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Response from delete_documents tool.
/// </summary>
/// <param name="Status">"deleted" for actual deletion, "preview" for dry-run.</param>
/// <param name="DeletedCount">Number of documents deleted (or would be deleted).</param>
/// <param name="DeletedChunks">Number of chunks deleted (or would be deleted).</param>
/// <param name="ProjectName">Project that was targeted.</param>
/// <param name="BranchName">Branch filter (null if all branches).</param>
/// <param name="PathHash">Path hash filter (null if all paths).</param>
/// <param name="DryRun">Whether this was a dry-run.</param>
public record DeleteDocumentsResponse(
    string Status,
    int DeletedCount,
    int DeletedChunks,
    string ProjectName,
    string? BranchName,
    string? PathHash,
    bool DryRun);
```

### Deletion Service Interface

```csharp
// src/CompoundDocs.McpServer/Services/IDocumentDeletionService.cs
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Service for deleting documents and associated chunks.
/// </summary>
public interface IDocumentDeletionService
{
    /// <summary>
    /// Deletes documents matching the filter criteria.
    /// </summary>
    /// <param name="request">Deletion request with filters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with deletion counts.</returns>
    Task<DeletionResult> DeleteDocumentsAsync(
        DeleteDocumentsRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request parameters for document deletion.
/// </summary>
public record DeleteDocumentsRequest(
    string ProjectName,
    string? BranchName,
    string? PathHash,
    bool DryRun);

/// <summary>
/// Result of a deletion operation.
/// </summary>
public record DeletionResult(
    int DeletedCount,
    int DeletedChunks);
```

### Deletion Service Implementation

```csharp
// src/CompoundDocs.McpServer/Services/DocumentDeletionService.cs
using Microsoft.Extensions.VectorData;
using Npgsql;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Implements document and chunk deletion with transactional cascade behavior.
/// </summary>
public sealed class DocumentDeletionService : IDocumentDeletionService
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<DocumentDeletionService> _logger;

    public DocumentDeletionService(
        NpgsqlDataSource dataSource,
        ILogger<DocumentDeletionService> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task<DeletionResult> DeleteDocumentsAsync(
        DeleteDocumentsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ProjectName);

        if (request.DryRun)
        {
            return await GetDeletionCountsAsync(request, cancellationToken);
        }

        return await ExecuteDeletionAsync(request, cancellationToken);
    }

    private async Task<DeletionResult> GetDeletionCountsAsync(
        DeleteDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        // Build WHERE clause based on filters
        var (whereClause, parameters) = BuildWhereClause(request);

        // Count documents
        var docCountSql = $"SELECT COUNT(*) FROM documents {whereClause}";
        await using var docCmd = new NpgsqlCommand(docCountSql, connection);
        AddParameters(docCmd, parameters);
        var docCount = Convert.ToInt32(await docCmd.ExecuteScalarAsync(cancellationToken));

        // Count chunks (join to get matching documents' chunks)
        var chunkCountSql = $@"
            SELECT COUNT(*) FROM document_chunks dc
            INNER JOIN documents d ON dc.document_id = d.id
            {whereClause.Replace("WHERE", "WHERE d.")}";
        await using var chunkCmd = new NpgsqlCommand(chunkCountSql, connection);
        AddParameters(chunkCmd, parameters);
        var chunkCount = Convert.ToInt32(await chunkCmd.ExecuteScalarAsync(cancellationToken));

        return new DeletionResult(docCount, chunkCount);
    }

    private async Task<DeletionResult> ExecuteDeletionAsync(
        DeleteDocumentsRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var (whereClause, parameters) = BuildWhereClause(request);

            // Delete chunks first (cascade order: children before parents)
            var deleteChunksSql = $@"
                DELETE FROM document_chunks dc
                USING documents d
                WHERE dc.document_id = d.id
                AND d.{whereClause.Replace("WHERE ", "")}";
            await using var chunkCmd = new NpgsqlCommand(deleteChunksSql, connection, transaction);
            AddParameters(chunkCmd, parameters);
            var chunksDeleted = await chunkCmd.ExecuteNonQueryAsync(cancellationToken);

            // Delete documents
            var deleteDocsSql = $"DELETE FROM documents {whereClause}";
            await using var docCmd = new NpgsqlCommand(deleteDocsSql, connection, transaction);
            AddParameters(docCmd, parameters);
            var docsDeleted = await docCmd.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully deleted {DocumentCount} documents and {ChunkCount} chunks for project={Project}",
                docsDeleted, chunksDeleted, request.ProjectName);

            return new DeletionResult(docsDeleted, chunksDeleted);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to delete documents, transaction rolled back");
            throw;
        }
    }

    private static (string WhereClause, Dictionary<string, object> Parameters) BuildWhereClause(
        DeleteDocumentsRequest request)
    {
        var conditions = new List<string> { "project_name = @projectName" };
        var parameters = new Dictionary<string, object>
        {
            ["@projectName"] = request.ProjectName
        };

        if (!string.IsNullOrEmpty(request.BranchName))
        {
            conditions.Add("branch_name = @branchName");
            parameters["@branchName"] = request.BranchName;
        }

        if (!string.IsNullOrEmpty(request.PathHash))
        {
            conditions.Add("path_hash = @pathHash");
            parameters["@pathHash"] = request.PathHash;
        }

        var whereClause = $"WHERE {string.Join(" AND ", conditions)}";
        return (whereClause, parameters);
    }

    private static void AddParameters(NpgsqlCommand cmd, Dictionary<string, object> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            cmd.Parameters.AddWithValue(name, value);
        }
    }
}
```

### Service Registration

```csharp
// In DI registration (Program.cs or extension method)
services.AddScoped<IDocumentDeletionService, DocumentDeletionService>();
```

### Example Usage Flow

The expected usage pattern for the `delete_documents` tool follows a confirmation workflow:

```json
// Step 1: Skills call with dry_run=true to preview
{
  "tool": "delete_documents",
  "arguments": {
    "project_name": "my-project",
    "branch_name": "feature/old-branch",
    "dry_run": true
  }
}

// Response shows what would be deleted
{
  "status": "preview",
  "deleted_count": 34,
  "deleted_chunks": 12,
  "project_name": "my-project",
  "branch_name": "feature/old-branch",
  "path_hash": null,
  "dry_run": true
}

// Step 2: After user confirmation, call with dry_run=false
{
  "tool": "delete_documents",
  "arguments": {
    "project_name": "my-project",
    "branch_name": "feature/old-branch",
    "dry_run": false
  }
}

// Response confirms deletion
{
  "status": "deleted",
  "deleted_count": 34,
  "deleted_chunks": 12,
  "project_name": "my-project",
  "branch_name": "feature/old-branch",
  "path_hash": null,
  "dry_run": false
}
```

---

## Dependencies

### Depends On

- **Phase 025**: MCP Tool Registration System (provides `[McpServerToolType]`, `[McpServerTool]` attributes)
- **Phase 038**: Multi-Tenant Context Service (provides `ITenantContext` for tenant validation)
- **Phase 042**: CompoundDocument Model (document entity to delete)
- **Phase 043**: DocumentChunk Model (chunk entity to cascade delete)
- **Phase 027**: Error Responses (standard error response format)

### Blocks

- **Phase 078+**: Any phase requiring document management workflow
- **Skill Implementation**: Skills using delete_documents for cleanup operations

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Tools/DeleteDocumentsToolTests.cs
using System.Text.Json;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Services;
using CompoundDocs.McpServer.Tools;
using Moq;

namespace CompoundDocs.Tests.Tools;

public class DeleteDocumentsToolTests
{
    private readonly Mock<IDocumentDeletionService> _deletionServiceMock;
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly Mock<ILogger<DocumentTools>> _loggerMock;
    private readonly DocumentTools _sut;

    public DeleteDocumentsToolTests()
    {
        _deletionServiceMock = new Mock<IDocumentDeletionService>();
        _tenantContextMock = new Mock<ITenantContext>();
        _loggerMock = new Mock<ILogger<DocumentTools>>();

        _sut = new DocumentTools(
            _deletionServiceMock.Object,
            _tenantContextMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task DeleteDocuments_WhenProjectNotActivated_ReturnsError()
    {
        // Arrange
        _tenantContextMock.Setup(x => x.IsInitialized).Returns(false);

        // Act
        var result = await _sut.DeleteDocuments("my-project");
        var response = JsonSerializer.Deserialize<ToolErrorResponse>(result);

        // Assert
        Assert.True(response!.Error);
        Assert.Equal("PROJECT_NOT_ACTIVATED", response.Code);
    }

    [Fact]
    public async Task DeleteDocuments_WhenProjectMismatch_ReturnsError()
    {
        // Arrange
        _tenantContextMock.Setup(x => x.IsInitialized).Returns(true);
        _tenantContextMock.Setup(x => x.ProjectName).Returns("different-project");

        // Act
        var result = await _sut.DeleteDocuments("my-project");
        var response = JsonSerializer.Deserialize<ToolErrorResponse>(result);

        // Assert
        Assert.True(response!.Error);
        Assert.Equal("INVALID_PROJECT", response.Code);
    }

    [Fact]
    public async Task DeleteDocuments_DryRun_ReturnsPreviewStatus()
    {
        // Arrange
        _tenantContextMock.Setup(x => x.IsInitialized).Returns(true);
        _tenantContextMock.Setup(x => x.ProjectName).Returns("my-project");
        _deletionServiceMock
            .Setup(x => x.DeleteDocumentsAsync(It.IsAny<DeleteDocumentsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeletionResult(10, 5));

        // Act
        var result = await _sut.DeleteDocuments("my-project", dry_run: true);
        var response = JsonSerializer.Deserialize<DeleteDocumentsResponse>(result);

        // Assert
        Assert.Equal("preview", response!.Status);
        Assert.Equal(10, response.DeletedCount);
        Assert.Equal(5, response.DeletedChunks);
        Assert.True(response.DryRun);
    }

    [Fact]
    public async Task DeleteDocuments_ActualDelete_ReturnsDeletedStatus()
    {
        // Arrange
        _tenantContextMock.Setup(x => x.IsInitialized).Returns(true);
        _tenantContextMock.Setup(x => x.ProjectName).Returns("my-project");
        _deletionServiceMock
            .Setup(x => x.DeleteDocumentsAsync(It.IsAny<DeleteDocumentsRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeletionResult(10, 5));

        // Act
        var result = await _sut.DeleteDocuments("my-project", dry_run: false);
        var response = JsonSerializer.Deserialize<DeleteDocumentsResponse>(result);

        // Assert
        Assert.Equal("deleted", response!.Status);
        Assert.False(response.DryRun);
    }

    [Fact]
    public async Task DeleteDocuments_WithBranchFilter_PassesFilterToService()
    {
        // Arrange
        _tenantContextMock.Setup(x => x.IsInitialized).Returns(true);
        _tenantContextMock.Setup(x => x.ProjectName).Returns("my-project");
        _deletionServiceMock
            .Setup(x => x.DeleteDocumentsAsync(
                It.Is<DeleteDocumentsRequest>(r => r.BranchName == "feature/test"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeletionResult(5, 2));

        // Act
        var result = await _sut.DeleteDocuments("my-project", branch_name: "feature/test");

        // Assert
        _deletionServiceMock.Verify(x => x.DeleteDocumentsAsync(
            It.Is<DeleteDocumentsRequest>(r => r.BranchName == "feature/test"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.Tests/Integration/DeleteDocumentsIntegrationTests.cs
namespace CompoundDocs.Tests.Integration;

public class DeleteDocumentsIntegrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public DeleteDocumentsIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task DeleteDocuments_CascadesChunkDeletion()
    {
        // Arrange: Create document with chunks
        var document = await _fixture.CreateTestDocumentAsync("test-project", "main");
        await _fixture.CreateTestChunksAsync(document.Id, chunkCount: 3);

        var service = new DocumentDeletionService(_fixture.DataSource, NullLogger<DocumentDeletionService>.Instance);

        // Act
        var result = await service.DeleteDocumentsAsync(new DeleteDocumentsRequest(
            ProjectName: "test-project",
            BranchName: "main",
            PathHash: null,
            DryRun: false));

        // Assert
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(3, result.DeletedChunks);
        Assert.False(await _fixture.DocumentExistsAsync(document.Id));
        Assert.False(await _fixture.ChunksExistForDocumentAsync(document.Id));
    }

    [Fact]
    public async Task DeleteDocuments_DryRun_DoesNotDelete()
    {
        // Arrange
        var document = await _fixture.CreateTestDocumentAsync("test-project", "main");
        await _fixture.CreateTestChunksAsync(document.Id, chunkCount: 2);

        var service = new DocumentDeletionService(_fixture.DataSource, NullLogger<DocumentDeletionService>.Instance);

        // Act
        var result = await service.DeleteDocumentsAsync(new DeleteDocumentsRequest(
            ProjectName: "test-project",
            BranchName: "main",
            PathHash: null,
            DryRun: true));

        // Assert
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(2, result.DeletedChunks);
        Assert.True(await _fixture.DocumentExistsAsync(document.Id)); // Still exists
        Assert.True(await _fixture.ChunksExistForDocumentAsync(document.Id)); // Still exist
    }

    [Fact]
    public async Task DeleteDocuments_TransactionRollbackOnFailure()
    {
        // This test verifies that if chunk deletion fails, documents are not deleted
        // Implementation depends on ability to inject/simulate failure
    }
}
```

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `src/CompoundDocs.McpServer/Tools/DocumentTools.cs` | Tool class with `delete_documents` method (or add to existing) |
| `src/CompoundDocs.McpServer/Models/DeleteDocumentsResponse.cs` | Response DTO |
| `src/CompoundDocs.McpServer/Services/IDocumentDeletionService.cs` | Deletion service interface |
| `src/CompoundDocs.McpServer/Services/DocumentDeletionService.cs` | Deletion service implementation |
| `tests/CompoundDocs.Tests/Tools/DeleteDocumentsToolTests.cs` | Unit tests |
| `tests/CompoundDocs.Tests/Integration/DeleteDocumentsIntegrationTests.cs` | Integration tests |

### Modified Files

| File | Changes |
|------|---------|
| `src/CompoundDocs.McpServer/Program.cs` | Register `IDocumentDeletionService` in DI |

---

## Verification Steps

After completing this phase, verify:

1. **Tool Discovery**: Run MCP server and verify `delete_documents` appears in `tools/list`
2. **Parameter Schema**: Verify JSON schema shows all parameters with correct types and descriptions
3. **Dry-Run Preview**: Call with `dry_run=true` and verify no data is modified
4. **Cascade Deletion**: Delete a document and verify associated chunks are also deleted
5. **Transaction Safety**: Simulate failure and verify partial deletions are rolled back
6. **Tenant Isolation**: Verify attempts to delete from wrong project return error
7. **Logging**: Verify deletion operations produce structured log entries

### Manual Verification Commands

```bash
# List tools to verify registration
mcp-cli list-tools --stdio "dotnet run --project src/CompoundDocs.McpServer"

# Call delete_documents in dry-run mode
mcp-cli call-tool delete_documents \
  --param project_name=test-project \
  --param branch_name=old-branch \
  --param dry_run=true \
  --stdio "dotnet run --project src/CompoundDocs.McpServer"
```

---

## Notes

- This tool is destructive; always encourage dry-run first pattern in skill implementations
- Consider adding a `--force` flag in future for bypassing confirmation in automated scenarios
- The tool only deletes from the currently active project for safety
- Future enhancement: Consider soft-delete with TTL for recovery window
- Error handling should never expose internal database details to clients
- All parameter names use snake_case per MCP convention
