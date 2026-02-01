# Phase 008: Global Configuration Structure

> **Category**: Infrastructure Setup
> **Prerequisites**: Phase 001 (Project Scaffolding)
> **Estimated Effort**: Small (2-4 hours)

---

## Objective

Establish the global configuration directory structure at `~/.claude/.csharp-compounding-docs/` with properly validated configuration files that persist infrastructure settings across all projects.

---

## Spec References

- [spec/configuration.md](../spec/configuration.md) - Global Configuration section
- [structure/configuration.md](../structure/configuration.md) - Configuration structure summary

---

## Deliverables

### 1. Global Configuration Directory Structure

Create the infrastructure for managing the global config directory:

```
~/.claude/.csharp-compounding-docs/
├── ollama-config.json      # Ollama model and GPU settings
├── settings.json           # MCP server settings
└── docker-compose.yml      # Generated from template (Phase 003)
```

### 2. Configuration Models

#### 2.1 OllamaConfig Model

Create a strongly-typed model for `ollama-config.json`:

```csharp
namespace CSharpCompoundingDocs.Configuration;

public sealed record OllamaConfig
{
    public string GenerationModel { get; init; } = "mistral";
    public GpuConfig Gpu { get; init; } = new();
}

public sealed record GpuConfig
{
    public bool Enabled { get; init; } = false;
    public GpuType? Type { get; init; } = null;
}

public enum GpuType
{
    Nvidia,
    Amd
}
```

**Field Specifications**:

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `GenerationModel` | string | Ollama model for RAG synthesis | `"mistral"` |
| `Gpu.Enabled` | bool | Enable GPU acceleration | `false` |
| `Gpu.Type` | GpuType? | GPU type: `Nvidia`, `Amd`, or `null` | `null` |

**Important Note**: The embedding model is fixed to `mxbai-embed-large` (1024 dimensions) and is NOT configurable. This ensures consistent vector dimensions across all projects.

#### 2.2 GlobalSettings Model

Create a strongly-typed model for `settings.json`:

```csharp
namespace CSharpCompoundingDocs.Configuration;

public sealed record GlobalSettings
{
    public FileWatcherSettings FileWatcher { get; init; } = new();
}

public sealed record FileWatcherSettings
{
    public int DebounceMs { get; init; } = 500;
}
```

**Field Specifications**:

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `FileWatcher.DebounceMs` | int | Milliseconds to wait after file change before processing | `500` |

**Default Rationale**: `500ms` balances responsiveness with avoiding duplicate events from editors that perform multiple writes during save operations.

### 3. JSON Schema Definitions

Create JSON Schema files for validation (embedded as resources or in a schemas folder):

#### 3.1 ollama-config.schema.json

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "ollama-config.schema.json",
  "title": "Ollama Configuration",
  "description": "Global Ollama model and GPU acceleration settings",
  "type": "object",
  "properties": {
    "generation_model": {
      "type": "string",
      "description": "Ollama model for RAG synthesis",
      "default": "mistral"
    },
    "gpu": {
      "type": "object",
      "properties": {
        "enabled": {
          "type": "boolean",
          "description": "Enable GPU acceleration",
          "default": false
        },
        "type": {
          "type": ["string", "null"],
          "enum": ["nvidia", "amd", null],
          "description": "GPU type for acceleration",
          "default": null
        }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false
}
```

#### 3.2 settings.schema.json

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "settings.schema.json",
  "title": "Global Settings",
  "description": "User-scoped MCP server settings",
  "type": "object",
  "properties": {
    "file_watcher": {
      "type": "object",
      "properties": {
        "debounce_ms": {
          "type": "integer",
          "minimum": 100,
          "maximum": 5000,
          "description": "Milliseconds to wait after file change before processing",
          "default": 500
        }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false
}
```

### 4. GlobalConfigurationService

Implement a service for managing global configuration:

```csharp
namespace CSharpCompoundingDocs.Configuration;

public interface IGlobalConfigurationService
{
    /// <summary>
    /// Gets the path to the global configuration directory.
    /// Respects CDOCS_HOME environment variable override.
    /// </summary>
    string ConfigDirectory { get; }

    /// <summary>
    /// Ensures the global configuration directory exists with default files.
    /// Called on first run or when directory is missing.
    /// </summary>
    Task EnsureInitializedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the Ollama configuration, creating defaults if missing.
    /// </summary>
    Task<OllamaConfig> GetOllamaConfigAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves updated Ollama configuration.
    /// </summary>
    Task SaveOllamaConfigAsync(OllamaConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads global settings, creating defaults if missing.
    /// </summary>
    Task<GlobalSettings> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves updated global settings.
    /// </summary>
    Task SaveSettingsAsync(GlobalSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a configuration file against its schema.
    /// </summary>
    ValidationResult ValidateConfig<T>(T config) where T : class;
}
```

### 5. First-Run Initialization Logic

The service must handle first-run scenarios:

```csharp
public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
{
    // 1. Determine config directory (CDOCS_HOME env var or default)
    var configDir = GetConfigDirectory();

    // 2. Create directory if it doesn't exist
    if (!Directory.Exists(configDir))
    {
        Directory.CreateDirectory(configDir);
    }

    // 3. Create ollama-config.json with defaults if missing
    var ollamaConfigPath = Path.Combine(configDir, "ollama-config.json");
    if (!File.Exists(ollamaConfigPath))
    {
        var defaultConfig = new OllamaConfig();
        await WriteConfigAsync(ollamaConfigPath, defaultConfig, cancellationToken);
    }

    // 4. Create settings.json with defaults if missing
    var settingsPath = Path.Combine(configDir, "settings.json");
    if (!File.Exists(settingsPath))
    {
        var defaultSettings = new GlobalSettings();
        await WriteConfigAsync(settingsPath, defaultSettings, cancellationToken);
    }
}
```

### 6. Environment Variable Support

Support the `CDOCS_HOME` environment variable to override the default config directory:

```csharp
private string GetConfigDirectory()
{
    // Check for environment variable override
    var envHome = Environment.GetEnvironmentVariable("CDOCS_HOME");
    if (!string.IsNullOrWhiteSpace(envHome))
    {
        return envHome;
    }

    // Default: ~/.claude/.csharp-compounding-docs/
    var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    return Path.Combine(userHome, ".claude", ".csharp-compounding-docs");
}
```

### 7. JSON Serialization Options

Configure consistent JSON serialization across the application:

```csharp
public static class JsonSerializerOptionsFactory
{
    public static JsonSerializerOptions CreateDefault() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)
        }
    };
}
```

---

## Validation Requirements

### Schema Validation

Use JsonSchema.Net for runtime validation:

```csharp
public ValidationResult ValidateConfig<T>(T config) where T : class
{
    var json = JsonSerializer.Serialize(config, _jsonOptions);
    var jsonNode = JsonNode.Parse(json);

    var schema = GetSchemaFor<T>();
    var result = schema.Evaluate(jsonNode, new EvaluationOptions
    {
        OutputFormat = OutputFormat.List,
        ValidateAs = Draft.Draft202012
    });

    return new ValidationResult(
        IsValid: result.IsValid,
        Errors: result.Details
            .Where(d => !d.IsValid)
            .Select(d => $"{d.InstanceLocation}: {d.Errors?.FirstOrDefault().Value}")
            .ToList()
    );
}
```

### Value Constraints

| Field | Constraint | Validation |
|-------|-----------|------------|
| `generation_model` | Non-empty string | Required |
| `gpu.type` | `"nvidia"`, `"amd"`, or `null` | Enum validation |
| `file_watcher.debounce_ms` | 100-5000 | Range validation |

---

## File Structure

```
src/CSharpCompoundingDocs/
├── Configuration/
│   ├── Models/
│   │   ├── OllamaConfig.cs
│   │   ├── GpuConfig.cs
│   │   ├── GlobalSettings.cs
│   │   └── FileWatcherSettings.cs
│   ├── Schemas/
│   │   ├── ollama-config.schema.json    (embedded resource)
│   │   └── settings.schema.json         (embedded resource)
│   ├── IGlobalConfigurationService.cs
│   ├── GlobalConfigurationService.cs
│   ├── JsonSerializerOptionsFactory.cs
│   └── ValidationResult.cs
```

---

## Testing Requirements

### Unit Tests

1. **Default Value Tests**
   - Verify `OllamaConfig` defaults to `generation_model: "mistral"`, `gpu.enabled: false`, `gpu.type: null`
   - Verify `GlobalSettings` defaults to `file_watcher.debounce_ms: 500`

2. **Serialization Tests**
   - JSON round-trip preserves all values
   - Snake_case property naming in serialized JSON
   - Null values omitted from output

3. **Environment Variable Tests**
   - `CDOCS_HOME` override changes config directory path
   - Missing `CDOCS_HOME` uses default path

4. **Validation Tests**
   - Valid configurations pass validation
   - Invalid `gpu.type` values fail validation
   - `debounce_ms` outside 100-5000 range fails validation

### Integration Tests

1. **First-Run Initialization**
   - Empty directory creates both config files with defaults
   - Existing files are not overwritten
   - Directory created with correct permissions

2. **File Operations**
   - Config files are readable after write
   - Concurrent access handled correctly
   - Invalid JSON in existing file produces clear error

---

## Default Configuration Files

### ollama-config.json (Generated on First Run)

```json
{
  "generation_model": "mistral",
  "gpu": {
    "enabled": false,
    "type": null
  }
}
```

### settings.json (Generated on First Run)

```json
{
  "file_watcher": {
    "debounce_ms": 500
  }
}
```

---

## Dependencies

- **JsonSchema.Net** (8.0.5+) - JSON Schema Draft 2020-12 validation
- **System.Text.Json** - JSON serialization (built-in)

---

## Acceptance Criteria

- [ ] Global config directory created at `~/.claude/.csharp-compounding-docs/` on first run
- [ ] `ollama-config.json` generated with correct defaults
- [ ] `settings.json` generated with correct defaults
- [ ] `CDOCS_HOME` environment variable correctly overrides default path
- [ ] All config models have strongly-typed properties with sensible defaults
- [ ] JSON Schema validation catches invalid configurations
- [ ] Snake_case JSON property naming used consistently
- [ ] Existing config files are never overwritten during initialization
- [ ] Unit tests cover all default values and validation rules
- [ ] Integration tests verify first-run behavior

---

## Notes

- The `docker-compose.yml` file generation is handled in Phase 003 (Docker Infrastructure)
- Project-level configuration is handled in Phase 009 (Project Configuration Structure)
- The embedding model (`mxbai-embed-large`) is intentionally not configurable to ensure consistent vector dimensions
