# Skill Implementation Patterns

> **Status**: [DRAFT]
> **Parent**: [spec/skills.md](../skills.md)

---

## Overview

This document defines common implementation patterns shared across all doc-creation skills. These patterns ensure consistency and maintainability.

> **Background**: For comprehensive details on Claude Code's skill system including YAML frontmatter fields, hook types, MCP integration, and discovery mechanisms, see [Claude Code Skills Research](../../research/claude-code-skills-research.md).

---

## Skill File Structure

Each skill follows this structure:

```
${CLAUDE_PLUGIN_ROOT}/skills/cdocs-{name}/
├── SKILL.md              # Main skill definition
├── schema.yaml           # Schema for this doc-type (built-in types)
└── references/           # Optional reference materials
    └── examples.md
```

---

## Common Skill Patterns

### Context Gathering

All doc-creation skills share this pattern:

```markdown
### Step 1: Gather Context

Extract from conversation history:
- **Core insight**: What is the key takeaway?
- **Supporting details**: Evidence, code examples, error messages
- **Related context**: Files, modules, versions involved

**BLOCKING**: If critical context missing, ask and WAIT.
```

### Schema Validation

```markdown
### Step 2: Validate Schema

Load `schema.yaml` for this doc-type.
Validate all required fields present and match enum values.

**BLOCK if validation fails** - show specific errors.
```

### File Writing

```markdown
### Step 3: Write Documentation

1. Generate filename: `{sanitized-title}-{YYYYMMDD}.md`
2. Create directories: `mkdir -p ./csharp-compounding-docs/{doc-type}/`
3. Write file with YAML frontmatter + markdown body
```

### Decision Menu

```markdown
### Step 4: Post-Capture Options

✓ Documentation captured

File created: `./csharp-compounding-docs/{doc-type}/{filename}.md`

What's next?
1. Continue workflow
2. Link related docs
3. View documentation
4. Other
```

---

## Auto-Invocation

> **Background**: Hooks cannot directly invoke skills, but can inject context that triggers them. SessionStart and UserPromptSubmit hooks enable automatic skill activation with pattern matching. See [Claude Code Hooks for Skill Auto-Invocation Research](../../research/claude-code-hooks-skill-invocation.md).

### Project Entry Detection

The `/cdocs:activate` skill should auto-invoke when:
- Claude Code opens in a directory with `./csharp-compounding-docs/config.json`
- User switches to a project with compounding docs configured

### Capture Detection

Doc-creation skills may auto-trigger (with confirmation) when detecting:
- "that worked", "fixed it", "problem solved" → `/cdocs:problem`
- "good to know", "learned that" → context-dependent

### Auto-Invoke SKILL.md Frontmatter

> **Background**: Complete YAML frontmatter field reference including validation rules, invocation control, and tool restrictions. See [Claude Code Skills YAML Frontmatter Research](../../research/claude-skills-yaml-frontmatter-research.md).

```yaml
---
name: cdocs:problem
description: Capture solved problems with symptoms, root cause, and solution
allowed-tools:
  - Read  # Access conversation context
preconditions:
  - Project activated via /cdocs:activate
auto-invoke:
  trigger: conversation-pattern
  patterns:
    - "fixed"
    - "it's fixed"
    - "problem solved"
    - "the issue was"
    - "learned that"
    - "turns out"
    - "the issue was"
---
```

---

## Cross-References in Skills

When creating documents, skills should prompt for related docs:

```markdown
### Optional: Link Related Docs

Are there related documents to link?
- Use relative markdown links: `[Title](../problems/related-issue.md)`
- Links will be followed during RAG retrieval
```

### Link Format

```markdown
See also: [Database connection pooling issue](../problems/db-pool-exhaustion-20250115.md)
```

### Resolution Behavior

When the MCP server returns RAG results:
1. Parse the source documents with Markdig
2. Extract all relative markdown links
3. Resolve links to absolute paths
4. Return linked document metadata (path, char count) alongside main results
5. Agent decides whether to load linked documents based on token budget

---

## SKILL.md Template

> **Background**: Comprehensive guide to skill file structure, writing effective descriptions, iterative refinement workflow, and distribution methods. See [Building Custom Claude Code Skills Research](../../research/building-claude-code-skills-research.md).

```yaml
---
name: cdocs:{type}
description: Brief description in third person
allowed-tools:
  - Read
  - Write
  - Bash  # If git operations needed
preconditions:
  - Project activated via /cdocs:activate
  - [Type-specific preconditions]
auto-invoke:
  trigger: conversation-pattern  # or project-entry
  patterns:
    - "trigger phrase 1"
    - "trigger phrase 2"
---

# {Type} Documentation Skill

## Intake

[Describe what input this skill expects]

## Process

### Step 1: Gather Context
[Context gathering instructions]

### Step 2: Validate Schema
[Schema validation instructions]

### Step 3: Write Documentation
[File writing instructions]

### Step 4: Post-Capture Options
[Decision menu]

## Schema Reference

[Link to or inline the schema]

## Examples

[Example captured documents]
```

---

## Open Questions

1. Should auto-invocation be configurable per-project?

## Resolved Questions

1. ~~Should there be a `/cdocs:list` skill to show all captured docs?~~ **Resolved**: Explicitly excluded - use `semantic_search` with broad queries instead. Listing all documents could consume excessive tokens for large document sets. See [skills.md - Excluded Skills](../skills.md#excluded-skills).
2. ~~How to handle skill versioning when schemas change?~~ **Deferred to post-MVP**: No schema migration tooling for MVP. See [configuration.md - Open Questions](../configuration.md#open-questions).

