# .NET Generic Host with MCP C# SDK Research Report

## Executive Summary

This report provides comprehensive guidance on using the .NET Generic Host with the MCP C# SDK to build MCP stdio servers. The research covers host configuration patterns, dependency injection, configuration management, logging (with critical guidance for stdio transport), hosted services, and complete implementation examples.

**Key Finding**: When using stdio transport, ALL logging must be directed to stderr because stdout is reserved for MCP protocol messages.

---

## Table of Contents

1. [.NET Generic Host Overview](#1-net-generic-host-overview)
2. [Dependency Injection with Generic Host](#2-dependency-injection-with-generic-host)
3. [Configuration with Generic Host](#3-configuration-with-generic-host)
4. [Logging with Generic Host](#4-logging-with-generic-host)
5. [Hosted Services](#5-hosted-services)
6. [MCP Server Integration Patterns](#6-mcp-server-integration-patterns)
7. [Complete Implementation Examples](#7-complete-implementation-examples)
8. [Best Practices](#8-best-practices)
9. [Sources](#sources)

---

## 1. .NET Generic Host Overview

### What is the Generic Host?

The .NET Generic Host is responsible for app startup and lifetime management. It encapsulates an app's resources and lifetime functionality including:

- Dependency injection (DI)
- Logging
- Configuration
- App shutdown
- `IHostedService` implementations

### Host Builder Patterns

#### IHostApplicationBuilder (Modern Pattern - Recommended)

```csharp
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();
```

#### IHostBuilder (Legacy Pattern)

```csharp
using Microsoft.Extensions.Hosting;

IHostBuilder builder = Host.CreateDefaultBuilder(args);
builder.ConfigureServices(services =>
{
    services.AddHostedService<Worker>();
});

IHost host = builder.Build();
host.Run();
```

### Builder Comparison

| Method | Interface | Pattern | Use Case |
|--------|-----------|---------|----------|
| `Host.CreateApplicationBuilder()` | `IHostApplicationBuilder` | Modern | New projects (recommended) |
| `Host.CreateDefaultBuilder()` | `IHostBuilder` | Legacy | Existing projects, migrations |
| `Host.CreateEmptyApplicationBuilder()` | `IHostApplicationBuilder` | Minimal | Full control, no defaults |

### Default Configuration

Both `CreateApplicationBuilder` and `CreateDefaultBuilder` set identical defaults:

- **Content root**: `GetCurrentDirectory()`
- **Host configuration from**:
  - Environment variables prefixed with `DOTNET_`
  - Command-line arguments
- **App configuration from**:
  - `appsettings.json`
  - `appsettings.{Environment}.json`
  - Secret Manager (Development environment only)
  - Environment variables
  - Command-line arguments
- **Logging providers**: Console, Debug, EventSource, EventLog (Windows only)
- **Scope and dependency validation** in Development environment

### Host Lifecycle

#### Running the Host

```csharp
// Synchronous (blocks until shutdown)
host.Run();

// Asynchronous (blocks until shutdown)
await host.RunAsync();

// Start without blocking
await host.StartAsync();

// Wait for shutdown signal
await host.WaitForShutdownAsync();
```

#### Lifecycle Sequence

**Startup Order:**
1. `IHostedLifecycleService.StartingAsync`
2. `IHostedService.StartAsync`
3. `IHostedLifecycleService.StartedAsync`
4. `IHostApplicationLifetime.ApplicationStarted`

**Shutdown Order (e.g., Ctrl+C):**
1. `IHostApplicationLifetime.ApplicationStopping`
2. `IHostedLifecycleService.StoppingAsync`
3. `IHostedService.StopAsync`
4. `IHostedLifecycleService.StoppedAsync`
5. `IHostApplicationLifetime.ApplicationStopped`

#### Complete Lifecycle Example

```csharp
public sealed class ExampleHostedService : IHostedService, IHostedLifecycleService
{
    private readonly ILogger _logger;

    public ExampleHostedService(
        ILogger<ExampleHostedService> logger,
        IHostApplicationLifetime appLifetime)
    {
        _logger = logger;

        appLifetime.ApplicationStarted.Register(OnStarted);
        appLifetime.ApplicationStopping.Register(OnStopping);
        appLifetime.ApplicationStopped.Register(OnStopped);
    }

    // Startup sequence
    Task IHostedLifecycleService.StartingAsync(CancellationToken ct)
    {
        _logger.LogInformation("1. StartingAsync called.");
        return Task.CompletedTask;
    }

    Task IHostedService.StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("2. StartAsync called.");
        return Task.CompletedTask;
    }

    Task IHostedLifecycleService.StartedAsync(CancellationToken ct)
    {
        _logger.LogInformation("3. StartedAsync called.");
        return Task.CompletedTask;
    }

    private void OnStarted() => _logger.LogInformation("4. OnStarted called.");

    // Shutdown sequence
    private void OnStopping() => _logger.LogInformation("5. OnStopping called.");

    Task IHostedLifecycleService.StoppingAsync(CancellationToken ct)
    {
        _logger.LogInformation("6. StoppingAsync called.");
        return Task.CompletedTask;
    }

    Task IHostedService.StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("7. StopAsync called.");
        return Task.CompletedTask;
    }

    Task IHostedLifecycleService.StoppedAsync(CancellationToken ct)
    {
        _logger.LogInformation("8. StoppedAsync called.");
        return Task.CompletedTask;
    }

    private void OnStopped() => _logger.LogInformation("9. OnStopped called.");
}
```

### Core Host Services

| Service | Purpose |
|---------|---------|
| `IHostApplicationLifetime` | Handle post-startup and graceful shutdown tasks |
| `IHostLifetime` | Controls when host starts and stops |
| `IHostEnvironment` | Provides environment information |

### IHostApplicationLifetime

```csharp
public interface IHostApplicationLifetime
{
    CancellationToken ApplicationStarted { get; }
    CancellationToken ApplicationStopping { get; }
    CancellationToken ApplicationStopped { get; }
    void StopApplication();
}
```

**Usage Example:**
```csharp
public class MyService
{
    public MyService(IHostApplicationLifetime lifetime)
    {
        lifetime.ApplicationStopping.Register(() =>
        {
            // Cleanup before shutdown
        });
    }
}
```

### IHostEnvironment

```csharp
public interface IHostEnvironment
{
    string ApplicationName { get; }
    IFileProvider ContentRootFileProvider { get; }
    string ContentRootPath { get; }
    string EnvironmentName { get; }
}

// Extension methods
environment.IsDevelopment();
environment.IsProduction();
environment.IsStaging();
environment.IsEnvironment("Custom");
```

---

## 2. Dependency Injection with Generic Host

### Service Registration

```csharp
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Interface and implementation
builder.Services.AddSingleton<IMessageWriter, MessageWriter>();

// Factory with dependency resolution
builder.Services.AddSingleton<IMessageWriter>(sp =>
    new MessageWriter(sp.GetRequiredService<ILogger<MessageWriter>>()));

// Implementation only
builder.Services.AddSingleton<MyService>();

// Instance registration
builder.Services.AddSingleton<IMessageWriter>(new MessageWriter());
```

### Service Lifetimes

| Lifetime | Registration | Behavior | Disposal |
|----------|--------------|----------|----------|
| **Transient** | `AddTransient` | New instance each request | End of scope |
| **Scoped** | `AddScoped` | Once per scope/request | End of scope |
| **Singleton** | `AddSingleton` | Once for app lifetime | App shutdown |

#### Transient Services

```csharp
builder.Services.AddTransient<IMyService, MyService>();
```

- Created each time requested
- Use for stateless, lightweight services
- Common uses: utility services, formatting, calculations

#### Scoped Services

```csharp
builder.Services.AddScoped<IMyService, MyService>();
```

- Created once per scope (web request in HTTP scenarios)
- Default for Entity Framework `DbContext`
- **Critical**: Must be used within an explicit scope in non-HTTP scenarios

#### Singleton Services

```csharp
builder.Services.AddSingleton<IMyService, MyService>();
```

- Created once, reused for entire app lifetime
- **Must be thread-safe**
- Consider memory implications
- Common uses: configuration, caching, shared state

### Scoped Services in Non-HTTP Scenarios (Critical for MCP)

**Problem**: MCP tools run in a singleton context, but you may need scoped services like `DbContext`.

**Solution**: Use `IServiceScopeFactory` to create explicit scopes.

```csharp
public sealed class Worker(
    ILogger<Worker> logger,
    IServiceScopeFactory serviceScopeFactory)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Create explicit scope for scoped services
            using IServiceScope scope = serviceScopeFactory.CreateScope();

            try
            {
                var dbContext = scope.ServiceProvider
                    .GetRequiredService<MyDbContext>();

                var processor = scope.ServiceProvider
                    .GetRequiredService<IDataProcessor>();

                await processor.ProcessAsync(stoppingToken);
            }
            finally
            {
                logger.LogInformation("Scope disposed, provider hash: {Hash}",
                    scope.ServiceProvider.GetHashCode());
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

### Avoiding Captive Dependencies

**Never inject a Scoped service into a Singleton**. This causes the scoped service to behave like a singleton, leading to:
- Memory leaks
- Stale data
- Connection issues

```csharp
// BAD - Captive dependency
public class MySingleton
{
    public MySingleton(MyScopedService scoped) // Will throw in Development!
    {
    }
}

// GOOD - Use IServiceScopeFactory
public class MySingleton
{
    private readonly IServiceScopeFactory _scopeFactory;

    public MySingleton(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task DoWorkAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var scoped = scope.ServiceProvider.GetRequiredService<MyScopedService>();
        await scoped.ProcessAsync();
    }
}
```

### Multiple Implementations

```csharp
builder.Services.AddSingleton<IMessageWriter, ConsoleMessageWriter>();
builder.Services.AddSingleton<IMessageWriter, LoggingMessageWriter>();

public sealed class ExampleService
{
    public ExampleService(
        IMessageWriter messageWriter,           // Last registered (LoggingMessageWriter)
        IEnumerable<IMessageWriter> writers)    // All registered
    {
    }
}
```

### Keyed Services (.NET 8+)

```csharp
builder.Services.AddKeyedSingleton<ICache, MemoryCache>("memory");
builder.Services.AddKeyedSingleton<ICache, RedisCache>("redis");

public class MyService
{
    public MyService(
        [FromKeyedServices("redis")] ICache cache)
    {
    }
}
```

### TryAdd Methods

Register only if not already registered:

```csharp
services.TryAddSingleton<IMyService, MyService>();
services.TryAddEnumerable(ServiceDescriptor.Singleton<IPlugin, MyPlugin>());
```

---

## 3. Configuration with Generic Host

### Configuration Sources (Default Priority)

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Secret Manager (Development only)
4. Environment variables
5. Command-line arguments (highest priority)

### Accessing Configuration

```csharp
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Direct access
string connectionString = builder.Configuration["ConnectionStrings:Default"];
IConfigurationSection section = builder.Configuration.GetSection("MySettings");

// Add custom sources
builder.Configuration.AddJsonFile("custom.json", optional: true);
builder.Configuration.AddEnvironmentVariables(prefix: "MYAPP_");
```

### Options Pattern

#### Basic Options Class

```csharp
public sealed class MyOptions
{
    public const string SectionName = "MySettings";

    public string ApiKey { get; set; } = string.Empty;
    public int MaxRetries { get; set; } = 3;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
```

#### Registration

```csharp
builder.Services.Configure<MyOptions>(
    builder.Configuration.GetSection(MyOptions.SectionName));
```

#### appsettings.json

```json
{
  "MySettings": {
    "ApiKey": "secret-key",
    "MaxRetries": 5,
    "Timeout": "00:01:00"
  }
}
```

### Options Interfaces Comparison

| Interface | Lifetime | Reloads | Inject into Singletons | Use Case |
|-----------|----------|---------|------------------------|----------|
| `IOptions<T>` | Singleton | No | Yes | Static config |
| `IOptionsSnapshot<T>` | Scoped | Per request | No | Request-scoped config |
| `IOptionsMonitor<T>` | Singleton | Immediate | Yes | Dynamic config |

#### IOptions<T> (Static Configuration)

```csharp
public class MyService
{
    private readonly MyOptions _options;

    public MyService(IOptions<MyOptions> options)
    {
        _options = options.Value; // Cached forever
    }
}
```

#### IOptionsSnapshot<T> (Per-Request)

```csharp
public class MyScopedService
{
    private readonly MyOptions _options;

    public MyScopedService(IOptionsSnapshot<MyOptions> options)
    {
        _options = options.Value; // Fresh per scope/request
    }
}
```

#### IOptionsMonitor<T> (Real-time Updates)

```csharp
public class MySingletonService
{
    private readonly IOptionsMonitor<MyOptions> _monitor;

    public MySingletonService(IOptionsMonitor<MyOptions> monitor)
    {
        _monitor = monitor;

        // Subscribe to changes
        monitor.OnChange(options =>
        {
            Console.WriteLine($"Config changed: {options.ApiKey}");
        });
    }

    public void DoWork()
    {
        var current = _monitor.CurrentValue; // Always latest
    }
}
```

### Options Validation

#### Using Data Annotations

```csharp
using System.ComponentModel.DataAnnotations;

public sealed class MyOptions
{
    [Required]
    public required string ApiKey { get; set; }

    [Range(1, 10)]
    public int MaxRetries { get; set; } = 3;
}

// Registration with validation
builder.Services
    .AddOptions<MyOptions>()
    .Bind(builder.Configuration.GetSection("MySettings"))
    .ValidateDataAnnotations();
```

#### Validation on Startup

```csharp
builder.Services
    .AddOptionsWithValidateOnStart<MyOptions>()
    .Bind(builder.Configuration.GetSection("MySettings"))
    .ValidateDataAnnotations()
    .Validate(options => options.MaxRetries > 0, "MaxRetries must be positive");
```

#### Custom IValidateOptions

```csharp
public class MyOptionsValidator : IValidateOptions<MyOptions>
{
    public ValidateOptionsResult Validate(string? name, MyOptions options)
    {
        if (string.IsNullOrEmpty(options.ApiKey))
        {
            return ValidateOptionsResult.Fail("ApiKey is required");
        }
        return ValidateOptionsResult.Success;
    }
}

// Register validator
builder.Services.TryAddEnumerable(
    ServiceDescriptor.Singleton<IValidateOptions<MyOptions>, MyOptionsValidator>());
```

### Named Options

```csharp
// Configuration
{
  "Storage": {
    "Primary": { "ConnectionString": "..." },
    "Backup": { "ConnectionString": "..." }
  }
}

// Registration
builder.Services.Configure<StorageOptions>("Primary",
    config.GetSection("Storage:Primary"));
builder.Services.Configure<StorageOptions>("Backup",
    config.GetSection("Storage:Backup"));

// Usage
public class StorageService
{
    public StorageService(IOptionsSnapshot<StorageOptions> options)
    {
        var primary = options.Get("Primary");
        var backup = options.Get("Backup");
    }
}
```

---

## 4. Logging with Generic Host

### CRITICAL: Logging to stderr for stdio Transport

**When using stdio transport for MCP, ALL logging MUST go to stderr because stdout is reserved for MCP protocol messages.**

```csharp
var builder = Host.CreateApplicationBuilder(args);

// CRITICAL: Configure ALL logs to stderr for stdio transport
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
```

### LogToStandardErrorThreshold

| Value | Behavior |
|-------|----------|
| `LogLevel.None` (default) | All logs to stdout |
| `LogLevel.Trace` | ALL logs to stderr |
| `LogLevel.Error` | Error and Critical to stderr, others to stdout |

### Basic Logging Configuration

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Clear default providers
builder.Logging.ClearProviders();

// Add specific providers
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace; // All to stderr
});
builder.Logging.AddDebug();

// Set minimum level
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Filter by category
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);
builder.Logging.AddFilter("MyApp", LogLevel.Debug);
```

### Configuration via appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
    },
    "Console": {
      "LogToStandardErrorThreshold": "Trace",
      "FormatterName": "simple",
      "FormatterOptions": {
        "SingleLine": true,
        "TimestampFormat": "HH:mm:ss ",
        "UseUtcTimestamp": true
      }
    }
  }
}
```

### Using ILogger

```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }

    public void DoWork()
    {
        _logger.LogInformation("Starting work at {Time}", DateTime.UtcNow);

        try
        {
            // Work
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during work");
            throw;
        }
    }
}
```

### Log Levels

| Level | Value | Use Case |
|-------|-------|----------|
| Trace | 0 | Most detailed, debugging |
| Debug | 1 | Development debugging |
| Information | 2 | General flow |
| Warning | 3 | Abnormal events |
| Error | 4 | Failures |
| Critical | 5 | System failures |
| None | 6 | Disable logging |

---

## 5. Hosted Services

### IHostedService Interface

```csharp
public interface IHostedService
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
```

### BackgroundService Base Class

```csharp
public abstract class BackgroundService : IHostedService
{
    protected abstract Task ExecuteAsync(CancellationToken stoppingToken);
}
```

### Basic Background Service

```csharp
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {Time}", DateTimeOffset.Now);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                break;
            }
        }
    }
}

// Registration
builder.Services.AddHostedService<Worker>();
```

### Consuming Scoped Services

```csharp
public class ScopedWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScopedWorker> _logger;

    public ScopedWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ScopedWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();

            var processor = scope.ServiceProvider
                .GetRequiredService<IScopedProcessor>();

            await processor.ProcessAsync(stoppingToken);

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

### Timed Background Service with PeriodicTimer

```csharp
public class TimedWorker : BackgroundService
{
    private readonly ILogger<TimedWorker> _logger;
    private int _executionCount;

    public TimedWorker(ILogger<TimedWorker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(30));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var count = Interlocked.Increment(ref _executionCount);
                _logger.LogInformation("Timed work #{Count}", count);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Timed worker stopping");
        }
    }
}
```

### Graceful Shutdown

**Default Timeout**: 5 seconds (configurable)

```csharp
// Extend shutdown timeout
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});
```

**Handling Shutdown in BackgroundService**:

```csharp
public class GracefulWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Check cancellation frequently
            if (stoppingToken.IsCancellationRequested)
                break;

            await DoWorkAsync(stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Cleanup logic here
        await base.StopAsync(cancellationToken);
    }
}
```

---

## 6. MCP Server Integration Patterns

### Required NuGet Packages

```xml
<PackageReference Include="ModelContextProtocol" Version="*-*" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" />
```

### Basic MCP Server Setup

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// CRITICAL: Route ALL logs to stderr (stdout is for MCP protocol)
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Configure MCP server with stdio transport
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
```

### Tool Registration Patterns

#### Attribute-Based Discovery (Recommended)

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
public static class MyTools
{
    [McpServerTool]
    [Description("Echoes the input message back")]
    public static string Echo(
        [Description("The message to echo")] string message)
    {
        return $"Echo: {message}";
    }

    [McpServerTool(Name = "get_data")]
    [Description("Retrieves data from the database")]
    public static async Task<string> GetData(
        [Description("The data ID")] int id,
        CancellationToken cancellationToken)
    {
        // Async operations supported
        await Task.Delay(100, cancellationToken);
        return $"Data for ID: {id}";
    }
}
```

#### Tool with Dependency Injection

```csharp
[McpServerToolType]
public static class DataTools
{
    [McpServerTool]
    [Description("Fetches content from a URL")]
    public static async Task<string> FetchUrl(
        HttpClient httpClient,  // Injected via DI
        ILogger<DataTools> logger,  // Injected via DI
        [Description("The URL to fetch")] string url,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Fetching URL: {Url}", url);
        return await httpClient.GetStringAsync(url, cancellationToken);
    }
}

// Registration
builder.Services.AddHttpClient();
```

#### Tool with McpServer Access

```csharp
[McpServerToolType]
public static class SamplingTools
{
    [McpServerTool]
    [Description("Summarizes content using LLM")]
    public static async Task<string> Summarize(
        McpServer thisServer,  // Access to MCP server for sampling
        [Description("Content to summarize")] string content,
        CancellationToken cancellationToken)
    {
        // Use server's sampling capability
        var chatClient = thisServer.AsSamplingChatClient();
        var response = await chatClient.GetResponseAsync(
            $"Summarize: {content}",
            cancellationToken: cancellationToken);
        return response.Text;
    }
}
```

### Resource Registration

```csharp
[McpServerResourceType]
public static class MyResources
{
    [McpServerResource("config://app")]
    [Description("Application configuration")]
    public static string GetConfig()
    {
        return "{ \"version\": \"1.0\" }";
    }
}
```

### Prompt Registration

```csharp
using Microsoft.Extensions.AI;

[McpServerPromptType]
public static class MyPrompts
{
    [McpServerPrompt]
    [Description("Creates a summarization prompt")]
    public static ChatMessage Summarize(
        [Description("Content to summarize")] string content)
    {
        return new ChatMessage(ChatRole.User, $"Please summarize: {content}");
    }
}
```

### Low-Level Server Configuration

```csharp
using ModelContextProtocol.Server;

McpServerOptions options = new()
{
    ServerInfo = new Implementation
    {
        Name = "MyServer",
        Version = "1.0.0"
    },
    Handlers = new McpServerHandlers
    {
        ListToolsHandler = (request, ct) =>
        {
            // Custom tool listing
            return Task.FromResult(new ListToolsResult { Tools = [...] });
        },
        CallToolHandler = async (request, ct) =>
        {
            // Custom tool invocation
            return new CallToolResult { Content = [...] };
        }
    }
};

await using McpServer server = McpServer.Create(
    new StdioServerTransport("MyServer"), options);
await server.RunAsync();
```

### Service Lifetimes for MCP Tools

| Service Type | Recommended Lifetime | Notes |
|--------------|---------------------|-------|
| Stateless utilities | Singleton | Thread-safe required |
| HttpClient | Singleton (via factory) | Use `AddHttpClient()` |
| DbContext | Scoped | Use `IServiceScopeFactory` |
| Per-call state | Transient | Fresh each invocation |

### Using Scoped Services in MCP Tools

```csharp
[McpServerToolType]
public class DatabaseTools
{
    [McpServerTool]
    [Description("Queries the database")]
    public static async Task<string> QueryDatabase(
        IServiceScopeFactory scopeFactory,  // Injected
        [Description("SQL query")] string query,
        CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MyDbContext>();

        // Use dbContext within scope
        var result = await dbContext.Database
            .SqlQueryRaw<string>(query)
            .ToListAsync(cancellationToken);

        return string.Join("\n", result);
    }
}
```

---

## 7. Complete Implementation Examples

### Minimal MCP Server

```csharp
// Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);

// CRITICAL: All logs to stderr
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

[McpServerToolType]
public static class EchoTool
{
    [McpServerTool]
    [Description("Echoes the message back")]
    public static string Echo(string message) => $"Hello: {message}";
}
```

### Full-Featured MCP Server for RAG

```csharp
// Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);

// ===== LOGGING =====
// CRITICAL: All logs to stderr for stdio transport
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Logging.SetMinimumLevel(LogLevel.Information);

// ===== CONFIGURATION =====
builder.Services.Configure<RagOptions>(
    builder.Configuration.GetSection(RagOptions.SectionName));
builder.Services
    .AddOptionsWithValidateOnStart<RagOptions>()
    .ValidateDataAnnotations();

// ===== SERVICES =====
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IVectorStore, InMemoryVectorStore>();
builder.Services.AddSingleton<IEmbeddingService, EmbeddingService>();
builder.Services.AddScoped<IDocumentProcessor, DocumentProcessor>();

// ===== MCP SERVER =====
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// ===== SHUTDOWN TIMEOUT =====
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

var host = builder.Build();

// Validate configuration on startup
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("RAG MCP Server starting...");

await host.RunAsync();

// ===== OPTIONS =====
public sealed class RagOptions
{
    public const string SectionName = "Rag";

    [Required]
    public required string EmbeddingModel { get; set; }

    [Range(1, 100)]
    public int TopK { get; set; } = 5;

    public double SimilarityThreshold { get; set; } = 0.7;
}

// ===== SERVICES =====
public interface IVectorStore
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] embedding, int topK, CancellationToken ct);
    Task IndexAsync(string id, string content, float[] embedding, CancellationToken ct);
}

public interface IEmbeddingService
{
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct);
}

public interface IDocumentProcessor
{
    Task<ProcessedDocument> ProcessAsync(string content, CancellationToken ct);
}

// ===== MCP TOOLS =====
[McpServerToolType]
public static class RagTools
{
    [McpServerTool(Name = "search_documents")]
    [Description("Search indexed documents using semantic similarity")]
    public static async Task<string> SearchDocuments(
        IVectorStore vectorStore,
        IEmbeddingService embeddingService,
        IOptions<RagOptions> options,
        ILogger<RagTools> logger,
        [Description("The search query")] string query,
        [Description("Number of results (default: from config)")] int? topK,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Searching for: {Query}", query);

        var embedding = await embeddingService.GetEmbeddingAsync(query, cancellationToken);
        var results = await vectorStore.SearchAsync(
            embedding,
            topK ?? options.Value.TopK,
            cancellationToken);

        logger.LogInformation("Found {Count} results", results.Count);

        return JsonSerializer.Serialize(results);
    }

    [McpServerTool(Name = "index_document")]
    [Description("Index a document for later retrieval")]
    public static async Task<string> IndexDocument(
        IServiceScopeFactory scopeFactory,  // For scoped services
        IVectorStore vectorStore,
        IEmbeddingService embeddingService,
        ILogger<RagTools> logger,
        [Description("Unique document ID")] string documentId,
        [Description("Document content to index")] string content,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Indexing document: {Id}", documentId);

        // Use scoped service for document processing
        using var scope = scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IDocumentProcessor>();

        var processed = await processor.ProcessAsync(content, cancellationToken);
        var embedding = await embeddingService.GetEmbeddingAsync(
            processed.Content, cancellationToken);

        await vectorStore.IndexAsync(documentId, processed.Content, embedding, cancellationToken);

        return $"Indexed document {documentId} successfully";
    }
}

// ===== MCP RESOURCES =====
[McpServerResourceType]
public static class RagResources
{
    [McpServerResource("stats://index")]
    [Description("Current index statistics")]
    public static string GetIndexStats(IVectorStore vectorStore)
    {
        // Return index statistics
        return "{ \"documentCount\": 0 }";
    }
}
```

### appsettings.json for RAG Server

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    },
    "Console": {
      "LogToStandardErrorThreshold": "Trace"
    }
  },
  "Rag": {
    "EmbeddingModel": "text-embedding-ada-002",
    "TopK": 5,
    "SimilarityThreshold": 0.7
  }
}
```

### Client Configuration (mcp.json)

```json
{
  "servers": {
    "rag-server": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/RagMcpServer.csproj"],
      "env": {
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  }
}
```

---

## 8. Best Practices

### Service Registration Order

1. Configuration and options first
2. Core services (logging, HTTP clients)
3. Business services
4. MCP server registration last

```csharp
// 1. Configuration
builder.Services.Configure<MyOptions>(...);

// 2. Core services
builder.Services.AddHttpClient();

// 3. Business services
builder.Services.AddSingleton<IMyService, MyService>();
builder.Services.AddScoped<IScopedService, ScopedService>();

// 4. MCP server (last)
builder.Services.AddMcpServer()...;
```

### Configuration Validation

Always validate configuration on startup:

```csharp
builder.Services
    .AddOptionsWithValidateOnStart<MyOptions>()
    .Bind(builder.Configuration.GetSection("MySettings"))
    .ValidateDataAnnotations();
```

### Startup and Shutdown Sequences

```csharp
var host = builder.Build();

// Pre-startup validation
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Server starting...");

try
{
    await host.RunAsync();
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Server crashed");
    throw;
}
finally
{
    logger.LogInformation("Server stopped");
}
```

### Error Handling in MCP Tools

```csharp
[McpServerTool]
[Description("Processes data")]
public static async Task<string> ProcessData(
    ILogger<MyTools> logger,
    [Description("Input data")] string input,
    CancellationToken cancellationToken)
{
    try
    {
        // Process
        return "Success";
    }
    catch (OperationCanceledException)
    {
        logger.LogInformation("Operation cancelled");
        throw;  // Re-throw to signal cancellation
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Processing failed for input: {Input}", input);
        return $"Error: {ex.Message}";  // Return error to client
    }
}
```

### Testing MCP Servers with Generic Host

```csharp
public class McpServerTests
{
    [Fact]
    public async Task Tool_ReturnsExpectedResult()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddMcpServer().WithToolsFromAssembly();

        // Add test doubles
        builder.Services.AddSingleton<IMyService, MockMyService>();

        var host = builder.Build();

        // Act - invoke tool directly
        var result = EchoTool.Echo("test");

        // Assert
        Assert.Equal("Hello: test", result);
    }
}
```

### Memory and Performance

1. **Use Singleton for stateless services** - Reduces allocations
2. **Dispose scopes promptly** - Prevents memory leaks
3. **Use IOptionsMonitor sparingly** - Has overhead for change detection
4. **Batch operations** - Reduce scope creation frequency

### Logging Best Practices for MCP

1. **Always use stderr** - stdout is reserved for protocol
2. **Use structured logging** - `LogInformation("Processing {Id}", id)`
3. **Log at appropriate levels** - Debug for details, Info for flow
4. **Include correlation IDs** - Track requests across tools

```csharp
[McpServerTool]
public static async Task<string> MyTool(
    ILogger<MyTools> logger,
    string input,
    CancellationToken ct)
{
    var correlationId = Guid.NewGuid().ToString("N")[..8];

    using var scope = logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = correlationId
    });

    logger.LogInformation("Starting processing");
    // ...
    logger.LogInformation("Completed processing");

    return result;
}
```

---

## Sources

### Microsoft Documentation
- [.NET Generic Host](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)
- [Dependency injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [Dependency injection guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection-guidelines)
- [Options pattern in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- [Background tasks with hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services)
- [Quickstart: Build MCP Server](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-mcp-server)

### MCP C# SDK
- [MCP C# SDK GitHub Repository](https://github.com/modelcontextprotocol/csharp-sdk)
- [Build MCP Server in C# - .NET Blog](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/)

### Community Resources
- [Extending shutdown timeout for IHostedService](https://andrewlock.net/extending-the-shutdown-timeout-setting-to-ensure-graceful-ihostedservice-shutdown/)
- [Control graceful shutdown time](https://makolyte.com/aspnetcore-control-the-graceful-shutdown-time-for-background-services/)
- [IOptions vs IOptionsSnapshot vs IOptionsMonitor](https://www.code4it.dev/blog/ioptions-ioptionsmonitor-ioptionssnapshot/)
- [Understanding IOptions pattern](https://gavilan.blog/2025/03/25/understanding-ioptions-ioptionssnapshot-and-ioptionsmonitor/)
- [Using IServiceScopeFactory](https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service)

---

## Quick Reference

### Minimal MCP Server Template

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.ComponentModel;

var builder = Host.CreateApplicationBuilder(args);

// CRITICAL: All logs to stderr
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();

[McpServerToolType]
public static class Tools
{
    [McpServerTool]
    [Description("Description for the AI")]
    public static string MyTool(
        [Description("Parameter description")] string input)
    {
        return $"Result: {input}";
    }
}
```

### Key Points Checklist

- [ ] Use `Host.CreateApplicationBuilder(args)` for modern pattern
- [ ] Configure `LogToStandardErrorThreshold = LogLevel.Trace` for stdio
- [ ] Register services in correct order
- [ ] Use `IServiceScopeFactory` for scoped services in tools
- [ ] Validate options on startup with `AddOptionsWithValidateOnStart`
- [ ] Handle `CancellationToken` in all async tools
- [ ] Configure appropriate `ShutdownTimeout`
- [ ] Use `[Description]` attributes for AI context
