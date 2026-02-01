# Phase 037: Environment Variable Overrides

> **Status**: NOT_STARTED
> **Effort Estimate**: 2-3 hours
> **Category**: MCP Server Core
> **Prerequisites**: Phase 036 (Configuration Validation)

---

## Spec References

This phase implements the environment variable override system defined in:

- **spec/configuration.md** - Environment Variables section for CI/CD overrides
- **research/environment-variables-iconfiguration.md** - Comprehensive IConfiguration integration patterns

---

## Overview

This phase implements environment variable support for configuration overrides, enabling CI/CD pipelines and advanced users to customize infrastructure settings without modifying configuration files. The implementation follows .NET Generic Host conventions using the `CDOCS_` prefix and double-underscore hierarchical key mapping.

---

## Objectives

1. Establish the `CDOCS_` prefix convention for all environment variables
2. Integrate environment variable configuration provider with proper precedence
3. Support hierarchical configuration keys using double-underscore notation
4. Document all available environment variables with defaults
5. Provide validation for environment variable values
6. Enable CI/CD scenarios with infrastructure connection overrides

---

## Acceptance Criteria

### Environment Variable Naming Convention

- [ ] All application environment variables use the `CDOCS_` prefix
- [ ] Prefix is stripped when loading into IConfiguration
- [ ] Hierarchical keys use double-underscore (`__`) delimiter for cross-platform compatibility
- [ ] Names use PascalCase after prefix (e.g., `CDOCS_Postgres__Host`)

### IConfiguration Integration

- [ ] Environment variable provider registered after JSON configuration providers
- [ ] Environment variables override all file-based configuration
- [ ] Command-line arguments take precedence over environment variables
- [ ] Configuration binding to options classes works seamlessly

### Supported Environment Variables

- [ ] `CDOCS_Postgres__Host` - PostgreSQL server hostname
- [ ] `CDOCS_Postgres__Port` - PostgreSQL server port
- [ ] `CDOCS_Postgres__Database` - PostgreSQL database name
- [ ] `CDOCS_Postgres__Username` - PostgreSQL username
- [ ] `CDOCS_Postgres__Password` - PostgreSQL password
- [ ] `CDOCS_Ollama__Host` - Ollama server hostname
- [ ] `CDOCS_Ollama__Port` - Ollama server port
- [ ] `CDOCS_Home` - Override global configuration directory path
- [ ] `CDOCS_LogLevel` - Override default log level

### CI/CD Scenario Support

- [ ] Docker Compose can pass environment variables to MCP server
- [ ] GitHub Actions workflow can configure infrastructure connections
- [ ] Connection strings can be composed from individual env vars
- [ ] Sensitive values (passwords) work via environment variables

### Override Precedence Rules

- [ ] Document and enforce: CLI args > Env vars > User config > Default
- [ ] Precedence rules apply consistently across all configuration values
- [ ] Tool parameters still override all configuration (as per spec)

### Documentation

- [ ] All environment variables documented with descriptions and defaults
- [ ] Example CI/CD configurations provided
- [ ] Docker Compose override examples included

---

## Implementation Notes

### Configuration Provider Registration

Register environment variables with the `CDOCS_` prefix in the host builder:

```csharp
// Program.cs
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Clear default configuration and rebuild with explicit ordering
builder.Configuration.Sources.Clear();

builder.Configuration
    // 1. Base configuration (lowest priority)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    // 2. Environment-specific configuration
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    // 3. Global user configuration
    .AddJsonFile(GetGlobalConfigPath(), optional: true)
    // 4. Environment variables with CDOCS_ prefix (high priority)
    .AddEnvironmentVariables(prefix: "CDOCS_")
    // 5. Command-line arguments (highest priority)
    .AddCommandLine(args);

var host = builder.Build();
await host.RunAsync();
```

### Environment Variable Mapping

Map environment variables to configuration sections using double-underscore hierarchy:

| Environment Variable | Configuration Key | Options Class Property |
|---------------------|-------------------|----------------------|
| `CDOCS_Postgres__Host` | `Postgres:Host` | `PostgresOptions.Host` |
| `CDOCS_Postgres__Port` | `Postgres:Port` | `PostgresOptions.Port` |
| `CDOCS_Postgres__Database` | `Postgres:Database` | `PostgresOptions.Database` |
| `CDOCS_Postgres__Username` | `Postgres:Username` | `PostgresOptions.Username` |
| `CDOCS_Postgres__Password` | `Postgres:Password` | `PostgresOptions.Password` |
| `CDOCS_Ollama__Host` | `Ollama:Host` | `OllamaOptions.Host` |
| `CDOCS_Ollama__Port` | `Ollama:Port` | `OllamaOptions.Port` |
| `CDOCS_Home` | `Home` | `CompoundDocsOptions.Home` |
| `CDOCS_LogLevel` | `LogLevel` | N/A (Logging config) |

### Options Classes with Environment Variable Support

Create options classes that align with environment variable structure:

```csharp
namespace CompoundDocs.McpServer.Options;

/// <summary>
/// PostgreSQL connection options.
/// Can be overridden via CDOCS_Postgres__* environment variables.
/// </summary>
public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    /// <summary>
    /// PostgreSQL server hostname.
    /// Default: from Docker Compose (typically "localhost" for host networking)
    /// Env: CDOCS_Postgres__Host
    /// </summary>
    [Required]
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// PostgreSQL server port.
    /// Default: 5433 (non-standard to avoid conflicts)
    /// Env: CDOCS_Postgres__Port
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 5433;

    /// <summary>
    /// PostgreSQL database name.
    /// Default: "compounddocs"
    /// Env: CDOCS_Postgres__Database
    /// </summary>
    [Required]
    public string Database { get; set; } = "compounddocs";

    /// <summary>
    /// PostgreSQL username.
    /// Default: "postgres"
    /// Env: CDOCS_Postgres__Username
    /// </summary>
    [Required]
    public string Username { get; set; } = "postgres";

    /// <summary>
    /// PostgreSQL password.
    /// Default: "postgres" (development only!)
    /// Env: CDOCS_Postgres__Password
    /// </summary>
    [Required]
    public string Password { get; set; } = "postgres";

    /// <summary>
    /// Builds a connection string from individual properties.
    /// </summary>
    public string BuildConnectionString() =>
        $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password}";
}

/// <summary>
/// Ollama service connection options.
/// Can be overridden via CDOCS_Ollama__* environment variables.
/// </summary>
public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    /// <summary>
    /// Ollama server hostname.
    /// Default: from Docker Compose (typically "localhost" for host networking)
    /// Env: CDOCS_Ollama__Host
    /// </summary>
    [Required]
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Ollama server port.
    /// Default: 11435 (non-standard to avoid conflicts)
    /// Env: CDOCS_Ollama__Port
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; set; } = 11435;

    /// <summary>
    /// Builds the Ollama endpoint URL.
    /// </summary>
    public string BuildEndpointUrl() => $"http://{Host}:{Port}";
}

/// <summary>
/// Global compounding docs options.
/// </summary>
public sealed class CompoundDocsOptions
{
    public const string SectionName = "CompoundDocs";

    /// <summary>
    /// Override path for global configuration directory.
    /// Default: ~/.claude/.csharp-compounding-docs/
    /// Env: CDOCS_Home
    /// </summary>
    public string? Home { get; set; }

    /// <summary>
    /// Gets the effective home directory path.
    /// </summary>
    public string GetEffectiveHomePath()
    {
        if (!string.IsNullOrEmpty(Home))
        {
            return Environment.ExpandEnvironmentVariables(Home);
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".claude", ".csharp-compounding-docs");
    }
}
```

### Service Registration with Environment Variable Support

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public static class EnvironmentConfigurationExtensions
{
    /// <summary>
    /// Adds environment variable configuration with CDOCS_ prefix.
    /// </summary>
    public static IConfigurationBuilder AddCompoundDocsEnvironmentVariables(
        this IConfigurationBuilder builder)
    {
        return builder.AddEnvironmentVariables(prefix: "CDOCS_");
    }

    /// <summary>
    /// Registers options classes that support environment variable overrides.
    /// </summary>
    public static IServiceCollection AddCompoundDocsInfrastructureOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptionsWithValidateOnStart<PostgresOptions>()
            .Bind(configuration.GetSection(PostgresOptions.SectionName))
            .ValidateDataAnnotations();

        services.AddOptionsWithValidateOnStart<OllamaOptions>()
            .Bind(configuration.GetSection(OllamaOptions.SectionName))
            .ValidateDataAnnotations();

        services.AddOptionsWithValidateOnStart<CompoundDocsOptions>()
            .Bind(configuration.GetSection(CompoundDocsOptions.SectionName))
            .ValidateDataAnnotations();

        return services;
    }
}
```

### Connection String Builder Service

Create a service that constructs connection strings from options:

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Builds connection strings from environment-overridable options.
/// </summary>
public interface IConnectionStringBuilder
{
    /// <summary>
    /// Gets the PostgreSQL connection string.
    /// </summary>
    string GetPostgresConnectionString();

    /// <summary>
    /// Gets the Ollama endpoint URL.
    /// </summary>
    string GetOllamaEndpoint();
}

public sealed class ConnectionStringBuilder : IConnectionStringBuilder
{
    private readonly PostgresOptions _postgresOptions;
    private readonly OllamaOptions _ollamaOptions;

    public ConnectionStringBuilder(
        IOptions<PostgresOptions> postgresOptions,
        IOptions<OllamaOptions> ollamaOptions)
    {
        _postgresOptions = postgresOptions.Value;
        _ollamaOptions = ollamaOptions.Value;
    }

    public string GetPostgresConnectionString() =>
        _postgresOptions.BuildConnectionString();

    public string GetOllamaEndpoint() =>
        _ollamaOptions.BuildEndpointUrl();
}
```

### CI/CD Configuration Examples

#### GitHub Actions Example

```yaml
# .github/workflows/integration-tests.yml
name: Integration Tests

jobs:
  test:
    runs-on: ubuntu-latest

    services:
      postgres:
        image: pgvector/pgvector:pg16
        env:
          POSTGRES_PASSWORD: test_password
          POSTGRES_DB: compounddocs_test
        ports:
          - 5432:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      - uses: actions/checkout@v4

      - name: Run Integration Tests
        env:
          CDOCS_Postgres__Host: localhost
          CDOCS_Postgres__Port: 5432
          CDOCS_Postgres__Database: compounddocs_test
          CDOCS_Postgres__Username: postgres
          CDOCS_Postgres__Password: test_password
          CDOCS_Ollama__Host: localhost
          CDOCS_Ollama__Port: 11434
        run: dotnet test --filter "Category=Integration"
```

#### Docker Compose Override Example

```yaml
# docker-compose.override.yml
version: '3.8'

services:
  mcp-server:
    environment:
      - CDOCS_Postgres__Host=postgres
      - CDOCS_Postgres__Port=5432
      - CDOCS_Postgres__Database=compounddocs
      - CDOCS_Postgres__Username=postgres
      - CDOCS_Postgres__Password=${POSTGRES_PASSWORD}
      - CDOCS_Ollama__Host=ollama
      - CDOCS_Ollama__Port=11434
      - CDOCS_LogLevel=Debug
```

#### Kubernetes ConfigMap Example

```yaml
# k8s/configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: compounddocs-config
data:
  CDOCS_Postgres__Host: "postgres-service"
  CDOCS_Postgres__Port: "5432"
  CDOCS_Postgres__Database: "compounddocs"
  CDOCS_Ollama__Host: "ollama-service"
  CDOCS_Ollama__Port: "11434"
---
apiVersion: v1
kind: Secret
metadata:
  name: compounddocs-secrets
type: Opaque
stringData:
  CDOCS_Postgres__Username: "app_user"
  CDOCS_Postgres__Password: "secure_password_here"
```

### Logging Configuration via Environment Variables

```csharp
// Support CDOCS_Logging__LogLevel__Default for log level override
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

// Example environment variable:
// CDOCS_Logging__LogLevel__Default=Debug
// CDOCS_Logging__LogLevel__Microsoft=Warning
```

---

## Configuration Precedence Documentation

Document the full precedence chain (from lowest to highest priority):

1. **Built-in defaults** - Hardcoded in options classes
2. **appsettings.json** - Base application configuration
3. **appsettings.{Environment}.json** - Environment-specific overrides
4. **Global user config** - `~/.claude/.csharp-compounding-docs/settings.json`
5. **Environment variables** - `CDOCS_*` prefixed variables
6. **Command-line arguments** - Passed to application
7. **Tool parameters** - MCP tool call arguments (highest priority)

```csharp
/// <summary>
/// Documents the configuration precedence for a given setting.
/// </summary>
public static class ConfigurationPrecedence
{
    /// <summary>
    /// Gets the effective value following precedence rules.
    /// </summary>
    /// <typeparam name="T">Value type</typeparam>
    /// <param name="toolParameter">Value from MCP tool parameter (highest priority)</param>
    /// <param name="configuration">Value from IConfiguration (env vars, files)</param>
    /// <param name="defaultValue">Built-in default (lowest priority)</param>
    public static T GetEffectiveValue<T>(T? toolParameter, T? configuration, T defaultValue)
        where T : struct
    {
        // Tool parameters always win
        if (toolParameter.HasValue)
            return toolParameter.Value;

        // Configuration (includes env vars) next
        if (configuration.HasValue)
            return configuration.Value;

        // Fall back to default
        return defaultValue;
    }
}
```

---

## Dependencies

### Depends On

- Phase 036: Configuration Validation (validation infrastructure)
- Phase 017: Dependency Injection Container Setup (service registration patterns)
- Phase 008: Global Configuration System (home directory structure)

### Blocks

- Phase 038+: Any phase requiring configurable infrastructure connections
- CI/CD pipeline configurations
- Docker-based deployment scenarios

---

## Verification Steps

After completing this phase, verify:

1. **Environment variable loading**: Variables with `CDOCS_` prefix are loaded
2. **Prefix stripping**: Prefix is removed in IConfiguration keys
3. **Hierarchical mapping**: Double-underscore maps to nested configuration
4. **Precedence enforcement**: Env vars override JSON, CLI overrides env vars
5. **Options binding**: Options classes receive environment values correctly
6. **Connection strings**: Composed correctly from individual env vars

### Manual Verification

```bash
# Set environment variables
export CDOCS_Postgres__Host=test-host
export CDOCS_Postgres__Port=5555
export CDOCS_Ollama__Host=ollama-test

# Run and verify configuration is picked up
dotnet run --project src/CompoundDocs.McpServer/

# Should log: "Connecting to PostgreSQL at test-host:5555"
# Should log: "Using Ollama at http://ollama-test:11435"
```

### Unit Test Verification

```csharp
[Fact]
public void EnvironmentVariables_OverrideJsonConfiguration()
{
    // Arrange
    Environment.SetEnvironmentVariable("CDOCS_Postgres__Host", "env-host");

    var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")  // Contains Host=json-host
        .AddEnvironmentVariables(prefix: "CDOCS_")
        .Build();

    // Act
    var host = config["Postgres:Host"];

    // Assert
    Assert.Equal("env-host", host);

    // Cleanup
    Environment.SetEnvironmentVariable("CDOCS_Postgres__Host", null);
}

[Fact]
public void PostgresOptions_BindsFromEnvironmentVariables()
{
    // Arrange
    Environment.SetEnvironmentVariable("CDOCS_Postgres__Host", "ci-host");
    Environment.SetEnvironmentVariable("CDOCS_Postgres__Port", "5432");

    var config = new ConfigurationBuilder()
        .AddEnvironmentVariables(prefix: "CDOCS_")
        .Build();

    var services = new ServiceCollection();
    services.AddOptionsWithValidateOnStart<PostgresOptions>()
        .Bind(config.GetSection("Postgres"));

    using var provider = services.BuildServiceProvider();
    var options = provider.GetRequiredService<IOptions<PostgresOptions>>();

    // Assert
    Assert.Equal("ci-host", options.Value.Host);
    Assert.Equal(5432, options.Value.Port);

    // Cleanup
    Environment.SetEnvironmentVariable("CDOCS_Postgres__Host", null);
    Environment.SetEnvironmentVariable("CDOCS_Postgres__Port", null);
}

[Fact]
public void ConnectionStringBuilder_UsesOptionsFromEnvironment()
{
    // Arrange
    var postgresOptions = Options.Create(new PostgresOptions
    {
        Host = "env-host",
        Port = 5432,
        Database = "testdb",
        Username = "testuser",
        Password = "testpass"
    });
    var ollamaOptions = Options.Create(new OllamaOptions
    {
        Host = "ollama-host",
        Port = 11434
    });

    var builder = new ConnectionStringBuilder(postgresOptions, ollamaOptions);

    // Act
    var connString = builder.GetPostgresConnectionString();
    var ollamaUrl = builder.GetOllamaEndpoint();

    // Assert
    Assert.Contains("Host=env-host", connString);
    Assert.Contains("Port=5432", connString);
    Assert.Equal("http://ollama-host:11434", ollamaUrl);
}
```

---

## Testing Requirements

### Unit Tests

1. **EnvironmentVariableConfigurationTests**
   - Environment variables with CDOCS_ prefix are loaded
   - Prefix is stripped from configuration keys
   - Hierarchical keys work with double-underscore
   - Environment variables override JSON configuration
   - Case-insensitive key access works

2. **PostgresOptionsTests**
   - Default values are applied when no env vars set
   - Environment variables override defaults
   - BuildConnectionString() produces valid connection string
   - Validation attributes are enforced

3. **OllamaOptionsTests**
   - Default values are applied when no env vars set
   - BuildEndpointUrl() produces valid URL
   - Port range validation works

4. **ConnectionStringBuilderTests**
   - Constructs valid PostgreSQL connection strings
   - Constructs valid Ollama endpoint URLs
   - Handles special characters in passwords

5. **ConfigurationPrecedenceTests**
   - Environment variables override file configuration
   - Command-line arguments override environment variables
   - Tool parameters override all configuration

### Integration Tests

1. **End-to-end configuration loading**: Full pipeline from env vars to options
2. **Docker Compose environment injection**: Variables passed correctly
3. **Connection validation**: Built connection strings work against services

---

## Documentation Deliverables

### Environment Variables Reference

Create documentation file listing all supported environment variables:

```markdown
# Environment Variables Reference

## Infrastructure Connection Variables

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `CDOCS_Postgres__Host` | PostgreSQL server hostname | `localhost` | `db.example.com` |
| `CDOCS_Postgres__Port` | PostgreSQL server port | `5433` | `5432` |
| `CDOCS_Postgres__Database` | Database name | `compounddocs` | `myapp_docs` |
| `CDOCS_Postgres__Username` | Database username | `postgres` | `app_user` |
| `CDOCS_Postgres__Password` | Database password | `postgres` | (use secrets) |
| `CDOCS_Ollama__Host` | Ollama server hostname | `localhost` | `ollama.local` |
| `CDOCS_Ollama__Port` | Ollama server port | `11435` | `11434` |

## Application Configuration Variables

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `CDOCS_Home` | Global config directory | `~/.claude/.csharp-compounding-docs/` | `/etc/compounddocs` |
| `CDOCS_Logging__LogLevel__Default` | Default log level | `Information` | `Debug` |

## Security Notes

- Never commit environment variable values to source control
- Use secret management for `CDOCS_Postgres__Password`
- In production, use orchestrator secrets (Kubernetes, Docker Swarm)
```

---

## Notes

- The `CDOCS_` prefix ensures no collision with other applications' environment variables
- Double-underscore (`__`) is required for cross-platform compatibility (colon doesn't work on Linux)
- Environment variables are ideal for CI/CD because they don't require file modifications
- Sensitive values like passwords should use proper secret management in production
- The IConfiguration system automatically handles type conversion for bound options
