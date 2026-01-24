# CSharp Compound Docs Plugin Specification

> **Plugin Name**: `csharp-compounding-docs`
> **Marketplace Name**: CSharp Compound Docs

> **Version**: 0.1.0-draft
> **Status**: Requirements Discovery
> **Last Updated**: 2025-01-24

---

## Meta-Documentation

This document serves as the **entry point** for the plugin specification. It provides high-level overview and links to detailed sub-topic specifications.

### Document Structure Rules

1. **Line Limit**: This file (`SPEC.md`) must never exceed **500 lines**. If it approaches this limit, content must be refactored into sub-topic files in `./spec/`.

2. **Sub-Topic Files**: Detailed specifications live in `./spec/*.md`. Each sub-topic file is also limited to **500 lines**.

3. **Recursive Decomposition**: If a sub-topic file exceeds 500 lines:
   - Create a folder named after the sub-topic (e.g., `./spec/mcp-server/`)
   - Place sub-sub-topic files within it
   - Refactor the original file to be an index linking to sub-sub-topics
   - Apply this rule recursively as needed

4. **Linking Convention**: All references to sub-topics use relative markdown links (e.g., `[MCP Server](./spec/mcp-server.md)`).

5. **Link Preface Requirement**: Before any link to another markdown document, include a **brief preface** (1-2 sentences) describing what the linked document contains. Include a note that the document should only be loaded if necessary for the current context. This optimizes context window usage.

   **Example**:
   > The MCP server architecture, including activation protocol and tool definitions, is detailed in the linked document. Load only if MCP implementation details are needed for the current task. See [spec/mcp-server.md](./spec/mcp-server.md).

6. **Status Tracking**: Each sub-topic file should include a status indicator:
   - `[DRAFT]` - Initial requirements, subject to change
   - `[REVIEW]` - Ready for stakeholder review
   - `[APPROVED]` - Requirements locked
   - `[IMPLEMENTED]` - Code complete

---

## Executive Summary

A Claude Code plugin that implements the "compound-engineering" paradigm for C#/.NET projects. The plugin captures and retrieves institutional knowledge through:

- **Disk-based storage** of markdown documentation in `./csharp-compounding-docs/`
- **RAG and semantic search** retrieval via a bundled MCP server
- **PostgreSQL + pgvector** for vector storage
- **Ollama** for embeddings and RAG generation
- **Docker Compose** for shared infrastructure

Unlike the original Ruby/Python-focused compound-engineering plugin, this implementation:
- Centers on C#/.NET ecosystem
- Expands doc-types beyond problems/solutions to include product insights, codebase knowledge, tools/libraries, and coding styles
- Uses semantic retrieval instead of file-based grep search
- Provides a meta-skill for creating custom doc-type skills

---

## Complete Workflow Example

### Day 1: Capturing Knowledge

**Context**: Developer solves a database connection pool exhaustion bug after 2 hours of debugging.

**Conversation**:
```
User: "Finally fixed it! The issue was that our background job wasn't disposing
       SqlConnection objects properly. Added using statements and pool exhaustion stopped."
```

**System Behavior**:
1. `/cdocs:problem` skill auto-triggers (matches: "fixed", "issue was")
2. Skill gathers context from conversation:
   - Problem type: `bug`
   - Symptoms: `["connection pool exhaustion", "background job failures"]`
   - Root cause: `"SqlConnection not disposed in background jobs"`
   - Solution: `"Added using statements to ensure disposal"`
3. Skill shows confirmation:
   ```
   📝 Capture as Problem Documentation?

   Title: Database Connection Pool Exhaustion
   Type: bug
   Severity: high

   [Capture] [Edit First] [Skip]
   ```
4. User clicks [Capture]
5. File created: `./csharp-compounding-docs/problems/db-pool-exhaustion-20250123.md`
6. File watcher detects new file (within 500ms)
7. MCP server generates embedding via Ollama
8. Document upserted to PostgreSQL vector store
9. Skill confirms: "✓ Documentation captured and indexed"

**Generated File**:
```yaml
---
title: Database Connection Pool Exhaustion
date: 2025-01-23
summary: Background jobs not disposing SqlConnection caused pool exhaustion
doc_type: problem
problem_type: bug
severity: high
symptoms:
  - connection pool exhaustion
  - background job failures
root_cause: SqlConnection not disposed in background jobs
solution: Added using statements to ensure disposal
promotion_level: standard
---

# Database Connection Pool Exhaustion

## Symptoms
- Connection pool exhaustion errors in logs
- Background jobs failing intermittently

## Root Cause
The `ProcessDataJob` class was creating `SqlConnection` objects without
`using` statements, causing connections to leak...

## Solution
Added `using` statements to all database operations in background jobs...
```

### Day 30: Retrieving Knowledge

**Context**: Different developer encounters similar symptoms.

**Conversation**:
```
User: "/cdocs:query We're seeing connection pool errors in our job scheduler"
```

**System Behavior**:
1. `/cdocs:query` skill invoked
2. Calls MCP `rag_query` tool with query
3. MCP server:
   - Generates embedding for query
   - Searches vector store (relevance threshold 0.7)
   - Finds `db-pool-exhaustion-20250123.md` (score: 0.89)
   - Loads document, parses for linked docs
   - Sends context to Ollama for RAG synthesis
4. Returns synthesized answer with sources

**User Sees**:
```
Based on documented knowledge:

A similar issue was resolved on 2025-01-23. The root cause was
SqlConnection objects not being disposed in background jobs.

**Solution**: Ensure all SqlConnection usages are wrapped in `using`
statements to guarantee disposal.

📚 Sources:
- [Database Connection Pool Exhaustion](./csharp-compounding-docs/problems/db-pool-exhaustion-20250123.md)

Would you like me to load the full document?
```

---

## Sub-Topic Index

| Topic | File | Status |
|-------|------|--------|
| Doc-Types Architecture | [spec/doc-types.md](./spec/doc-types.md) | [DRAFT] |
| MCP Server | [spec/mcp-server.md](./spec/mcp-server.md) | [DRAFT] |
| ↳ Database Schema | [spec/mcp-server/database-schema.md](./spec/mcp-server/database-schema.md) | [DRAFT] |
| Infrastructure (Docker) | [spec/infrastructure.md](./spec/infrastructure.md) | [DRAFT] |
| Skills & Commands | [spec/skills.md](./spec/skills.md) | [DRAFT] |
| ↳ Skill Patterns | [spec/skills/skill-patterns.md](./spec/skills/skill-patterns.md) | [DRAFT] |
| Agents | [spec/agents.md](./spec/agents.md) | [DRAFT] |
| Plugin Marketplace | [spec/marketplace.md](./spec/marketplace.md) | [DRAFT] |
| Configuration | [spec/configuration.md](./spec/configuration.md) | [DRAFT] |
| Testing | [spec/testing.md](./spec/testing.md) | [DRAFT] |
| Observability | [spec/observability.md](./spec/observability.md) | [DRAFT] |
| Research Index | [spec/research-index.md](./spec/research-index.md) | [DRAFT] |

---

## Core Principles

### 1. Capture Only Significant Insights

Following the original compound-engineering philosophy:
- Only "big" insights should be captured
- Skip mundane, trivial information
- **Exception**: Common misconceptions or easy mistakes warrant documentation even if seemingly trivial

### 2. File System as Source of Truth

- All documentation lives in `./csharp-compounding-docs/{doc-type}/`
- Vector database is a derived index, rebuilt from files
- File watchers maintain sync automatically
- Git operations (checkout, pull, merge) naturally trigger re-sync

### 3. Semantic Retrieval First

- RAG is the preferred retrieval method
- Semantic search used for highly specific queries
- Agent decides retrieval mode via decision matrix in skill
- Cross-references resolved by parsing markdown links

### 4. Shared Infrastructure

- Single Docker Compose stack serves all MCP server instances
- Containers run in `~/.claude/.csharp-compounding-docs/`
- **Single PostgreSQL schema** with tenant isolation via compound keys (project + branch + path_hash)
- Supports git worktrees with concurrent Claude Code sessions on different branches

### 5. Multi-Tenant Isolation

- Documents keyed by: `project_name`, `branch_name`, `path_hash`
- Path hash derived from absolute repo path (supports worktrees)
- Separate cleanup console app manages orphaned data

### 6. External MCP Dependencies: Check and Warn, Never Install

**Principle**: The plugin does NOT install, manage, or bundle external MCP servers.

- **SessionStart hook** checks if required MCP servers (Context7, Microsoft Docs, Sequential Thinking) are configured
- **Warning displayed** if any are missing, with configuration instructions
- **No installation attempted** - users are responsible for their own MCP server setup
- **Skills assume availability** - no defensive checks within skills; if hook passed, MCPs are available

This separation of concerns ensures:
- Users control their own MCP ecosystem
- Plugin never modifies user configuration
- Clear responsibility boundary between plugin and user

See [marketplace.md - External MCP Server Prerequisites](./spec/marketplace.md#external-mcp-server-prerequisites) for implementation details.

---

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Claude Code Agent                         │
├─────────────────────────────────────────────────────────────────┤
│  Skills (17 total)                                               │
│  ├── /cdocs:activate        (auto-invoke on project entry)      │
│  ├── /cdocs:problem         (problems & solutions)              │
│  ├── /cdocs:insight         (product/project insights)          │
│  ├── /cdocs:codebase        (codebase knowledge)                │
│  ├── /cdocs:tool            (tools & libraries)                 │
│  ├── /cdocs:style           (coding styles & preferences)       │
│  ├── /cdocs:query           (RAG query for answers)             │
│  ├── /cdocs:search          (semantic search for docs)          │
│  ├── /cdocs:search-external (search external project docs)      │
│  ├── /cdocs:query-external  (RAG query external docs)           │
│  ├── /cdocs:delete          (delete docs by project/branch)     │
│  ├── /cdocs:promote         (change doc visibility level)       │
│  ├── /cdocs:research        (orchestrate research agents)       │
│  ├── /cdocs:create-type     (meta-skill: create new doc-type)   │
│  ├── /cdocs:capture-select  (multi-trigger conflict resolution) │
│  ├── /cdocs:todo            (file-based todo tracking)          │
│  └── /cdocs:worktree        (git worktree management)           │
├─────────────────────────────────────────────────────────────────┤
│  Agents (4 total - Research category only)                       │
│  ├── best-practices-researcher                                   │
│  ├── framework-docs-researcher                                   │
│  ├── git-history-analyzer                                        │
│  └── repo-research-analyst                                       │
└───────────────────────────┬─────────────────────────────────────┘
                            │ stdio
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                    MCP Server (.NET Generic Host)                │
│  ├── Activation Tool (reads ./csharp-compounding-docs/config.json)     │
│  ├── RAG Query Tool (synthesize + return sources)               │
│  ├── Semantic Search Tool (return ranked documents)             │
│  └── File Watcher Service (sync docs ↔ vector DB)               │
├─────────────────────────────────────────────────────────────────┤
│  Microsoft.SemanticKernel / Microsoft.Extensions.AI             │
└───────────────────────────┬─────────────────────────────────────┘
                            │
        ┌───────────────────┴───────────────────┐
        ▼                                       ▼
┌───────────────────┐                 ┌───────────────────┐
│   PostgreSQL      │                 │     Ollama        │
│   + pgvector      │                 │   (embeddings +   │
│   (vector store)  │                 │    generation)    │
└───────────────────┘                 └───────────────────┘
        └───────────────────┬───────────────────┘
                            │
                    Docker Compose
              (~/.claude/.csharp-compounding-docs/)
```

---

## Technology Stack

| Component | Technology | Notes |
|-----------|------------|-------|
| MCP Server | C# / .NET Generic Host | stdio transport |
| AI Abstraction | Microsoft.SemanticKernel and/or Microsoft.Extensions.AI | Per research |
| Vector Database | PostgreSQL + pgvector | Single schema, tenant isolation via keys |
| Embeddings & RAG | Ollama | Local, GPU-optional (mxbai-embed-large, mistral) |
| In-Memory Graph | QuikGraph | Link tracking, circular reference detection |
| Containerization | Docker Compose | Shared infrastructure |
| Scripting | PowerShell (cross-platform) | `#!/usr/bin/env pwsh` |
| Markdown Parsing | Markdig (per research) | Link resolution |
| Schema Validation | JsonSchema.Net + Yaml2JsonNode | JSON Schema Draft 2020-12 |

---

## Project Repository Structure

```
csharp-compound-engineering/
├── SPEC.md                          # This file
├── CLAUDE.md                        # Claude Code instructions
├── spec/                            # Detailed specifications
│   ├── doc-types.md
│   ├── mcp-server.md
│   ├── infrastructure.md
│   ├── skills.md
│   ├── marketplace.md
│   ├── configuration.md
│   └── testing.md
├── research/                        # Pre-research documents
│   └── *.md
├── src/                             # Source code (future)
│   ├── CompoundDocs.McpServer/      # MCP server project
│   ├── CompoundDocs.Cleanup/        # Orphan data cleanup console app
│   └── CompoundDocs.Common/         # Shared library
├── plugins/                         # Claude Code plugin
│   └── csharp-compounding-docs/
│       ├── .claude-plugin/
│       │   ├── plugin.json
│       │   └── hooks.json
│       ├── .mcp.json                # MCP server configuration
│       ├── skills/
│       ├── agents/
│       │   └── research/            # 4 research agents
│       ├── hooks/
│       │   └── check-dependencies.ps1
│       ├── CLAUDE.md
│       └── README.md
├── scripts/                         # PowerShell scripts
│   └── start-infrastructure.ps1
├── docker/                          # Docker configs (templates)
│   └── docker-compose.yml
├── marketplace/                     # GitHub Pages marketplace
│   └── index.html
├── tests/                           # Test projects
│   ├── CompoundDocs.Tests/          # Unit tests
│   ├── CompoundDocs.IntegrationTests/  # Integration tests
│   ├── CompoundDocs.E2ETests/       # End-to-end tests
│   └── Directory.Build.props        # Shared coverage config
└── .github/
    └── workflows/
        └── test.yml                 # CI/CD pipeline
```

---

## Resolved Decisions

| Decision | Resolution |
|----------|------------|
| Embedding model | `mxbai-embed-large` (1024 dimensions) - **static, not configurable** |
| RAG generation model | `mistral` - configurable |
| Relevance threshold | 0.7 default - configurable per project |
| Max results | 3 (not including linked docs) - configurable |
| Max linked docs | Configurable separately |
| Link depth | Configurable per project |
| Doc size limit | 500 lines max; MCP server chunks larger docs in database only (source files remain intact) |
| Chunking strategy | By markdown headers with backlinks |
| Markdown parser | Markdig - most mature .NET parser, full AST access, native YAML frontmatter support |
| In-memory graph library | QuikGraph - comprehensive algorithm suite, cycle detection via `IsDirectedAcyclicGraph()` |
| Test framework | xUnit 2.9.3 - stable version preferred over v3 preview |
| Assertion library | Shouldly 4.2.1+ - free/MIT license (FluentAssertions requires paid license) |
| Mocking library | Moq 4.20.72+ - industry standard |
| Code coverage | Coverlet (coverlet.msbuild 6.0.4+) - 100% line/branch/method enforcement |
| Test infrastructure | .NET Aspire (Aspire.Hosting.Testing) - container orchestration for integration/E2E |
| MCP E2E testing | ModelContextProtocol client with StdioClientTransport |

## Excluded Components

> Components from the original [compound-engineering-plugin](https://github.com/anthropics/compound-engineering-plugin) intentionally **not** included in this C# implementation.

### Excluded Agents

| Category | Agents | Reason |
|----------|--------|--------|
| Review (8) | `architecture-strategist`, `security-sentinel`, `performance-detective`, `accessibility-advocate`, `testing-tactician`, `documentation-analyst`, `dependency-watchdog`, `code-style-guardian` | Scope reduction - focus on knowledge capture over review automation |
| Design (3) | `design-implementation-reviewer`, `figma-design-sync`, `storybook-verifier` | Not applicable to C#/.NET backend focus |
| Workflow (4) | `bug-reproduction-validator`, `pr-comment-resolver`, `test-failure-analyst`, `release-notes-generator` | Scope reduction - can be added later if needed |

### Excluded Skills

| Skill | Purpose | Reason |
|-------|---------|--------|
| `frontend-design` | Frontend patterns | Not applicable to C#/.NET backend focus |
| `agent-native-architecture` | AI agent design patterns | Out of scope for initial release |
| `dspy-ruby` | Type-safe LLM apps | Ruby-specific |
| `rclone` | File uploads | Utility function, not core to knowledge capture |
| `agent-browser` | Browser automation | Out of scope for initial release |

### Excluded Utility Commands (16+)

| Command | Reason |
|---------|--------|
| `/deepen-plan`, `/plan_review` | Planning utilities - out of scope |
| `/changelog`, `/release-docs`, `/deploy-docs` | Release automation - out of scope |
| `/create-agent-skill`, `/generate_command`, `/heal-skill` | Meta-tooling - out of scope |
| `/report-bug`, `/reproduce-bug`, `/triage` | Bug workflow - out of scope |
| `/resolve_parallel`, `/resolve_pr_parallel`, `/resolve_todo_parallel` | Parallel resolution - out of scope |
| `/test-browser`, `/xcode-test` | Platform-specific testing |
| `/feature-video` | Documentation media - out of scope |
| `/agent-native-audit`, `/lfg` | Specialized utilities |

**Note**: These exclusions are intentional scope decisions. Components can be reconsidered for future releases based on user feedback.

---

## Resolved Questions

> Questions resolved during requirements discovery

| Question | Resolution | See |
|----------|------------|-----|
| Schema validation library | JsonSchema.Net + Yaml2JsonNode | [spec/configuration.md](./spec/configuration.md#schema-validation-libraries) |
| Plugin marketplace design | Nextra (Next.js static site generator) | [spec/marketplace.md](./spec/marketplace.md) |

---

## Change Log

| Date | Version | Changes |
|------|---------|---------|
| 2025-01-24 | 0.1.0-draft | Resolved open questions (schema validation: JsonSchema.Net, marketplace: Nextra) |
| 2025-01-23 | 0.1.0-draft | Finalized component review - documented excluded components |
| 2025-01-23 | 0.1.0-draft | Added Sequential Thinking MCP as user prerequisite (check and warn, not managed by plugin) |
| 2025-01-23 | 0.1.0-draft | Switched to distributed capture pattern (removed `/cdocs:capture`) |
| 2025-01-23 | 0.1.0-draft | Reduced agents to 4 research agents only (supersedes earlier 19-agent specification) |
| 2025-01-23 | 0.1.0-draft | Added `/cdocs:todo`, `/cdocs:worktree` skills; promotion levels; testing specification |
| 2025-01-23 | 0.1.0-draft | Documented external MCP prerequisites (Context7, Microsoft Docs) with check-and-warn hook |
| 2025-01-22 | 0.1.0-draft | Initial requirements discovery |

