# Phase 024: Command Line Argument Parsing

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-4 hours
> **Category**: MCP Server Core
> **Prerequisites**: Phase 021 (MCP Server Project Skeleton)

---

## Spec References

This phase implements the command-line argument parsing defined in:

- **spec/mcp-server/ollama-integration.md** - [Connection](../spec/mcp-server/ollama-integration.md#connection) - Ollama host:port passed via CLI
- **spec/infrastructure.md** - [Launch Script](../spec/infrastructure.md#launch-script-scriptslaunch-mcp-serverps1) - Connection parameters passed to MCP server

---

## Objectives

1. Implement command-line argument parsing for all connection parameters
2. Support PostgreSQL connection string construction from individual arguments
3. Support Ollama host:port configuration
4. Provide argument validation with helpful error messages
5. Generate help text for command-line usage
6. Integrate parsed arguments with `IConfiguration` for dependency injection

---

## Acceptance Criteria

### Command Line Arguments

- [ ] `--postgres-host` argument is parsed (default: `127.0.0.1`)
- [ ] `--postgres-port` argument is parsed (default: `5433`)
- [ ] `--postgres-database` argument is parsed (default: `compounding_docs`)
- [ ] `--postgres-user` argument is parsed (default: `compounding`)
- [ ] `--postgres-password` argument is parsed (default: `compounding`)
- [ ] `--ollama-host` argument is parsed (default: `127.0.0.1`)
- [ ] `--ollama-port` argument is parsed (default: `11435`)
- [ ] `--help` or `-h` displays help text and exits
- [ ] `--version` displays version and exits

### Argument Validation

- [ ] Port numbers are validated as integers in valid range (1-65535)
- [ ] Required arguments that are missing produce clear error messages
- [ ] Invalid argument values produce specific error messages with expected format
- [ ] Validation errors are written to stderr (stdout reserved for MCP protocol)

### Help Text Generation

- [ ] Help text includes all arguments with descriptions
- [ ] Help text shows default values for each argument
- [ ] Help text includes usage examples
- [ ] Help text formatted for terminal readability

### IConfiguration Integration

- [ ] Parsed arguments are available via `IConfiguration`
- [ ] Configuration section `PostgreSQL` contains connection settings
- [ ] Configuration section `Ollama` contains host/port settings
- [ ] Connection string can be built from configuration values
- [ ] Services can inject `IOptions<PostgresOptions>` and `IOptions<OllamaOptions>`

---

## Implementation Notes

### Argument Specification

The launcher script passes arguments to the MCP server (from `spec/infrastructure.md`):

```powershell
& $mcpServerPath `
    --postgres-host $infraConfig.postgres.host `
    --postgres-port $infraConfig.postgres.port `
    --postgres-database $infraConfig.postgres.database `
    --postgres-user $infraConfig.postgres.username `
    --postgres-password $infraConfig.postgres.password `
    --ollama-host $infraConfig.ollama.host `
    --ollama-port $infraConfig.ollama.port
```

### Recommended Library

Use `System.CommandLine` for argument parsing (modern, feature-rich, Microsoft-maintained):

```xml
<PackageReference Include="System.CommandLine" Version="2.*" />
```

### Command Configuration

Configure the root command with all options:

```csharp
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

var rootCommand = new RootCommand("CSharp Compound Docs MCP Server")
{
    // PostgreSQL options
    new Option<string>(
        aliases: ["--postgres-host"],
        getDefaultValue: () => "127.0.0.1",
        description: "PostgreSQL server hostname"),

    new Option<int>(
        aliases: ["--postgres-port"],
        getDefaultValue: () => 5433,
        description: "PostgreSQL server port"),

    new Option<string>(
        aliases: ["--postgres-database"],
        getDefaultValue: () => "compounding_docs",
        description: "PostgreSQL database name"),

    new Option<string>(
        aliases: ["--postgres-user"],
        getDefaultValue: () => "compounding",
        description: "PostgreSQL username"),

    new Option<string>(
        aliases: ["--postgres-password"],
        getDefaultValue: () => "compounding",
        description: "PostgreSQL password"),

    // Ollama options
    new Option<string>(
        aliases: ["--ollama-host"],
        getDefaultValue: () => "127.0.0.1",
        description: "Ollama server hostname"),

    new Option<int>(
        aliases: ["--ollama-port"],
        getDefaultValue: () => 11435,
        description: "Ollama server port")
};
```

### Port Validation

Add validators for port arguments:

```csharp
var portOption = new Option<int>("--postgres-port");
portOption.AddValidator(result =>
{
    var port = result.GetValueOrDefault<int>();
    if (port < 1 || port > 65535)
    {
        result.ErrorMessage = $"Port must be between 1 and 65535, got: {port}";
    }
});
```

### Options Classes

Create strongly-typed options for dependency injection:

```csharp
namespace CSharpCompoundDocs.McpServer.Configuration;

/// <summary>
/// PostgreSQL connection options parsed from command-line arguments.
/// </summary>
public class PostgresOptions
{
    public const string SectionName = "PostgreSQL";

    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5433;
    public string Database { get; set; } = "compounding_docs";
    public string Username { get; set; } = "compounding";
    public string Password { get; set; } = "compounding";

    /// <summary>
    /// Builds an Npgsql connection string from the options.
    /// </summary>
    public string GetConnectionString() =>
        $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password}";
}

/// <summary>
/// Ollama connection options parsed from command-line arguments.
/// </summary>
public class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 11435;

    /// <summary>
    /// Gets the full Ollama API endpoint URL.
    /// </summary>
    public Uri GetEndpoint() => new($"http://{Host}:{Port}");
}
```

### Integration with IConfiguration

Bridge command-line arguments to `IConfiguration` for the host builder:

```csharp
using Microsoft.Extensions.Configuration;
using System.CommandLine.Parsing;

public static class CommandLineConfigurationExtensions
{
    /// <summary>
    /// Adds command-line parsed values to configuration.
    /// </summary>
    public static IConfigurationBuilder AddCommandLineParsed(
        this IConfigurationBuilder builder,
        ParseResult parseResult)
    {
        var settings = new Dictionary<string, string?>
        {
            // PostgreSQL section
            [$"{PostgresOptions.SectionName}:Host"] =
                parseResult.GetValueForOption(postgresHostOption),
            [$"{PostgresOptions.SectionName}:Port"] =
                parseResult.GetValueForOption(postgresPortOption)?.ToString(),
            [$"{PostgresOptions.SectionName}:Database"] =
                parseResult.GetValueForOption(postgresDatabaseOption),
            [$"{PostgresOptions.SectionName}:Username"] =
                parseResult.GetValueForOption(postgresUserOption),
            [$"{PostgresOptions.SectionName}:Password"] =
                parseResult.GetValueForOption(postgresPasswordOption),

            // Ollama section
            [$"{OllamaOptions.SectionName}:Host"] =
                parseResult.GetValueForOption(ollamaHostOption),
            [$"{OllamaOptions.SectionName}:Port"] =
                parseResult.GetValueForOption(ollamaPortOption)?.ToString()
        };

        return builder.AddInMemoryCollection(settings);
    }
}
```

### Host Builder Integration

Wire up in `Program.cs`:

```csharp
using System.CommandLine;
using System.CommandLine.Parsing;

// Parse command line
var parser = new CommandLineBuilder(rootCommand)
    .UseDefaults()
    .Build();

var parseResult = parser.Parse(args);

// Check for parse errors or help/version
if (parseResult.Errors.Count > 0)
{
    foreach (var error in parseResult.Errors)
    {
        Console.Error.WriteLine(error.Message);
    }
    return 1;
}

// Build host with parsed configuration
var builder = Host.CreateApplicationBuilder();
builder.Configuration.AddCommandLineParsed(parseResult);

// Register options
builder.Services.Configure<PostgresOptions>(
    builder.Configuration.GetSection(PostgresOptions.SectionName));
builder.Services.Configure<OllamaOptions>(
    builder.Configuration.GetSection(OllamaOptions.SectionName));

var host = builder.Build();
await host.RunAsync();
```

### Help Text Output

Help text should follow this format:

```
CSharp Compound Docs MCP Server

Usage:
  CompoundDocs.McpServer [options]

Options:
  --postgres-host <host>        PostgreSQL server hostname [default: 127.0.0.1]
  --postgres-port <port>        PostgreSQL server port [default: 5433]
  --postgres-database <name>    PostgreSQL database name [default: compounding_docs]
  --postgres-user <username>    PostgreSQL username [default: compounding]
  --postgres-password <pass>    PostgreSQL password [default: compounding]
  --ollama-host <host>          Ollama server hostname [default: 127.0.0.1]
  --ollama-port <port>          Ollama server port [default: 11435]
  --version                     Show version information
  -?, -h, --help                Show help and usage information

Examples:
  # Use defaults (localhost infrastructure)
  CompoundDocs.McpServer

  # Custom PostgreSQL host
  CompoundDocs.McpServer --postgres-host 192.168.1.100 --postgres-port 5432

  # Custom Ollama endpoint
  CompoundDocs.McpServer --ollama-host ollama.local --ollama-port 11434
```

### Error Output

All errors must go to stderr (stdout reserved for MCP protocol):

```csharp
// System.CommandLine writes errors to stderr by default
// Verify this is configured correctly

var parser = new CommandLineBuilder(rootCommand)
    .UseDefaults()
    .UseExceptionHandler((ex, context) =>
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        context.ExitCode = 1;
    })
    .Build();
```

---

## Dependencies

### Depends On

- Phase 021: MCP Server Project Skeleton (requires project structure and Program.cs)

### Blocks

- Phase 025: PostgreSQL Connection Setup (requires connection string from CLI args)
- Phase 026: Ollama Service Integration (requires Ollama endpoint from CLI args)
- All MCP tool implementations (require database and Ollama connections)

---

## Verification Steps

After completing this phase, verify:

1. **Help text**: Run with `--help` and verify all options are documented

```bash
dotnet run --project src/CompoundDocs.McpServer -- --help
```

2. **Default values**: Run with no arguments and verify defaults are used

```bash
dotnet run --project src/CompoundDocs.McpServer 2>&1 | grep -i "config"
```

3. **Custom values**: Run with custom arguments and verify they override defaults

```bash
dotnet run --project src/CompoundDocs.McpServer -- \
    --postgres-host custom.host \
    --postgres-port 5432 \
    --ollama-port 11434
```

4. **Validation errors**: Run with invalid port and verify error message

```bash
dotnet run --project src/CompoundDocs.McpServer -- --postgres-port 99999 2>&1
# Should show: "Port must be between 1 and 65535, got: 99999"
```

5. **IConfiguration integration**: Verify services can inject options

```csharp
// In a test or startup logging
var pgOptions = services.GetRequiredService<IOptions<PostgresOptions>>().Value;
logger.LogInformation("PostgreSQL: {Host}:{Port}", pgOptions.Host, pgOptions.Port);
```

6. **stderr output**: Verify errors go to stderr, not stdout

```bash
dotnet run --project src/CompoundDocs.McpServer -- --invalid-arg 2>/dev/null
# Should show nothing (stdout is empty)

dotnet run --project src/CompoundDocs.McpServer -- --invalid-arg 2>&1
# Should show error message
```

---

## Notes

- `System.CommandLine` is used instead of simpler alternatives because it provides built-in help generation, validation, and proper exit code handling
- The defaults match the infrastructure spec's Docker Compose exposed ports (5433 for PostgreSQL, 11435 for Ollama)
- Connection string is built on-demand from individual components to allow for future flexibility (e.g., adding SSL options)
- Password is passed via command line in MVP; consider environment variable support in future phases for production security
- All output during argument parsing must go to stderr to avoid corrupting the MCP stdio channel
