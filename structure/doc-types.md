# Doc-Types Structure Summary

This file contains summaries for the doc-types specification and its children.

---

## spec/doc-types.md

### What This File Covers

The doc-types architecture specification defines how the plugin categorizes and manages institutional knowledge. Key topics include:

- **Doc-type concept**: Categories of knowledge (problem, insight, codebase, tool, style) each with dedicated storage folders, schemas, skills, and YAML frontmatter validation
- **Naming conventions**: Context-specific casing (kebab-case for URLs/CLI, snake_case for JSON/YAML, PascalCase for C#)
- **Two-stage classification model**: Trigger phrases cast a wide net; classification hints provide semantic validation to reduce false positives
- **Common fields**: Required fields (doc_type, title, date, summary, significance) and optional fields (tags, related_docs, supersedes, promotion_level) shared across all doc-types
- **Document lifecycle**: Creation, update, deletion, and promotion workflows including file watcher integration with vector DB synchronization

### Structural Relationships

| Relationship | File |
|--------------|------|
| **Parent** | [SPEC.md](../SPEC.md) |
| **Child** | [doc-types/built-in-types.md](./spec/doc-types/built-in-types.md) - Complete schemas for 5 built-in types |
| **Child** | [doc-types/custom-types.md](./spec/doc-types/custom-types.md) - Creating custom doc-types via /cdocs:create-type |
| **Child** | [doc-types/promotion.md](./spec/doc-types/promotion.md) - Promotion levels and workflow |
| **Sibling** | [mcp-server.md](./spec/mcp-server.md) - MCP server implementation |
| **Sibling** | [skills.md](./spec/skills.md) - Skills and commands |
| **Sibling** | [configuration.md](./spec/configuration.md) - Plugin configuration |
| **Cross-ref** | [configuration.md](./spec/configuration.md#built-in-schema-locations) - Built-in schema storage locations |

---

## spec/doc-types/built-in-types.md

### What This File Covers

This specification defines the five built-in doc-types that ship with the compound-engineering plugin for capturing institutional knowledge:

1. **Problems & Solutions (`problem`)** - Documents solved problems with symptoms, root cause, and solution
2. **Product/Project Insights (`insight`)** - Captures significant learnings about the product (business logic, user behavior, domain knowledge)
3. **Codebase Knowledge (`codebase`)** - Documents architectural decisions, code patterns, and structural knowledge
4. **Tools & Libraries (`tool`)** - Captures knowledge about tools, libraries, and dependency gotchas
5. **Coding Styles & Preferences (`style`)** - Documents coding conventions and team standards

Each doc-type includes:
- A dedicated storage folder under `./csharp-compounding-docs/{type}/`
- A YAML schema with `trigger_phrases` and `classification_hints` for auto-capture
- Required and optional fields specific to that knowledge type
- An associated skill (`/cdocs:{type}`) for capturing documents

The file also provides capture guidelines (what to capture vs. avoid) and cross-referencing mechanisms for linking related documents.

### Structural Relationships

- **Parent**: `spec/doc-types.md` (Doc-Types Architecture)
- **Siblings**:
  - `spec/doc-types/custom-types.md` - Creating custom doc-types
  - `spec/doc-types/promotion.md` - Document promotion levels and workflow
- **Grandparent**: `spec/SPEC.md` (main specification)
- **Referenced Research**:
  - `research/compound-engineering-paradigm-research.md`
  - `research/yaml-schema-formats.md`
  - `research/claude-code-skills-research.md`
  - `research/building-claude-code-skills-research.md`
  - `research/dotnet-markdown-parser-research.md`

---

## spec/doc-types/custom-types.md

### What This File Covers

This specification defines how projects can create custom doc-types beyond the five built-in types (problem, insight, codebase, tool, style). Key topics include:

- **Creation Process**: Custom doc-types are created via `/cdocs:create-type`, which generates a schema file, a dedicated skill, and config registration
- **Schema Structure**: YAML schema files define required/optional fields, enum values, validation rules, trigger phrases, and classification hints
- **Two-Stage Classification**: Trigger phrases cast a wide net for detection, while classification hints provide semantic validation to reduce false positives
- **Field Types**: Supports string, enum, array, and boolean field types
- **Validation Rules**: MCP server validates documents against schemas for required fields, enum values, and type checking
- **Generated Skills**: Each custom doc-type gets an independent skill at `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-{name}/SKILL.md`

### Structural Relationships

- **Parent**: `spec/doc-types.md` (Doc-Types Architecture)
- **Siblings**:
  - `spec/doc-types/built-in-types.md` - Defines the 5 built-in doc-types with complete schemas
  - `spec/doc-types/promotion.md` - Covers document promotion levels and workflow
- **Referenced Research**:
  - `research/yaml-schema-formats.md` - JSON Schema for YAML validation
  - `research/building-claude-code-skills-research.md` - Skill file structure and auto-invocation
  - `research/json-schema-validation-library-research.md` - Validation library evaluation

---

## spec/doc-types/promotion.md

### What This File Covers

The `promotion.md` specification defines a three-tier document visibility system for controlling how readily documents surface in RAG queries and search results:

- **Standard** (default): Retrieved via normal RAG/search
- **Important**: Higher relevance boost (1.5x weight), surfaces more readily
- **Critical**: Required reading, must be surfaced before code generation in related areas

Key topics include:
- Promotion/demotion workflows via `/cdocs:promote` skill
- Guidelines for when to promote (and warnings against over-promotion)
- Database schema integration with Semantic Kernel's model-first approach
- MCP tool support (`rag_query`, `semantic_search`) with promotion filtering parameters
- Promotion guidelines specific to each doc-type (problem, insight, codebase, tool, style)
- YAML frontmatter examples for each promotion level
- RAG pipeline integration including critical document injection and relevance boosting
- Best practices for review cadence and team consensus

### Structural Relationships

- **Parent**: `spec/doc-types.md` (Doc-Types Architecture)
- **Siblings**:
  - `spec/doc-types/built-in-types.md` (defines the 5 built-in doc-types)
  - `spec/doc-types/custom-types.md` (creating custom doc-types)
- **Referenced Documents**:
  - `research/compound-engineering-paradigm-research.md`
  - `research/microsoft-semantic-kernel-research.md`
  - `research/mcp-csharp-sdk-research.md`
  - `research/semantic-kernel-ollama-rag-research.md`
  - `research/postgresql-pgvector-research.md`
  - `spec/mcp-server/database-schema.md`
