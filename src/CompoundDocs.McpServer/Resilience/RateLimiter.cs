using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Resilience;

/// <summary>
/// Configuration options for rate limiting.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Whether rate limiting is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Default rate limit configuration for tools without specific settings.
    /// </summary>
    public RateLimitConfig Default { get; set; } = new()
    {
        RequestsPerMinute = 60,
        RequestsPerHour = 1000,
        BurstSize = 10
    };

    /// <summary>
    /// Per-tool rate limit configurations. Key is the tool name.
    /// </summary>
    public Dictionary<string, RateLimitConfig> Tools { get; set; } = new();
}

/// <summary>
/// Rate limit configuration for a specific tool.
/// </summary>
public sealed class RateLimitConfig
{
    /// <summary>
    /// Maximum requests per minute.
    /// </summary>
    public int RequestsPerMinute { get; set; } = 60;

    /// <summary>
    /// Maximum requests per hour.
    /// </summary>
    public int RequestsPerHour { get; set; } = 1000;

    /// <summary>
    /// Maximum burst size (tokens available for immediate use).
    /// </summary>
    public int BurstSize { get; set; } = 10;
}

/// <summary>
/// Result of a rate limit check.
/// </summary>
public sealed record RateLimitResult
{
    /// <summary>
    /// Whether the request is allowed.
    /// </summary>
    public required bool IsAllowed { get; init; }

    /// <summary>
    /// Time to wait before the next request is allowed (if rejected).
    /// </summary>
    public TimeSpan RetryAfter { get; init; }

    /// <summary>
    /// Remaining requests in the current minute window.
    /// </summary>
    public int RemainingPerMinute { get; init; }

    /// <summary>
    /// Remaining requests in the current hour window.
    /// </summary>
    public int RemainingPerHour { get; init; }

    /// <summary>
    /// The reason for rejection if not allowed.
    /// </summary>
    public string? RejectionReason { get; init; }

    /// <summary>
    /// Creates an allowed result.
    /// </summary>
    public static RateLimitResult Allowed(int remainingPerMinute, int remainingPerHour) => new()
    {
        IsAllowed = true,
        RemainingPerMinute = remainingPerMinute,
        RemainingPerHour = remainingPerHour
    };

    /// <summary>
    /// Creates a rejected result.
    /// </summary>
    public static RateLimitResult Rejected(TimeSpan retryAfter, string reason) => new()
    {
        IsAllowed = false,
        RetryAfter = retryAfter,
        RejectionReason = reason
    };
}

/// <summary>
/// Token bucket rate limiter implementation for per-tool rate limiting.
/// Supports configurable limits per minute and per hour with burst capacity.
/// </summary>
public sealed class RateLimiter : IRateLimiter, IDisposable
{
    private readonly ILogger<RateLimiter> _logger;
    private readonly RateLimitOptions _options;
    private readonly ConcurrentDictionary<string, TokenBucket> _minuteBuckets = new();
    private readonly ConcurrentDictionary<string, TokenBucket> _hourBuckets = new();
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Creates a new instance of RateLimiter.
    /// </summary>
    /// <param name="options">Rate limit options.</param>
    /// <param name="logger">Logger instance.</param>
    public RateLimiter(
        IOptions<RateLimitOptions> options,
        ILogger<RateLimiter> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Clean up stale buckets every 5 minutes
        _cleanupTimer = new Timer(CleanupStaleBuckets, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <inheritdoc />
    public RateLimitResult TryAcquire(string toolName, string? clientId = null)
    {
        if (!_options.Enabled)
        {
            return RateLimitResult.Allowed(int.MaxValue, int.MaxValue);
        }

        var config = GetConfigForTool(toolName);
        var bucketKey = CreateBucketKey(toolName, clientId);

        // Get or create minute bucket
        var minuteBucket = _minuteBuckets.GetOrAdd(bucketKey, _ => new TokenBucket(
            config.RequestsPerMinute,
            config.BurstSize,
            TimeSpan.FromMinutes(1)));

        // Get or create hour bucket
        var hourBucket = _hourBuckets.GetOrAdd(bucketKey, _ => new TokenBucket(
            config.RequestsPerHour,
            config.BurstSize * 2,
            TimeSpan.FromHours(1)));

        // Check minute limit first
        if (!minuteBucket.TryConsume(out var minuteRetryAfter))
        {
            _logger.LogWarning(
                "Rate limit exceeded for tool {ToolName} (per-minute). Retry after {RetryAfterMs}ms",
                toolName,
                minuteRetryAfter.TotalMilliseconds);

            return RateLimitResult.Rejected(minuteRetryAfter, "Per-minute rate limit exceeded");
        }

        // Check hour limit
        if (!hourBucket.TryConsume(out var hourRetryAfter))
        {
            // Refund the minute token since we're rejecting
            minuteBucket.Refund();

            _logger.LogWarning(
                "Rate limit exceeded for tool {ToolName} (per-hour). Retry after {RetryAfterMs}ms",
                toolName,
                hourRetryAfter.TotalMilliseconds);

            return RateLimitResult.Rejected(hourRetryAfter, "Per-hour rate limit exceeded");
        }

        return RateLimitResult.Allowed(minuteBucket.AvailableTokens, hourBucket.AvailableTokens);
    }

    /// <inheritdoc />
    public async Task<RateLimitResult> WaitAndAcquireAsync(
        string toolName,
        string? clientId = null,
        TimeSpan? maxWait = null,
        CancellationToken cancellationToken = default)
    {
        var maxWaitTime = maxWait ?? TimeSpan.FromSeconds(30);
        var startTime = DateTime.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = TryAcquire(toolName, clientId);
            if (result.IsAllowed)
            {
                return result;
            }

            var elapsed = DateTime.UtcNow - startTime;
            if (elapsed >= maxWaitTime)
            {
                return RateLimitResult.Rejected(result.RetryAfter, "Max wait time exceeded");
            }

            var waitTime = TimeSpan.FromMilliseconds(Math.Min(
                result.RetryAfter.TotalMilliseconds,
                (maxWaitTime - elapsed).TotalMilliseconds));

            if (waitTime > TimeSpan.Zero)
            {
                await Task.Delay(waitTime, cancellationToken);
            }
        }

        return RateLimitResult.Rejected(TimeSpan.Zero, "Operation cancelled");
    }

    /// <inheritdoc />
    public RateLimitConfig GetConfigForTool(string toolName)
    {
        return _options.Tools.TryGetValue(toolName, out var config)
            ? config
            : _options.Default;
    }

    /// <inheritdoc />
    public void Reset(string toolName, string? clientId = null)
    {
        var bucketKey = CreateBucketKey(toolName, clientId);
        _minuteBuckets.TryRemove(bucketKey, out _);
        _hourBuckets.TryRemove(bucketKey, out _);
    }

    /// <inheritdoc />
    public void ResetAll()
    {
        _minuteBuckets.Clear();
        _hourBuckets.Clear();
    }

    private static string CreateBucketKey(string toolName, string? clientId)
    {
        return clientId != null ? $"{toolName}:{clientId}" : toolName;
    }

    private void CleanupStaleBuckets(object? state)
    {
        var staleThreshold = TimeSpan.FromMinutes(10);
        var now = DateTime.UtcNow;

        foreach (var kvp in _minuteBuckets)
        {
            if (now - kvp.Value.LastAccess > staleThreshold)
            {
                _minuteBuckets.TryRemove(kvp.Key, out _);
            }
        }

        foreach (var kvp in _hourBuckets)
        {
            if (now - kvp.Value.LastAccess > staleThreshold)
            {
                _hourBuckets.TryRemove(kvp.Key, out _);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Interface for rate limiting operations.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Attempts to acquire a rate limit token for the specified tool.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="clientId">Optional client identifier for per-client limiting.</param>
    /// <returns>The rate limit result indicating if the request is allowed.</returns>
    RateLimitResult TryAcquire(string toolName, string? clientId = null);

    /// <summary>
    /// Waits until a rate limit token is available or max wait time is exceeded.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <param name="clientId">Optional client identifier.</param>
    /// <param name="maxWait">Maximum time to wait.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rate limit result.</returns>
    Task<RateLimitResult> WaitAndAcquireAsync(
        string toolName,
        string? clientId = null,
        TimeSpan? maxWait = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the rate limit configuration for a tool.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <returns>The rate limit configuration.</returns>
    RateLimitConfig GetConfigForTool(string toolName);

    /// <summary>
    /// Resets the rate limit state for a tool.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="clientId">Optional client identifier.</param>
    void Reset(string toolName, string? clientId = null);

    /// <summary>
    /// Resets all rate limit state.
    /// </summary>
    void ResetAll();
}

/// <summary>
/// Thread-safe token bucket implementation for rate limiting.
/// </summary>
internal sealed class TokenBucket
{
    private readonly object _lock = new();
    private readonly int _capacity;
    private readonly int _burstCapacity;
    private readonly double _refillRatePerMs;
    private double _tokens;
    private DateTime _lastRefill;
    private DateTime _lastAccess;

    /// <summary>
    /// Creates a new token bucket.
    /// </summary>
    /// <param name="capacity">Maximum tokens (requests) per window.</param>
    /// <param name="burstCapacity">Initial burst capacity.</param>
    /// <param name="window">Time window for the capacity.</param>
    public TokenBucket(int capacity, int burstCapacity, TimeSpan window)
    {
        _capacity = capacity;
        _burstCapacity = Math.Min(burstCapacity, capacity);
        _refillRatePerMs = (double)capacity / window.TotalMilliseconds;
        _tokens = _burstCapacity;
        _lastRefill = DateTime.UtcNow;
        _lastAccess = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the last access time.
    /// </summary>
    public DateTime LastAccess
    {
        get
        {
            lock (_lock)
            {
                return _lastAccess;
            }
        }
    }

    /// <summary>
    /// Gets the available tokens.
    /// </summary>
    public int AvailableTokens
    {
        get
        {
            lock (_lock)
            {
                Refill();
                return (int)_tokens;
            }
        }
    }

    /// <summary>
    /// Attempts to consume a token.
    /// </summary>
    /// <param name="retryAfter">Time to wait if no tokens available.</param>
    /// <returns>True if a token was consumed, false otherwise.</returns>
    public bool TryConsume(out TimeSpan retryAfter)
    {
        lock (_lock)
        {
            _lastAccess = DateTime.UtcNow;
            Refill();

            if (_tokens >= 1)
            {
                _tokens -= 1;
                retryAfter = TimeSpan.Zero;
                return true;
            }

            // Calculate time until next token is available
            var tokensNeeded = 1 - _tokens;
            var msUntilToken = tokensNeeded / _refillRatePerMs;
            retryAfter = TimeSpan.FromMilliseconds(Math.Ceiling(msUntilToken));
            return false;
        }
    }

    /// <summary>
    /// Refunds a consumed token.
    /// </summary>
    public void Refund()
    {
        lock (_lock)
        {
            _tokens = Math.Min(_tokens + 1, _capacity);
        }
    }

    private void Refill()
    {
        var now = DateTime.UtcNow;
        var elapsed = now - _lastRefill;
        var tokensToAdd = elapsed.TotalMilliseconds * _refillRatePerMs;
        _tokens = Math.Min(_tokens + tokensToAdd, _capacity);
        _lastRefill = now;
    }
}
