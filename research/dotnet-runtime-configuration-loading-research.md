# .NET Runtime Configuration Loading Research

## Overview

This document covers comprehensive patterns for loading configuration files at arbitrary points during runtime in .NET applications using the Generic Host and `Microsoft.Extensions.DependencyInjection`. Unlike startup-time configuration, runtime configuration loading requires careful consideration of thread safety, service lifetimes, and change notification mechanisms.

## Table of Contents

1. [Runtime Configuration Loading](#1-runtime-configuration-loading)
2. [IOptions Pattern for Dynamic Configuration](#2-ioptions-pattern-for-dynamic-configuration)
3. [Dynamic Configuration Providers](#3-dynamic-configuration-providers)
4. [Patterns for Loading Config Files on Demand](#4-patterns-for-loading-config-files-on-demand)
5. [Challenges and Limitations](#5-challenges-and-limitations)
6. [Alternative Approaches](#6-alternative-approaches)
7. [Code Examples](#7-code-examples)

---

## 1. Runtime Configuration Loading

### 1.1 Using IConfigurationRoot.Reload()

The `IConfigurationRoot.Reload()` method forces configuration values to be reloaded from underlying `IConfigurationProvider` instances.

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

public class ConfigurationReloadService : BackgroundService
{
    private readonly IConfigurationRoot _configurationRoot;
    private readonly ILogger<ConfigurationReloadService> _logger;

    public ConfigurationReloadService(
        IConfiguration configuration,
        ILogger<ConfigurationReloadService> logger)
    {
        // Cast IConfiguration to IConfigurationRoot to access Reload()
        _configurationRoot = (IConfigurationRoot)configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            _logger.LogInformation("Manually reloading configuration...");
            _configurationRoot.Reload();
            _logger.LogInformation("Configuration reloaded successfully");
        }
    }
}
```

### 1.2 Adding New Configuration Sources After Host Startup

While the configuration system is primarily designed for setup during startup, you can manipulate the `Sources` collection on `IConfigurationBuilder`. With `ConfigurationManager` (introduced in .NET 6), sources are immediately loaded when added.

```csharp
using Microsoft.Extensions.Configuration;

public class DynamicConfigurationLoader
{
    private readonly IConfigurationRoot _configurationRoot;

    public DynamicConfigurationLoader(IConfiguration configuration)
    {
        _configurationRoot = (IConfigurationRoot)configuration;
    }

    /// <summary>
    /// Adds a new JSON configuration file at runtime.
    /// Note: This approach has limitations with WebApplicationBuilder.
    /// </summary>
    public void AddJsonConfigurationFile(string filePath, bool optional = true, bool reloadOnChange = true)
    {
        // Create a new configuration builder with the new source
        var builder = new ConfigurationBuilder();

        // Copy existing sources
        foreach (var source in _configurationRoot.Providers
            .Select(p => p as IConfigurationSource)
            .Where(s => s != null))
        {
            // Note: This is a simplified approach; actual implementation
            // may need to handle provider-to-source conversion
        }

        // Add the new JSON file
        builder.AddJsonFile(filePath, optional: optional, reloadOnChange: reloadOnChange);

        // Build and merge
        var newConfig = builder.Build();

        // Trigger reload to pick up new values
        _configurationRoot.Reload();
    }
}
```

### 1.3 Working with ConfigurationManager (Recommended for .NET 6+)

`ConfigurationManager` implements both `IConfigurationBuilder` and `IConfigurationRoot`, allowing sources to be immediately loaded when added:

```csharp
using Microsoft.Extensions.Configuration;

public class RuntimeConfigurationManager
{
    private readonly ConfigurationManager _configurationManager;

    public RuntimeConfigurationManager()
    {
        _configurationManager = new ConfigurationManager();
    }

    public void AddSource(IConfigurationSource source)
    {
        // With ConfigurationManager, calling Add() immediately:
        // 1. Calls Build() on the IConfigurationSource
        // 2. Calls Load() on the resulting IConfigurationProvider
        // 3. Updates the configuration
        ((IConfigurationBuilder)_configurationManager).Add(source);
    }

    public void AddJsonFile(string path, bool optional = true, bool reloadOnChange = true)
    {
        _configurationManager.AddJsonFile(path, optional, reloadOnChange);
    }

    public string? GetValue(string key) => _configurationManager[key];
}
```

### 1.4 Using Memory Configuration Provider for Runtime Updates

The `MemoryConfigurationProvider` is ideal for runtime configuration updates:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;

public class InMemoryConfigurationService
{
    private readonly Dictionary<string, string?> _configData = new();
    private readonly IConfigurationRoot _configurationRoot;
    private MemoryConfigurationSource? _memorySource;

    public InMemoryConfigurationService(IConfiguration configuration)
    {
        _configurationRoot = (IConfigurationRoot)configuration;
    }

    public void Initialize(IConfigurationBuilder builder)
    {
        _memorySource = new MemoryConfigurationSource
        {
            InitialData = _configData
        };
        builder.Add(_memorySource);
    }

    public void SetValue(string key, string value)
    {
        _configData[key] = value;
        _configurationRoot.Reload();
    }

    public void SetValues(IDictionary<string, string?> values)
    {
        foreach (var kvp in values)
        {
            _configData[kvp.Key] = kvp.Value;
        }
        _configurationRoot.Reload();
    }

    public void RemoveValue(string key)
    {
        _configData.Remove(key);
        _configurationRoot.Reload();
    }
}
```

---

## 2. IOptions Pattern for Dynamic Configuration

### 2.1 IOptions vs IOptionsSnapshot vs IOptionsMonitor

| Interface | Lifetime | Reloads | Best Use Case |
|-----------|----------|---------|---------------|
| `IOptions<T>` | Singleton | No | Static configuration that never changes |
| `IOptionsSnapshot<T>` | Scoped | Per-request | Web requests needing consistent config per request |
| `IOptionsMonitor<T>` | Singleton | Real-time | Background services, real-time updates |

### 2.2 Using IOptionsMonitor for Runtime Updates

`IOptionsMonitor<T>` is a singleton that provides real-time configuration updates:

```csharp
using Microsoft.Extensions.Options;

public class DynamicSettingsService
{
    private readonly IOptionsMonitor<AppSettings> _optionsMonitor;
    private readonly ILogger<DynamicSettingsService> _logger;
    private IDisposable? _changeListener;

    public DynamicSettingsService(
        IOptionsMonitor<AppSettings> optionsMonitor,
        ILogger<DynamicSettingsService> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;

        // Subscribe to configuration changes
        _changeListener = _optionsMonitor.OnChange((settings, name) =>
        {
            _logger.LogInformation(
                "Configuration changed. New value: {Value}, Name: {Name}",
                settings.SomeProperty,
                name ?? "default");

            // React to configuration changes
            OnConfigurationChanged(settings);
        });
    }

    public AppSettings CurrentSettings => _optionsMonitor.CurrentValue;

    private void OnConfigurationChanged(AppSettings newSettings)
    {
        // Handle configuration change
        // This is called whenever the underlying configuration changes
    }

    public void Dispose()
    {
        _changeListener?.Dispose();
    }
}

public class AppSettings
{
    public string SomeProperty { get; set; } = string.Empty;
    public int RefreshIntervalSeconds { get; set; } = 30;
    public bool FeatureEnabled { get; set; }
}
```

### 2.3 Using IOptionsSnapshot for Scoped Updates

`IOptionsSnapshot<T>` provides consistent configuration within a single request scope:

```csharp
using Microsoft.Extensions.Options;

public class ScopedConfigService
{
    private readonly IOptionsSnapshot<FeatureSettings> _optionsSnapshot;

    public ScopedConfigService(IOptionsSnapshot<FeatureSettings> optionsSnapshot)
    {
        // This snapshot is computed once per request and cached
        _optionsSnapshot = optionsSnapshot;
    }

    public FeatureSettings Settings => _optionsSnapshot.Value;

    // For named options
    public FeatureSettings GetNamedSettings(string name)
    {
        return _optionsSnapshot.Get(name);
    }
}

public class FeatureSettings
{
    public bool IsEnabled { get; set; }
    public string[] AllowedFeatures { get; set; } = Array.Empty<string>();
}
```

### 2.4 IOptionsMonitorCache for Cache Invalidation

`IOptionsMonitorCache<T>` allows manual cache manipulation:

```csharp
using Microsoft.Extensions.Options;

public class OptionsRefreshService
{
    private readonly IOptionsMonitorCache<AppSettings> _cache;
    private readonly IOptionsMonitor<AppSettings> _monitor;

    public OptionsRefreshService(
        IOptionsMonitorCache<AppSettings> cache,
        IOptionsMonitor<AppSettings> monitor)
    {
        _cache = cache;
        _monitor = monitor;
    }

    /// <summary>
    /// Forces the options to be recomputed on next access.
    /// </summary>
    public void InvalidateCache()
    {
        // Remove the default (unnamed) options instance
        _cache.TryRemove(Options.DefaultName);
    }

    /// <summary>
    /// Invalidates a named options instance.
    /// </summary>
    public void InvalidateNamedCache(string name)
    {
        _cache.TryRemove(name);
    }

    /// <summary>
    /// Clears all cached options instances.
    /// </summary>
    public void ClearAllCaches()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Manually adds a pre-configured options instance to the cache.
    /// </summary>
    public bool TryAddToCache(string name, AppSettings options)
    {
        return _cache.TryAdd(name, options);
    }

    /// <summary>
    /// Gets the current value after cache invalidation.
    /// </summary>
    public AppSettings GetFreshSettings()
    {
        InvalidateCache();
        return _monitor.CurrentValue; // Will be recomputed
    }
}
```

### 2.5 Named Options for Multiple Configurations

```csharp
// Registration
services.Configure<DatabaseSettings>("Primary", config.GetSection("Databases:Primary"));
services.Configure<DatabaseSettings>("Secondary", config.GetSection("Databases:Secondary"));
services.Configure<DatabaseSettings>("Archive", config.GetSection("Databases:Archive"));

// Usage with IOptionsMonitor
public class MultiDatabaseService
{
    private readonly IOptionsMonitor<DatabaseSettings> _optionsMonitor;

    public MultiDatabaseService(IOptionsMonitor<DatabaseSettings> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    public DatabaseSettings GetPrimarySettings() => _optionsMonitor.Get("Primary");
    public DatabaseSettings GetSecondarySettings() => _optionsMonitor.Get("Secondary");
    public DatabaseSettings GetArchiveSettings() => _optionsMonitor.Get("Archive");
}

public class DatabaseSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public int ConnectionTimeout { get; set; } = 30;
    public int MaxPoolSize { get; set; } = 100;
}
```

---

## 3. Dynamic Configuration Providers

### 3.1 Creating a Custom Reloadable Configuration Provider

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

/// <summary>
/// Configuration source that supports dynamic reloading.
/// </summary>
public class DynamicConfigurationSource : IConfigurationSource
{
    public string SourceName { get; set; } = "Dynamic";
    public TimeSpan? PollingInterval { get; set; }
    public Func<Task<Dictionary<string, string?>>>? DataLoader { get; set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DynamicConfigurationProvider(this);
    }
}

/// <summary>
/// Configuration provider that supports runtime updates and periodic polling.
/// </summary>
public class DynamicConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly DynamicConfigurationSource _source;
    private readonly Timer? _pollingTimer;
    private bool _disposed;

    public DynamicConfigurationProvider(DynamicConfigurationSource source)
    {
        _source = source;

        // Set up periodic polling if configured
        if (source.PollingInterval.HasValue && source.DataLoader != null)
        {
            _pollingTimer = new Timer(
                async _ => await ReloadAsync(),
                null,
                source.PollingInterval.Value,
                source.PollingInterval.Value);
        }
    }

    public override void Load()
    {
        LoadAsync().GetAwaiter().GetResult();
    }

    private async Task LoadAsync()
    {
        if (_source.DataLoader != null)
        {
            var data = await _source.DataLoader();
            Data = new Dictionary<string, string?>(data, StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Reloads configuration from the data source and notifies listeners.
    /// </summary>
    public async Task ReloadAsync()
    {
        await LoadAsync();
        OnReload(); // Triggers ConfigurationReloadToken
    }

    /// <summary>
    /// Manually updates a configuration value and triggers reload notification.
    /// </summary>
    public void UpdateValue(string key, string? value)
    {
        Data[key] = value;
        OnReload();
    }

    /// <summary>
    /// Bulk updates multiple configuration values.
    /// </summary>
    public void UpdateValues(IDictionary<string, string?> values)
    {
        foreach (var kvp in values)
        {
            Data[kvp.Key] = kvp.Value;
        }
        OnReload();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _pollingTimer?.Dispose();
            _disposed = true;
        }
    }
}
```

### 3.2 Extension Methods for Registration

```csharp
using Microsoft.Extensions.Configuration;

public static class DynamicConfigurationExtensions
{
    public static IConfigurationBuilder AddDynamicConfiguration(
        this IConfigurationBuilder builder,
        Action<DynamicConfigurationSource> configure)
    {
        var source = new DynamicConfigurationSource();
        configure(source);
        return builder.Add(source);
    }

    public static IConfigurationBuilder AddDynamicConfiguration(
        this IConfigurationBuilder builder,
        Func<Task<Dictionary<string, string?>>> dataLoader,
        TimeSpan? pollingInterval = null)
    {
        return builder.AddDynamicConfiguration(source =>
        {
            source.DataLoader = dataLoader;
            source.PollingInterval = pollingInterval;
        });
    }
}
```

### 3.3 Database-Backed Configuration Provider with Reload Support

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

public class DatabaseConfigurationSource : IConfigurationSource
{
    public string ConnectionString { get; set; } = string.Empty;
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(5);
    public string TableName { get; set; } = "AppConfiguration";

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new DatabaseConfigurationProvider(this);
    }
}

public class DatabaseConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly DatabaseConfigurationSource _source;
    private readonly Timer _pollingTimer;
    private bool _disposed;

    public DatabaseConfigurationProvider(DatabaseConfigurationSource source)
    {
        _source = source;
        _pollingTimer = new Timer(
            _ => ReloadFromDatabase(),
            null,
            source.PollingInterval,
            source.PollingInterval);
    }

    public override void Load()
    {
        LoadFromDatabase();
    }

    private void LoadFromDatabase()
    {
        try
        {
            using var context = CreateDbContext();

            Data = context.ConfigurationEntries
                .ToDictionary(
                    e => e.Key,
                    e => e.Value,
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            // Log error but don't throw - keep existing configuration
            System.Diagnostics.Debug.WriteLine($"Failed to load configuration: {ex.Message}");
        }
    }

    private void ReloadFromDatabase()
    {
        LoadFromDatabase();
        OnReload(); // Notify listeners of change
    }

    private ConfigurationDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ConfigurationDbContext>();
        optionsBuilder.UseSqlServer(_source.ConnectionString);
        return new ConfigurationDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Allows manual triggering of configuration reload.
    /// </summary>
    public void ForceReload()
    {
        ReloadFromDatabase();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _pollingTimer.Dispose();
            _disposed = true;
        }
    }
}

// DbContext for configuration storage
public class ConfigurationDbContext : DbContext
{
    public ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options)
        : base(options) { }

    public DbSet<ConfigurationEntry> ConfigurationEntries => Set<ConfigurationEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConfigurationEntry>(entity =>
        {
            entity.HasKey(e => e.Key);
            entity.Property(e => e.Key).HasMaxLength(256);
            entity.Property(e => e.Value).HasMaxLength(4000);
        });
    }
}

public class ConfigurationEntry
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public DateTime LastModified { get; set; }
}
```

### 3.4 Using ConfigurationReloadToken and Change Tokens

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

public class ConfigurationChangeWatcher : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationChangeWatcher> _logger;
    private IDisposable? _changeTokenRegistration;

    public ConfigurationChangeWatcher(
        IConfiguration configuration,
        ILogger<ConfigurationChangeWatcher> logger)
    {
        _configuration = configuration;
        _logger = logger;
        WatchForChanges();
    }

    private void WatchForChanges()
    {
        // Register for change notifications
        // Important: Must re-register after each callback as tokens are single-use
        _changeTokenRegistration = ChangeToken.OnChange(
            () => _configuration.GetReloadToken(),
            OnConfigurationChanged);
    }

    private void OnConfigurationChanged()
    {
        _logger.LogInformation("Configuration has been reloaded at {Time}", DateTime.UtcNow);

        // Handle the configuration change
        // Note: GetReloadToken() returns a new token after each reload
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? ConfigurationChanged;

    public void Dispose()
    {
        _changeTokenRegistration?.Dispose();
    }
}

// Alternative: Manual token watching with re-registration
public class ManualConfigurationWatcher
{
    private readonly IConfiguration _configuration;

    public ManualConfigurationWatcher(IConfiguration configuration)
    {
        _configuration = configuration;
        RegisterForChanges();
    }

    private void RegisterForChanges()
    {
        var reloadToken = _configuration.GetReloadToken();

        reloadToken.RegisterChangeCallback(_ =>
        {
            Console.WriteLine("Configuration changed!");

            // Re-register for next change (tokens are single-use)
            RegisterForChanges();
        }, state: null);
    }
}
```

---

## 4. Patterns for Loading Config Files on Demand

### 4.1 Plugin/Module Configuration Loading

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public interface IPlugin
{
    string Name { get; }
    void Configure(IServiceCollection services);
    void Execute();
}

public class PluginConfigurationManager
{
    private readonly IConfigurationRoot _configuration;
    private readonly IServiceCollection _services;
    private readonly Dictionary<string, IConfigurationRoot> _pluginConfigs = new();

    public PluginConfigurationManager(
        IConfiguration configuration,
        IServiceCollection services)
    {
        _configuration = (IConfigurationRoot)configuration;
        _services = services;
    }

    /// <summary>
    /// Loads a plugin and its configuration at runtime.
    /// </summary>
    public async Task LoadPluginAsync(string pluginPath)
    {
        var pluginDirectory = Path.GetDirectoryName(pluginPath)!;
        var pluginName = Path.GetFileNameWithoutExtension(pluginPath);

        // Look for plugin-specific configuration
        var configPath = Path.Combine(pluginDirectory, $"{pluginName}.config.json");

        if (File.Exists(configPath))
        {
            // Create plugin-specific configuration
            var pluginConfig = new ConfigurationBuilder()
                .SetBasePath(pluginDirectory)
                .AddJsonFile($"{pluginName}.config.json", optional: false, reloadOnChange: true)
                .Build();

            _pluginConfigs[pluginName] = pluginConfig;

            // Register plugin options with DI
            // Note: This requires rebuilding the service provider
            _services.Configure<PluginSettings>(
                pluginName,
                pluginConfig.GetSection("PluginSettings"));
        }

        // Load and activate the plugin assembly
        await LoadPluginAssemblyAsync(pluginPath, pluginName);
    }

    /// <summary>
    /// Unloads a plugin and removes its configuration.
    /// </summary>
    public void UnloadPlugin(string pluginName)
    {
        if (_pluginConfigs.TryGetValue(pluginName, out var config))
        {
            // Dispose of configuration providers if they implement IDisposable
            foreach (var provider in config.Providers.OfType<IDisposable>())
            {
                provider.Dispose();
            }
            _pluginConfigs.Remove(pluginName);
        }
    }

    public IConfiguration? GetPluginConfiguration(string pluginName)
    {
        return _pluginConfigs.TryGetValue(pluginName, out var config) ? config : null;
    }

    private Task LoadPluginAssemblyAsync(string pluginPath, string pluginName)
    {
        // Plugin assembly loading implementation
        // This would typically use AssemblyLoadContext
        return Task.CompletedTask;
    }
}

public class PluginSettings
{
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public Dictionary<string, string> CustomSettings { get; set; } = new();
}
```

### 4.2 Feature Flag Configuration at Runtime

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

public class FeatureFlagService
{
    private readonly IOptionsMonitor<FeatureFlagSettings> _optionsMonitor;
    private readonly IOptionsMonitorCache<FeatureFlagSettings> _cache;
    private readonly DynamicConfigurationProvider _dynamicProvider;
    private readonly ILogger<FeatureFlagService> _logger;

    public FeatureFlagService(
        IOptionsMonitor<FeatureFlagSettings> optionsMonitor,
        IOptionsMonitorCache<FeatureFlagSettings> cache,
        IServiceProvider serviceProvider,
        ILogger<FeatureFlagService> logger)
    {
        _optionsMonitor = optionsMonitor;
        _cache = cache;
        _logger = logger;

        // Get the dynamic provider for runtime updates
        var config = (IConfigurationRoot)serviceProvider.GetRequiredService<IConfiguration>();
        _dynamicProvider = config.Providers
            .OfType<DynamicConfigurationProvider>()
            .FirstOrDefault()!;
    }

    public bool IsFeatureEnabled(string featureName)
    {
        var settings = _optionsMonitor.CurrentValue;
        return settings.Features.TryGetValue(featureName, out var enabled) && enabled;
    }

    /// <summary>
    /// Enables or disables a feature flag at runtime.
    /// </summary>
    public void SetFeatureFlag(string featureName, bool enabled)
    {
        var key = $"FeatureFlags:Features:{featureName}";
        _dynamicProvider.UpdateValue(key, enabled.ToString().ToLowerInvariant());

        // Invalidate the options cache to force refresh
        _cache.TryRemove(Options.DefaultName);

        _logger.LogInformation(
            "Feature flag '{Feature}' set to {Enabled}",
            featureName,
            enabled);
    }

    /// <summary>
    /// Adds multiple feature flags at runtime.
    /// </summary>
    public void SetFeatureFlags(IDictionary<string, bool> flags)
    {
        var updates = flags.ToDictionary(
            kvp => $"FeatureFlags:Features:{kvp.Key}",
            kvp => (string?)kvp.Value.ToString().ToLowerInvariant());

        _dynamicProvider.UpdateValues(updates);
        _cache.TryRemove(Options.DefaultName);
    }

    /// <summary>
    /// Loads feature flags from an external file.
    /// </summary>
    public async Task LoadFeatureFlagsFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Feature flag file not found", filePath);
        }

        var json = await File.ReadAllTextAsync(filePath);
        var flags = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(json);

        if (flags != null)
        {
            SetFeatureFlags(flags);
        }
    }
}

public class FeatureFlagSettings
{
    public Dictionary<string, bool> Features { get; set; } = new();
    public DateTime LastModified { get; set; }
}
```

### 4.3 Multi-Tenant Configuration Pattern

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

public interface ITenantContextAccessor
{
    string? CurrentTenantId { get; }
}

public class TenantConfigurationService
{
    private readonly ConcurrentDictionary<string, IConfigurationRoot> _tenantConfigs = new();
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly string _configBasePath;
    private readonly ILogger<TenantConfigurationService> _logger;

    public TenantConfigurationService(
        ITenantContextAccessor tenantContextAccessor,
        IConfiguration configuration,
        ILogger<TenantConfigurationService> logger)
    {
        _tenantContextAccessor = tenantContextAccessor;
        _configBasePath = configuration["TenantConfigPath"] ?? "tenants";
        _logger = logger;
    }

    /// <summary>
    /// Gets or loads configuration for the current tenant.
    /// </summary>
    public IConfiguration GetCurrentTenantConfiguration()
    {
        var tenantId = _tenantContextAccessor.CurrentTenantId
            ?? throw new InvalidOperationException("No tenant context available");

        return GetTenantConfiguration(tenantId);
    }

    /// <summary>
    /// Gets or loads configuration for a specific tenant.
    /// </summary>
    public IConfiguration GetTenantConfiguration(string tenantId)
    {
        return _tenantConfigs.GetOrAdd(tenantId, LoadTenantConfiguration);
    }

    /// <summary>
    /// Loads tenant configuration from file system.
    /// </summary>
    private IConfigurationRoot LoadTenantConfiguration(string tenantId)
    {
        var tenantConfigPath = Path.Combine(_configBasePath, tenantId);

        var builder = new ConfigurationBuilder()
            .SetBasePath(tenantConfigPath);

        // Add base tenant configuration
        var baseConfigPath = Path.Combine(tenantConfigPath, "appsettings.json");
        if (File.Exists(baseConfigPath))
        {
            builder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        }

        // Add environment-specific configuration
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var envConfigPath = Path.Combine(tenantConfigPath, $"appsettings.{environment}.json");
        if (File.Exists(envConfigPath))
        {
            builder.AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true);
        }

        _logger.LogInformation("Loaded configuration for tenant {TenantId}", tenantId);

        return builder.Build();
    }

    /// <summary>
    /// Reloads configuration for a specific tenant.
    /// </summary>
    public void ReloadTenantConfiguration(string tenantId)
    {
        if (_tenantConfigs.TryGetValue(tenantId, out var config))
        {
            config.Reload();
            _logger.LogInformation("Reloaded configuration for tenant {TenantId}", tenantId);
        }
    }

    /// <summary>
    /// Adds a new tenant configuration dynamically.
    /// </summary>
    public async Task AddTenantConfigurationAsync(string tenantId, Stream configStream)
    {
        var tenantConfigPath = Path.Combine(_configBasePath, tenantId);
        Directory.CreateDirectory(tenantConfigPath);

        var configFilePath = Path.Combine(tenantConfigPath, "appsettings.json");

        await using var fileStream = File.Create(configFilePath);
        await configStream.CopyToAsync(fileStream);

        // Force reload if already loaded
        if (_tenantConfigs.TryRemove(tenantId, out var oldConfig))
        {
            // Dispose old configuration providers
            foreach (var provider in oldConfig.Providers.OfType<IDisposable>())
            {
                provider.Dispose();
            }
        }

        _logger.LogInformation("Added new configuration for tenant {TenantId}", tenantId);
    }

    /// <summary>
    /// Removes a tenant's configuration.
    /// </summary>
    public void RemoveTenantConfiguration(string tenantId)
    {
        if (_tenantConfigs.TryRemove(tenantId, out var config))
        {
            foreach (var provider in config.Providers.OfType<IDisposable>())
            {
                provider.Dispose();
            }
            _logger.LogInformation("Removed configuration for tenant {TenantId}", tenantId);
        }
    }
}

/// <summary>
/// Tenant-aware IOptionsMonitorCache implementation.
/// </summary>
public class TenantOptionsCache<TOptions> : IOptionsMonitorCache<TOptions>
    where TOptions : class
{
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Lazy<TOptions>>> _tenantCaches = new();

    public TenantOptionsCache(ITenantContextAccessor tenantContextAccessor)
    {
        _tenantContextAccessor = tenantContextAccessor;
    }

    public void Clear()
    {
        var tenantId = _tenantContextAccessor.CurrentTenantId ?? "__default";
        if (_tenantCaches.TryGetValue(tenantId, out var cache))
        {
            cache.Clear();
        }
    }

    public TOptions GetOrAdd(string? name, Func<TOptions> createOptions)
    {
        var tenantId = _tenantContextAccessor.CurrentTenantId ?? "__default";
        var tenantCache = _tenantCaches.GetOrAdd(tenantId, _ => new ConcurrentDictionary<string, Lazy<TOptions>>());

        return tenantCache.GetOrAdd(
            name ?? Options.DefaultName,
            _ => new Lazy<TOptions>(createOptions)).Value;
    }

    public bool TryAdd(string? name, TOptions options)
    {
        var tenantId = _tenantContextAccessor.CurrentTenantId ?? "__default";
        var tenantCache = _tenantCaches.GetOrAdd(tenantId, _ => new ConcurrentDictionary<string, Lazy<TOptions>>());

        return tenantCache.TryAdd(name ?? Options.DefaultName, new Lazy<TOptions>(() => options));
    }

    public bool TryRemove(string? name)
    {
        var tenantId = _tenantContextAccessor.CurrentTenantId ?? "__default";
        if (_tenantCaches.TryGetValue(tenantId, out var cache))
        {
            return cache.TryRemove(name ?? Options.DefaultName, out _);
        }
        return false;
    }
}
```

### 4.4 User-Uploadable Configuration Files

```csharp
using Microsoft.Extensions.Configuration;
using System.Text.Json;

public class UserConfigurationService
{
    private readonly IConfigurationRoot _configuration;
    private readonly string _userConfigBasePath;
    private readonly ILogger<UserConfigurationService> _logger;

    public UserConfigurationService(
        IConfiguration configuration,
        ILogger<UserConfigurationService> logger)
    {
        _configuration = (IConfigurationRoot)configuration;
        _userConfigBasePath = configuration["UserConfigPath"] ?? "userconfig";
        _logger = logger;

        Directory.CreateDirectory(_userConfigBasePath);
    }

    /// <summary>
    /// Uploads and applies a user configuration file.
    /// </summary>
    public async Task<ConfigurationUploadResult> UploadConfigurationAsync(
        string userId,
        string configName,
        Stream configStream)
    {
        var result = new ConfigurationUploadResult { Success = false };

        try
        {
            // Read and validate the JSON
            using var reader = new StreamReader(configStream);
            var json = await reader.ReadToEndAsync();

            // Validate JSON structure
            var validationResult = ValidateConfiguration(json);
            if (!validationResult.IsValid)
            {
                result.Errors = validationResult.Errors;
                return result;
            }

            // Save to user's configuration directory
            var userConfigPath = Path.Combine(_userConfigBasePath, userId);
            Directory.CreateDirectory(userConfigPath);

            var configFilePath = Path.Combine(userConfigPath, $"{configName}.json");
            await File.WriteAllTextAsync(configFilePath, json);

            // Trigger configuration reload
            _configuration.Reload();

            result.Success = true;
            result.ConfigurationPath = configFilePath;

            _logger.LogInformation(
                "User {UserId} uploaded configuration {ConfigName}",
                userId,
                configName);
        }
        catch (JsonException ex)
        {
            result.Errors = new[] { $"Invalid JSON format: {ex.Message}" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload configuration for user {UserId}", userId);
            result.Errors = new[] { "An error occurred while processing the configuration" };
        }

        return result;
    }

    /// <summary>
    /// Validates configuration JSON before applying.
    /// </summary>
    private ConfigurationValidationResult ValidateConfiguration(string json)
    {
        var result = new ConfigurationValidationResult { IsValid = true };
        var errors = new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(json);

            // Check for dangerous keys
            var dangerousKeys = new[] { "ConnectionStrings", "Secrets", "Credentials" };
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (dangerousKeys.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"Configuration key '{prop.Name}' is not allowed in user configurations");
                }
            }

            // Validate maximum depth
            if (GetJsonDepth(doc.RootElement) > 5)
            {
                errors.Add("Configuration nesting depth exceeds maximum allowed (5 levels)");
            }

            // Validate maximum size
            if (json.Length > 100_000)
            {
                errors.Add("Configuration file size exceeds maximum allowed (100KB)");
            }
        }
        catch (JsonException ex)
        {
            errors.Add($"JSON parsing error: {ex.Message}");
        }

        result.IsValid = errors.Count == 0;
        result.Errors = errors.ToArray();
        return result;
    }

    private int GetJsonDepth(JsonElement element, int currentDepth = 0)
    {
        var maxDepth = currentDepth;

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                maxDepth = Math.Max(maxDepth, GetJsonDepth(prop.Value, currentDepth + 1));
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                maxDepth = Math.Max(maxDepth, GetJsonDepth(item, currentDepth + 1));
            }
        }

        return maxDepth;
    }

    /// <summary>
    /// Lists user's configuration files.
    /// </summary>
    public IEnumerable<string> GetUserConfigurations(string userId)
    {
        var userConfigPath = Path.Combine(_userConfigBasePath, userId);
        if (!Directory.Exists(userConfigPath))
        {
            return Enumerable.Empty<string>();
        }

        return Directory.GetFiles(userConfigPath, "*.json")
            .Select(Path.GetFileNameWithoutExtension)!;
    }

    /// <summary>
    /// Deletes a user's configuration file.
    /// </summary>
    public bool DeleteUserConfiguration(string userId, string configName)
    {
        var configFilePath = Path.Combine(_userConfigBasePath, userId, $"{configName}.json");

        if (File.Exists(configFilePath))
        {
            File.Delete(configFilePath);
            _configuration.Reload();
            return true;
        }

        return false;
    }
}

public class ConfigurationUploadResult
{
    public bool Success { get; set; }
    public string? ConfigurationPath { get; set; }
    public string[]? Errors { get; set; }
}

public class ConfigurationValidationResult
{
    public bool IsValid { get; set; }
    public string[]? Errors { get; set; }
}
```

---

## 5. Challenges and Limitations

### 5.1 What Cannot Be Changed After Startup

Some aspects of configuration cannot be dynamically updated after the host has started:

1. **Service registrations** - DI container is built at startup
2. **Singleton services using `IOptions<T>`** - These capture values at first resolution
3. **Logging configuration** - Providers are configured at startup
4. **Kestrel server options** - Require server restart
5. **Middleware pipeline** - Defined at startup

```csharp
// This singleton will NOT see configuration changes
public class SingletonWithIOptions
{
    private readonly MySettings _settings;

    public SingletonWithIOptions(IOptions<MySettings> options)
    {
        // This value is captured once and never updated
        _settings = options.Value;
    }

    // _settings will always contain startup values
}

// This singleton WILL see configuration changes
public class SingletonWithIOptionsMonitor
{
    private readonly IOptionsMonitor<MySettings> _optionsMonitor;

    public SingletonWithIOptionsMonitor(IOptionsMonitor<MySettings> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;
    }

    public MySettings CurrentSettings => _optionsMonitor.CurrentValue;
}
```

### 5.2 Service Lifetime Implications

| Service Lifetime | Recommended Interface | Behavior |
|-----------------|----------------------|----------|
| Singleton | `IOptionsMonitor<T>` | Real-time updates via `CurrentValue` |
| Scoped | `IOptionsSnapshot<T>` | Consistent per-request snapshot |
| Transient | `IOptionsSnapshot<T>` or `IOptionsMonitor<T>` | Either works |

```csharp
// WRONG: IOptionsSnapshot in Singleton throws at runtime
public class BadSingletonService
{
    public BadSingletonService(IOptionsSnapshot<MySettings> options)
    {
        // This will throw: Cannot resolve scoped service from singleton
    }
}

// CORRECT: Use IOptionsMonitor in Singleton
public class GoodSingletonService
{
    private readonly IOptionsMonitor<MySettings> _options;

    public GoodSingletonService(IOptionsMonitor<MySettings> options)
    {
        _options = options;
    }
}
```

### 5.3 Thread Safety Considerations

```csharp
/// <summary>
/// Thread-safe configuration access wrapper.
/// </summary>
public class ThreadSafeConfigurationAccessor
{
    private readonly IConfiguration _configuration;
    private readonly ReaderWriterLockSlim _lock = new();

    public ThreadSafeConfigurationAccessor(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string? GetValue(string key)
    {
        _lock.EnterReadLock();
        try
        {
            return _configuration[key];
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public T? GetSection<T>(string sectionName) where T : class, new()
    {
        _lock.EnterReadLock();
        try
        {
            var section = _configuration.GetSection(sectionName);
            var value = new T();
            section.Bind(value);
            return value;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}

// Note: IOptionsMonitor<T>.CurrentValue is thread-safe and preferred
```

### 5.4 Known Issue: WebApplicationBuilder and Reload()

There is a [documented issue](https://github.com/dotnet/aspnetcore/issues/36181) where `IConfigurationRoot.Reload()` does not work correctly with `WebApplicationBuilder` due to `ChainedConfigurationProvider` wrapping.

```csharp
// Workaround for WebApplicationBuilder reload issue
public static class ConfigurationReloadWorkaround
{
    public static void ForceReload(this IConfigurationRoot configuration)
    {
        // Walk through all providers and reload each one
        foreach (var provider in configuration.Providers)
        {
            if (provider is ChainedConfigurationProvider chained)
            {
                // Access internal configuration and reload it
                var innerConfig = GetInnerConfiguration(chained);
                innerConfig?.Reload();
            }
            else
            {
                provider.Load();
            }
        }
    }

    private static IConfigurationRoot? GetInnerConfiguration(ChainedConfigurationProvider provider)
    {
        // Use reflection to access the internal Configuration property
        var configProperty = typeof(ChainedConfigurationProvider)
            .GetProperty("Configuration", BindingFlags.NonPublic | BindingFlags.Instance);

        return configProperty?.GetValue(provider) as IConfigurationRoot;
    }
}
```

### 5.5 Configuration Validation on Reload

```csharp
using Microsoft.Extensions.Options;

public class ValidatedSettings
{
    public string ApiEndpoint { get; set; } = string.Empty;
    public int MaxRetries { get; set; }
    public TimeSpan Timeout { get; set; }
}

public class ValidatedSettingsValidator : IValidateOptions<ValidatedSettings>
{
    public ValidateOptionsResult Validate(string? name, ValidatedSettings options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ApiEndpoint))
        {
            errors.Add("ApiEndpoint is required");
        }
        else if (!Uri.TryCreate(options.ApiEndpoint, UriKind.Absolute, out _))
        {
            errors.Add("ApiEndpoint must be a valid URI");
        }

        if (options.MaxRetries < 0 || options.MaxRetries > 10)
        {
            errors.Add("MaxRetries must be between 0 and 10");
        }

        if (options.Timeout < TimeSpan.Zero || options.Timeout > TimeSpan.FromMinutes(5))
        {
            errors.Add("Timeout must be between 0 and 5 minutes");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

// Registration
services.AddSingleton<IValidateOptions<ValidatedSettings>, ValidatedSettingsValidator>();
services.AddOptions<ValidatedSettings>()
    .Bind(configuration.GetSection("ValidatedSettings"))
    .ValidateOnStart(); // Validates at startup
    // Note: Validation also runs on each reload when using IOptionsMonitor
```

---

## 6. Alternative Approaches

### 6.1 Configuration Service/Repository Pattern

Instead of relying on `IConfiguration`, implement a dedicated service:

```csharp
public interface IConfigurationRepository
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value) where T : class;
    Task<bool> ExistsAsync(string key);
    Task DeleteAsync(string key);
    IAsyncEnumerable<string> GetKeysAsync(string prefix);
    event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
}

public class ConfigurationChangedEventArgs : EventArgs
{
    public string Key { get; init; } = string.Empty;
    public ChangeType ChangeType { get; init; }
}

public enum ChangeType { Added, Updated, Deleted }

public class DatabaseConfigurationRepository : IConfigurationRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;
    private readonly ILogger<DatabaseConfigurationRepository> _logger;

    public event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;

    public DatabaseConfigurationRepository(
        IDbConnectionFactory dbConnectionFactory,
        ILogger<DatabaseConfigurationRepository> logger)
    {
        _dbConnectionFactory = dbConnectionFactory;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();

        var json = await connection.QuerySingleOrDefaultAsync<string>(
            "SELECT Value FROM Configuration WHERE Key = @Key",
            new { Key = key });

        return json != null
            ? JsonSerializer.Deserialize<T>(json)
            : null;
    }

    public async Task SetAsync<T>(string key, T value) where T : class
    {
        var json = JsonSerializer.Serialize(value);
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();

        var exists = await ExistsAsync(key);

        if (exists)
        {
            await connection.ExecuteAsync(
                "UPDATE Configuration SET Value = @Value, Modified = @Modified WHERE Key = @Key",
                new { Key = key, Value = json, Modified = DateTime.UtcNow });
        }
        else
        {
            await connection.ExecuteAsync(
                "INSERT INTO Configuration (Key, Value, Created) VALUES (@Key, @Value, @Created)",
                new { Key = key, Value = json, Created = DateTime.UtcNow });
        }

        ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
        {
            Key = key,
            ChangeType = exists ? ChangeType.Updated : ChangeType.Added
        });
    }

    public async Task<bool> ExistsAsync(string key)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        return await connection.ExecuteScalarAsync<bool>(
            "SELECT COUNT(1) FROM Configuration WHERE Key = @Key",
            new { Key = key });
    }

    public async Task DeleteAsync(string key)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        await connection.ExecuteAsync(
            "DELETE FROM Configuration WHERE Key = @Key",
            new { Key = key });

        ConfigurationChanged?.Invoke(this, new ConfigurationChangedEventArgs
        {
            Key = key,
            ChangeType = ChangeType.Deleted
        });
    }

    public async IAsyncEnumerable<string> GetKeysAsync(string prefix)
    {
        using var connection = await _dbConnectionFactory.CreateConnectionAsync();
        var keys = await connection.QueryAsync<string>(
            "SELECT Key FROM Configuration WHERE Key LIKE @Prefix",
            new { Prefix = $"{prefix}%" });

        foreach (var key in keys)
        {
            yield return key;
        }
    }
}
```

### 6.2 Database-Backed Configuration with Polling

```csharp
public class PollingConfigurationService : BackgroundService
{
    private readonly IConfigurationRepository _repository;
    private readonly IOptionsMonitorCache<AppSettings> _cache;
    private readonly ILogger<PollingConfigurationService> _logger;
    private readonly TimeSpan _pollingInterval;

    public PollingConfigurationService(
        IConfigurationRepository repository,
        IOptionsMonitorCache<AppSettings> cache,
        IConfiguration configuration,
        ILogger<PollingConfigurationService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
        _pollingInterval = TimeSpan.FromSeconds(
            configuration.GetValue<int>("ConfigPollingIntervalSeconds", 30));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollForChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling for configuration changes");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task PollForChangesAsync(CancellationToken cancellationToken)
    {
        var settings = await _repository.GetAsync<AppSettings>("AppSettings");

        if (settings != null)
        {
            // Invalidate cache to pick up new values
            _cache.Clear();
            _logger.LogDebug("Configuration cache invalidated after polling");
        }
    }
}
```

### 6.3 External Configuration Services

#### Azure App Configuration

```csharp
using Azure.Identity;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

public static class AzureAppConfigurationSetup
{
    public static IHostBuilder ConfigureAzureAppConfiguration(this IHostBuilder hostBuilder)
    {
        return hostBuilder.ConfigureAppConfiguration((context, config) =>
        {
            var settings = config.Build();
            var connectionString = settings["AzureAppConfiguration:ConnectionString"];

            config.AddAzureAppConfiguration(options =>
            {
                options.Connect(connectionString)
                    // Configure refresh
                    .ConfigureRefresh(refresh =>
                    {
                        // Watch a sentinel key for changes
                        refresh.Register("Settings:Sentinel", refreshAll: true)
                               .SetCacheExpiration(TimeSpan.FromMinutes(5));
                    })
                    // Feature flags support
                    .UseFeatureFlags(flags =>
                    {
                        flags.CacheExpirationInterval = TimeSpan.FromMinutes(5);
                    })
                    // Select specific keys
                    .Select(KeyFilter.Any, LabelFilter.Null)
                    .Select(KeyFilter.Any, context.HostingEnvironment.EnvironmentName);
            });
        });
    }
}

// Middleware to trigger refresh (for ASP.NET Core)
public class AzureAppConfigurationRefreshMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfigurationRefresher _refresher;

    public AzureAppConfigurationRefreshMiddleware(
        RequestDelegate next,
        IConfigurationRefresherProvider refresherProvider)
    {
        _next = next;
        _refresher = refresherProvider.Refreshers.First();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Try to refresh configuration before processing request
        await _refresher.TryRefreshAsync();
        await _next(context);
    }
}
```

#### Consul Configuration Provider

```csharp
using Winton.Extensions.Configuration.Consul;

public static class ConsulConfigurationSetup
{
    public static IConfigurationBuilder AddConsulConfiguration(
        this IConfigurationBuilder builder,
        string consulEndpoint,
        string keyPrefix)
    {
        return builder.AddConsul(
            keyPrefix,
            options =>
            {
                options.ConsulConfigurationOptions = consul =>
                {
                    consul.Address = new Uri(consulEndpoint);
                };

                options.Optional = true;
                options.ReloadOnChange = true;
                options.PollWaitTime = TimeSpan.FromMinutes(5);

                options.OnLoadException = ctx =>
                {
                    ctx.Ignore = true; // Don't crash on load failure
                };

                options.OnWatchException = ctx =>
                {
                    // Log and continue on watch errors
                    Console.WriteLine($"Consul watch error: {ctx.Exception.Message}");
                    return TimeSpan.FromSeconds(30); // Retry delay
                };
            });
    }
}
```

#### etcd Configuration Provider (Custom Implementation)

```csharp
using dotnet_etcd;
using Etcdserverpb;
using Google.Protobuf;

public class EtcdConfigurationSource : IConfigurationSource
{
    public string Endpoint { get; set; } = "http://localhost:2379";
    public string KeyPrefix { get; set; } = "/config/";
    public bool ReloadOnChange { get; set; } = true;

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new EtcdConfigurationProvider(this);
    }
}

public class EtcdConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly EtcdConfigurationSource _source;
    private readonly EtcdClient _client;
    private CancellationTokenSource? _watchCts;
    private bool _disposed;

    public EtcdConfigurationProvider(EtcdConfigurationSource source)
    {
        _source = source;
        _client = new EtcdClient(source.Endpoint);
    }

    public override void Load()
    {
        LoadAsync().GetAwaiter().GetResult();

        if (_source.ReloadOnChange)
        {
            StartWatching();
        }
    }

    private async Task LoadAsync()
    {
        var response = await _client.GetRangeAsync(_source.KeyPrefix);

        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in response.Kvs)
        {
            var key = kv.Key.ToStringUtf8()
                .Replace(_source.KeyPrefix, "")
                .Replace("/", ":");

            data[key] = kv.Value.ToStringUtf8();
        }

        Data = data;
    }

    private void StartWatching()
    {
        _watchCts = new CancellationTokenSource();

        Task.Run(async () =>
        {
            await foreach (var response in _client.WatchRangeAsync(
                _source.KeyPrefix,
                _watchCts.Token))
            {
                foreach (var evt in response.Events)
                {
                    var key = evt.Kv.Key.ToStringUtf8()
                        .Replace(_source.KeyPrefix, "")
                        .Replace("/", ":");

                    if (evt.Type == Mvccpb.Event.Types.EventType.Put)
                    {
                        Data[key] = evt.Kv.Value.ToStringUtf8();
                    }
                    else if (evt.Type == Mvccpb.Event.Types.EventType.Delete)
                    {
                        Data.Remove(key);
                    }
                }

                OnReload();
            }
        }, _watchCts.Token);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _watchCts?.Cancel();
            _watchCts?.Dispose();
            _client.Dispose();
            _disposed = true;
        }
    }
}
```

---

## 7. Code Examples

### 7.1 Complete Example: Adding a New JSON Configuration File at Runtime

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Program.cs
var builder = Host.CreateApplicationBuilder(args);

// Add services
builder.Services.AddSingleton<RuntimeConfigurationService>();
builder.Services.Configure<ApplicationSettings>(
    builder.Configuration.GetSection("ApplicationSettings"));

var host = builder.Build();

// Example usage
var configService = host.Services.GetRequiredService<RuntimeConfigurationService>();
var optionsMonitor = host.Services.GetRequiredService<IOptionsMonitor<ApplicationSettings>>();

Console.WriteLine($"Initial setting: {optionsMonitor.CurrentValue.FeatureName}");

// Add new configuration file at runtime
await configService.AddConfigurationFileAsync("runtime-config.json");

Console.WriteLine($"After adding file: {optionsMonitor.CurrentValue.FeatureName}");

await host.RunAsync();

// Services
public class RuntimeConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitorCache<ApplicationSettings> _cache;
    private readonly ILogger<RuntimeConfigurationService> _logger;
    private readonly string _configBasePath;

    public RuntimeConfigurationService(
        IConfiguration configuration,
        IOptionsMonitorCache<ApplicationSettings> cache,
        ILogger<RuntimeConfigurationService> logger)
    {
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
        _configBasePath = AppContext.BaseDirectory;
    }

    public async Task AddConfigurationFileAsync(string fileName)
    {
        var filePath = Path.Combine(_configBasePath, fileName);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Configuration file not found: {FilePath}", filePath);
            return;
        }

        // For .NET 6+ with ConfigurationManager, you could add sources directly
        // Here we're working with the existing configuration root
        var configRoot = (IConfigurationRoot)_configuration;

        // Create a new configuration with the additional file
        var additionalConfig = new ConfigurationBuilder()
            .SetBasePath(_configBasePath)
            .AddJsonFile(fileName, optional: false, reloadOnChange: true)
            .Build();

        // Copy values to memory provider (if one exists) or use the reload mechanism
        foreach (var kvp in additionalConfig.AsEnumerable())
        {
            if (kvp.Value != null)
            {
                // Note: This doesn't add to the configuration tree directly
                // It's a demonstration - real implementation needs custom provider
                _logger.LogInformation("Would add: {Key} = {Value}", kvp.Key, kvp.Value);
            }
        }

        // Trigger reload
        configRoot.Reload();

        // Clear options cache to force refresh
        _cache.Clear();

        _logger.LogInformation("Added configuration from: {FileName}", fileName);
    }
}

public class ApplicationSettings
{
    public string FeatureName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}
```

### 7.2 Complete Example: Reloadable Configuration Provider

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json;

// Custom reloadable provider
public class ReloadableJsonConfigurationSource : IConfigurationSource
{
    public string FilePath { get; set; } = string.Empty;
    public bool Optional { get; set; }
    public TimeSpan? PollingInterval { get; set; }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new ReloadableJsonConfigurationProvider(this);
    }
}

public class ReloadableJsonConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly ReloadableJsonConfigurationSource _source;
    private FileSystemWatcher? _watcher;
    private Timer? _pollingTimer;
    private DateTime _lastWriteTime;
    private bool _disposed;

    public ReloadableJsonConfigurationProvider(ReloadableJsonConfigurationSource source)
    {
        _source = source;
    }

    public override void Load()
    {
        LoadFile();
        SetupReloadMechanism();
    }

    private void LoadFile()
    {
        if (!File.Exists(_source.FilePath))
        {
            if (_source.Optional)
            {
                Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                return;
            }
            throw new FileNotFoundException($"Configuration file not found: {_source.FilePath}");
        }

        try
        {
            var json = File.ReadAllText(_source.FilePath);
            Data = ParseJson(json);
            _lastWriteTime = File.GetLastWriteTimeUtc(_source.FilePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Error reading configuration from {_source.FilePath}", ex);
        }
    }

    private Dictionary<string, string?> ParseJson(string json)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(json);
        ParseElement(doc.RootElement, "", result);

        return result;
    }

    private void ParseElement(JsonElement element, string prefix, Dictionary<string, string?> data)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix)
                        ? property.Name
                        : $"{prefix}:{property.Name}";
                    ParseElement(property.Value, key, data);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    ParseElement(item, $"{prefix}:{index}", data);
                    index++;
                }
                break;

            default:
                data[prefix] = element.ToString();
                break;
        }
    }

    private void SetupReloadMechanism()
    {
        // Use FileSystemWatcher for immediate change detection
        var directory = Path.GetDirectoryName(_source.FilePath);
        var fileName = Path.GetFileName(_source.FilePath);

        if (!string.IsNullOrEmpty(directory))
        {
            _watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
            };
            _watcher.Changed += OnFileChanged;
            _watcher.EnableRaisingEvents = true;
        }

        // Also set up polling as backup (FileSystemWatcher can be unreliable on network shares)
        if (_source.PollingInterval.HasValue)
        {
            _pollingTimer = new Timer(
                _ => CheckForChanges(),
                null,
                _source.PollingInterval.Value,
                _source.PollingInterval.Value);
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce rapid changes
        Thread.Sleep(100);
        ReloadConfiguration();
    }

    private void CheckForChanges()
    {
        if (File.Exists(_source.FilePath))
        {
            var currentWriteTime = File.GetLastWriteTimeUtc(_source.FilePath);
            if (currentWriteTime > _lastWriteTime)
            {
                ReloadConfiguration();
            }
        }
    }

    private void ReloadConfiguration()
    {
        try
        {
            LoadFile();
            OnReload(); // Notify listeners
        }
        catch (Exception ex)
        {
            // Log but don't crash - keep existing configuration
            Console.WriteLine($"Error reloading configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Manually trigger a reload.
    /// </summary>
    public void ForceReload()
    {
        ReloadConfiguration();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _watcher?.Dispose();
            _pollingTimer?.Dispose();
            _disposed = true;
        }
    }
}

// Extension method
public static class ReloadableJsonConfigurationExtensions
{
    public static IConfigurationBuilder AddReloadableJsonFile(
        this IConfigurationBuilder builder,
        string path,
        bool optional = false,
        TimeSpan? pollingInterval = null)
    {
        return builder.Add(new ReloadableJsonConfigurationSource
        {
            FilePath = path,
            Optional = optional,
            PollingInterval = pollingInterval
        });
    }
}
```

### 7.3 Complete Example: Plugin System with Dynamic Configuration

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Runtime.Loader;

// Plugin interfaces
public interface IPlugin
{
    string Name { get; }
    string Version { get; }
    void Initialize(IServiceProvider services);
    Task ExecuteAsync(CancellationToken cancellationToken);
}

public interface IPluginConfiguration
{
    bool Enabled { get; set; }
    Dictionary<string, string> Settings { get; set; }
}

// Plugin manager
public class PluginManager : IDisposable
{
    private readonly IServiceCollection _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PluginManager> _logger;
    private readonly Dictionary<string, LoadedPlugin> _loadedPlugins = new();
    private readonly string _pluginsPath;

    public PluginManager(
        IServiceCollection services,
        IConfiguration configuration,
        ILogger<PluginManager> logger)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
        _pluginsPath = configuration["PluginsPath"] ?? "plugins";
    }

    public async Task LoadPluginAsync(string pluginName)
    {
        if (_loadedPlugins.ContainsKey(pluginName))
        {
            _logger.LogWarning("Plugin {PluginName} is already loaded", pluginName);
            return;
        }

        var pluginPath = Path.Combine(_pluginsPath, pluginName);
        var pluginDll = Path.Combine(pluginPath, $"{pluginName}.dll");
        var pluginConfig = Path.Combine(pluginPath, "plugin.json");

        if (!File.Exists(pluginDll))
        {
            throw new FileNotFoundException($"Plugin assembly not found: {pluginDll}");
        }

        // Load plugin configuration
        IConfigurationRoot? configuration = null;
        if (File.Exists(pluginConfig))
        {
            configuration = new ConfigurationBuilder()
                .SetBasePath(pluginPath)
                .AddJsonFile("plugin.json", optional: false, reloadOnChange: true)
                .Build();
        }

        // Load plugin assembly in isolated context
        var loadContext = new PluginLoadContext(pluginDll);
        var assembly = loadContext.LoadFromAssemblyPath(pluginDll);

        // Find plugin implementation
        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        if (pluginType == null)
        {
            throw new InvalidOperationException($"No IPlugin implementation found in {pluginDll}");
        }

        // Register plugin configuration with DI
        if (configuration != null)
        {
            _services.Configure<PluginSettings>(
                pluginName,
                configuration.GetSection("PluginSettings"));
        }

        var loadedPlugin = new LoadedPlugin
        {
            Name = pluginName,
            LoadContext = loadContext,
            Assembly = assembly,
            PluginType = pluginType,
            Configuration = configuration
        };

        _loadedPlugins[pluginName] = loadedPlugin;

        _logger.LogInformation("Loaded plugin: {PluginName}", pluginName);
    }

    public void UnloadPlugin(string pluginName)
    {
        if (!_loadedPlugins.TryGetValue(pluginName, out var plugin))
        {
            _logger.LogWarning("Plugin {PluginName} is not loaded", pluginName);
            return;
        }

        // Dispose configuration providers
        if (plugin.Configuration != null)
        {
            foreach (var provider in plugin.Configuration.Providers.OfType<IDisposable>())
            {
                provider.Dispose();
            }
        }

        // Unload assembly context
        plugin.LoadContext.Unload();
        _loadedPlugins.Remove(pluginName);

        _logger.LogInformation("Unloaded plugin: {PluginName}", pluginName);
    }

    public IPlugin? CreatePluginInstance(string pluginName, IServiceProvider services)
    {
        if (!_loadedPlugins.TryGetValue(pluginName, out var plugin))
        {
            return null;
        }

        return (IPlugin?)ActivatorUtilities.CreateInstance(services, plugin.PluginType);
    }

    public IEnumerable<string> GetLoadedPlugins() => _loadedPlugins.Keys;

    public IConfiguration? GetPluginConfiguration(string pluginName)
    {
        return _loadedPlugins.TryGetValue(pluginName, out var plugin)
            ? plugin.Configuration
            : null;
    }

    public void Dispose()
    {
        foreach (var pluginName in _loadedPlugins.Keys.ToList())
        {
            UnloadPlugin(pluginName);
        }
    }

    private class LoadedPlugin
    {
        public required string Name { get; init; }
        public required PluginLoadContext LoadContext { get; init; }
        public required Assembly Assembly { get; init; }
        public required Type PluginType { get; init; }
        public IConfigurationRoot? Configuration { get; init; }
    }
}

// Plugin load context for isolation
public class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath != null
            ? LoadFromAssemblyPath(assemblyPath)
            : null;
    }
}

public class PluginSettings
{
    public bool Enabled { get; set; } = true;
    public string DisplayName { get; set; } = string.Empty;
    public Dictionary<string, string> CustomSettings { get; set; } = new();
}
```

### 7.4 Complete Example: Using IOptionsMonitorCache to Force Refresh

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// Configure options
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));

// Add the refresh service
builder.Services.AddSingleton<OptionsRefreshService<ApiSettings>>();

// Add background service that periodically refreshes options
builder.Services.AddHostedService<OptionsRefreshBackgroundService>();

var host = builder.Build();
await host.RunAsync();

// Generic options refresh service
public class OptionsRefreshService<TOptions> where TOptions : class
{
    private readonly IOptionsMonitorCache<TOptions> _cache;
    private readonly IOptionsMonitor<TOptions> _monitor;
    private readonly ILogger<OptionsRefreshService<TOptions>> _logger;

    public OptionsRefreshService(
        IOptionsMonitorCache<TOptions> cache,
        IOptionsMonitor<TOptions> monitor,
        ILogger<OptionsRefreshService<TOptions>> logger)
    {
        _cache = cache;
        _monitor = monitor;
        _logger = logger;
    }

    /// <summary>
    /// Forces the default options instance to be recomputed on next access.
    /// </summary>
    public TOptions ForceRefresh()
    {
        _cache.TryRemove(Options.DefaultName);
        _logger.LogDebug("Invalidated default options cache for {OptionsType}", typeof(TOptions).Name);
        return _monitor.CurrentValue;
    }

    /// <summary>
    /// Forces a named options instance to be recomputed on next access.
    /// </summary>
    public TOptions ForceRefresh(string name)
    {
        _cache.TryRemove(name);
        _logger.LogDebug("Invalidated options cache '{Name}' for {OptionsType}", name, typeof(TOptions).Name);
        return _monitor.Get(name);
    }

    /// <summary>
    /// Clears all cached options instances.
    /// </summary>
    public void ClearAll()
    {
        _cache.Clear();
        _logger.LogDebug("Cleared all options cache for {OptionsType}", typeof(TOptions).Name);
    }

    /// <summary>
    /// Manually injects a pre-configured options instance.
    /// </summary>
    public bool InjectOptions(TOptions options, string? name = null)
    {
        var optionName = name ?? Options.DefaultName;
        var added = _cache.TryAdd(optionName, options);

        if (added)
        {
            _logger.LogInformation(
                "Injected custom options '{Name}' for {OptionsType}",
                optionName,
                typeof(TOptions).Name);
        }

        return added;
    }

    /// <summary>
    /// Gets the current value, optionally forcing a refresh first.
    /// </summary>
    public TOptions GetCurrent(bool forceRefresh = false)
    {
        if (forceRefresh)
        {
            _cache.TryRemove(Options.DefaultName);
        }
        return _monitor.CurrentValue;
    }
}

// Background service example
public class OptionsRefreshBackgroundService : BackgroundService
{
    private readonly OptionsRefreshService<ApiSettings> _refreshService;
    private readonly IOptionsMonitor<ApiSettings> _optionsMonitor;
    private readonly ILogger<OptionsRefreshBackgroundService> _logger;

    public OptionsRefreshBackgroundService(
        OptionsRefreshService<ApiSettings> refreshService,
        IOptionsMonitor<ApiSettings> optionsMonitor,
        ILogger<OptionsRefreshBackgroundService> logger)
    {
        _refreshService = refreshService;
        _optionsMonitor = optionsMonitor;
        _logger = logger;

        // Subscribe to changes
        _optionsMonitor.OnChange((settings, name) =>
        {
            _logger.LogInformation(
                "ApiSettings changed. New endpoint: {Endpoint}",
                settings.Endpoint);
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Periodically force refresh to pick up external changes
            var settings = _refreshService.ForceRefresh();

            _logger.LogInformation(
                "Current API endpoint: {Endpoint}, Timeout: {Timeout}s",
                settings.Endpoint,
                settings.TimeoutSeconds);

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

public class ApiSettings
{
    public string Endpoint { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
}
```

---

## Summary

### Key Takeaways

1. **Use `IOptionsMonitor<T>`** for singleton services that need real-time configuration updates
2. **Use `IOptionsSnapshot<T>`** for scoped services (per-request) that need consistent configuration
3. **Never use `IOptions<T>`** if you need configuration to update at runtime
4. **`IOptionsMonitorCache<T>`** allows manual cache invalidation with `TryRemove()`, `TryAdd()`, and `Clear()`
5. **Custom configuration providers** can support runtime updates by calling `OnReload()` after updating `Data`
6. **Be aware of thread safety** issues with `IConfiguration` under concurrent access
7. **WebApplicationBuilder has a known issue** where `Reload()` may not work correctly
8. **External configuration services** (Azure App Configuration, Consul, etcd) are recommended for distributed systems

### Recommended Patterns

| Scenario | Recommended Approach |
|----------|---------------------|
| Static configuration | `IOptions<T>` |
| Per-request configuration | `IOptionsSnapshot<T>` |
| Singleton with dynamic config | `IOptionsMonitor<T>` |
| Plugin configuration | Custom `IConfigurationSource` per plugin |
| Multi-tenant configuration | Tenant-aware `IOptionsMonitorCache<T>` |
| External configuration | Azure App Configuration, Consul, or etcd |
| Database configuration | Custom provider with polling |

---

## References

- [IConfigurationRoot.Reload Method - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.configuration.iconfigurationroot.reload?view=net-9.0-pp)
- [Options pattern - .NET - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- [Options pattern in ASP.NET Core - Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0)
- [Implement a custom configuration provider - .NET - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/custom-configuration-provider)
- [Configuration providers - .NET - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers)
- [WebApplicationBuilder Reload Issue - GitHub](https://github.com/dotnet/aspnetcore/issues/36181)
- [IConfiguration Thread Safety Issue - GitHub](https://github.com/dotnet/runtime/issues/38392)
- [ConfigurationManager Deadlock Issue - GitHub](https://github.com/dotnet/runtime/issues/61747)
- [Tutorial: Use dynamic configuration in ASP.NET Core - Azure App Configuration](https://learn.microsoft.com/en-us/azure/azure-app-configuration/enable-dynamic-configuration-aspnet-core)
- [Redesigning Configuration Refresh for Azure App Configuration - .NET Blog](https://devblogs.microsoft.com/dotnet/redesigning-configuration-refresh-for-azure-app-configuration/)
- [Winton.Extensions.Configuration.Consul - GitHub](https://github.com/wintoncode/Winton.Extensions.Configuration.Consul)
- [Understanding IOptions, IOptionsMonitor, and IOptionsSnapshot - Felipe Gavilan](https://gavilan.blog/2025/03/25/understanding-ioptions-ioptionssnapshot-and-ioptionsmonitor/)
- [Custom Configuration Provider in .NET - Egor Tarasov](https://medium.com/@vosarat1995/custom-configuration-provider-in-net-step-by-step-guide-3d8a3a8f7203)
- [Multi-tenant .NET Core Application - Michael McKenna](https://michael-mckenna.com/multi-tenant-asp-dot-net-core-application-tenant-specific-configuration-options/)
