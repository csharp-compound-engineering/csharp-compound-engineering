# Skills & Commands Specification

> **Status**: [DRAFT]
> **Parent**: [SPEC.md](../SPEC.md)

---

## Overview

All skills use the `/cdocs:` prefix. Skills are Claude Code SKILL.md files that guide the agent through capturing or retrieving compounding documentation.

---

## Architectural Difference from Original Plugin

> **Reference**: [compound-engineering-plugin](https://github.com/anthropics/compound-engineering-plugin)

> **Background**: For comprehensive analysis of the original compound-engineering paradigm including the four pillars (Plan, Delegate, Assess, Compound), workflow commands, agents, skills, and knowledge layer architecture. See [Compound Engineering Paradigm Research](../research/compound-engineering-paradigm-research.md).

The original `compound-engineering-plugin` uses a **centralized capture pattern**:
- `/workflows:compound` is a single entry point that auto-invokes on trigger phrases
- It classifies content and routes to the appropriate doc-type handler internally
- Doc-type handlers are internal to the compound-docs skill, not standalone skills

This C# implementation uses a **distributed capture pattern**:
- Each doc-type skill (`/cdocs:problem`, `/cdocs:insight`, etc.) has its **own auto-invoke triggers**
- No centralized router - each skill independently detects its relevant trigger phrases
- Custom doc-types (created via `/cdocs:create-type`) follow the same pattern with their own triggers

**Rationale for Distributed Pattern**:
1. **Consistency**: Built-in and custom doc-types behave identically
2. **Precision**: Each skill has domain-specific triggers without competing heuristics
3. **Simplicity**: No classification logic layer - direct trigger-to-skill mapping
4. **Extensibility**: Adding new doc-types doesn't require modifying a central router

**Trade-off**: Multiple skills may trigger on the same conversation. This is handled by a meta-skill:
- When 2+ capture skills trigger simultaneously, the `/cdocs:capture-select` meta-skill auto-invokes
- User sees a multi-select dialog listing all triggered doc-types
- User selects which ones to capture (can select multiple or none)
- If only 1 skill triggers, it proceeds normally (no meta-skill involvement)

See [Multi-Trigger Conflict Resolution](./skills/meta-skills.md#multi-trigger-conflict-resolution) for details.

---

## Skill Naming Convention

| Pattern | Example | Purpose |
|---------|---------|---------|
| `/cdocs:{doc-type}` | `/cdocs:problem` | Create doc of specific type |
| `/cdocs:activate` | `/cdocs:activate` | Activate project (auto-invoked) |
| `/cdocs:query` | `/cdocs:query` | RAG query against docs |
| `/cdocs:search` | `/cdocs:search` | Semantic search |
| `/cdocs:promote` | `/cdocs:promote` | Change document visibility level |
| `/cdocs:research` | `/cdocs:research` | Orchestrate research agents with knowledge context |
| `/cdocs:create-type` | `/cdocs:create-type` | Meta-skill: create new doc-type |
| `/cdocs:capture-select` | `/cdocs:capture-select` | Meta-skill: multi-trigger conflict resolution (auto-invoked only) |
| `/cdocs:todo` | `/cdocs:todo` | File-based todo tracking |
| `/cdocs:worktree` | `/cdocs:worktree` | Git worktree management |

---

## Built-in Skills

**Note on Auto-Invoke Mechanism**: Claude Code's skill system supports auto-invocation via SKILL.md frontmatter. Each skill declares `auto-invoke:` with either:
- `trigger: project-entry` - invoked when entering a project directory (e.g., `/cdocs:activate`)
- `trigger: conversation-pattern` with `patterns:` - invoked when LLM detects matching patterns in conversation

The LLM performs two-stage classification: (1) trigger phrase matching casts a wide net, (2) classification hints provide semantic validation. Skills only auto-invoke when both stages align - trigger phrases alone are insufficient. See [doc-types.md](./doc-types.md) for the full two-stage classification model.

> **Background**: For hooks-based approaches to skill auto-invocation including SessionStart patterns, UserPromptSubmit handlers, and forced evaluation techniques that can improve skill activation reliability to ~80-84%. See [Claude Code Hooks for Skill Auto-Invocation](../research/claude-code-hooks-skill-invocation.md).

> **Background**: For comprehensive details on Claude Code's skill system including YAML frontmatter fields, hook integration, MCP tool coordination, and best practices, see [Claude Code Skills Research](../research/claude-code-skills-research.md).

**Note on Indexing**: Doc-creation skills (problem, insight, codebase, tool, style) write markdown files to disk. The file watcher service automatically indexes new/modified files to the vector database. Skills do not need to call `index_document` explicitly unless manual re-indexing is required.

**Note on External MCP Servers**: Skills reference external MCP servers (Sequential Thinking, Context7, Microsoft Docs) in their "MCP Integration" sections. **Skills assume these servers are available** - the SessionStart hook has already verified their configuration. Skills should NOT include defensive checks for MCP availability. See [Core Principle #6](../SPEC.md#6-external-mcp-dependencies-check-and-warn-never-install).

> **Background**: For MCP server configuration patterns, transport types (HTTP, stdio, SSE), configuration file locations, and plugin architecture details. See [Claude Code Plugin Architecture Research](../research/claude-code-plugin-architecture-research.md).

Skills are organized into four categories:

### Capture Skills

Skills that detect knowledge patterns in conversations and create structured documentation.

| Skill | Purpose |
|-------|---------|
| `/cdocs:problem` | Capture solved problems (bugs, errors, fixes) |
| `/cdocs:insight` | Capture product/project insights |
| `/cdocs:codebase` | Capture architectural knowledge |
| `/cdocs:tool` | Capture tool/library knowledge |
| `/cdocs:style` | Capture coding style/preferences |

See **[Capture Skills](./skills/capture-skills.md)** for full documentation including auto-invoke triggers, MCP integration, and schema fields.

### Query Skills

Skills that search and retrieve captured documentation.

> **Background**: For RAG implementation details including Semantic Kernel integration with Claude, vector store configurations (PostgreSQL/pgvector, Qdrant), embedding options (OpenAI, Ollama), and streaming response patterns. See [Semantic Kernel + Claude RAG Integration Research](../research/semantic-kernel-claude-rag-integration-research.md).

| Skill | Purpose |
|-------|---------|
| `/cdocs:query` | RAG query against local docs |
| `/cdocs:search` | Semantic search for specific documents |
| `/cdocs:search-external` | Search external project docs (read-only) |
| `/cdocs:query-external` | RAG query against external docs |

See **[Query Skills](./skills/query-skills.md)** for full documentation including decision matrices and behavior details.

### Meta Skills

Skills that operate on the skill system itself.

| Skill | Purpose |
|-------|---------|
| `/cdocs:activate` | Activate compounding docs for current project |
| `/cdocs:create-type` | Create new custom doc-type with dedicated skill |
| `/cdocs:capture-select` | Handle multi-trigger conflict resolution |

See **[Meta Skills](./skills/meta-skills.md)** for full documentation including conflict resolution flow and custom type generation.

### Utility Skills

Skills that provide management and maintenance capabilities.

| Skill | Purpose |
|-------|---------|
| `/cdocs:delete` | Delete docs by project, branch, or path |
| `/cdocs:promote` | Change document visibility level |
| `/cdocs:todo` | File-based todo tracking |
| `/cdocs:worktree` | Git worktree management |
| `/cdocs:research` | Orchestrate research agents with knowledge context |

See **[Utility Skills](./skills/utility-skills.md)** for full documentation including MCP tool parameters and workflow details.

---

## Implementation Patterns

Common patterns for skill implementation (file structure, context gathering, schema validation, auto-invocation) are documented in the skill patterns sub-topic. Load only when implementing skills. See [spec/skills/skill-patterns.md](./skills/skill-patterns.md).

---

## Excluded Skills

1. **`/cdocs:list` skill**: Explicitly excluded - could consume excessive tokens for large doc sets. Use `/cdocs:search` with specific queries instead.
2. **Per-project auto-invocation**: Custom doc-type skills are placed in `.claude/skills/` (project scope), allowing per-project customization of triggers.

---

## Open Questions

1. How to handle skill versioning when schemas change?
