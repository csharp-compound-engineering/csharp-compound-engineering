# Phase 029: Embedding Service Implementation

> **Status**: PLANNED
> **Category**: MCP Server Core
> **Estimated Effort**: M
> **Prerequisites**: Phase 028 (Semantic Kernel Integration)

---

## Spec References

- [mcp-server/ollama-integration.md - Embedding Model](../spec/mcp-server/ollama-integration.md#embedding-model)
- [mcp-server/ollama-integration.md - Semantic Kernel Integration](../spec/mcp-server/ollama-integration.md#semantic-kernel-integration)
- [mcp-server/ollama-integration.md - HttpClient Configuration](../spec/mcp-server/ollama-integration.md#httpclient-configuration)
- [research/semantic-kernel-ollama-rag-research.md - Embedding Generation](../research/semantic-kernel-ollama-rag-research.md#3-embedding-generation-with-ollama)

---

## Objectives

1. Create `IEmbeddingService` wrapper interface for testability
2. Implement `SemanticKernelEmbeddingService` production wrapper
3. Configure Ollama embedding client with `mxbai-embed-large` model (1024 dimensions)
4. Set up HttpClient with 5-minute timeout for large document processing
5. Implement model auto-pull capability for first-run scenarios
6. Add embedding dimension validation at startup
7. Register embedding service in dependency injection container

---

## Acceptance Criteria

- [ ] `IEmbeddingService` interface defined with `GenerateEmbeddingAsync` method
- [ ] `SemanticKernelEmbeddingService` wraps `ITextEmbeddingGenerationService`
- [ ] Embedding model fixed to `mxbai-embed-large` (not configurable)
- [ ] HttpClient timeout configured to 5 minutes (300 seconds)
- [ ] Startup validation confirms embedding dimensions are 1024
- [ ] Model auto-pull implemented if model not present on first use
- [ ] Unit tests cover embedding service wrapper
- [ ] Integration test verifies actual embedding generation with Ollama
- [ ] Proper `#pragma warning disable SKEXP0070` for experimental Ollama connector

---

## Implementation Notes

### 1. IEmbeddingService Interface

Create a testable wrapper interface in `CompoundDocs.Common`:

```csharp
// src/CompoundDocs.Common/Services/IEmbeddingService.cs
namespace CompoundDocs.Common.Services;

/// <summary>
/// Abstraction over ITextEmbeddingGenerationService for testability.
/// Generates vector embeddings from text content.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates a vector embedding for the given text content.
    /// </summary>
    /// <param name="content">The text content to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A 1024-dimensional float vector representing the content.</returns>
    Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates vector embeddings for multiple text contents in batch.
    /// </summary>
    /// <param name="contents">The text contents to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of 1024-dimensional float vectors.</returns>
    Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> contents,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the dimension count of embeddings produced by this service.
    /// </summary>
    int EmbeddingDimensions { get; }
}
```

### 2. SemanticKernelEmbeddingService Implementation

```csharp
// src/CompoundDocs.McpServer/Services/SemanticKernelEmbeddingService.cs
#pragma warning disable SKEXP0070 // Ollama connector is experimental

using CompoundDocs.Common.Services;
using Microsoft.SemanticKernel.Embeddings;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Production implementation of IEmbeddingService using Semantic Kernel's
/// Ollama connector with mxbai-embed-large model.
/// </summary>
public sealed class SemanticKernelEmbeddingService : IEmbeddingService
{
    private readonly ITextEmbeddingGenerationService _skService;
    private readonly ILogger<SemanticKernelEmbeddingService> _logger;

    /// <summary>
    /// mxbai-embed-large produces 1024-dimensional embeddings.
    /// </summary>
    public const int ExpectedDimensions = 1024;

    /// <summary>
    /// Fixed embedding model - not configurable.
    /// </summary>
    public const string ModelId = "mxbai-embed-large";

    public int EmbeddingDimensions => ExpectedDimensions;

    public SemanticKernelEmbeddingService(
        ITextEmbeddingGenerationService skService,
        ILogger<SemanticKernelEmbeddingService> logger)
    {
        _skService = skService ?? throw new ArgumentNullException(nameof(skService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        _logger.LogDebug("Generating embedding for content of length {Length}", content.Length);

        var embedding = await _skService.GenerateEmbeddingAsync(
            content,
            kernel: null,
            cancellationToken);

        ValidateEmbeddingDimensions(embedding.Length);

        return embedding;
    }

    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> contents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contents);

        if (contents.Count == 0)
        {
            return [];
        }

        _logger.LogDebug("Generating embeddings for {Count} contents", contents.Count);

        var embeddings = await _skService.GenerateEmbeddingsAsync(
            contents,
            kernel: null,
            cancellationToken);

        // Validate all embeddings have correct dimensions
        foreach (var embedding in embeddings)
        {
            ValidateEmbeddingDimensions(embedding.Length);
        }

        return embeddings;
    }

    private void ValidateEmbeddingDimensions(int actualDimensions)
    {
        if (actualDimensions != ExpectedDimensions)
        {
            _logger.LogError(
                "Embedding dimension mismatch: expected {Expected}, got {Actual}",
                ExpectedDimensions,
                actualDimensions);

            throw new InvalidOperationException(
                $"Embedding dimension mismatch: expected {ExpectedDimensions}, " +
                $"got {actualDimensions}. Ensure mxbai-embed-large model is being used.");
        }
    }
}
```

### 3. HttpClient Configuration

Configure the HttpClient factory for Ollama with appropriate timeout:

```csharp
// In ServiceCollectionExtensions.cs or Program.cs
services.AddHttpClient("ollama", client =>
{
    client.BaseAddress = new Uri(ollamaEndpoint);
    client.Timeout = TimeSpan.FromMinutes(5); // Long timeout for embedding large documents
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
    MaxConnectionsPerServer = 10,
    EnableMultipleHttp2Connections = true
});
```

### 4. Semantic Kernel Registration

Register the Ollama embedding service with Semantic Kernel:

```csharp
#pragma warning disable SKEXP0070

// Register Ollama text embedding generation
kernelBuilder.AddOllamaTextEmbeddingGeneration(
    modelId: SemanticKernelEmbeddingService.ModelId, // "mxbai-embed-large"
    endpoint: new Uri(ollamaEndpoint),
    serviceId: "ollama-embeddings"
);

// Register our wrapper service
services.AddSingleton<IEmbeddingService, SemanticKernelEmbeddingService>();
```

### 5. Startup Dimension Validation

Implement a startup check to validate embedding dimensions:

```csharp
// src/CompoundDocs.McpServer/Services/EmbeddingServiceValidator.cs
public sealed class EmbeddingServiceValidator : IHostedService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<EmbeddingServiceValidator> _logger;

    public EmbeddingServiceValidator(
        IEmbeddingService embeddingService,
        ILogger<EmbeddingServiceValidator> logger)
    {
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Validating embedding service configuration...");

        try
        {
            // Generate a test embedding to verify dimensions
            var testEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                "Embedding service validation test",
                cancellationToken);

            if (testEmbedding.Length != SemanticKernelEmbeddingService.ExpectedDimensions)
            {
                throw new InvalidOperationException(
                    $"Embedding service returned {testEmbedding.Length} dimensions, " +
                    $"expected {SemanticKernelEmbeddingService.ExpectedDimensions}. " +
                    $"Ensure mxbai-embed-large model is installed.");
            }

            _logger.LogInformation(
                "Embedding service validated: {Dimensions}-dimensional embeddings",
                testEmbedding.Length);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "Failed to connect to Ollama for embedding validation. " +
                "Ensure Ollama is running and mxbai-embed-large model is available.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

### 6. Model Auto-Pull Capability

Implement auto-pull functionality for first-run scenarios:

```csharp
// src/CompoundDocs.McpServer/Services/OllamaModelManager.cs
public interface IOllamaModelManager
{
    Task EnsureModelAvailableAsync(string modelId, CancellationToken cancellationToken = default);
    Task<bool> IsModelAvailableAsync(string modelId, CancellationToken cancellationToken = default);
}

public sealed class OllamaModelManager : IOllamaModelManager
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaModelManager> _logger;

    public OllamaModelManager(
        IHttpClientFactory httpClientFactory,
        ILogger<OllamaModelManager> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ollama");
        _logger = logger;
    }

    public async Task<bool> IsModelAvailableAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var tagsResponse = JsonSerializer.Deserialize<OllamaTagsResponse>(content);

            return tagsResponse?.Models?.Any(m =>
                m.Name.Equals(modelId, StringComparison.OrdinalIgnoreCase) ||
                m.Name.StartsWith($"{modelId}:", StringComparison.OrdinalIgnoreCase)) ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check if model {ModelId} is available", modelId);
            return false;
        }
    }

    public async Task EnsureModelAvailableAsync(
        string modelId,
        CancellationToken cancellationToken = default)
    {
        if (await IsModelAvailableAsync(modelId, cancellationToken))
        {
            _logger.LogDebug("Model {ModelId} is already available", modelId);
            return;
        }

        _logger.LogInformation("Pulling model {ModelId}. This may take several minutes...", modelId);

        var pullRequest = new { name = modelId };
        var content = new StringContent(
            JsonSerializer.Serialize(pullRequest),
            Encoding.UTF8,
            "application/json");

        // Model pulls can take a long time, use longer timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(30)); // 30 minute timeout for model download

        var response = await _httpClient.PostAsync("/api/pull", content, cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to pull model {modelId}: {response.StatusCode} - {errorContent}");
        }

        // Stream the response to monitor progress
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (!string.IsNullOrEmpty(line))
            {
                var progress = JsonSerializer.Deserialize<OllamaPullProgress>(line);
                if (progress?.Status != null)
                {
                    _logger.LogDebug("Model pull progress: {Status}", progress.Status);
                }
            }
        }

        _logger.LogInformation("Model {ModelId} pulled successfully", modelId);
    }

    private record OllamaTagsResponse(List<OllamaModel>? Models);
    private record OllamaModel(string Name, string? ModifiedAt, long? Size);
    private record OllamaPullProgress(string? Status, long? Completed, long? Total);
}
```

### 7. Startup Model Initialization

Ensure embedding model is available at startup:

```csharp
// src/CompoundDocs.McpServer/Services/EmbeddingModelInitializer.cs
public sealed class EmbeddingModelInitializer : IHostedService
{
    private readonly IOllamaModelManager _modelManager;
    private readonly ILogger<EmbeddingModelInitializer> _logger;

    public EmbeddingModelInitializer(
        IOllamaModelManager modelManager,
        ILogger<EmbeddingModelInitializer> logger)
    {
        _modelManager = modelManager;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Ensuring embedding model {ModelId} is available...",
            SemanticKernelEmbeddingService.ModelId);

        await _modelManager.EnsureModelAvailableAsync(
            SemanticKernelEmbeddingService.ModelId,
            cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

### 8. Service Registration

```csharp
// In ServiceCollectionExtensions.cs
public static IServiceCollection AddEmbeddingServices(
    this IServiceCollection services,
    string ollamaEndpoint)
{
    // HttpClient for Ollama with long timeout
    services.AddHttpClient("ollama", client =>
    {
        client.BaseAddress = new Uri(ollamaEndpoint);
        client.Timeout = TimeSpan.FromMinutes(5);
    });

    // Model manager for auto-pull
    services.AddSingleton<IOllamaModelManager, OllamaModelManager>();

    // Embedding service wrapper
    services.AddSingleton<IEmbeddingService, SemanticKernelEmbeddingService>();

    // Startup services (order matters)
    services.AddHostedService<EmbeddingModelInitializer>();  // First: ensure model available
    services.AddHostedService<EmbeddingServiceValidator>();   // Second: validate dimensions

    return services;
}
```

---

## Dependencies

### Depends On

- **Phase 028**: Semantic Kernel Integration - Kernel builder and ITextEmbeddingGenerationService registration

### Blocks

- **Phase 030**: RAG Generation Service - Needs embedding service for query embeddings
- **Phase 031+**: Chunking and Indexing - Needs embedding service for document embeddings
- **Phase 035+**: Vector Store Integration - Embeddings stored in pgvector

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Services/SemanticKernelEmbeddingServiceTests.cs
public class SemanticKernelEmbeddingServiceTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_ReturnsCorrectDimensions()
    {
        // Arrange
        var mockSkService = new Mock<ITextEmbeddingGenerationService>();
        var expectedEmbedding = new float[1024];
        mockSkService
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), null, default))
            .ReturnsAsync(new ReadOnlyMemory<float>(expectedEmbedding));

        var service = new SemanticKernelEmbeddingService(
            mockSkService.Object,
            Mock.Of<ILogger<SemanticKernelEmbeddingService>>());

        // Act
        var result = await service.GenerateEmbeddingAsync("test content");

        // Assert
        Assert.Equal(1024, result.Length);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ThrowsOnDimensionMismatch()
    {
        // Arrange
        var mockSkService = new Mock<ITextEmbeddingGenerationService>();
        var wrongEmbedding = new float[512]; // Wrong dimensions
        mockSkService
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), null, default))
            .ReturnsAsync(new ReadOnlyMemory<float>(wrongEmbedding));

        var service = new SemanticKernelEmbeddingService(
            mockSkService.Object,
            Mock.Of<ILogger<SemanticKernelEmbeddingService>>());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.GenerateEmbeddingAsync("test content"));
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ThrowsOnEmptyContent()
    {
        var service = new SemanticKernelEmbeddingService(
            Mock.Of<ITextEmbeddingGenerationService>(),
            Mock.Of<ILogger<SemanticKernelEmbeddingService>>());

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GenerateEmbeddingAsync(""));
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Services/EmbeddingServiceIntegrationTests.cs
[Trait("Category", "Integration")]
public class EmbeddingServiceIntegrationTests : IClassFixture<OllamaFixture>
{
    private readonly IEmbeddingService _service;

    public EmbeddingServiceIntegrationTests(OllamaFixture fixture)
    {
        _service = fixture.GetService<IEmbeddingService>();
    }

    [Fact]
    public async Task GenerateEmbedding_WithRealOllama_Returns1024Dimensions()
    {
        // Act
        var embedding = await _service.GenerateEmbeddingAsync(
            "This is a test document for semantic search.");

        // Assert
        Assert.Equal(1024, embedding.Length);
        Assert.All(embedding.ToArray(), f => Assert.False(float.IsNaN(f)));
    }

    [Fact]
    public async Task GenerateEmbeddings_Batch_ReturnsCorrectCount()
    {
        // Arrange
        var contents = new List<string>
        {
            "Document one about C# programming",
            "Document two about .NET development",
            "Document three about semantic search"
        };

        // Act
        var embeddings = await _service.GenerateEmbeddingsAsync(contents);

        // Assert
        Assert.Equal(3, embeddings.Count);
        Assert.All(embeddings, e => Assert.Equal(1024, e.Length));
    }
}
```

### Manual Verification

```bash
# 1. Ensure Ollama is running
curl http://localhost:11434/api/tags

# 2. Check if mxbai-embed-large is available
curl http://localhost:11434/api/tags | jq '.models[] | select(.name | contains("mxbai"))'

# 3. Pull model if not present
curl http://localhost:11434/api/pull -d '{"name": "mxbai-embed-large"}'

# 4. Test embedding generation
curl http://localhost:11434/api/embeddings -d '{
  "model": "mxbai-embed-large",
  "prompt": "Test embedding generation"
}' | jq '.embedding | length'
# Expected output: 1024
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.Common/Services/IEmbeddingService.cs` | Create | Abstraction interface |
| `src/CompoundDocs.McpServer/Services/SemanticKernelEmbeddingService.cs` | Create | Production implementation |
| `src/CompoundDocs.McpServer/Services/OllamaModelManager.cs` | Create | Model auto-pull logic |
| `src/CompoundDocs.McpServer/Services/EmbeddingServiceValidator.cs` | Create | Startup dimension validation |
| `src/CompoundDocs.McpServer/Services/EmbeddingModelInitializer.cs` | Create | Startup model pull |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Add embedding service registration |
| `tests/CompoundDocs.Tests/Services/SemanticKernelEmbeddingServiceTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.IntegrationTests/Services/EmbeddingServiceIntegrationTests.cs` | Create | Integration tests |

---

## Configuration Reference

### HttpClient Settings

| Setting | Value | Rationale |
|---------|-------|-----------|
| Timeout | 5 minutes | Large documents may take time to embed |
| PooledConnectionLifetime | 15 minutes | Efficient connection reuse |
| MaxConnectionsPerServer | 10 | Support concurrent embedding requests |

### Embedding Model Settings

| Setting | Value | Notes |
|---------|-------|-------|
| Model ID | `mxbai-embed-large` | Fixed, not configurable |
| Dimensions | 1024 | Validated at startup |
| Auto-pull timeout | 30 minutes | Model download can be slow |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Ollama not running | Clear error message with startup instructions |
| Model not installed | Auto-pull with progress logging |
| Dimension mismatch | Startup validation fails fast with clear error |
| Embedding timeout | 5-minute HttpClient timeout, configurable per-request |
| Memory pressure on batch | Process in reasonable batch sizes (32 recommended) |
