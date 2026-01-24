# Semantic Kernel Request Queuing and Concurrency Management Research

**Research Date:** January 22, 2026
**Context:** Building an MCP server for RAG using Semantic Kernel with Ollama
**Problem:** Managing request flow to avoid overwhelming Ollama when `OLLAMA_NUM_PARALLEL` is limited

---

## Executive Summary

Microsoft Semantic Kernel does **not** include built-in request queuing or rate limiting mechanisms. Concurrency management must be implemented at the application level using .NET primitives, the `System.Threading.RateLimiting` namespace, or resilience libraries like Polly. This research provides comprehensive patterns and code examples for implementing effective request throttling for Semantic Kernel services backed by Ollama.

---

## Table of Contents

1. [Semantic Kernel Built-in Concurrency Support](#1-semantic-kernel-built-in-concurrency-support)
2. [SK Service Configuration Options](#2-sk-service-configuration-options)
3. [Ollama Behavior Under Load](#3-ollama-behavior-under-load)
4. [.NET Concurrency Primitives](#4-net-concurrency-primitives)
5. [Rate Limiting in .NET](#5-rate-limiting-in-net)
6. [Polly Resilience Library](#6-polly-resilience-library)
7. [Custom Queue Implementation](#7-custom-queue-implementation)
8. [Wrapper Service Pattern](#8-wrapper-service-pattern)
9. [Embedding-Specific Batching](#9-embedding-specific-batching)
10. [Best Practices and Recommendations](#10-best-practices-and-recommendations)
11. [Complete Code Examples](#11-complete-code-examples)
12. [Monitoring and Observability](#12-monitoring-and-observability)

---

## 1. Semantic Kernel Built-in Concurrency Support

### Key Finding: No Built-in Rate Limiting

Semantic Kernel does **not** have built-in request queuing or rate limiting capabilities. The framework is designed as a lightweight orchestration layer that delegates to underlying AI services.

### What SK Provides

- **HttpClient Configuration**: SK services accept custom `HttpClient` instances
- **Timeout Configuration**: Can be set via HttpClient
- **Service Abstraction**: `IChatCompletionService` and `ITextEmbeddingGenerationService` interfaces

### What SK Does NOT Provide

- Request queuing
- Rate limiting
- Automatic throttling
- Connection pooling management
- Built-in retry with backoff (relies on Polly or similar)

### Ollama Connector Status

The Ollama connector is currently **experimental** and requires:
```csharp
#pragma warning disable SKEXP0070
```

**Sources:**
- [Semantic Kernel Overview - Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/overview/)
- [Introducing Ollama Connector - Semantic Kernel Blog](https://devblogs.microsoft.com/semantic-kernel/introducing-new-ollama-connector-for-local-models/)

---

## 2. SK Service Configuration Options

### OllamaSharp Client Configuration

The Semantic Kernel Ollama connector uses the [OllamaSharp library](https://github.com/awaescher/OllamaSharp) internally.

#### Method 1: HttpClient with Custom Timeout

```csharp
var httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:11434"),
    Timeout = TimeSpan.FromMinutes(5) // Important for slow CPU inference
};

var builder = Kernel.CreateBuilder();
builder.AddOllamaChatCompletion("llama3.2", httpClient);
```

#### Method 2: Using OllamaApiClient Directly

```csharp
using OllamaSharp;

var httpClient = new HttpClient
{
    BaseAddress = new Uri("http://localhost:11434"),
    Timeout = TimeSpan.FromMinutes(5)
};

var ollamaClient = new OllamaApiClient(httpClient);
var chatService = ollamaClient.AsChatCompletionService();
```

#### Method 3: With Dependency Injection

```csharp
builder.Services.AddHttpClient("ollama", client =>
{
    client.BaseAddress = new Uri("http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddSingleton<IChatCompletionService>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("ollama");
    return new OllamaApiClient(httpClient).AsChatCompletionService();
});
```

### Known Issue: Polly Timeout Conflict

When using ASP.NET with default resilience handlers, Polly's 30-second timeout may override custom HttpClient timeouts, causing `Polly.Timeout.TimeoutRejectedException`.

**Solution:** Configure Polly's timeout separately or remove default handlers:
```csharp
services.AddHttpClient("ollama")
    .RemoveAllResilienceHandlers()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromMinutes(5);
    });
```

**Sources:**
- [Add Chat Completion Services - Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/concepts/ai-services/chat-completion/)
- [GitHub Issue #5423 - HttpClient Configuration Fails](https://github.com/microsoft/semantic-kernel/issues/5423)

---

## 3. Ollama Behavior Under Load

### OLLAMA_NUM_PARALLEL Configuration

| Environment Variable | Description | Default |
|---------------------|-------------|---------|
| `OLLAMA_NUM_PARALLEL` | Max parallel requests per model | Auto (4 or 1 based on memory) |
| `OLLAMA_MAX_QUEUE` | Max queued requests before rejection | 512 |

### Behavior When Capacity Exceeded

1. **Requests are QUEUED** (not immediately rejected)
2. Queue operates as FIFO (First In, First Out)
3. Queue holds up to 512 requests by default
4. **Only when queue is full**: New requests receive **503 Server Overloaded**

### Memory Considerations

- Parallel requests multiply context memory usage
- Example: 2K context with 4 parallel = 8K effective context
- More memory is allocated when `OLLAMA_NUM_PARALLEL > 1`

### Error Response Format

When queue is full:
```
HTTP 503 Service Unavailable
{"error": "server busy"}
```

### Recommendation

Since Ollama has built-in queuing, you may not need application-level queuing for simple use cases. However, for production MCP servers:
- Implement client-side throttling to prevent queue buildup
- Monitor queue depth via Ollama metrics
- Set appropriate `OLLAMA_MAX_QUEUE` for your use case

**Sources:**
- [Ollama FAQ](https://docs.ollama.com/faq)
- [How Ollama Handles Parallel Requests - Glukhov Blog](https://www.glukhov.org/post/2025/05/how-ollama-handles-parallel-requests/)

---

## 4. .NET Concurrency Primitives

### SemaphoreSlim for Limiting Concurrent Operations

`SemaphoreSlim` is the recommended primitive for limiting concurrent async operations.

#### Basic Pattern

```csharp
public class ThrottledService
{
    private readonly SemaphoreSlim _semaphore;
    private readonly IChatCompletionService _chatService;

    public ThrottledService(IChatCompletionService chatService, int maxConcurrency = 2)
    {
        _chatService = chatService;
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public async Task<ChatMessageContent> GetChatCompletionAsync(
        ChatHistory chatHistory,
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            return await _chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

#### With Timeout

```csharp
public async Task<ChatMessageContent?> GetChatCompletionAsync(
    ChatHistory chatHistory,
    TimeSpan timeout,
    CancellationToken cancellationToken = default)
{
    if (!await _semaphore.WaitAsync(timeout, cancellationToken))
    {
        // Could not acquire semaphore within timeout
        return null; // Or throw exception
    }

    try
    {
        return await _chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);
    }
    finally
    {
        _semaphore.Release();
    }
}
```

### Best Practices for SemaphoreSlim

1. Always use `try/finally` to ensure `Release()` is called
2. Use `WaitAsync()` instead of `Wait()` in async code
3. Dispose the semaphore when the service is disposed
4. Set `maxConcurrency` based on `OLLAMA_NUM_PARALLEL`

**Sources:**
- [SemaphoreSlim.WaitAsync - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim.waitasync)
- [Limiting Concurrent Operations - Ken Dale](https://kendaleiv.com/limiting-concurrent-operations-with-semaphoreslim-using-csharp/)

---

## 5. Rate Limiting in .NET

### System.Threading.RateLimiting (Available .NET 7+)

The `System.Threading.RateLimiting` namespace provides four built-in rate limiters:

| Limiter | Description | Best For |
|---------|-------------|----------|
| `ConcurrencyLimiter` | Limits concurrent operations | Matching `OLLAMA_NUM_PARALLEL` |
| `TokenBucketRateLimiter` | Token bucket algorithm | API rate limits (requests/minute) |
| `FixedWindowRateLimiter` | Fixed time windows | Simple rate limiting |
| `SlidingWindowRateLimiter` | Sliding time windows | Smooth rate limiting |

### ConcurrencyLimiter (Recommended for Ollama)

```csharp
using System.Threading.RateLimiting;

var limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
{
    PermitLimit = 2,              // Match OLLAMA_NUM_PARALLEL
    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    QueueLimit = 10               // Requests to queue before rejecting
});

// Usage
using RateLimitLease lease = await limiter.AcquireAsync(permitCount: 1);
if (lease.IsAcquired)
{
    try
    {
        // Make request to Ollama
        var result = await chatService.GetChatMessageContentAsync(history);
    }
    finally
    {
        // Lease is released when disposed
    }
}
else
{
    // Rate limit exceeded, queue was full
    throw new RateLimitExceededException("Service is busy");
}
```

### TokenBucketRateLimiter (For API Rate Limits)

```csharp
var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
{
    TokenLimit = 10,                           // Max tokens in bucket
    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
    QueueLimit = 5,
    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
    TokensPerPeriod = 2,                       // Tokens added per period
    AutoReplenishment = true
});
```

### Key Differences: ConcurrencyLimiter vs SemaphoreSlim

| Feature | ConcurrencyLimiter | SemaphoreSlim |
|---------|-------------------|---------------|
| Statistics | `GetStatistics()` available | None |
| Idle Duration | Tracks `IdleDuration` | None |
| Queue Limit | Configurable `QueueLimit` | None (waits indefinitely) |
| Lease Pattern | Returns `RateLimitLease` | Manual release |
| Part of | `System.Threading.RateLimiting` | `System.Threading` |

**Sources:**
- [Announcing Rate Limiting for .NET](https://devblogs.microsoft.com/dotnet/announcing-rate-limiting-for-dotnet/)
- [ConcurrencyLimiter Class - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.threading.ratelimiting.concurrencylimiter)

---

## 6. Polly Resilience Library

### Polly v8 Overview

Polly v8 (Polly.Core) introduces **Resilience Pipelines** that combine multiple strategies:

| Strategy Order | Strategy | Purpose |
|----------------|----------|---------|
| 1 | Rate Limiter | Limit concurrent requests |
| 2 | Timeout | Overall request timeout |
| 3 | Retry | Retry transient failures |
| 4 | Circuit Breaker | Block on repeated failures |
| 5 | Fallback | Provide fallback response |

### Important Change in v8

The old "Bulkhead" isolation is now the **Rate Limiter** strategy, which uses `System.Threading.RateLimiting` internally.

### Required Packages

```xml
<PackageReference Include="Polly.Core" Version="8.*" />
<PackageReference Include="Polly.RateLimiting" Version="8.*" />
<PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="9.*" />
```

### Using with HttpClientFactory

```csharp
builder.Services.AddHttpClient("ollama", client =>
{
    client.BaseAddress = new Uri("http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(5);
})
.AddResilienceHandler("ollama-resilience", builder =>
{
    // Rate limiter (matches OLLAMA_NUM_PARALLEL)
    builder.AddRateLimiter(new ConcurrencyLimiter(
        new ConcurrencyLimiterOptions
        {
            PermitLimit = 2,
            QueueLimit = 10,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        }));

    // Retry on transient errors
    builder.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromSeconds(2)
    });

    // Circuit breaker
    builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,
        MinimumThroughput = 5,
        SamplingDuration = TimeSpan.FromSeconds(30),
        BreakDuration = TimeSpan.FromSeconds(30)
    });
});
```

### Standard Resilience Handler

Microsoft provides a pre-configured resilience pipeline:

```csharp
builder.Services.AddHttpClient("ollama")
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
        options.Retry.MaxRetryAttempts = 2;
        options.Retry.Delay = TimeSpan.FromSeconds(5);
        options.CircuitBreaker.FailureRatio = 0.3;
    });
```

**Default Standard Resilience Handler Settings:**

| Strategy | Default Value |
|----------|---------------|
| Rate Limiter | Queue: 0, Permit: 1000 |
| Total Timeout | 30 seconds |
| Retry | 3 attempts, exponential backoff, 2s delay |
| Circuit Breaker | 10% failure ratio, 30s sampling, 5s break |
| Attempt Timeout | 10 seconds |

**Sources:**
- [Polly Documentation](https://www.pollydocs.org/)
- [Rate Limiter Strategy - Polly Docs](https://www.pollydocs.org/strategies/rate-limiter.html)
- [Building Resilient Cloud Services with .NET 8](https://devblogs.microsoft.com/dotnet/building-resilient-cloud-services-with-dotnet-8/)

---

## 7. Custom Queue Implementation

### Channel<T> for Request Queuing

`System.Threading.Channels` provides high-performance producer/consumer queues ideal for request queuing.

### Bounded Channel (Recommended)

```csharp
using System.Threading.Channels;

public class OllamaRequestQueue
{
    private readonly Channel<OllamaRequest> _channel;
    private readonly Task _processingTask;
    private readonly IChatCompletionService _chatService;
    private readonly int _maxConcurrency;

    public OllamaRequestQueue(
        IChatCompletionService chatService,
        int maxConcurrency = 2,
        int queueCapacity = 100)
    {
        _chatService = chatService;
        _maxConcurrency = maxConcurrency;

        _channel = Channel.CreateBounded<OllamaRequest>(
            new BoundedChannelOptions(queueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });

        _processingTask = StartProcessingAsync();
    }

    public async Task<ChatMessageContent> EnqueueAsync(
        ChatHistory chatHistory,
        CancellationToken cancellationToken = default)
    {
        var request = new OllamaRequest(chatHistory);

        await _channel.Writer.WriteAsync(request, cancellationToken);

        return await request.CompletionSource.Task;
    }

    private async Task StartProcessingAsync()
    {
        var semaphore = new SemaphoreSlim(_maxConcurrency);

        await foreach (var request in _channel.Reader.ReadAllAsync())
        {
            await semaphore.WaitAsync();

            _ = ProcessRequestAsync(request, semaphore);
        }
    }

    private async Task ProcessRequestAsync(OllamaRequest request, SemaphoreSlim semaphore)
    {
        try
        {
            var result = await _chatService.GetChatMessageContentAsync(
                request.ChatHistory);
            request.CompletionSource.SetResult(result);
        }
        catch (Exception ex)
        {
            request.CompletionSource.SetException(ex);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private record OllamaRequest(ChatHistory ChatHistory)
    {
        public TaskCompletionSource<ChatMessageContent> CompletionSource { get; } = new();
    }
}
```

### Priority Queue with Multiple Channels

For separate priority handling (e.g., embeddings vs chat):

```csharp
public class PriorityRequestQueue
{
    private readonly Channel<Request> _highPriorityChannel;
    private readonly Channel<Request> _lowPriorityChannel;

    public PriorityRequestQueue(int capacity = 100)
    {
        _highPriorityChannel = Channel.CreateBounded<Request>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            });

        _lowPriorityChannel = Channel.CreateBounded<Request>(
            new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest // Drop old embeddings if overwhelmed
            });
    }

    public async Task EnqueueChatAsync(ChatRequest request, CancellationToken ct = default)
    {
        await _highPriorityChannel.Writer.WriteAsync(request, ct);
    }

    public async Task EnqueueEmbeddingAsync(EmbeddingRequest request, CancellationToken ct = default)
    {
        await _lowPriorityChannel.Writer.WriteAsync(request, ct);
    }

    private async Task ProcessAsync()
    {
        while (true)
        {
            // Prioritize chat requests
            if (_highPriorityChannel.Reader.TryRead(out var highPriority))
            {
                await ProcessRequest(highPriority);
            }
            else if (_lowPriorityChannel.Reader.TryRead(out var lowPriority))
            {
                await ProcessRequest(lowPriority);
            }
            else
            {
                await Task.WhenAny(
                    _highPriorityChannel.Reader.WaitToReadAsync().AsTask(),
                    _lowPriorityChannel.Reader.WaitToReadAsync().AsTask());
            }
        }
    }
}
```

### BoundedChannelFullMode Options

| Mode | Behavior |
|------|----------|
| `Wait` | Block producer until space available (default) |
| `DropNewest` | Drop the newest item to make room |
| `DropOldest` | Drop the oldest item to make room |
| `DropWrite` | Drop the item being written |

**Sources:**
- [Channels - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)
- [Using Channels for Async Queuing](https://makolyte.com/event-driven-dotnet-concurrent-producer-consumer-using-a-channel-as-a-non-blocking-async-queue/)

---

## 8. Wrapper Service Pattern

### Throttled IChatCompletionService Wrapper

```csharp
public class ThrottledChatCompletionService : IChatCompletionService
{
    private readonly IChatCompletionService _innerService;
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger<ThrottledChatCompletionService> _logger;

    public ThrottledChatCompletionService(
        IChatCompletionService innerService,
        int maxConcurrency,
        ILogger<ThrottledChatCompletionService> logger)
    {
        _innerService = innerService;
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _logger = logger;
    }

    public IReadOnlyDictionary<string, object?> Attributes => _innerService.Attributes;

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Waiting for semaphore. Current count: {Count}", _semaphore.CurrentCount);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            _logger.LogDebug("Acquired semaphore. Making request...");
            return await _innerService.GetChatMessageContentsAsync(
                chatHistory, executionSettings, kernel, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
            _logger.LogDebug("Released semaphore. Current count: {Count}", _semaphore.CurrentCount);
        }
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await foreach (var content in _innerService.GetStreamingChatMessageContentsAsync(
                chatHistory, executionSettings, kernel, cancellationToken))
            {
                yield return content;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

### Throttled ITextEmbeddingGenerationService Wrapper

```csharp
public class ThrottledEmbeddingService : ITextEmbeddingGenerationService
{
    private readonly ITextEmbeddingGenerationService _innerService;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _batchSize;

    public ThrottledEmbeddingService(
        ITextEmbeddingGenerationService innerService,
        int maxConcurrency,
        int batchSize = 10)
    {
        _innerService = innerService;
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _batchSize = batchSize;
    }

    public IReadOnlyDictionary<string, object?> Attributes => _innerService.Attributes;

    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> data,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ReadOnlyMemory<float>>();

        // Process in batches
        foreach (var batch in data.Chunk(_batchSize))
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                var batchResults = await _innerService.GenerateEmbeddingsAsync(
                    batch.ToList(), kernel, cancellationToken);
                results.AddRange(batchResults);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        return results;
    }
}
```

### DI Registration Patterns

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddThrottledSemanticKernel(
        this IServiceCollection services,
        string ollamaEndpoint,
        string modelId,
        int maxChatConcurrency = 2,
        int maxEmbeddingConcurrency = 4)
    {
        // Register the raw services
        services.AddSingleton(sp =>
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(ollamaEndpoint),
                Timeout = TimeSpan.FromMinutes(5)
            };
            return new OllamaApiClient(httpClient, modelId);
        });

        // Register throttled chat service
        services.AddSingleton<IChatCompletionService>(sp =>
        {
            var ollamaClient = sp.GetRequiredService<OllamaApiClient>();
            var logger = sp.GetRequiredService<ILogger<ThrottledChatCompletionService>>();
            var innerService = ollamaClient.AsChatCompletionService();
            return new ThrottledChatCompletionService(innerService, maxChatConcurrency, logger);
        });

        // Register throttled embedding service
        services.AddSingleton<ITextEmbeddingGenerationService>(sp =>
        {
            var ollamaClient = sp.GetRequiredService<OllamaApiClient>();
            var innerService = ollamaClient.AsTextEmbeddingGenerationService();
            return new ThrottledEmbeddingService(innerService, maxEmbeddingConcurrency);
        });

        // Register Kernel
        services.AddTransient(sp =>
        {
            var chatService = sp.GetRequiredService<IChatCompletionService>();
            var embeddingService = sp.GetRequiredService<ITextEmbeddingGenerationService>();

            return Kernel.CreateBuilder()
                .AddService(chatService)
                .AddService(embeddingService)
                .Build();
        });

        return services;
    }
}
```

**Sources:**
- [Using Semantic Kernel with Dependency Injection](https://devblogs.microsoft.com/semantic-kernel/using-semantic-kernel-with-dependency-injection/)
- [Understanding the Kernel - Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/concepts/kernel)

---

## 9. Embedding-Specific Batching

### SK Batch Embedding Support

The `ITextEmbeddingGenerationService` interface natively supports batch operations:

```csharp
IList<ReadOnlyMemory<float>> embeddings = await textEmbeddingService.GenerateEmbeddingsAsync(
    ["text 1", "text 2", "text 3", "text 4"]);
```

### Optimal Batch Sizes for Ollama

| Scenario | Recommended Batch Size |
|----------|----------------------|
| Development | 2-5 |
| Production (small GPU) | 5-10 |
| Production (large GPU) | 10-32 |

**Important Warnings:**
- Batch sizes > 16 may degrade embedding quality (known Ollama issue)
- Larger batches require proportionally more memory
- Monitor embedding quality when adjusting batch size

### Batched Embedding Queue

```csharp
public class BatchedEmbeddingQueue
{
    private readonly Channel<EmbeddingRequest> _channel;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly int _batchSize;
    private readonly TimeSpan _batchTimeout;

    public BatchedEmbeddingQueue(
        ITextEmbeddingGenerationService embeddingService,
        int batchSize = 10,
        TimeSpan? batchTimeout = null)
    {
        _embeddingService = embeddingService;
        _batchSize = batchSize;
        _batchTimeout = batchTimeout ?? TimeSpan.FromMilliseconds(100);

        _channel = Channel.CreateUnbounded<EmbeddingRequest>();
        _ = ProcessBatchesAsync();
    }

    public Task<ReadOnlyMemory<float>> GetEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        var request = new EmbeddingRequest(text);
        _channel.Writer.TryWrite(request);
        return request.CompletionSource.Task;
    }

    private async Task ProcessBatchesAsync()
    {
        var batch = new List<EmbeddingRequest>();

        while (await _channel.Reader.WaitToReadAsync())
        {
            // Collect batch
            var deadline = DateTime.UtcNow + _batchTimeout;

            while (batch.Count < _batchSize && DateTime.UtcNow < deadline)
            {
                if (_channel.Reader.TryRead(out var request))
                {
                    batch.Add(request);
                }
                else
                {
                    await Task.Delay(10);
                }
            }

            if (batch.Count > 0)
            {
                await ProcessBatchAsync(batch);
                batch.Clear();
            }
        }
    }

    private async Task ProcessBatchAsync(List<EmbeddingRequest> batch)
    {
        try
        {
            var texts = batch.Select(r => r.Text).ToList();
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(texts);

            for (int i = 0; i < batch.Count; i++)
            {
                batch[i].CompletionSource.SetResult(embeddings[i]);
            }
        }
        catch (Exception ex)
        {
            foreach (var request in batch)
            {
                request.CompletionSource.SetException(ex);
            }
        }
    }

    private record EmbeddingRequest(string Text)
    {
        public TaskCompletionSource<ReadOnlyMemory<float>> CompletionSource { get; } = new();
    }
}
```

**Sources:**
- [Add Embedding Generation Services - Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/concepts/ai-services/embedding-generation/)
- [Ollama Embedding Batch Issues - GitHub #6262](https://github.com/ollama/ollama/issues/6262)

---

## 10. Best Practices and Recommendations

### Recommended Architecture for MCP Server

```
┌─────────────────────────────────────────────────────────────┐
│                       MCP Server                             │
├─────────────────────────────────────────────────────────────┤
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────────┐ │
│  │ MCP Handler │    │ MCP Handler │    │   MCP Handler   │ │
│  └──────┬──────┘    └──────┬──────┘    └────────┬────────┘ │
│         │                  │                     │          │
│         ▼                  ▼                     ▼          │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              Throttled Service Layer                  │  │
│  │  ┌────────────────────┐ ┌──────────────────────────┐ │  │
│  │  │ ThrottledChat (2)  │ │ ThrottledEmbedding (4)   │ │  │
│  │  │ SemaphoreSlim      │ │ SemaphoreSlim + Batching │ │  │
│  │  └─────────┬──────────┘ └───────────┬──────────────┘ │  │
│  └────────────┼────────────────────────┼────────────────┘  │
│               │                        │                    │
│               ▼                        ▼                    │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              Semantic Kernel Services                 │  │
│  │  IChatCompletionService    ITextEmbeddingGenService  │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
                    ┌───────────────────┐
                    │      Ollama       │
                    │ OLLAMA_NUM_PARALLEL│
                    │    (Queue: 512)   │
                    └───────────────────┘
```

### Separate Limits for Different Operations

| Operation | Recommended Concurrency | Reason |
|-----------|------------------------|--------|
| Chat Completion | 1-2 | Long-running, high memory |
| Text Embedding | 2-4 | Faster, can batch |
| Background Indexing | 1 | Lower priority |

### Configuration Recommendations

```csharp
public class OllamaThrottlingOptions
{
    public int MaxChatConcurrency { get; set; } = 2;
    public int MaxEmbeddingConcurrency { get; set; } = 4;
    public int EmbeddingBatchSize { get; set; } = 10;
    public int MaxQueueDepth { get; set; } = 100;
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan QueueTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

### Graceful Degradation Strategies

1. **Tiered Model Fallback**: If primary model is overloaded, fall back to smaller/faster model
2. **Cached Responses**: Return cached responses for common queries
3. **Queue Overflow Handling**: Return meaningful error when queue is full
4. **Circuit Breaker**: Temporarily stop accepting requests if Ollama is unhealthy

```csharp
public class ResilientChatService
{
    private readonly IChatCompletionService _primaryService;
    private readonly IChatCompletionService? _fallbackService;
    private readonly IMemoryCache _cache;

    public async Task<ChatMessageContent> GetResponseAsync(
        ChatHistory history,
        CancellationToken cancellationToken)
    {
        // Try cache first
        var cacheKey = ComputeHistoryHash(history);
        if (_cache.TryGetValue(cacheKey, out ChatMessageContent? cached))
        {
            return cached!;
        }

        try
        {
            var result = await _primaryService.GetChatMessageContentAsync(history, cancellationToken: cancellationToken);
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            return result;
        }
        catch (Exception) when (_fallbackService != null)
        {
            // Try fallback service
            return await _fallbackService.GetChatMessageContentAsync(history, cancellationToken: cancellationToken);
        }
    }
}
```

**Sources:**
- [MCP Best Practices](https://modelcontextprotocol.info/docs/best-practices/)
- [Graceful Degradation in Distributed Systems](https://www.geeksforgeeks.org/system-design/graceful-degradation-in-distributed-systems/)

---

## 11. Complete Code Examples

### Full Throttled Service Implementation

```csharp
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;

namespace YourMcpServer.Services;

/// <summary>
/// Options for configuring Ollama throttling behavior
/// </summary>
public class OllamaThrottlingOptions
{
    public int MaxChatConcurrency { get; set; } = 2;
    public int MaxEmbeddingConcurrency { get; set; } = 4;
    public int EmbeddingBatchSize { get; set; } = 10;
    public TimeSpan EmbeddingBatchTimeout { get; set; } = TimeSpan.FromMilliseconds(100);
    public int MaxQueueDepth { get; set; } = 100;
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Throttled chat completion service with semaphore-based concurrency control
/// </summary>
public sealed class ThrottledChatCompletionService : IChatCompletionService, IDisposable
{
    private readonly IChatCompletionService _innerService;
    private readonly SemaphoreSlim _semaphore;
    private readonly ILogger<ThrottledChatCompletionService> _logger;
    private readonly TimeSpan _timeout;
    private int _queuedRequests;
    private int _activeRequests;

    public ThrottledChatCompletionService(
        IChatCompletionService innerService,
        OllamaThrottlingOptions options,
        ILogger<ThrottledChatCompletionService> logger)
    {
        _innerService = innerService;
        _semaphore = new SemaphoreSlim(options.MaxChatConcurrency, options.MaxChatConcurrency);
        _logger = logger;
        _timeout = options.RequestTimeout;
    }

    public IReadOnlyDictionary<string, object?> Attributes => _innerService.Attributes;

    public int QueuedRequests => _queuedRequests;
    public int ActiveRequests => _activeRequests;

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _queuedRequests);
        _logger.LogDebug("Chat request queued. Queue depth: {QueueDepth}, Active: {Active}",
            _queuedRequests, _activeRequests);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);

            await _semaphore.WaitAsync(timeoutCts.Token);

            Interlocked.Decrement(ref _queuedRequests);
            Interlocked.Increment(ref _activeRequests);

            try
            {
                _logger.LogDebug("Processing chat request. Active: {Active}", _activeRequests);
                return await _innerService.GetChatMessageContentsAsync(
                    chatHistory, executionSettings, kernel, timeoutCts.Token);
            }
            finally
            {
                Interlocked.Decrement(ref _activeRequests);
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Interlocked.Decrement(ref _queuedRequests);
            _logger.LogWarning("Chat request timed out after {Timeout}", _timeout);
            throw new TimeoutException($"Chat request timed out after {_timeout}");
        }
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _queuedRequests);

        await _semaphore.WaitAsync(cancellationToken);

        Interlocked.Decrement(ref _queuedRequests);
        Interlocked.Increment(ref _activeRequests);

        try
        {
            await foreach (var content in _innerService.GetStreamingChatMessageContentsAsync(
                chatHistory, executionSettings, kernel, cancellationToken))
            {
                yield return content;
            }
        }
        finally
        {
            Interlocked.Decrement(ref _activeRequests);
            _semaphore.Release();
        }
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}

/// <summary>
/// Throttled embedding service with batching support
/// </summary>
public sealed class ThrottledEmbeddingService : ITextEmbeddingGenerationService, IDisposable
{
    private readonly ITextEmbeddingGenerationService _innerService;
    private readonly Channel<EmbeddingRequest> _channel;
    private readonly Task _processingTask;
    private readonly CancellationTokenSource _cts;
    private readonly int _batchSize;
    private readonly TimeSpan _batchTimeout;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly ILogger<ThrottledEmbeddingService> _logger;

    public ThrottledEmbeddingService(
        ITextEmbeddingGenerationService innerService,
        OllamaThrottlingOptions options,
        ILogger<ThrottledEmbeddingService> logger)
    {
        _innerService = innerService;
        _batchSize = options.EmbeddingBatchSize;
        _batchTimeout = options.EmbeddingBatchTimeout;
        _logger = logger;
        _cts = new CancellationTokenSource();
        _concurrencySemaphore = new SemaphoreSlim(options.MaxEmbeddingConcurrency);

        _channel = Channel.CreateBounded<EmbeddingRequest>(
            new BoundedChannelOptions(options.MaxQueueDepth)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

        _processingTask = ProcessBatchesAsync(_cts.Token);
    }

    public IReadOnlyDictionary<string, object?> Attributes => _innerService.Attributes;

    public async Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> data,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        if (data.Count == 0)
            return Array.Empty<ReadOnlyMemory<float>>();

        if (data.Count == 1)
        {
            // Single item - use the batching queue for efficiency
            var request = new EmbeddingRequest(data[0]);
            await _channel.Writer.WriteAsync(request, cancellationToken);
            var result = await request.CompletionSource.Task;
            return new[] { result };
        }

        // Multiple items - process directly with semaphore
        await _concurrencySemaphore.WaitAsync(cancellationToken);
        try
        {
            return await _innerService.GenerateEmbeddingsAsync(data, kernel, cancellationToken);
        }
        finally
        {
            _concurrencySemaphore.Release();
        }
    }

    private async Task ProcessBatchesAsync(CancellationToken cancellationToken)
    {
        var batch = new List<EmbeddingRequest>();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Wait for first item
                if (!await _channel.Reader.WaitToReadAsync(cancellationToken))
                    break;

                // Collect batch with timeout
                var deadline = DateTime.UtcNow + _batchTimeout;

                while (batch.Count < _batchSize && DateTime.UtcNow < deadline)
                {
                    if (_channel.Reader.TryRead(out var request))
                    {
                        batch.Add(request);
                    }
                    else if (batch.Count > 0)
                    {
                        // Have items, wait briefly for more
                        await Task.Delay(10, cancellationToken);
                    }
                    else
                    {
                        break;
                    }
                }

                if (batch.Count > 0)
                {
                    await _concurrencySemaphore.WaitAsync(cancellationToken);
                    try
                    {
                        await ProcessBatchAsync(batch);
                    }
                    finally
                    {
                        _concurrencySemaphore.Release();
                    }
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in embedding batch processor");
        }
    }

    private async Task ProcessBatchAsync(List<EmbeddingRequest> batch)
    {
        try
        {
            _logger.LogDebug("Processing embedding batch of {Count} items", batch.Count);

            var texts = batch.Select(r => r.Text).ToList();
            var embeddings = await _innerService.GenerateEmbeddingsAsync(texts);

            for (int i = 0; i < batch.Count; i++)
            {
                batch[i].CompletionSource.TrySetResult(embeddings[i]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing embedding batch");
            foreach (var request in batch)
            {
                request.CompletionSource.TrySetException(ex);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _channel.Writer.Complete();
        _processingTask.Wait(TimeSpan.FromSeconds(5));
        _cts.Dispose();
        _concurrencySemaphore.Dispose();
    }

    private record EmbeddingRequest(string Text)
    {
        public TaskCompletionSource<ReadOnlyMemory<float>> CompletionSource { get; } = new();
    }
}
```

### DI Registration

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using OllamaSharp;

namespace YourMcpServer.Extensions;

public static class SemanticKernelExtensions
{
    public static IServiceCollection AddThrottledOllamaServices(
        this IServiceCollection services,
        Action<OllamaThrottlingOptions>? configureOptions = null)
    {
        // Configure options
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.Configure<OllamaThrottlingOptions>(_ => { });
        }

        // Register OllamaApiClient
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<OllamaThrottlingOptions>>().Value;
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:11434"),
                Timeout = options.RequestTimeout
            };
            return new OllamaApiClient(httpClient);
        });

        // Register throttled chat service
        services.AddSingleton<IChatCompletionService>(sp =>
        {
            var ollamaClient = sp.GetRequiredService<OllamaApiClient>();
            var options = sp.GetRequiredService<IOptions<OllamaThrottlingOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<ThrottledChatCompletionService>>();

            #pragma warning disable SKEXP0070
            var innerService = ollamaClient.AsChatCompletionService();
            #pragma warning restore SKEXP0070

            return new ThrottledChatCompletionService(innerService, options, logger);
        });

        // Register throttled embedding service
        services.AddSingleton<ITextEmbeddingGenerationService>(sp =>
        {
            var ollamaClient = sp.GetRequiredService<OllamaApiClient>();
            var options = sp.GetRequiredService<IOptions<OllamaThrottlingOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<ThrottledEmbeddingService>>();

            #pragma warning disable SKEXP0070
            var innerService = ollamaClient.AsTextEmbeddingGenerationService();
            #pragma warning restore SKEXP0070

            return new ThrottledEmbeddingService(innerService, options, logger);
        });

        // Register Kernel as transient (recommended by Microsoft)
        services.AddTransient(sp =>
        {
            var chatService = sp.GetRequiredService<IChatCompletionService>();
            var embeddingService = sp.GetRequiredService<ITextEmbeddingGenerationService>();

            return Kernel.CreateBuilder()
                .Services
                .AddSingleton(chatService)
                .AddSingleton(embeddingService)
                .BuildServiceProvider()
                .GetRequiredService<Kernel>();
        });

        return services;
    }
}
```

### Usage in Program.cs

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add throttled Ollama services
builder.Services.AddThrottledOllamaServices(options =>
{
    options.MaxChatConcurrency = 2;           // Match OLLAMA_NUM_PARALLEL
    options.MaxEmbeddingConcurrency = 4;
    options.EmbeddingBatchSize = 10;
    options.MaxQueueDepth = 100;
    options.RequestTimeout = TimeSpan.FromMinutes(5);
});

// Add MCP server services
builder.Services.AddMcpServer();

var app = builder.Build();

// Configure MCP endpoints
app.MapMcpEndpoints();

app.Run();
```

---

## 12. Monitoring and Observability

### OpenTelemetry Metrics for Queue Monitoring

```csharp
using System.Diagnostics.Metrics;

public class OllamaMetrics
{
    private static readonly Meter Meter = new("YourMcpServer.Ollama", "1.0.0");

    public static readonly UpDownCounter<int> ChatQueueDepth = Meter.CreateUpDownCounter<int>(
        "ollama.chat.queue_depth",
        description: "Number of chat requests waiting in queue");

    public static readonly UpDownCounter<int> EmbeddingQueueDepth = Meter.CreateUpDownCounter<int>(
        "ollama.embedding.queue_depth",
        description: "Number of embedding requests waiting in queue");

    public static readonly Counter<int> ChatRequestsTotal = Meter.CreateCounter<int>(
        "ollama.chat.requests_total",
        description: "Total number of chat requests processed");

    public static readonly Counter<int> EmbeddingRequestsTotal = Meter.CreateCounter<int>(
        "ollama.embedding.requests_total",
        description: "Total number of embedding requests processed");

    public static readonly Counter<int> RejectedRequestsTotal = Meter.CreateCounter<int>(
        "ollama.requests.rejected_total",
        description: "Total number of requests rejected due to queue overflow");

    public static readonly Histogram<double> ChatLatency = Meter.CreateHistogram<double>(
        "ollama.chat.latency_seconds",
        unit: "s",
        description: "Chat request latency in seconds");

    public static readonly Histogram<double> EmbeddingLatency = Meter.CreateHistogram<double>(
        "ollama.embedding.latency_seconds",
        unit: "s",
        description: "Embedding request latency in seconds");
}
```

### Instrumented Service Example

```csharp
public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
    ChatHistory chatHistory,
    PromptExecutionSettings? executionSettings = null,
    Kernel? kernel = null,
    CancellationToken cancellationToken = default)
{
    var stopwatch = Stopwatch.StartNew();
    OllamaMetrics.ChatQueueDepth.Add(1);

    try
    {
        await _semaphore.WaitAsync(cancellationToken);
        OllamaMetrics.ChatQueueDepth.Add(-1);

        try
        {
            var result = await _innerService.GetChatMessageContentsAsync(
                chatHistory, executionSettings, kernel, cancellationToken);

            OllamaMetrics.ChatRequestsTotal.Add(1);
            return result;
        }
        finally
        {
            _semaphore.Release();
            stopwatch.Stop();
            OllamaMetrics.ChatLatency.Record(stopwatch.Elapsed.TotalSeconds);
        }
    }
    catch (OperationCanceledException)
    {
        OllamaMetrics.ChatQueueDepth.Add(-1);
        OllamaMetrics.RejectedRequestsTotal.Add(1);
        throw;
    }
}
```

### OpenTelemetry Setup

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter("YourMcpServer.Ollama")
            .AddPrometheusExporter();
    })
    .WithTracing(tracing =>
    {
        tracing
            .AddSource("YourMcpServer")
            .AddOtlpExporter();
    });
```

### Health Check for Ollama

```csharp
public class OllamaHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;

    public OllamaHealthCheck(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("ollama");
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("Ollama is responding");
            }

            return HealthCheckResult.Degraded($"Ollama returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Ollama is not reachable", ex);
        }
    }
}
```

**Sources:**
- [.NET Observability with OpenTelemetry - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-with-otel)
- [OpenTelemetry Metrics](https://opentelemetry.io/docs/concepts/signals/metrics/)

---

## Summary and Key Recommendations

### For Your MCP Server with Ollama

1. **Use SemaphoreSlim or ConcurrencyLimiter** to match `OLLAMA_NUM_PARALLEL`
2. **Create wrapper services** around `IChatCompletionService` and `ITextEmbeddingGenerationService`
3. **Implement batching** for embedding requests (batch size 5-10)
4. **Set appropriate timeouts** (5+ minutes for CPU inference)
5. **Monitor queue depth** using OpenTelemetry metrics
6. **Consider separate limits** for chat (lower) vs embeddings (higher)
7. **Implement graceful degradation** with fallback models or cached responses

### Quick Start Configuration

```csharp
// Environment: OLLAMA_NUM_PARALLEL=2
services.AddThrottledOllamaServices(options =>
{
    options.MaxChatConcurrency = 2;        // Match Ollama
    options.MaxEmbeddingConcurrency = 4;   // 2x Ollama (batching helps)
    options.EmbeddingBatchSize = 10;       // Good balance
    options.MaxQueueDepth = 50;            // Reasonable queue
    options.RequestTimeout = TimeSpan.FromMinutes(5);
});
```

---

## Additional Resources

- [Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)
- [Ollama FAQ](https://docs.ollama.com/faq)
- [System.Threading.RateLimiting](https://learn.microsoft.com/en-us/dotnet/api/system.threading.ratelimiting)
- [Polly Documentation](https://www.pollydocs.org/)
- [.NET Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)
- [Microsoft.Extensions.Http.Resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience)
- [MCP Best Practices](https://modelcontextprotocol.info/docs/best-practices/)
