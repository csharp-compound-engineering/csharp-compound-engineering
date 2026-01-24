# .NET Aspire Testing with MCP Client Research

## Executive Summary

This document provides comprehensive research on using .NET Aspire for integration and E2E testing with the C# MCP client SDK. It covers test infrastructure setup, MCP client patterns, database isolation strategies, and concrete code examples for the `csharp-compounding-docs` plugin.

## 1. Aspire.Hosting.Testing Package

### Overview

The `Aspire.Hosting.Testing` NuGet package provides the `DistributedApplicationTestingBuilder` class for creating test hosts that run your AppHost with instrumentation hooks.

**Key Package References:**
```xml
<PackageReference Include="Aspire.Hosting.Testing" Version="13.1.0" />
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
```

### DistributedApplicationTestingBuilder Usage

```csharp
// Create the test host from your AppHost project
var appHost = await DistributedApplicationTestingBuilder
    .CreateAsync<Projects.CsharpCompoundingDocs_AppHost>();

// Build and start the application
await using var app = await appHost.BuildAsync();
await app.StartAsync();
```

### Resource Health Checks and Readiness

```csharp
// Wait for a resource to be running
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
await app.ResourceNotifications.WaitForResourceAsync(
    "postgres",
    KnownResourceStates.Running,
    cts.Token);

// For resources with health checks, wait for healthy state
await app.ResourceNotifications.WaitForResourceHealthyAsync(
    "webfrontend",
    cts.Token);
```

**Important:** A resource enters the `Running` state as soon as it starts executing, but this does not mean it is ready to serve requests. Use `WaitForResourceHealthyAsync` for resources with health checks.

### Accessing Resources in Tests

```csharp
// HTTP resources - use CreateHttpClient
var httpClient = app.CreateHttpClient("api-service");

// Database resources - use GetConnectionString
var connectionString = await app.GetConnectionStringAsync("postgres");

// Use connection string with Npgsql
await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync();
```

---

## 2. MCP Client for Testing

### Package Installation

```bash
dotnet add package ModelContextProtocol --prerelease
```

### McpClient with StdioClientTransport

For testing an MCP server running as a child process:

```csharp
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

// Configure transport to launch server process
var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "CsharpCompoundingDocs Server",
    Command = "dotnet",
    Arguments = ["run", "--project", "path/to/McpServer.csproj"],
    WorkingDirectory = "/path/to/server",
    EnvironmentVariables = new Dictionary<string, string>
    {
        ["POSTGRES_CONNECTION"] = connectionString,
        ["OLLAMA_ENDPOINT"] = ollamaEndpoint
    }
});

// Create and initialize client with timeout
var client = await McpClient.CreateAsync(
    transport,
    clientOptions: new McpClientOptions
    {
        ClientInfo = new Implementation { Name = "TestClient", Version = "1.0.0" },
        InitializationTimeout = TimeSpan.FromSeconds(30)
    },
    cancellationToken: cancellationToken);

// Verify connection
Console.WriteLine($"Connected to: {client.ServerInfo.Name} v{client.ServerInfo.Version}");
```

### McpClient with HTTP Transport

For HTTP/SSE-based MCP servers:

```csharp
using ModelContextProtocol.Client;

var transport = new HttpClientTransport(new HttpClientTransportOptions
{
    Endpoint = new Uri("http://localhost:3001/mcp"),
    TransportMode = HttpTransportMode.AutoDetect,
    ConnectionTimeout = TimeSpan.FromSeconds(30),
    RequestTimeout = TimeSpan.FromMinutes(5)
});

var client = await McpClient.CreateAsync(transport);

// Check server capabilities
if (client.ServerCapabilities.Tools?.ListChanged == true)
{
    Console.WriteLine("Server supports dynamic tool updates");
}
```

### Calling MCP Tools

```csharp
// List available tools
IList<McpClientTool> tools = await client.ListToolsAsync();
foreach (var tool in tools)
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
}

// Call a tool with arguments
var result = await client.CallToolAsync(
    "index_document",
    new Dictionary<string, object?>
    {
        ["path"] = "/path/to/document.md",
        ["collection"] = "default"
    },
    cancellationToken: CancellationToken.None);

// Process result
var textContent = result.Content.OfType<TextContentBlock>().FirstOrDefault();
Console.WriteLine(textContent?.Text);
```

### Reading MCP Resources

```csharp
// List available resources
IList<McpClientResource> resources = await client.ListResourcesAsync(cancellationToken);
foreach (var resource in resources)
{
    Console.WriteLine($"Resource: {resource.Name} ({resource.Uri})");
}

// Read a specific resource
ReadResourceResult result = await client.ReadResourceAsync(
    "docs://indexed/config",
    cancellationToken);

foreach (var content in result.Contents)
{
    if (content.Type == "text")
    {
        Console.WriteLine($"Content: {content.Text}");
    }
}

// Read from resource template with parameters
var templateResult = await client.ReadResourceAsync(
    "docs://search/{query}",
    new Dictionary<string, object?> { ["query"] = "embeddings" },
    cancellationToken);
```

### Error Handling

```csharp
try
{
    var result = await client.CallToolAsync("nonexistent_tool", new Dictionary<string, object?>());
}
catch (McpProtocolException ex) when (ex.ErrorCode == McpErrorCode.InvalidRequest)
{
    Console.WriteLine($"Tool not found: {ex.Message}");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation timed out");
}
finally
{
    await client.DisposeAsync();
}
```

---

## 3. Integration Test Patterns

### Test Fixture with IAsyncLifetime

```csharp
using Aspire.Hosting.Testing;
using Aspire.Hosting.ApplicationModel;
using ModelContextProtocol.Client;
using Xunit;

namespace CsharpCompoundingDocs.IntegrationTests;

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
        // Create the test host
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.CsharpCompoundingDocs_AppHost>();

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

        // Wait for PostgreSQL
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("postgres", cts.Token);
        PostgresConnectionString = await _app.GetConnectionStringAsync("postgres", cts.Token)
            ?? throw new InvalidOperationException("PostgreSQL connection string not available");

        // Wait for Ollama (may take longer due to model downloads)
        await _app.ResourceNotifications.WaitForResourceHealthyAsync("ollama", cts.Token);

        // Get Ollama endpoint
        var ollamaResource = _app.Resources.Single(r => r.Name == "ollama");
        OllamaEndpoint = await GetResourceEndpointAsync(ollamaResource, cts.Token);

        // Initialize MCP Client
        await InitializeMcpClientAsync(cts.Token);
    }

    private async Task InitializeMcpClientAsync(CancellationToken cancellationToken)
    {
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "CsharpCompoundingDocs",
            Command = "dotnet",
            Arguments = ["run", "--project", GetMcpServerProjectPath()],
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

    private string GetMcpServerProjectPath()
    {
        // Navigate from test project to MCP server project
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..",
            "src", "CsharpCompoundingDocs.McpServer", "CsharpCompoundingDocs.McpServer.csproj"));
    }

    private async Task<string> GetResourceEndpointAsync(IResource resource, CancellationToken ct)
    {
        // Implementation depends on resource type
        // For containers, get the mapped port
        if (resource is ContainerResource container)
        {
            var endpoint = container.GetEndpoint("http");
            return endpoint?.Url ?? throw new InvalidOperationException("Endpoint not available");
        }
        throw new NotSupportedException($"Resource type {resource.GetType()} not supported");
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

### Collection Fixture for Shared Infrastructure

```csharp
[CollectionDefinition("Aspire")]
public class AspireCollection : ICollectionFixture<AspireIntegrationFixture>
{
    // This class has no code, it just defines the collection
}

[Collection("Aspire")]
public class RagQueryIntegrationTests
{
    private readonly AspireIntegrationFixture _fixture;

    public RagQueryIntegrationTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task IndexDocument_And_QueryRag_ReturnsRelevantResults()
    {
        // Arrange
        var mcpClient = _fixture.McpClient!;
        var testDocument = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}.md");
        await File.WriteAllTextAsync(testDocument, "# Test Document\n\nThis is about vector embeddings and RAG.");

        try
        {
            // Act - Index document
            var indexResult = await mcpClient.CallToolAsync(
                "index_document",
                new Dictionary<string, object?>
                {
                    ["path"] = testDocument,
                    ["collection"] = "test"
                });

            // Assert - Indexing succeeded
            var indexText = indexResult.Content.OfType<TextContentBlock>().First().Text;
            Assert.Contains("indexed", indexText, StringComparison.OrdinalIgnoreCase);

            // Act - Query via RAG
            var queryResult = await mcpClient.CallToolAsync(
                "rag_query",
                new Dictionary<string, object?>
                {
                    ["query"] = "What is this document about?",
                    ["collection"] = "test"
                });

            // Assert - Query returns relevant content
            var queryText = queryResult.Content.OfType<TextContentBlock>().First().Text;
            Assert.Contains("vector", queryText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            // Cleanup
            if (File.Exists(testDocument))
            {
                File.Delete(testDocument);
            }
        }
    }
}
```

### PostgreSQL with pgvector Setup

In your AppHost:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with pgvector custom image
var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg16")
    .WithDataVolume("pgvector-data")
    .WithLifetime(ContainerLifetime.Persistent);

var vectorDb = postgres.AddDatabase("vectordb");

// Add init script for pgvector extension
postgres.WithInitBindMount("./postgres-init");
```

Create `postgres-init/01-init-pgvector.sql`:
```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

### Ollama Setup with Model Pre-download

```csharp
var ollama = builder.AddOllama("ollama")
    .WithDataVolume("ollama-models")  // Persist models
    .WithLifetime(ContainerLifetime.Persistent)
    .AddModel("mxbai-embed-large")    // Embedding model
    .AddModel("mistral");              // RAG generation model
```

**Important:** First-run model downloads can take several minutes. The `WithDataVolume()` ensures models persist between container restarts.

---

## 4. Database Test Isolation Strategies

### Strategy 1: Transaction Rollback (Fast, Limited)

```csharp
public class TransactionIsolatedTest : IAsyncLifetime
{
    private NpgsqlConnection _connection = null!;
    private NpgsqlTransaction _transaction = null!;

    public async Task InitializeAsync()
    {
        _connection = new NpgsqlConnection(TestFixture.ConnectionString);
        await _connection.OpenAsync();
        _transaction = await _connection.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Test_WithIsolatedTransaction()
    {
        // Use _connection with _transaction
        // Changes automatically rolled back
    }
}
```

**Limitations:** Does not work if test spawns external processes (like MCP server) that create their own connections.

### Strategy 2: Unique Schema Per Test Class

```csharp
public class SchemaIsolatedFixture : IAsyncLifetime
{
    private readonly string _schemaName = $"test_{Guid.NewGuid():N}";

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(BaseConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $@"
            CREATE SCHEMA {_schemaName};
            SET search_path TO {_schemaName};
            -- Run migrations
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

### Strategy 3: TRUNCATE Between Tests

```csharp
public static class DatabaseCleaner
{
    public static async Task CleanAsync(NpgsqlConnection connection, string schema = "public")
    {
        var tables = new[] { "embeddings", "documents", "chunks" };

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = string.Join("\n",
            tables.Select(t => $"TRUNCATE TABLE {schema}.{t} CASCADE;"));
        await cmd.ExecuteNonQueryAsync();
    }
}

// In test setup
[Collection("Aspire")]
public class CleanDatabaseTests : IAsyncLifetime
{
    private readonly AspireIntegrationFixture _fixture;

    public CleanDatabaseTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(_fixture.PostgresConnectionString);
        await connection.OpenAsync();
        await DatabaseCleaner.CleanAsync(connection);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
```

### Strategy 4: Unique Collection Names (Recommended for Vector DB)

```csharp
public class VectorDbIsolatedTest
{
    private readonly string _collectionName = $"test_{Guid.NewGuid():N}";

    [Fact]
    public async Task Test_WithIsolatedCollection()
    {
        // Use _collectionName for all operations
        var result = await _mcpClient.CallToolAsync(
            "index_document",
            new Dictionary<string, object?>
            {
                ["path"] = testFile,
                ["collection"] = _collectionName
            });

        // Cleanup in finally block or DisposeAsync
    }
}
```

---

## 5. E2E Test Patterns

### Full Workflow Test

```csharp
[Collection("Aspire")]
[Trait("Category", "E2E")]
public class FullWorkflowE2ETests
{
    private readonly AspireIntegrationFixture _fixture;
    private readonly string _testCollection;

    public FullWorkflowE2ETests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
        _testCollection = $"e2e_{Guid.NewGuid():N}";
    }

    [Fact(Timeout = 120000)] // 2 minute timeout for embedding generation
    public async Task CompleteWorkflow_CreateDocument_Index_Query_ReturnsAccurateResponse()
    {
        var mcpClient = _fixture.McpClient!;

        // Step 1: Create test document
        var testDir = Path.Combine(Path.GetTempPath(), "e2e-test-docs");
        Directory.CreateDirectory(testDir);
        var testFile = Path.Combine(testDir, "architecture.md");

        await File.WriteAllTextAsync(testFile, @"
# System Architecture

## Overview
The system uses a microservices architecture with three main components:
1. API Gateway - handles authentication and routing
2. Document Service - manages document storage and retrieval
3. Vector Database - stores embeddings for semantic search

## Technology Stack
- PostgreSQL with pgvector for vector storage
- Ollama for local LLM inference
- .NET 8 for all services
");

        try
        {
            // Step 2: Index document
            var indexResult = await mcpClient.CallToolAsync(
                "index_document",
                new Dictionary<string, object?>
                {
                    ["path"] = testFile,
                    ["collection"] = _testCollection
                },
                cancellationToken: CancellationToken.None);

            var indexResponse = indexResult.Content.OfType<TextContentBlock>().First().Text;
            Assert.Contains("success", indexResponse, StringComparison.OrdinalIgnoreCase);

            // Step 3: Query via RAG
            var queryResult = await mcpClient.CallToolAsync(
                "rag_query",
                new Dictionary<string, object?>
                {
                    ["query"] = "What are the three main components of the system?",
                    ["collection"] = _testCollection,
                    ["top_k"] = 3
                },
                cancellationToken: CancellationToken.None);

            var queryResponse = queryResult.Content.OfType<TextContentBlock>().First().Text;

            // Assert response mentions the key components
            Assert.Contains("API Gateway", queryResponse, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Document Service", queryResponse, StringComparison.OrdinalIgnoreCase);

            // Step 4: Verify document is listed in resources
            var resources = await mcpClient.ListResourcesAsync();
            Assert.Contains(resources, r => r.Uri.Contains(_testCollection));
        }
        finally
        {
            // Cleanup test files
            if (Directory.Exists(testDir))
            {
                Directory.Delete(testDir, recursive: true);
            }

            // Cleanup collection (if your MCP server supports it)
            try
            {
                await mcpClient.CallToolAsync(
                    "delete_collection",
                    new Dictionary<string, object?> { ["collection"] = _testCollection });
            }
            catch { /* Ignore if not supported */ }
        }
    }
}
```

### File Watcher Integration Test

```csharp
[Collection("Aspire")]
[Trait("Category", "E2E")]
public class FileWatcherE2ETests
{
    private readonly AspireIntegrationFixture _fixture;

    public FileWatcherE2ETests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(Timeout = 60000)]
    public async Task FileWatcher_NewDocument_AutomaticallyIndexed()
    {
        var mcpClient = _fixture.McpClient!;
        var watchedDir = await GetWatchedDirectoryAsync();
        var testFile = Path.Combine(watchedDir, $"auto-index-{Guid.NewGuid()}.md");

        try
        {
            // Create file in watched directory
            await File.WriteAllTextAsync(testFile, "# Auto-indexed Document\n\nThis should be indexed automatically.");

            // Wait for indexing (with polling)
            var indexed = await WaitForConditionAsync(
                async () =>
                {
                    var result = await mcpClient.CallToolAsync(
                        "search_documents",
                        new Dictionary<string, object?> { ["query"] = "auto-indexed" });
                    return result.Content.OfType<TextContentBlock>().Any(c =>
                        c.Text.Contains(Path.GetFileName(testFile)));
                },
                timeout: TimeSpan.FromSeconds(30),
                pollInterval: TimeSpan.FromSeconds(2));

            Assert.True(indexed, "Document was not auto-indexed within timeout");
        }
        finally
        {
            if (File.Exists(testFile))
            {
                File.Delete(testFile);
            }
        }
    }

    private static async Task<bool> WaitForConditionAsync(
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

## 6. xUnit Configuration

### Test Project Structure

```
tests/
  CsharpCompoundingDocs.Tests/                    # Unit tests
    CsharpCompoundingDocs.Tests.csproj
    Services/
      EmbeddingServiceTests.cs
      DocumentProcessorTests.cs

  CsharpCompoundingDocs.IntegrationTests/         # Integration tests
    CsharpCompoundingDocs.IntegrationTests.csproj
    Fixtures/
      AspireIntegrationFixture.cs
    Database/
      VectorStorageTests.cs
    Mcp/
      McpToolTests.cs
      McpResourceTests.cs

  CsharpCompoundingDocs.E2ETests/                 # End-to-end tests
    CsharpCompoundingDocs.E2ETests.csproj
    Fixtures/
      E2EFixture.cs
    Workflows/
      DocumentIndexingWorkflowTests.cs
      RagQueryWorkflowTests.cs
```

### Integration Test Project (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <!-- Test Framework -->
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
    <PackageReference Include="coverlet.collector" Version="6.0.4" />

    <!-- Assertions -->
    <PackageReference Include="Shouldly" Version="4.2.1" />
    <PackageReference Include="Moq" Version="4.20.72" />

    <!-- Aspire Testing -->
    <PackageReference Include="Aspire.Hosting.Testing" Version="13.1.0" />

    <!-- MCP Client -->
    <PackageReference Include="ModelContextProtocol" Version="0.6.0-preview.1" />

    <!-- Database -->
    <PackageReference Include="Npgsql" Version="9.0.2" />
  </ItemGroup>

  <ItemGroup>
    <!-- Reference to AppHost for DistributedApplicationTestingBuilder -->
    <ProjectReference Include="..\..\src\CsharpCompoundingDocs.AppHost\CsharpCompoundingDocs.AppHost.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- Global usings -->
    <Using Include="System.Net" />
    <Using Include="Microsoft.Extensions.DependencyInjection" />
    <Using Include="Microsoft.Extensions.Logging" />
    <Using Include="Aspire.Hosting.ApplicationModel" />
    <Using Include="Aspire.Hosting.Testing" />
    <Using Include="Xunit" />
    <Using Include="Shouldly" />
    <Using Include="ModelContextProtocol.Client" />
    <Using Include="ModelContextProtocol.Protocol" />
  </ItemGroup>

</Project>
```

### xunit.runner.json for Timeouts and Parallelization

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

### Trait-Based Test Filtering

```csharp
// Mark integration tests
[Trait("Category", "Integration")]
public class DatabaseIntegrationTests { }

// Mark E2E tests
[Trait("Category", "E2E")]
public class WorkflowE2ETests { }

// Mark slow tests
[Trait("Category", "Slow")]
public class ModelDownloadTests { }
```

Run specific categories:
```bash
# Run only unit tests (fast)
dotnet test --filter "Category!=Integration&Category!=E2E"

# Run only integration tests
dotnet test --filter "Category=Integration"

# Run everything except slow tests
dotnet test --filter "Category!=Slow"
```

---

## 7. CI/CD Considerations

### GitHub Actions Workflow

```yaml
name: Integration Tests

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  integration-tests:
    runs-on: ubuntu-latest
    timeout-minutes: 30

    services:
      # Pre-pull images to speed up tests
      docker:
        image: docker:24-dind
        options: --privileged

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Pull Docker images (parallel)
        run: |
          docker pull pgvector/pgvector:pg16 &
          docker pull ollama/ollama:latest &
          wait

      - name: Pre-download Ollama models
        run: |
          docker run -d --name ollama-setup -v ollama-models:/root/.ollama ollama/ollama
          docker exec ollama-setup ollama pull mxbai-embed-large
          docker exec ollama-setup ollama pull mistral
          docker stop ollama-setup

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Run Unit Tests
        run: dotnet test tests/CsharpCompoundingDocs.Tests --no-build

      - name: Run Integration Tests
        run: dotnet test tests/CsharpCompoundingDocs.IntegrationTests --no-build
        env:
          OLLAMA_MODELS_VOLUME: ollama-models

      - name: Run E2E Tests
        run: dotnet test tests/CsharpCompoundingDocs.E2ETests --no-build
        timeout-minutes: 15
```

### Handling Slow Model Downloads

```csharp
public class OllamaModelFixture : IAsyncLifetime
{
    private static readonly SemaphoreSlim _modelDownloadLock = new(1, 1);
    private static bool _modelsDownloaded = false;

    public async Task InitializeAsync()
    {
        await _modelDownloadLock.WaitAsync();
        try
        {
            if (!_modelsDownloaded)
            {
                // Wait for Ollama with extended timeout for model downloads
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                await WaitForModelsAsync(cts.Token);
                _modelsDownloaded = true;
            }
        }
        finally
        {
            _modelDownloadLock.Release();
        }
    }

    private async Task WaitForModelsAsync(CancellationToken ct)
    {
        // Poll Ollama API until models are available
        using var httpClient = new HttpClient { BaseAddress = new Uri(OllamaEndpoint) };

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var response = await httpClient.GetAsync("/api/tags", ct);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(ct);
                    if (content.Contains("mxbai-embed-large") && content.Contains("mistral"))
                    {
                        return;
                    }
                }
            }
            catch { /* Continue polling */ }

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
```

---

## 8. Challenges and Workarounds

### Challenge 1: MCP Server stdio Transport in Tests

**Problem:** The MCP server runs as a child process with stdio transport. Tests need to connect to it.

**Solution:** Configure `StdioClientTransport` with environment variables from Aspire resources:

```csharp
var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "TestMcpServer",
    Command = "dotnet",
    Arguments = ["run", "--project", mcpServerPath, "--no-build"],
    EnvironmentVariables = new Dictionary<string, string>
    {
        ["POSTGRES_CONNECTION"] = await app.GetConnectionStringAsync("postgres"),
        ["OLLAMA_ENDPOINT"] = ollamaEndpoint
    }
});
```

### Challenge 2: pgvector Extension Not Available

**Problem:** Standard PostgreSQL image does not include pgvector.

**Solution:** Use custom image with init script:

```csharp
var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg16")
    .WithInitBindMount("./init-scripts");
```

### Challenge 3: Test Flakiness with Async Operations

**Solution:** Use polling with timeouts:

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
}
```

### Challenge 4: Slow First-Run Model Downloads

**Solution:** Use persistent volumes and CI caching:

```csharp
// In AppHost
var ollama = builder.AddOllama("ollama")
    .WithDataVolume("ollama-models-cache")
    .WithLifetime(ContainerLifetime.Persistent);
```

---

## 9. Recommended Test Structure

### Should Integration and E2E Tests Share AppHost?

**Recommendation:** Yes, but with separate test collections.

```csharp
// Shared fixture
public class SharedAspireFixture : IAsyncLifetime
{
    // ... initialization code
}

// Integration tests collection
[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<SharedAspireFixture> { }

// E2E tests collection
[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<SharedAspireFixture> { }
```

**Benefits:**
- Single AppHost startup (expensive operation)
- Shared PostgreSQL and Ollama containers
- Faster overall test execution

**Important:** Ensure test isolation through:
- Unique collection names per test
- Database cleanup between tests
- No shared mutable state in fixtures

---

## Sources

- [Write your first Aspire test - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/aspire/testing/write-your-first-test?pivots=xunit)
- [.NET Aspire Testing Documentation](https://aspire.dev/testing/write-your-first-test/)
- [Model Context Protocol C# SDK - GitHub](https://github.com/modelcontextprotocol/csharp-sdk)
- [MCP C# SDK API Reference](https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.Client.html)
- [CommunityToolkit.Aspire.Hosting.Ollama - NuGet](https://www.nuget.org/packages/CommunityToolkit.Aspire.Hosting.Ollama)
- [pgvector Docker Image](https://hub.docker.com/r/pgvector/pgvector)
- [xUnit Shared Context Documentation](https://xunit.net/docs/shared-context)
- [Testcontainers Best Practices for .NET](https://www.milanjovanovic.tech/blog/testcontainers-best-practices-dotnet-integration-testing)
- [Getting started with testing and .NET Aspire](https://devblogs.microsoft.com/dotnet/getting-started-with-testing-and-dotnet-aspire/)
