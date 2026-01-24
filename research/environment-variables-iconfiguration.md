# Environment Variables with IConfiguration and the .NET Generic Host

This document provides comprehensive coverage of using environment variables with `IConfiguration` in .NET applications using the Generic Host.

## Table of Contents

1. [Generic Host Overview](#1-generic-host-overview)
2. [Environment Variables Configuration](#2-environment-variables-configuration)
3. [Configuring Environment Variable Providers](#3-configuring-environment-variable-providers)
4. [Integration with IConfiguration](#4-integration-with-iconfiguration)
5. [Best Practices](#5-best-practices)

---

## 1. Generic Host Overview

### What is the .NET Generic Host?

The .NET Generic Host is a foundational component that manages application lifetime, dependency injection, configuration, and logging. It provides a consistent programming model for console applications, worker services, and web applications.

### Host Builder Methods

There are two primary approaches to creating a host:

| Feature | `Host.CreateApplicationBuilder` | `Host.CreateDefaultBuilder` |
|---------|--------------------------------|----------------------------|
| **API Type** | `IHostApplicationBuilder` (newer) | `IHostBuilder` (legacy) |
| **Configuration** | Direct property access | `Configure*()` methods |
| **Usage** | Modern .NET 6+ templates | Older implementations |

#### CreateApplicationBuilder (Modern Approach)

```csharp
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Direct access to services and configuration
builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();
```

#### CreateDefaultBuilder (Legacy Approach)

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

### Default Configuration Providers

Both builders automatically configure the following providers in order (last provider wins):

#### Host Configuration Sources
1. Environment variables prefixed with `DOTNET_`
2. Command-line arguments

#### Application Configuration Sources (in order of precedence, lowest to highest)
1. `appsettings.json`
2. `appsettings.{Environment}.json` (e.g., `appsettings.Development.json`)
3. Secret Manager (Development environment only)
4. Environment variables (no prefix)
5. Command-line arguments

### Configuration Source Precedence

The key principle is: **later sources override earlier ones**. This means:

- Command-line arguments have the highest priority
- Environment variables override all JSON files and secrets
- Environment-specific JSON files override the base `appsettings.json`

```csharp
// Demonstration of precedence
// appsettings.json:        { "ApiUrl": "https://default.api.com" }
// appsettings.Development.json: { "ApiUrl": "https://dev.api.com" }
// Environment variable:    ApiUrl=https://env.api.com
// Command line:           --ApiUrl=https://cli.api.com

// Result: "https://cli.api.com" (command line wins)
```

---

## 2. Environment Variables Configuration

### How Environment Variables Are Loaded by Default

When you use `Host.CreateApplicationBuilder()` or `Host.CreateDefaultBuilder()`, environment variables are automatically loaded:

```csharp
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Environment variables are already loaded at this point
string? connectionString = builder.Configuration["ConnectionStrings:DefaultConnection"];
```

### Naming Conventions and Prefixes

#### Built-in Prefixes

| Prefix | Purpose | Example |
|--------|---------|---------|
| `DOTNET_` | .NET runtime/host configuration | `DOTNET_ENVIRONMENT=Production` |
| `ASPNETCORE_` | ASP.NET Core specific settings | `ASPNETCORE_URLS=http://+:5000` |
| Custom | Application-specific settings | `MYAPP_DatabaseUrl=...` |

#### Important: Prefixes Are Stripped

When using a prefix, the prefix is removed when reading the configuration:

```csharp
// Environment variable: DOTNET_ENVIRONMENT=Development
// Configuration key: Environment (prefix stripped)

// Environment variable: ASPNETCORE_URLS=http://+:5000
// Configuration key: Urls (prefix stripped)
```

### Hierarchical Key Mapping

Configuration supports hierarchical/nested settings. The delimiter depends on the platform:

| Delimiter | Platform Support | Example |
|-----------|-----------------|---------|
| `__` (double underscore) | All platforms | `ConnectionStrings__DefaultConnection` |
| `:` (colon) | Windows only | `ConnectionStrings:DefaultConnection` |

**Recommendation:** Always use double underscore (`__`) for cross-platform compatibility.

#### Example: Nested Configuration

Given this `appsettings.json`:

```json
{
    "TransientFaultHandling": {
        "Enabled": true,
        "AutoRetryDelay": "00:00:07"
    },
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "Microsoft": "Warning"
        }
    }
}
```

Equivalent environment variables:

```bash
# Linux/macOS (use double underscore)
export TransientFaultHandling__Enabled=true
export TransientFaultHandling__AutoRetryDelay=00:00:13
export Logging__LogLevel__Default=Debug
export Logging__LogLevel__Microsoft=Information

# Windows (can use either, but __ is recommended)
set TransientFaultHandling__Enabled=true
set TransientFaultHandling__AutoRetryDelay=00:00:13
```

### Case Sensitivity

- **Environment variable names:** Case-insensitive on Windows, case-sensitive on Linux/macOS
- **Configuration keys:** Case-insensitive in .NET (normalized internally)

**Best Practice:** Use consistent casing (typically PascalCase) to avoid confusion.

### Connection String Prefixes

The Configuration API has special handling for connection string environment variables:

| Prefix | Provider | Configuration Key |
|--------|----------|-------------------|
| `CUSTOMCONNSTR_{KEY}` | Custom | `ConnectionStrings:{KEY}` |
| `MYSQLCONNSTR_{KEY}` | MySQL | `ConnectionStrings:{KEY}` |
| `SQLAZURECONNSTR_{KEY}` | Azure SQL | `ConnectionStrings:{KEY}` |
| `SQLCONNSTR_{KEY}` | SQL Server | `ConnectionStrings:{KEY}` |

```bash
# Setting a SQL Server connection string
export SQLCONNSTR_DefaultConnection="Server=localhost;Database=MyDb;Trusted_Connection=True"

# Accessed as:
# Configuration["ConnectionStrings:DefaultConnection"]
```

---

## 3. Configuring Environment Variable Providers

### AddEnvironmentVariables() Method

The `AddEnvironmentVariables()` extension method adds the environment variables provider to the configuration builder.

#### Basic Usage (All Environment Variables)

```csharp
using Microsoft.Extensions.Configuration;

IConfigurationRoot config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()  // Loads ALL environment variables
    .Build();
```

#### With Prefix Filtering

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Only load environment variables starting with "MyApp_"
builder.Configuration.AddEnvironmentVariables(prefix: "MyApp_");
```

### Prefix Stripping Behavior

When you specify a prefix, it is automatically stripped from the keys:

```bash
# Environment variables set:
export MyApp_Database__Host=localhost
export MyApp_Database__Port=5432
export MyApp_ApiKey=secret123
export OtherApp_Setting=ignored  # Not loaded (wrong prefix)
```

```csharp
builder.Configuration.AddEnvironmentVariables(prefix: "MyApp_");

// Access without the prefix:
string? host = builder.Configuration["Database:Host"];     // "localhost"
string? port = builder.Configuration["Database:Port"];     // "5432"
string? apiKey = builder.Configuration["ApiKey"];          // "secret123"
```

### Multiple Providers with Different Prefixes

```csharp
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Clear default configuration and rebuild with custom sources
builder.Configuration.Sources.Clear();

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables()                           // All env vars (lowest priority)
    .AddEnvironmentVariables(prefix: "MYAPP_")           // App-specific (higher priority)
    .AddCommandLine(args);                               // Command line (highest priority)
```

### Custom Host Configuration

For host-level settings (like environment name, content root):

```csharp
Host.CreateDefaultBuilder(args)
    .ConfigureHostConfiguration(hostConfig =>
    {
        hostConfig.SetBasePath(Directory.GetCurrentDirectory());
        hostConfig.AddJsonFile("hostsettings.json", optional: true);
        hostConfig.AddEnvironmentVariables(prefix: "MYHOST_");
        hostConfig.AddCommandLine(args);
    })
    .ConfigureAppConfiguration((context, config) =>
    {
        // App configuration here
        config.AddEnvironmentVariables(prefix: "MYAPP_");
    });
```

---

## 4. Integration with IConfiguration

### Accessing Values via IConfiguration

#### Direct Key Access

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
IConfiguration config = builder.Configuration;

// Single value
string? apiUrl = config["ApiUrl"];

// Nested value (using colon delimiter)
string? logLevel = config["Logging:LogLevel:Default"];

// Array/indexed value
string? firstServer = config["Servers:0"];
string? secondServer = config["Servers:1"];
```

#### Using GetValue<T>

```csharp
// With type conversion
int port = config.GetValue<int>("Server:Port");
bool isEnabled = config.GetValue<bool>("Features:Caching:Enabled");
TimeSpan timeout = config.GetValue<TimeSpan>("HttpClient:Timeout");

// With default value
int maxRetries = config.GetValue<int>("MaxRetries", defaultValue: 3);
```

#### Using GetSection and GetChildren

```csharp
// Get a configuration section
IConfigurationSection loggingSection = config.GetSection("Logging");
string? defaultLevel = loggingSection["LogLevel:Default"];

// Enumerate children
foreach (IConfigurationSection child in config.GetSection("Servers").GetChildren())
{
    string? host = child["Host"];
    int port = child.GetValue<int>("Port");
    Console.WriteLine($"Server: {host}:{port}");
}
```

### Binding to Strongly-Typed Objects

#### Options Classes

```csharp
public sealed class DatabaseOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int MaxPoolSize { get; set; } = 100;
}

public sealed class ApiClientOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRetries { get; set; } = 3;
}
```

#### Direct Binding with Get<T>

```csharp
DatabaseOptions? dbOptions = config
    .GetSection("Database")
    .Get<DatabaseOptions>();

// Or using GetRequiredSection (throws if section doesn't exist)
ApiClientOptions apiOptions = config
    .GetRequiredSection("ApiClient")
    .Get<ApiClientOptions>()!;
```

#### Using the Options Pattern with DI

```csharp
using Microsoft.Extensions.Options;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Register options with DI
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection("Database"));

builder.Services.Configure<ApiClientOptions>(
    builder.Configuration.GetSection("ApiClient"));

// Add your services
builder.Services.AddSingleton<MyService>();

IHost host = builder.Build();
```

#### Consuming Options in Services

```csharp
public sealed class MyService
{
    private readonly DatabaseOptions _dbOptions;
    private readonly ApiClientOptions _apiOptions;

    public MyService(
        IOptions<DatabaseOptions> dbOptions,
        IOptions<ApiClientOptions> apiOptions)
    {
        _dbOptions = dbOptions.Value;
        _apiOptions = apiOptions.Value;
    }

    public void DoWork()
    {
        Console.WriteLine($"Connecting to {_dbOptions.Host}:{_dbOptions.Port}");
        Console.WriteLine($"API timeout: {_apiOptions.Timeout}");
    }
}
```

### IOptions vs IOptionsSnapshot vs IOptionsMonitor

| Interface | Lifetime | Reloads on Change | Use Case |
|-----------|----------|-------------------|----------|
| `IOptions<T>` | Singleton | No | Static configuration |
| `IOptionsSnapshot<T>` | Scoped | Yes (per request) | Web request-scoped |
| `IOptionsMonitor<T>` | Singleton | Yes (real-time) | Background services |

```csharp
// For singleton services that need config changes
public sealed class BackgroundWorker : BackgroundService
{
    private readonly IOptionsMonitor<WorkerOptions> _optionsMonitor;

    public BackgroundWorker(IOptionsMonitor<WorkerOptions> optionsMonitor)
    {
        _optionsMonitor = optionsMonitor;

        // Subscribe to configuration changes
        _optionsMonitor.OnChange(options =>
        {
            Console.WriteLine($"Configuration changed: Interval={options.Interval}");
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Always get current value
            var options = _optionsMonitor.CurrentValue;
            await Task.Delay(options.Interval, stoppingToken);
        }
    }
}
```

### Options Validation

```csharp
using System.ComponentModel.DataAnnotations;

public sealed class DatabaseOptions
{
    [Required]
    [MinLength(1)]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 5432;

    [Required]
    public string Database { get; set; } = string.Empty;

    [Range(1, 1000)]
    public int MaxPoolSize { get; set; } = 100;
}

// Registration with validation
builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection("Database"))
    .ValidateDataAnnotations()
    .ValidateOnStart();  // Fail fast at startup
```

### Environment-Specific Settings

```csharp
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Environment is automatically set from:
// 1. DOTNET_ENVIRONMENT environment variable
// 2. ASPNETCORE_ENVIRONMENT environment variable (for web apps)
string environment = builder.Environment.EnvironmentName;

// Configuration files are loaded automatically:
// - appsettings.json
// - appsettings.{Environment}.json

// Check environment programmatically
if (builder.Environment.IsDevelopment())
{
    // Development-specific configuration
    builder.Services.AddSingleton<IEmailService, FakeEmailService>();
}
else
{
    builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
}
```

---

## 5. Best Practices

### Security Considerations for Sensitive Data

#### Never Store Secrets in Source Control

```csharp
// BAD: Hardcoded secrets
var connectionString = "Server=prod;Password=secret123";

// BAD: Secrets in appsettings.json (committed to source control)
// {
//   "Database": {
//     "Password": "secret123"  // NEVER DO THIS
//   }
// }

// GOOD: Use environment variables or secret stores
var connectionString = config["Database:ConnectionString"];
```

#### Use Secret Management Tools in Production

- **Azure Key Vault** - Recommended for Azure deployments
- **AWS Secrets Manager** - For AWS deployments
- **HashiCorp Vault** - Multi-cloud/on-premises

```csharp
// Azure Key Vault integration
builder.Configuration.AddAzureKeyVault(
    new Uri("https://my-keyvault.vault.azure.net/"),
    new DefaultAzureCredential());
```

### Using User Secrets in Development

User Secrets provides a safe way to store development secrets outside of source control.

#### Setup

```bash
# Initialize user secrets for your project
dotnet user-secrets init

# Set secrets
dotnet user-secrets set "Database:Password" "dev-password-123"
dotnet user-secrets set "ApiKeys:ThirdParty" "dev-api-key"

# List all secrets
dotnet user-secrets list

# Remove a secret
dotnet user-secrets remove "Database:Password"

# Clear all secrets
dotnet user-secrets clear
```

#### Project File Configuration

```xml
<PropertyGroup>
    <UserSecretsId>79a3edd0-2092-40a2-a04d-dcb46d5ca9ed</UserSecretsId>
</PropertyGroup>
```

#### Automatic Loading

User secrets are automatically loaded in the Development environment:

```csharp
// No additional code needed - automatically loaded by CreateApplicationBuilder
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Secrets are available in configuration
string? dbPassword = builder.Configuration["Database:Password"];
```

#### Manual Loading (Non-Web Apps)

```csharp
using Microsoft.Extensions.Configuration;

IConfigurationRoot config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()  // Load user secrets
    .AddEnvironmentVariables()
    .Build();
```

### Docker/Containerized Environments

#### Using Environment Variables in Docker

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Set default environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "MyApp.dll"]
```

```yaml
# docker-compose.yml
version: '3.8'
services:
  myapp:
    image: myapp:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - Database__Host=db-server
      - Database__Port=5432
      - Database__Database=myapp
      - Database__Username=appuser
      # Use Docker secrets for sensitive data
    secrets:
      - db_password
    env_file:
      - .env.production

secrets:
  db_password:
    external: true
```

#### Using .env Files

```bash
# .env.production (not committed to source control)
Database__Host=prod-db.example.com
Database__Port=5432
ApiClient__BaseUrl=https://api.example.com
```

```csharp
// Reading .env files (requires additional package or custom code)
builder.Configuration.AddEnvironmentVariables();
```

#### Kubernetes ConfigMaps and Secrets

```yaml
# configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: myapp-config
data:
  Database__Host: "db-service"
  Database__Port: "5432"
  Logging__LogLevel__Default: "Information"

---
# secret.yaml
apiVersion: v1
kind: Secret
metadata:
  name: myapp-secrets
type: Opaque
stringData:
  Database__Password: "super-secret-password"
```

```yaml
# deployment.yaml
spec:
  containers:
    - name: myapp
      envFrom:
        - configMapRef:
            name: myapp-config
        - secretRef:
            name: myapp-secrets
```

### 12-Factor App Compliance

The [12-Factor App methodology](https://12factor.net/config) recommends storing configuration in environment variables. .NET's `IConfiguration` system naturally supports this:

#### Factor III: Config

Store config in environment variables:

```csharp
// Application reads from environment, not config files in production
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// Environment-based configuration
string environment = builder.Environment.EnvironmentName;
bool isDevelopment = builder.Environment.IsDevelopment();

// All configuration comes from IConfiguration
// which includes environment variables by default
var dbHost = builder.Configuration["Database:Host"];
```

#### Factor X: Dev/Prod Parity

Keep development and production as similar as possible:

```csharp
// Same code path, different configuration sources
public sealed class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration configuration)
    {
        // Works the same in dev (user secrets) and prod (env vars)
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string not configured");
    }
}
```

#### Factor XI: Logs

Treat logs as event streams:

```csharp
// Configure logging via environment variables
// LOGGING__LOGLEVEL__DEFAULT=Information
// LOGGING__LOGLEVEL__MICROSOFT=Warning

builder.Services.AddLogging(logging =>
{
    logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
    logging.AddConsole();  // Writes to stdout (12-factor compliant)
});
```

### Complete Example Application

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

// Options classes
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string Host { get; set; } = "localhost";

    [Range(1, 65535)]
    public int Port { get; set; } = 5432;

    [Required]
    public string Database { get; set; } = string.Empty;

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed class AppSettings
{
    public const string SectionName = "App";

    [Required]
    public string Name { get; set; } = string.Empty;

    public bool EnableFeatureX { get; set; }

    [Range(1, 100)]
    public int MaxConcurrency { get; set; } = 10;
}

// Service using options
public sealed class DataService
{
    private readonly DatabaseOptions _dbOptions;
    private readonly AppSettings _appSettings;

    public DataService(
        IOptions<DatabaseOptions> dbOptions,
        IOptions<AppSettings> appSettings)
    {
        _dbOptions = dbOptions.Value;
        _appSettings = appSettings.Value;
    }

    public void PrintConfiguration()
    {
        Console.WriteLine($"App: {_appSettings.Name}");
        Console.WriteLine($"Feature X Enabled: {_appSettings.EnableFeatureX}");
        Console.WriteLine($"Max Concurrency: {_appSettings.MaxConcurrency}");
        Console.WriteLine($"Database: {_dbOptions.Host}:{_dbOptions.Port}/{_dbOptions.Database}");
    }
}

// Program entry point
public class Program
{
    public static void Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        // Configure options with validation
        builder.Services
            .AddOptions<DatabaseOptions>()
            .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services
            .AddOptions<AppSettings>()
            .Bind(builder.Configuration.GetSection(AppSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register services
        builder.Services.AddSingleton<DataService>();

        IHost host = builder.Build();

        // Use the service
        var dataService = host.Services.GetRequiredService<DataService>();
        dataService.PrintConfiguration();

        host.Run();
    }
}
```

With corresponding environment variables:

```bash
# Set these before running the application
export App__Name="My Application"
export App__EnableFeatureX=true
export App__MaxConcurrency=25
export Database__Host=db.example.com
export Database__Port=5432
export Database__Database=myapp
export Database__Username=appuser
export Database__Password=secret123
```

Or `appsettings.json` for development:

```json
{
    "App": {
        "Name": "My Application (Dev)",
        "EnableFeatureX": true,
        "MaxConcurrency": 5
    },
    "Database": {
        "Host": "localhost",
        "Port": 5432,
        "Database": "myapp_dev",
        "Username": "devuser"
    }
}
```

With user secrets for the password:

```bash
dotnet user-secrets set "Database:Password" "dev-password"
```

---

## Summary

| Topic | Key Points |
|-------|------------|
| **Generic Host** | Use `Host.CreateApplicationBuilder()` for modern apps; provides default configuration providers |
| **Precedence** | Command line > Environment variables > User Secrets > appsettings.{Env}.json > appsettings.json |
| **Hierarchy** | Use `__` (double underscore) for nested keys in environment variables |
| **Prefixes** | `DOTNET_` for host config, `ASPNETCORE_` for web config, custom prefixes for app config |
| **Options Pattern** | Prefer strongly-typed options over direct `IConfiguration` access |
| **Validation** | Use `ValidateDataAnnotations()` and `ValidateOnStart()` for fail-fast behavior |
| **Security** | Use User Secrets in development, Azure Key Vault or similar in production |
| **Containers** | Environment variables are the standard way to configure containerized .NET apps |

---

## Sources

- [Configuration in .NET - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
- [.NET Generic Host - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)
- [Configuration Providers - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration-providers)
- [Options Pattern in .NET - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/extensions/options)
- [Safe Storage of App Secrets - Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
- [The Twelve-Factor App - Config](https://12factor.net/config)
