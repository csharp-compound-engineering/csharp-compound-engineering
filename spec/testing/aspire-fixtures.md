# Aspire Test Fixtures Specification

> **Status**: [DRAFT]
> **Last Updated**: 2025-01-23
> **Parent**: [spec/testing.md](../testing.md)

---

## Overview

This document specifies the .NET Aspire fixture patterns for integration and E2E testing of the `csharp-compounding-docs` plugin.

> **Background**: Comprehensive coverage of Aspire testing infrastructure, MCP client integration, database isolation strategies, and CI/CD considerations. See [.NET Aspire Testing with MCP Client Research](../../research/aspire-testing-mcp-client.md).

> **Background**: xUnit fixtures (`IClassFixture`, `ICollectionFixture`, `IAsyncLifetime`), test organization patterns, and best practices for structuring integration tests. See [Unit Testing in C#/.NET: xUnit, Moq, and Shouldly Research Report](../../research/unit-testing-xunit-moq-shouldly.md).

> **Background**: MCP C# SDK client patterns including `StdioClientTransport`, `McpClient` lifecycle management, and tool invocation. See [MCP C# SDK Comprehensive Research Report](../../research/mcp-csharp-sdk-research.md).

---

## Core Fixture: AspireIntegrationFixture

The primary fixture manages Aspire application lifecycle and provides access to infrastructure resources.

```csharp
using Aspire.Hosting.Testing;
using Aspire.Hosting.ApplicationModel;
using ModelContextProtocol.Client;
using Xunit;

namespace CompoundDocs.IntegrationTests.Fixtures;

public class AspireIntegrationFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public DistributedApplication App => _app
        ?? throw new InvalidOperationException("App not initialized");

    public string PostgresConnectionString { get; private set; } = string.Empty;
    public string OllamaEndpoint { get; private set; } = string.Empty;
    public McpClient? McpClient { get; private set; }

    public async Task InitializeAsync()
    {
        // Create the test host from AppHost project
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.CompoundDocs_AppHost>();

        // Configure logging for debugging
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Warning);
        });

        // Build and start
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // Wait for resources with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        // Wait for PostgreSQL to be healthy
        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("postgres", cts.Token);
        PostgresConnectionString = await _app
            .GetConnectionStringAsync("postgres", cts.Token)
            ?? throw new InvalidOperationException("PostgreSQL connection string not available");

        // Wait for Ollama (may take longer due to model downloads)
        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("ollama", cts.Token);

        // Get Ollama endpoint
        OllamaEndpoint = await GetOllamaEndpointAsync(cts.Token);

        // Initialize MCP Client for E2E testing
        await InitializeMcpClientAsync(cts.Token);
    }

    private async Task<string> GetOllamaEndpointAsync(CancellationToken cancellationToken)
    {
        var ollamaResource = App.Resources.Single(r => r.Name == "ollama");

        if (ollamaResource is ContainerResource container)
        {
            var endpoint = container.GetEndpoint("http");
            return endpoint?.Url
                ?? throw new InvalidOperationException("Ollama endpoint not available");
        }

        throw new NotSupportedException($"Resource type {ollamaResource.GetType()} not supported");
    }

    private async Task InitializeMcpClientAsync(CancellationToken cancellationToken)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "CompoundDocs",
            Command = "dotnet",
            Arguments = ["run", "--project", GetMcpServerProjectPath(), "--no-build"],
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["POSTGRES_CONNECTION"] = PostgresConnectionString,
                ["OLLAMA_ENDPOINT"] = OllamaEndpoint
            }
        });

        McpClient = await McpClient.CreateAsync(
            transport,
            clientOptions: new McpClientOptions
            {
                ClientInfo = new Implementation { Name = "IntegrationTests", Version = "1.0.0" },
                InitializationTimeout = TimeSpan.FromSeconds(60)
            },
            cancellationToken: cancellationToken);
    }

    private static string GetMcpServerProjectPath()
    {
        // Navigate from test project to MCP server project
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(
            baseDir, "..", "..", "..", "..",
            "src", "CompoundDocs.McpServer", "CompoundDocs.McpServer.csproj"));
    }

    public async Task DisposeAsync()
    {
        if (McpClient != null)
        {
            await McpClient.DisposeAsync();
        }

        if (_app != null)
        {
            await _app.DisposeAsync();
        }
    }
}
```

---

## Collection Definition

xUnit collection fixtures share a single fixture instance across all tests in the collection.

```csharp
[CollectionDefinition("Aspire")]
public class AspireCollection : ICollectionFixture<AspireIntegrationFixture>
{
    // This class has no code - it's a marker for xUnit
}
```

**Usage in test classes**:

```csharp
[Collection("Aspire")]
public class VectorStorageIntegrationTests
{
    private readonly AspireIntegrationFixture _fixture;

    public VectorStorageIntegrationTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task StoreVector_Persists_Successfully()
    {
        // Use _fixture.PostgresConnectionString
    }
}
```

---

## Resource Waiting Patterns

### WaitForResourceHealthyAsync vs WaitForResourceAsync

| Method | Use Case |
|--------|----------|
| `WaitForResourceAsync(..., KnownResourceStates.Running)` | Resource has started executing |
| `WaitForResourceHealthyAsync(...)` | Resource is ready to serve requests |

**Important**: A resource enters `Running` as soon as it starts. For databases and services with health checks, always use `WaitForResourceHealthyAsync`.

### Timeout Recommendations

| Resource | Timeout | Rationale |
|----------|---------|-----------|
| PostgreSQL | 1 min | Container start + init scripts |
| Ollama | 1 min | Container start only |

**Note on Ollama model downloads**: The Aspire fixture timeout applies only to **container startup** (HTTP endpoint becoming available), not model downloads. Model downloads happen lazily when tests first invoke the embedding API. Ollama handles this internally—tests simply wait for the API response. There is no need to specify model download timeouts; tests take as long as they need.

---

## Database Fixture (Lightweight Alternative)

For tests that only need database access without full Aspire:

```csharp
public class DatabaseFixture : IAsyncLifetime
{
    public NpgsqlConnection Connection { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Use environment variable or test configuration
        var connectionString = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")
            ?? "Host=localhost;Database=test_compounddocs;Username=postgres;Password=postgres";

        Connection = new NpgsqlConnection(connectionString);
        await Connection.OpenAsync();

        // Ensure pgvector extension
        await using var cmd = Connection.CreateCommand();
        cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS vector;";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await Connection.CloseAsync();
        await Connection.DisposeAsync();
    }
}
```

---

## Database Isolation Strategies

### Strategy 1: Unique Collection Names (Recommended)

Each test uses a unique collection name for isolation:

```csharp
public class VectorSearchTests : IAsyncLifetime
{
    private readonly AspireIntegrationFixture _fixture;
    private readonly string _collection;

    public VectorSearchTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
        _collection = $"test_{Guid.NewGuid():N}";
    }

    public async Task InitializeAsync()
    {
        // Seed test data to _collection
    }

    public async Task DisposeAsync()
    {
        // Cleanup
        await _fixture.McpClient!.CallToolAsync("delete_collection",
            new Dictionary<string, object?> { ["collection"] = _collection });
    }
}
```

### Strategy 2: TRUNCATE Between Tests

For shared state scenarios:

```csharp
public static class DatabaseCleaner
{
    public static async Task CleanAsync(NpgsqlConnection connection)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            TRUNCATE TABLE documents CASCADE;
            TRUNCATE TABLE embeddings CASCADE;
            TRUNCATE TABLE chunks CASCADE;";
        await cmd.ExecuteNonQueryAsync();
    }
}
```

### Strategy 3: Schema Per Test Class

For complete isolation:

```csharp
public class SchemaIsolatedFixture : IAsyncLifetime
{
    private readonly string _schemaName = $"test_{Guid.NewGuid():N}";
    public string SchemaName => _schemaName;

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(BaseConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            CREATE SCHEMA {_schemaName};
            SET search_path TO {_schemaName};
            -- Run migrations here
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        await using var connection = new NpgsqlConnection(BaseConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"DROP SCHEMA {_schemaName} CASCADE;";
        await cmd.ExecuteNonQueryAsync();
    }
}
```

---

## xUnit Configuration for Fixtures

### xunit.runner.json

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeAssembly": false,
  "parallelizeTestCollections": false,
  "maxParallelThreads": 1,
  "diagnosticMessages": true,
  "longRunningTestSeconds": 60
}
```

**Rationale**: Integration tests share database state via the Aspire fixture. Parallel execution would cause race conditions and test interference.

---

## Test Helpers

### Retry Helper for Async Operations

```csharp
public static class TestHelpers
{
    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> operation,
        Func<T, bool> successCondition,
        int maxAttempts = 5,
        TimeSpan? delayBetweenAttempts = null)
    {
        var delay = delayBetweenAttempts ?? TimeSpan.FromSeconds(1);

        for (int i = 0; i < maxAttempts; i++)
        {
            var result = await operation();
            if (successCondition(result))
                return result;

            if (i < maxAttempts - 1)
                await Task.Delay(delay);
        }

        throw new TimeoutException($"Operation did not succeed after {maxAttempts} attempts");
    }

    public static async Task<bool> WaitForConditionAsync(
        Func<Task<bool>> condition,
        TimeSpan timeout,
        TimeSpan pollInterval)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return true;
            await Task.Delay(pollInterval);
        }
        return false;
    }
}
```

---

## Change Log

| Date | Changes |
|------|---------|
| 2025-01-23 | Initial draft |
