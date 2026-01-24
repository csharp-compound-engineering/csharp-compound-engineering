namespace CompoundDocs.McpServer.Resilience;

/// <summary>
/// Configuration options for resilience policies.
/// </summary>
public sealed class ResilienceOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "Resilience";

    /// <summary>
    /// Options for retry policy.
    /// </summary>
    public RetryOptions Retry { get; set; } = new();

    /// <summary>
    /// Options for circuit breaker policy.
    /// </summary>
    public CircuitBreakerOptions CircuitBreaker { get; set; } = new();

    /// <summary>
    /// Options for timeout policy.
    /// </summary>
    public TimeoutOptions Timeout { get; set; } = new();
}

/// <summary>
/// Configuration options for retry policy with exponential backoff.
/// </summary>
public sealed class RetryOptions
{
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Initial delay before the first retry in milliseconds.
    /// </summary>
    public int InitialDelayMs { get; set; } = 200;

    /// <summary>
    /// Maximum delay between retries in milliseconds.
    /// </summary>
    public int MaxDelayMs { get; set; } = 5000;

    /// <summary>
    /// Multiplier for exponential backoff.
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Whether to add jitter to retry delays to prevent thundering herd.
    /// </summary>
    public bool UseJitter { get; set; } = true;
}

/// <summary>
/// Configuration options for circuit breaker policy.
/// </summary>
public sealed class CircuitBreakerOptions
{
    /// <summary>
    /// Failure ratio threshold to trip the circuit (0.0 to 1.0).
    /// </summary>
    public double FailureRatioThreshold { get; set; } = 0.5;

    /// <summary>
    /// Minimum number of calls before calculating failure ratio.
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Sampling duration in seconds for calculating failure ratio.
    /// </summary>
    public int SamplingDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Duration the circuit stays open before attempting half-open state in seconds.
    /// </summary>
    public int BreakDurationSeconds { get; set; } = 30;
}

/// <summary>
/// Configuration options for timeout policy.
/// </summary>
public sealed class TimeoutOptions
{
    /// <summary>
    /// Default timeout for operations in seconds.
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout for embedding operations in seconds.
    /// </summary>
    public int EmbeddingTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Timeout for database operations in seconds.
    /// </summary>
    public int DatabaseTimeoutSeconds { get; set; } = 15;
}
