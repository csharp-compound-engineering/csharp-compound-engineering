using CompoundDocs.McpServer.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CompoundDocs.Tests.Resilience;

/// <summary>
/// Unit tests for RateLimiter.
/// </summary>
public sealed class RateLimiterTests : IDisposable
{
    private readonly RateLimiter _rateLimiter;
    private readonly RateLimitOptions _options;

    public RateLimiterTests()
    {
        _options = new RateLimitOptions
        {
            Enabled = true,
            Default = new RateLimitConfig
            {
                RequestsPerMinute = 10,
                RequestsPerHour = 100,
                BurstSize = 5
            }
        };

        _rateLimiter = new RateLimiter(
            Options.Create(_options),
            NullLogger<RateLimiter>.Instance);
    }

    [Fact]
    public void TryAcquire_FirstRequest_ShouldSucceed()
    {
        // Act
        var result = _rateLimiter.TryAcquire("test_tool");

        // Assert
        result.IsAllowed.ShouldBeTrue();
        result.RejectionReason.ShouldBeNull();
    }

    [Fact]
    public void TryAcquire_WithinBurstLimit_ShouldSucceed()
    {
        // Arrange
        const string toolName = "burst_test";

        // Act - Make requests up to burst limit
        var results = new List<RateLimitResult>();
        for (var i = 0; i < 5; i++)
        {
            results.Add(_rateLimiter.TryAcquire(toolName));
        }

        // Assert
        results.ShouldAllBe(r => r.IsAllowed);
    }

    [Fact]
    public void TryAcquire_ExceedingBurstLimit_ShouldBeRejected()
    {
        // Arrange
        const string toolName = "exceed_burst_test";
        var options = new RateLimitOptions
        {
            Enabled = true,
            Default = new RateLimitConfig
            {
                RequestsPerMinute = 10,
                RequestsPerHour = 100,
                BurstSize = 3
            }
        };
        using var limiter = new RateLimiter(Options.Create(options), NullLogger<RateLimiter>.Instance);

        // Act - Exhaust burst capacity
        for (var i = 0; i < 3; i++)
        {
            limiter.TryAcquire(toolName);
        }

        // Now try one more
        var result = limiter.TryAcquire(toolName);

        // Assert
        result.IsAllowed.ShouldBeFalse();
        result.RejectionReason.ShouldNotBeNull();
        result.RetryAfter.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void TryAcquire_WhenDisabled_ShouldAlwaysAllow()
    {
        // Arrange
        var options = new RateLimitOptions { Enabled = false };
        using var limiter = new RateLimiter(Options.Create(options), NullLogger<RateLimiter>.Instance);

        // Act
        var results = new List<RateLimitResult>();
        for (var i = 0; i < 1000; i++)
        {
            results.Add(limiter.TryAcquire("any_tool"));
        }

        // Assert
        results.ShouldAllBe(r => r.IsAllowed);
    }

    [Fact]
    public void TryAcquire_WithClientId_SeparatesRateLimits()
    {
        // Arrange
        const string toolName = "client_test";
        var options = new RateLimitOptions
        {
            Enabled = true,
            Default = new RateLimitConfig
            {
                RequestsPerMinute = 5,
                RequestsPerHour = 50,
                BurstSize = 2
            }
        };
        using var limiter = new RateLimiter(Options.Create(options), NullLogger<RateLimiter>.Instance);

        // Act - Exhaust client1's limit
        limiter.TryAcquire(toolName, "client1");
        limiter.TryAcquire(toolName, "client1");
        var client1Result = limiter.TryAcquire(toolName, "client1");

        // Client2 should still have capacity
        var client2Result = limiter.TryAcquire(toolName, "client2");

        // Assert
        client1Result.IsAllowed.ShouldBeFalse();
        client2Result.IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public void TryAcquire_WithToolSpecificConfig_UsesCorrectLimits()
    {
        // Arrange
        var options = new RateLimitOptions
        {
            Enabled = true,
            Default = new RateLimitConfig
            {
                RequestsPerMinute = 100,
                RequestsPerHour = 1000,
                BurstSize = 50
            },
            Tools = new Dictionary<string, RateLimitConfig>
            {
                ["expensive_tool"] = new RateLimitConfig
                {
                    RequestsPerMinute = 5,
                    RequestsPerHour = 20,
                    BurstSize = 2
                }
            }
        };
        using var limiter = new RateLimiter(Options.Create(options), NullLogger<RateLimiter>.Instance);

        // Act - Exhaust expensive_tool's burst
        limiter.TryAcquire("expensive_tool");
        limiter.TryAcquire("expensive_tool");
        var expensiveResult = limiter.TryAcquire("expensive_tool");

        // Default tool should still work
        var defaultResult = limiter.TryAcquire("default_tool");

        // Assert
        expensiveResult.IsAllowed.ShouldBeFalse();
        defaultResult.IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public void GetConfigForTool_WithConfiguredTool_ReturnsToolConfig()
    {
        // Arrange
        var toolConfig = new RateLimitConfig
        {
            RequestsPerMinute = 5,
            RequestsPerHour = 20,
            BurstSize = 2
        };
        var options = new RateLimitOptions
        {
            Tools = new Dictionary<string, RateLimitConfig>
            {
                ["special_tool"] = toolConfig
            }
        };
        using var limiter = new RateLimiter(Options.Create(options), NullLogger<RateLimiter>.Instance);

        // Act
        var config = limiter.GetConfigForTool("special_tool");

        // Assert
        config.RequestsPerMinute.ShouldBe(5);
        config.RequestsPerHour.ShouldBe(20);
        config.BurstSize.ShouldBe(2);
    }

    [Fact]
    public void GetConfigForTool_WithUnconfiguredTool_ReturnsDefaultConfig()
    {
        // Arrange
        var options = new RateLimitOptions
        {
            Default = new RateLimitConfig
            {
                RequestsPerMinute = 60,
                RequestsPerHour = 1000,
                BurstSize = 10
            }
        };
        using var limiter = new RateLimiter(Options.Create(options), NullLogger<RateLimiter>.Instance);

        // Act
        var config = limiter.GetConfigForTool("unknown_tool");

        // Assert
        config.RequestsPerMinute.ShouldBe(60);
        config.RequestsPerHour.ShouldBe(1000);
        config.BurstSize.ShouldBe(10);
    }

    [Fact]
    public void Reset_ClearsToolState()
    {
        // Arrange
        const string toolName = "reset_test";
        var options = new RateLimitOptions
        {
            Enabled = true,
            Default = new RateLimitConfig
            {
                RequestsPerMinute = 5,
                RequestsPerHour = 50,
                BurstSize = 2
            }
        };
        using var limiter = new RateLimiter(Options.Create(options), NullLogger<RateLimiter>.Instance);

        // Exhaust burst
        limiter.TryAcquire(toolName);
        limiter.TryAcquire(toolName);
        limiter.TryAcquire(toolName).IsAllowed.ShouldBeFalse();

        // Act
        limiter.Reset(toolName);

        // Assert - Should have burst capacity again
        limiter.TryAcquire(toolName).IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public void ResetAll_ClearsAllState()
    {
        // Arrange
        var options = new RateLimitOptions
        {
            Enabled = true,
            Default = new RateLimitConfig
            {
                RequestsPerMinute = 5,
                RequestsPerHour = 50,
                BurstSize = 2
            }
        };
        using var limiter = new RateLimiter(Options.Create(options), NullLogger<RateLimiter>.Instance);

        // Exhaust multiple tools
        limiter.TryAcquire("tool1");
        limiter.TryAcquire("tool1");
        limiter.TryAcquire("tool2");
        limiter.TryAcquire("tool2");

        // Act
        limiter.ResetAll();

        // Assert - All tools should have burst capacity again
        limiter.TryAcquire("tool1").IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("tool2").IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public async Task WaitAndAcquireAsync_WhenAvailable_ReturnsImmediately()
    {
        // Arrange
        const string toolName = "wait_test";

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _rateLimiter.WaitAndAcquireAsync(toolName);
        stopwatch.Stop();

        // Assert
        result.IsAllowed.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task WaitAndAcquireAsync_WhenCancelled_ReturnsRejected()
    {
        // Arrange
        const string toolName = "cancel_test";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _rateLimiter.WaitAndAcquireAsync(
            toolName,
            cancellationToken: cts.Token);

        // Assert
        result.IsAllowed.ShouldBeFalse();
        result.RejectionReason.ShouldNotBeNull();
        result.RejectionReason!.ShouldContain("cancelled");
    }

    [Fact]
    public void TryAcquire_ReturnsRemainingCapacity()
    {
        // Arrange
        const string toolName = "capacity_test";
        var options = new RateLimitOptions
        {
            Enabled = true,
            Default = new RateLimitConfig
            {
                RequestsPerMinute = 10,
                RequestsPerHour = 100,
                BurstSize = 5
            }
        };
        using var limiter = new RateLimiter(Options.Create(options), NullLogger<RateLimiter>.Instance);

        // Act
        var result1 = limiter.TryAcquire(toolName);
        var result2 = limiter.TryAcquire(toolName);

        // Assert
        result1.RemainingPerMinute.ShouldBeGreaterThan(0);
        result2.RemainingPerMinute.ShouldBeLessThan(result1.RemainingPerMinute);
    }

    public void Dispose()
    {
        _rateLimiter.Dispose();
    }
}
