# Phase 017: Dependency Injection Container Setup

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-5 hours
> **Category**: Infrastructure Setup
> **Prerequisites**: Phase 001 (Solution & Project Structure)

---

## Spec References

This phase implements the dependency injection infrastructure defined in:

- **spec/mcp-server.md** - .NET Generic Host architecture and service interfaces
- **research/dotnet-dependency-injection-patterns.md** - Comprehensive DI patterns guide

---

## Objectives

1. Configure Microsoft.Extensions.DependencyInjection in the MCP server host
2. Establish interface-based service registration patterns
3. Implement the IOptions pattern for configuration binding
4. Define service lifetime conventions (Singleton, Scoped, Transient)
5. Enable service provider validation for development builds
6. Create extension methods for organized service registration

---

## Acceptance Criteria

### Service Registration Infrastructure

- [ ] `ServiceCollectionExtensions.cs` exists with organized registration methods
- [ ] Core service registration extension: `AddCompoundDocsCore()`
- [ ] Infrastructure service registration extension: `AddCompoundDocsInfrastructure()`
- [ ] MCP-specific service registration extension: `AddMcpServices()`

### Service Lifetime Conventions

- [ ] Singleton services registered:
  - [ ] `IConfiguration` (auto-registered by host)
  - [ ] `ILogger<T>` (auto-registered by host)
  - [ ] `IOptions<T>` variants for configuration
  - [ ] Background services (file watcher, sync service)
- [ ] Scoped services registered:
  - [ ] `IDocumentRepository` - per-request document operations
  - [ ] `ITenantContext` - per-request tenant isolation
  - [ ] `IUnitOfWork` - per-request transaction boundary
- [ ] Transient services registered:
  - [ ] `IDateTimeProvider` - stateless utility
  - [ ] `IChunkingService` - stateless document processing

### Options Pattern Integration

- [ ] `CompoundDocsOptions` class with configuration properties
- [ ] `OllamaOptions` class for embedding/RAG model configuration
- [ ] `FileWatcherOptions` class for debounce and path configuration
- [ ] `DatabaseOptions` class for connection and pool settings
- [ ] All options classes use `ValidateDataAnnotations()` and `ValidateOnStart()`

### Service Provider Validation

- [ ] Development builds enable `ValidateScopes` option
- [ ] Development builds enable `ValidateOnBuild` option
- [ ] Captive dependency detection is active in development

### Interface Registrations

- [ ] `IEmbeddingService` -> `SemanticKernelEmbeddingService`
- [ ] `IDocumentRepository` -> `PostgresDocumentRepository`
- [ ] `IChunkingService` -> `MarkdownChunkingService`
- [ ] `IFileWatcherService` -> `FileSystemWatcherService`
- [ ] `IRagService` -> `SemanticKernelRagService`

---

## Implementation Notes

### Host Configuration Pattern

Use the modern `Host.CreateApplicationBuilder()` pattern for .NET 9.0:

```csharp
// Program.cs
using CompoundDocs.McpServer.Extensions;

var builder = Host.CreateApplicationBuilder(args);

// Configure services using extension methods
builder.Services
    .AddCompoundDocsOptions(builder.Configuration)
    .AddCompoundDocsCore()
    .AddCompoundDocsInfrastructure(builder.Configuration)
    .AddMcpServices();

// Enable validation in development
if (builder.Environment.IsDevelopment())
{
    builder.Services.Configure<ServiceProviderOptions>(options =>
    {
        options.ValidateScopes = true;
        options.ValidateOnBuild = true;
    });
}

var host = builder.Build();
await host.RunAsync();
```

### Service Registration Extension Methods

Create `Extensions/ServiceCollectionExtensions.cs`:

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers options classes with validation.
    /// </summary>
    public static IServiceCollection AddCompoundDocsOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptionsWithValidateOnStart<CompoundDocsOptions>()
            .Bind(configuration.GetSection(CompoundDocsOptions.SectionName))
            .ValidateDataAnnotations();

        services.AddOptionsWithValidateOnStart<OllamaOptions>()
            .Bind(configuration.GetSection(OllamaOptions.SectionName))
            .ValidateDataAnnotations();

        services.AddOptionsWithValidateOnStart<FileWatcherOptions>()
            .Bind(configuration.GetSection(FileWatcherOptions.SectionName))
            .ValidateDataAnnotations();

        services.AddOptionsWithValidateOnStart<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations();

        return services;
    }

    /// <summary>
    /// Registers core domain services.
    /// </summary>
    public static IServiceCollection AddCompoundDocsCore(
        this IServiceCollection services)
    {
        // Transient - stateless utilities
        services.AddTransient<IDateTimeProvider, DateTimeProvider>();
        services.AddTransient<IChunkingService, MarkdownChunkingService>();

        // Scoped - per-request services
        services.AddScoped<ITenantContext, TenantContext>();

        return services;
    }

    /// <summary>
    /// Registers infrastructure services (database, external APIs).
    /// </summary>
    public static IServiceCollection AddCompoundDocsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        services.AddNpgsqlDataSource(
            configuration.GetConnectionString("PostgreSQL")!);

        // Repositories - scoped for connection management
        services.AddScoped<IDocumentRepository, PostgresDocumentRepository>();

        // Embedding service - singleton (thread-safe, expensive to create)
        services.AddSingleton<IEmbeddingService, SemanticKernelEmbeddingService>();

        // RAG service - singleton (stateless, uses injected dependencies)
        services.AddSingleton<IRagService, SemanticKernelRagService>();

        return services;
    }

    /// <summary>
    /// Registers MCP server and tool services.
    /// </summary>
    public static IServiceCollection AddMcpServices(
        this IServiceCollection services)
    {
        // Background services - singleton
        services.AddHostedService<FileWatcherService>();
        services.AddHostedService<ReconciliationService>();

        // MCP tool handlers - transient (stateless)
        services.AddTransient<IRagQueryHandler, RagQueryHandler>();
        services.AddTransient<ISemanticSearchHandler, SemanticSearchHandler>();
        services.AddTransient<IIndexDocumentHandler, IndexDocumentHandler>();

        return services;
    }
}
```

### Options Classes Structure

Create `Options/CompoundDocsOptions.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace CompoundDocs.McpServer.Options;

public class CompoundDocsOptions
{
    public const string SectionName = "CompoundDocs";

    [Required]
    public string DocsPath { get; set; } = "./csharp-compounding-docs";

    [Range(1, 10)]
    public int MaxLinkDepth { get; set; } = 2;

    [Range(1, 20)]
    public int MaxLinkedDocs { get; set; } = 5;

    [Range(0.0, 1.0)]
    public float DefaultRelevanceThreshold { get; set; } = 0.7f;

    [Range(1, 20)]
    public int DefaultMaxSources { get; set; } = 3;
}

public class OllamaOptions
{
    public const string SectionName = "Ollama";

    [Required]
    [Url]
    public string Endpoint { get; set; } = "http://localhost:11434";

    [Required]
    public string EmbeddingModel { get; set; } = "mxbai-embed-large";

    [Required]
    public string RagModel { get; set; } = "mistral";

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 60;
}

public class FileWatcherOptions
{
    public const string SectionName = "FileWatcher";

    [Required]
    public string WatchPath { get; set; } = "./csharp-compounding-docs";

    [Range(100, 5000)]
    public int DebounceMilliseconds { get; set; } = 500;

    public string FileFilter { get; set; } = "*.md";

    public bool IncludeSubdirectories { get; set; } = true;
}

public class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Range(1, 100)]
    public int MaxPoolSize { get; set; } = 20;

    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; set; } = 30;
}
```

### Keyed Services for Multiple Implementations

If multiple cache or storage backends are needed, use keyed services:

```csharp
// Example: Different embedding models for different use cases
builder.Services.AddKeyedSingleton<IEmbeddingService, OllamaEmbeddingService>("ollama");
builder.Services.AddKeyedSingleton<IEmbeddingService, OpenAIEmbeddingService>("openai");

// Consumer uses [FromKeyedServices] attribute
public class HybridEmbeddingService(
    [FromKeyedServices("ollama")] IEmbeddingService localEmbedding,
    [FromKeyedServices("openai")] IEmbeddingService cloudEmbedding)
{
    // Use local for development, cloud for production
}
```

### Scoped Services in Background Services

Background services (singletons) that need scoped services must use `IServiceScopeFactory`:

```csharp
public class FileWatcherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FileWatcherService> _logger;

    public FileWatcherService(
        IServiceScopeFactory scopeFactory,
        ILogger<FileWatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();

            // Process file changes with scoped repository
            await ProcessPendingChangesAsync(repository, stoppingToken);
        }
    }
}
```

### Service Lifetime Decision Matrix

| Service Type | Lifetime | Rationale |
|-------------|----------|-----------|
| Configuration options (`IOptions<T>`) | Singleton | Read once at startup, immutable |
| Logging (`ILogger<T>`) | Singleton | Thread-safe, shared infrastructure |
| Embedding service | Singleton | Expensive HTTP client, thread-safe |
| RAG service | Singleton | Stateless, uses other singletons |
| Document repository | Scoped | Database connection per request |
| Tenant context | Scoped | Request-specific isolation |
| Chunking service | Transient | Stateless, lightweight |
| Date/time provider | Transient | Stateless utility |
| MCP tool handlers | Transient | Stateless, created per invocation |
| Background services | Singleton | Long-running, managed by host |

---

## Dependencies

### Depends On
- Phase 001: Solution & Project Structure (solution file must exist)

### Blocks
- Phase 018+: Any phase requiring service injection
- MCP tool implementation phases
- Repository implementation phases
- Background service implementation phases

---

## Verification Steps

After completing this phase, verify:

1. **Service registration compiles**: No missing type references
2. **Options validation**: Invalid appsettings.json values cause startup failure
3. **Scope validation**: Captive dependency errors are caught in development
4. **Extension methods**: Clean, chainable service registration in Program.cs
5. **Interface resolution**: All registered interfaces can be resolved

### Manual Verification

```bash
# Build the project
dotnet build src/CompoundDocs.McpServer/

# Run with invalid configuration (should fail on startup)
echo '{"Ollama":{"Endpoint":"not-a-url"}}' > appsettings.Development.json
dotnet run --project src/CompoundDocs.McpServer/
# Expected: OptionsValidationException at startup

# Run with valid configuration
dotnet run --project src/CompoundDocs.McpServer/
# Expected: Host starts successfully
```

### Unit Test Verification

```csharp
[Fact]
public void ServiceRegistration_ResolvesAllCoreServices()
{
    // Arrange
    var services = new ServiceCollection();
    var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build();

    // Act
    services.AddCompoundDocsOptions(configuration);
    services.AddCompoundDocsCore();
    services.AddCompoundDocsInfrastructure(configuration);

    using var provider = services.BuildServiceProvider(validateScopes: true);

    // Assert - all services resolve without exception
    using var scope = provider.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
    var embedding = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
    var chunking = scope.ServiceProvider.GetRequiredService<IChunkingService>();

    Assert.NotNull(repository);
    Assert.NotNull(embedding);
    Assert.NotNull(chunking);
}
```

---

## Configuration Files

### appsettings.json Structure

```json
{
  "CompoundDocs": {
    "DocsPath": "./csharp-compounding-docs",
    "MaxLinkDepth": 2,
    "MaxLinkedDocs": 5,
    "DefaultRelevanceThreshold": 0.7,
    "DefaultMaxSources": 3
  },
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "EmbeddingModel": "mxbai-embed-large",
    "RagModel": "mistral",
    "TimeoutSeconds": 60
  },
  "FileWatcher": {
    "WatchPath": "./csharp-compounding-docs",
    "DebounceMilliseconds": 500,
    "FileFilter": "*.md",
    "IncludeSubdirectories": true
  },
  "Database": {
    "ConnectionString": "",
    "MaxPoolSize": 20,
    "CommandTimeoutSeconds": 30
  },
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Database=compounddocs;Username=postgres;Password=postgres"
  }
}
```

---

## Notes

- Service provider validation (`ValidateScopes`, `ValidateOnBuild`) should only be enabled in Development to avoid performance overhead in production
- The IOptions pattern family (`IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>`) is preferred over direct `IConfiguration` injection for type safety and validation
- Background services that need scoped dependencies must create scopes explicitly via `IServiceScopeFactory`
- Keyed services (.NET 8+) eliminate the need for factory patterns when multiple implementations of the same interface are needed
- Consider using Scrutor for assembly scanning and decorator registration if the codebase grows significantly
