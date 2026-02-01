# Phase 148: Security Review

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: Final Integration
> **Prerequisites**: Phase 080 (Tool Parameter Validation Framework)

---

## Spec References

This phase validates security controls implemented across the system as defined in:

- **spec/mcp-server/tools.md** - Parameter validation, path handling, tool-specific security constraints
- **spec/observability.md** - Sensitive data handling, logging restrictions (lines 175-204)

---

## Objectives

1. Validate path traversal prevention is effective across all tools accepting path inputs
2. Review input sanitization implementation against the validation framework from Phase 080
3. Confirm no credentials are stored in configuration, memory, or persisted state
4. Verify sensitive data is never logged per observability spec requirements
5. Validate multi-tenant isolation ensures data cannot leak between projects/branches

---

## Acceptance Criteria

### Path Traversal Prevention

- [ ] `NoPathTraversalAttribute` blocks all traversal patterns: `../`, `..\\`, `..`, `%2e%2e`, `%2e%2e%2f`
- [ ] URL-encoded traversal variants detected and blocked: `%2e`, `%2f`, mixed case
- [ ] Null byte injection blocked: `file.md%00.txt`
- [ ] Unicode/UTF-8 encoding bypasses blocked: `\u002e\u002e/`
- [ ] Tools validate paths remain within allowed boundaries:
  - [ ] `index_document`: paths resolve within `./csharp-compounding-docs/`
  - [ ] `update_promotion_level`: paths resolve within `./csharp-compounding-docs/`
  - [ ] `activate_project`: config paths validated as existing `.csharp-compounding-docs/config.json`
- [ ] Symlink resolution does not escape allowed directories
- [ ] Path normalization removes redundant separators and `.` segments before validation
- [ ] Security tests attempt known bypass techniques and verify rejection

### Input Sanitization Review

- [ ] All 9 MCP tools have complete validation schemas in `ToolParameterSchemas`
- [ ] Required parameters validated as non-null and non-empty
- [ ] String length limits enforced to prevent memory exhaustion:
  - [ ] `query` parameters: max 10,000 characters
  - [ ] `path` parameters: max 1,000 characters
  - [ ] `project_name`: max 255 characters
  - [ ] `branch_name`: max 255 characters
- [ ] Numeric parameters bounded to prevent integer overflow:
  - [ ] `max_sources`: 1-10
  - [ ] `limit`: 1-100
  - [ ] `min_relevance_score`: 0.0-1.0
- [ ] Array parameters bounded to prevent resource exhaustion:
  - [ ] `doc_types`: max 20 elements
  - [ ] `promotion_levels`: max 3 elements
- [ ] Enum parameters validated against allowed values (case-insensitive)
- [ ] No raw SQL injection possible (parameterized queries only)
- [ ] No code injection vectors in tool parameters

### Credential Handling Verification

- [ ] Global config (`appsettings.json`) contains no secrets
- [ ] Project config (`config.json`) contains no authentication tokens
- [ ] Environment variable secrets (if any) never written to disk
- [ ] No credentials persisted in PostgreSQL tables
- [ ] No API keys or tokens in memory beyond immediate use
- [ ] Connection strings use environment variable substitution, not hardcoded values
- [ ] Ollama connection uses local-only endpoint (no remote credentials needed)
- [ ] PostgreSQL connection string documented as requiring secure credential management
- [ ] No secrets in git history (verify no accidental commits)

### Sensitive Data Logging Prevention

- [ ] Document content NEVER logged (paths only) per spec/observability.md lines 177-189
- [ ] Embedding vectors NEVER logged (dimensions only)
- [ ] Full SQL queries with data NEVER logged (parameterized query templates only)
- [ ] User credentials/tokens NEVER logged
- [ ] Error messages sanitized before logging (no sensitive context)
- [ ] Request/response bodies not logged (tool names and timing only)
- [ ] Logging scopes do not include sensitive fields
- [ ] Debug/Trace levels follow same restrictions in production builds
- [ ] Stack traces sanitized (no parameter values in exception messages)
- [ ] Log sampling does not selectively expose sensitive data

Safe to log (verified present):
- [ ] File paths (relative to repo)
- [ ] Document metadata (title, type, char count)
- [ ] Operation timing
- [ ] Document IDs and hashes
- [ ] Error codes (not full messages with context)
- [ ] Correlation IDs

### Multi-Tenant Isolation Validation

- [ ] Tenant context (project_name, branch_name, path_hash) required for all data operations
- [ ] Vector searches filtered by tenant context before similarity scoring
- [ ] Document operations validate ownership before modification
- [ ] No cross-tenant data leakage in:
  - [ ] `rag_query` results
  - [ ] `semantic_search` results
  - [ ] `search_external_docs` results
  - [ ] `list_doc_types` results
  - [ ] `delete_documents` cascading deletes
- [ ] Session isolation: activating new project clears previous context completely
- [ ] File watcher only monitors paths within active tenant's repository
- [ ] External docs search respects tenant's configured `external_docs` path only
- [ ] Orphan cleanup does not affect other tenants' documents
- [ ] Promotion level updates only affect documents within tenant scope
- [ ] Critical document prepending only pulls from same tenant

---

## Security Test Scenarios

### Path Traversal Test Cases

```csharp
namespace CompoundDocs.McpServer.Tests.Security;

public class PathTraversalSecurityTests
{
    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("..\\windows\\system32\\config\\sam")]
    [InlineData("....//....//etc/passwd")]
    [InlineData("..%2f..%2fetc/passwd")]
    [InlineData("..%252f..%252fetc/passwd")]  // Double encoding
    [InlineData("%2e%2e/%2e%2e/etc/passwd")]
    [InlineData("..%c0%af..%c0%afetc/passwd")]  // UTF-8 overlong encoding
    [InlineData("problems/..\\..\\..\\etc\\passwd")]
    [InlineData("problems/../../../etc/passwd")]
    [InlineData("valid/path/..%00/etc/passwd")]  // Null byte
    [InlineData("problems/\u002e\u002e/secret")]  // Unicode
    public void PathTraversal_IsBlocked(string maliciousPath)
    {
        var validator = new NoPathTraversalAttribute();
        var context = new ValidationContext("test_tool", typeof(string), true);

        var result = validator.Validate("path", maliciousPath, context);

        Assert.False(result.IsValid);
        Assert.Contains("traversal", result.Errors[0].ErrorMessage.ToLowerInvariant());
    }

    [Fact]
    public void IndexDocument_PathStaysWithinBoundary()
    {
        var tool = BuildIndexDocumentTool();
        var repoRoot = "/home/user/project";

        // Even with escaped traversal, resolved path must be within boundary
        var result = tool.ValidateAndResolvePath(
            "problems/../../../etc/passwd",
            repoRoot);

        Assert.False(result.IsValid);
        Assert.Contains("outside allowed directory", result.Error);
    }
}
```

### Tenant Isolation Test Cases

```csharp
namespace CompoundDocs.McpServer.Tests.Security;

public class TenantIsolationSecurityTests
{
    [Fact]
    public async Task SemanticSearch_ReturnsOnlyTenantDocuments()
    {
        // Arrange: Two tenants with documents
        var tenant1 = new TenantContext("project-a", "main", "hash-a");
        var tenant2 = new TenantContext("project-b", "main", "hash-b");

        await _repository.UpsertAsync(CreateDocument("doc1", tenant1));
        await _repository.UpsertAsync(CreateDocument("doc2", tenant2));

        // Act: Search as tenant1
        var results = await _searchService.SearchAsync(
            "test query",
            tenant1,
            limit: 100);

        // Assert: Only tenant1's documents returned
        Assert.All(results, r => Assert.Equal(tenant1.PathHash, r.PathHash));
        Assert.DoesNotContain(results, r => r.PathHash == tenant2.PathHash);
    }

    [Fact]
    public async Task RagQuery_CriticalDocsFromSameTenantOnly()
    {
        // Arrange: Critical docs in different tenants
        var tenant1 = new TenantContext("project-a", "main", "hash-a");
        var tenant2 = new TenantContext("project-b", "main", "hash-b");

        await _repository.UpsertAsync(CreateCriticalDocument("critical-a", tenant1));
        await _repository.UpsertAsync(CreateCriticalDocument("critical-b", tenant2));

        // Act: RAG query as tenant1 with include_critical=true
        var result = await _ragService.QueryAsync(
            "test query",
            tenant1,
            includeCritical: true);

        // Assert: Only tenant1's critical docs prepended
        var criticalSources = result.Sources.Where(s => s.PromotionLevel == "critical");
        Assert.All(criticalSources, s => Assert.Equal(tenant1.PathHash, s.PathHash));
    }

    [Fact]
    public async Task DeleteDocuments_CannotDeleteOtherTenantDocs()
    {
        // Arrange
        var tenant1 = new TenantContext("project-a", "main", "hash-a");
        var tenant2 = new TenantContext("project-b", "main", "hash-b");

        var doc1 = await _repository.UpsertAsync(CreateDocument("doc1", tenant1));
        var doc2 = await _repository.UpsertAsync(CreateDocument("doc2", tenant2));

        // Act: Attempt to delete all with tenant1 context
        await _deleteService.DeleteAsync(
            projectName: "project-a",
            branchName: null,  // All branches
            pathHash: null,    // All paths
            context: tenant1);

        // Assert: Only tenant1 docs deleted
        Assert.Null(await _repository.GetAsync(doc1.Id, tenant1));
        Assert.NotNull(await _repository.GetAsync(doc2.Id, tenant2));  // Still exists
    }

    [Fact]
    public async Task ActivateProject_ClearsPreviousTenantContext()
    {
        // Arrange: Activate first project
        var session = new SessionState();
        await session.ActivateProjectAsync("/project-a/.csharp-compounding-docs/config.json", "main");

        var originalContext = session.CurrentTenant;

        // Act: Activate second project
        await session.ActivateProjectAsync("/project-b/.csharp-compounding-docs/config.json", "feature");

        // Assert: Context completely replaced
        Assert.NotEqual(originalContext.PathHash, session.CurrentTenant.PathHash);
        Assert.Equal("project-b", session.CurrentTenant.ProjectName);
        Assert.Equal("feature", session.CurrentTenant.BranchName);

        // File watcher should be on new path only
        Assert.DoesNotContain("/project-a/", session.ActiveWatchPaths);
        Assert.Contains("/project-b/", session.ActiveWatchPaths);
    }
}
```

### Logging Security Test Cases

```csharp
namespace CompoundDocs.McpServer.Tests.Security;

public class LoggingSecurityTests
{
    [Fact]
    public void DocumentIndexing_DoesNotLogContent()
    {
        var logCapture = new TestLoggerProvider();
        var logger = logCapture.CreateLogger<IndexingService>();

        var service = new IndexingService(logger, _embeddings, _repository);

        // Act: Index document with sensitive content
        await service.IndexAsync(new Document
        {
            Path = "secrets/api-keys.md",
            Content = "API_KEY=sk-secret123456789",
            // ...
        });

        // Assert: Content not in logs
        var allLogs = logCapture.GetAllLogMessages();
        Assert.DoesNotContain(allLogs, log => log.Contains("sk-secret"));
        Assert.DoesNotContain(allLogs, log => log.Contains("API_KEY"));

        // Path IS logged (expected)
        Assert.Contains(allLogs, log => log.Contains("secrets/api-keys.md"));
    }

    [Fact]
    public void EmbeddingService_DoesNotLogVectors()
    {
        var logCapture = new TestLoggerProvider();
        var logger = logCapture.CreateLogger<EmbeddingService>();

        var service = new EmbeddingService(logger, _ollama);

        // Act: Generate embedding
        var embedding = await service.GenerateAsync("test content");

        // Assert: Vector values not logged
        var allLogs = logCapture.GetAllLogMessages();
        Assert.DoesNotContain(allLogs, log =>
            log.Contains("[0.") ||   // Typical embedding start
            log.Contains("embedding:") && log.Contains(","));

        // Dimensions ARE logged (expected)
        Assert.Contains(allLogs, log => log.Contains("1024") || log.Contains("dimensions"));
    }

    [Fact]
    public void ErrorLogging_SanitizesParameters()
    {
        var logCapture = new TestLoggerProvider();
        var logger = logCapture.CreateLogger<RagQueryTool>();

        var tool = new RagQueryTool(logger, _services);

        // Act: Cause an error with sensitive query
        await Assert.ThrowsAsync<Exception>(() =>
            tool.ExecuteAsync(query: "What is the password for admin@company.com?"));

        // Assert: Query content not in error logs
        var errorLogs = logCapture.GetLogMessages(LogLevel.Error);
        Assert.DoesNotContain(errorLogs, log => log.Contains("password"));
        Assert.DoesNotContain(errorLogs, log => log.Contains("admin@company.com"));
    }
}
```

---

## Implementation Notes

### Enhanced Path Traversal Prevention

Phase 080 implements basic `NoPathTraversalAttribute`. Security review should verify or enhance with:

```csharp
namespace CompoundDocs.McpServer.Validation.Attributes;

/// <summary>
/// Enhanced path traversal prevention with encoding awareness.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class SecurePathAttribute : ValidationAttribute
{
    private static readonly string[] TraversalPatterns =
    [
        "..",           // Basic traversal
        "%2e%2e",       // URL encoded
        "%252e%252e",   // Double URL encoded
        "%c0%ae",       // UTF-8 overlong encoding for .
        "\\u002e",      // Unicode escape
        "%00",          // Null byte injection
    ];

    private readonly string? _allowedBasePath;

    public SecurePathAttribute(string? allowedBasePath = null)
    {
        _allowedBasePath = allowedBasePath;
    }

    public override ParameterValidationResult Validate(
        string parameterName, object? value, ValidationContext context)
    {
        if (value is not string path)
            return ParameterValidationResult.Success();

        // Step 1: Decode and check for traversal patterns at multiple levels
        var decodedPath = DecodeMultipleLevels(path, maxLevels: 3);

        foreach (var decoded in decodedPath)
        {
            if (ContainsTraversal(decoded))
            {
                return ParameterValidationResult.Failure(
                    new ValidationError(
                        parameterName,
                        $"{parameterName} contains invalid path traversal sequence",
                        value));
            }
        }

        // Step 2: If base path specified, verify resolved path stays within
        if (_allowedBasePath is not null)
        {
            var normalizedBase = Path.GetFullPath(_allowedBasePath);
            var normalizedPath = Path.GetFullPath(
                Path.Combine(normalizedBase, path));

            if (!normalizedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
            {
                return ParameterValidationResult.Failure(
                    new ValidationError(
                        parameterName,
                        $"{parameterName} resolves outside allowed directory",
                        value));
            }
        }

        return ParameterValidationResult.Success();
    }

    private static IEnumerable<string> DecodeMultipleLevels(string path, int maxLevels)
    {
        var current = path;
        yield return current;

        for (int i = 0; i < maxLevels; i++)
        {
            var decoded = Uri.UnescapeDataString(current);
            if (decoded == current)
                break;
            current = decoded;
            yield return current;
        }
    }

    private static bool ContainsTraversal(string path)
    {
        var normalized = path.ToLowerInvariant()
            .Replace('\\', '/')
            .Replace("//", "/");

        // Check for .. at start, end, or surrounded by separators
        var segments = normalized.Split('/');
        return segments.Any(s => s == ".." || s.Contains(".."));
    }
}
```

### Tenant Filter Injection Pattern

Ensure all queries include tenant filtering:

```csharp
namespace CompoundDocs.Core.Data;

/// <summary>
/// Query builder extension ensuring tenant isolation.
/// </summary>
public static class TenantQueryExtensions
{
    /// <summary>
    /// Applies mandatory tenant filter to query.
    /// Should be called before any other filters.
    /// </summary>
    public static IQueryable<T> ForTenant<T>(
        this IQueryable<T> query,
        TenantContext tenant) where T : ITenantScoped
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant.PathHash);

        return query
            .Where(x => x.ProjectName == tenant.ProjectName)
            .Where(x => x.BranchName == tenant.BranchName)
            .Where(x => x.PathHash == tenant.PathHash);
    }
}
```

### Logging Sanitization Helper

```csharp
namespace CompoundDocs.Core.Observability;

/// <summary>
/// Sanitizes values for safe logging per observability spec.
/// </summary>
public static class LogSanitizer
{
    /// <summary>
    /// Sanitizes a query for logging (truncates, removes potential secrets).
    /// </summary>
    public static string SanitizeQuery(string? query)
    {
        if (string.IsNullOrEmpty(query))
            return "[empty]";

        // Truncate for log readability
        var truncated = query.Length > 100
            ? query[..100] + "..."
            : query;

        // Replace potential sensitive patterns
        return Regex.Replace(truncated,
            @"(password|secret|key|token|auth)[=:\s]*\S+",
            "$1=[REDACTED]",
            RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Returns only safe document metadata for logging.
    /// </summary>
    public static object SafeDocumentInfo(Document doc) => new
    {
        doc.Path,
        doc.Title,
        doc.DocType,
        CharCount = doc.Content?.Length ?? 0,
        doc.PromotionLevel,
        doc.PathHash
        // NOTE: Content, Embedding, FullText deliberately excluded
    };
}
```

---

## File Structure

This phase produces no new files but validates existing security controls:

```
Files to Review:
├── src/CompoundDocs.McpServer/
│   └── Validation/
│       └── Attributes/
│           ├── NoPathTraversalAttribute.cs  # Verify encoding bypass prevention
│           ├── RelativePathAttribute.cs     # Verify boundary enforcement
│           └── AbsolutePathAttribute.cs     # Verify symlink handling
│
├── src/CompoundDocs.Core/
│   ├── Data/
│   │   ├── DocumentRepository.cs            # Verify tenant filtering
│   │   └── ChunkRepository.cs               # Verify tenant filtering
│   └── Services/
│       ├── IndexingService.cs               # Verify no content logging
│       └── EmbeddingService.cs              # Verify no vector logging
│
├── src/CompoundDocs.McpServer/
│   └── Tools/
│       ├── RagQueryTool.cs                  # Verify parameter validation
│       ├── SemanticSearchTool.cs            # Verify tenant isolation
│       ├── IndexDocumentTool.cs             # Verify path validation
│       ├── DeleteDocumentsTool.cs           # Verify tenant scoping
│       ├── UpdatePromotionTool.cs           # Verify path validation
│       └── ActivateProjectTool.cs           # Verify context clearing
│
└── tests/CompoundDocs.McpServer.Tests/
    └── Security/
        ├── PathTraversalSecurityTests.cs    # NEW: Security test suite
        ├── TenantIsolationSecurityTests.cs  # NEW: Isolation tests
        └── LoggingSecurityTests.cs          # NEW: Logging tests
```

---

## Dependencies

### Depends On
- Phase 080: Tool Parameter Validation Framework (validation attributes, path validation)

### Blocks
- Phase 150+: Production deployment phases

---

## Verification Steps

After completing this phase, verify:

1. **Path traversal blocked**: Run full test suite against known bypass techniques
   ```bash
   dotnet test --filter "FullyQualifiedName~PathTraversalSecurityTests"
   ```

2. **Tenant isolation**: Run cross-tenant data access tests
   ```bash
   dotnet test --filter "FullyQualifiedName~TenantIsolationSecurityTests"
   ```

3. **Log sanitization**: Run logging security tests with log capture
   ```bash
   dotnet test --filter "FullyQualifiedName~LoggingSecurityTests"
   ```

4. **Configuration audit**: Verify no secrets in config files
   ```bash
   grep -r "password\|secret\|key\|token" src/ --include="*.json" --include="*.yaml"
   # Should return empty or only schema/documentation references
   ```

5. **Code review checklist**:
   - [ ] All SQL uses parameterized queries
   - [ ] All file operations validate paths
   - [ ] All data queries include tenant context
   - [ ] All logging uses structured logging without content
   - [ ] No string concatenation in queries

6. **Security test coverage**: Verify minimum 90% coverage on security-critical paths

---

## Security Review Checklist Summary

| Category | Status | Notes |
|----------|--------|-------|
| Path Traversal Prevention | [ ] | Basic + encoding + boundary |
| Input Sanitization | [ ] | All parameters validated |
| Credential Handling | [ ] | None stored |
| Sensitive Data Logging | [ ] | Content/vectors never logged |
| Multi-Tenant Isolation | [ ] | All queries filtered |

---

## Notes

- This phase is primarily review and validation, not implementation
- Security tests should be run in CI/CD pipeline for ongoing assurance
- Consider periodic security audits post-MVP with updated test cases
- Path traversal prevention is the highest priority given file system access
- Tenant isolation failures would be critical - require 100% test coverage
- Logging restrictions may require developer training to maintain compliance
