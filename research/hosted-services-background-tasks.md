# IHostedService and BackgroundService Patterns in .NET

This document provides comprehensive coverage of long-running periodic tasks in .NET Generic Host and ASP.NET Core web hosts.

## Table of Contents

1. [IHostedService Interface](#1-ihostedservice-interface)
2. [BackgroundService Base Class](#2-backgroundservice-base-class)
3. [Timer-Based Periodic Tasks](#3-timer-based-periodic-tasks)
4. [Best Practices](#4-best-practices)
5. [Common Patterns](#5-common-patterns)
6. [.NET 8+ Improvements](#6-net-8-improvements)
7. [Code Examples](#7-complete-code-examples)

---

## 1. IHostedService Interface

### What is IHostedService?

`IHostedService` is the fundamental interface for implementing background tasks in .NET. It defines a contract for objects managed by the host with two lifecycle methods.

```csharp
public interface IHostedService
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
```

### StartAsync Lifecycle Method

`StartAsync` contains the logic to start the background task.

**Key characteristics:**
- Called **before** the app's request processing pipeline is configured
- Called **before** the server is started and `IApplicationLifetime.ApplicationStarted` is triggered
- Should be limited to **short-running tasks** because hosted services run sequentially
- No further services start until `StartAsync` completes

```csharp
public class MyHostedService : IHostedService
{
    private readonly ILogger<MyHostedService> _logger;
    private Task? _executingTask;
    private CancellationTokenSource? _cts;

    public MyHostedService(ILogger<MyHostedService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Hosted service starting...");

        // Create a linked token for graceful shutdown
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start the background work (fire and forget pattern)
        _executingTask = DoWorkAsync(_cts.Token);

        // Return immediately - don't block startup
        return _executingTask.IsCompleted ? _executingTask : Task.CompletedTask;
    }

    private async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Working...");
            await Task.Delay(5000, stoppingToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Hosted service stopping...");

        if (_executingTask == null)
            return;

        // Signal cancellation
        _cts?.Cancel();

        // Wait for the task to complete or timeout
        await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
    }
}
```

### StopAsync Lifecycle Method

`StopAsync` is triggered when the host performs a graceful shutdown.

**Key characteristics:**
- Default timeout: 30 seconds (configurable via `ShutdownTimeout`)
- `Dispose` is called even if `StopAsync` fails
- **Not called** during unexpected shutdowns (e.g., process crashes)

### Registration with AddHostedService<T>()

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register the hosted service
builder.Services.AddHostedService<MyHostedService>();

var app = builder.Build();
app.Run();
```

### Execution Order and Startup Behavior

Hosted services are started **sequentially** in the order they are registered:

```csharp
// These start in order: Service1 -> Service2 -> Service3
builder.Services.AddHostedService<Service1>();
builder.Services.AddHostedService<Service2>();
builder.Services.AddHostedService<Service3>();
```

**Important:** If `Service1.StartAsync` blocks (runs synchronously for a long time), `Service2` and `Service3` won't start until it completes.

To enable concurrent startup (.NET 8+):

```csharp
builder.Services.Configure<HostOptions>(options =>
{
    options.ServicesStartConcurrently = true;
    options.ServicesStopConcurrently = true;
});
```

---

## 2. BackgroundService Base Class

### How BackgroundService Simplifies IHostedService

`BackgroundService` is an abstract base class that implements `IHostedService`, handling common concerns like cancellation token management and error propagation.

```csharp
public abstract class BackgroundService : IHostedService, IDisposable
{
    private Task? _executeTask;
    private CancellationTokenSource? _stoppingCts;

    protected abstract Task ExecuteAsync(CancellationToken stoppingToken);

    public virtual Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executeTask = ExecuteAsync(_stoppingCts.Token);

        if (_executeTask.IsCompleted)
            return _executeTask;  // Propagate exceptions immediately

        return Task.CompletedTask;  // Don't block startup
    }

    public virtual async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_executeTask == null)
            return;

        _stoppingCts?.Cancel();

        await Task.WhenAny(_executeTask, Task.Delay(Timeout.Infinite, cancellationToken))
            .ConfigureAwait(false);
    }

    public virtual void Dispose()
    {
        _stoppingCts?.Cancel();
    }
}
```

### The ExecuteAsync Method Pattern

`ExecuteAsync` is the single method you must override. It represents the entire lifetime of your background service.

```csharp
public class MyBackgroundService : BackgroundService
{
    private readonly ILogger<MyBackgroundService> _logger;

    public MyBackgroundService(ILogger<MyBackgroundService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown - don't log as error
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in background service");
                // Optionally add delay before retry
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        _logger.LogInformation("Background service stopped");
    }

    private async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        // Your business logic here
        await Task.CompletedTask;
    }
}
```

### Proper Cancellation Token Handling

The `stoppingToken` is triggered when `IHostedService.StopAsync` is called. Your implementation should respond promptly.

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // Register a callback for cancellation
    stoppingToken.Register(() =>
        _logger.LogInformation("Cancellation requested"));

    while (!stoppingToken.IsCancellationRequested)
    {
        // Pass the token to all async operations
        await ProcessItemsAsync(stoppingToken);

        // Use the token with delays
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Graceful exit
            break;
        }
    }
}

private async Task ProcessItemsAsync(CancellationToken stoppingToken)
{
    var items = await GetItemsAsync(stoppingToken);

    foreach (var item in items)
    {
        // Check cancellation periodically in long loops
        stoppingToken.ThrowIfCancellationRequested();

        await ProcessItemAsync(item, stoppingToken);
    }
}
```

### Exception Handling and Service Restart Behavior

**.NET 6+ Behavior (Breaking Change):**

Starting in .NET 6, unhandled exceptions in `ExecuteAsync` are logged and **the host is stopped by default**.

```csharp
// Configure exception behavior
builder.Services.Configure<HostOptions>(options =>
{
    // Default: StopHost - stops the application on unhandled exception
    options.BackgroundServiceExceptionBehavior =
        BackgroundServiceExceptionBehavior.StopHost;

    // Alternative: Ignore - logs exception but continues running
    // options.BackgroundServiceExceptionBehavior =
    //     BackgroundServiceExceptionBehavior.Ignore;
});
```

**Best Practice:** Handle exceptions within your service and implement retry logic:

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            await DoWorkAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during shutdown
            _logger.LogInformation("Service is shutting down");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in background service");

            // Implement exponential backoff or circuit breaker
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
```

---

## 3. Timer-Based Periodic Tasks

### Using System.Threading.Timer (Synchronous Callbacks)

`System.Threading.Timer` is suitable when your work is synchronous or you need precise timing.

```csharp
public class TimerBasedService : IHostedService, IDisposable
{
    private readonly ILogger<TimerBasedService> _logger;
    private Timer? _timer;
    private int _executionCount;

    public TimerBasedService(ILogger<TimerBasedService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Timer service starting");

        // Timer parameters: callback, state, dueTime, period
        _timer = new Timer(
            DoWork,
            null,
            TimeSpan.Zero,           // Start immediately
            TimeSpan.FromSeconds(5)  // Repeat every 5 seconds
        );

        return Task.CompletedTask;
    }

    private void DoWork(object? state)
    {
        var count = Interlocked.Increment(ref _executionCount);
        _logger.LogInformation("Timer tick #{Count}", count);

        // WARNING: This callback runs on a ThreadPool thread
        // If work takes longer than the period, callbacks can overlap!
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Timer service stopping");

        // Disable the timer (don't dispose yet)
        _timer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
```

**Limitations of System.Threading.Timer:**
- Callbacks are synchronous (must use fire-and-forget for async)
- Callbacks can overlap if work exceeds the period
- No built-in cancellation support

### Using PeriodicTimer (.NET 6+) - Recommended

`PeriodicTimer` is designed for async-friendly periodic work and naturally prevents overlapping executions.

```csharp
public class PeriodicTimerService : BackgroundService
{
    private readonly ILogger<PeriodicTimerService> _logger;
    private readonly TimeSpan _period = TimeSpan.FromSeconds(30);
    private int _executionCount;

    public PeriodicTimerService(ILogger<PeriodicTimerService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Periodic timer service started");

        // Optionally execute immediately on startup
        await DoWorkAsync(stoppingToken);

        using var timer = new PeriodicTimer(_period);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await DoWorkAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Periodic timer service stopping");
        }
    }

    private async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        var count = Interlocked.Increment(ref _executionCount);

        _logger.LogInformation("Periodic work starting. Count: {Count}", count);

        // Simulate work
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        _logger.LogInformation("Periodic work completed. Count: {Count}", count);
    }
}
```

**Advantages of PeriodicTimer:**
- Async-native (`WaitForNextTickAsync`)
- Automatically prevents overlapping executions
- Clean cancellation via `CancellationToken`
- Disposable for resource cleanup

### Using Task.Delay Loops vs Dedicated Timers

**Task.Delay Loop:**

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await DoWorkAsync(stoppingToken);

        // Wait between executions
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
    }
}
```

**Comparison:**

| Aspect | Task.Delay Loop | PeriodicTimer |
|--------|-----------------|---------------|
| Simplicity | Very simple | Slightly more setup |
| Drift | Time drifts (includes work duration) | Consistent intervals |
| Overlap | Never overlaps | Never overlaps |
| Precision | Less precise | More precise timing |
| Use Case | Simple scenarios | Production periodic tasks |

### Preventing Overlapping Executions

**With PeriodicTimer (automatic):**

```csharp
// PeriodicTimer naturally prevents overlap - the next tick only starts
// after WaitForNextTickAsync returns AND the work completes
while (await timer.WaitForNextTickAsync(stoppingToken))
{
    await DoWorkAsync(stoppingToken);  // Must complete before next tick
}
```

**With System.Threading.Timer (manual):**

```csharp
public class NonOverlappingTimerService : IHostedService, IDisposable
{
    private Timer? _timer;
    private int _isRunning;  // 0 = not running, 1 = running

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        // Try to acquire the "lock"
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            // Another execution is already in progress
            return;
        }

        try
        {
            await DoWorkAsync();
        }
        finally
        {
            // Release the "lock"
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }

    private async Task DoWorkAsync()
    {
        // Your async work here
        await Task.Delay(5000);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();
}
```

### Cron-Like Scheduling Options

For cron-like scheduling, consider these options:

**1. Manual Cron Parsing with Cronos:**

```csharp
using Cronos;

public class CronScheduledService : BackgroundService
{
    private readonly CronExpression _cronExpression;
    private readonly TimeZoneInfo _timeZone;

    public CronScheduledService()
    {
        // Run at 2:00 AM every day
        _cronExpression = CronExpression.Parse("0 2 * * *");
        _timeZone = TimeZoneInfo.Local;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.Now;
            var nextOccurrence = _cronExpression.GetNextOccurrence(now, _timeZone);

            if (nextOccurrence.HasValue)
            {
                var delay = nextOccurrence.Value - now;

                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, stoppingToken);
                }

                await DoScheduledWorkAsync(stoppingToken);
            }
        }
    }
}
```

**2. Using NCronJob (Lightweight):**

```csharp
// NuGet: NCronJob

[CronJob("0 */5 * * * *")]  // Every 5 minutes
public class MyCronJob : IJob
{
    public async Task RunAsync(JobExecutionContext context, CancellationToken token)
    {
        await DoWorkAsync(token);
    }
}

// Registration
builder.Services.AddNCronJob(options =>
{
    options.AddCronJob<MyCronJob>();
});
```

**3. Using Hangfire (Full-Featured):**

```csharp
// NuGet: Hangfire

// In ConfigureServices
builder.Services.AddHangfire(x => x.UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer();

// Schedule recurring job
RecurringJob.AddOrUpdate<MyJob>(
    "my-job-id",
    job => job.Execute(),
    Cron.Daily(2, 0)  // 2:00 AM daily
);
```

**4. Using Quartz.NET (Enterprise):**

```csharp
// NuGet: Quartz, Quartz.Extensions.Hosting

builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("my-job");

    q.AddJob<MyJob>(opts => opts.WithIdentity(jobKey));

    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithCronSchedule("0 0 2 * * ?"));  // 2:00 AM daily
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
```

---

## 4. Best Practices

### Avoiding Blocking in StartAsync

**Bad - Blocks other services from starting:**

```csharp
public Task StartAsync(CancellationToken cancellationToken)
{
    // DON'T DO THIS - blocks startup
    while (true)
    {
        DoWork();
        Thread.Sleep(5000);
    }
}
```

**Good - Returns quickly, work runs in background:**

```csharp
public Task StartAsync(CancellationToken cancellationToken)
{
    _logger.LogInformation("Service starting");

    // Start work in background without awaiting
    _executingTask = ExecuteAsync(_stoppingCts.Token);

    // Return immediately
    return Task.CompletedTask;
}
```

**Also Good - Use BackgroundService:**

```csharp
// BackgroundService handles this for you
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    // This runs in the background automatically
    // The first await yields control, allowing startup to proceed
    await Task.Yield();  // Explicit yield if needed

    while (!stoppingToken.IsCancellationRequested)
    {
        await DoWorkAsync(stoppingToken);
    }
}
```

### Graceful Shutdown Handling

```csharp
public class GracefulShutdownService : BackgroundService
{
    private readonly ILogger<GracefulShutdownService> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public GracefulShutdownService(
        ILogger<GracefulShutdownService> logger,
        IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Register for application stopping event
        _lifetime.ApplicationStopping.Register(() =>
            _logger.LogInformation("Application is stopping..."));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessBatchAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected - shutdown requested
        }
        finally
        {
            // Cleanup resources
            await CleanupAsync();
            _logger.LogInformation("Service shutdown complete");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping service...");

        // Allow base class to signal cancellation
        await base.StopAsync(cancellationToken);

        _logger.LogInformation("Service stopped");
    }
}
```

**Configure shutdown timeout:**

```csharp
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(60);  // Default is 30s
});
```

### Dependency Injection in Hosted Services (Scoped Services)

**Problem:** Hosted services are singletons, but many services (like DbContext) are scoped.

**Solution:** Create a scope manually:

```csharp
public class ScopedServiceConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScopedServiceConsumer> _logger;

    public ScopedServiceConsumer(
        IServiceProvider serviceProvider,
        ILogger<ScopedServiceConsumer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scoped service consumer started");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Create a new scope for each iteration
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider
                    .GetRequiredService<MyDbContext>();

                var processor = scope.ServiceProvider
                    .GetRequiredService<IDataProcessor>();

                await processor.ProcessAsync(dbContext, stoppingToken);
            }  // Scope disposed - scoped services cleaned up

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

**With async scope (.NET 6+):**

```csharp
using (var scope = _serviceProvider.CreateAsyncScope())
{
    var service = scope.ServiceProvider.GetRequiredService<IScopedService>();
    await service.DoWorkAsync(stoppingToken);
}  // Async disposal
```

### Error Handling and Resilience

```csharp
public class ResilientBackgroundService : BackgroundService
{
    private readonly ILogger<ResilientBackgroundService> _logger;
    private int _consecutiveFailures;
    private const int MaxConsecutiveFailures = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkAsync(stoppingToken);
                _consecutiveFailures = 0;  // Reset on success
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;  // Graceful shutdown
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                _logger.LogError(ex,
                    "Error in background service. Failure #{Count}",
                    _consecutiveFailures);

                if (_consecutiveFailures >= MaxConsecutiveFailures)
                {
                    _logger.LogCritical(
                        "Max consecutive failures reached. Service unhealthy.");
                    // Could trigger health check failure or alert here
                }

                // Exponential backoff
                var delay = TimeSpan.FromSeconds(
                    Math.Min(Math.Pow(2, _consecutiveFailures), 300));

                await Task.Delay(delay, stoppingToken);
            }
        }
    }
}
```

### Health Checks for Background Services

```csharp
// Health check implementation
public class BackgroundServiceHealthCheck : IHealthCheck
{
    private volatile bool _isHealthy = true;
    private DateTimeOffset _lastSuccessfulRun = DateTimeOffset.MinValue;
    private readonly TimeSpan _unhealthyThreshold = TimeSpan.FromMinutes(5);

    public void ReportHealthy()
    {
        _isHealthy = true;
        _lastSuccessfulRun = DateTimeOffset.UtcNow;
    }

    public void ReportUnhealthy() => _isHealthy = false;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var timeSinceLastRun = DateTimeOffset.UtcNow - _lastSuccessfulRun;

        if (!_isHealthy)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Background service reported unhealthy"));
        }

        if (timeSinceLastRun > _unhealthyThreshold)
        {
            return Task.FromResult(
                HealthCheckResult.Degraded(
                    $"No successful run in {timeSinceLastRun.TotalMinutes:F1} minutes"));
        }

        return Task.FromResult(
            HealthCheckResult.Healthy(
                $"Last run: {_lastSuccessfulRun:u}"));
    }
}

// Background service using health check
public class MonitoredBackgroundService : BackgroundService
{
    private readonly BackgroundServiceHealthCheck _healthCheck;

    public MonitoredBackgroundService(BackgroundServiceHealthCheck healthCheck)
    {
        _healthCheck = healthCheck;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkAsync(stoppingToken);
                _healthCheck.ReportHealthy();
            }
            catch (Exception)
            {
                _healthCheck.ReportUnhealthy();
                throw;
            }
        }
    }
}

// Registration
builder.Services.AddSingleton<BackgroundServiceHealthCheck>();
builder.Services.AddHealthChecks()
    .AddCheck<BackgroundServiceHealthCheck>("background-service");
builder.Services.AddHostedService<MonitoredBackgroundService>();
```

---

## 5. Common Patterns

### Queue-Based Background Processing

```csharp
// Queue interface
public interface IBackgroundTaskQueue
{
    ValueTask QueueAsync(Func<CancellationToken, ValueTask> workItem);
    ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(
        CancellationToken cancellationToken);
}

// Channel-based implementation
public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<CancellationToken, ValueTask>> _queue;

    public BackgroundTaskQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait  // Apply backpressure
        };
        _queue = Channel.CreateBounded<Func<CancellationToken, ValueTask>>(options);
    }

    public async ValueTask QueueAsync(Func<CancellationToken, ValueTask> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        await _queue.Writer.WriteAsync(workItem);
    }

    public async ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(
        CancellationToken cancellationToken)
    {
        return await _queue.Reader.ReadAsync(cancellationToken);
    }
}

// Queue processor service
public class QueueProcessorService : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<QueueProcessorService> _logger;

    public QueueProcessorService(
        IBackgroundTaskQueue taskQueue,
        ILogger<QueueProcessorService> logger)
    {
        _taskQueue = taskQueue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queue processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var workItem = await _taskQueue.DequeueAsync(stoppingToken);

                await workItem(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing queued work item");
            }
        }
    }
}

// Registration
builder.Services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
builder.Services.AddHostedService<QueueProcessorService>();

// Usage (e.g., in a controller)
public class MyController : ControllerBase
{
    private readonly IBackgroundTaskQueue _queue;

    public MyController(IBackgroundTaskQueue queue) => _queue = queue;

    [HttpPost("process")]
    public async Task<IActionResult> ProcessAsync([FromBody] DataModel data)
    {
        await _queue.QueueAsync(async token =>
        {
            // This runs in the background
            await ProcessDataAsync(data, token);
        });

        return Accepted();  // Return immediately
    }
}
```

### Timed Background Tasks

```csharp
public class DataSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DataSyncService> _logger;
    private readonly TimeSpan _syncInterval = TimeSpan.FromHours(1);

    public DataSyncService(
        IServiceProvider serviceProvider,
        ILogger<DataSyncService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run immediately on startup
        await SyncDataAsync(stoppingToken);

        using var timer = new PeriodicTimer(_syncInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SyncDataAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Data sync service stopping");
        }
    }

    private async Task SyncDataAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting data sync at {Time}", DateTime.UtcNow);

        using var scope = _serviceProvider.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<IDataSyncService>();

        try
        {
            await syncService.SyncAsync(stoppingToken);
            _logger.LogInformation("Data sync completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data sync failed");
        }
    }
}
```

### Long-Running Worker Services

```csharp
public class MessageProcessorWorker : BackgroundService
{
    private readonly ILogger<MessageProcessorWorker> _logger;
    private readonly IMessageBroker _messageBroker;

    public MessageProcessorWorker(
        ILogger<MessageProcessorWorker> logger,
        IMessageBroker messageBroker)
    {
        _logger = logger;
        _messageBroker = messageBroker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message processor worker started");

        await _messageBroker.SubscribeAsync("my-queue", async message =>
        {
            await ProcessMessageAsync(message, stoppingToken);
        }, stoppingToken);

        // Keep the service alive until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessMessageAsync(Message message, CancellationToken token)
    {
        _logger.LogInformation("Processing message {Id}", message.Id);
        // Process the message
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Message processor stopping, draining queue...");
        await _messageBroker.UnsubscribeAsync();
        await base.StopAsync(cancellationToken);
    }
}
```

### Coordinating Multiple Background Services

```csharp
// Shared state for coordination
public class ServiceCoordinator
{
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private volatile bool _isInitialized;
    private TaskCompletionSource _initializationComplete = new();

    public bool IsInitialized => _isInitialized;
    public Task WaitForInitializationAsync() => _initializationComplete.Task;

    public async Task InitializeAsync(Func<Task> initializationWork)
    {
        if (_isInitialized) return;

        await _initializationLock.WaitAsync();
        try
        {
            if (_isInitialized) return;

            await initializationWork();
            _isInitialized = true;
            _initializationComplete.TrySetResult();
        }
        finally
        {
            _initializationLock.Release();
        }
    }
}

// Initializer service
public class DatabaseInitializerService : BackgroundService
{
    private readonly ServiceCoordinator _coordinator;

    public DatabaseInitializerService(ServiceCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _coordinator.InitializeAsync(async () =>
        {
            // Run migrations, seed data, etc.
            await Task.Delay(2000, stoppingToken);  // Simulated work
        });
    }
}

// Dependent service
public class DependentWorkerService : BackgroundService
{
    private readonly ServiceCoordinator _coordinator;
    private readonly ILogger<DependentWorkerService> _logger;

    public DependentWorkerService(
        ServiceCoordinator coordinator,
        ILogger<DependentWorkerService> logger)
    {
        _coordinator = coordinator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for database to be initialized
        _logger.LogInformation("Waiting for initialization...");
        await _coordinator.WaitForInitializationAsync();
        _logger.LogInformation("Initialization complete, starting work");

        while (!stoppingToken.IsCancellationRequested)
        {
            await DoWorkAsync(stoppingToken);
            await Task.Delay(5000, stoppingToken);
        }
    }

    private Task DoWorkAsync(CancellationToken token) => Task.CompletedTask;
}

// Registration
builder.Services.AddSingleton<ServiceCoordinator>();
builder.Services.AddHostedService<DatabaseInitializerService>();
builder.Services.AddHostedService<DependentWorkerService>();
```

---

## 6. .NET 8+ Improvements

### IHostedLifecycleService Interface

.NET 8 introduces `IHostedLifecycleService` for more granular lifecycle control:

```csharp
public interface IHostedLifecycleService : IHostedService
{
    Task StartingAsync(CancellationToken cancellationToken);
    Task StartedAsync(CancellationToken cancellationToken);
    Task StoppingAsync(CancellationToken cancellationToken);
    Task StoppedAsync(CancellationToken cancellationToken);
}
```

**Lifecycle order:**
1. `StartingAsync` - All services, before any `StartAsync`
2. `StartAsync` - The main startup logic
3. `StartedAsync` - All services, after all `StartAsync` complete
4. (Application runs)
5. `StoppingAsync` - All services, before any `StopAsync`
6. `StopAsync` - The main shutdown logic
7. `StoppedAsync` - All services, after all `StopAsync` complete

**Example:**

```csharp
public class LifecycleAwareService : IHostedLifecycleService
{
    private readonly ILogger<LifecycleAwareService> _logger;

    public LifecycleAwareService(ILogger<LifecycleAwareService> logger)
    {
        _logger = logger;
    }

    public Task StartingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StartingAsync - Validating prerequisites...");
        // Validate database connections, external services, etc.
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StartAsync - Main initialization...");
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StartedAsync - All services started, ready to process...");
        // Safe to start processing - all services are initialized
        return Task.CompletedTask;
    }

    public Task StoppingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StoppingAsync - Preparing for shutdown...");
        // Stop accepting new work
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StopAsync - Cleaning up...");
        return Task.CompletedTask;
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("StoppedAsync - Shutdown complete");
        return Task.CompletedTask;
    }
}
```

**Use case - Database initialization:**

```csharp
public class DatabaseInitializer : IHostedLifecycleService
{
    private readonly IServiceProvider _serviceProvider;

    public DatabaseInitializer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        // Initialize database BEFORE other services start
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MyDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
```

### Startup Timeout Option

```csharp
builder.Services.Configure<HostOptions>(options =>
{
    // Maximum time for all hosted services to start
    options.StartupTimeout = TimeSpan.FromMinutes(2);

    // Maximum time for all hosted services to stop
    options.ShutdownTimeout = TimeSpan.FromSeconds(60);
});
```

### Concurrent Service Startup/Shutdown

```csharp
builder.Services.Configure<HostOptions>(options =>
{
    // Start all hosted services concurrently (default: false)
    options.ServicesStartConcurrently = true;

    // Stop all hosted services concurrently (default: false)
    options.ServicesStopConcurrently = true;
});
```

### Keyed Services with Hosted Services

.NET 8 introduces keyed dependency injection, useful for multiple implementations:

```csharp
// Register keyed services
builder.Services.AddKeyedSingleton<IMessageProcessor, EmailProcessor>("email");
builder.Services.AddKeyedSingleton<IMessageProcessor, SmsProcessor>("sms");
builder.Services.AddKeyedSingleton<IMessageProcessor, PushProcessor>("push");

// Hosted service using keyed services
public class NotificationWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public NotificationWorker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var notification = await GetNextNotificationAsync(stoppingToken);

            // Get the appropriate processor by key
            var processor = _serviceProvider
                .GetRequiredKeyedService<IMessageProcessor>(notification.Channel);

            await processor.ProcessAsync(notification, stoppingToken);
        }
    }
}

// Or inject directly with [FromKeyedServices]
public class TypedNotificationWorker : BackgroundService
{
    private readonly IMessageProcessor _emailProcessor;
    private readonly IMessageProcessor _smsProcessor;

    public TypedNotificationWorker(
        [FromKeyedServices("email")] IMessageProcessor emailProcessor,
        [FromKeyedServices("sms")] IMessageProcessor smsProcessor)
    {
        _emailProcessor = emailProcessor;
        _smsProcessor = smsProcessor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Use processors directly
    }
}
```

---

## 7. Complete Code Examples

### Example 1: Periodic Timer-Based Service Using PeriodicTimer

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyApp.BackgroundServices;

/// <summary>
/// A background service that performs periodic work using PeriodicTimer.
/// Demonstrates best practices for timer-based background tasks.
/// </summary>
public class PeriodicCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PeriodicCleanupService> _logger;
    private readonly PeriodicCleanupOptions _options;
    private int _executionCount;

    public PeriodicCleanupService(
        IServiceProvider serviceProvider,
        ILogger<PeriodicCleanupService> logger,
        IOptions<PeriodicCleanupOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Periodic Cleanup Service started. Interval: {Interval}",
            _options.CleanupInterval);

        // Optionally run immediately on startup
        if (_options.RunOnStartup)
        {
            await PerformCleanupAsync(stoppingToken);
        }

        using var timer = new PeriodicTimer(_options.CleanupInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PerformCleanupAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Periodic Cleanup Service is stopping");
        }
    }

    private async Task PerformCleanupAsync(CancellationToken stoppingToken)
    {
        var count = Interlocked.Increment(ref _executionCount);
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Cleanup #{Count} starting at {Time}",
            count,
            DateTimeOffset.UtcNow);

        try
        {
            // Create scope for scoped dependencies
            using var scope = _serviceProvider.CreateScope();

            var dbContext = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            // Perform cleanup operations
            var deletedCount = await CleanupOldRecordsAsync(
                dbContext,
                _options.RetentionDays,
                stoppingToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Cleanup #{Count} completed. Deleted {DeletedCount} records in {Duration}ms",
                count,
                deletedCount,
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Cleanup #{Count} failed", count);
            // Don't rethrow - let the timer continue
        }
    }

    private static async Task<int> CleanupOldRecordsAsync(
        ApplicationDbContext dbContext,
        int retentionDays,
        CancellationToken stoppingToken)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        return await dbContext.AuditLogs
            .Where(log => log.CreatedAt < cutoffDate)
            .ExecuteDeleteAsync(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Periodic Cleanup Service stopping. Total executions: {Count}",
            _executionCount);

        await base.StopAsync(cancellationToken);
    }
}

public class PeriodicCleanupOptions
{
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
    public int RetentionDays { get; set; } = 30;
    public bool RunOnStartup { get; set; } = true;
}

// Registration in Program.cs
public static class PeriodicCleanupServiceExtensions
{
    public static IServiceCollection AddPeriodicCleanupService(
        this IServiceCollection services,
        Action<PeriodicCleanupOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.AddHostedService<PeriodicCleanupService>();

        return services;
    }
}

// Usage:
// builder.Services.AddPeriodicCleanupService(options =>
// {
//     options.CleanupInterval = TimeSpan.FromMinutes(30);
//     options.RetentionDays = 7;
// });
```

### Example 2: Queue-Based Processing Service

```csharp
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyApp.BackgroundServices;

/// <summary>
/// Represents a work item that can be queued for background processing.
/// </summary>
public record WorkItem(
    Guid Id,
    string Name,
    Func<IServiceProvider, CancellationToken, ValueTask> WorkDelegate,
    DateTimeOffset QueuedAt);

/// <summary>
/// Interface for the background task queue.
/// </summary>
public interface IBackgroundTaskQueue
{
    /// <summary>
    /// Queues a work item for background processing.
    /// </summary>
    ValueTask<Guid> QueueWorkItemAsync(
        string name,
        Func<IServiceProvider, CancellationToken, ValueTask> workDelegate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues the next work item.
    /// </summary>
    ValueTask<WorkItem> DequeueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current queue count.
    /// </summary>
    int Count { get; }
}

/// <summary>
/// Channel-based implementation of the background task queue.
/// </summary>
public class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<WorkItem> _queue;
    private int _count;

    public BackgroundTaskQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,  // Allow multiple consumers
            SingleWriter = false   // Allow multiple producers
        };

        _queue = Channel.CreateBounded<WorkItem>(options);
    }

    public int Count => _count;

    public async ValueTask<Guid> QueueWorkItemAsync(
        string name,
        Func<IServiceProvider, CancellationToken, ValueTask> workDelegate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workDelegate);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var workItem = new WorkItem(
            Guid.NewGuid(),
            name,
            workDelegate,
            DateTimeOffset.UtcNow);

        await _queue.Writer.WriteAsync(workItem, cancellationToken);
        Interlocked.Increment(ref _count);

        return workItem.Id;
    }

    public async ValueTask<WorkItem> DequeueAsync(CancellationToken cancellationToken)
    {
        var workItem = await _queue.Reader.ReadAsync(cancellationToken);
        Interlocked.Decrement(ref _count);
        return workItem;
    }
}

/// <summary>
/// Background service that processes queued work items.
/// </summary>
public class QueuedBackgroundService : BackgroundService
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueuedBackgroundService> _logger;
    private int _processedCount;
    private int _failedCount;

    public QueuedBackgroundService(
        IBackgroundTaskQueue taskQueue,
        IServiceProvider serviceProvider,
        ILogger<QueuedBackgroundService> logger)
    {
        _taskQueue = taskQueue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Queue processor service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            WorkItem? workItem = null;

            try
            {
                workItem = await _taskQueue.DequeueAsync(stoppingToken);

                var queueTime = DateTimeOffset.UtcNow - workItem.QueuedAt;

                _logger.LogInformation(
                    "Processing work item {Id} ({Name}). Queue time: {QueueTime}ms",
                    workItem.Id,
                    workItem.Name,
                    queueTime.TotalMilliseconds);

                var stopwatch = Stopwatch.StartNew();

                await workItem.WorkDelegate(_serviceProvider, stoppingToken);

                stopwatch.Stop();
                Interlocked.Increment(ref _processedCount);

                _logger.LogInformation(
                    "Completed work item {Id} ({Name}) in {Duration}ms",
                    workItem.Id,
                    workItem.Name,
                    stopwatch.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref _failedCount);

                _logger.LogError(
                    ex,
                    "Error processing work item {Id} ({Name})",
                    workItem?.Id,
                    workItem?.Name ?? "unknown");

                // Continue processing other items
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Queue processor stopping. Processed: {Processed}, Failed: {Failed}, Pending: {Pending}",
            _processedCount,
            _failedCount,
            _taskQueue.Count);

        await base.StopAsync(cancellationToken);
    }
}

// Registration
public static class QueuedBackgroundServiceExtensions
{
    public static IServiceCollection AddQueuedBackgroundService(
        this IServiceCollection services,
        int queueCapacity = 100)
    {
        services.AddSingleton<IBackgroundTaskQueue>(
            new BackgroundTaskQueue(queueCapacity));

        services.AddHostedService<QueuedBackgroundService>();

        return services;
    }
}

// Example usage in a controller:
[ApiController]
[Route("api/[controller]")]
public class JobsController : ControllerBase
{
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        IBackgroundTaskQueue taskQueue,
        ILogger<JobsController> logger)
    {
        _taskQueue = taskQueue;
        _logger = logger;
    }

    [HttpPost("send-email")]
    public async Task<IActionResult> SendEmailAsync(
        [FromBody] SendEmailRequest request,
        CancellationToken cancellationToken)
    {
        var workItemId = await _taskQueue.QueueWorkItemAsync(
            $"Send email to {request.To}",
            async (sp, ct) =>
            {
                using var scope = sp.CreateScope();
                var emailService = scope.ServiceProvider
                    .GetRequiredService<IEmailService>();

                await emailService.SendAsync(
                    request.To,
                    request.Subject,
                    request.Body,
                    ct);
            },
            cancellationToken);

        _logger.LogInformation(
            "Email job queued with ID {WorkItemId}",
            workItemId);

        return Accepted(new { JobId = workItemId });
    }

    [HttpPost("generate-report")]
    public async Task<IActionResult> GenerateReportAsync(
        [FromBody] ReportRequest request,
        CancellationToken cancellationToken)
    {
        var workItemId = await _taskQueue.QueueWorkItemAsync(
            $"Generate {request.ReportType} report",
            async (sp, ct) =>
            {
                using var scope = sp.CreateScope();
                var reportService = scope.ServiceProvider
                    .GetRequiredService<IReportService>();

                await reportService.GenerateAsync(request, ct);
            },
            cancellationToken);

        return Accepted(new { JobId = workItemId });
    }
}

public record SendEmailRequest(string To, string Subject, string Body);
public record ReportRequest(string ReportType, DateTime StartDate, DateTime EndDate);
```

### Example 3: Proper Scoped Service Consumption

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyApp.BackgroundServices;

/// <summary>
/// Demonstrates proper scoped service consumption in a background service.
/// </summary>
public class OrderProcessingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderProcessingService> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(30);

    public OrderProcessingService(
        IServiceProvider serviceProvider,
        ILogger<OrderProcessingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Order processing service started");

        using var timer = new PeriodicTimer(_pollingInterval);

        try
        {
            // Process immediately on startup
            await ProcessPendingOrdersAsync(stoppingToken);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessPendingOrdersAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Order processing service stopping");
        }
    }

    private async Task ProcessPendingOrdersAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Checking for pending orders...");

        // Create a new scope for each processing cycle
        // This ensures fresh DbContext instances and proper disposal
        await using var scope = _serviceProvider.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        // Get pending orders
        var pendingOrders = await dbContext.Orders
            .Where(o => o.Status == OrderStatus.Pending)
            .OrderBy(o => o.CreatedAt)
            .Take(10)  // Process in batches
            .ToListAsync(stoppingToken);

        if (pendingOrders.Count == 0)
        {
            _logger.LogDebug("No pending orders found");
            return;
        }

        _logger.LogInformation(
            "Processing {Count} pending orders",
            pendingOrders.Count);

        foreach (var order in pendingOrders)
        {
            // Check cancellation between items
            stoppingToken.ThrowIfCancellationRequested();

            try
            {
                await ProcessOrderAsync(
                    order,
                    dbContext,
                    paymentService,
                    notificationService,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process order {OrderId}",
                    order.Id);

                order.Status = OrderStatus.Failed;
                order.ErrorMessage = ex.Message;
            }
        }

        // Save all changes in a single transaction
        await dbContext.SaveChangesAsync(stoppingToken);
    }

    private async Task ProcessOrderAsync(
        Order order,
        OrderDbContext dbContext,
        IPaymentService paymentService,
        INotificationService notificationService,
        CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing order {OrderId}", order.Id);

        // Update status to processing
        order.Status = OrderStatus.Processing;
        await dbContext.SaveChangesAsync(stoppingToken);

        // Process payment
        var paymentResult = await paymentService.ProcessPaymentAsync(
            order.Id,
            order.TotalAmount,
            stoppingToken);

        if (!paymentResult.Success)
        {
            order.Status = OrderStatus.PaymentFailed;
            order.ErrorMessage = paymentResult.ErrorMessage;
            return;
        }

        order.PaymentTransactionId = paymentResult.TransactionId;

        // Mark as completed
        order.Status = OrderStatus.Completed;
        order.CompletedAt = DateTime.UtcNow;

        // Send notification (fire and forget with its own error handling)
        _ = notificationService.SendOrderConfirmationAsync(
            order.CustomerEmail,
            order.Id,
            CancellationToken.None);

        _logger.LogInformation(
            "Order {OrderId} completed successfully",
            order.Id);
    }
}

// Supporting types
public class Order
{
    public Guid Id { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public string? PaymentTransactionId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum OrderStatus
{
    Pending,
    Processing,
    Completed,
    PaymentFailed,
    Failed
}

public class OrderDbContext : DbContext
{
    public DbSet<Order> Orders => Set<Order>();

    public OrderDbContext(DbContextOptions<OrderDbContext> options)
        : base(options) { }
}

public interface IPaymentService
{
    Task<PaymentResult> ProcessPaymentAsync(
        Guid orderId,
        decimal amount,
        CancellationToken cancellationToken);
}

public record PaymentResult(
    bool Success,
    string? TransactionId,
    string? ErrorMessage);

public interface INotificationService
{
    Task SendOrderConfirmationAsync(
        string email,
        Guid orderId,
        CancellationToken cancellationToken);
}

// Registration in Program.cs
/*
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHostedService<OrderProcessingService>();
*/
```

---

## Summary

| Feature | When to Use |
|---------|-------------|
| `IHostedService` | Simple services with custom start/stop logic |
| `BackgroundService` | Long-running tasks with single execution loop |
| `IHostedLifecycleService` | Fine-grained lifecycle control (.NET 8+) |
| `PeriodicTimer` | Async-friendly periodic tasks (.NET 6+) |
| `System.Threading.Timer` | Sync callbacks, precise timing |
| Scoped services | When using DbContext or request-scoped dependencies |
| Queue-based | Decoupling work producers from consumers |
| Keyed services | Multiple implementations of same interface (.NET 8+) |

**Key best practices:**
1. Never block in `StartAsync` - return quickly
2. Always handle cancellation tokens properly
3. Create scopes for scoped dependencies
4. Implement proper error handling with retries
5. Use health checks for monitoring
6. Configure appropriate timeouts
7. Consider using Hangfire/Quartz.NET for complex scheduling

---

## Sources

- [Background tasks with hosted services in ASP.NET Core - Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-8.0)
- [Introducing the new IHostedLifecycleService Interface in .NET 8 - Steve Gordon](https://www.stevejgordon.co.uk/introducing-the-new-ihostedlifecycleservice-interface-in-dotnet-8)
- [.NET 6 breaking change: Exception handling in hosting - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/6.0/hosting-exception-handling)
- [BackgroundService Gotcha: Silent Failures - Stephen Cleary](https://blog.stephencleary.com/2020/05/backgroundservice-gotcha-silent-failure.html)
- [Understanding Background Services in .NET 8 - DEV Community](https://dev.to/moh_moh701/understanding-background-services-in-net-8-ihostedservice-and-backgroundservice-2eoh)
- [Different Ways to Run Background Tasks in ASP.NET Core - Code Maze](https://code-maze.com/aspnetcore-different-ways-to-run-background-tasks/)
- [Keyed Services in .NET 8 - Andrew Lock](https://andrewlock.net/exploring-the-dotnet-8-preview-keyed-services-dependency-injection-support/)
- [ASP.NET Core Background Jobs: Hangfire vs Quartz.NET](https://boldsign.com/blogs/aspnet-core-background-jobs-hosted-services-hangfire-quartz/)
- [Health checks in ASP.NET Core - Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [Implement background tasks in microservices - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/background-tasks-with-ihostedservice)
