# Phase 026: Tool Execution Pipeline

> **Status**: [PLANNED]
> **Category**: MCP Server Core
> **Estimated Effort**: L
> **Prerequisites**: Phase 025 (Tool Registration Infrastructure)

---

## Spec References

- [mcp-server.md - Error Handling](../spec/mcp-server.md#error-handling) - Standard error response format and error codes
- [mcp-server/tools.md - Error Handling](../spec/mcp-server/tools.md#error-handling) - Tool-specific error codes and behaviors
- [observability.md - MCP Tool Execution](../spec/observability.md#mcp-tool-execution) - Tool execution logging patterns
- [observability.md - Correlation ID Pattern](../spec/observability.md#correlation-id-pattern) - Request correlation tracking
- [research/mcp-csharp-sdk-research.md](../research/mcp-csharp-sdk-research.md) - MCP SDK tool implementation patterns

---

## Objectives

1. Implement the tool invocation pipeline that handles MCP tool calls from request to response
2. Create parameter validation and binding infrastructure with descriptive error messages
3. Implement correlation ID generation and injection into logging scopes
4. Build standardized error handling with consistent error response format
5. Add execution timing and structured logging for all tool operations
6. Create response serialization with proper JSON formatting

---

## Acceptance Criteria

### Pipeline Infrastructure
- [ ] `IToolExecutionPipeline` interface defined for tool invocation abstraction
- [ ] `ToolExecutionPipeline` implementation handles the complete request lifecycle
- [ ] Pipeline integrates with MCP SDK's `WithCallToolHandler` for custom handling
- [ ] Pipeline supports async/await and proper cancellation token propagation

### Parameter Validation
- [ ] `IParameterValidator` interface for validating tool parameters
- [ ] `ParameterValidationResult` record with errors collection
- [ ] Required parameter validation (non-null, non-empty for strings)
- [ ] Type coercion from JSON to CLR types (string, int, float, bool, arrays, enums)
- [ ] Enum validation with descriptive error messages listing valid values
- [ ] Range validation for numeric parameters (min/max relevance scores, limits)
- [ ] Parameter binding uses `[Description]` attributes for error messages

### Correlation ID
- [ ] `ICorrelationIdGenerator` interface for ID generation strategy
- [ ] Default implementation uses shortened GUID (8 characters)
- [ ] Correlation ID injected via `ILogger.BeginScope()` for all tool operations
- [ ] Correlation ID included in error responses for debugging

### Error Handling
- [ ] `ToolError` record type with `Code`, `Message`, and `Details` properties
- [ ] `ToolErrorResult` implements standard error response format from spec
- [ ] Error codes mapped from exceptions:
  - `PROJECT_NOT_ACTIVATED` - No active project context
  - `DOCUMENT_NOT_FOUND` - Requested document doesn't exist
  - `INVALID_DOC_TYPE` - Unknown doc-type specified
  - `SCHEMA_VALIDATION_FAILED` - Frontmatter validation failure
  - `EMBEDDING_SERVICE_ERROR` - Ollama embedding generation failed
  - `DATABASE_ERROR` - PostgreSQL operation failed
  - `FILE_SYSTEM_ERROR` - File read/write operation failed
  - `INVALID_PARAMS` - Parameter validation failed
- [ ] Exception-to-error-code mapping via `IErrorCodeMapper`
- [ ] Unhandled exceptions logged and converted to `INTERNAL_ERROR`

### Execution Timing
- [ ] `Stopwatch` used to measure total execution time per tool call
- [ ] Timing logged at Information level with `{ToolName}` and `{ElapsedMs}` fields
- [ ] Slow execution warnings logged when exceeding configurable threshold (default: 5000ms)

### Response Serialization
- [ ] Successful responses serialized as JSON text content
- [ ] Error responses follow standard `{ error: true, code, message, details }` format
- [ ] JSON serialization uses `System.Text.Json` with camelCase naming policy
- [ ] Large responses (>100KB) logged at Warning level

---

## Implementation Notes

### 1. Tool Execution Pipeline Interface

```csharp
namespace CompoundDocs.McpServer.Pipeline;

/// <summary>
/// Defines the tool execution pipeline for processing MCP tool calls.
/// </summary>
public interface IToolExecutionPipeline
{
    /// <summary>
    /// Executes a tool and returns the result.
    /// </summary>
    /// <param name="toolName">The name of the tool to execute.</param>
    /// <param name="arguments">The tool arguments dictionary.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tool execution result.</returns>
    Task<CallToolResult> ExecuteAsync(
        string toolName,
        IDictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default);
}
```

### 2. Correlation ID Generator

```csharp
namespace CompoundDocs.McpServer.Pipeline;

/// <summary>
/// Generates correlation IDs for request tracing.
/// </summary>
public interface ICorrelationIdGenerator
{
    /// <summary>
    /// Generates a new correlation ID.
    /// </summary>
    string Generate();
}

/// <summary>
/// Default implementation using shortened GUIDs.
/// </summary>
public class GuidCorrelationIdGenerator : ICorrelationIdGenerator
{
    public string Generate() => Guid.NewGuid().ToString("N")[..8];
}
```

### 3. Tool Execution Pipeline Implementation

```csharp
namespace CompoundDocs.McpServer.Pipeline;

public class ToolExecutionPipeline : IToolExecutionPipeline
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICorrelationIdGenerator _correlationIdGenerator;
    private readonly IParameterValidator _parameterValidator;
    private readonly IErrorCodeMapper _errorCodeMapper;
    private readonly ILogger<ToolExecutionPipeline> _logger;
    private readonly ToolExecutionOptions _options;

    public ToolExecutionPipeline(
        IServiceProvider serviceProvider,
        ICorrelationIdGenerator correlationIdGenerator,
        IParameterValidator parameterValidator,
        IErrorCodeMapper errorCodeMapper,
        ILogger<ToolExecutionPipeline> logger,
        IOptions<ToolExecutionOptions> options)
    {
        _serviceProvider = serviceProvider;
        _correlationIdGenerator = correlationIdGenerator;
        _parameterValidator = parameterValidator;
        _errorCodeMapper = errorCodeMapper;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<CallToolResult> ExecuteAsync(
        string toolName,
        IDictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default)
    {
        var correlationId = _correlationIdGenerator.Generate();
        var stopwatch = Stopwatch.StartNew();

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["ToolName"] = toolName
        }))
        {
            try
            {
                _logger.LogInformation("Tool invoked: {ToolName}", toolName);
                _logger.LogDebug("Tool parameters: {Parameters}",
                    JsonSerializer.Serialize(arguments));

                // Validate parameters
                var validationResult = await _parameterValidator.ValidateAsync(
                    toolName, arguments, cancellationToken);

                if (!validationResult.IsValid)
                {
                    return CreateErrorResult(
                        "INVALID_PARAMS",
                        "Parameter validation failed",
                        new { errors = validationResult.Errors, correlationId });
                }

                // Execute the tool (delegate to MCP SDK handler)
                var result = await ExecuteToolCoreAsync(
                    toolName, arguments, cancellationToken);

                stopwatch.Stop();
                LogExecutionComplete(toolName, stopwatch.ElapsedMilliseconds, result);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return HandleException(ex, correlationId, stopwatch.ElapsedMilliseconds);
            }
        }
    }

    private void LogExecutionComplete(string toolName, long elapsedMs, CallToolResult result)
    {
        _logger.LogInformation(
            "Tool completed: {ToolName} in {ElapsedMs}ms",
            toolName,
            elapsedMs);

        if (elapsedMs > _options.SlowExecutionThresholdMs)
        {
            _logger.LogWarning(
                "Slow tool execution: {ToolName} took {ElapsedMs}ms (threshold: {Threshold}ms)",
                toolName,
                elapsedMs,
                _options.SlowExecutionThresholdMs);
        }

        // Warn on large responses
        var responseSize = EstimateResponseSize(result);
        if (responseSize > _options.LargeResponseThresholdBytes)
        {
            _logger.LogWarning(
                "Large response from {ToolName}: {ResponseSize} bytes",
                toolName,
                responseSize);
        }
    }

    private CallToolResult HandleException(
        Exception ex,
        string correlationId,
        long elapsedMs)
    {
        var errorCode = _errorCodeMapper.MapException(ex);

        _logger.LogError(ex,
            "Tool failed: {ToolName} with error {ErrorCode} after {ElapsedMs}ms",
            "unknown",
            errorCode,
            elapsedMs);

        return CreateErrorResult(
            errorCode,
            GetSafeErrorMessage(ex),
            new { correlationId, exceptionType = ex.GetType().Name });
    }

    private static CallToolResult CreateErrorResult(
        string code,
        string message,
        object? details = null)
    {
        var errorResponse = new
        {
            error = true,
            code,
            message,
            details
        };

        return new CallToolResult
        {
            Content = new List<ContentBlock>
            {
                new TextContentBlock
                {
                    Text = JsonSerializer.Serialize(errorResponse, JsonOptions.Default)
                }
            },
            IsError = true
        };
    }

    private string GetSafeErrorMessage(Exception ex)
    {
        // Return safe message without exposing internal details
        return ex switch
        {
            ProjectNotActivatedException => "No project is currently activated",
            DocumentNotFoundException => "The requested document was not found",
            InvalidDocTypeException => "The specified doc-type is invalid",
            SchemaValidationException => "Document frontmatter validation failed",
            EmbeddingServiceException => "Embedding generation service unavailable",
            DatabaseException => "Database operation failed",
            FileSystemException => "File system operation failed",
            _ => "An unexpected error occurred"
        };
    }
}
```

### 4. Parameter Validator

```csharp
namespace CompoundDocs.McpServer.Pipeline;

/// <summary>
/// Validates tool parameters before execution.
/// </summary>
public interface IParameterValidator
{
    /// <summary>
    /// Validates parameters for the specified tool.
    /// </summary>
    Task<ParameterValidationResult> ValidateAsync(
        string toolName,
        IDictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of parameter validation.
/// </summary>
public record ParameterValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<ParameterValidationError> Errors { get; init; } = [];

    public static ParameterValidationResult Success() => new();

    public static ParameterValidationResult Failure(params ParameterValidationError[] errors) =>
        new() { Errors = errors };
}

/// <summary>
/// A single parameter validation error.
/// </summary>
public record ParameterValidationError(
    string ParameterName,
    string ErrorMessage,
    object? AttemptedValue = null);

public class ParameterValidator : IParameterValidator
{
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<ParameterValidator> _logger;

    public ParameterValidator(
        IToolRegistry toolRegistry,
        ILogger<ParameterValidator> logger)
    {
        _toolRegistry = toolRegistry;
        _logger = logger;
    }

    public async Task<ParameterValidationResult> ValidateAsync(
        string toolName,
        IDictionary<string, object?>? arguments,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ParameterValidationError>();
        var toolDefinition = _toolRegistry.GetToolDefinition(toolName);

        if (toolDefinition is null)
        {
            return ParameterValidationResult.Failure(
                new ParameterValidationError("toolName", $"Unknown tool: {toolName}"));
        }

        arguments ??= new Dictionary<string, object?>();

        foreach (var param in toolDefinition.Parameters)
        {
            var hasValue = arguments.TryGetValue(param.Name, out var value);

            // Required parameter check
            if (param.IsRequired && (!hasValue || value is null))
            {
                errors.Add(new ParameterValidationError(
                    param.Name,
                    $"Required parameter '{param.Name}' is missing"));
                continue;
            }

            if (!hasValue || value is null)
                continue;

            // Type validation
            var typeError = ValidateType(param, value);
            if (typeError is not null)
            {
                errors.Add(typeError);
                continue;
            }

            // Range validation for numeric types
            var rangeError = ValidateRange(param, value);
            if (rangeError is not null)
            {
                errors.Add(rangeError);
            }

            // Enum validation
            var enumError = ValidateEnum(param, value);
            if (enumError is not null)
            {
                errors.Add(enumError);
            }
        }

        return errors.Count > 0
            ? ParameterValidationResult.Failure(errors.ToArray())
            : ParameterValidationResult.Success();
    }

    private ParameterValidationError? ValidateType(
        ToolParameterDefinition param,
        object value)
    {
        try
        {
            // Attempt type coercion
            var converted = ConvertValue(value, param.ClrType);
            return null;
        }
        catch (Exception)
        {
            return new ParameterValidationError(
                param.Name,
                $"Parameter '{param.Name}' must be of type {param.TypeName}",
                value);
        }
    }

    private ParameterValidationError? ValidateRange(
        ToolParameterDefinition param,
        object value)
    {
        if (param.MinValue is null && param.MaxValue is null)
            return null;

        if (value is not IComparable comparable)
            return null;

        if (param.MinValue is not null && comparable.CompareTo(param.MinValue) < 0)
        {
            return new ParameterValidationError(
                param.Name,
                $"Parameter '{param.Name}' must be at least {param.MinValue}",
                value);
        }

        if (param.MaxValue is not null && comparable.CompareTo(param.MaxValue) > 0)
        {
            return new ParameterValidationError(
                param.Name,
                $"Parameter '{param.Name}' must be at most {param.MaxValue}",
                value);
        }

        return null;
    }

    private ParameterValidationError? ValidateEnum(
        ToolParameterDefinition param,
        object value)
    {
        if (param.AllowedValues is null || param.AllowedValues.Count == 0)
            return null;

        var stringValue = value.ToString();
        if (!param.AllowedValues.Contains(stringValue, StringComparer.OrdinalIgnoreCase))
        {
            return new ParameterValidationError(
                param.Name,
                $"Parameter '{param.Name}' must be one of: {string.Join(", ", param.AllowedValues)}",
                value);
        }

        return null;
    }

    private static object ConvertValue(object value, Type targetType)
    {
        if (value.GetType() == targetType)
            return value;

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        // Handle JsonElement from System.Text.Json
        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.String => ConvertFromString(
                    jsonElement.GetString()!, underlyingType),
                JsonValueKind.Number => ConvertNumber(jsonElement, underlyingType),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => ConvertArray(jsonElement, underlyingType),
                _ => throw new InvalidCastException(
                    $"Cannot convert {jsonElement.ValueKind} to {targetType.Name}")
            };
        }

        // Handle string to target type
        if (value is string stringValue)
        {
            return ConvertFromString(stringValue, underlyingType);
        }

        return Convert.ChangeType(value, underlyingType);
    }

    private static object ConvertFromString(string value, Type targetType)
    {
        if (targetType == typeof(string))
            return value;

        if (targetType.IsEnum)
            return Enum.Parse(targetType, value, ignoreCase: true);

        if (targetType == typeof(int))
            return int.Parse(value);

        if (targetType == typeof(float))
            return float.Parse(value);

        if (targetType == typeof(double))
            return double.Parse(value);

        if (targetType == typeof(bool))
            return bool.Parse(value);

        return Convert.ChangeType(value, targetType);
    }

    private static object ConvertNumber(JsonElement element, Type targetType)
    {
        if (targetType == typeof(int))
            return element.GetInt32();
        if (targetType == typeof(long))
            return element.GetInt64();
        if (targetType == typeof(float))
            return element.GetSingle();
        if (targetType == typeof(double))
            return element.GetDouble();
        if (targetType == typeof(decimal))
            return element.GetDecimal();

        return element.GetDouble();
    }

    private static object ConvertArray(JsonElement element, Type targetType)
    {
        if (!targetType.IsArray && !targetType.IsGenericType)
            throw new InvalidCastException($"Cannot convert array to {targetType.Name}");

        var elementType = targetType.IsArray
            ? targetType.GetElementType()!
            : targetType.GetGenericArguments()[0];

        var items = element.EnumerateArray()
            .Select(e => ConvertValue(e, elementType))
            .ToArray();

        var typedArray = Array.CreateInstance(elementType, items.Length);
        Array.Copy(items, typedArray, items.Length);

        return typedArray;
    }
}
```

### 5. Error Code Mapper

```csharp
namespace CompoundDocs.McpServer.Pipeline;

/// <summary>
/// Maps exceptions to standardized error codes.
/// </summary>
public interface IErrorCodeMapper
{
    /// <summary>
    /// Maps an exception to an error code string.
    /// </summary>
    string MapException(Exception exception);
}

public class ErrorCodeMapper : IErrorCodeMapper
{
    public string MapException(Exception exception)
    {
        return exception switch
        {
            ProjectNotActivatedException => "PROJECT_NOT_ACTIVATED",
            DocumentNotFoundException => "DOCUMENT_NOT_FOUND",
            InvalidDocTypeException => "INVALID_DOC_TYPE",
            SchemaValidationException => "SCHEMA_VALIDATION_FAILED",
            EmbeddingServiceException => "EMBEDDING_SERVICE_ERROR",
            DatabaseException => "DATABASE_ERROR",
            FileSystemException or IOException => "FILE_SYSTEM_ERROR",
            ArgumentException or ArgumentNullException => "INVALID_PARAMS",
            OperationCanceledException => "OPERATION_CANCELLED",
            TimeoutException => "TIMEOUT",
            _ => "INTERNAL_ERROR"
        };
    }
}
```

### 6. Tool Execution Options

```csharp
namespace CompoundDocs.McpServer.Pipeline;

/// <summary>
/// Configuration options for tool execution pipeline.
/// </summary>
public class ToolExecutionOptions
{
    /// <summary>
    /// Execution time threshold (ms) above which a warning is logged.
    /// Default: 5000ms.
    /// </summary>
    public int SlowExecutionThresholdMs { get; set; } = 5000;

    /// <summary>
    /// Response size threshold (bytes) above which a warning is logged.
    /// Default: 100KB.
    /// </summary>
    public int LargeResponseThresholdBytes { get; set; } = 100 * 1024;

    /// <summary>
    /// Whether to include stack traces in error details (development only).
    /// Default: false.
    /// </summary>
    public bool IncludeStackTraceInErrors { get; set; } = false;
}
```

### 7. JSON Serialization Options

```csharp
namespace CompoundDocs.McpServer.Pipeline;

/// <summary>
/// Shared JSON serialization options for consistent formatting.
/// </summary>
public static class JsonOptions
{
    public static JsonSerializerOptions Default { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static JsonSerializerOptions Indented { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
```

### 8. Custom Exceptions

```csharp
namespace CompoundDocs.McpServer.Exceptions;

/// <summary>
/// Base exception for compound docs operations.
/// </summary>
public abstract class CompoundDocsException : Exception
{
    public string ErrorCode { get; }
    public object? Details { get; }

    protected CompoundDocsException(string errorCode, string message, object? details = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Details = details;
    }
}

public class ProjectNotActivatedException : CompoundDocsException
{
    public ProjectNotActivatedException()
        : base("PROJECT_NOT_ACTIVATED", "No project is currently activated") { }
}

public class DocumentNotFoundException : CompoundDocsException
{
    public string DocumentPath { get; }

    public DocumentNotFoundException(string documentPath)
        : base("DOCUMENT_NOT_FOUND", $"Document not found: {documentPath}",
            new { documentPath })
    {
        DocumentPath = documentPath;
    }
}

public class InvalidDocTypeException : CompoundDocsException
{
    public string DocType { get; }
    public IReadOnlyList<string> ValidDocTypes { get; }

    public InvalidDocTypeException(string docType, IReadOnlyList<string> validDocTypes)
        : base("INVALID_DOC_TYPE",
            $"Invalid doc-type: {docType}. Valid types: {string.Join(", ", validDocTypes)}",
            new { docType, validDocTypes })
    {
        DocType = docType;
        ValidDocTypes = validDocTypes;
    }
}

public class SchemaValidationException : CompoundDocsException
{
    public IReadOnlyList<string> ValidationErrors { get; }

    public SchemaValidationException(IReadOnlyList<string> validationErrors)
        : base("SCHEMA_VALIDATION_FAILED",
            "Document frontmatter validation failed",
            new { errors = validationErrors })
    {
        ValidationErrors = validationErrors;
    }
}

public class EmbeddingServiceException : CompoundDocsException
{
    public EmbeddingServiceException(string message, Exception? innerException = null)
        : base("EMBEDDING_SERVICE_ERROR", message) { }
}

public class DatabaseException : CompoundDocsException
{
    public DatabaseException(string message, Exception? innerException = null)
        : base("DATABASE_ERROR", message) { }
}

public class FileSystemException : CompoundDocsException
{
    public string FilePath { get; }

    public FileSystemException(string filePath, string message)
        : base("FILE_SYSTEM_ERROR", message, new { filePath })
    {
        FilePath = filePath;
    }
}
```

### 9. Service Registration

```csharp
namespace CompoundDocs.McpServer.Pipeline;

public static class ToolExecutionServiceCollectionExtensions
{
    public static IServiceCollection AddToolExecutionPipeline(
        this IServiceCollection services,
        Action<ToolExecutionOptions>? configureOptions = null)
    {
        // Configure options
        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }

        // Register pipeline services
        services.AddSingleton<ICorrelationIdGenerator, GuidCorrelationIdGenerator>();
        services.AddSingleton<IParameterValidator, ParameterValidator>();
        services.AddSingleton<IErrorCodeMapper, ErrorCodeMapper>();
        services.AddScoped<IToolExecutionPipeline, ToolExecutionPipeline>();

        return services;
    }
}
```

### 10. Integration with MCP Server

```csharp
// In Program.cs or MCP server setup
builder.Services.AddToolExecutionPipeline(options =>
{
    options.SlowExecutionThresholdMs = 5000;
    options.LargeResponseThresholdBytes = 100 * 1024;
    options.IncludeStackTraceInErrors = builder.Environment.IsDevelopment();
});

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithCallToolHandler(async (request, ct) =>
    {
        var pipeline = request.Services.GetRequiredService<IToolExecutionPipeline>();
        return await pipeline.ExecuteAsync(
            request.Params.Name,
            request.Params.Arguments,
            ct);
    })
    .WithToolsFromAssembly();
```

---

## Dependencies

### Depends On

- **Phase 025**: Tool Registration Infrastructure - Tool registry and definitions needed for parameter validation

### Blocks

- **Phase 027+**: Individual tool implementations - Tools depend on the execution pipeline
- **Phase 030+**: RAG query tool - Uses pipeline for execution
- **Phase 031+**: Semantic search tool - Uses pipeline for execution

---

## Testing Verification

After implementation, verify with:

### Unit Tests

```csharp
[Fact]
public async Task ExecuteAsync_ValidParameters_ReturnsSuccessResult()
{
    // Arrange
    var pipeline = CreatePipeline();
    var arguments = new Dictionary<string, object?>
    {
        ["query"] = "test query",
        ["maxResults"] = 5
    };

    // Act
    var result = await pipeline.ExecuteAsync("semantic_search", arguments);

    // Assert
    Assert.False(result.IsError);
}

[Fact]
public async Task ExecuteAsync_MissingRequiredParameter_ReturnsValidationError()
{
    // Arrange
    var pipeline = CreatePipeline();
    var arguments = new Dictionary<string, object?>(); // Missing required 'query'

    // Act
    var result = await pipeline.ExecuteAsync("semantic_search", arguments);

    // Assert
    Assert.True(result.IsError);
    var content = result.Content.First() as TextContentBlock;
    Assert.Contains("INVALID_PARAMS", content?.Text);
}

[Fact]
public async Task ExecuteAsync_InvalidEnumValue_ReturnsDescriptiveError()
{
    // Arrange
    var pipeline = CreatePipeline();
    var arguments = new Dictionary<string, object?>
    {
        ["document_path"] = "test.md",
        ["promotion_level"] = "invalid_level" // Not a valid enum value
    };

    // Act
    var result = await pipeline.ExecuteAsync("update_promotion_level", arguments);

    // Assert
    Assert.True(result.IsError);
    var content = result.Content.First() as TextContentBlock;
    Assert.Contains("must be one of: standard, important, critical", content?.Text);
}

[Fact]
public async Task ExecuteAsync_SlowExecution_LogsWarning()
{
    // Arrange
    var logger = new FakeLogger<ToolExecutionPipeline>();
    var pipeline = CreatePipeline(logger, slowThreshold: 100);

    // Act (tool that takes >100ms)
    await pipeline.ExecuteAsync("slow_tool", null);

    // Assert
    Assert.Contains(logger.Logs,
        l => l.Level == LogLevel.Warning && l.Message.Contains("Slow tool execution"));
}

[Fact]
public async Task ExecuteAsync_IncludesCorrelationIdInScope()
{
    // Arrange
    var logger = new FakeLogger<ToolExecutionPipeline>();
    var pipeline = CreatePipeline(logger);

    // Act
    await pipeline.ExecuteAsync("test_tool", null);

    // Assert
    Assert.All(logger.Logs, l =>
        l.Scopes.ContainsKey("CorrelationId"));
}
```

### Integration Test

```csharp
[Fact]
public async Task Pipeline_Integration_WithMcpServer()
{
    // Arrange
    var builder = Host.CreateApplicationBuilder();
    builder.Services.AddToolExecutionPipeline();
    builder.Services.AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    using var host = builder.Build();

    // Act - verify pipeline integrates with MCP server
    var pipeline = host.Services.GetRequiredService<IToolExecutionPipeline>();
    var result = await pipeline.ExecuteAsync("list_doc_types", null);

    // Assert
    Assert.NotNull(result);
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Pipeline/IToolExecutionPipeline.cs` | Create | Pipeline interface |
| `src/CompoundDocs.McpServer/Pipeline/ToolExecutionPipeline.cs` | Create | Pipeline implementation |
| `src/CompoundDocs.McpServer/Pipeline/ICorrelationIdGenerator.cs` | Create | Correlation ID interface |
| `src/CompoundDocs.McpServer/Pipeline/GuidCorrelationIdGenerator.cs` | Create | Default correlation ID impl |
| `src/CompoundDocs.McpServer/Pipeline/IParameterValidator.cs` | Create | Parameter validation interface |
| `src/CompoundDocs.McpServer/Pipeline/ParameterValidator.cs` | Create | Parameter validation impl |
| `src/CompoundDocs.McpServer/Pipeline/ParameterValidationResult.cs` | Create | Validation result types |
| `src/CompoundDocs.McpServer/Pipeline/IErrorCodeMapper.cs` | Create | Error mapping interface |
| `src/CompoundDocs.McpServer/Pipeline/ErrorCodeMapper.cs` | Create | Error mapping impl |
| `src/CompoundDocs.McpServer/Pipeline/ToolExecutionOptions.cs` | Create | Configuration options |
| `src/CompoundDocs.McpServer/Pipeline/JsonOptions.cs` | Create | Shared JSON options |
| `src/CompoundDocs.McpServer/Pipeline/ServiceCollectionExtensions.cs` | Create | DI registration |
| `src/CompoundDocs.McpServer/Exceptions/CompoundDocsException.cs` | Create | Base exception |
| `src/CompoundDocs.McpServer/Exceptions/ProjectNotActivatedException.cs` | Create | Project exception |
| `src/CompoundDocs.McpServer/Exceptions/DocumentNotFoundException.cs` | Create | Document exception |
| `src/CompoundDocs.McpServer/Exceptions/InvalidDocTypeException.cs` | Create | Doc-type exception |
| `src/CompoundDocs.McpServer/Exceptions/SchemaValidationException.cs` | Create | Schema exception |
| `src/CompoundDocs.McpServer/Exceptions/EmbeddingServiceException.cs` | Create | Embedding exception |
| `src/CompoundDocs.McpServer/Exceptions/DatabaseException.cs` | Create | Database exception |
| `src/CompoundDocs.McpServer/Exceptions/FileSystemException.cs` | Create | File system exception |
| `tests/CompoundDocs.Tests/Pipeline/ToolExecutionPipelineTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.Tests/Pipeline/ParameterValidatorTests.cs` | Create | Validator tests |
| `tests/CompoundDocs.Tests/Pipeline/ErrorCodeMapperTests.cs` | Create | Error mapper tests |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Pipeline adds latency | Measure overhead; optimize hot paths; use ValueTask where appropriate |
| Error messages leak internal details | Always use safe error messages; only include details in development |
| Parameter validation too strict | Log validation failures; iterate on rules based on real usage |
| Large responses cause memory pressure | Stream large responses; add response size limits |
| Correlation ID collisions | 8-char GUIDs have negligible collision risk for logging purposes |
| MCP SDK breaking changes | Pin SDK version; abstract pipeline from SDK internals |

---

## Notes

- The pipeline wraps MCP SDK's tool execution to add observability and error handling
- Correlation IDs enable tracing a complete request flow across services
- Parameter validation happens before tool execution to fail fast with descriptive errors
- Error codes from the spec are enforced consistently via the error code mapper
- The pipeline is designed to be testable with dependency injection
- JSON serialization uses camelCase to match JavaScript conventions in MCP clients
