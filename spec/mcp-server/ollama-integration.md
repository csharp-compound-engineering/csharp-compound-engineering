# Ollama Integration

> **Status**: [DRAFT]
> **Parent**: [../mcp-server.md](../mcp-server.md)

---

## Overview

The MCP server uses Ollama for embedding generation and RAG synthesis. This document covers configuration, HttpClient setup, resilience patterns, and graceful degradation.

> **Background**: Comprehensive coverage of Semantic Kernel's Ollama connector, embedding generation, and RAG pipeline implementation patterns. See [Semantic Kernel + Ollama RAG Research](../../research/semantic-kernel-ollama-rag-research.md).

---

## Embedding Model

| Setting | Value |
|---------|-------|
| **Model** | `mxbai-embed-large` (fixed) |
| **Dimensions** | 1024 |
| **Auto-pull** | Model pulled automatically on first use if not present |

### Model Selection Rationale

`mxbai-embed-large` was chosen for:
- High-quality embeddings (outperforms many alternatives on benchmarks)
- Reasonable size (1024 dimensions balances quality vs. storage)
- Good performance on Apple Silicon
- Active maintenance and updates

---

## RAG Generation Model

> **Background**: Detailed analysis of running embedding + LLM models simultaneously, memory management, `OLLAMA_KEEP_ALIVE` settings, and recommended model combinations. See [Ollama Multi-Model Research](../../research/ollama-multi-model-research.md).

| Setting | Value |
|---------|-------|
| **Model** | Configurable via `~/.claude/.csharp-compounding-docs/ollama-config.json` |
| **Default** | `mistral` |
| **Auto-pull** | Model pulled automatically on first use if not present |

### System Prompt

The RAG generation model uses a system prompt tailored for synthesizing answers from documentation context:

```
You are a helpful assistant that answers questions based on the provided documentation context.
- Always cite your sources by referencing the document paths
- If the context doesn't contain enough information, say so clearly
- Focus on accuracy over completeness
- Use code examples from the context when relevant
```

---

## Connection

The MCP server receives Ollama host:port as a command-line argument from the launcher script:

```bash
./mcp-server --ollama-host http://localhost:11434
```

### Default Configuration

| Parameter | Default | Description |
|-----------|---------|-------------|
| Host | `http://localhost:11434` | Ollama API endpoint |
| Timeout | 5 minutes | Long timeout for embedding large documents |

---

## HttpClient Configuration

```csharp
builder.Services.AddHttpClient("ollama", client =>
{
    client.BaseAddress = new Uri("http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(5);  // Long timeout for embeddings
})
.AddResilienceHandler("ollama-resilience", builder =>
{
    // Rate limiter (align with OLLAMA_NUM_PARALLEL)
    builder.AddRateLimiter(new ConcurrencyLimiter(
        new ConcurrencyLimiterOptions
        {
            PermitLimit = 2,  // Match OLLAMA_NUM_PARALLEL
            QueueLimit = 10,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        }));

    // Retry on transient errors
    builder.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromSeconds(1)
    });

    // Circuit breaker
    builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,           // 50% failure ratio
        MinimumThroughput = 5,         // 5 requests minimum
        SamplingDuration = TimeSpan.FromSeconds(30),  // 30s sampling
        BreakDuration = TimeSpan.FromSeconds(30)      // 30s break
    });
});
```

---

## Resilience and Circuit Breaker

### Required Packages

```xml
<PackageReference Include="Polly.Core" Version="8.*" />
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.*" />
```

### Retry Policies

| Error Type | Retry | Max Attempts | Backoff | Fallback |
|------------|-------|--------------|---------|----------|
| `EMBEDDING_SERVICE_ERROR` | Yes | 3 | Exponential (1s, 2s, 4s) | Return error with retry suggestion |
| `DATABASE_ERROR` | Yes | 3 | Exponential (100ms, 200ms, 400ms) | Return error |
| `FILE_SYSTEM_ERROR` | No | - | - | Log and skip, continue with other files |
| `SCHEMA_VALIDATION_FAILED` | No | - | - | Return specific field errors |

### Circuit Breaker (Ollama)

Ollama requests use a circuit breaker to prevent cascading failures:

| Parameter | Value |
|-----------|-------|
| Failure ratio | 50% |
| Minimum throughput | 5 requests |
| Sampling duration | 30 seconds |
| Break duration | 30 seconds |

### Circuit Breaker States

```
        Requests succeed
              |
              v
    +------------------+
    |     CLOSED       |  <-- Normal operation
    |  (requests flow) |
    +------------------+
              |
        50% failures over
        5+ requests in 30s
              |
              v
    +------------------+
    |      OPEN        |  <-- All requests fail fast
    | (30s break time) |
    +------------------+
              |
        30 seconds elapsed
              |
              v
    +------------------+
    |   HALF-OPEN      |  <-- Test with 1 request
    | (1 test request) |
    +------------------+
              |
         +----+----+
         |         |
      Success    Failure
         |         |
         v         v
      CLOSED     OPEN
```

---

## Graceful Degradation

When Ollama is unavailable:

### RAG Queries

Return error with helpful message:
```json
{
  "error": true,
  "code": "EMBEDDING_SERVICE_ERROR",
  "message": "Embedding service unavailable. The circuit breaker is open. Try again in 30 seconds.",
  "details": {
    "circuit_state": "open",
    "retry_after_seconds": 30
  }
}
```

### File Watcher

Documents are queued for later indexing:
- Queue stored in memory (not persisted)
- Reprocessed on next startup via reconciliation
- See [file-watcher.md](./file-watcher.md#crash-recovery) for details

### Semantic Search

Return error (no fallback to full-text search in MVP):
```json
{
  "error": true,
  "code": "EMBEDDING_SERVICE_ERROR",
  "message": "Cannot perform semantic search: embedding service unavailable.",
  "details": {}
}
```

---

## Apple Silicon Note

> **See also**: [infrastructure.md - Apple Silicon Note](../infrastructure.md#apple-silicon-note) for launcher script detection and infrastructure setup.

> **Background**: Technical details on why GPU acceleration is not available in Docker on macOS, performance comparisons between native and Docker Ollama, and Docker Compose configurations for various GPU platforms. See [Ollama Docker GPU Research](../../research/ollama-docker-gpu-research.md).

For macOS with Apple Silicon:
- The launcher script detects Apple Silicon architecture
- Assumes Ollama is running natively (Metal acceleration not available in Docker)
- Looks for Ollama at the default port (11434)

### Missing Ollama Handling

If the MCP server detects that it is running on macOS with Apple Silicon, and the Ollama server isn't running:
- Return clear error to the calling agent
- Suggest reporting to the user that they should start Ollama natively before using the plugin

```json
{
  "error": true,
  "code": "OLLAMA_NOT_RUNNING",
  "message": "Ollama server not detected. On Apple Silicon, Ollama must be running natively for Metal acceleration. Please start Ollama before using this tool.",
  "details": {
    "platform": "darwin-arm64",
    "expected_host": "http://localhost:11434"
  }
}
```

---

## Semantic Kernel Integration

The MCP server uses Semantic Kernel's `ITextEmbeddingGenerationService`:

```csharp
#pragma warning disable SKEXP0070  // Ollama connector is experimental

using Microsoft.SemanticKernel.Embeddings;

// Registration
kernelBuilder.AddOllamaTextEmbeddingGeneration(
    modelId: "mxbai-embed-large",
    endpoint: new Uri("http://localhost:11434")
);

// Usage
var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
ReadOnlyMemory<float> embedding = await embeddingService.GenerateEmbeddingAsync(content, ct);
```

### Testable Wrapper

For unit testing, the SK service is wrapped:

```csharp
/// <summary>
/// Wraps ITextEmbeddingGenerationService for testability.
/// </summary>
public interface IEmbeddingService
{
    Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string content,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Production implementation using Semantic Kernel.
/// </summary>
public class SemanticKernelEmbeddingService : IEmbeddingService
{
    private readonly ITextEmbeddingGenerationService _skService;

    public SemanticKernelEmbeddingService(ITextEmbeddingGenerationService skService)
        => _skService = skService;

    public Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string content, CancellationToken ct)
        => _skService.GenerateEmbeddingAsync(content, kernel: null, ct);
}
```

---

## Rate Limiting

### OLLAMA_NUM_PARALLEL Alignment

The HttpClient rate limiter should match Ollama's `OLLAMA_NUM_PARALLEL` environment variable:

| Setting | Purpose |
|---------|---------|
| `PermitLimit = 2` | Maximum concurrent requests to Ollama |
| `QueueLimit = 10` | Requests waiting when at capacity |
| `QueueProcessingOrder.OldestFirst` | FIFO ordering for fairness |

### Adjusting for Different Hardware

| Hardware | Recommended OLLAMA_NUM_PARALLEL | PermitLimit |
|----------|--------------------------------|-------------|
| Apple M1/M2 (8GB) | 1 | 1 |
| Apple M1/M2 (16GB+) | 2 | 2 |
| NVIDIA GPU (8GB) | 2 | 2 |
| NVIDIA GPU (16GB+) | 4 | 4 |
| CPU only | 1 | 1 |

---

## Research References

- [Microsoft Semantic Kernel Research](../../research/microsoft-semantic-kernel-research.md)
- [Semantic Kernel Request Queuing Research](../../research/semantic-kernel-request-queuing-research.md)

---

## Related Files

- [tools.md](./tools.md) - Tools that use embedding generation
- [file-watcher.md](./file-watcher.md) - Embedding generation on file changes
- [chunking.md](./chunking.md) - Embedding generation for chunks
- [../infrastructure.md](../infrastructure.md) - Ollama deployment options
