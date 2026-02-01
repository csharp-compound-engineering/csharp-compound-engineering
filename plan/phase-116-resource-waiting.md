# Phase 116: Aspire Resource Waiting Patterns

> **Status**: NOT_STARTED
> **Effort Estimate**: 2-3 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 115 (Aspire Container Resources)

---

## Spec References

This phase implements resource waiting patterns defined in:

- **spec/testing/aspire-fixtures.md** - Resource Waiting Patterns section

---

## Objectives

1. Implement `WaitForResourceHealthyAsync` for database and service readiness
2. Differentiate between `Running` state and actual health/readiness
3. Configure appropriate timeouts for PostgreSQL and Ollama containers
4. Implement health check polling patterns for custom readiness verification
5. Handle timeout and resource failure scenarios gracefully

---

## Acceptance Criteria

### WaitForResourceHealthyAsync vs WaitForResourceAsync

- [ ] Tests use `WaitForResourceHealthyAsync` for PostgreSQL (not just `WaitForResourceAsync`)
- [ ] Tests use `WaitForResourceHealthyAsync` for Ollama service
- [ ] Code comments document the difference between `Running` and `Healthy` states
- [ ] Integration fixture demonstrates correct usage pattern

### Timeout Configuration

- [ ] PostgreSQL wait timeout set to 1 minute (container start + init scripts)
- [ ] Ollama wait timeout set to 1 minute (container start only)
- [ ] Overall fixture initialization timeout set to 5 minutes
- [ ] `CancellationTokenSource` properly configured with timeouts

### Health Check Polling

- [ ] Custom health check polling helper implemented for edge cases
- [ ] `WaitForConditionAsync` helper supports configurable poll intervals
- [ ] Health checks verify actual service functionality (not just port availability)
- [ ] Exponential backoff pattern available for retry scenarios

### Timeout Handling

- [ ] `OperationCanceledException` caught and wrapped with descriptive message
- [ ] Timeout errors include resource name and elapsed time
- [ ] Test output includes diagnostic information on timeout failures
- [ ] Cleanup runs even when initialization times out

### Resource Failure Detection

- [ ] Tests detect resource startup failures (not just timeouts)
- [ ] Container exit codes are captured on failure
- [ ] Container logs are surfaced in test output on failure
- [ ] Fixture provides `IsResourceHealthy()` method for manual checks

---

## Implementation Notes

### Resource State Transitions

Understanding resource states is critical for reliable test fixtures:

```
Created -> Starting -> Running -> Healthy (or FailedToStart)
```

| State | Meaning |
|-------|---------|
| `Created` | Resource definition registered |
| `Starting` | Container image pulling / process launching |
| `Running` | Process started, but may not be ready for connections |
| `Healthy` | Health checks pass, ready to serve requests |
| `FailedToStart` | Resource could not start successfully |

### WaitForResourceHealthyAsync Pattern

```csharp
public async Task InitializeAsync()
{
    // Build and start the Aspire application
    _app = await appHost.BuildAsync();
    await _app.StartAsync();

    // Create a single cancellation token for all waits
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

    try
    {
        // Wait for PostgreSQL to be HEALTHY (not just running)
        // Health = accepting connections + init scripts complete
        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("postgres", cts.Token);

        // Wait for Ollama to be HEALTHY
        // Health = HTTP endpoint responding
        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("ollama", cts.Token);
    }
    catch (OperationCanceledException)
    {
        throw new TimeoutException(
            $"Timed out waiting for resources after {cts.Token} " +
            "Check container logs for startup failures.");
    }
}
```

### Why WaitForResourceHealthyAsync Matters

**Incorrect** - Race condition with database:
```csharp
// BAD: Database may not be ready for connections
await _app.ResourceNotifications
    .WaitForResourceAsync("postgres", KnownResourceStates.Running, cts.Token);

// This connection attempt may fail!
var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync(); // Could throw: "server is starting up"
```

**Correct** - Wait for actual readiness:
```csharp
// GOOD: Database is ready to accept connections
await _app.ResourceNotifications
    .WaitForResourceHealthyAsync("postgres", cts.Token);

// Connection will succeed
var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync(); // Works reliably
```

### Timeout Recommendations Table

| Resource | Recommended Timeout | Rationale |
|----------|---------------------|-----------|
| PostgreSQL | 1 minute | Container pull + start + init scripts + pgvector extension |
| Ollama | 1 minute | Container pull + start (model downloads are lazy) |
| MCP Server | 30 seconds | Process start + initialization |
| Overall Fixture | 5 minutes | Sum of all resources + buffer |

**Note on Ollama model downloads**: Model downloads occur lazily when tests first invoke the embedding API, not during container startup. The 1-minute timeout is for container startup only. Tests that use embeddings may take additional time on first run.

### Health Check Polling Helper

For scenarios where Aspire's built-in health checks are insufficient:

```csharp
public static class ResourceWaitHelpers
{
    /// <summary>
    /// Polls a condition until it returns true or timeout expires.
    /// </summary>
    public static async Task<bool> WaitForConditionAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        TimeSpan pollInterval,
        CancellationToken cancellationToken = default)
    {
        using var linkedCts = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(timeout);

        try
        {
            while (!linkedCts.Token.IsCancellationRequested)
            {
                if (await condition())
                    return true;

                await Task.Delay(pollInterval, linkedCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout or external cancellation
        }

        return false;
    }

    /// <summary>
    /// Waits for an HTTP endpoint to respond with success status.
    /// </summary>
    public static async Task WaitForHttpEndpointAsync(
        string url,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        var success = await WaitForConditionAsync(
            async () =>
            {
                try
                {
                    var response = await httpClient.GetAsync(url, cancellationToken);
                    return response.IsSuccessStatusCode;
                }
                catch (HttpRequestException)
                {
                    return false;
                }
            },
            timeout,
            TimeSpan.FromSeconds(1),
            cancellationToken);

        if (!success)
        {
            throw new TimeoutException($"HTTP endpoint {url} did not become available within {timeout}");
        }
    }

    /// <summary>
    /// Waits for PostgreSQL to accept connections.
    /// </summary>
    public static async Task WaitForPostgresAsync(
        string connectionString,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var success = await WaitForConditionAsync(
            async () =>
            {
                try
                {
                    await using var conn = new NpgsqlConnection(connectionString);
                    await conn.OpenAsync(cancellationToken);
                    return true;
                }
                catch (NpgsqlException)
                {
                    return false;
                }
            },
            timeout,
            TimeSpan.FromSeconds(1),
            cancellationToken);

        if (!success)
        {
            throw new TimeoutException($"PostgreSQL did not become available within {timeout}");
        }
    }
}
```

### Timeout Error Handling

Provide actionable error messages when waits fail:

```csharp
public async Task InitializeAsync()
{
    var startTime = DateTime.UtcNow;

    try
    {
        await WaitForResourcesAsync();
    }
    catch (OperationCanceledException ex)
    {
        var elapsed = DateTime.UtcNow - startTime;
        var diagnostics = await GatherResourceDiagnosticsAsync();

        throw new InvalidOperationException(
            $"Test fixture initialization timed out after {elapsed.TotalSeconds:F1}s. " +
            $"Resource diagnostics:\n{diagnostics}", ex);
    }
}

private async Task<string> GatherResourceDiagnosticsAsync()
{
    var sb = new StringBuilder();

    foreach (var resource in _app!.Resources)
    {
        var state = await GetResourceStateAsync(resource);
        sb.AppendLine($"  {resource.Name}: {state}");

        if (resource is ContainerResource container)
        {
            // Note: Actual log retrieval depends on Aspire version
            sb.AppendLine($"    Container ID: {container.GetContainerId()}");
        }
    }

    return sb.ToString();
}
```

### Resource Failure Detection

Detect when resources fail to start (distinct from timeout):

```csharp
public class AspireIntegrationFixture : IAsyncLifetime
{
    private readonly List<string> _failedResources = new();

    public async Task InitializeAsync()
    {
        _app = await appHost.BuildAsync();

        // Subscribe to resource state changes
        _app.ResourceNotifications.ResourceUpdated += (sender, args) =>
        {
            if (args.ResourceSnapshot.State == KnownResourceStates.FailedToStart)
            {
                _failedResources.Add(args.ResourceSnapshot.Name);
            }
        };

        await _app.StartAsync();

        // Check for immediate failures
        if (_failedResources.Any())
        {
            throw new InvalidOperationException(
                $"The following resources failed to start: {string.Join(", ", _failedResources)}");
        }

        // Then wait for healthy state
        await WaitForResourcesHealthyAsync();
    }

    /// <summary>
    /// Checks if a specific resource is currently healthy.
    /// </summary>
    public async Task<bool> IsResourceHealthyAsync(string resourceName)
    {
        var resource = _app!.Resources.Single(r => r.Name == resourceName);
        // Implementation depends on Aspire version - may need to track state internally
        return !_failedResources.Contains(resourceName);
    }
}
```

### Cancellation Token Patterns

Proper cancellation token usage in test fixtures:

```csharp
public async Task InitializeAsync()
{
    // Outer timeout for entire initialization
    using var initCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

    try
    {
        await _app.StartAsync(initCts.Token);

        // Individual resource timeouts (shorter, more specific)
        using var postgresCts = CancellationTokenSource
            .CreateLinkedTokenSource(initCts.Token);
        postgresCts.CancelAfter(TimeSpan.FromMinutes(1));

        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("postgres", postgresCts.Token);

        using var ollamaCts = CancellationTokenSource
            .CreateLinkedTokenSource(initCts.Token);
        ollamaCts.CancelAfter(TimeSpan.FromMinutes(1));

        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("ollama", ollamaCts.Token);
    }
    catch (OperationCanceledException) when (initCts.IsCancellationRequested)
    {
        throw new TimeoutException("Overall fixture initialization timed out");
    }
    catch (OperationCanceledException ex)
    {
        // Individual resource timeout - identify which one
        throw new TimeoutException($"Resource wait timed out: {ex.Message}", ex);
    }
}
```

### Retry Pattern with Exponential Backoff

For flaky resource initialization scenarios:

```csharp
public static class RetryHelpers
{
    public static async Task<T> RetryWithBackoffAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        TimeSpan? initialDelay = null,
        CancellationToken cancellationToken = default)
    {
        var delay = initialDelay ?? TimeSpan.FromSeconds(1);
        Exception? lastException = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastException = ex;
                await Task.Delay(delay, cancellationToken);
                delay *= 2; // Exponential backoff
            }
        }

        throw new InvalidOperationException(
            $"Operation failed after {maxAttempts} attempts", lastException);
    }
}
```

---

## Dependencies

### Depends On
- Phase 115: Aspire Container Resources (PostgreSQL and Ollama container definitions)

### Blocks
- Phase 117+: Tests requiring Aspire fixture initialization
- E2E test phases that depend on healthy resources

---

## Verification Steps

After completing this phase, verify:

1. **Health wait compiles**: `WaitForResourceHealthyAsync` calls compile without errors
2. **Timeout configuration**: Timeouts match specification (1 min each, 5 min overall)
3. **Error messages**: Timeout failures include resource name and diagnostic info
4. **Helper methods**: `WaitForConditionAsync` and related helpers function correctly
5. **Cancellation**: Cancellation tokens properly linked and respected

### Manual Verification

```bash
# Run integration tests with verbose output
dotnet test tests/CompoundDocs.IntegrationTests/ \
    --logger "console;verbosity=detailed" \
    --filter "Category=Integration"

# Verify timeout behavior by stopping Docker before test
docker stop $(docker ps -q)
dotnet test tests/CompoundDocs.IntegrationTests/ \
    --filter "FullyQualifiedName~AspireFixture"
# Expected: TimeoutException with resource diagnostics
```

### Unit Test Verification

```csharp
[Fact]
public async Task WaitForConditionAsync_ReturnsTrueWhenConditionMet()
{
    // Arrange
    var callCount = 0;

    // Act
    var result = await ResourceWaitHelpers.WaitForConditionAsync(
        async () =>
        {
            callCount++;
            return callCount >= 3;
        },
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMilliseconds(100));

    // Assert
    result.ShouldBeTrue();
    callCount.ShouldBe(3);
}

[Fact]
public async Task WaitForConditionAsync_ReturnsFalseOnTimeout()
{
    // Arrange & Act
    var result = await ResourceWaitHelpers.WaitForConditionAsync(
        async () => false, // Never succeeds
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromMilliseconds(100));

    // Assert
    result.ShouldBeFalse();
}

[Fact]
public async Task WaitForConditionAsync_RespectsCancellation()
{
    // Arrange
    using var cts = new CancellationTokenSource();
    cts.CancelAfter(TimeSpan.FromMilliseconds(100));

    // Act & Assert
    var result = await ResourceWaitHelpers.WaitForConditionAsync(
        async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            return true;
        },
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMilliseconds(50),
        cts.Token);

    result.ShouldBeFalse();
}
```

### Integration Test Verification

```csharp
[Collection("Aspire")]
public class ResourceWaitingTests
{
    private readonly AspireIntegrationFixture _fixture;

    public ResourceWaitingTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void PostgresConnectionString_IsAvailable_AfterFixtureInit()
    {
        // Assert - fixture waited for healthy state
        _fixture.PostgresConnectionString.ShouldNotBeNullOrEmpty();
        _fixture.PostgresConnectionString.ShouldContain("Host=");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void OllamaEndpoint_IsAvailable_AfterFixtureInit()
    {
        // Assert
        _fixture.OllamaEndpoint.ShouldNotBeNullOrEmpty();
        _fixture.OllamaEndpoint.ShouldStartWith("http");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Postgres_AcceptsConnections_AfterHealthyWait()
    {
        // Arrange & Act
        await using var connection = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await connection.OpenAsync();

        // Assert - connection successful proves healthy wait worked
        connection.State.ShouldBe(ConnectionState.Open);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Ollama_RespondsToHealthCheck_AfterHealthyWait()
    {
        // Arrange
        using var httpClient = new HttpClient();

        // Act
        var response = await httpClient.GetAsync($"{_fixture.OllamaEndpoint}/api/tags");

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue();
    }
}
```

---

## Notes

- The distinction between `Running` and `Healthy` is critical - many test failures stem from connecting to services that are running but not yet ready
- Model downloads in Ollama happen lazily; the fixture timeout only covers container startup
- Always use linked cancellation tokens to ensure cleanup can proceed even when individual waits time out
- Consider implementing a health check dashboard or logging for CI/CD debugging
- The retry helper with exponential backoff should be used sparingly - prefer longer initial timeouts over aggressive retries
