# Phase 033: Rate Limiting for Ollama

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: MCP Server Core
> **Prerequisites**: Phase 029

---

## Spec References

This phase implements the rate limiting infrastructure defined in:

- **spec/mcp-server/ollama-integration.md** - Rate Limiting Alignment section
- **spec/mcp-server/ollama-integration.md** - HttpClient Configuration section

---

## Objectives

1. Implement HttpClient concurrency control matching OLLAMA_NUM_PARALLEL
2. Create SemaphoreSlim-based request throttling for embedding generation
3. Configure hardware-specific concurrency recommendations
4. Implement queue management for embedding requests
5. Create configurable rate limit options with validation

---

## Acceptance Criteria

### HttpClient Concurrency Limiter

- [ ] `ConcurrencyLimiter` configured with `PermitLimit` matching OLLAMA_NUM_PARALLEL
- [ ] `QueueLimit` set to 10 requests for backpressure handling
- [ ] `QueueProcessingOrder.OldestFirst` for FIFO fairness
- [ ] Rate limiter integrated with resilience pipeline

### SemaphoreSlim Request Throttling

- [ ] `IEmbeddingThrottler` interface defined for testability
- [ ] `SemaphoreSlim`-based implementation for concurrent request limiting
- [ ] Configurable concurrency limit via `OllamaOptions`
- [ ] Async-safe throttling with proper disposal
- [ ] Timeout handling for semaphore acquisition

### Hardware-Specific Configuration

- [ ] `RateLimitOptions` class with hardware profile presets
- [ ] Apple Silicon (8GB) configuration: PermitLimit = 1
- [ ] Apple Silicon (16GB+) configuration: PermitLimit = 2
- [ ] NVIDIA GPU (8GB) configuration: PermitLimit = 2
- [ ] NVIDIA GPU (16GB+) configuration: PermitLimit = 4
- [ ] CPU-only configuration: PermitLimit = 1
- [ ] Auto-detection or manual override support

### Queue Management

- [ ] `IEmbeddingQueue` interface for managing pending requests
- [ ] In-memory queue implementation with bounded capacity
- [ ] Queue overflow handling (reject with 503-equivalent error)
- [ ] Queue depth monitoring for observability
- [ ] Priority support for interactive vs batch requests

### Configuration Options

- [ ] `RateLimitOptions` class with DataAnnotations validation
- [ ] Configuration section in appsettings.json
- [ ] Environment variable override support
- [ ] ValidateOnStart for fail-fast configuration errors

---

## Implementation Notes

### 1. Rate Limiter Configuration

Extend the HttpClient resilience pipeline with rate limiting:

```csharp
// In ServiceCollectionExtensions.cs
public static IServiceCollection AddOllamaHttpClient(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var rateLimitOptions = configuration
        .GetSection(RateLimitOptions.SectionName)
        .Get<RateLimitOptions>() ?? new RateLimitOptions();

    services.AddHttpClient("ollama", client =>
    {
        client.BaseAddress = new Uri(configuration["Ollama:Endpoint"]!);
        client.Timeout = TimeSpan.FromMinutes(5);  // Long timeout for embeddings
    })
    .AddResilienceHandler("ollama-resilience", builder =>
    {
        // Rate limiter (align with OLLAMA_NUM_PARALLEL)
        builder.AddConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = rateLimitOptions.ConcurrencyLimit,
            QueueLimit = rateLimitOptions.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });

        // Retry on transient errors (from Phase 029)
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromSeconds(1)
        });

        // Circuit breaker (from Phase 029)
        builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(30)
        });
    });

    return services;
}
```

### 2. Embedding Throttler Interface

Create `Services/IEmbeddingThrottler.cs`:

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Throttles concurrent embedding requests to prevent overloading Ollama.
/// </summary>
public interface IEmbeddingThrottler : IAsyncDisposable
{
    /// <summary>
    /// Acquires a permit to execute an embedding request.
    /// Blocks until a permit is available or timeout expires.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A disposable that releases the permit when disposed.</returns>
    /// <exception cref="OperationCanceledException">If cancelled or timeout exceeded.</exception>
    Task<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current number of available permits.
    /// </summary>
    int AvailablePermits { get; }

    /// <summary>
    /// Gets the current queue depth of waiting requests.
    /// </summary>
    int QueueDepth { get; }
}
```

### 3. SemaphoreSlim Implementation

Create `Services/SemaphoreSlimEmbeddingThrottler.cs`:

```csharp
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// SemaphoreSlim-based implementation of embedding request throttling.
/// </summary>
public sealed class SemaphoreSlimEmbeddingThrottler : IEmbeddingThrottler
{
    private readonly SemaphoreSlim _semaphore;
    private readonly TimeSpan _acquireTimeout;
    private readonly ILogger<SemaphoreSlimEmbeddingThrottler> _logger;
    private int _queueDepth;
    private bool _disposed;

    public SemaphoreSlimEmbeddingThrottler(
        IOptions<RateLimitOptions> options,
        ILogger<SemaphoreSlimEmbeddingThrottler> logger)
    {
        var config = options.Value;
        _semaphore = new SemaphoreSlim(config.ConcurrencyLimit, config.ConcurrencyLimit);
        _acquireTimeout = TimeSpan.FromSeconds(config.AcquireTimeoutSeconds);
        _logger = logger;
    }

    public int AvailablePermits => _semaphore.CurrentCount;

    public int QueueDepth => _queueDepth;

    public async Task<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Interlocked.Increment(ref _queueDepth);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_acquireTimeout);

            var acquired = await _semaphore.WaitAsync(timeoutCts.Token);

            if (!acquired)
            {
                throw new TimeoutException(
                    $"Failed to acquire embedding permit within {_acquireTimeout.TotalSeconds}s");
            }

            _logger.LogDebug(
                "Acquired embedding permit. Available: {Available}, Queue: {Queue}",
                AvailablePermits, QueueDepth);

            return new PermitReleaser(this);
        }
        finally
        {
            Interlocked.Decrement(ref _queueDepth);
        }
    }

    private void Release()
    {
        if (!_disposed)
        {
            _semaphore.Release();
            _logger.LogDebug("Released embedding permit. Available: {Available}", AvailablePermits);
        }
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _semaphore.Dispose();
        }
        return ValueTask.CompletedTask;
    }

    private sealed class PermitReleaser : IAsyncDisposable
    {
        private readonly SemaphoreSlimEmbeddingThrottler _owner;
        private bool _released;

        public PermitReleaser(SemaphoreSlimEmbeddingThrottler owner) => _owner = owner;

        public ValueTask DisposeAsync()
        {
            if (!_released)
            {
                _released = true;
                _owner.Release();
            }
            return ValueTask.CompletedTask;
        }
    }
}
```

### 4. Rate Limit Options

Create `Options/RateLimitOptions.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace CompoundDocs.McpServer.Options;

/// <summary>
/// Configuration options for Ollama request rate limiting.
/// </summary>
public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    /// <summary>
    /// Maximum concurrent requests to Ollama.
    /// Should match OLLAMA_NUM_PARALLEL environment variable.
    /// </summary>
    [Range(1, 10)]
    public int ConcurrencyLimit { get; set; } = 2;

    /// <summary>
    /// Maximum requests waiting in queue when at capacity.
    /// </summary>
    [Range(1, 100)]
    public int QueueLimit { get; set; } = 10;

    /// <summary>
    /// Timeout in seconds for acquiring a permit.
    /// </summary>
    [Range(5, 300)]
    public int AcquireTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Hardware profile for automatic configuration.
    /// Overrides ConcurrencyLimit if set.
    /// </summary>
    public HardwareProfile? HardwareProfile { get; set; }
}

/// <summary>
/// Hardware profiles with recommended rate limit settings.
/// </summary>
public enum HardwareProfile
{
    /// <summary>Apple Silicon with 8GB RAM - conservative limits.</summary>
    AppleSilicon8GB,

    /// <summary>Apple Silicon with 16GB+ RAM - moderate limits.</summary>
    AppleSilicon16GB,

    /// <summary>NVIDIA GPU with 8GB VRAM - moderate limits.</summary>
    NvidiaGpu8GB,

    /// <summary>NVIDIA GPU with 16GB+ VRAM - higher limits.</summary>
    NvidiaGpu16GB,

    /// <summary>CPU-only inference - conservative limits.</summary>
    CpuOnly
}

/// <summary>
/// Extension methods for hardware profile configuration.
/// </summary>
public static class HardwareProfileExtensions
{
    /// <summary>
    /// Gets the recommended concurrency limit for a hardware profile.
    /// </summary>
    public static int GetRecommendedConcurrency(this HardwareProfile profile) => profile switch
    {
        HardwareProfile.AppleSilicon8GB => 1,
        HardwareProfile.AppleSilicon16GB => 2,
        HardwareProfile.NvidiaGpu8GB => 2,
        HardwareProfile.NvidiaGpu16GB => 4,
        HardwareProfile.CpuOnly => 1,
        _ => 2
    };

    /// <summary>
    /// Gets the recommended queue limit for a hardware profile.
    /// </summary>
    public static int GetRecommendedQueueLimit(this HardwareProfile profile) => profile switch
    {
        HardwareProfile.AppleSilicon8GB => 5,
        HardwareProfile.AppleSilicon16GB => 10,
        HardwareProfile.NvidiaGpu8GB => 10,
        HardwareProfile.NvidiaGpu16GB => 20,
        HardwareProfile.CpuOnly => 5,
        _ => 10
    };
}
```

### 5. Embedding Queue Interface

Create `Services/IEmbeddingQueue.cs`:

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Manages queued embedding requests for batch processing.
/// </summary>
public interface IEmbeddingQueue
{
    /// <summary>
    /// Enqueues a document for embedding generation.
    /// </summary>
    /// <param name="request">The embedding request.</param>
    /// <param name="priority">Priority level (lower = higher priority).</param>
    /// <returns>True if enqueued, false if queue is full.</returns>
    bool TryEnqueue(EmbeddingRequest request, int priority = 10);

    /// <summary>
    /// Attempts to dequeue the next request.
    /// </summary>
    /// <param name="request">The dequeued request, if available.</param>
    /// <returns>True if a request was dequeued.</returns>
    bool TryDequeue(out EmbeddingRequest? request);

    /// <summary>
    /// Gets the current queue depth.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets whether the queue is at capacity.
    /// </summary>
    bool IsFull { get; }
}

/// <summary>
/// Request for embedding generation.
/// </summary>
public record EmbeddingRequest
{
    /// <summary>Unique identifier for the request.</summary>
    public required Guid Id { get; init; }

    /// <summary>Tenant ID for isolation.</summary>
    public required string TenantId { get; init; }

    /// <summary>Relative file path of the document.</summary>
    public required string FilePath { get; init; }

    /// <summary>Content to embed.</summary>
    public required string Content { get; init; }

    /// <summary>Chunk index if this is a chunk.</summary>
    public int? ChunkIndex { get; init; }

    /// <summary>Timestamp when request was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Priority level (lower = higher priority).</summary>
    public int Priority { get; init; } = 10;
}
```

### 6. Bounded Priority Queue Implementation

Create `Services/BoundedEmbeddingQueue.cs`:

```csharp
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Bounded in-memory queue for embedding requests with priority support.
/// </summary>
public sealed class BoundedEmbeddingQueue : IEmbeddingQueue
{
    private readonly PriorityQueue<EmbeddingRequest, int> _queue = new();
    private readonly object _lock = new();
    private readonly int _maxCapacity;
    private readonly ILogger<BoundedEmbeddingQueue> _logger;

    public BoundedEmbeddingQueue(
        IOptions<RateLimitOptions> options,
        ILogger<BoundedEmbeddingQueue> logger)
    {
        _maxCapacity = options.Value.QueueLimit * 10; // Allow 10x queue depth for batch operations
        _logger = logger;
    }

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count;
            }
        }
    }

    public bool IsFull => Count >= _maxCapacity;

    public bool TryEnqueue(EmbeddingRequest request, int priority = 10)
    {
        lock (_lock)
        {
            if (_queue.Count >= _maxCapacity)
            {
                _logger.LogWarning(
                    "Embedding queue full. Rejecting request for {FilePath}",
                    request.FilePath);
                return false;
            }

            _queue.Enqueue(request with { Priority = priority }, priority);

            _logger.LogDebug(
                "Enqueued embedding request for {FilePath}. Queue depth: {Depth}",
                request.FilePath, _queue.Count);

            return true;
        }
    }

    public bool TryDequeue(out EmbeddingRequest? request)
    {
        lock (_lock)
        {
            if (_queue.TryDequeue(out request, out _))
            {
                _logger.LogDebug(
                    "Dequeued embedding request for {FilePath}. Queue depth: {Depth}",
                    request.FilePath, _queue.Count);
                return true;
            }

            request = null;
            return false;
        }
    }
}
```

### 7. Service Registration

Add to `ServiceCollectionExtensions.cs`:

```csharp
/// <summary>
/// Registers rate limiting services for Ollama.
/// </summary>
public static IServiceCollection AddRateLimiting(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Options with validation
    services.AddOptionsWithValidateOnStart<RateLimitOptions>()
        .Bind(configuration.GetSection(RateLimitOptions.SectionName))
        .ValidateDataAnnotations()
        .Configure(options =>
        {
            // Apply hardware profile if specified
            if (options.HardwareProfile.HasValue)
            {
                options.ConcurrencyLimit = options.HardwareProfile.Value.GetRecommendedConcurrency();
                options.QueueLimit = options.HardwareProfile.Value.GetRecommendedQueueLimit();
            }
        });

    // Throttler - singleton (manages shared semaphore)
    services.AddSingleton<IEmbeddingThrottler, SemaphoreSlimEmbeddingThrottler>();

    // Queue - singleton (shared state)
    services.AddSingleton<IEmbeddingQueue, BoundedEmbeddingQueue>();

    return services;
}
```

### 8. Configuration Section

Add to appsettings.json:

```json
{
  "RateLimit": {
    "ConcurrencyLimit": 2,
    "QueueLimit": 10,
    "AcquireTimeoutSeconds": 60,
    "HardwareProfile": null
  }
}
```

### Hardware Profile Selection Guide

| Hardware | Profile | OLLAMA_NUM_PARALLEL | ConcurrencyLimit | Notes |
|----------|---------|---------------------|------------------|-------|
| Apple M1/M2 (8GB) | `AppleSilicon8GB` | 1 | 1 | Memory constrained |
| Apple M1/M2 (16GB+) | `AppleSilicon16GB` | 2 | 2 | Balanced performance |
| NVIDIA GPU (8GB) | `NvidiaGpu8GB` | 2 | 2 | VRAM constrained |
| NVIDIA GPU (16GB+) | `NvidiaGpu16GB` | 4 | 4 | High throughput |
| CPU only | `CpuOnly` | 1 | 1 | Slowest, most conservative |

### Usage in Embedding Service

```csharp
public class SemanticKernelEmbeddingService : IEmbeddingService
{
    private readonly ITextEmbeddingGenerationService _skService;
    private readonly IEmbeddingThrottler _throttler;
    private readonly ILogger<SemanticKernelEmbeddingService> _logger;

    public SemanticKernelEmbeddingService(
        ITextEmbeddingGenerationService skService,
        IEmbeddingThrottler throttler,
        ILogger<SemanticKernelEmbeddingService> logger)
    {
        _skService = skService;
        _throttler = throttler;
        _logger = logger;
    }

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        // Acquire throttling permit
        await using var permit = await _throttler.AcquireAsync(cancellationToken);

        _logger.LogDebug(
            "Generating embedding. Permits available: {Available}, Queue: {Queue}",
            _throttler.AvailablePermits,
            _throttler.QueueDepth);

        return await _skService.GenerateEmbeddingAsync(content, kernel: null, cancellationToken);
    }
}
```

---

## Dependencies

### Depends On

- **Phase 029**: HttpClient Resilience - Base resilience pipeline must exist

### Blocks

- **Phase 034+**: Embedding generation phases requiring rate limiting
- **Phase 050+**: File watcher embedding integration

---

## Verification Steps

After completing this phase, verify:

1. **Rate limiter integration**: HttpClient respects concurrency limits
2. **Throttler behavior**: Concurrent requests are properly queued
3. **Hardware profiles**: Configuration applies correct limits
4. **Queue management**: Requests are processed FIFO with priority
5. **Timeout handling**: Acquisitions timeout gracefully

### Manual Verification

```bash
# Build the project
dotnet build src/CompoundDocs.McpServer/

# Run with different hardware profiles
export CompoundDocs__RateLimit__HardwareProfile=AppleSilicon8GB
dotnet run --project src/CompoundDocs.McpServer/
# Verify logs show ConcurrencyLimit=1

export CompoundDocs__RateLimit__HardwareProfile=NvidiaGpu16GB
dotnet run --project src/CompoundDocs.McpServer/
# Verify logs show ConcurrencyLimit=4
```

### Unit Test Verification

```csharp
[Fact]
public async Task EmbeddingThrottler_LimitsConcurrentRequests()
{
    // Arrange
    var options = Options.Create(new RateLimitOptions { ConcurrencyLimit = 2 });
    var logger = NullLogger<SemaphoreSlimEmbeddingThrottler>.Instance;
    await using var throttler = new SemaphoreSlimEmbeddingThrottler(options, logger);

    // Act - Acquire all permits
    await using var permit1 = await throttler.AcquireAsync();
    await using var permit2 = await throttler.AcquireAsync();

    // Assert - No permits available
    Assert.Equal(0, throttler.AvailablePermits);
}

[Fact]
public async Task EmbeddingThrottler_TimesOutWhenNoPermitsAvailable()
{
    // Arrange
    var options = Options.Create(new RateLimitOptions
    {
        ConcurrencyLimit = 1,
        AcquireTimeoutSeconds = 1
    });
    var logger = NullLogger<SemaphoreSlimEmbeddingThrottler>.Instance;
    await using var throttler = new SemaphoreSlimEmbeddingThrottler(options, logger);

    // Act - Acquire the only permit
    await using var permit = await throttler.AcquireAsync();

    // Assert - Second acquire times out
    await Assert.ThrowsAsync<OperationCanceledException>(
        () => throttler.AcquireAsync());
}

[Fact]
public void BoundedQueue_RejectsWhenFull()
{
    // Arrange
    var options = Options.Create(new RateLimitOptions { QueueLimit = 1 });
    var logger = NullLogger<BoundedEmbeddingQueue>.Instance;
    var queue = new BoundedEmbeddingQueue(options, logger);

    // Fill to capacity (10x queue limit = 10)
    for (int i = 0; i < 10; i++)
    {
        Assert.True(queue.TryEnqueue(new EmbeddingRequest
        {
            Id = Guid.NewGuid(),
            TenantId = "test",
            FilePath = $"doc{i}.md",
            Content = "content"
        }));
    }

    // Assert - Additional enqueue rejected
    Assert.True(queue.IsFull);
    Assert.False(queue.TryEnqueue(new EmbeddingRequest
    {
        Id = Guid.NewGuid(),
        TenantId = "test",
        FilePath = "overflow.md",
        Content = "content"
    }));
}

[Theory]
[InlineData(HardwareProfile.AppleSilicon8GB, 1)]
[InlineData(HardwareProfile.AppleSilicon16GB, 2)]
[InlineData(HardwareProfile.NvidiaGpu8GB, 2)]
[InlineData(HardwareProfile.NvidiaGpu16GB, 4)]
[InlineData(HardwareProfile.CpuOnly, 1)]
public void HardwareProfile_ReturnsCorrectConcurrency(HardwareProfile profile, int expected)
{
    Assert.Equal(expected, profile.GetRecommendedConcurrency());
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Options/RateLimitOptions.cs` | Create | Rate limiting configuration options |
| `src/CompoundDocs.McpServer/Services/IEmbeddingThrottler.cs` | Create | Throttler interface |
| `src/CompoundDocs.McpServer/Services/SemaphoreSlimEmbeddingThrottler.cs` | Create | SemaphoreSlim implementation |
| `src/CompoundDocs.McpServer/Services/IEmbeddingQueue.cs` | Create | Queue interface and request model |
| `src/CompoundDocs.McpServer/Services/BoundedEmbeddingQueue.cs` | Create | Priority queue implementation |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Add rate limiting registration |
| `src/CompoundDocs.McpServer/appsettings.json` | Modify | Add RateLimit configuration section |
| `tests/CompoundDocs.McpServer.Tests/Services/EmbeddingThrottlerTests.cs` | Create | Throttler unit tests |
| `tests/CompoundDocs.McpServer.Tests/Services/BoundedEmbeddingQueueTests.cs` | Create | Queue unit tests |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Deadlock from semaphore | Use async-only patterns, always dispose permits |
| Memory pressure from queue | Bounded queue with configurable capacity |
| Misconfigured hardware profile | Validation on startup, sane defaults |
| Timeout too aggressive | Generous default (60s), configurable |
| Lost requests on shutdown | Graceful shutdown drains queue (future phase) |

---

## Notes

- The `ConcurrencyLimiter` in the HTTP resilience pipeline and `IEmbeddingThrottler` serve different purposes:
  - HTTP rate limiter: Limits outbound HTTP connections to Ollama
  - Embedding throttler: Provides application-level control for embedding service consumers
- Hardware profiles are suggestions; users can override with explicit `ConcurrencyLimit`
- The queue is in-memory and not persisted; crashed requests are recovered via reconciliation (separate phase)
- Priority values follow convention: lower = higher priority (1-10 for interactive, 50+ for batch)
