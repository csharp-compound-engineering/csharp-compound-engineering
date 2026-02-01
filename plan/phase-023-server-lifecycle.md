# Phase 023: MCP Server Lifecycle Management

> **Status**: [PLANNED]
> **Category**: MCP Server Core
> **Estimated Effort**: M
> **Prerequisites**: Phase 022

---

## Spec References

- [mcp-server.md](../spec/mcp-server.md) - Server overview and core responsibilities
- [research/dotnet-generic-host-mcp-research.md](../research/dotnet-generic-host-mcp-research.md) - Host lifecycle patterns
- [research/hosted-services-background-tasks.md](../research/hosted-services-background-tasks.md) - IHostedService and BackgroundService patterns

---

## Objectives

1. Implement `IHostedLifecycleService` for the MCP server with proper startup/shutdown sequencing
2. Create graceful startup sequence with dependency validation
3. Implement graceful shutdown handling with configurable timeout
4. Establish CancellationToken propagation pattern across all services
5. Build health state tracking with observable health status
6. Integrate with .NET Generic Host lifecycle events

---

## Acceptance Criteria

- [ ] `McpServerHostedService` implements `IHostedLifecycleService` for fine-grained lifecycle control
- [ ] Startup sequence validates Ollama connectivity before accepting MCP requests
- [ ] Startup sequence validates PostgreSQL connectivity before accepting MCP requests
- [ ] Shutdown timeout is configurable via `HostOptions.ShutdownTimeout` (default: 30 seconds)
- [ ] All async operations accept and honor `CancellationToken`
- [ ] `IServerHealthTracker` interface provides observable health state
- [ ] Health state includes: Starting, Ready, Degraded, Stopping, Stopped
- [ ] `StartingAsync` validates prerequisites before `StartAsync`
- [ ] `StoppingAsync` signals services to stop accepting new work
- [ ] `StoppedAsync` confirms all cleanup completed
- [ ] File watcher shutdown waits for pending operations with timeout
- [ ] Unhandled exceptions in background services trigger graceful shutdown
- [ ] All logs directed to stderr (stdout reserved for MCP protocol)

---

## Implementation Notes

### 1. Server Health State Enum

```csharp
/// <summary>
/// Represents the health state of the MCP server.
/// </summary>
public enum ServerHealthState
{
    /// <summary>Server is initializing and validating dependencies.</summary>
    Starting,

    /// <summary>Server is fully operational and accepting requests.</summary>
    Ready,

    /// <summary>Server is operational but some dependencies are unavailable.</summary>
    Degraded,

    /// <summary>Server is shutting down and no longer accepting new requests.</summary>
    Stopping,

    /// <summary>Server has completely stopped.</summary>
    Stopped
}
```

### 2. IServerHealthTracker Interface

```csharp
/// <summary>
/// Tracks and exposes the health state of the MCP server.
/// </summary>
public interface IServerHealthTracker
{
    /// <summary>Gets the current health state.</summary>
    ServerHealthState CurrentState { get; }

    /// <summary>Gets detailed health information.</summary>
    ServerHealthInfo GetHealthInfo();

    /// <summary>Event raised when health state changes.</summary>
    event EventHandler<ServerHealthStateChangedEventArgs>? StateChanged;

    /// <summary>Signals the server to transition to Ready state.</summary>
    void MarkReady();

    /// <summary>Signals the server to transition to Degraded state with reason.</summary>
    void MarkDegraded(string reason);

    /// <summary>Signals the server is stopping.</summary>
    void MarkStopping();

    /// <summary>Signals the server has stopped.</summary>
    void MarkStopped();
}

public record ServerHealthInfo(
    ServerHealthState State,
    DateTimeOffset LastStateChange,
    bool OllamaConnected,
    bool PostgresConnected,
    string? DegradedReason,
    int ActiveOperations);

public class ServerHealthStateChangedEventArgs : EventArgs
{
    public required ServerHealthState PreviousState { get; init; }
    public required ServerHealthState NewState { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public string? Reason { get; init; }
}
```

### 3. ServerHealthTracker Implementation

```csharp
/// <summary>
/// Thread-safe implementation of server health tracking.
/// </summary>
public sealed class ServerHealthTracker : IServerHealthTracker
{
    private readonly ILogger<ServerHealthTracker> _logger;
    private readonly object _lock = new();

    private ServerHealthState _currentState = ServerHealthState.Starting;
    private DateTimeOffset _lastStateChange = DateTimeOffset.UtcNow;
    private string? _degradedReason;
    private volatile int _activeOperations;

    // Dependency health (updated by health checks)
    private volatile bool _ollamaConnected;
    private volatile bool _postgresConnected;

    public event EventHandler<ServerHealthStateChangedEventArgs>? StateChanged;

    public ServerHealthTracker(ILogger<ServerHealthTracker> logger)
    {
        _logger = logger;
    }

    public ServerHealthState CurrentState
    {
        get
        {
            lock (_lock)
            {
                return _currentState;
            }
        }
    }

    public ServerHealthInfo GetHealthInfo()
    {
        lock (_lock)
        {
            return new ServerHealthInfo(
                _currentState,
                _lastStateChange,
                _ollamaConnected,
                _postgresConnected,
                _degradedReason,
                _activeOperations);
        }
    }

    public void MarkReady()
    {
        TransitionTo(ServerHealthState.Ready, "All dependencies validated");
    }

    public void MarkDegraded(string reason)
    {
        lock (_lock)
        {
            _degradedReason = reason;
        }
        TransitionTo(ServerHealthState.Degraded, reason);
    }

    public void MarkStopping()
    {
        TransitionTo(ServerHealthState.Stopping, "Shutdown initiated");
    }

    public void MarkStopped()
    {
        TransitionTo(ServerHealthState.Stopped, "Shutdown complete");
    }

    public void UpdateDependencyHealth(bool ollamaConnected, bool postgresConnected)
    {
        _ollamaConnected = ollamaConnected;
        _postgresConnected = postgresConnected;

        // Auto-transition to degraded if dependencies fail while ready
        if (_currentState == ServerHealthState.Ready)
        {
            if (!ollamaConnected || !postgresConnected)
            {
                var reason = !ollamaConnected ? "Ollama disconnected" : "PostgreSQL disconnected";
                MarkDegraded(reason);
            }
        }
        else if (_currentState == ServerHealthState.Degraded)
        {
            if (ollamaConnected && postgresConnected)
            {
                MarkReady();
            }
        }
    }

    public void IncrementActiveOperations() => Interlocked.Increment(ref _activeOperations);
    public void DecrementActiveOperations() => Interlocked.Decrement(ref _activeOperations);
    public int ActiveOperations => _activeOperations;

    private void TransitionTo(ServerHealthState newState, string reason)
    {
        ServerHealthStateChangedEventArgs? args = null;

        lock (_lock)
        {
            if (_currentState == newState)
                return;

            var previousState = _currentState;
            _currentState = newState;
            _lastStateChange = DateTimeOffset.UtcNow;

            args = new ServerHealthStateChangedEventArgs
            {
                PreviousState = previousState,
                NewState = newState,
                Timestamp = _lastStateChange,
                Reason = reason
            };
        }

        _logger.LogInformation(
            "Server health state changed: {PreviousState} -> {NewState}. Reason: {Reason}",
            args.PreviousState,
            args.NewState,
            reason);

        StateChanged?.Invoke(this, args);
    }
}
```

### 4. McpServerHostedService Implementation

```csharp
/// <summary>
/// Hosted service that manages the MCP server lifecycle.
/// Implements IHostedLifecycleService for fine-grained startup/shutdown control.
/// </summary>
public sealed class McpServerHostedService : IHostedLifecycleService
{
    private readonly ILogger<McpServerHostedService> _logger;
    private readonly IServerHealthTracker _healthTracker;
    private readonly IOllamaHealthCheck _ollamaHealth;
    private readonly IPostgresHealthCheck _postgresHealth;
    private readonly IHostApplicationLifetime _appLifetime;
    private readonly IOptions<ServerLifecycleOptions> _options;

    private CancellationTokenSource? _stoppingCts;

    public McpServerHostedService(
        ILogger<McpServerHostedService> logger,
        IServerHealthTracker healthTracker,
        IOllamaHealthCheck ollamaHealth,
        IPostgresHealthCheck postgresHealth,
        IHostApplicationLifetime appLifetime,
        IOptions<ServerLifecycleOptions> options)
    {
        _logger = logger;
        _healthTracker = healthTracker;
        _ollamaHealth = ollamaHealth;
        _postgresHealth = postgresHealth;
        _appLifetime = appLifetime;
        _options = options;
    }

    /// <summary>
    /// Called before any hosted service's StartAsync.
    /// Validates prerequisites before the server can start.
    /// </summary>
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MCP Server starting - validating dependencies...");

        // Validate Ollama connectivity
        var ollamaReady = await ValidateOllamaAsync(cancellationToken);
        if (!ollamaReady && _options.Value.RequireOllamaOnStartup)
        {
            throw new InvalidOperationException(
                "Ollama is not available. Ensure Ollama is running and accessible.");
        }

        // Validate PostgreSQL connectivity
        var postgresReady = await ValidatePostgresAsync(cancellationToken);
        if (!postgresReady && _options.Value.RequirePostgresOnStartup)
        {
            throw new InvalidOperationException(
                "PostgreSQL is not available. Ensure the database is running and accessible.");
        }

        _logger.LogInformation(
            "Dependency validation complete. Ollama: {Ollama}, PostgreSQL: {Postgres}",
            ollamaReady ? "Ready" : "Unavailable",
            postgresReady ? "Ready" : "Unavailable");
    }

    /// <summary>
    /// Main startup logic - initialize the MCP server.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MCP Server initializing...");

        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Register for application stopping notification
        _appLifetime.ApplicationStopping.Register(() =>
        {
            _logger.LogInformation("Application stopping signal received");
            _healthTracker.MarkStopping();
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called after all hosted services have started.
    /// Server is now ready to accept requests.
    /// </summary>
    public Task StartedAsync(CancellationToken cancellationToken)
    {
        _healthTracker.MarkReady();
        _logger.LogInformation("MCP Server is ready to accept requests");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called before any hosted service's StopAsync.
    /// Signal services to stop accepting new work.
    /// </summary>
    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MCP Server stopping - rejecting new requests...");
        _healthTracker.MarkStopping();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Main shutdown logic - cleanup resources.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MCP Server shutting down...");

        // Signal cancellation to all linked tokens
        _stoppingCts?.Cancel();

        // Wait for active operations to complete (with timeout)
        var timeout = _options.Value.ActiveOperationsShutdownTimeout;
        var waitStart = DateTimeOffset.UtcNow;

        while (_healthTracker is ServerHealthTracker tracker &&
               tracker.ActiveOperations > 0 &&
               DateTimeOffset.UtcNow - waitStart < timeout)
        {
            _logger.LogDebug(
                "Waiting for {Count} active operations to complete...",
                tracker.ActiveOperations);

            await Task.Delay(100, cancellationToken);
        }

        if (_healthTracker is ServerHealthTracker t && t.ActiveOperations > 0)
        {
            _logger.LogWarning(
                "Shutdown timeout reached with {Count} operations still active",
                t.ActiveOperations);
        }
    }

    /// <summary>
    /// Called after all hosted services have stopped.
    /// Final cleanup confirmation.
    /// </summary>
    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        _healthTracker.MarkStopped();
        _logger.LogInformation("MCP Server has stopped");

        _stoppingCts?.Dispose();

        return Task.CompletedTask;
    }

    private async Task<bool> ValidateOllamaAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _ollamaHealth.CheckHealthAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate Ollama connectivity");
            return false;
        }
    }

    private async Task<bool> ValidatePostgresAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _postgresHealth.CheckHealthAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate PostgreSQL connectivity");
            return false;
        }
    }
}
```

### 5. ServerLifecycleOptions Configuration

```csharp
/// <summary>
/// Configuration options for server lifecycle management.
/// </summary>
public sealed class ServerLifecycleOptions
{
    public const string SectionName = "ServerLifecycle";

    /// <summary>
    /// Whether to require Ollama connectivity on startup.
    /// Default: true
    /// </summary>
    public bool RequireOllamaOnStartup { get; set; } = true;

    /// <summary>
    /// Whether to require PostgreSQL connectivity on startup.
    /// Default: true
    /// </summary>
    public bool RequirePostgresOnStartup { get; set; } = true;

    /// <summary>
    /// Timeout for active operations to complete during shutdown.
    /// Default: 10 seconds
    /// </summary>
    public TimeSpan ActiveOperationsShutdownTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Interval for periodic health checks.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum time to wait for dependencies on startup.
    /// Default: 60 seconds
    /// </summary>
    public TimeSpan StartupDependencyTimeout { get; set; } = TimeSpan.FromSeconds(60);
}
```

### 6. Health Check Interfaces

```csharp
/// <summary>
/// Health check for Ollama service.
/// </summary>
public interface IOllamaHealthCheck
{
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Health check for PostgreSQL database.
/// </summary>
public interface IPostgresHealthCheck
{
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}
```

### 7. CancellationToken Propagation Pattern

All services must follow this pattern for CancellationToken propagation:

```csharp
/// <summary>
/// Provides a unified cancellation token for the MCP server.
/// Links the host shutdown token with server-specific cancellation.
/// </summary>
public interface IServerCancellation
{
    /// <summary>
    /// Gets a token that is cancelled when the server is stopping.
    /// </summary>
    CancellationToken StoppingToken { get; }

    /// <summary>
    /// Creates a linked token that respects both server and operation-specific cancellation.
    /// </summary>
    CancellationToken CreateLinkedToken(CancellationToken operationToken);
}

public sealed class ServerCancellation : IServerCancellation, IDisposable
{
    private readonly CancellationTokenSource _serverCts;

    public ServerCancellation(IHostApplicationLifetime lifetime)
    {
        _serverCts = new CancellationTokenSource();

        // Link to application stopping
        lifetime.ApplicationStopping.Register(() => _serverCts.Cancel());
    }

    public CancellationToken StoppingToken => _serverCts.Token;

    public CancellationToken CreateLinkedToken(CancellationToken operationToken)
    {
        return CancellationTokenSource
            .CreateLinkedTokenSource(_serverCts.Token, operationToken)
            .Token;
    }

    public void Dispose() => _serverCts.Dispose();
}
```

### 8. Service Registration

```csharp
public static class ServerLifecycleServiceExtensions
{
    public static IServiceCollection AddServerLifecycleServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configuration
        services.Configure<ServerLifecycleOptions>(
            configuration.GetSection(ServerLifecycleOptions.SectionName));

        services.AddOptionsWithValidateOnStart<ServerLifecycleOptions>();

        // Health tracking
        services.AddSingleton<ServerHealthTracker>();
        services.AddSingleton<IServerHealthTracker>(sp =>
            sp.GetRequiredService<ServerHealthTracker>());

        // Cancellation
        services.AddSingleton<IServerCancellation, ServerCancellation>();

        // Health checks (implementations in separate phases)
        services.AddSingleton<IOllamaHealthCheck, OllamaHealthCheck>();
        services.AddSingleton<IPostgresHealthCheck, PostgresHealthCheck>();

        // Hosted service
        services.AddHostedService<McpServerHostedService>();

        return services;
    }
}
```

### 9. Host Options Configuration

```csharp
// In Program.cs
builder.Services.Configure<HostOptions>(options =>
{
    // Maximum time for all hosted services to start
    options.StartupTimeout = TimeSpan.FromMinutes(2);

    // Maximum time for all hosted services to stop
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);

    // Stop application on unhandled exceptions in background services
    options.BackgroundServiceExceptionBehavior =
        BackgroundServiceExceptionBehavior.StopHost;

    // Start services sequentially (for dependency ordering)
    options.ServicesStartConcurrently = false;

    // Stop services sequentially (reverse order)
    options.ServicesStopConcurrently = false;
});
```

### 10. Lifecycle Sequence Diagram

```
Startup Sequence:
==================
1. StartingAsync (all services)
   - Validate Ollama connectivity
   - Validate PostgreSQL connectivity
   - Create CancellationTokenSource

2. StartAsync (all services)
   - Initialize MCP server
   - Register shutdown handlers

3. StartedAsync (all services)
   - Mark health as Ready
   - Begin accepting MCP requests

Shutdown Sequence:
==================
1. ApplicationStopping event fires (Ctrl+C, SIGTERM)
   - Health marked as Stopping

2. StoppingAsync (all services)
   - Stop accepting new requests
   - Signal pending operations

3. StopAsync (all services)
   - Cancel CancellationTokenSource
   - Wait for active operations (with timeout)

4. StoppedAsync (all services)
   - Final cleanup
   - Health marked as Stopped
```

---

## Dependencies

### Depends On

- **Phase 022**: MCP Server Project Scaffold - Base project structure must exist

### Blocks

- **Phase 024**: MCP Tool Registration - Tools need lifecycle hooks for graceful shutdown
- **Phase 025**: File Watcher Service - Watcher needs health tracker integration
- **Phase 026**: Ollama Service Integration - Health checks need Ollama client

---

## Testing Verification

```csharp
[Fact]
public async Task StartingAsync_ValidatesDependencies()
{
    // Arrange
    var ollamaHealth = new Mock<IOllamaHealthCheck>();
    ollamaHealth.Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);

    var postgresHealth = new Mock<IPostgresHealthCheck>();
    postgresHealth.Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);

    var service = CreateService(ollamaHealth.Object, postgresHealth.Object);

    // Act & Assert - should not throw
    await service.StartingAsync(CancellationToken.None);
}

[Fact]
public async Task StartingAsync_ThrowsWhenOllamaUnavailable()
{
    // Arrange
    var ollamaHealth = new Mock<IOllamaHealthCheck>();
    ollamaHealth.Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);

    var options = Options.Create(new ServerLifecycleOptions
    {
        RequireOllamaOnStartup = true
    });

    var service = CreateService(ollamaHealth.Object, options: options);

    // Act & Assert
    await Assert.ThrowsAsync<InvalidOperationException>(
        () => service.StartingAsync(CancellationToken.None));
}

[Fact]
public void HealthTracker_TransitionsCorrectly()
{
    // Arrange
    var tracker = new ServerHealthTracker(NullLogger<ServerHealthTracker>.Instance);
    var stateChanges = new List<ServerHealthState>();
    tracker.StateChanged += (_, e) => stateChanges.Add(e.NewState);

    // Act
    Assert.Equal(ServerHealthState.Starting, tracker.CurrentState);

    tracker.MarkReady();
    Assert.Equal(ServerHealthState.Ready, tracker.CurrentState);

    tracker.MarkDegraded("Test degradation");
    Assert.Equal(ServerHealthState.Degraded, tracker.CurrentState);

    tracker.MarkStopping();
    Assert.Equal(ServerHealthState.Stopping, tracker.CurrentState);

    tracker.MarkStopped();
    Assert.Equal(ServerHealthState.Stopped, tracker.CurrentState);

    // Assert
    Assert.Equal(4, stateChanges.Count);
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.Server/Lifecycle/ServerHealthState.cs` | Create | Health state enum |
| `src/CompoundDocs.Server/Lifecycle/IServerHealthTracker.cs` | Create | Health tracking interface |
| `src/CompoundDocs.Server/Lifecycle/ServerHealthTracker.cs` | Create | Health tracking implementation |
| `src/CompoundDocs.Server/Lifecycle/McpServerHostedService.cs` | Create | Main hosted service |
| `src/CompoundDocs.Server/Lifecycle/ServerLifecycleOptions.cs` | Create | Configuration options |
| `src/CompoundDocs.Server/Lifecycle/IServerCancellation.cs` | Create | Cancellation token provider |
| `src/CompoundDocs.Server/Lifecycle/ServerCancellation.cs` | Create | Cancellation implementation |
| `src/CompoundDocs.Server/Health/IOllamaHealthCheck.cs` | Create | Ollama health check interface |
| `src/CompoundDocs.Server/Health/IPostgresHealthCheck.cs` | Create | PostgreSQL health check interface |
| `src/CompoundDocs.Server/Extensions/ServerLifecycleServiceExtensions.cs` | Create | DI registration |
| `tests/CompoundDocs.Server.Tests/Lifecycle/ServerHealthTrackerTests.cs` | Create | Health tracker tests |
| `tests/CompoundDocs.Server.Tests/Lifecycle/McpServerHostedServiceTests.cs` | Create | Hosted service tests |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Startup timeout exceeded | Configure generous StartupTimeout (2 minutes default) |
| Shutdown timeout exceeded | Log warning and continue with forced shutdown |
| Circular dependency in health checks | Health check interfaces have no dependencies on other services |
| Race condition in health state | Use thread-safe implementation with locks |
| Lost requests during shutdown | Health tracker prevents new operations once Stopping |
| Unhandled exceptions crash server | Configure BackgroundServiceExceptionBehavior.StopHost for graceful handling |
