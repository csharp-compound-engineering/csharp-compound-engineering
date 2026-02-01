# Phase 047: Embedding Dimension Validation at Startup

> **Status**: PLANNED
> **Category**: Database & Storage
> **Estimated Effort**: S
> **Prerequisites**: Phase 029 (Embedding Service Implementation), Phase 041 (Vector Store Collections)

---

## Spec References

- [mcp-server/database-schema.md - Embedding Dimensions](../spec/mcp-server/database-schema.md#embedding-dimensions)
- [mcp-server/ollama-integration.md - Embedding Model](../spec/mcp-server/ollama-integration.md#embedding-model)

---

## Overview

This phase implements comprehensive startup validation to ensure embedding dimensions are consistent across all system components. The `mxbai-embed-large` model produces 1024-dimensional embeddings, and all database vector columns are configured for 1024 dimensions. A mismatch between Ollama's actual output and the database schema would cause silent failures or data corruption during vector storage and retrieval.

From the spec:
> **Startup Validation**: The MCP server validates embedding dimensions at startup by:
> 1. Generating a test embedding via Ollama
> 2. Comparing `embedding.Length` against configured dimensions (1024)
> 3. Failing startup with clear error if dimensions mismatch
>
> This prevents silent failures from model changes or configuration errors.

---

## Objectives

1. Create comprehensive startup validation service for embedding dimension consistency
2. Validate Ollama model produces expected 1024-dimensional embeddings
3. Validate database schema vector columns match expected dimensions
4. Provide clear, actionable error messages on dimension mismatch
5. Support future configuration for alternative embedding models
6. Implement fast-fail startup behavior to prevent corrupted data

---

## Acceptance Criteria

- [ ] `IDimensionValidationService` interface defined for dimension validation
- [ ] `DimensionValidationService` implementation validates Ollama model dimensions
- [ ] Database schema dimension verification queries vector column metadata
- [ ] Startup fails fast with `DimensionMismatchException` on inconsistency
- [ ] Error messages specify expected vs actual dimensions and which component mismatched
- [ ] Configuration option for expected dimensions (default 1024, anticipating future models)
- [ ] Unit tests cover validation logic with dimension mismatch scenarios
- [ ] Integration tests verify end-to-end validation with real Ollama and PostgreSQL
- [ ] Validation completes within 10 seconds under normal conditions

---

## Implementation Notes

### 1. Dimension Configuration

Create a configuration record for embedding dimension settings:

```csharp
// src/CompoundDocs.Common/Configuration/EmbeddingDimensionOptions.cs
namespace CompoundDocs.Common.Configuration;

/// <summary>
/// Configuration for embedding dimension validation.
/// </summary>
public sealed record EmbeddingDimensionOptions
{
    public const string SectionName = "EmbeddingDimensions";

    /// <summary>
    /// Expected embedding dimensions from the Ollama model.
    /// Default is 1024 for mxbai-embed-large.
    /// </summary>
    public int ExpectedDimensions { get; init; } = 1024;

    /// <summary>
    /// Whether to skip dimension validation at startup.
    /// Should only be used for testing scenarios.
    /// </summary>
    public bool SkipValidation { get; init; } = false;

    /// <summary>
    /// Timeout for validation operations in seconds.
    /// </summary>
    public int ValidationTimeoutSeconds { get; init; } = 30;
}
```

### 2. Dimension Mismatch Exception

Create a specific exception for dimension mismatches:

```csharp
// src/CompoundDocs.Common/Exceptions/DimensionMismatchException.cs
namespace CompoundDocs.Common.Exceptions;

/// <summary>
/// Thrown when embedding dimensions don't match expected values.
/// This is a startup-fatal error that prevents data corruption.
/// </summary>
public sealed class DimensionMismatchException : Exception
{
    public string Component { get; }
    public int ExpectedDimensions { get; }
    public int ActualDimensions { get; }

    public DimensionMismatchException(
        string component,
        int expectedDimensions,
        int actualDimensions)
        : base(BuildMessage(component, expectedDimensions, actualDimensions))
    {
        Component = component;
        ExpectedDimensions = expectedDimensions;
        ActualDimensions = actualDimensions;
    }

    private static string BuildMessage(string component, int expected, int actual)
    {
        return $"Embedding dimension mismatch in {component}: " +
               $"expected {expected} dimensions, but got {actual}. " +
               $"This would cause data corruption. " +
               GetRemediationHint(component, expected, actual);
    }

    private static string GetRemediationHint(string component, int expected, int actual)
    {
        return component switch
        {
            "Ollama" => $"Ensure the embedding model (mxbai-embed-large) produces {expected}-dimensional vectors. " +
                        $"The current model may be different or misconfigured.",
            "Database" => $"Database vector columns are configured for {actual} dimensions but application expects {expected}. " +
                          $"Recreate the vector collections or update configuration.",
            _ => "Check embedding model and database schema configuration."
        };
    }
}
```

### 3. Dimension Validation Service Interface

```csharp
// src/CompoundDocs.Common/Services/IDimensionValidationService.cs
namespace CompoundDocs.Common.Services;

/// <summary>
/// Validates embedding dimensions across system components.
/// </summary>
public interface IDimensionValidationService
{
    /// <summary>
    /// Validates that all components use consistent embedding dimensions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with details on each component.</returns>
    /// <exception cref="DimensionMismatchException">
    /// Thrown when any component has mismatched dimensions.
    /// </exception>
    Task<DimensionValidationResult> ValidateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the expected embedding dimensions from configuration.
    /// </summary>
    int ExpectedDimensions { get; }
}

/// <summary>
/// Result of dimension validation across components.
/// </summary>
public sealed record DimensionValidationResult
{
    public required int ExpectedDimensions { get; init; }
    public required OllamaValidationResult Ollama { get; init; }
    public required DatabaseValidationResult Database { get; init; }
    public bool IsValid => Ollama.IsValid && Database.IsValid;
}

public sealed record OllamaValidationResult
{
    public required bool IsValid { get; init; }
    public required int ActualDimensions { get; init; }
    public required string ModelId { get; init; }
    public TimeSpan ValidationDuration { get; init; }
}

public sealed record DatabaseValidationResult
{
    public required bool IsValid { get; init; }
    public required IReadOnlyDictionary<string, int> CollectionDimensions { get; init; }
    public string? MismatchedCollection { get; init; }
}
```

### 4. Dimension Validation Service Implementation

```csharp
// src/CompoundDocs.McpServer/Services/DimensionValidationService.cs
using System.Diagnostics;
using CompoundDocs.Common.Configuration;
using CompoundDocs.Common.Exceptions;
using CompoundDocs.Common.Services;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Validates embedding dimensions match across Ollama and database.
/// </summary>
public sealed class DimensionValidationService : IDimensionValidationService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<DimensionValidationService> _logger;
    private readonly EmbeddingDimensionOptions _options;

    // Vector collections that must be validated
    private static readonly string[] VectorCollections =
    [
        "documents",
        "document_chunks",
        "external_documents",
        "external_document_chunks"
    ];

    public int ExpectedDimensions => _options.ExpectedDimensions;

    public DimensionValidationService(
        IEmbeddingService embeddingService,
        NpgsqlDataSource dataSource,
        IOptions<EmbeddingDimensionOptions> options,
        ILogger<DimensionValidationService> logger)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DimensionValidationResult> ValidateAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting embedding dimension validation. Expected: {Expected} dimensions",
            ExpectedDimensions);

        // Validate Ollama first (it's the source of truth for embeddings)
        var ollamaResult = await ValidateOllamaAsync(cancellationToken);

        if (!ollamaResult.IsValid)
        {
            throw new DimensionMismatchException(
                "Ollama",
                ExpectedDimensions,
                ollamaResult.ActualDimensions);
        }

        // Validate database schema
        var databaseResult = await ValidateDatabaseAsync(cancellationToken);

        if (!databaseResult.IsValid && databaseResult.MismatchedCollection is not null)
        {
            var actualDims = databaseResult.CollectionDimensions[databaseResult.MismatchedCollection];
            throw new DimensionMismatchException(
                "Database",
                ExpectedDimensions,
                actualDims);
        }

        var result = new DimensionValidationResult
        {
            ExpectedDimensions = ExpectedDimensions,
            Ollama = ollamaResult,
            Database = databaseResult
        };

        _logger.LogInformation(
            "Embedding dimension validation completed successfully. " +
            "Ollama ({Model}): {OllamaDims} dims, Database collections validated: {CollectionCount}",
            ollamaResult.ModelId,
            ollamaResult.ActualDimensions,
            databaseResult.CollectionDimensions.Count);

        return result;
    }

    private async Task<OllamaValidationResult> ValidateOllamaAsync(
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Validating Ollama embedding dimensions...");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Generate a test embedding to check dimensions
            var testEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                "Dimension validation test embedding",
                cancellationToken);

            stopwatch.Stop();

            var isValid = testEmbedding.Length == ExpectedDimensions;

            _logger.LogDebug(
                "Ollama validation complete: {Dimensions} dimensions in {Duration}ms",
                testEmbedding.Length,
                stopwatch.ElapsedMilliseconds);

            return new OllamaValidationResult
            {
                IsValid = isValid,
                ActualDimensions = testEmbedding.Length,
                ModelId = SemanticKernelEmbeddingService.ModelId,
                ValidationDuration = stopwatch.Elapsed
            };
        }
        catch (Exception ex) when (ex is not DimensionMismatchException)
        {
            _logger.LogError(ex,
                "Failed to validate Ollama embedding dimensions. " +
                "Ensure Ollama is running and mxbai-embed-large model is available.");
            throw;
        }
    }

    private async Task<DatabaseValidationResult> ValidateDatabaseAsync(
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Validating database vector column dimensions...");

        var collectionDimensions = new Dictionary<string, int>();
        string? mismatchedCollection = null;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        foreach (var collection in VectorCollections)
        {
            var dimensions = await GetVectorColumnDimensionsAsync(
                connection,
                "compounding",
                collection,
                "embedding",
                cancellationToken);

            if (dimensions.HasValue)
            {
                collectionDimensions[collection] = dimensions.Value;

                if (dimensions.Value != ExpectedDimensions && mismatchedCollection is null)
                {
                    mismatchedCollection = collection;
                    _logger.LogError(
                        "Database dimension mismatch in collection {Collection}: " +
                        "expected {Expected}, found {Actual}",
                        collection,
                        ExpectedDimensions,
                        dimensions.Value);
                }
            }
            else
            {
                _logger.LogDebug(
                    "Collection {Collection} not yet created (will be created by Semantic Kernel)",
                    collection);
            }
        }

        return new DatabaseValidationResult
        {
            IsValid = mismatchedCollection is null,
            CollectionDimensions = collectionDimensions,
            MismatchedCollection = mismatchedCollection
        };
    }

    private static async Task<int?> GetVectorColumnDimensionsAsync(
        NpgsqlConnection connection,
        string schema,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        // Query PostgreSQL information_schema to get vector column dimensions
        // pgvector stores dimension info in the column type modifier
        const string sql = """
            SELECT atttypmod
            FROM pg_attribute a
            JOIN pg_class c ON a.attrelid = c.oid
            JOIN pg_namespace n ON c.relnamespace = n.oid
            WHERE n.nspname = @schema
              AND c.relname = @table
              AND a.attname = @column
              AND a.atttypid = 'vector'::regtype;
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        cmd.Parameters.AddWithValue("column", column);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);

        if (result is null or DBNull)
        {
            return null; // Table or column doesn't exist yet
        }

        // atttypmod for vector type contains the dimension count
        return (int)result;
    }
}
```

### 5. Startup Validation Hosted Service

```csharp
// src/CompoundDocs.McpServer/Services/DimensionValidationHostedService.cs
using CompoundDocs.Common.Configuration;
using CompoundDocs.Common.Services;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Runs dimension validation at application startup.
/// Fails startup if dimensions are inconsistent.
/// </summary>
public sealed class DimensionValidationHostedService : IHostedService
{
    private readonly IDimensionValidationService _validationService;
    private readonly ILogger<DimensionValidationHostedService> _logger;
    private readonly EmbeddingDimensionOptions _options;
    private readonly IHostApplicationLifetime _lifetime;

    public DimensionValidationHostedService(
        IDimensionValidationService validationService,
        IOptions<EmbeddingDimensionOptions> options,
        IHostApplicationLifetime lifetime,
        ILogger<DimensionValidationHostedService> logger)
    {
        _validationService = validationService;
        _options = options.Value;
        _lifetime = lifetime;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.SkipValidation)
        {
            _logger.LogWarning(
                "Embedding dimension validation SKIPPED. " +
                "This should only be used for testing.");
            return;
        }

        _logger.LogInformation(
            "Starting embedding dimension validation " +
            "(expected: {Expected} dimensions)...",
            _options.ExpectedDimensions);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.ValidationTimeoutSeconds));

            var result = await _validationService.ValidateAsync(cts.Token);

            _logger.LogInformation(
                "Dimension validation successful. " +
                "Ollama: {OllamaDims} dims ({OllamaModel}), " +
                "Database collections: {DbCollections}",
                result.Ollama.ActualDimensions,
                result.Ollama.ModelId,
                string.Join(", ", result.Database.CollectionDimensions.Keys));
        }
        catch (DimensionMismatchException ex)
        {
            _logger.LogCritical(ex,
                "STARTUP FAILED: Embedding dimension mismatch detected. " +
                "Component: {Component}, Expected: {Expected}, Actual: {Actual}",
                ex.Component,
                ex.ExpectedDimensions,
                ex.ActualDimensions);

            // Request application shutdown
            _lifetime.StopApplication();
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Dimension validation cancelled during shutdown.");
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogCritical(
                "STARTUP FAILED: Dimension validation timed out after {Timeout} seconds. " +
                "Check Ollama connectivity and database availability.",
                _options.ValidationTimeoutSeconds);

            _lifetime.StopApplication();
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "STARTUP FAILED: Unexpected error during dimension validation.");

            _lifetime.StopApplication();
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

### 6. Service Registration

```csharp
// In ServiceCollectionExtensions.cs
public static IServiceCollection AddDimensionValidation(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Bind configuration
    services.Configure<EmbeddingDimensionOptions>(
        configuration.GetSection(EmbeddingDimensionOptions.SectionName));

    // Register validation service
    services.AddSingleton<IDimensionValidationService, DimensionValidationService>();

    // Register startup validation (runs after embedding service is available)
    services.AddHostedService<DimensionValidationHostedService>();

    return services;
}
```

### 7. Configuration Schema

Add to `appsettings.json`:

```json
{
  "EmbeddingDimensions": {
    "ExpectedDimensions": 1024,
    "SkipValidation": false,
    "ValidationTimeoutSeconds": 30
  }
}
```

---

## Dependencies

### Depends On

- **Phase 029**: Embedding Service Implementation - `IEmbeddingService` for generating test embeddings
- **Phase 041**: Vector Store Collections - Database schema must exist for validation

### Blocks

- **Phase 048+**: Production deployment - Must have validation in place before production use

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Services/DimensionValidationServiceTests.cs
public class DimensionValidationServiceTests
{
    [Fact]
    public async Task ValidateAsync_WithCorrectDimensions_ReturnsValidResult()
    {
        // Arrange
        var mockEmbeddingService = new Mock<IEmbeddingService>();
        mockEmbeddingService
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new ReadOnlyMemory<float>(new float[1024]));

        var mockDataSource = CreateMockDataSource(1024);
        var options = Options.Create(new EmbeddingDimensionOptions
        {
            ExpectedDimensions = 1024
        });

        var service = new DimensionValidationService(
            mockEmbeddingService.Object,
            mockDataSource,
            options,
            Mock.Of<ILogger<DimensionValidationService>>());

        // Act
        var result = await service.ValidateAsync();

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(1024, result.Ollama.ActualDimensions);
    }

    [Fact]
    public async Task ValidateAsync_WithOllamaDimensionMismatch_ThrowsException()
    {
        // Arrange
        var mockEmbeddingService = new Mock<IEmbeddingService>();
        mockEmbeddingService
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new ReadOnlyMemory<float>(new float[768])); // Wrong!

        var options = Options.Create(new EmbeddingDimensionOptions
        {
            ExpectedDimensions = 1024
        });

        var service = new DimensionValidationService(
            mockEmbeddingService.Object,
            Mock.Of<NpgsqlDataSource>(),
            options,
            Mock.Of<ILogger<DimensionValidationService>>());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DimensionMismatchException>(
            () => service.ValidateAsync());

        Assert.Equal("Ollama", ex.Component);
        Assert.Equal(1024, ex.ExpectedDimensions);
        Assert.Equal(768, ex.ActualDimensions);
    }

    [Fact]
    public async Task ValidateAsync_WithDatabaseDimensionMismatch_ThrowsException()
    {
        // Arrange
        var mockEmbeddingService = new Mock<IEmbeddingService>();
        mockEmbeddingService
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new ReadOnlyMemory<float>(new float[1024]));

        var mockDataSource = CreateMockDataSource(512); // Wrong!
        var options = Options.Create(new EmbeddingDimensionOptions
        {
            ExpectedDimensions = 1024
        });

        var service = new DimensionValidationService(
            mockEmbeddingService.Object,
            mockDataSource,
            options,
            Mock.Of<ILogger<DimensionValidationService>>());

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DimensionMismatchException>(
            () => service.ValidateAsync());

        Assert.Equal("Database", ex.Component);
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Services/DimensionValidationIntegrationTests.cs
[Trait("Category", "Integration")]
public class DimensionValidationIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IDimensionValidationService _service;

    public DimensionValidationIntegrationTests(IntegrationTestFixture fixture)
    {
        _service = fixture.GetService<IDimensionValidationService>();
    }

    [Fact]
    public async Task ValidateAsync_WithRealInfrastructure_Succeeds()
    {
        // Act
        var result = await _service.ValidateAsync();

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(1024, result.Ollama.ActualDimensions);
        Assert.Equal("mxbai-embed-large", result.Ollama.ModelId);
        Assert.True(result.Ollama.ValidationDuration < TimeSpan.FromSeconds(10));
    }
}
```

### Manual Verification

```bash
# 1. Start with correct configuration - should succeed
dotnet run --project src/CompoundDocs.McpServer
# Expected: "Dimension validation successful" in logs

# 2. Temporarily change expected dimensions - should fail
# Edit appsettings.json: "ExpectedDimensions": 512
dotnet run --project src/CompoundDocs.McpServer
# Expected: STARTUP FAILED with DimensionMismatchException

# 3. Skip validation for testing
# Edit appsettings.json: "SkipValidation": true
dotnet run --project src/CompoundDocs.McpServer
# Expected: Warning about skipped validation
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.Common/Configuration/EmbeddingDimensionOptions.cs` | Create | Configuration options |
| `src/CompoundDocs.Common/Exceptions/DimensionMismatchException.cs` | Create | Specific exception type |
| `src/CompoundDocs.Common/Services/IDimensionValidationService.cs` | Create | Validation interface |
| `src/CompoundDocs.McpServer/Services/DimensionValidationService.cs` | Create | Validation implementation |
| `src/CompoundDocs.McpServer/Services/DimensionValidationHostedService.cs` | Create | Startup validation |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Add registration |
| `src/CompoundDocs.McpServer/appsettings.json` | Modify | Add configuration section |
| `tests/CompoundDocs.Tests/Services/DimensionValidationServiceTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.IntegrationTests/Services/DimensionValidationIntegrationTests.cs` | Create | Integration tests |

---

## Configuration Reference

### EmbeddingDimensionOptions

| Setting | Default | Description |
|---------|---------|-------------|
| `ExpectedDimensions` | 1024 | Expected embedding dimensions (mxbai-embed-large) |
| `SkipValidation` | false | Skip validation (testing only) |
| `ValidationTimeoutSeconds` | 30 | Timeout for validation operations |

### Future Model Changes

When changing embedding models in the future:

1. Update `ExpectedDimensions` in configuration
2. Update `ModelId` constant in `SemanticKernelEmbeddingService`
3. Recreate all vector collections (Semantic Kernel will create with new dimensions)
4. Re-index all documents with new embeddings

The validation service will catch any mismatches during this migration process.

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Ollama not responding | Timeout with clear error message and startup failure |
| Database collections not yet created | Skip database validation for non-existent collections (expected on first run) |
| Validation takes too long | Configurable timeout (default 30s) |
| False positive on dimension check | Validation uses actual embedding generation, not metadata |
| Model change without config update | Fast-fail startup prevents silent data corruption |
| Accidental production skip | `SkipValidation` logs warning-level message |

---

## Success Criteria

1. Startup validates Ollama produces 1024-dimensional embeddings
2. Startup validates existing database vector columns have 1024 dimensions
3. Dimension mismatch causes immediate, clear startup failure
4. Error messages provide actionable remediation steps
5. Validation completes in under 10 seconds for normal operation
6. Configuration supports future embedding model changes
7. Skipping validation logs prominent warning
