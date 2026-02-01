# Phase 019: Correlation ID Pattern Implementation

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-5 hours
> **Category**: Infrastructure Setup
> **Prerequisites**: Phase 018

---

## Spec References

This phase implements the correlation ID pattern and request tracing defined in:

- **spec/observability.md** - [Correlation ID Pattern](../spec/observability.md#correlation-id-pattern) (lines 132-154)
- **spec/observability.md** - [Standard Log Fields](../spec/observability.md#standard-log-fields) (lines 119-131)
- **spec/observability.md** - [Logging Scopes](../spec/observability.md#logging-scopes) (lines 156-172)

---

## Objectives

1. Implement correlation ID generation for each MCP tool invocation
2. Create AsyncLocal-based correlation context for cross-service propagation
3. Establish logging scope pattern for automatic correlation ID inclusion
4. Define standard log fields (ProjectName, BranchName, ToolName, CorrelationId)
5. Enable end-to-end request tracing across all service calls

---

## Acceptance Criteria

- [ ] `CorrelationContext` class implemented with AsyncLocal storage
  - [ ] Generates unique correlation IDs (8-character hex format)
  - [ ] Provides thread-safe access via AsyncLocal<T>
  - [ ] Supports getting/setting current correlation ID
- [ ] `ICorrelationContextAccessor` interface defined
  - [ ] `CorrelationId` property for current correlation ID
  - [ ] `CreateScope()` method for establishing new correlation context
- [ ] `CorrelationContextAccessor` implementation registered with DI
  - [ ] Singleton lifetime registration
  - [ ] Proper AsyncLocal initialization
- [ ] Logging scope extension methods created
  - [ ] `BeginCorrelationScope(ILogger, string correlationId)` method
  - [ ] `BeginToolScope(ILogger, string toolName, string correlationId)` method
  - [ ] `BeginProjectScope(ILogger, string projectName, string branchName)` method
- [ ] MCP tool invocation middleware/wrapper applies correlation context
  - [ ] New correlation ID generated per tool invocation
  - [ ] Correlation ID included in all log entries for the invocation
  - [ ] Tool name captured in logging scope
- [ ] Standard log fields consistently applied across services
  - [ ] `CorrelationId` - Request correlation identifier
  - [ ] `ToolName` - MCP tool being executed
  - [ ] `ProjectName` - Current tenant project
  - [ ] `BranchName` - Current git branch
  - [ ] `ElapsedMs` - Operation duration where applicable
- [ ] Unit tests verify correlation ID propagation
  - [ ] Correlation ID persists across async boundaries
  - [ ] Child scopes inherit parent correlation context
  - [ ] Independent requests have unique correlation IDs

---

## Implementation Notes

### CorrelationContext with AsyncLocal

Create a correlation context that flows across async operations:

```csharp
namespace CompoundDocs.Common.Observability;

/// <summary>
/// Provides ambient correlation context using AsyncLocal storage.
/// </summary>
public sealed class CorrelationContext
{
    private static readonly AsyncLocal<string?> _correlationId = new();
    private static readonly AsyncLocal<string?> _toolName = new();
    private static readonly AsyncLocal<string?> _projectName = new();
    private static readonly AsyncLocal<string?> _branchName = new();

    /// <summary>
    /// Gets or sets the current correlation ID.
    /// </summary>
    public static string? CorrelationId
    {
        get => _correlationId.Value;
        set => _correlationId.Value = value;
    }

    /// <summary>
    /// Gets or sets the current tool name.
    /// </summary>
    public static string? ToolName
    {
        get => _toolName.Value;
        set => _toolName.Value = value;
    }

    /// <summary>
    /// Gets or sets the current project name.
    /// </summary>
    public static string? ProjectName
    {
        get => _projectName.Value;
        set => _projectName.Value = value;
    }

    /// <summary>
    /// Gets or sets the current branch name.
    /// </summary>
    public static string? BranchName
    {
        get => _branchName.Value;
        set => _branchName.Value = value;
    }

    /// <summary>
    /// Generates a new short correlation ID (8-character hex).
    /// </summary>
    public static string GenerateCorrelationId()
        => Guid.NewGuid().ToString("N")[..8];
}
```

### ICorrelationContextAccessor Interface

Define an interface for DI-friendly correlation context access:

```csharp
namespace CompoundDocs.Common.Observability;

/// <summary>
/// Provides access to the current correlation context.
/// </summary>
public interface ICorrelationContextAccessor
{
    /// <summary>
    /// Gets or sets the current correlation ID.
    /// </summary>
    string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the current tool name.
    /// </summary>
    string? ToolName { get; set; }

    /// <summary>
    /// Gets or sets the current project name.
    /// </summary>
    string? ProjectName { get; set; }

    /// <summary>
    /// Gets or sets the current branch name.
    /// </summary>
    string? BranchName { get; set; }

    /// <summary>
    /// Creates a new correlation scope with a fresh correlation ID.
    /// </summary>
    /// <returns>A disposable scope that restores previous context on disposal.</returns>
    IDisposable CreateScope();

    /// <summary>
    /// Creates a new correlation scope with a specified correlation ID.
    /// </summary>
    IDisposable CreateScope(string correlationId);
}
```

### CorrelationContextAccessor Implementation

```csharp
namespace CompoundDocs.Common.Observability;

/// <summary>
/// Default implementation of <see cref="ICorrelationContextAccessor"/>.
/// </summary>
public sealed class CorrelationContextAccessor : ICorrelationContextAccessor
{
    public string? CorrelationId
    {
        get => CorrelationContext.CorrelationId;
        set => CorrelationContext.CorrelationId = value;
    }

    public string? ToolName
    {
        get => CorrelationContext.ToolName;
        set => CorrelationContext.ToolName = value;
    }

    public string? ProjectName
    {
        get => CorrelationContext.ProjectName;
        set => CorrelationContext.ProjectName = value;
    }

    public string? BranchName
    {
        get => CorrelationContext.BranchName;
        set => CorrelationContext.BranchName = value;
    }

    public IDisposable CreateScope()
        => CreateScope(CorrelationContext.GenerateCorrelationId());

    public IDisposable CreateScope(string correlationId)
        => new CorrelationScope(this, correlationId);

    private sealed class CorrelationScope : IDisposable
    {
        private readonly string? _previousCorrelationId;
        private readonly string? _previousToolName;
        private readonly ICorrelationContextAccessor _accessor;
        private bool _disposed;

        public CorrelationScope(ICorrelationContextAccessor accessor, string correlationId)
        {
            _accessor = accessor;
            _previousCorrelationId = accessor.CorrelationId;
            _previousToolName = accessor.ToolName;
            accessor.CorrelationId = correlationId;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _accessor.CorrelationId = _previousCorrelationId;
            _accessor.ToolName = _previousToolName;
        }
    }
}
```

### Logging Scope Extension Methods

Create extension methods for establishing logging scopes with correlation context:

```csharp
namespace CompoundDocs.Common.Observability;

using Microsoft.Extensions.Logging;

/// <summary>
/// Extension methods for creating correlation-aware logging scopes.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Begins a logging scope with the specified correlation ID.
    /// </summary>
    public static IDisposable BeginCorrelationScope(
        this ILogger logger,
        string correlationId)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });
    }

    /// <summary>
    /// Begins a logging scope for MCP tool execution.
    /// </summary>
    public static IDisposable BeginToolScope(
        this ILogger logger,
        string toolName,
        string correlationId)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["ToolName"] = toolName
        });
    }

    /// <summary>
    /// Begins a logging scope with project/branch context.
    /// </summary>
    public static IDisposable BeginProjectScope(
        this ILogger logger,
        string projectName,
        string branchName)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            ["ProjectName"] = projectName,
            ["BranchName"] = branchName
        });
    }

    /// <summary>
    /// Begins a comprehensive logging scope with all standard fields.
    /// </summary>
    public static IDisposable BeginFullScope(
        this ILogger logger,
        string correlationId,
        string toolName,
        string? projectName = null,
        string? branchName = null)
    {
        var scope = new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["ToolName"] = toolName
        };

        if (projectName is not null)
            scope["ProjectName"] = projectName;

        if (branchName is not null)
            scope["BranchName"] = branchName;

        return logger.BeginScope(scope);
    }
}
```

### MCP Tool Invocation Pattern

Apply correlation context in MCP tool handlers:

```csharp
public class RagQueryTool
{
    private readonly ILogger<RagQueryTool> _logger;
    private readonly ICorrelationContextAccessor _correlationAccessor;
    private readonly IRagService _ragService;

    public RagQueryTool(
        ILogger<RagQueryTool> logger,
        ICorrelationContextAccessor correlationAccessor,
        IRagService ragService)
    {
        _logger = logger;
        _correlationAccessor = correlationAccessor;
        _ragService = ragService;
    }

    public async Task<RagQueryResult> ExecuteAsync(
        RagQueryRequest request,
        CancellationToken ct)
    {
        // Create new correlation scope for this tool invocation
        using var correlationScope = _correlationAccessor.CreateScope();
        _correlationAccessor.ToolName = "rag_query";

        var correlationId = _correlationAccessor.CorrelationId!;

        // Establish logging scope with all context
        using var loggingScope = _logger.BeginToolScope("rag_query", correlationId);

        _logger.LogInformation("RAG query started: {Query}", request.Query);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var results = await _ragService.QueryAsync(request, ct);

            _logger.LogInformation(
                "RAG query completed in {ElapsedMs}ms with {ResultCount} results",
                stopwatch.ElapsedMilliseconds,
                results.Count);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "RAG query failed after {ElapsedMs}ms",
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
```

### Service Registration

Register correlation services with dependency injection:

```csharp
namespace CompoundDocs.Common.Observability;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering correlation services.
/// </summary>
public static class CorrelationServiceExtensions
{
    /// <summary>
    /// Adds correlation context services to the service collection.
    /// </summary>
    public static IServiceCollection AddCorrelationContext(
        this IServiceCollection services)
    {
        services.AddSingleton<ICorrelationContextAccessor, CorrelationContextAccessor>();
        return services;
    }
}
```

### Correlation ID Propagation to Nested Services

Services receiving injected `ICorrelationContextAccessor` can access the current correlation context:

```csharp
public class EmbeddingService : IEmbeddingService
{
    private readonly ILogger<EmbeddingService> _logger;
    private readonly ICorrelationContextAccessor _correlationAccessor;

    public EmbeddingService(
        ILogger<EmbeddingService> logger,
        ICorrelationContextAccessor correlationAccessor)
    {
        _logger = logger;
        _correlationAccessor = correlationAccessor;
    }

    public async Task<float[]> GenerateEmbeddingAsync(
        string content,
        CancellationToken ct)
    {
        // Correlation ID automatically flows via AsyncLocal
        var correlationId = _correlationAccessor.CorrelationId ?? "unknown";

        using var scope = _logger.BeginCorrelationScope(correlationId);

        _logger.LogDebug(
            "Generating embedding for content ({CharCount} chars)",
            content.Length);

        // ... embedding generation logic ...

        _logger.LogInformation(
            "Embedding generated in {ElapsedMs}ms",
            elapsed);

        return embedding;
    }
}
```

---

## File Locations

| File | Project | Purpose |
|------|---------|---------|
| `CorrelationContext.cs` | CompoundDocs.Common | AsyncLocal correlation storage |
| `ICorrelationContextAccessor.cs` | CompoundDocs.Common | DI-friendly accessor interface |
| `CorrelationContextAccessor.cs` | CompoundDocs.Common | Default accessor implementation |
| `LoggingExtensions.cs` | CompoundDocs.Common | Logging scope helper methods |
| `CorrelationServiceExtensions.cs` | CompoundDocs.Common | DI registration extensions |

---

## Dependencies

### Depends On
- Phase 018: Logging Infrastructure (provides ILogger configuration and console/stderr setup)

### Blocks
- Phase 020+: MCP Tool implementations (will use correlation context for request tracing)
- Any phase implementing services that log (will use correlation scopes)

---

## Verification Steps

After completing this phase, verify:

1. **Correlation ID generation**: Call `CorrelationContext.GenerateCorrelationId()` multiple times and verify unique 8-char hex values
2. **AsyncLocal propagation**: Start a correlation scope, spawn async tasks, verify correlation ID is accessible in all tasks
3. **Scope restoration**: Verify nested scopes properly restore previous correlation IDs on disposal
4. **Logging output**: Verify log entries include `CorrelationId` field when using logging scopes
5. **DI registration**: Verify `ICorrelationContextAccessor` resolves correctly from service provider
6. **Thread safety**: Concurrent tool invocations maintain independent correlation IDs

### Test Cases

```csharp
[Fact]
public void GenerateCorrelationId_ReturnsUniqueIds()
{
    var ids = Enumerable.Range(0, 100)
        .Select(_ => CorrelationContext.GenerateCorrelationId())
        .ToHashSet();

    Assert.Equal(100, ids.Count);
    Assert.All(ids, id => Assert.Equal(8, id.Length));
}

[Fact]
public async Task CorrelationId_PropagatesAcrossAsyncBoundaries()
{
    var accessor = new CorrelationContextAccessor();

    using (accessor.CreateScope("test1234"))
    {
        Assert.Equal("test1234", accessor.CorrelationId);

        await Task.Run(() =>
        {
            Assert.Equal("test1234", accessor.CorrelationId);
        });
    }

    Assert.Null(accessor.CorrelationId);
}

[Fact]
public async Task ConcurrentInvocations_MaintainIndependentCorrelationIds()
{
    var accessor = new CorrelationContextAccessor();
    var results = new ConcurrentBag<string>();

    var tasks = Enumerable.Range(0, 10).Select(async i =>
    {
        using (accessor.CreateScope($"corr{i:D4}"))
        {
            await Task.Delay(Random.Shared.Next(10, 50));
            results.Add(accessor.CorrelationId!);
        }
    });

    await Task.WhenAll(tasks);

    Assert.Equal(10, results.Count);
    Assert.Equal(10, results.Distinct().Count());
}
```

---

## Notes

- The 8-character correlation ID format (`Guid.NewGuid().ToString("N")[..8]`) provides a balance between uniqueness and readability in logs
- AsyncLocal<T> is the recommended approach for ambient context in .NET, as it properly flows across async/await boundaries
- The scope pattern (returning IDisposable) ensures correlation context is properly cleaned up even in exception scenarios
- For post-MVP distributed scenarios, the correlation ID could be propagated via HTTP headers (e.g., `X-Correlation-Id`)
- Consider adding correlation ID to structured error responses for client-side correlation
