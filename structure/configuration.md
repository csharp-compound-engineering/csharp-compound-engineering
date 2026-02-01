# Configuration Structure Summary

This file contains summaries for the configuration specification and its children.

---

## spec/configuration.md

### What This File Covers

The `spec/configuration.md` file defines the two-tier configuration system for the csharp-compounding-docs plugin:

1. **Global Configuration** (`~/.claude/.csharp-compounding-docs/`): Infrastructure-level settings including Ollama model selection, GPU acceleration settings, and file watcher debounce timing. Contains `ollama-config.json`, `settings.json`, and generated `docker-compose.yml`.

2. **Project Configuration** (`.csharp-compounding-docs/config.json`): Per-project settings with JSON Schema validation including project name, RAG retrieval parameters (relevance scores, result limits, link depth), semantic search settings, external documentation paths, and custom doc-type definitions.

Key topics addressed:
- Full JSON Schema for project configuration with detailed field definitions
- Retrieval vs semantic search threshold rationale (0.7 vs 0.5 defaults)
- Configuration precedence order (tool parameter > project config > built-in default)
- External documentation indexing with path validation and schema isolation
- Custom doc-type schema file definitions using JSON Schema Draft 2020-12
- Project directory structure (hidden `.csharp-compounding-docs/` for config, visible `csharp-compounding-docs/` for docs)
- First-time project initialization workflow
- Schema validation using JsonSchema.Net with Yaml2JsonNode
- Environment variable overrides for CI/CD scenarios

### Structural Relationships

- **Parent**: `SPEC.md` (root specification document)
- **Children**: `spec/configuration/schema-files.md` (detailed doc-type schema file specifications)
- **Siblings**: `spec/doc-types.md`, `spec/mcp-server.md`, `spec/infrastructure.md`, `spec/skills.md`, `spec/agents.md`, `spec/marketplace.md`, `spec/testing.md`, `spec/observability.md`, `spec/research-index.md`
- **Cross-references**: Links to `spec/infrastructure.md` for Docker Compose details, multiple research documents for background on implementation decisions

---

## spec/configuration/schema-files.md

### What This File Covers

This specification defines how doc-type schema files work in the csharp-compounding-docs system. Key topics include:

- **Schema Location**: Custom schemas stored at `./csharp-compounding-docs/schemas/{doc-type}.schema.yaml` (or `.json`)
- **Format Support**: Both YAML and JSON schemas supported; file extension determines the parser used
- **Schema Standard**: JSON Schema Draft 2020-12 is the required format
- **Built-in Schemas**: Pre-defined schemas embedded in the plugin under `${CLAUDE_PLUGIN_ROOT}/skills/` for doc-types: problem, insight, codebase, tool, and style
- **Example Schema**: Provides a complete `api-contract.schema.yaml` example demonstrating typical frontmatter validation fields (title, date, summary, contract_type, api_version, etc.)

### Structural Relationships

- **Parent**: `spec/configuration.md` - The main configuration specification that covers global/project config, retrieval settings, and custom doc-types
- **Siblings**: None currently (only child under `spec/configuration/`)
- **Grandparent**: `SPEC.md` - The root specification document
- **Referenced Research**:
  - `research/yaml-schema-formats.md` - Background on JSON Schema for YAML validation
  - `research/json-schema-validation-libraries-research.md` - .NET implementation details (JsonSchema.Net + YamlDotNet)
  - `research/claude-code-plugin-installation-mechanism.md` - Plugin root environment variable details
