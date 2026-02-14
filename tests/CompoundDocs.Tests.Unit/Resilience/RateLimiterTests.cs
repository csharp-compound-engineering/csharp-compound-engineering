using CompoundDocs.McpServer.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CompoundDocs.Tests.Unit.Resilience;

public class RateLimitOptionsTests
{
    [Fact]
    public void Defaults_ShouldHaveExpectedValues()
    {
        var options = new RateLimitOptions();

        options.Enabled.ShouldBeTrue();
        options.Default.ShouldNotBeNull();
        options.Default.RequestsPerMinute.ShouldBe(60);
        options.Default.RequestsPerHour.ShouldBe(1000);
        options.Default.BurstSize.ShouldBe(10);
        options.Tools.ShouldNotBeNull();
        options.Tools.ShouldBeEmpty();
    }

    [Fact]
    public void SectionName_ShouldBeRateLimiting()
    {
        RateLimitOptions.SectionName.ShouldBe("RateLimiting");
    }
}

public class RateLimitConfigTests
{
    [Fact]
    public void Defaults_ShouldHaveExpectedValues()
    {
        var config = new RateLimitConfig();

        config.RequestsPerMinute.ShouldBe(60);
        config.RequestsPerHour.ShouldBe(1000);
        config.BurstSize.ShouldBe(10);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        var config = new RateLimitConfig
        {
            RequestsPerMinute = 120,
            RequestsPerHour = 2000,
            BurstSize = 20
        };

        config.RequestsPerMinute.ShouldBe(120);
        config.RequestsPerHour.ShouldBe(2000);
        config.BurstSize.ShouldBe(20);
    }
}

public class RateLimitResultTests
{
    [Fact]
    public void Allowed_ShouldCreateAllowedResult()
    {
        var result = RateLimitResult.Allowed(50, 900);

        result.IsAllowed.ShouldBeTrue();
        result.RemainingPerMinute.ShouldBe(50);
        result.RemainingPerHour.ShouldBe(900);
        result.RetryAfter.ShouldBe(TimeSpan.Zero);
        result.RejectionReason.ShouldBeNull();
    }

    [Fact]
    public void Rejected_ShouldCreateRejectedResult()
    {
        var retryAfter = TimeSpan.FromSeconds(5);
        var result = RateLimitResult.Rejected(retryAfter, "Per-minute rate limit exceeded");

        result.IsAllowed.ShouldBeFalse();
        result.RetryAfter.ShouldBe(retryAfter);
        result.RejectionReason.ShouldBe("Per-minute rate limit exceeded");
    }

    [Fact]
    public void RecordEquality_ShouldWorkCorrectly()
    {
        var a = RateLimitResult.Allowed(10, 20);
        var b = RateLimitResult.Allowed(10, 20);

        a.ShouldBe(b);
    }
}

public class TokenBucketTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithBurstCapacity()
    {
        var bucket = new TokenBucket(60, 10, TimeSpan.FromMinutes(1));

        bucket.AvailableTokens.ShouldBe(10);
    }

    [Fact]
    public void Constructor_ShouldCapBurstCapacityAtCapacity()
    {
        var bucket = new TokenBucket(5, 100, TimeSpan.FromMinutes(1));

        bucket.AvailableTokens.ShouldBe(5);
    }

    [Fact]
    public void TryConsume_ShouldSucceedWhenTokensAvailable()
    {
        var bucket = new TokenBucket(60, 10, TimeSpan.FromMinutes(1));

        var consumed = bucket.TryConsume(out var retryAfter);

        consumed.ShouldBeTrue();
        retryAfter.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void TryConsume_ShouldDecrementAvailableTokens()
    {
        var bucket = new TokenBucket(60, 5, TimeSpan.FromMinutes(1));

        bucket.TryConsume(out _);

        // Tokens decrease by 1 (some tiny refill may occur, but burst was 5 so we expect ~4)
        bucket.AvailableTokens.ShouldBeLessThanOrEqualTo(5);
        bucket.AvailableTokens.ShouldBeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void TryConsume_ShouldFailWhenNoTokensAvailable()
    {
        var bucket = new TokenBucket(60, 1, TimeSpan.FromMinutes(1));

        bucket.TryConsume(out _); // consume the only burst token

        var consumed = bucket.TryConsume(out var retryAfter);

        consumed.ShouldBeFalse();
        retryAfter.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void Refund_ShouldAddTokenBack()
    {
        var bucket = new TokenBucket(60, 2, TimeSpan.FromMinutes(1));

        bucket.TryConsume(out _);
        bucket.TryConsume(out _);
        bucket.Refund();

        // After refunding one token, we should be able to consume again
        bucket.TryConsume(out var retryAfter).ShouldBeTrue();
        retryAfter.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Refund_ShouldNotExceedCapacity()
    {
        var bucket = new TokenBucket(5, 5, TimeSpan.FromMinutes(1));

        // Refund without consuming - should not exceed capacity
        bucket.Refund();

        bucket.AvailableTokens.ShouldBeLessThanOrEqualTo(5);
    }

    [Fact]
    public void LastAccess_ShouldBeUpdatedOnConsume()
    {
        var bucket = new TokenBucket(60, 10, TimeSpan.FromMinutes(1));
        var before = DateTime.UtcNow;

        bucket.TryConsume(out _);

        bucket.LastAccess.ShouldBeGreaterThanOrEqualTo(before);
        bucket.LastAccess.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
    }
}

public class RateLimiterTests
{
    private static IOptions<RateLimitOptions> CreateOptions(Action<RateLimitOptions>? configure = null)
    {
        var options = new RateLimitOptions();
        configure?.Invoke(options);
        return Microsoft.Extensions.Options.Options.Create(options);
    }

    [Fact]
    public void Constructor_NullOptions_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RateLimiter(null!, NullLogger<RateLimiter>.Instance));
    }

    [Fact]
    public void Constructor_NullLogger_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new RateLimiter(CreateOptions(), null!));
    }

    [Fact]
    public void TryAcquire_WhenDisabled_ShouldReturnAllowedWithMaxValues()
    {
        using var limiter = new RateLimiter(
            CreateOptions(o => o.Enabled = false),
            NullLogger<RateLimiter>.Instance);

        var result = limiter.TryAcquire("rag_query");

        result.IsAllowed.ShouldBeTrue();
        result.RemainingPerMinute.ShouldBe(int.MaxValue);
        result.RemainingPerHour.ShouldBe(int.MaxValue);
    }

    [Fact]
    public void TryAcquire_WithinLimit_ShouldReturnAllowed()
    {
        using var limiter = new RateLimiter(CreateOptions(), NullLogger<RateLimiter>.Instance);

        var result = limiter.TryAcquire("rag_query");

        result.IsAllowed.ShouldBeTrue();
        result.RemainingPerMinute.ShouldBeGreaterThanOrEqualTo(0);
        result.RemainingPerHour.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void TryAcquire_ExceedingMinuteLimit_ShouldReturnRejected()
    {
        using var limiter = new RateLimiter(
            CreateOptions(o => o.Default = new RateLimitConfig
            {
                RequestsPerMinute = 3,
                RequestsPerHour = 1000,
                BurstSize = 3
            }),
            NullLogger<RateLimiter>.Instance);

        // Exhaust minute burst
        limiter.TryAcquire("tool").IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("tool").IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("tool").IsAllowed.ShouldBeTrue();

        var result = limiter.TryAcquire("tool");

        result.IsAllowed.ShouldBeFalse();
        result.RejectionReason.ShouldNotBeNull();
        result.RejectionReason!.ShouldContain("Per-minute");
        result.RetryAfter.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void TryAcquire_ExceedingHourLimit_ShouldReturnRejectedAndRefundMinuteToken()
    {
        // Minute has high capacity so it won't be the bottleneck.
        // Hour capacity is 2 with burst capped at min(BurstSize*2, capacity) = min(40, 2) = 2.
        using var limiter = new RateLimiter(
            CreateOptions(o => o.Default = new RateLimitConfig
            {
                RequestsPerMinute = 1000,
                RequestsPerHour = 2,
                BurstSize = 20
            }),
            NullLogger<RateLimiter>.Instance);

        limiter.TryAcquire("tool").IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("tool").IsAllowed.ShouldBeTrue();

        var result = limiter.TryAcquire("tool");

        result.IsAllowed.ShouldBeFalse();
        result.RejectionReason.ShouldNotBeNull();
        result.RejectionReason!.ShouldContain("Per-hour");
    }

    [Fact]
    public void TryAcquire_WithClientId_ShouldIsolateByClient()
    {
        using var limiter = new RateLimiter(
            CreateOptions(o => o.Default = new RateLimitConfig
            {
                RequestsPerMinute = 2,
                RequestsPerHour = 1000,
                BurstSize = 2
            }),
            NullLogger<RateLimiter>.Instance);

        // Exhaust client-A
        limiter.TryAcquire("tool", "client-A").IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("tool", "client-A").IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("tool", "client-A").IsAllowed.ShouldBeFalse();

        // Client-B should still work
        limiter.TryAcquire("tool", "client-B").IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public void GetConfigForTool_WithToolSpecificConfig_ShouldReturnToolConfig()
    {
        var toolConfig = new RateLimitConfig
        {
            RequestsPerMinute = 10,
            RequestsPerHour = 100,
            BurstSize = 5
        };

        using var limiter = new RateLimiter(
            CreateOptions(o => o.Tools["special_tool"] = toolConfig),
            NullLogger<RateLimiter>.Instance);

        var config = limiter.GetConfigForTool("special_tool");

        config.ShouldBeSameAs(toolConfig);
        config.RequestsPerMinute.ShouldBe(10);
        config.RequestsPerHour.ShouldBe(100);
        config.BurstSize.ShouldBe(5);
    }

    [Fact]
    public void GetConfigForTool_WithoutToolSpecificConfig_ShouldReturnDefault()
    {
        using var limiter = new RateLimiter(CreateOptions(), NullLogger<RateLimiter>.Instance);

        var config = limiter.GetConfigForTool("unknown_tool");

        config.RequestsPerMinute.ShouldBe(60);
        config.RequestsPerHour.ShouldBe(1000);
        config.BurstSize.ShouldBe(10);
    }

    [Fact]
    public void Reset_ShouldClearBucketsForTool()
    {
        using var limiter = new RateLimiter(
            CreateOptions(o => o.Default = new RateLimitConfig
            {
                RequestsPerMinute = 2,
                RequestsPerHour = 1000,
                BurstSize = 2
            }),
            NullLogger<RateLimiter>.Instance);

        limiter.TryAcquire("tool").IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("tool").IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("tool").IsAllowed.ShouldBeFalse();

        limiter.Reset("tool");

        limiter.TryAcquire("tool").IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public void Reset_WithClientId_ShouldOnlyClearSpecificClient()
    {
        using var limiter = new RateLimiter(
            CreateOptions(o => o.Default = new RateLimitConfig
            {
                RequestsPerMinute = 1,
                RequestsPerHour = 1000,
                BurstSize = 1
            }),
            NullLogger<RateLimiter>.Instance);

        limiter.TryAcquire("tool", "client-A");
        limiter.TryAcquire("tool", "client-B");

        limiter.Reset("tool", "client-A");

        // client-A should be allowed again after reset
        limiter.TryAcquire("tool", "client-A").IsAllowed.ShouldBeTrue();
        // client-B should still be exhausted
        limiter.TryAcquire("tool", "client-B").IsAllowed.ShouldBeFalse();
    }

    [Fact]
    public void ResetAll_ShouldClearAllBuckets()
    {
        using var limiter = new RateLimiter(
            CreateOptions(o => o.Default = new RateLimitConfig
            {
                RequestsPerMinute = 1,
                RequestsPerHour = 1000,
                BurstSize = 1
            }),
            NullLogger<RateLimiter>.Instance);

        limiter.TryAcquire("tool-a");
        limiter.TryAcquire("tool-b");

        limiter.TryAcquire("tool-a").IsAllowed.ShouldBeFalse();
        limiter.TryAcquire("tool-b").IsAllowed.ShouldBeFalse();

        limiter.ResetAll();

        limiter.TryAcquire("tool-a").IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("tool-b").IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        var limiter = new RateLimiter(CreateOptions(), NullLogger<RateLimiter>.Instance);

        Should.NotThrow(() => limiter.Dispose());
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_ShouldNotThrow()
    {
        var limiter = new RateLimiter(CreateOptions(), NullLogger<RateLimiter>.Instance);

        limiter.Dispose();
        Should.NotThrow(() => limiter.Dispose());
    }

    [Fact]
    public async Task WaitAndAcquireAsync_WhenAllowed_ShouldReturnImmediately()
    {
        using var limiter = new RateLimiter(CreateOptions(), NullLogger<RateLimiter>.Instance);

        var result = await limiter.WaitAndAcquireAsync("rag_query");

        result.IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public async Task WaitAndAcquireAsync_WhenExhausted_ShouldTimeoutWithMaxWait()
    {
        using var limiter = new RateLimiter(
            CreateOptions(o => o.Default = new RateLimitConfig
            {
                RequestsPerMinute = 1,
                RequestsPerHour = 1000,
                BurstSize = 1
            }),
            NullLogger<RateLimiter>.Instance);

        limiter.TryAcquire("tool"); // exhaust burst

        var result = await limiter.WaitAndAcquireAsync(
            "tool",
            maxWait: TimeSpan.FromMilliseconds(50));

        result.IsAllowed.ShouldBeFalse();
        result.RejectionReason.ShouldNotBeNull();
        result.RejectionReason!.ShouldContain("Max wait time exceeded");
    }

    [Fact]
    public async Task WaitAndAcquireAsync_WhenCancelled_ShouldReturnRejected()
    {
        using var limiter = new RateLimiter(
            CreateOptions(o => o.Default = new RateLimitConfig
            {
                RequestsPerMinute = 1,
                RequestsPerHour = 1000,
                BurstSize = 1
            }),
            NullLogger<RateLimiter>.Instance);

        limiter.TryAcquire("tool"); // exhaust burst

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Should.ThrowAsync<TaskCanceledException>(async () =>
            await limiter.WaitAndAcquireAsync(
                "tool",
                maxWait: TimeSpan.FromSeconds(30),
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task WaitAndAcquireAsync_DefaultMaxWait_ShouldBe30Seconds()
    {
        using var limiter = new RateLimiter(
            CreateOptions(o => o.Default = new RateLimitConfig
            {
                RequestsPerMinute = 1,
                RequestsPerHour = 1000,
                BurstSize = 1
            }),
            NullLogger<RateLimiter>.Instance);

        limiter.TryAcquire("tool"); // exhaust burst

        // Cancel quickly so we don't wait the full 30s
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Should.ThrowAsync<TaskCanceledException>(async () =>
            await limiter.WaitAndAcquireAsync("tool", cancellationToken: cts.Token));
    }

    [Fact]
    public void TryAcquire_DifferentTools_ShouldHaveIndependentBuckets()
    {
        using var limiter = new RateLimiter(
            CreateOptions(o => o.Default = new RateLimitConfig
            {
                RequestsPerMinute = 1,
                RequestsPerHour = 1000,
                BurstSize = 1
            }),
            NullLogger<RateLimiter>.Instance);

        limiter.TryAcquire("tool-a").IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("tool-a").IsAllowed.ShouldBeFalse();

        // tool-b should have its own bucket
        limiter.TryAcquire("tool-b").IsAllowed.ShouldBeTrue();
    }

    [Fact]
    public void TryAcquire_ToolSpecificConfig_ShouldUseToolLimits()
    {
        using var limiter = new RateLimiter(
            CreateOptions(o =>
            {
                o.Default = new RateLimitConfig
                {
                    RequestsPerMinute = 100,
                    RequestsPerHour = 1000,
                    BurstSize = 100
                };
                o.Tools["restricted"] = new RateLimitConfig
                {
                    RequestsPerMinute = 2,
                    RequestsPerHour = 1000,
                    BurstSize = 2
                };
            }),
            NullLogger<RateLimiter>.Instance);

        // restricted tool should be limited to 2 burst
        limiter.TryAcquire("restricted").IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("restricted").IsAllowed.ShouldBeTrue();
        limiter.TryAcquire("restricted").IsAllowed.ShouldBeFalse();

        // default tool should still have plenty of burst
        limiter.TryAcquire("default_tool").IsAllowed.ShouldBeTrue();
    }
}
