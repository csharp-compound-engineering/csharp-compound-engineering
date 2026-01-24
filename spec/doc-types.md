# Doc-Types Architecture

> **Status**: [DRAFT]
> **Parent**: [SPEC.md](../SPEC.md)

---

> **Background**: Detailed research on skill file structure, YAML frontmatter fields, auto-invocation mechanisms, and tool permissions used by doc-type capture skills. See [Building Claude Code Skills Research](../research/building-claude-code-skills-research.md).

> **Background**: Comprehensive coverage of JSON Schema for YAML validation, schema definition patterns, and best practices for schema design. See [YAML Schema Formats Research](../research/yaml-schema-formats.md).

---

## Overview

Doc-types are categories of institutional knowledge that the plugin captures. Each doc-type has:
- A dedicated storage folder (`./csharp-compounding-docs/{doc-type}/`)
- A schema file defining required/optional fields
- A skill for creating documents of that type
- Enum-validated metadata in YAML frontmatter

---

## Sub-Documents

This specification is organized into the following sub-documents:

### Built-in Types

The 5 built-in doc-types (problem, insight, codebase, tool, style) are fully specified with complete schemas, trigger phrases, classification hints, and capture guidelines. Load when implementing or extending capture skills. See [doc-types/built-in-types.md](./doc-types/built-in-types.md).

### Custom Types

Creating custom doc-types via `/cdocs:create-type`, including schema file format, field types, validation rules, and the two-stage classification mechanism. Load when users need to define project-specific doc-types. See [doc-types/custom-types.md](./doc-types/custom-types.md).

### Promotion

Document promotion levels (standard, important, critical), promotion workflow, demotion patterns, and RAG integration for boosting critical knowledge. Load when implementing or using the promotion system. See [doc-types/promotion.md](./doc-types/promotion.md).

---

## Naming Conventions

Doc-type identifiers use different casing depending on context:

| Context | Format | Example |
|---------|--------|---------|
| URLs, folders, CLI | kebab-case | `api-contract`, `problem` |
| JSON/YAML keys | snake_case | `doc_type`, `api_contract` |
| C# classes/properties | PascalCase | `DocType`, `ApiContract` |

This is intentional to follow conventions in each environment. The MCP server handles translation between formats automatically.

---

## Two-Stage Classification Model

Trigger phrases and classification hints work **together** to reduce false positives:

1. **Trigger phrases** cast a wide net - they're intentionally broad to avoid missing relevant content
2. **Classification hints** provide semantic validation - the skill checks conversation context against these hints before prompting for capture
3. Some trigger phrases (e.g., "always", "never", "prefer" in the `style` doc-type) are generic by design. The classification hints (e.g., "convention", "naming", "formatting") help distinguish "we always use PascalCase for public methods" from "I always drink coffee in the morning"

If classification hints don't match the conversation context, the skill should NOT auto-invoke even if trigger phrases are detected.

### How Auto-Invoke Works

1. Each doc-type skill independently monitors for its own `trigger_phrases`
2. When trigger phrases are detected, the `/cdocs:{name}` skill activates directly
3. `classification_hints` help the skill confirm it's the right doc-type for the content
4. If multiple skills trigger simultaneously, `/cdocs:capture-select` auto-invokes for disambiguation

See [Built-in Types](./doc-types/built-in-types.md) for the complete schemas including trigger phrases and classification hints for each built-in type.

---

## Common Fields

All doc-type schemas share common required and optional fields.

### Common Required Fields (All Doc-Types)

| Field | Type | Description |
|-------|------|-------------|
| `doc_type` | string | Document type identifier (e.g., `problem`, `insight`, `codebase`) |
| `title` | string | Human-readable title |
| `date` | string (YYYY-MM-DD) | Date captured |
| `summary` | string | One-line summary for search results |
| `significance` | enum | Why this matters: `architectural`, `behavioral`, `performance`, `correctness`, `convention`, `integration` |

### Common Optional Fields (All Doc-Types)

| Field | Type | Description |
|-------|------|-------------|
| `tags` | array[string] | Searchable keywords |
| `related_docs` | array[string] | Relative paths to related documents |
| `supersedes` | string | Path to doc this replaces (if any) |
| `promotion_level` | enum | Document visibility tier (default: `standard`) |

Each doc-type extends these common fields with its own required/optional fields and enums. See [Built-in Types](./doc-types/built-in-types.md) for type-specific fields and [Custom Types](./doc-types/custom-types.md) for creating your own.

---

## Document Lifecycle

> **Background**: In-depth coverage of FileSystemWatcher patterns, event debouncing, background service architecture, and embedding operation mapping for RAG synchronization. See [.NET FileSystemWatcher for RAG Embedding Synchronization](../research/dotnet-file-watcher-embeddings-research.md).

### Creation
1. Skill gathers context from conversation
2. Validates against doc-type schema
3. Generates filename: `{sanitized-title}-{YYYYMMDD}.md`
4. Writes to `./csharp-compounding-docs/{doc-type}/`
5. File watcher detects new file
6. MCP server generates embedding and upserts to vector DB

### Update
1. Agent or user edits file
2. File watcher detects change
3. MCP server re-generates embedding
4. Upserts to vector DB (same document ID)

### Deletion
1. File removed from disk (git operation, manual, etc.)
2. File watcher detects deletion
3. MCP server removes from vector DB

### Promotion
1. Document identified for higher visibility
2. `/cdocs:promote` skill updates frontmatter and database
3. Document surfaces more readily in related queries

See [Promotion](./doc-types/promotion.md) for detailed promotion workflow and guidelines.

---

## Open Questions

1. What's the minimum required schema for a custom doc-type?
2. Should there be a "deprecated" status for documents?

## Resolved Questions

| Question | Resolution | See |
|----------|------------|-----|
| Built-in schema storage | Files in plugin at `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-{name}/schema.yaml` | [configuration.md](./configuration.md#built-in-schema-locations) |
