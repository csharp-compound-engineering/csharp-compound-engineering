# Skills Structure Summary

This file contains summaries for the skills specification and its children.

---

## spec/skills.md

### What This File Covers

The Skills and Commands Specification defines all `/cdocs:` prefixed skills that guide Claude Code agents in capturing and retrieving compounding documentation. Key content includes:

- **Architectural Pattern**: Uses a distributed capture pattern where each doc-type skill has its own auto-invoke triggers (versus the original plugin's centralized router)
- **Multi-Trigger Resolution**: When multiple skills trigger simultaneously, `/cdocs:capture-select` meta-skill presents a selection dialog
- **Skill Categories**:
  - **Capture Skills** (5): problem, insight, codebase, tool, style - detect knowledge patterns and create structured docs
  - **Query Skills** (4): query, search, search-external, query-external - RAG and semantic search retrieval
  - **Meta Skills** (3): activate, create-type, capture-select - system-level operations
  - **Utility Skills** (5): delete, promote, todo, worktree, research - management capabilities
- **Auto-Invoke Mechanism**: Two-stage classification using trigger phrase matching plus semantic validation
- **Excluded**: `/cdocs:list` skill explicitly excluded to avoid token consumption issues

### Structural Relationships

| Relationship | File |
|--------------|------|
| **Parent** | [SPEC.md](../SPEC.md) |
| **Children** | [skills/capture-skills.md](./skills/capture-skills.md), [skills/query-skills.md](./skills/query-skills.md), [skills/meta-skills.md](./skills/meta-skills.md), [skills/utility-skills.md](./skills/utility-skills.md), [skills/skill-patterns.md](./skills/skill-patterns.md) |
| **Siblings** | doc-types.md, mcp-server.md, infrastructure.md, agents.md, marketplace.md, configuration.md, testing.md, observability.md, research-index.md |

---

## spec/skills/skill-patterns.md

### What This File Covers

This document defines common implementation patterns shared across all doc-creation skills in the cdocs system. It establishes consistency and maintainability by standardizing:

1. **Skill File Structure**: Directory layout with SKILL.md, schema.yaml, and optional references
2. **Common Skill Patterns**: Four-step workflow (Context Gathering, Schema Validation, File Writing, Decision Menu)
3. **Auto-Invocation**: Project entry detection and capture detection via trigger phrases (e.g., "fixed it", "problem solved")
4. **Cross-References**: How skills link related documents and how the MCP server resolves markdown links during RAG retrieval
5. **SKILL.md Template**: Complete YAML frontmatter template with allowed-tools, preconditions, and auto-invoke configuration

Key patterns include blocking on missing context, validating against schema.yaml before writing, generating filenames with `{sanitized-title}-{YYYYMMDD}.md` format, and presenting post-capture options to users.

### Structural Relationships

- **Parent**: `spec/skills.md` - The main Skills & Commands Specification that defines all `/cdocs:` prefixed skills and their naming conventions
- **Siblings**:
  - `spec/skills/capture-skills.md` - Individual capture skill definitions (problem, insight, codebase, tool, style)
  - `spec/skills/query-skills.md` - Search and retrieval skill definitions
  - `spec/skills/meta-skills.md` - Skills that operate on the skill system (activate, create-type, capture-select)
  - `spec/skills/utility-skills.md` - Management and maintenance skills (delete, promote, todo, worktree, research)
- **Grandparent**: `SPEC.md` - Root specification document

---

## spec/skills/capture-skills.md

### What This File Covers

The `capture-skills.md` file specifies five skills that detect knowledge patterns in conversations and create structured documentation. Each skill has:

- **Auto-invoke triggers**: Phrase patterns that automatically activate the skill
- **MCP integration**: Uses Sequential Thinking for complex analysis
- **Schema validation**: Validates captured content against defined fields
- **Output location**: Writes markdown files to `./csharp-compounding-docs/{type}/`

### Skills Defined

| Skill | Purpose | Output Directory |
|-------|---------|------------------|
| `/cdocs:problem` | Capture solved problems (bugs, errors, root causes) | `problems/` |
| `/cdocs:insight` | Capture product/project insights | `insights/` |
| `/cdocs:codebase` | Capture architectural decisions and patterns | `codebase/` |
| `/cdocs:tool` | Capture tool/library gotchas and workarounds | `tools/` |
| `/cdocs:style` | Capture coding conventions and preferences | `styles/` |

Key behaviors:
- File watcher auto-indexes new docs to vector database (no explicit indexing needed)
- Skills assume MCP servers are available (SessionStart hook verifies this)
- All skills adapted from `compound-docs` skill in original compound-engineering-plugin

### Structural Relationships

```
SPEC.md (root)
  └── skills.md (parent)
        └── skills/
              ├── capture-skills.md    <-- THIS FILE
              ├── query-skills.md      (sibling - search/retrieve docs)
              ├── meta-skills.md       (sibling - create-type, conflict resolution)
              ├── utility-skills.md    (sibling - delete, promote, management)
              └── skill-patterns.md    (sibling - implementation patterns)
```

**Parent**: `spec/skills.md` - Skills & Commands Specification (defines skill categories, naming conventions, distributed capture pattern)

**Siblings**:
- `query-skills.md` - Skills for searching and retrieving captured documentation
- `meta-skills.md` - Skills that operate on the skill system (create-type, capture-select for multi-trigger conflicts)
- `utility-skills.md` - Management skills (delete, promote, todo, worktree, research)
- `skill-patterns.md` - Common implementation patterns for all skills

**Referenced by**: `doc-types.md` (schema definitions and validation rules for capture skill output)

---

## spec/skills/query-skills.md

### What This File Covers

The `query-skills.md` specification defines four skills for searching and retrieving captured documentation:

1. **`/cdocs:query`** - RAG-based question answering against local documentation. Uses the `rag_query` MCP tool. Best for open-ended questions requiring synthesis across multiple sources.

2. **`/cdocs:search`** - Semantic search for specific documents. Uses the `semantic_search` MCP tool. Best for finding one specific document by query.

3. **`/cdocs:search-external`** - Read-only semantic search against external project documentation. Requires `external_docs` configuration. Uses `search_external_docs` MCP tool.

4. **`/cdocs:query-external`** - Read-only RAG query against external project documentation. Uses `rag_query_external` MCP tool.

The spec includes decision matrices for choosing between RAG queries (synthesis needs) vs semantic search (specific document retrieval).

### Structural Relationships

**Parent**: `spec/skills.md` - The main skills specification that categorizes all `/cdocs:` skills into four groups (Capture, Query, Meta, Utility) and defines the distributed capture pattern architecture.

**Siblings**:
- `spec/skills/capture-skills.md` - Skills for creating new documentation (problem, insight, codebase, tool, style)
- `spec/skills/meta-skills.md` - Skills operating on the skill system itself (activate, create-type, capture-select)
- `spec/skills/utility-skills.md` - Management skills (delete, promote, todo, worktree, research)
- `spec/skills/skill-patterns.md` - Common implementation patterns for all skills

**Referenced Research**:
- `research/semantic-kernel-ollama-rag-research.md` - RAG pipeline implementation details
- `research/mcp-csharp-sdk-research.md` - MCP tool registration patterns
- `research/postgresql-pgvector-research.md` - Vector database and similarity search
- `research/microsoft-extensions-ai-rag-research.md` - Alternative AI abstraction layer
- `research/building-claude-code-skills-research.md` - Skill authoring best practices

---

## spec/skills/meta-skills.md

### What This File Covers

The `spec/skills/meta-skills.md` file defines skills that operate on the skill system itself rather than on documentation content. It specifies three meta skills:

1. **`/cdocs:activate`** - Activates the compounding docs system for the current project. Auto-invoked when entering a project with `.csharp-compounding-docs/config.json`. Detects git root, reads branch name, calls MCP activation tools, lists available doc-types, and updates CLAUDE.md with available documentation types.

2. **`/cdocs:create-type`** - Creates new custom doc-types with dedicated skills and schemas. Interviews the user about the new type (name, fields, trigger phrases, classification hints), uses Sequential Thinking MCP for schema design reasoning, generates schema files and dedicated capture skills, and registers the type in config.

3. **`/cdocs:capture-select`** - Presents a unified selection interface when 2+ capture skills trigger simultaneously. Provides a multi-select dialog allowing users to choose which doc-types to capture when ambiguous trigger phrase matches occur.

The file also documents the **Multi-Trigger Conflict Resolution** mechanism, explaining how the distributed capture pattern (where each skill has independent triggers) can result in multiple skills matching the same conversation, and how `/cdocs:capture-select` resolves these conflicts.

### Structural Relationships

- **Parent**: `spec/skills.md` - The main Skills & Commands Specification that provides an overview of all skills and explains the distributed capture pattern architecture
- **Siblings**:
  - `spec/skills/capture-skills.md` - Skills that create documentation for specific doc-types
  - `spec/skills/query-skills.md` - Skills that search and retrieve documentation
  - `spec/skills/utility-skills.md` - Skills for management operations (delete, promote, todo, worktree, research)
  - `spec/skills/skill-patterns.md` - Common implementation patterns for skills
- **Referenced Research**: Multiple research documents are referenced for background on YAML frontmatter, hooks, git detection, schema formats, and MCP integration

---

## spec/skills/utility-skills.md

### What This File Covers

The `utility-skills.md` specification defines five management and maintenance skills for the compounding documentation system:

1. **`/cdocs:delete`** - Removes compounding docs data from the database by project, branch, or path. Uses a dry-run workflow for safety, requiring user confirmation before deletion.

2. **`/cdocs:promote`** - Changes a document's visibility level (standard, important, or critical). Updates both the file's YAML frontmatter and the database record. Only works on local docs, not external ones.

3. **`/cdocs:todo`** - File-based todo tracking where the filesystem is the database. Uses a filename convention (`{id}-{status}-{priority}-{description}.md`) to encode state, making todos git-trackable and human-readable. Adapted from the original compound-engineering-plugin.

4. **`/cdocs:worktree`** - Manages Git worktrees for parallel development across branches. Integrates with compound docs via the `/cdocs:activate` skill to handle worktree context detection. Uses only git CLI commands (no MCP integration).

5. **`/cdocs:research`** - Orchestrates research agents with knowledge-base context. Uses Sequential Thinking MCP for research planning, finding synthesis, and capture routing. Coordinates multiple specialized agents (best-practices-researcher, framework-docs-researcher, git-history-analyzer, repo-research-analyst).

### Structural Relationships

- **Parent**: `spec/skills.md` - The main skills specification that categorizes all skills into four groups (Capture, Query, Meta, Utility) and explains the distributed capture pattern architecture.

- **Siblings** (other skill category specifications):
  - `spec/skills/capture-skills.md` - Skills for creating documentation (problem, insight, codebase, tool, style)
  - `spec/skills/query-skills.md` - Skills for searching and retrieving documentation
  - `spec/skills/meta-skills.md` - Skills that operate on the skill system itself (activate, create-type, capture-select)
  - `spec/skills/skill-patterns.md` - Common implementation patterns for skills

- **Grandparent**: `spec/SPEC.md` - The root specification document

- **Cross-references**: References research documents for origin context:
  - `research/compound-engineering-paradigm-research.md` - Original skill designs
  - `research/sequential-thinking-mcp-verification.md` - MCP configuration details
