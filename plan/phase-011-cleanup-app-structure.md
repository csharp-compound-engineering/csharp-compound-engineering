# Phase 011: Cleanup Console Application Structure

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Infrastructure Setup
> **Prerequisites**: Phase 001 (Solution Structure), Phase 007 (Database Configuration)

---

## Spec References

This phase implements the cleanup console application defined in:

- **spec/infrastructure/cleanup-app.md** - Complete cleanup app specification
- **spec/infrastructure.md** - [Cleanup Console App](../spec/infrastructure.md#cleanup-console-app) section
- **structure/infrastructure.md** - Summary of cleanup app requirements
- **research/hosted-services-background-tasks.md** - BackgroundService patterns and PeriodicTimer usage

---

## Objectives

1. Create the `CompoundDocs.Cleanup` .NET console application project
2. Implement the .NET Generic Host with `BackgroundService` pattern
3. Configure Docker container for running as part of the infrastructure stack
4. Implement command-line argument parsing (`--once`, `--dry-run`)
5. Set up configuration options (interval, grace period, dry-run mode)
6. Configure volume mounts for host path detection

---

## Acceptance Criteria

### Project Structure

- [ ] `src/CompoundDocs.Cleanup/` directory exists with proper structure
- [ ] `CompoundDocs.Cleanup.csproj` configured as console application targeting `net9.0`
- [ ] Project added to solution file `csharp-compounding-docs.sln`
- [ ] Project references `CompoundDocs.Common` for shared database access

### Program Entry Point

- [ ] `Program.cs` uses `Host.CreateDefaultBuilder()` pattern
- [ ] Hosted service `CleanupWorker` registered via `AddHostedService<CleanupWorker>()`
- [ ] Configuration bound from `Cleanup` section via `IOptions<CleanupOptions>`
- [ ] Command-line arguments properly parsed and applied

### CleanupWorker Implementation

- [ ] `CleanupWorker` extends `BackgroundService`
- [ ] `ExecuteAsync` implements periodic cleanup loop using `PeriodicTimer`
- [ ] Proper cancellation token handling throughout
- [ ] Scoped service consumption pattern for database access
- [ ] Support for single-run mode (`--once` flag)

### Configuration

- [ ] `CleanupOptions` class with all configurable properties
- [ ] Default configuration in `appsettings.json`
- [ ] Environment variable override support
- [ ] Docker Compose environment variable mapping

### Command-Line Arguments

- [ ] `--once` flag for single execution then exit
- [ ] `--dry-run` flag to preview deletions without executing
- [ ] Arguments parsed and applied to configuration

### Docker Configuration

- [ ] `Dockerfile` in `docker/cleanup/` directory
- [ ] Multi-stage build for optimized image size
- [ ] Volume mount configuration for host path detection
- [ ] Service definition added to `docker-compose.yml`
- [ ] Dependency on PostgreSQL health check

---

## Implementation Notes

### Project Structure

Create the following directory structure:

```
src/CompoundDocs.Cleanup/
├── Program.cs
├── CompoundDocs.Cleanup.csproj
├── CleanupWorker.cs
├── CleanupOptions.cs
├── Services/
│   ├── IOrphanDetector.cs
│   ├── OrphanedPathDetector.cs
│   └── OrphanedBranchDetector.cs
└── appsettings.json
```

### Project File

```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="Npgsql" />
    <PackageReference Include="System.CommandLine" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\CompoundDocs.Common\CompoundDocs.Common.csproj" />
  </ItemGroup>

</Project>
```

### Program.cs with Command-Line Parsing

```csharp
using System.CommandLine;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using CompoundDocs.Cleanup;

var onceOption = new Option<bool>(
    "--once",
    "Run cleanup once and exit instead of running continuously");

var dryRunOption = new Option<bool>(
    "--dry-run",
    "Preview what would be deleted without actually deleting");

var rootCommand = new RootCommand("CompoundDocs Cleanup Service")
{
    onceOption,
    dryRunOption
};

rootCommand.SetHandler(async (bool once, bool dryRun) =>
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            services.Configure<CleanupOptions>(options =>
            {
                context.Configuration.GetSection("Cleanup").Bind(options);
                options.RunOnce = once;
                options.DryRun = dryRun;
            });

            services.AddHostedService<CleanupWorker>();
            services.AddScoped<IOrphanDetector, OrphanedPathDetector>();
            services.AddScoped<IOrphanDetector, OrphanedBranchDetector>();
        })
        .Build();

    await host.RunAsync();
}, onceOption, dryRunOption);

return await rootCommand.InvokeAsync(args);
```

### CleanupOptions Class

```csharp
namespace CompoundDocs.Cleanup;

public class CleanupOptions
{
    /// <summary>
    /// Interval between cleanup runs in minutes. Default: 60 minutes.
    /// </summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Whether to check remote branches for orphan detection.
    /// </summary>
    public bool CheckRemoteBranches { get; set; } = true;

    /// <summary>
    /// Preview deletions without actually executing them.
    /// </summary>
    public bool DryRun { get; set; } = false;

    /// <summary>
    /// Run once and exit instead of continuous operation.
    /// </summary>
    public bool RunOnce { get; set; } = false;

    /// <summary>
    /// Grace period in minutes before deleting newly orphaned items.
    /// Prevents deletion of items that may be temporarily unavailable.
    /// </summary>
    public int GracePeriodMinutes { get; set; } = 0;

    /// <summary>
    /// Path prefix for detecting host paths when running in Docker.
    /// Maps container paths back to original host paths.
    /// </summary>
    public string HostPathPrefix { get; set; } = "/host-home";
}
```

### CleanupWorker Implementation

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.Cleanup;

public class CleanupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<CleanupOptions> _options;
    private readonly ILogger<CleanupWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public CleanupWorker(
        IServiceProvider serviceProvider,
        IOptions<CleanupOptions> options,
        ILogger<CleanupWorker> logger,
        IHostApplicationLifetime lifetime)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Cleanup worker starting. Mode: {Mode}, DryRun: {DryRun}",
            _options.Value.RunOnce ? "Once" : "Continuous",
            _options.Value.DryRun);

        // Run immediately on startup
        await RunCleanupCycleAsync(stoppingToken);

        if (_options.Value.RunOnce)
        {
            _logger.LogInformation("Single run complete. Shutting down.");
            _lifetime.StopApplication();
            return;
        }

        // Continuous mode with PeriodicTimer
        var interval = TimeSpan.FromMinutes(_options.Value.IntervalMinutes);
        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunCleanupCycleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Cleanup worker stopping");
        }
    }

    private async Task RunCleanupCycleAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting cleanup cycle at {Time}", DateTimeOffset.UtcNow);

        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var detectors = scope.ServiceProvider.GetServices<IOrphanDetector>();

            foreach (var detector in detectors)
            {
                stoppingToken.ThrowIfCancellationRequested();
                await detector.DetectAndCleanAsync(
                    _options.Value.DryRun,
                    _options.Value.GracePeriodMinutes,
                    stoppingToken);
            }

            _logger.LogInformation("Cleanup cycle completed");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error during cleanup cycle");
        }
    }
}
```

### IOrphanDetector Interface

```csharp
namespace CompoundDocs.Cleanup.Services;

public interface IOrphanDetector
{
    /// <summary>
    /// Detects and removes orphaned data.
    /// </summary>
    /// <param name="dryRun">If true, only logs what would be deleted.</param>
    /// <param name="gracePeriodMinutes">Minimum age before deletion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DetectAndCleanAsync(
        bool dryRun,
        int gracePeriodMinutes,
        CancellationToken cancellationToken);
}
```

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information"
    }
  },
  "Cleanup": {
    "IntervalMinutes": 60,
    "CheckRemoteBranches": true,
    "DryRun": false,
    "GracePeriodMinutes": 0
  },
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5433;Database=compounding_docs;Username=compounding;Password=compounding"
  }
}
```

### Dockerfile

Create `docker/cleanup/Dockerfile`:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Copy csproj and restore
COPY src/CompoundDocs.Common/*.csproj src/CompoundDocs.Common/
COPY src/CompoundDocs.Cleanup/*.csproj src/CompoundDocs.Cleanup/
RUN dotnet restore src/CompoundDocs.Cleanup/CompoundDocs.Cleanup.csproj

# Copy source and build
COPY src/CompoundDocs.Common/ src/CompoundDocs.Common/
COPY src/CompoundDocs.Cleanup/ src/CompoundDocs.Cleanup/
RUN dotnet publish src/CompoundDocs.Cleanup/CompoundDocs.Cleanup.csproj \
    -c Release \
    -o /app \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app

# Install git for branch detection
RUN apt-get update && apt-get install -y git && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

ENTRYPOINT ["dotnet", "CompoundDocs.Cleanup.dll"]
```

### Docker Compose Service Definition

Add to `docker-compose.yml`:

```yaml
  cleanup:
    build:
      context: ../..
      dockerfile: docker/cleanup/Dockerfile
    container_name: csharp-compounding-docs-cleanup
    depends_on:
      postgres:
        condition: service_healthy
    environment:
      - ConnectionStrings__Default=Host=postgres;Database=compounding_docs;Username=compounding;Password=compounding
      - Cleanup__IntervalMinutes=60
      - Cleanup__CheckRemoteBranches=true
      - Cleanup__GracePeriodMinutes=0
    volumes:
      # Mount host home directory for path detection (read-only)
      - ${HOME}:/host-home:ro
    restart: unless-stopped
```

### Volume Mount Strategy

The cleanup app needs to detect whether repository paths still exist on the host filesystem. When running in Docker:

1. **Host Path Mapping**: The user's home directory is mounted at `/host-home` (read-only)
2. **Path Translation**: Paths stored in the database (e.g., `/Users/john/repos/myproject`) are translated to container paths (e.g., `/host-home/repos/myproject`)
3. **Detection Logic**: `Directory.Exists()` is called on the translated path

```csharp
// In OrphanedPathDetector
private string TranslateToContainerPath(string hostPath, string hostHomePrefix)
{
    // hostPath: /Users/john/repos/myproject
    // HOME env: /Users/john
    // Returns: /host-home/repos/myproject

    var homeDir = Environment.GetEnvironmentVariable("ORIGINAL_HOME")
        ?? throw new InvalidOperationException("ORIGINAL_HOME not set");

    if (hostPath.StartsWith(homeDir))
    {
        return _options.Value.HostPathPrefix + hostPath[homeDir.Length..];
    }

    return hostPath; // Path outside home directory, cannot check
}
```

---

## Dependencies

### Depends On

- **Phase 001**: Solution & Project Structure (solution file, directory structure)
- **Phase 007**: Database Configuration (schema for `tenant_management.repo_paths` and `tenant_management.branches`)

### Blocks

- Phase 012: Orphan Detection Implementation (builds on this structure)
- Infrastructure integration testing

---

## Verification Steps

After completing this phase, verify:

1. **Project builds**: `dotnet build src/CompoundDocs.Cleanup/`
2. **Solution includes project**: `dotnet sln list` shows CompoundDocs.Cleanup
3. **Help output works**: `dotnet run --project src/CompoundDocs.Cleanup -- --help`
4. **Dry run executes**: `dotnet run --project src/CompoundDocs.Cleanup -- --once --dry-run`
5. **Docker builds**: `docker build -f docker/cleanup/Dockerfile .`
6. **Configuration loads**: Verify logging shows correct configuration values

---

## Notes

- The cleanup worker uses `PeriodicTimer` (.NET 6+) for clean async periodic execution
- Scoped services are created for each cleanup cycle to ensure fresh database connections
- The `--once` flag is useful for manual cleanup runs or debugging
- Grace period prevents deletion of paths that may be temporarily unmounted
- Docker volume mount is read-only to prevent accidental modifications to host filesystem
- The `ORIGINAL_HOME` environment variable should be set in docker-compose.yml to enable path translation
