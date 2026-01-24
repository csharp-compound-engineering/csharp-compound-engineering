# Configuration Specification

> **Status**: [DRAFT]
> **Parent**: [SPEC.md](../SPEC.md)

---

## Overview

Configuration exists at two levels:
1. **Global** (`~/.claude/.csharp-compounding-docs/`) - Infrastructure settings
2. **Project** (`./csharp-compounding-docs/config.json`) - Project-specific settings

---

## Global Configuration

### Location

`~/.claude/.csharp-compounding-docs/`

### Files

#### `ollama-config.json`

> **Background**: GPU passthrough configuration differs significantly between NVIDIA, AMD, and Apple Silicon platforms. See [Ollama Docker GPU Research](../research/ollama-docker-gpu-research.md).

```json
{
  "generation_model": "mistral",
  "gpu": {
    "enabled": false,
    "type": null
  }
}
```

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `generation_model` | string | Ollama model for RAG synthesis | `"mistral"` |
| `gpu.enabled` | boolean | Enable GPU acceleration | `false` |
| `gpu.type` | string\|null | GPU type: `"nvidia"`, `"amd"`, or `null` | `null` |

**Note**: The embedding model is fixed to `mxbai-embed-large` (1024 dimensions) and is not configurable. This ensures consistent vector dimensions across all projects and simplifies the database schema.

#### `settings.json`

User-scoped MCP server settings:

```json
{
  "file_watcher": {
    "debounce_ms": 500
  }
}
```

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `file_watcher.debounce_ms` | integer | Milliseconds to wait after file change before processing | `500` |

**Rationale for defaults**:
- `debounce_ms: 500` balances responsiveness with avoiding duplicate events from editors that perform multiple writes during save operations.

> **Background**: FileSystemWatcher can fire multiple events for a single logical file change. See [.NET FileSystemWatcher for RAG Embedding Synchronization](../research/dotnet-file-watcher-embeddings-research.md) for debouncing strategies and event handling patterns.

#### `docker-compose.yml`

Generated from template. See [infrastructure.md](./infrastructure.md).

---

## Project Configuration

> **Background**: Project configuration is loaded dynamically at runtime when `activate_project()` is called. A custom switchable configuration provider enables hot-reloading with `IOptionsMonitor`. See [IOptionsMonitor with Dynamically Discovered Configuration Paths](../research/ioptions-monitor-dynamic-paths.md).

### Location

`.csharp-compounding-docs/config.json` (hidden folder at project/repo root)

### Schema

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["project_name"],
  "properties": {
    "project_name": {
      "type": "string",
      "pattern": "^[a-z][a-z0-9-]*$",
      "description": "Project identifier (used as PostgreSQL schema name)"
    },
    "retrieval": {
      "type": "object",
      "description": "RAG query settings",
      "properties": {
        "min_relevance_score": {
          "type": "number",
          "minimum": 0,
          "maximum": 1,
          "default": 0.7,
          "description": "Minimum similarity score for RAG queries"
        },
        "max_results": {
          "type": "integer",
          "minimum": 1,
          "default": 3,
          "description": "Maximum documents returned (excluding linked)"
        },
        "max_linked_docs": {
          "type": "integer",
          "minimum": 0,
          "default": 5,
          "description": "Maximum linked documents to include"
        }
      }
    },
    "semantic_search": {
      "type": "object",
      "description": "Semantic search settings (separate from RAG)",
      "properties": {
        "min_relevance_score": {
          "type": "number",
          "minimum": 0,
          "maximum": 1,
          "default": 0.5,
          "description": "Minimum similarity score for semantic search"
        },
        "default_limit": {
          "type": "integer",
          "minimum": 1,
          "default": 10,
          "description": "Default number of results"
        }
      }
    },
    "link_resolution": {
      "type": "object",
      "properties": {
        "max_depth": {
          "type": "integer",
          "minimum": 0,
          "default": 2,
          "description": "How many levels deep to follow links"
        }
      }
    },
    "external_docs": {
      "type": "object",
      "description": "External documentation settings (read-only, separate index)",
      "properties": {
        "path": {
          "type": "string",
          "description": "Path to external docs folder"
        },
        "include_patterns": {
          "type": "array",
          "items": { "type": "string" },
          "default": ["**/*.md"]
        },
        "exclude_patterns": {
          "type": "array",
          "items": { "type": "string" },
          "default": ["**/node_modules/**"]
        }
      }
    },
    "custom_doc_types": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/customDocType"
      },
      "default": []
    }
  },
  "$defs": {
    "customDocType": {
      "type": "object",
      "required": ["name", "description", "folder", "schema_file"],
      "properties": {
        "name": {
          "type": "string",
          "pattern": "^[a-z][a-z0-9-]*$",
          "description": "Doc-type identifier (used in skill name)"
        },
        "description": {
          "type": "string",
          "description": "Human-readable description"
        },
        "folder": {
          "type": "string",
          "description": "Subfolder name in ./csharp-compounding-docs/"
        },
        "schema_file": {
          "type": "string",
          "description": "Path to schema file (relative to ./csharp-compounding-docs/)"
        }
      }
    }
  }
}
```

### Full Example

```json
{
  "project_name": "my-awesome-app",
  "retrieval": {
    "min_relevance_score": 0.7,
    "max_results": 3,
    "max_linked_docs": 5
  },
  "semantic_search": {
    "min_relevance_score": 0.5,
    "default_limit": 10
  },
  "link_resolution": {
    "max_depth": 2
  },
  "custom_doc_types": [
    {
      "name": "api-contract",
      "description": "API design decisions and contract specifications",
      "folder": "api-contracts",
      "schema_file": "./schemas/api-contract.schema.yaml"
    },
    {
      "name": "migration",
      "description": "Database migration notes and decisions",
      "folder": "migrations",
      "schema_file": "./schemas/migration.schema.json"
    }
  ]
}
```

### Retrieval Settings

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `retrieval.min_relevance_score` | float | Minimum similarity score (0.0-1.0) | `0.7` |
| `retrieval.max_results` | integer | Max documents returned (excluding linked) | `3` |
| `retrieval.max_linked_docs` | integer | Max linked documents to include | `5` |
| `link_resolution.max_depth` | integer | How many levels deep to follow links | `2` |

### Semantic Search Settings

Semantic search uses separate thresholds from RAG retrieval since the use cases differ:

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `semantic_search.min_relevance_score` | float | Minimum similarity score (0.0-1.0) | `0.5` |
| `semantic_search.default_limit` | integer | Default number of results | `10` |

**Why separate settings?** RAG queries need high-relevance documents for synthesis (default 0.7), while semantic search is exploratory and benefits from showing more results at lower thresholds (default 0.5).

**Configuration Precedence**: When MCP tools accept parameters that overlap with project config settings, the precedence order is:
1. **Tool parameter** (if explicitly provided in the tool call)
2. **Project config** (from `.csharp-compounding-docs/config.json`)
3. **Built-in default** (hardcoded in MCP server)

For example, if `retrieval.min_relevance_score` is set to `0.8` in project config, but `rag_query` is called with `min_relevance_score: 0.6`, the tool uses `0.6`.

### External Documentation (Optional)

Projects can optionally configure an external documentation folder for read-only search:

| Field | Type | Description | Default |
|-------|------|-------------|---------|
| `external_docs.path` | string | Path to external docs folder (relative or absolute) | `null` |
| `external_docs.include_patterns` | array[string] | Glob patterns to include | `["**/*.md"]` |
| `external_docs.exclude_patterns` | array[string] | Glob patterns to exclude | `["**/node_modules/**"]` |

**Path Resolution**: Relative paths are resolved from the **repository root** (the directory containing `.git/`). For example, `"./docs"` resolves to `{repo_root}/docs`.

**Path Validation**: At activation time, the MCP server validates that `external_docs.path` exists and is a directory. If the path doesn't exist, activation returns an error with a clear message: `"External docs path '{path}' does not exist"`. This prevents silent failures during indexing.

**Example**:
```json
{
  "project_name": "my-app",
  "external_docs": {
    "path": "./docs",
    "include_patterns": ["**/*.md", "**/*.rst"],
    "exclude_patterns": ["**/drafts/**"]
  }
}
```

**Note**: External docs are **read-only**. No skills are provided to modify these documents. The assumption is that external documentation is maintained via an external process (e.g., documentation generators, wikis).

**Schema Isolation**: External docs are indexed in a **separate collection** from compounding docs. This ensures:
- External doc search results don't pollute RAG queries for institutional knowledge
- Different relevance thresholds can apply (tool default is 0.7, but `semantic_search.min_relevance_score` overrides it)
- Clear separation between captured knowledge and reference documentation

---

## Doc-Type Schema Files

Custom doc-type schemas are stored in `./csharp-compounding-docs/schemas/` using JSON Schema Draft 2020-12 format.

> **Background**: JSON Schema is the most portable choice for YAML validation since YAML is a superset of JSON. See [YAML Schema Formats](../research/yaml-schema-formats.md) for schema definition patterns and validation strategies.

- **Supported formats**: `.yaml`, `.yml`, `.json`
- **Built-in schemas**: Embedded in plugin under `${CLAUDE_PLUGIN_ROOT}/skills/`

> **Full specification**: [configuration/schema-files.md](./configuration/schema-files.md)

---

## Project Directory Structure

When compounding docs is initialized for a project:

```
my-project/
├── .csharp-compounding-docs/           # Note: hidden folder
│   └── config.json              # Project configuration
├── csharp-compounding-docs/            # Documentation storage (not hidden)
│   ├── problems/
│   ├── insights/
│   ├── codebase/
│   ├── tools/
│   ├── styles/
│   ├── schemas/                 # Custom schema files
│   │   └── api-contract.schema.yaml
│   └── {custom-folders}/        # Custom doc-type folders
└── ...
```

### Why Two Folders?

- `.csharp-compounding-docs/` (hidden): Configuration that shouldn't clutter the repo
- `csharp-compounding-docs/` (visible): Actual documentation that should be visible and version-controlled

---

## Initialization

### First-Time Project Setup

When `/cdocs:activate` runs on a project without config:

1. Create `.csharp-compounding-docs/config.json`:
   ```json
   {
     "project_name": "{derived-from-folder-name}",
     "custom_doc_types": []
   }
   ```

2. Create `csharp-compounding-docs/` with built-in folders:
   ```
   csharp-compounding-docs/
   ├── problems/
   ├── insights/
   ├── codebase/
   ├── tools/
   ├── styles/
   └── schemas/
   ```

3. Add to `.gitignore` (suggest, don't force):
   ```
   # Compounding docs config (optional)
   # .csharp-compounding-docs/
   ```

---

## Schema Validation Libraries

### Resolved Decision

Use **JsonSchema.Net** (from json-everything) with **Yaml2JsonNode** for YAML support.

**Required Packages**:
```xml
<PackageReference Include="JsonSchema.Net" Version="8.0.5" />
<PackageReference Include="Yaml2JsonNode" Version="2.4.0" />
```

**Rationale** (see [research/json-schema-validation-libraries-research.md](../research/json-schema-validation-libraries-research.md)):
- Full JSON Schema Draft 2020-12 support (NJsonSchema only supports Draft 4-7)
- Uses System.Text.Json (modern .NET standard)
- MIT license
- Active maintenance (used by PowerShell)
- Yaml2JsonNode provides seamless YAML-to-JsonNode conversion

### Validation Flow

1. Load schema file (YAML or JSON)
2. Convert to `JsonNode` (use Yaml2JsonNode for YAML files)
3. Load schema using `JsonSchema.FromText()`
4. Evaluate frontmatter against schema with `Draft.Draft202012`
5. Return detailed validation errors if any

### Example Usage

```csharp
using Json.Schema;
using Yaml2JsonNode;

// Parse YAML frontmatter
var yaml = new YamlStream();
yaml.Load(new StringReader(yamlContent));
var jsonNode = yaml.Documents[0].RootNode.ToJsonNode();

// Validate against schema
var schema = JsonSchema.FromText(schemaJson);
var result = schema.Evaluate(jsonNode, new EvaluationOptions
{
    OutputFormat = OutputFormat.List,
    ValidateAs = Draft.Draft202012
});
```

---

## Environment Variables

> **Background**: Environment variables follow .NET Generic Host conventions with hierarchical key mapping using double underscores. See [Environment Variables with IConfiguration](../research/environment-variables-iconfiguration.md) for naming conventions and precedence rules.

For CI/CD or advanced users, some settings can be overridden:

| Variable | Description | Default |
|----------|-------------|---------|
| `CDOCS_POSTGRES_HOST` | PostgreSQL host | From Docker |
| `CDOCS_POSTGRES_PORT` | PostgreSQL port | `5433` |
| `CDOCS_OLLAMA_HOST` | Ollama host | From Docker |
| `CDOCS_OLLAMA_PORT` | Ollama port | `11435` |
| `CDOCS_HOME` | Override `~/.claude/.csharp-compounding-docs/` | `~/.claude/.csharp-compounding-docs/` |

---

## Open Questions

1. Should `config.json` support inheriting from a base config?
2. ~~How to handle schema migrations when doc-type schemas change?~~ **Deferred to post-MVP**: No schema migration tooling for MVP. Users are responsible for manually updating existing documents when they modify custom doc-type schemas.
3. Should there be project-level overrides for embedding/generation models?

