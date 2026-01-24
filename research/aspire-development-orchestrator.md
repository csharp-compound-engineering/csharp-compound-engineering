# .NET Aspire Research: Development Orchestrator for csharp-compounding-docs

## Executive Summary

.NET Aspire is a development-time orchestration framework that provides a code-first approach to managing distributed applications. For the `csharp-compounding-docs` plugin project, Aspire can effectively orchestrate the MCP server, PostgreSQL with pgvector, and Ollama during development, while Docker Compose handles production deployment.

**Key Finding**: Aspire generates Docker Compose files from its application model, enabling a smooth development-to-production transition. The AppHost project is development-only and not deployed to production.

---

## 1. Aspire Basics for Development

### What is .NET Aspire?

.NET Aspire is a **polyglot local dev-time orchestration toolchain** for building, running, debugging, and deploying distributed applications. Unlike Docker Compose (which is a container orchestration tool), Aspire provides:

- **Code-first configuration** - Define infrastructure in C# instead of YAML
- **Automatic service discovery** - Services find each other without hardcoded URLs
- **Built-in observability** - Dashboard with OpenTelemetry integration
- **Health checks and resilience** - Automatic health monitoring and retry policies
- **Hot reload and debugging** - Full IDE debugging experience

### Aspire vs Docker Compose

| Feature | .NET Aspire | Docker Compose |
|---------|-------------|----------------|
| Configuration | C# code | YAML files |
| Service Discovery | Automatic | Manual networking |
| Debugging | Full IDE support | Requires attach |
| Observability | Built-in dashboard | External tools needed |
| Environment | Development-focused | Production-ready |
| Container Runtime | Uses Docker/Podman | Uses Docker |

### AppHost Project Setup

The AppHost is the orchestrator project that defines your application topology:

```csharp
// AppHost/Program.cs
var builder = DistributedApplication.CreateBuilder(args);

// Add infrastructure
var postgres = builder.AddPostgres("db")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

var database = postgres.AddDatabase("docsdb");

// Add your MCP server project
var mcpServer = builder.AddProject<Projects.McpServer>("mcp-server")
    .WithReference(database)
    .WaitFor(postgres);

builder.Build().Run();
```

### Service Discovery

Aspire automatically injects connection information via environment variables and configuration:

```csharp
// In your service project
var connectionString = builder.Configuration.GetConnectionString("docsdb");
// Or via dependency injection with Aspire integrations
```

Referenced services can be accessed by name (e.g., `http://mcp-server`).

### Dashboard and Observability

Aspire includes a built-in dashboard that provides:
- **Resource status** - See all running services, containers, and their health
- **Structured logs** - Aggregated logs from all services
- **Distributed traces** - Request flow across services
- **Metrics** - Performance counters and custom metrics
- **Console output** - Real-time stdout/stderr from all resources

The dashboard runs automatically at `http://localhost:15041` (or similar) when you start the AppHost.

---

## 2. Integrating External Services

### PostgreSQL with pgvector

**Challenge**: Aspire doesn't have first-class pgvector support yet (see [GitHub Issue #3052](https://github.com/dotnet/aspire/issues/3052)).

**Solution 1: Use pgvector Docker Image**

```csharp
// AppHost/Program.cs
var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg16")  // Use pgvector image
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("vectordb");
```

**Solution 2: Use Initialization Script**

```csharp
var postgres = builder.AddPostgres("postgres")
    .WithBindMount("./init-scripts", "/docker-entrypoint-initdb.d")
    .WithDataVolume();
```

With `init-scripts/01-pgvector.sql`:
```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

**Solution 3: Custom Dockerfile**

```csharp
var postgres = builder.AddPostgres("postgres")
    .WithDockerfile("./postgres-pgvector");
```

With `postgres-pgvector/Dockerfile`:
```dockerfile
FROM postgres:16
RUN apt-get update && apt-get install -y postgresql-16-pgvector
```

### Ollama Integration

Ollama has **official Community Toolkit support** via `CommunityToolkit.Aspire.Hosting.Ollama`:

```bash
# In AppHost project
dotnet add package CommunityToolkit.Aspire.Hosting.Ollama
```

```csharp
// AppHost/Program.cs
var ollama = builder.AddOllama("ollama")
    .WithDataVolume()  // Persist downloaded models
    .WithOpenWebUI();  // Optional: adds Open WebUI for testing

// Add specific models
var embedModel = ollama.AddModel("embed", "mxbai-embed-large");
var ragModel = ollama.AddModel("rag", "mistral");

// Reference in your service
var mcpServer = builder.AddProject<Projects.McpServer>("mcp-server")
    .WithReference(embedModel)
    .WithReference(ragModel)
    .WaitFor(embedModel)
    .WaitFor(ragModel);
```

**Important**: Models are downloaded on first run. The Aspire dashboard shows download progress in the State column. Use `WithDataVolume()` to persist models across container restarts.

### Client Integration (in your service project)

```bash
# In your service project
dotnet add package CommunityToolkit.Aspire.OllamaSharp
```

```csharp
// In your service's Program.cs
builder.AddOllamaClientApi("embed");  // For embedding model
builder.AddOllamaClientApi("rag");    // For RAG model

// Or with Microsoft.Extensions.AI abstraction
builder.AddKeyedOllamaSharpChatClient("rag");
builder.AddKeyedOllamaSharpEmbeddingGenerator("embed");
```

---

## 3. MCP Server Development with Aspire

### Can stdio-based MCP Servers Work with Aspire?

**Yes, with caveats.** The MCP server can be developed and tested using Aspire, but the stdio transport has specific considerations:

1. **For development/testing**: Run the MCP server as an HTTP/SSE service, which integrates well with Aspire's service discovery and dashboard.

2. **For Claude Code integration**: The final MCP server uses stdio transport, which runs outside Aspire's orchestration when invoked by Claude Code.

### Hybrid Mode Approach

Use the `mcp-server-hybrid` pattern to support both transports:

```csharp
// MCP Server Program.cs
var builder = Host.CreateEmptyApplicationBuilder(settings: null);

if (args.Contains("--stdio"))
{
    // Claude Code invocation - use stdio
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();
}
else
{
    // Aspire development - use SSE/HTTP
    builder.Services
        .AddMcpServer()
        .WithHttpServerTransport()
        .WithToolsFromAssembly();

    // Add Aspire service defaults for observability
    builder.AddServiceDefaults();
}

await builder.Build().RunAsync();
```

### AppHost Configuration for MCP Server

```csharp
// For development with HTTP transport
var mcpServer = builder.AddProject<Projects.McpServer>("mcp-server")
    .WithReference(database)
    .WithReference(embedModel)
    .WithExternalHttpEndpoints()  // Expose for testing
    .WaitFor(postgres)
    .WaitFor(embedModel);
```

### Testing Strategy

1. **Unit tests**: Test MCP tools directly without transport
2. **Integration tests**: Use Aspire's `DistributedApplicationTestingBuilder`
3. **End-to-end**: Test stdio transport separately

```csharp
// Integration test example
[Fact]
public async Task McpServer_RespondsToToolCall()
{
    var appHost = await DistributedApplicationTestingBuilder
        .CreateAsync<Projects.AppHost>();

    await using var app = await appHost.BuildAsync();
    await app.StartAsync();

    var httpClient = app.CreateHttpClient("mcp-server");
    // Test your MCP endpoints
}
```

---

## 4. Development vs Production Parity

### Project Structure for Dual Orchestration

```
csharp-compounding-docs/
├── src/
│   ├── McpServer/                    # Main MCP server project
│   │   ├── McpServer.csproj
│   │   └── Program.cs
│   ├── McpServer.ServiceDefaults/    # Shared observability config
│   │   ├── ServiceDefaults.csproj
│   │   └── Extensions.cs
│   └── McpServer.AppHost/            # Development-only orchestrator
│       ├── AppHost.csproj
│       └── Program.cs
├── tests/
│   └── McpServer.Tests/
├── docker/
│   ├── docker-compose.yml            # Production deployment
│   ├── docker-compose.dev.yml        # Optional dev override
│   └── postgres-pgvector/
│       └── Dockerfile
├── csharp-compounding-docs.sln
└── CLAUDE.md
```

### AppHost is Dev-Only

The AppHost project:
- Is NOT deployed to production
- Has `<IsAspireHost>true</IsAspireHost>` in csproj
- References all services but is not referenced by them
- Can be excluded from CI/CD publish steps

### Generating Docker Compose from Aspire

Aspire can generate Docker Compose files for deployment:

```bash
# Generate Docker Compose files
aspire publish --publisher docker-compose --output ./docker

# Or use the older manifest approach
dotnet run --project ./src/McpServer.AppHost -- --publisher manifest
```

This generates:
- `docker-compose.yml` - Main compose file
- `.env` / `.env.Production` - Environment variables
- Container configurations matching your Aspire model

### Configuration Management

**Development (Aspire)**:
```csharp
// AppHost/Program.cs
var postgres = builder.AddPostgres("postgres")
    .WithEnvironment("POSTGRES_DB", "vectordb");
```

**Production (Docker Compose)**:
```yaml
# docker-compose.yml
services:
  postgres:
    image: pgvector/pgvector:pg16
    environment:
      POSTGRES_DB: vectordb
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
```

**Shared Configuration via Environment Variables**:
```csharp
// In your service - works with both Aspire and Docker Compose
var connectionString = builder.Configuration.GetConnectionString("postgres")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__postgres");
```

### Best Practices for Dual Orchestration

1. **Use environment variables for configuration** - Both Aspire and Docker Compose inject them the same way

2. **ServiceDefaults for observability** - Include in your service but make it optional for production:
   ```csharp
   if (builder.Environment.IsDevelopment())
   {
       builder.AddServiceDefaults();  // Aspire observability
   }
   ```

3. **Abstract infrastructure access** - Use interfaces that can be configured differently per environment

4. **Keep Docker Compose simple** - Let it focus on container orchestration, not app configuration

5. **Test both paths** - CI should test both Aspire (integration tests) and Docker Compose (deployment tests)

---

## 5. Recommended Project Structure

### Solution Layout

```
csharp-compounding-docs.sln
├── src/
│   ├── McpServer/
│   │   ├── McpServer.csproj
│   │   ├── Program.cs
│   │   ├── Tools/
│   │   │   ├── SearchDocsTool.cs
│   │   │   └── IndexDocsTool.cs
│   │   └── Services/
│   │       ├── VectorStore.cs
│   │       ├── EmbeddingService.cs
│   │       └── FileWatcherService.cs
│   │
│   ├── McpServer.ServiceDefaults/
│   │   ├── ServiceDefaults.csproj
│   │   └── Extensions.cs
│   │
│   └── McpServer.AppHost/
│       ├── AppHost.csproj
│       ├── Program.cs
│       └── init-scripts/
│           └── 01-pgvector.sql
│
├── tests/
│   ├── McpServer.Tests/
│   │   └── McpServer.Tests.csproj
│   └── McpServer.IntegrationTests/
│       └── McpServer.IntegrationTests.csproj
│
├── docker/
│   ├── docker-compose.yml
│   └── .env.example
│
└── CLAUDE.md
```

### Key Project Files

**McpServer.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ModelContextProtocol" Version="1.*" />
    <PackageReference Include="Microsoft.SemanticKernel" Version="1.*" />
    <PackageReference Include="Npgsql" Version="8.*" />
    <PackageReference Include="Pgvector" Version="0.*" />
    <PackageReference Include="OllamaSharp" Version="4.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\McpServer.ServiceDefaults\ServiceDefaults.csproj" />
  </ItemGroup>
</Project>
```

**AppHost.csproj**:
```xml
<Project Sdk="Aspire.AppHost.Sdk/9.0">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <IsAspireHost>true</IsAspireHost>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Aspire.Hosting.PostgreSQL" Version="9.*" />
    <PackageReference Include="CommunityToolkit.Aspire.Hosting.Ollama" Version="9.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\McpServer\McpServer.csproj" />
  </ItemGroup>
</Project>
```

**ServiceDefaults.csproj**:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsAspireSharedProject>true</IsAspireSharedProject>
  </PropertyGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" Version="9.*" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Extensions.Hosting" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Runtime" Version="1.*" />
  </ItemGroup>
</Project>
```

---

## 6. Limitations and Gotchas

### Aspire Limitations

1. **Community Toolkit packages are not officially supported** - The Ollama integration is maintained by the community, not Microsoft.

2. **No first-class pgvector support** - Requires workarounds (custom image, init scripts, or Dockerfile).

3. **AppHost doesn't deploy** - It's strictly for local development; you need separate deployment tooling.

4. **Model downloads can be slow** - First run with Ollama downloads models; use `WithDataVolume()` to persist.

5. **stdio transport limitations** - MCP servers using stdio can't fully leverage Aspire's service discovery during Claude Code invocation.

6. **Resource naming** - Names must be lowercase and DNS-compatible.

### PostgreSQL/pgvector Gotchas

1. **Extension must be created per database** - The pgvector extension needs `CREATE EXTENSION vector` in each database.

2. **Volume permissions** - Ensure proper permissions when using bind mounts for init scripts.

3. **Connection string format** - Aspire uses `Host=...;Port=...;Database=...` format, same as Npgsql.

### Ollama-Specific Considerations

1. **Model download progress** - Visible in Aspire dashboard State column; don't close AppHost during download.

2. **GPU support** - May require additional Docker configuration for GPU passthrough.

3. **Memory requirements** - mxbai-embed-large and mistral need adequate RAM; monitor in dashboard.

4. **Model naming** - Use `AddModel("name", "model:tag")` to reference specific versions.

### When to Fall Back to Docker Compose (Even in Dev)

1. **GPU workloads** - Complex GPU passthrough configuration
2. **Network simulation** - Testing network partitions or latency
3. **Volume corruption testing** - Testing data recovery scenarios
4. **Multi-machine scenarios** - When services need to run on different hosts
5. **CI/CD pipelines** - Some CI systems work better with Docker Compose directly

---

## 7. Complete AppHost Example

```csharp
// AppHost/Program.cs
using CommunityToolkit.Aspire.Hosting.Ollama;

var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with pgvector
var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector", "pg16")
    .WithDataVolume("postgres-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin();  // Optional: web-based DB admin

var vectorDb = postgres.AddDatabase("vectordb");

// Ollama with models
var ollama = builder.AddOllama("ollama")
    .WithDataVolume("ollama-models")
    .WithLifetime(ContainerLifetime.Persistent);

var embedModel = ollama.AddModel("embed", "mxbai-embed-large");
var ragModel = ollama.AddModel("rag", "mistral");

// MCP Server
var mcpServer = builder.AddProject<Projects.McpServer>("mcp-server")
    .WithReference(vectorDb)
    .WithReference(embedModel)
    .WithReference(ragModel)
    .WaitFor(postgres)
    .WaitFor(embedModel)
    .WaitFor(ragModel)
    .WithEnvironment("MCP_MODE", "development");

builder.Build().Run();
```

---

## 8. Corresponding Docker Compose (Production)

```yaml
# docker/docker-compose.yml
version: '3.8'

services:
  postgres:
    image: pgvector/pgvector:pg16
    environment:
      POSTGRES_USER: ${POSTGRES_USER:-postgres}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:?required}
      POSTGRES_DB: vectordb
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./init-scripts:/docker-entrypoint-initdb.d:ro
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s
      timeout: 5s
      retries: 5

  ollama:
    image: ollama/ollama:latest
    volumes:
      - ollama_data:/root/.ollama
    environment:
      OLLAMA_KEEP_ALIVE: "24h"
    # GPU support (uncomment if available)
    # deploy:
    #   resources:
    #     reservations:
    #       devices:
    #         - driver: nvidia
    #           count: 1
    #           capabilities: [gpu]

  mcp-server:
    build:
      context: ../src/McpServer
      dockerfile: Dockerfile
    depends_on:
      postgres:
        condition: service_healthy
      ollama:
        condition: service_started
    environment:
      ConnectionStrings__vectordb: "Host=postgres;Database=vectordb;Username=${POSTGRES_USER:-postgres};Password=${POSTGRES_PASSWORD}"
      Ollama__Endpoint: "http://ollama:11434"
      Ollama__EmbedModel: "mxbai-embed-large"
      Ollama__RagModel: "mistral"
    stdin_open: true
    tty: true

volumes:
  postgres_data:
  ollama_data:
```

---

## Sources

- [.NET Aspire Overview](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/aspire-overview)
- [Aspire Orchestration Overview](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/app-host-overview)
- [Aspire PostgreSQL Integration](https://learn.microsoft.com/en-us/dotnet/aspire/database/postgresql-integration)
- [pgvector Support Issue #3052](https://github.com/dotnet/aspire/issues/3052)
- [Aspire Ollama Integration (Community Toolkit)](https://learn.microsoft.com/en-us/dotnet/aspire/community-toolkit/ollama)
- [Using Local AI Models with Aspire](https://devblogs.microsoft.com/dotnet/local-ai-models-with-dotnet-aspire/)
- [Docker Compose Publishing](https://learn.microsoft.com/en-us/dotnet/aspire/deployment/docker-integration)
- [Using .NET Aspire With Docker Publisher](https://www.milanjovanovic.tech/blog/using-dotnet-aspire-with-the-docker-publisher)
- [Build MCP Server in C#](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/)
- [MCP Server Development with Aspire](https://nikiforovall.blog/dotnet/2025/04/04/mcp-template-and-aspire.html)
- [Aspire Service Defaults](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/service-defaults)
- [Semantic Kernel and Aspire Integration](https://devblogs.microsoft.com/semantic-kernel/build-ai-applications-with-ease-using-semantic-kernel-and-net-aspire/)
- [Aspire Roadmap 2025-2026](https://github.com/dotnet/aspire/discussions/10644)
- [Aspire FAQ](https://learn.microsoft.com/en-us/dotnet/aspire/reference/aspire-faq)
