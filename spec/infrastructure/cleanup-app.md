# Cleanup Console App

> **Status**: [DRAFT]
> **Parent**: [infrastructure.md](../infrastructure.md)

> **Background**: For comprehensive patterns on implementing .NET BackgroundService workers with proper cancellation, error handling, and periodic execution using PeriodicTimer, see [IHostedService and BackgroundService Patterns in .NET](../../research/hosted-services-background-tasks.md).

> **Background**: For details on .NET Generic Host configuration, dependency injection in hosted services, and consuming scoped services (like database contexts) from singleton background workers, see [.NET Generic Host with MCP C# SDK Research Report](../../research/dotnet-generic-host-mcp-research.md).

---

## Purpose

A standalone .NET console app (`CompoundDocs.Cleanup`) that periodically removes orphaned data from the database.

---

## Orphan Detection

1. **Orphaned Paths**: Repo paths in `tenant_management.repo_paths` where the directory no longer exists on disk
2. **Orphaned Branches**: Branches in `tenant_management.branches` that no longer exist on the git remote

> **Background**: For checking whether branches exist on a remote using `git ls-remote --heads origin`, including scripting best practices and error handling, see [Git Current Branch Detection: Comprehensive Research](../../research/git-current-branch-detection.md).

---

## Implementation

```csharp
// Program.cs - CompoundDocs.Cleanup
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddHostedService<CleanupWorker>();
        services.Configure<CleanupOptions>(
            context.Configuration.GetSection("Cleanup"));
    })
    .Build();

await host.RunAsync();

// CleanupWorker.cs
public class CleanupWorker : BackgroundService
{
    private readonly IOptions<CleanupOptions> _options;
    private readonly NpgsqlDataSource _dataSource;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanOrphanedPathsAsync(stoppingToken);
            await CleanOrphanedBranchesAsync(stoppingToken);

            await Task.Delay(_options.Value.IntervalMinutes * 60 * 1000, stoppingToken);
        }
    }

    private async Task CleanOrphanedPathsAsync(CancellationToken ct)
    {
        // Query all paths from tenant_management.repo_paths
        // For each path, check if Directory.Exists(absolute_path)
        // If not exists, delete:
        //   1. All documents with matching path_hash
        //   2. All chunks with matching path_hash
        //   3. The path record itself
    }

    private async Task CleanOrphanedBranchesAsync(CancellationToken ct)
    {
        // For each unique project_name in tenant_management.branches
        // Get the repo path from repo_paths
        // Run: git ls-remote --heads origin
        // Compare with stored branches
        // Delete branches that no longer exist on remote:
        //   1. All documents with matching (project_name, branch_name)
        //   2. All chunks with matching (project_name, branch_name)
        //   3. The branch record itself
    }
}
```

---

## Configuration

```json
{
  "Cleanup": {
    "IntervalMinutes": 60,
    "CheckRemoteBranches": true,
    "DryRun": false
  }
}
```

---

## Running

> **Background**: For Docker Compose configuration patterns including health checks, volume mounting, and PostgreSQL connectivity from host or containerized applications, see [Docker Compose for PostgreSQL with Host-Based MCP Server](../../research/docker-compose-postgresql-host-mcp-research.md).

The cleanup app runs as a Docker container in the shared infrastructure stack:

```yaml
# Added to docker-compose.yml
cleanup:
  build:
    context: ./cleanup
    dockerfile: Dockerfile
  container_name: compounding-docs-cleanup
  depends_on:
    postgres:
      condition: service_healthy
  environment:
    - ConnectionStrings__Default=Host=postgres;Database=compounding_docs;Username=postgres;Password=compound123
    - Cleanup__IntervalMinutes=60
  volumes:
    # Mount paths to detect orphaned directories
    - ${HOME}:/host-home:ro
  restart: unless-stopped
```

**Manual execution** is also supported for one-time cleanup:

```bash
# Run once (outside Docker)
./CompoundDocs.Cleanup --once

# Dry run to preview deletions
./CompoundDocs.Cleanup --once --dry-run
```

---

## Safety Features

- **Logging**: All deletions logged with document IDs and paths
- **Dry Run Mode**: `--dry-run` flag to preview what would be deleted
- **Grace Period**: Optional delay before deleting newly orphaned items
