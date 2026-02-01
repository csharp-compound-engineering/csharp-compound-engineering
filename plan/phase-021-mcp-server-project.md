# Phase 021: MCP Server Project Structure

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-5 hours
> **Category**: MCP Server Core
> **Prerequisites**: Phase 001 (Solution Structure), Phase 017 (Common Library - if exists)

---

## Spec References

This phase implements the MCP server project scaffolding defined in:

- **spec/mcp-server.md** - [Overview](../spec/mcp-server.md#overview) and [Transport](../spec/mcp-server.md#transport)
- **research/mcp-csharp-sdk-research.md** - SDK architecture, server setup, and Generic Host integration
- **structure/mcp-server.md** - MCP server summary and structural relationships

---

## Objectives

1. Create the `CompoundDocs.McpServer` project targeting .NET 10.0+
2. Add the `ModelContextProtocol` NuGet package reference (prerelease)
3. Establish the project directory structure (Services/, Tools/, Models/, Resources/)
4. Create `Program.cs` skeleton with Generic Host configuration and stdio transport
5. Configure logging to stderr (required for stdio transport)
6. Add project to the solution file

---

## Acceptance Criteria

- [ ] Project file `src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj` exists
- [ ] Project targets `net10.0` (or `net9.0` if .NET 10 not yet available)
- [ ] NuGet package references are configured:
  - [ ] `ModelContextProtocol` (prerelease)
  - [ ] `Microsoft.Extensions.Hosting`
- [ ] Directory structure established:
  - [ ] `Services/` directory with `.gitkeep`
  - [ ] `Tools/` directory with `.gitkeep`
  - [ ] `Models/` directory with `.gitkeep`
  - [ ] `Resources/` directory with `.gitkeep`
- [ ] `Program.cs` contains Generic Host skeleton with:
  - [ ] `Host.CreateApplicationBuilder()` usage
  - [ ] Logging configured to stderr
  - [ ] MCP server registration with stdio transport
  - [ ] `WithToolsFromAssembly()` configuration
- [ ] Project added to solution file
- [ ] Project builds successfully with `dotnet build`

---

## Implementation Notes

### Project File Creation

Create `src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>CompoundDocs.McpServer</RootNamespace>
    <AssemblyName>CompoundDocs.McpServer</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <!-- MCP SDK (prerelease) -->
    <PackageReference Include="ModelContextProtocol" />

    <!-- Generic Host for dependency injection and lifecycle management -->
    <PackageReference Include="Microsoft.Extensions.Hosting" />
  </ItemGroup>

  <ItemGroup>
    <!-- Reference to shared library (when available) -->
    <!-- <ProjectReference Include="..\CompoundDocs.Common\CompoundDocs.Common.csproj" /> -->
  </ItemGroup>

</Project>
```

**Note**: If .NET 10.0 SDK is not yet released, use `net9.0` and update later. The spec recommends .NET 10.0+ but .NET 9.0 is acceptable for initial development.

### Package Version Management

Add to `Directory.Packages.props` at repository root:

```xml
<ItemGroup>
  <!-- MCP SDK -->
  <PackageVersion Include="ModelContextProtocol" Version="0.2.0-preview.*" />

  <!-- .NET Extensions -->
  <PackageVersion Include="Microsoft.Extensions.Hosting" Version="9.0.*" />
</ItemGroup>
```

**Note**: Use wildcard versions for prerelease packages; pin to specific versions once stable.

### Directory Structure

Create the following structure under `src/CompoundDocs.McpServer/`:

```
src/CompoundDocs.McpServer/
├── CompoundDocs.McpServer.csproj
├── Program.cs
├── Services/
│   └── .gitkeep
├── Tools/
│   └── .gitkeep
├── Models/
│   └── .gitkeep
└── Resources/
    └── .gitkeep
```

**Directory Purposes**:
- `Services/` - Business logic services (IEmbeddingService, IDocumentRepository, etc.)
- `Tools/` - MCP tool implementations (9 tools as per spec)
- `Models/` - Domain models (CompoundDocument, TenantContext, SearchResult, etc.)
- `Resources/` - MCP resource implementations (if any)

### Program.cs Skeleton

Create `src/CompoundDocs.McpServer/Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer;

/// <summary>
/// Entry point for the CompoundDocs MCP Server.
///
/// This server provides RAG and semantic search capabilities for compounding documentation.
/// It communicates via stdio transport and is designed to be launched per Claude Code instance.
/// </summary>
public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Configure logging to stderr (required for stdio transport)
        // stdout is reserved for MCP protocol messages
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole(consoleLogOptions =>
        {
            consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
        });

        // Set minimum log level from configuration or default to Information
        builder.Logging.SetMinimumLevel(
            builder.Configuration.GetValue("Logging:LogLevel:Default", LogLevel.Information));

        // Register application services
        ConfigureServices(builder.Services);

        // Configure MCP server with stdio transport
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        // Configure MCP server options
        builder.Services.AddOptions<McpServerOptions>()
            .Configure(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "CompoundDocs",
                    Version = GetVersion()
                };
                options.ServerInstructions =
                    "This server provides RAG and semantic search for compounding documentation. " +
                    "Use rag_query for answering questions and semantic_search for finding relevant documents.";
            });

        var host = builder.Build();
        await host.RunAsync();
    }

    /// <summary>
    /// Registers application-specific services with the dependency injection container.
    /// </summary>
    private static void ConfigureServices(IServiceCollection services)
    {
        // TODO: Register services in subsequent phases
        // Phase 022+: Embedding service, document repository, file watcher, etc.

        // Example registrations (uncomment when implementing):
        // services.AddSingleton<IEmbeddingService, SemanticKernelEmbeddingService>();
        // services.AddSingleton<IDocumentRepository, PostgresDocumentRepository>();
        // services.AddHostedService<FileWatcherService>();
    }

    /// <summary>
    /// Gets the assembly version for server identification.
    /// </summary>
    private static string GetVersion()
    {
        var assembly = typeof(Program).Assembly;
        var version = assembly.GetName().Version;
        return version?.ToString(3) ?? "0.0.1";
    }
}
```

### Add Project to Solution

Use the .NET CLI to add the project to the solution:

```bash
dotnet sln csharp-compounding-docs.sln add src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj
```

---

## Dependencies

### Depends On
- **Phase 001**: Solution & Project Structure (solution file must exist)
- **Phase 017**: CompoundDocs.Common Library (for shared types - optional, can be added later)

### Blocks
- **Phase 022+**: MCP tool implementations (requires project structure)
- **Phase 023+**: Service implementations (requires project structure)
- **Phase 024+**: File watcher integration (requires project structure)
- All MCP server feature phases

---

## Verification Steps

After completing this phase, verify:

1. **Project builds**:
   ```bash
   dotnet build src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj
   ```

2. **Solution includes project**:
   ```bash
   dotnet sln list
   # Should show CompoundDocs.McpServer
   ```

3. **Directory structure**:
   ```bash
   ls -la src/CompoundDocs.McpServer/
   # Should show Services/, Tools/, Models/, Resources/ directories
   ```

4. **Server starts** (basic smoke test):
   ```bash
   dotnet run --project src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj
   # Should start without errors (will wait for MCP input on stdin)
   # Press Ctrl+C to stop
   ```

5. **Logging to stderr**: When server starts, logs should appear on stderr, not stdout

---

## Key Technical Decisions

### .NET Version

- **Target**: .NET 10.0+ as per spec
- **Fallback**: .NET 9.0 is acceptable if .NET 10 SDK not available
- **Rationale**: Aligns with spec requirement; .NET 9.0 provides all necessary features

### MCP SDK Package

- **Package**: `ModelContextProtocol` (main package)
- **Status**: Prerelease (as of January 2026)
- **Transport**: Stdio (launched per Claude Code instance)

### Logging Configuration

- **stderr for all logs**: Required because stdout is used for MCP JSON-RPC messages
- **Trace threshold**: Allows all log levels to stderr
- **Configurable**: Log level can be set via configuration/environment

### Directory Organization

| Directory | Purpose | Phase |
|-----------|---------|-------|
| `Services/` | Business logic abstractions and implementations | Future phases |
| `Tools/` | MCP tool classes with `[McpServerToolType]` attribute | Future phases |
| `Models/` | Domain models (CompoundDocument, TenantContext, etc.) | Future phases |
| `Resources/` | MCP resource classes (if needed) | Future phases |

---

## Notes

- The server will not have functional tools until subsequent phases implement them
- The `WithToolsFromAssembly()` call will discover tools via reflection when they are added
- ProjectReference to CompoundDocs.Common is commented out until that project exists
- Consider adding health check capability in future phases (noted as open question in spec)
- The server is designed to be ephemeral - launched per Claude Code instance with stdio transport
