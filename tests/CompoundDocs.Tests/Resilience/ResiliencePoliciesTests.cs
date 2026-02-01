using CompoundDocs.McpServer.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace CompoundDocs.Tests.Resilience;

/// <summary>
/// Unit tests for ResiliencePolicies.
/// </summary>
public sealed class ResiliencePoliciesTests
{
    private readonly IOptions<ResilienceOptions> _defaultOptions;
    private readonly ILogger<ResiliencePolicies> _logger;

    public ResiliencePoliciesTests()
    {
        _defaultOptions = Options.Create(new ResilienceOptions());
        _logger = NullLogger<ResiliencePolicies>.Instance;
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var policies = new ResiliencePolicies(_defaultOptions, _logger);

        // Assert
        policies.ShouldNotBeNull();
        policies.OllamaPipeline.ShouldNotBeNull();
        policies.DatabasePipeline.ShouldNotBeNull();
        policies.DefaultPipeline.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ResiliencePolicies(null!, _logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => new ResiliencePolicies(_defaultOptions, null!));
    }

    [Fact]
    public async Task ExecuteWithOllamaResilienceAsync_SuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var policies = new ResiliencePolicies(_defaultOptions, _logger);
        var expectedResult = "success";

        // Act
        var result = await policies.ExecuteWithOllamaResilienceAsync(
            ct => Task.FromResult(expectedResult));

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public async Task ExecuteWithOllamaResilienceAsync_TransientFailure_RetriesAndSucceeds()
    {
        // Arrange
        var options = Options.Create(new ResilienceOptions
        {
            Retry = new RetryOptions
            {
                MaxRetryAttempts = 3,
                InitialDelayMs = 10,
                MaxDelayMs = 50,
                UseJitter = false
            }
        });
        var policies = new ResiliencePolicies(options, _logger);
        var attemptCount = 0;

        // Act
        var result = await policies.ExecuteWithOllamaResilienceAsync(ct =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                throw new HttpRequestException("Transient failure");
            }
            return Task.FromResult("success");
        });

        // Assert
        result.ShouldBe("success");
        attemptCount.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteWithDatabaseResilienceAsync_SuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var policies = new ResiliencePolicies(_defaultOptions, _logger);
        var expectedResult = 42;

        // Act
        var result = await policies.ExecuteWithDatabaseResilienceAsync(
            ct => Task.FromResult(expectedResult));

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public async Task ExecuteWithDefaultResilienceAsync_SuccessfulOperation_ReturnsResult()
    {
        // Arrange
        var policies = new ResiliencePolicies(_defaultOptions, _logger);
        var expectedResult = new { Name = "test" };

        // Act
        var result = await policies.ExecuteWithDefaultResilienceAsync(
            ct => Task.FromResult(expectedResult));

        // Assert
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public async Task ExecuteWithOllamaResilienceAsync_Timeout_ThrowsTimeoutRejectedException()
    {
        // Arrange
        var options = Options.Create(new ResilienceOptions
        {
            Timeout = new TimeoutOptions { EmbeddingTimeoutSeconds = 1 },
            Retry = new RetryOptions
            {
                MaxRetryAttempts = 1,
                InitialDelayMs = 10,
                MaxDelayMs = 50,
                UseJitter = false
            }
        });
        var policies = new ResiliencePolicies(options, _logger);

        // Act & Assert
        await Should.ThrowAsync<TimeoutRejectedException>(async () =>
        {
            await policies.ExecuteWithOllamaResilienceAsync(async ct =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return "should not reach here";
            });
        });
    }

    [Fact]
    public async Task ExecuteWithOllamaResilienceAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        var policies = new ResiliencePolicies(_defaultOptions, _logger);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await policies.ExecuteWithOllamaResilienceAsync(
                ct => Task.FromResult("test"),
                cts.Token);
        });
    }

    [Fact]
    public async Task ExecuteWithDatabaseResilienceAsync_NpgsqlException_Retries()
    {
        // Arrange
        var options = Options.Create(new ResilienceOptions
        {
            Retry = new RetryOptions
            {
                MaxRetryAttempts = 2,
                InitialDelayMs = 10,
                MaxDelayMs = 50,
                UseJitter = false
            }
        });
        var policies = new ResiliencePolicies(options, _logger);
        var attemptCount = 0;

        // Act
        var result = await policies.ExecuteWithDatabaseResilienceAsync(ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new Npgsql.NpgsqlException("Connection failed");
            }
            return Task.FromResult("success");
        });

        // Assert
        result.ShouldBe("success");
        attemptCount.ShouldBe(2);
    }

    [Fact]
    public void OllamaPipeline_ShouldBeSameInstance_OnMultipleAccesses()
    {
        // Arrange
        var policies = new ResiliencePolicies(_defaultOptions, _logger);

        // Act
        var pipeline1 = policies.OllamaPipeline;
        var pipeline2 = policies.OllamaPipeline;

        // Assert
        pipeline1.ShouldBeSameAs(pipeline2);
    }

    [Fact]
    public void DatabasePipeline_ShouldBeSameInstance_OnMultipleAccesses()
    {
        // Arrange
        var policies = new ResiliencePolicies(_defaultOptions, _logger);

        // Act
        var pipeline1 = policies.DatabasePipeline;
        var pipeline2 = policies.DatabasePipeline;

        // Assert
        pipeline1.ShouldBeSameAs(pipeline2);
    }

    [Fact]
    public async Task ExecuteWithOllamaResilienceAsync_MaxRetriesExceeded_ThrowsOriginalException()
    {
        // Arrange
        var options = Options.Create(new ResilienceOptions
        {
            Retry = new RetryOptions
            {
                MaxRetryAttempts = 2,
                InitialDelayMs = 10,
                MaxDelayMs = 50,
                UseJitter = false
            },
            CircuitBreaker = new CircuitBreakerOptions
            {
                MinimumThroughput = 100 // High threshold to avoid circuit breaking in this test
            }
        });
        var policies = new ResiliencePolicies(options, _logger);
        var attemptCount = 0;

        // Act & Assert
        var exception = await Should.ThrowAsync<HttpRequestException>(async () =>
        {
            await policies.ExecuteWithOllamaResilienceAsync<string>(async ct =>
            {
                attemptCount++;
                throw new HttpRequestException("Persistent failure");
#pragma warning disable CS0162 // Unreachable code detected
                return await Task.FromResult("never reached");
#pragma warning restore CS0162
            });
        });

        exception.Message.ShouldBe("Persistent failure");
        attemptCount.ShouldBe(3); // Initial attempt + 2 retries
    }
}
