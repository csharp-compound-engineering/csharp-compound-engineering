# Phase 115: Aspire Integration Fixture

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 109 (Aspire AppHost)

---

## Spec References

This phase implements the `AspireIntegrationFixture` class defined in:

- **spec/testing/aspire-fixtures.md** - [Core Fixture: AspireIntegrationFixture](../spec/testing/aspire-fixtures.md#core-fixture-aspireintegrationfixture) - Full fixture implementation, resource waiting patterns, timeout recommendations
- **spec/testing.md** - [Test Categories](../spec/testing.md#test-categories) - Integration and E2E test characteristics, timeout configuration
- **research/aspire-testing-mcp-client.md** - Comprehensive research on `DistributedApplicationTestingBuilder`, MCP client patterns, and database isolation strategies

---

## Objectives

1. Implement `AspireIntegrationFixture` class with `IAsyncLifetime` interface
2. Configure `DistributedApplicationTestingBuilder` to launch the AppHost
3. Provision PostgreSQL connection string from Aspire resources
4. Provision Ollama endpoint from Aspire container resources
5. Initialize MCP client using `StdioClientTransport` with environment variables
6. Implement proper fixture lifecycle (initialization and cleanup)
7. Create xUnit collection definition for shared fixture usage

---

## Acceptance Criteria

### AspireIntegrationFixture Class

- [ ] Class created at `tests/CompoundDocs.IntegrationTests/Fixtures/AspireIntegrationFixture.cs`
- [ ] Implements `IAsyncLifetime` interface (xUnit async fixture pattern)
- [ ] Exposes `DistributedApplication App` property
- [ ] Exposes `string PostgresConnectionString` property
- [ ] Exposes `string OllamaEndpoint` property
- [ ] Exposes `McpClient? McpClient` property
- [ ] Provides null-safety with appropriate exception for uninitialized access

### DistributedApplicationTestingBuilder Usage

- [ ] Uses `DistributedApplicationTestingBuilder.CreateAsync<Projects.CompoundDocs_AppHost>()`
- [ ] Configures logging via `appHost.Services.AddLogging()`
- [ ] Sets minimum log level to `Debug`
- [ ] Filters Aspire logs to `Warning` level
- [ ] Calls `BuildAsync()` and `StartAsync()` in sequence
- [ ] Properly stores `DistributedApplication` instance

### PostgreSQL Connection String Provision

- [ ] Waits for PostgreSQL resource to be healthy using `WaitForResourceHealthyAsync("postgres", ...)`
- [ ] Uses timeout of 1 minute for PostgreSQL (container start + init scripts)
- [ ] Retrieves connection string via `app.GetConnectionStringAsync("postgres", ...)`
- [ ] Throws descriptive exception if connection string unavailable
- [ ] Connection string stored in `PostgresConnectionString` property

### Ollama Endpoint Provision

- [ ] Waits for Ollama resource to be healthy using `WaitForResourceHealthyAsync("ollama", ...)`
- [ ] Uses timeout of 1 minute for Ollama (container start only, not model downloads)
- [ ] Retrieves Ollama endpoint from container resource
- [ ] Handles `ContainerResource` type to extract endpoint URL
- [ ] Endpoint stored in `OllamaEndpoint` property

### MCP Client Initialization (StdioClientTransport)

- [ ] Creates `StdioClientTransport` with proper options:
  - [ ] `Name` set to "CompoundDocs"
  - [ ] `Command` set to "dotnet"
  - [ ] `Arguments` includes `["run", "--project", <path>, "--no-build"]`
  - [ ] `EnvironmentVariables` includes `POSTGRES_CONNECTION` and `OLLAMA_ENDPOINT`
- [ ] Resolves MCP server project path relative to test project
- [ ] Creates `McpClient` via `McpClient.CreateAsync()`
- [ ] Sets `ClientInfo` with name "IntegrationTests" and version "1.0.0"
- [ ] Uses initialization timeout of 60 seconds
- [ ] Client stored in `McpClient` property

### Fixture Lifecycle (IAsyncLifetime)

- [ ] `InitializeAsync()` performs all setup in correct order:
  1. Create test host from AppHost
  2. Configure logging
  3. Build and start application
  4. Wait for PostgreSQL with timeout
  5. Retrieve PostgreSQL connection string
  6. Wait for Ollama with timeout
  7. Retrieve Ollama endpoint
  8. Initialize MCP client
- [ ] `DisposeAsync()` performs cleanup in correct order:
  1. Dispose MCP client (if initialized)
  2. Dispose distributed application (if initialized)
- [ ] Uses `CancellationTokenSource` with 5-minute overall timeout
- [ ] Handles null checks gracefully during disposal

### Collection Definition

- [ ] `AspireCollection` class created in fixtures directory
- [ ] Decorated with `[CollectionDefinition("Aspire")]`
- [ ] Implements `ICollectionFixture<AspireIntegrationFixture>`
- [ ] Empty class body (marker only)

### xUnit Configuration

- [ ] `xunit.runner.json` created/updated in integration test project
- [ ] `parallelizeAssembly` set to `false`
- [ ] `parallelizeTestCollections` set to `false`
- [ ] `maxParallelThreads` set to `1`
- [ ] `diagnosticMessages` set to `true`
- [ ] `longRunningTestSeconds` set to `60`

---

## Implementation Notes

### AspireIntegrationFixture.cs

```csharp
using Aspire.Hosting.Testing;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace CompoundDocs.IntegrationTests.Fixtures;

public class AspireIntegrationFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public DistributedApplication App => _app
        ?? throw new InvalidOperationException("App not initialized. Ensure InitializeAsync completed successfully.");

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

        // Wait for resources with overall timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        // Wait for PostgreSQL to be healthy (1 min timeout for container + init scripts)
        await _app.ResourceNotifications
            .WaitForResourceHealthyAsync("postgres", cts.Token);
        PostgresConnectionString = await _app
            .GetConnectionStringAsync("postgres", cts.Token)
            ?? throw new InvalidOperationException("PostgreSQL connection string not available");

        // Wait for Ollama to be healthy (1 min for container start, model downloads are lazy)
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
                ?? throw new InvalidOperationException("Ollama HTTP endpoint not available");
        }

        throw new NotSupportedException(
            $"Ollama resource type '{ollamaResource.GetType().Name}' is not supported. Expected ContainerResource.");
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
        // Navigate from test project output (bin/Debug/net9.0) to MCP server project
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

### AspireCollection.cs

```csharp
using Xunit;

namespace CompoundDocs.IntegrationTests.Fixtures;

/// <summary>
/// xUnit collection definition for tests that share the Aspire integration fixture.
/// This ensures a single Aspire application instance is shared across all tests in the collection.
/// </summary>
[CollectionDefinition("Aspire")]
public class AspireCollection : ICollectionFixture<AspireIntegrationFixture>
{
    // This class has no code - it's a marker for xUnit to wire up the fixture
}
```

### Usage in Test Classes

```csharp
using CompoundDocs.IntegrationTests.Fixtures;
using Xunit;

namespace CompoundDocs.IntegrationTests.Database;

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
        // Use _fixture.PostgresConnectionString for database operations
        await using var connection = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await connection.OpenAsync();

        // Test implementation...
    }
}
```

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

### Project File Updates

The integration test project must reference the AppHost:

```xml
<ItemGroup>
  <!-- Reference to AppHost for DistributedApplicationTestingBuilder -->
  <ProjectReference Include="..\..\src\CompoundDocs.AppHost\CompoundDocs.AppHost.csproj" />
</ItemGroup>
```

### Resource Waiting Patterns

| Method | Use Case | Notes |
|--------|----------|-------|
| `WaitForResourceAsync(..., KnownResourceStates.Running)` | Resource has started | Container is executing but may not be ready |
| `WaitForResourceHealthyAsync(...)` | Resource is ready | Health check passed, can serve requests |

**Important**: Always use `WaitForResourceHealthyAsync` for databases and services. A resource enters `Running` as soon as it starts, but this does not mean it is ready to accept connections.

### Timeout Recommendations

| Resource | Timeout | Rationale |
|----------|---------|-----------|
| PostgreSQL | 1 min | Container start + pgvector init scripts |
| Ollama | 1 min | Container start only (model downloads are lazy) |
| Overall fixture | 5 min | Allows for all resources + MCP client init |
| MCP client init | 60s | Server startup and handshake |

**Note on Ollama model downloads**: The fixture timeout applies only to container startup. Model downloads happen lazily when tests first invoke the embedding API. Tests simply wait for the API response; there is no need to specify model download timeouts separately.

---

## Dependencies

### Depends On

- **Phase 109**: Aspire AppHost (provides `CompoundDocs_AppHost` project)
- **Phase 021**: MCP Server Project (provides `CompoundDocs.McpServer` project)
- **Phase 003**: PostgreSQL pgvector Setup (database container configuration)
- **Phase 004**: Ollama Service (Ollama container configuration)

### Blocks

- **Phase 116**: Database Fixture (lightweight alternative, references patterns from this phase)
- **Phase 117**: Integration Test Project Structure (uses this fixture)
- **Phase 118**: E2E Test Project Structure (uses this fixture)

---

## Verification Steps

After completing this phase, verify:

1. **Fixture Compiles**: `AspireIntegrationFixture.cs` compiles without errors
2. **Collection Defined**: `AspireCollection` class is properly attributed
3. **AppHost Reference**: Integration test project references AppHost project
4. **xUnit Config**: `xunit.runner.json` exists with correct parallelization settings

### Integration Verification

Run a simple test to verify the fixture works:

```csharp
[Collection("Aspire")]
public class FixtureVerificationTests
{
    private readonly AspireIntegrationFixture _fixture;

    public FixtureVerificationTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Fixture_PostgresConnectionString_IsNotEmpty()
    {
        _fixture.PostgresConnectionString.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Fixture_OllamaEndpoint_IsNotEmpty()
    {
        _fixture.OllamaEndpoint.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Fixture_McpClient_IsInitialized()
    {
        _fixture.McpClient.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Fixture_PostgresConnection_CanConnect()
    {
        await using var connection = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await connection.OpenAsync();
        connection.State.ShouldBe(System.Data.ConnectionState.Open);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Fixture_McpClient_CanListTools()
    {
        var tools = await _fixture.McpClient!.ListToolsAsync();
        tools.ShouldNotBeNull();
    }
}
```

### Manual Verification

1. Run: `dotnet test tests/CompoundDocs.IntegrationTests --filter "Category=Integration"`
2. Verify Docker containers start for PostgreSQL and Ollama
3. Verify MCP server process spawns successfully
4. Verify tests pass without timeout errors
5. Verify containers are cleaned up after test run

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `tests/CompoundDocs.IntegrationTests/Fixtures/AspireIntegrationFixture.cs` | Main integration fixture |
| `tests/CompoundDocs.IntegrationTests/Fixtures/AspireCollection.cs` | xUnit collection definition |
| `tests/CompoundDocs.IntegrationTests/xunit.runner.json` | xUnit configuration |

### Modified Files

| File | Changes |
|------|---------|
| `tests/CompoundDocs.IntegrationTests/CompoundDocs.IntegrationTests.csproj` | Add AppHost project reference |

---

## Notes

- The `--no-build` flag in MCP server arguments assumes the solution was built before running tests
- In CI, ensure `dotnet build` runs before `dotnet test` for integration tests
- Model downloads for Ollama may cause first-run tests to be slow; use persistent volumes in development
- The fixture is designed for xUnit collection sharing to minimize AppHost startup overhead
- Tests using this fixture must use the `[Collection("Aspire")]` attribute
- Consider adding health check verification after MCP client init in production use
- The `McpClient` property is nullable to allow tests to check if initialization succeeded
