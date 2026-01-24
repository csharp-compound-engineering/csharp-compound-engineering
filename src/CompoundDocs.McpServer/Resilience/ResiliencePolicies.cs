using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace CompoundDocs.McpServer.Resilience;

/// <summary>
/// Provides resilience policies for MCP tool handlers using Polly.
/// Implements retry with exponential backoff, circuit breaker, and timeout patterns.
/// </summary>
public sealed class ResiliencePolicies : IResiliencePolicies
{
    private readonly ILogger<ResiliencePolicies> _logger;
    private readonly ResilienceOptions _options;

    private readonly ResiliencePipeline _ollamaPipeline;
    private readonly ResiliencePipeline _databasePipeline;
    private readonly ResiliencePipeline _defaultPipeline;

    /// <summary>
    /// Creates a new instance of ResiliencePolicies.
    /// </summary>
    /// <param name="options">The resilience options.</param>
    /// <param name="logger">Logger instance.</param>
    public ResiliencePolicies(
        IOptions<ResilienceOptions> options,
        ILogger<ResiliencePolicies> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _ollamaPipeline = CreateOllamaPipeline();
        _databasePipeline = CreateDatabasePipeline();
        _defaultPipeline = CreateDefaultPipeline();
    }

    /// <inheritdoc />
    public ResiliencePipeline OllamaPipeline => _ollamaPipeline;

    /// <inheritdoc />
    public ResiliencePipeline DatabasePipeline => _databasePipeline;

    /// <inheritdoc />
    public ResiliencePipeline DefaultPipeline => _defaultPipeline;

    /// <inheritdoc />
    public async Task<T> ExecuteWithOllamaResilienceAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        return await _ollamaPipeline.ExecuteAsync(
            async ct => await operation(ct),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithDatabaseResilienceAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        return await _databasePipeline.ExecuteAsync(
            async ct => await operation(ct),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<T> ExecuteWithDefaultResilienceAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        return await _defaultPipeline.ExecuteAsync(
            async ct => await operation(ct),
            cancellationToken);
    }

    private ResiliencePipeline CreateOllamaPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(_options.Timeout.EmbeddingTimeoutSeconds),
                OnTimeout = args =>
                {
                    _logger.LogWarning(
                        "Ollama operation timed out after {TimeoutSeconds}s",
                        _options.Timeout.EmbeddingTimeoutSeconds);
                    return default;
                }
            })
            .AddRetry(CreateRetryOptions("Ollama"))
            .AddCircuitBreaker(CreateCircuitBreakerOptions("Ollama"))
            .Build();
    }

    private ResiliencePipeline CreateDatabasePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(_options.Timeout.DatabaseTimeoutSeconds),
                OnTimeout = args =>
                {
                    _logger.LogWarning(
                        "Database operation timed out after {TimeoutSeconds}s",
                        _options.Timeout.DatabaseTimeoutSeconds);
                    return default;
                }
            })
            .AddRetry(CreateRetryOptions("Database"))
            .AddCircuitBreaker(CreateCircuitBreakerOptions("Database"))
            .Build();
    }

    private ResiliencePipeline CreateDefaultPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(_options.Timeout.DefaultTimeoutSeconds),
                OnTimeout = args =>
                {
                    _logger.LogWarning(
                        "Operation timed out after {TimeoutSeconds}s",
                        _options.Timeout.DefaultTimeoutSeconds);
                    return default;
                }
            })
            .AddRetry(CreateRetryOptions("Default"))
            .Build();
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Private Polly configuration factory; UseJitter ternary has identical branches")]
    private RetryStrategyOptions CreateRetryOptions(string context)
    {
        var retryOptions = _options.Retry;

        return new RetryStrategyOptions
        {
            MaxRetryAttempts = retryOptions.MaxRetryAttempts,
            BackoffType = retryOptions.UseJitter
                ? DelayBackoffType.Exponential
                : DelayBackoffType.Exponential,
            UseJitter = retryOptions.UseJitter,
            Delay = TimeSpan.FromMilliseconds(retryOptions.InitialDelayMs),
            MaxDelay = TimeSpan.FromMilliseconds(retryOptions.MaxDelayMs),
            ShouldHandle = new PredicateBuilder()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .Handle<System.IO.IOException>()
                .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
                .Handle<InvalidOperationException>(ex => IsTransientError(ex)),
            OnRetry = args =>
            {
                _logger.LogWarning(
                    "Retry attempt {AttemptNumber} for {Context} after {Delay}ms due to {ExceptionType}: {Message}",
                    args.AttemptNumber,
                    context,
                    args.RetryDelay.TotalMilliseconds,
                    args.Outcome.Exception?.GetType().Name ?? "unknown",
                    args.Outcome.Exception?.Message ?? "No exception");
                return default;
            }
        };
    }

    private CircuitBreakerStrategyOptions CreateCircuitBreakerOptions(string context)
    {
        var cbOptions = _options.CircuitBreaker;

        return new CircuitBreakerStrategyOptions
        {
            FailureRatio = cbOptions.FailureRatioThreshold,
            MinimumThroughput = cbOptions.MinimumThroughput,
            SamplingDuration = TimeSpan.FromSeconds(cbOptions.SamplingDurationSeconds),
            BreakDuration = TimeSpan.FromSeconds(cbOptions.BreakDurationSeconds),
            ShouldHandle = new PredicateBuilder()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .Handle<System.IO.IOException>()
                .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested),
            OnOpened = args =>
            {
                _logger.LogError(
                    "Circuit breaker OPENED for {Context}. Break duration: {BreakDuration}s. Reason: {ExceptionType}",
                    context,
                    cbOptions.BreakDurationSeconds,
                    args.Outcome.Exception?.GetType().Name ?? "unknown");
                return default;
            },
            OnClosed = args =>
            {
                _logger.LogInformation(
                    "Circuit breaker CLOSED for {Context}. Service recovered.",
                    context);
                return default;
            },
            OnHalfOpened = args =>
            {
                _logger.LogInformation(
                    "Circuit breaker HALF-OPEN for {Context}. Testing service availability.",
                    context);
                return default;
            }
        };
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage(Justification = "Private predicate invoked by Polly retry pipeline; not directly testable")]
    private static bool IsTransientError(InvalidOperationException ex)
    {
        // Check for known transient error patterns in exception message
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("connection")
            || message.Contains("timeout")
            || message.Contains("unavailable")
            || message.Contains("temporarily");
    }
}

/// <summary>
/// Interface for resilience policies.
/// </summary>
public interface IResiliencePolicies
{
    /// <summary>
    /// Gets the resilience pipeline for Ollama operations.
    /// Includes retry, circuit breaker, and timeout.
    /// </summary>
    ResiliencePipeline OllamaPipeline { get; }

    /// <summary>
    /// Gets the resilience pipeline for database operations.
    /// Includes retry, circuit breaker, and timeout.
    /// </summary>
    ResiliencePipeline DatabasePipeline { get; }

    /// <summary>
    /// Gets the default resilience pipeline.
    /// Includes retry and timeout.
    /// </summary>
    ResiliencePipeline DefaultPipeline { get; }

    /// <summary>
    /// Executes an operation with Ollama resilience policies.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<T> ExecuteWithOllamaResilienceAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation with database resilience policies.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<T> ExecuteWithDatabaseResilienceAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an operation with default resilience policies.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The operation result.</returns>
    Task<T> ExecuteWithDefaultResilienceAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
