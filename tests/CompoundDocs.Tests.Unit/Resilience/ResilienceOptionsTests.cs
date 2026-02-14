using CompoundDocs.McpServer.Resilience;
using Shouldly;

namespace CompoundDocs.Tests.Unit.Resilience;

public sealed class ResilienceOptionsTests
{
    // ── ResilienceOptions ──────────────────────────────────────────────

    [Fact]
    public void SectionName_IsResilience()
    {
        // Arrange & Act & Assert
        ResilienceOptions.SectionName.ShouldBe("Resilience");
    }

    [Fact]
    public void Default_Retry_IsNotNull()
    {
        // Arrange
        var sut = new ResilienceOptions();

        // Act
        var result = sut.Retry;

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Default_CircuitBreaker_IsNotNull()
    {
        // Arrange
        var sut = new ResilienceOptions();

        // Act
        var result = sut.CircuitBreaker;

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public void Default_Timeout_IsNotNull()
    {
        // Arrange
        var sut = new ResilienceOptions();

        // Act
        var result = sut.Timeout;

        // Assert
        result.ShouldNotBeNull();
    }

    // ── RetryOptions Defaults ──────────────────────────────────────────

    [Fact]
    public void Default_MaxRetryAttempts()
    {
        // Arrange
        var sut = new RetryOptions();

        // Act & Assert
        sut.MaxRetryAttempts.ShouldBe(3);
    }

    [Fact]
    public void Default_InitialDelayMs()
    {
        // Arrange
        var sut = new RetryOptions();

        // Act & Assert
        sut.InitialDelayMs.ShouldBe(200);
    }

    [Fact]
    public void Default_MaxDelayMs()
    {
        // Arrange
        var sut = new RetryOptions();

        // Act & Assert
        sut.MaxDelayMs.ShouldBe(5000);
    }

    [Fact]
    public void Default_BackoffMultiplier()
    {
        // Arrange
        var sut = new RetryOptions();

        // Act & Assert
        sut.BackoffMultiplier.ShouldBe(2.0);
    }

    [Fact]
    public void Default_UseJitter()
    {
        // Arrange
        var sut = new RetryOptions();

        // Act & Assert
        sut.UseJitter.ShouldBeTrue();
    }

    // ── CircuitBreakerOptions Defaults ─────────────────────────────────

    [Fact]
    public void Default_FailureRatioThreshold()
    {
        // Arrange
        var sut = new CircuitBreakerOptions();

        // Act & Assert
        sut.FailureRatioThreshold.ShouldBe(0.5);
    }

    [Fact]
    public void Default_MinimumThroughput()
    {
        // Arrange
        var sut = new CircuitBreakerOptions();

        // Act & Assert
        sut.MinimumThroughput.ShouldBe(10);
    }

    [Fact]
    public void Default_SamplingDurationSeconds()
    {
        // Arrange
        var sut = new CircuitBreakerOptions();

        // Act & Assert
        sut.SamplingDurationSeconds.ShouldBe(30);
    }

    [Fact]
    public void Default_BreakDurationSeconds()
    {
        // Arrange
        var sut = new CircuitBreakerOptions();

        // Act & Assert
        sut.BreakDurationSeconds.ShouldBe(30);
    }

    // ── TimeoutOptions Defaults ────────────────────────────────────────

    [Fact]
    public void Default_DefaultTimeoutSeconds()
    {
        // Arrange
        var sut = new TimeoutOptions();

        // Act & Assert
        sut.DefaultTimeoutSeconds.ShouldBe(30);
    }

    [Fact]
    public void Default_EmbeddingTimeoutSeconds()
    {
        // Arrange
        var sut = new TimeoutOptions();

        // Act & Assert
        sut.EmbeddingTimeoutSeconds.ShouldBe(60);
    }

    [Fact]
    public void Default_DatabaseTimeoutSeconds()
    {
        // Arrange
        var sut = new TimeoutOptions();

        // Act & Assert
        sut.DatabaseTimeoutSeconds.ShouldBe(15);
    }

    // ── Setter Override Tests ──────────────────────────────────────────

    [Fact]
    public void RetryOptions_CanOverrideDefaults()
    {
        // Arrange
        var sut = new RetryOptions();

        // Act
        sut.MaxRetryAttempts = 5;

        // Assert
        sut.MaxRetryAttempts.ShouldBe(5);
    }

    [Fact]
    public void CircuitBreakerOptions_CanOverrideDefaults()
    {
        // Arrange
        var sut = new CircuitBreakerOptions();

        // Act
        sut.FailureRatioThreshold = 0.8;

        // Assert
        sut.FailureRatioThreshold.ShouldBe(0.8);
    }
}
