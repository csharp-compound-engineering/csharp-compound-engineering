# Phase 030: Resilience Patterns (Circuit Breaker, Retry)

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: MCP Server Core
> **Prerequisites**: Phase 029

---

## Spec References

This phase implements resilience patterns defined in:

- **spec/mcp-server/ollama-integration.md** - [Resilience and Circuit Breaker](../spec/mcp-server/ollama-integration.md#resilience-and-circuit-breaker) (lines 116-179)
- **spec/mcp-server/ollama-integration.md** - [HttpClient Configuration](../spec/mcp-server/ollama-integration.md#httpclient-configuration) (lines 75-112)
- **spec/mcp-server/ollama-integration.md** - [Rate Limiting](../spec/mcp-server/ollama-integration.md#rate-limiting) (lines 307-328)
- **spec/mcp-server/ollama-integration.md** - [Graceful Degradation](../spec/mcp-server/ollama-integration.md#graceful-degradation) (lines 183-219)
- **spec/mcp-server.md** - [Error Handling](../spec/mcp-server.md#error-handling) (lines 230-248)

---

## Objectives

1. Implement rate limiting with concurrency limiter aligned to OLLAMA_NUM_PARALLEL
2. Implement exponential backoff retry strategy (3 attempts with jitter)
3. Implement circuit breaker pattern (50% failure ratio, 30s break duration)
4. Integrate Microsoft.Extensions.Http.Resilience (built on Polly v8)
5. Implement graceful degradation behavior for service unavailability
6. Implement health status reporting for circuit breaker state

---

## Acceptance Criteria

### Package References

- [ ] `Polly.Core` version 8.* added to project
- [ ] `Microsoft.Extensions.Http.Resilience` version 9.* added to project
- [ ] Packages added to `Directory.Packages.props` for central version management

### Rate Limiting Implementation

- [ ] `ConcurrencyLimiter` configured with `PermitLimit = 2` (default for 16GB+ Apple Silicon)
- [ ] `QueueLimit = 10` for requests waiting when at capacity
- [ ] `QueueProcessingOrder.OldestFirst` for FIFO fairness
- [ ] Rate limiter configurable via options pattern for different hardware profiles

### Retry Strategy Implementation

- [ ] `HttpRetryStrategyOptions` configured for transient error handling
- [ ] `MaxRetryAttempts = 3`
- [ ] `BackoffType = DelayBackoffType.Exponential`
- [ ] `UseJitter = true` for randomized delays
- [ ] Initial `Delay = TimeSpan.FromSeconds(1)` (results in 1s, 2s, 4s backoff)
- [ ] Retry only on transient HTTP errors (5xx, timeouts, network errors)

### Circuit Breaker Implementation

- [ ] `HttpCircuitBreakerStrategyOptions` configured per spec:
  - [ ] `FailureRatio = 0.5` (50% failure threshold)
  - [ ] `MinimumThroughput = 5` (minimum requests before evaluation)
  - [ ] `SamplingDuration = TimeSpan.FromSeconds(30)`
  - [ ] `BreakDuration = TimeSpan.FromSeconds(30)`
- [ ] Circuit breaker state machine (Closed -> Open -> Half-Open) working correctly
- [ ] Circuit state exposed via health reporting

### Graceful Degradation

- [ ] `EMBEDDING_SERVICE_ERROR` returned when circuit is open with retry suggestion
- [ ] Error response includes `circuit_state` and `retry_after_seconds`
- [ ] File watcher queues documents for later indexing when Ollama unavailable
- [ ] Semantic search returns clear error when embedding service unavailable

### Health Status Reporting

- [ ] `IOllamaHealthService` interface created
- [ ] `GetCircuitStateAsync()` method returns current circuit breaker state
- [ ] `GetLastErrorAsync()` method returns last failure information
- [ ] Health status available for diagnostic tools

---

## Implementation Notes

### Required NuGet Packages

Add to `Directory.Packages.props`:

```xml
<ItemGroup>
  <PackageVersion Include="Polly.Core" Version="8.*" />
  <PackageVersion Include="Microsoft.Extensions.Http.Resilience" Version="9.*" />
</ItemGroup>
```

### HttpClient Resilience Configuration

Configure in `Program.cs` or extension method:

```csharp
builder.Services.AddHttpClient("ollama", client =>
{
    client.BaseAddress = new Uri(ollamaHost);
    client.Timeout = TimeSpan.FromMinutes(5);  // Long timeout for embeddings
})
.AddResilienceHandler("ollama-resilience", ConfigureOllamaResilience);

private static void ConfigureOllamaResilience(ResiliencePipelineBuilder<HttpResponseMessage> builder)
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
}
```

### Resilience Options Class

Create configurable options:

```csharp
/// <summary>
/// Configuration options for Ollama resilience patterns.
/// </summary>
public class OllamaResilienceOptions
{
    public const string SectionName = "Ollama:Resilience";

    /// <summary>
    /// Maximum concurrent requests to Ollama. Should match OLLAMA_NUM_PARALLEL.
    /// </summary>
    public int ConcurrencyLimit { get; set; } = 2;

    /// <summary>
    /// Maximum requests waiting when at capacity.
    /// </summary>
    public int QueueLimit { get; set; } = 10;

    /// <summary>
    /// Maximum retry attempts for transient failures.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial delay before first retry.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Failure ratio threshold to open circuit (0.0 to 1.0).
    /// </summary>
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>
    /// Minimum requests before circuit breaker evaluates.
    /// </summary>
    public int CircuitBreakerMinimumThroughput { get; set; } = 5;

    /// <summary>
    /// Time window for sampling failures.
    /// </summary>
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Duration circuit stays open before testing.
    /// </summary>
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}
```

### Hardware Profile Presets

Create presets for different hardware configurations:

```csharp
/// <summary>
/// Predefined resilience options for different hardware profiles.
/// </summary>
public static class OllamaResiliencePresets
{
    /// <summary>
    /// Apple M1/M2 with 8GB RAM - conservative settings.
    /// </summary>
    public static OllamaResilienceOptions AppleSilicon8GB => new()
    {
        ConcurrencyLimit = 1,
        QueueLimit = 5
    };

    /// <summary>
    /// Apple M1/M2 with 16GB+ RAM - moderate settings.
    /// </summary>
    public static OllamaResilienceOptions AppleSilicon16GB => new()
    {
        ConcurrencyLimit = 2,
        QueueLimit = 10
    };

    /// <summary>
    /// NVIDIA GPU with 8GB VRAM.
    /// </summary>
    public static OllamaResilienceOptions NvidiaGpu8GB => new()
    {
        ConcurrencyLimit = 2,
        QueueLimit = 10
    };

    /// <summary>
    /// NVIDIA GPU with 16GB+ VRAM - higher throughput.
    /// </summary>
    public static OllamaResilienceOptions NvidiaGpu16GB => new()
    {
        ConcurrencyLimit = 4,
        QueueLimit = 20
    };

    /// <summary>
    /// CPU-only mode - minimal concurrency.
    /// </summary>
    public static OllamaResilienceOptions CpuOnly => new()
    {
        ConcurrencyLimit = 1,
        QueueLimit = 5
    };
}
```

### Health Service Interface

```csharp
/// <summary>
/// Service for monitoring Ollama connection health and circuit breaker state.
/// </summary>
public interface IOllamaHealthService
{
    /// <summary>
    /// Gets the current state of the circuit breaker.
    /// </summary>
    Task<CircuitBreakerState> GetCircuitStateAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets information about the last failure, if any.
    /// </summary>
    Task<FailureInfo?> GetLastFailureAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the estimated time until the circuit breaker may allow requests again.
    /// Returns null if circuit is closed or half-open.
    /// </summary>
    Task<TimeSpan?> GetRetryAfterAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if Ollama is currently available for requests.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}

/// <summary>
/// Circuit breaker state enumeration.
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>Normal operation - requests flow through.</summary>
    Closed,

    /// <summary>Circuit is open - requests fail fast.</summary>
    Open,

    /// <summary>Testing with single request to determine if service recovered.</summary>
    HalfOpen
}

/// <summary>
/// Information about a failure that contributed to circuit breaker state.
/// </summary>
public record FailureInfo(
    DateTimeOffset Timestamp,
    string ErrorType,
    string Message,
    int ConsecutiveFailures);
```

### Graceful Degradation Error Response

```csharp
/// <summary>
/// Creates standardized error response for embedding service unavailability.
/// </summary>
public static class EmbeddingServiceErrors
{
    public static ErrorResponse CircuitOpen(TimeSpan retryAfter) => new()
    {
        Error = true,
        Code = "EMBEDDING_SERVICE_ERROR",
        Message = $"Embedding service unavailable. The circuit breaker is open. Try again in {(int)retryAfter.TotalSeconds} seconds.",
        Details = new Dictionary<string, object>
        {
            ["circuit_state"] = "open",
            ["retry_after_seconds"] = (int)retryAfter.TotalSeconds
        }
    };

    public static ErrorResponse ServiceUnavailable() => new()
    {
        Error = true,
        Code = "EMBEDDING_SERVICE_ERROR",
        Message = "Cannot perform semantic search: embedding service unavailable.",
        Details = new Dictionary<string, object>()
    };

    public static ErrorResponse RetryExhausted(int attempts) => new()
    {
        Error = true,
        Code = "EMBEDDING_SERVICE_ERROR",
        Message = $"Embedding request failed after {attempts} retry attempts.",
        Details = new Dictionary<string, object>
        {
            ["retry_attempts"] = attempts
        }
    };
}
```

### Circuit Breaker State Diagram

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

### Retry Policy Error Type Matrix

| Error Type | Retry | Max Attempts | Backoff | Fallback |
|------------|-------|--------------|---------|----------|
| `EMBEDDING_SERVICE_ERROR` | Yes | 3 | Exponential (1s, 2s, 4s) | Return error with retry suggestion |
| `DATABASE_ERROR` | Yes | 3 | Exponential (100ms, 200ms, 400ms) | Return error |
| `FILE_SYSTEM_ERROR` | No | - | - | Log and skip, continue with other files |
| `SCHEMA_VALIDATION_FAILED` | No | - | - | Return specific field errors |

### Database Retry Strategy

For database operations, use a separate retry strategy:

```csharp
builder.Services.AddResiliencePipeline("database", builder =>
{
    builder.AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromMilliseconds(100),
        ShouldHandle = new PredicateBuilder()
            .Handle<NpgsqlException>(ex => ex.IsTransient)
            .Handle<TimeoutException>()
    });
});
```

### Extension Method for Service Registration

```csharp
public static class ResilienceServiceExtensions
{
    public static IServiceCollection AddOllamaResiliencePatterns(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind options from configuration
        services.Configure<OllamaResilienceOptions>(
            configuration.GetSection(OllamaResilienceOptions.SectionName));

        // Register health service
        services.AddSingleton<IOllamaHealthService, OllamaHealthService>();

        // Configure HttpClient with resilience
        services.AddHttpClient("ollama")
            .AddResilienceHandler("ollama-resilience", (builder, sp) =>
            {
                var options = sp.GetRequiredService<IOptions<OllamaResilienceOptions>>().Value;
                ConfigureOllamaResilience(builder, options);
            });

        return services;
    }

    private static void ConfigureOllamaResilience(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        OllamaResilienceOptions options)
    {
        // Implementation as shown above...
    }
}
```

---

## Test Cases

### Unit Tests

```csharp
[Fact]
public async Task RetryPolicy_RetriesOnTransientError()
{
    // Arrange: Mock HttpClient that fails twice then succeeds
    // Act: Make request through resilience pipeline
    // Assert: Request succeeds after retries, 3 total attempts made
}

[Fact]
public async Task CircuitBreaker_OpensAfter50PercentFailures()
{
    // Arrange: Mock HttpClient that fails 3 of 5 requests
    // Act: Make 5 requests
    // Assert: Circuit breaker is now Open
}

[Fact]
public async Task CircuitBreaker_FailsFastWhenOpen()
{
    // Arrange: Circuit breaker in Open state
    // Act: Attempt request
    // Assert: Immediately fails with BrokenCircuitException
}

[Fact]
public async Task CircuitBreaker_TransitionsToHalfOpenAfterBreakDuration()
{
    // Arrange: Circuit breaker in Open state
    // Act: Wait for break duration + make request
    // Assert: Request attempts (circuit is Half-Open)
}

[Fact]
public async Task RateLimiter_QueuesExcessRequests()
{
    // Arrange: Rate limiter with PermitLimit=2
    // Act: Submit 4 concurrent requests
    // Assert: Only 2 execute concurrently, others queue
}

[Fact]
public async Task GracefulDegradation_ReturnsProperErrorWhenCircuitOpen()
{
    // Arrange: Circuit breaker in Open state
    // Act: Call embedding service
    // Assert: Returns EMBEDDING_SERVICE_ERROR with circuit_state and retry_after_seconds
}
```

### Integration Tests

```csharp
[Fact]
public async Task EndToEnd_ResiliencePipeline_HandlesOllamaFailure()
{
    // Test with real (or mocked) Ollama that fails intermittently
}

[Fact]
public async Task EndToEnd_FileWatcher_QueuesDocumentsWhenOllamaDown()
{
    // Test that file watcher queues documents when circuit is open
}
```

---

## Dependencies

### Depends On

- Phase 029: (prerequisite - assumed to contain Ollama service setup)
- `Microsoft.Extensions.Http.Resilience` package
- `Polly.Core` package

### Blocks

- Subsequent phases that depend on resilient Ollama communication
- Health check and monitoring phases

---

## Verification Steps

After completing this phase, verify:

1. **Retry behavior**: Simulate transient failures and confirm 3 retry attempts with exponential backoff
2. **Circuit breaker**: Cause 50%+ failures and confirm circuit opens after 5 requests
3. **Rate limiting**: Submit concurrent requests and confirm they are properly queued
4. **Graceful degradation**: When circuit is open, verify proper error response format
5. **Health reporting**: Confirm circuit state is exposed via health service
6. **Configuration**: Verify options can be changed via configuration

---

## Notes

- The resilience pipeline order matters: Rate Limiter -> Retry -> Circuit Breaker (outside-in)
- Polly v8 uses `ResiliencePipeline` instead of the older `Policy` abstraction
- `Microsoft.Extensions.Http.Resilience` simplifies integration with IHttpClientFactory
- Circuit breaker state should be observable for debugging and monitoring
- Consider adding metrics/telemetry for production monitoring (future phase)
- The 5-minute timeout for HttpClient is intentionally long for large document embeddings
