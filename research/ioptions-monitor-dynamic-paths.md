# IOptionsMonitor with Dynamically Discovered Configuration Paths

> **Research Date**: 2025-01-23
> **Context**: MCP server needs hot-reloadable config where path is discovered at runtime

## Problem Statement

The MCP server starts as an stdio process with no project context. The config file path is discovered when the user calls `activate_project(projectPath)`. The config lives at `{projectPath}/.csharp-compounding-docs/config.json`.

**Challenge**: Standard IOptionsMonitor requires the config path at startup via `AddJsonFile()`.

## Solution: Custom Switchable Configuration Provider

Create a custom `IConfigurationProvider` that can have its source path changed at runtime and properly signals reloads to the configuration system.

### Architecture

```
┌─────────────────────────────────────────────────────────────┐
│ Your Services                                                │
│   - Inject IOptionsMonitor<ProjectConfig>                    │
│   - Use .CurrentValue or .OnChange()                         │
└─────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────┼───────────────────────────────┐
│ Options Layer               │                                │
│   IOptionsMonitor ←─────────┤                                │
│   IOptionsMonitorCache      │                                │
└─────────────────────────────┼───────────────────────────────┘
                              │
┌─────────────────────────────┼───────────────────────────────┐
│ Configuration Layer         │                                │
│   IConfigurationRoot ←──────┤                                │
│   SwitchableConfigProvider  │  ← SetPath() called here       │
└─────────────────────────────────────────────────────────────┘
```

### Flow

```
1. Server starts (no project active)
   └── SwitchableProvider has no path → returns empty config
   └── IOptionsMonitor<ProjectOptions> returns defaults

2. User calls activate_project("/path/to/project")
   └── ProjectActivationService.ActivateProject() called
   └── SwitchableProvider.SetPath("{path}/.csharp-compounding-docs/config.json")
       └── Loads JSON into Data dictionary
       └── Starts FileSystemWatcher
       └── Calls OnReload()
   └── IOptionsMonitorCache.Clear() forces refresh
   └── IOptionsMonitor.CurrentValue now returns project config

3. Config file changes on disk
   └── FileSystemWatcher detects change
   └── Provider.Load() re-reads file
   └── Provider.OnReload() signals change
   └── IOptionsMonitor.OnChange callbacks fire
   └── IOptionsMonitor.CurrentValue returns updated config

4. User calls activate_project("/different/project")
   └── Same flow as #2, but provider switches to new path
   └── Old FileSystemWatcher disposed, new one created

5. User calls deactivate_project()
   └── Provider.ClearPath()
   └── FileSystemWatcher disposed
   └── Data cleared
   └── OnReload() signals change
   └── IOptionsMonitor.CurrentValue returns defaults
```

## Implementation

### Interface

```csharp
public interface ISwitchableConfigurationProvider
{
    /// <summary>
    /// Sets the path to the configuration file and triggers reload.
    /// </summary>
    void SetPath(string filePath);

    /// <summary>
    /// Clears the current path, reverting to empty/default configuration.
    /// </summary>
    void ClearPath();

    /// <summary>
    /// Gets the current configuration file path, or null if not set.
    /// </summary>
    string? CurrentPath { get; }
}
```

### Custom Configuration Provider

```csharp
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

public class SwitchableJsonConfigurationProvider : ConfigurationProvider, ISwitchableConfigurationProvider, IDisposable
{
    private readonly object _lock = new();
    private string? _filePath;
    private FileSystemWatcher? _watcher;
    private readonly bool _reloadOnChange;

    public SwitchableJsonConfigurationProvider(bool reloadOnChange = true)
    {
        _reloadOnChange = reloadOnChange;
    }

    public string? CurrentPath => _filePath;

    public void SetPath(string filePath)
    {
        lock (_lock)
        {
            // Clean up previous watcher
            DisposeWatcher();

            _filePath = filePath;

            // Load the new configuration
            Load();

            // Set up file watching if enabled
            if (_reloadOnChange && File.Exists(filePath))
            {
                SetupWatcher(filePath);
            }

            // Signal that configuration has changed
            OnReload();
        }
    }

    public void ClearPath()
    {
        lock (_lock)
        {
            DisposeWatcher();
            _filePath = null;
            Data.Clear();
            OnReload();
        }
    }

    public override void Load()
    {
        Data.Clear();

        if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(_filePath);
            var jsonData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stream);

            if (jsonData != null)
            {
                foreach (var kvp in FlattenJson(jsonData, string.Empty))
                {
                    Data[kvp.Key] = kvp.Value;
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't throw - return empty config
            Console.Error.WriteLine($"Error loading config from {_filePath}: {ex.Message}");
        }
    }

    private void SetupWatcher(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        var fileName = Path.GetFileName(filePath);

        if (string.IsNullOrEmpty(directory)) return;

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.EnableRaisingEvents = true;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: file system can fire multiple events
        Task.Delay(100).ContinueWith(_ =>
        {
            lock (_lock)
            {
                Load();
                OnReload();
            }
        });
    }

    private void DisposeWatcher()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileChanged;
            _watcher.Created -= OnFileChanged;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    private static IEnumerable<KeyValuePair<string, string?>> FlattenJson(
        Dictionary<string, JsonElement> data,
        string prefix)
    {
        foreach (var kvp in data)
        {
            var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}:{kvp.Key}";

            switch (kvp.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    var nested = kvp.Value.Deserialize<Dictionary<string, JsonElement>>();
                    if (nested != null)
                    {
                        foreach (var item in FlattenJson(nested, key))
                        {
                            yield return item;
                        }
                    }
                    break;

                case JsonValueKind.Array:
                    var index = 0;
                    foreach (var element in kvp.Value.EnumerateArray())
                    {
                        yield return new KeyValuePair<string, string?>($"{key}:{index}", element.ToString());
                        index++;
                    }
                    break;

                default:
                    yield return new KeyValuePair<string, string?>(key, kvp.Value.ToString());
                    break;
            }
        }
    }

    public void Dispose()
    {
        DisposeWatcher();
    }
}
```

### Configuration Source

```csharp
public class SwitchableJsonConfigurationSource : IConfigurationSource
{
    private readonly SwitchableJsonConfigurationProvider _provider;

    public SwitchableJsonConfigurationSource(SwitchableJsonConfigurationProvider provider)
    {
        _provider = provider;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return _provider;
    }
}
```

### Service Registration

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSwitchableProjectConfiguration(
        this IServiceCollection services,
        IConfigurationManager configuration)
    {
        // Create the switchable provider
        var switchableProvider = new SwitchableJsonConfigurationProvider(reloadOnChange: true);

        // Add to configuration system
        ((IConfigurationBuilder)configuration).Add(
            new SwitchableJsonConfigurationSource(switchableProvider));

        // Register as a service for injection
        services.AddSingleton<ISwitchableConfigurationProvider>(switchableProvider);
        services.AddSingleton(switchableProvider);

        // Bind options to the "Project" section
        services.Configure<ProjectOptions>(configuration.GetSection("Project"));

        return services;
    }
}
```

### Project Activation Service

```csharp
public class ProjectActivationService
{
    private readonly ISwitchableConfigurationProvider _configProvider;
    private readonly IOptionsMonitorCache<ProjectOptions> _optionsCache;
    private readonly ILogger<ProjectActivationService> _logger;

    public ProjectActivationService(
        ISwitchableConfigurationProvider configProvider,
        IOptionsMonitorCache<ProjectOptions> optionsCache,
        ILogger<ProjectActivationService> logger)
    {
        _configProvider = configProvider;
        _optionsCache = optionsCache;
        _logger = logger;
    }

    public void ActivateProject(string projectPath)
    {
        var configPath = Path.Combine(
            projectPath,
            ".csharp-compounding-docs",
            "config.json");

        _logger.LogInformation("Activating project with config: {ConfigPath}", configPath);

        // Update the configuration source
        _configProvider.SetPath(configPath);

        // Force IOptionsMonitor to refresh immediately
        _optionsCache.Clear();

        _logger.LogInformation("Project activated: {ProjectPath}", projectPath);
    }

    public void DeactivateProject()
    {
        _logger.LogInformation("Deactivating current project");

        _configProvider.ClearPath();
        _optionsCache.Clear();
    }
}
```

### Consuming the Configuration

```csharp
public class MyMcpToolHandler
{
    private readonly IOptionsMonitor<ProjectOptions> _options;

    public MyMcpToolHandler(IOptionsMonitor<ProjectOptions> options)
    {
        _options = options;

        // Optional: React to changes
        _options.OnChange(newOptions =>
        {
            Console.WriteLine($"Configuration changed! New value: {newOptions.SomeSetting}");
        });
    }

    public void HandleToolCall()
    {
        // Always gets current value, even after project switch
        var currentOptions = _options.CurrentValue;

        // Use options...
    }
}
```

### Program.cs Setup

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Add the switchable configuration provider
builder.Services.AddSwitchableProjectConfiguration(builder.Configuration);

// Register project activation service
builder.Services.AddSingleton<ProjectActivationService>();

// Register your MCP handlers
builder.Services.AddSingleton<MyMcpToolHandler>();

// Build and run
var host = builder.Build();
await host.RunAsync();
```

## Considerations

| Topic | Notes |
|-------|-------|
| **Thread Safety** | Provider uses locking for path changes. For high-frequency access, consider `ReaderWriterLockSlim` |
| **File Doesn't Exist** | Provider handles gracefully by returning empty config. Consider watching directory for file creation |
| **Validation** | Consider adding `IValidateOptions<T>` to validate configuration on load |
| **Error Handling** | Example logs to stderr. In production, inject `ILogger<T>` |
| **Multiple Sections** | All options types from the same file automatically update when provider reloads |

## Required Packages

```xml
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
```

## Sources

- [.NET Configuration with IOptions, IOptionsMonitor, and IOptionsSnapshot](https://medium.com/@ludmal/net-configuration-with-ioptions-ioptionsmonitor-and-ioptionssnapshot-76e0efb0ad87)
- [Understanding IOptions, IOptionsSnapshot, and IOptionsMonitor](https://gavilan.blog/2025/03/25/understanding-ioptions-ioptionssnapshot-and-ioptionsmonitor/)
- [ConfigurationManager in .NET 6 (Andrew Lock)](https://andrewlock.net/exploring-dotnet-6-part-1-looking-inside-configurationmanager-in-dotnet-6/)
- [Microsoft Learn: Options pattern in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options)
- [Microsoft Learn: Configuration providers](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers)
- [Microsoft Learn: Custom configuration provider](https://learn.microsoft.com/en-us/dotnet/core/extensions/custom-configuration-provider)
- [IOptionsMonitorCache Interface](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.options.ioptionsmonitorcache-1)
