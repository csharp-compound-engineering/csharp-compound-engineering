# Claude Code Skills Research

A comprehensive guide to Claude Code skill structure, YAML frontmatter, auto-invoke mechanisms, best practices, hooks integration, and MCP tool usage.

---

## Table of Contents

1. [Overview](#overview)
2. [SKILL.md File Structure](#skillmd-file-structure)
3. [YAML Frontmatter Reference](#yaml-frontmatter-reference)
4. [Auto-Invoke Mechanisms](#auto-invoke-mechanisms)
5. [Skill Discovery](#skill-discovery)
6. [Skill Hooks](#skill-hooks)
7. [MCP Tool Integration](#mcp-tool-integration)
8. [Project vs User Scope](#project-vs-user-scope)
9. [Best Practices](#best-practices)
10. [Common Patterns](#common-patterns)
11. [Troubleshooting](#troubleshooting)
12. [Sources](#sources)

---

## Overview

Claude Code skills are modular, self-contained packages that extend Claude's capabilities by providing specialized workflows, tool integrations, domain expertise, and bundled resources. Skills follow the [Agent Skills](https://agentskills.io) open standard.

### Key Characteristics

- **Filesystem-based**: Skills are directories with a `SKILL.md` entrypoint
- **Progressive disclosure**: Metadata loads at startup, full content loads on-demand
- **Model-driven selection**: Claude uses descriptions to decide when to apply skills
- **Extensible**: Support scripts, references, templates, and other assets

---

## SKILL.md File Structure

Every skill requires a `SKILL.md` file with two parts:

1. **YAML frontmatter** (between `---` markers) - metadata for discovery
2. **Markdown body** - instructions Claude follows when invoked

### Basic Directory Structure

```
skill-name/
├── SKILL.md           # Main instructions (required)
├── template.md        # Template for Claude to fill in (optional)
├── examples/          # Example outputs (optional)
│   └── sample.md
├── scripts/           # Executable scripts (optional)
│   └── helper.py
├── references/        # Reference documentation (optional)
│   └── api-docs.md
└── assets/            # Output files, templates (optional)
    └── logo.png
```

### Bundled Resource Types

| Type | Purpose | Examples |
|------|---------|----------|
| `scripts/` | Executable code for deterministic tasks | `rotate_pdf.py`, `validate.sh` |
| `references/` | Documentation for Claude to consult | `finance.md`, `api_docs.md` |
| `assets/` | Output files, not loaded into context | `logo.png`, `template.pptx` |

### What NOT to Include

- README.md, INSTALLATION_GUIDE.md, CHANGELOG.md
- Auxiliary documentation unrelated to AI execution

---

## YAML Frontmatter Reference

### Required Fields

```yaml
---
name: my-skill-name
description: What this skill does and when to use it
---
```

| Field | Requirements | Notes |
|-------|-------------|-------|
| `name` | Max 64 chars, lowercase letters/numbers/hyphens only | Becomes the `/slash-command` |
| `description` | Max 1024 chars, non-empty | Critical for skill discovery |

**Important**: The description should be written in **third person** and include both what the skill does AND when to use it.

### Complete Frontmatter Reference

```yaml
---
# Required/Recommended
name: my-skill
description: |
  What this skill does. Use when [specific triggers].
  Processes X, generates Y, analyzes Z.

# Invocation Control
disable-model-invocation: true   # Only user can invoke via /name
user-invocable: false            # Only Claude can invoke (hidden from / menu)

# Tool Restrictions
allowed-tools: Read, Grep, Glob  # Limit available tools

# Model Override
model: claude-sonnet-4-20250514  # Override default model

# Subagent Execution
context: fork                    # Run in isolated subagent context
agent: Explore                   # Agent type: Explore, Plan, general-purpose

# Argument Hint
argument-hint: "[issue-number]"  # Shown during autocomplete

# Skill-Scoped Hooks
hooks:
  PreToolUse:
    - matcher: "Write|Edit"
      hooks:
        - type: command
          command: "./scripts/validate.sh"
  PostToolUse:
    - matcher: "Edit"
      hooks:
        - type: prompt
          prompt: "Check formatting"
  Stop:
    - matcher: "*"
      hooks:
        - type: command
          command: "./scripts/cleanup.sh"
---
```

### Field Descriptions

| Field | Default | Description |
|-------|---------|-------------|
| `name` | Directory name | Skill identifier and slash command |
| `description` | First paragraph | Discovery text (max 1024 chars) |
| `argument-hint` | None | Autocomplete hint, e.g., `[filename]` |
| `disable-model-invocation` | `false` | Prevent Claude from auto-invoking |
| `user-invocable` | `true` | Show in `/` menu |
| `allowed-tools` | All tools | Restrict available tools |
| `model` | Current model | Override model for this skill |
| `context` | Inline | Set to `fork` for isolated execution |
| `agent` | `general-purpose` | Agent type when using `context: fork` |
| `hooks` | None | Skill-scoped lifecycle hooks |

### String Substitution Variables

| Variable | Description |
|----------|-------------|
| `$ARGUMENTS` | Arguments passed when invoking the skill |
| `${CLAUDE_SESSION_ID}` | Current session ID for logging/correlation |

**Example:**
```yaml
---
name: fix-issue
description: Fix a GitHub issue by number
---
Fix GitHub issue $ARGUMENTS following our coding standards.
```

When invoked as `/fix-issue 123`, Claude receives "Fix GitHub issue 123..."

---

## Auto-Invoke Mechanisms

### How Claude Selects Skills

Claude uses **no algorithmic routing or intent classification**. Instead:

1. All skill descriptions are formatted into the Skill tool's prompt
2. Claude's language model decides which skill to invoke based on context
3. Descriptions are the primary discovery mechanism

### Invocation Patterns

| Frontmatter | User Can Invoke | Claude Can Invoke | Loaded Into Context |
|-------------|-----------------|-------------------|---------------------|
| (default) | Yes | Yes | Description always, full skill on invoke |
| `disable-model-invocation: true` | Yes | No | Description NOT in context |
| `user-invocable: false` | No | Yes | Description always in context |

### Writing Effective Descriptions

**Good example:**
```yaml
description: |
  Extract text and tables from PDF files, fill forms, merge documents.
  Use when working with PDF files or when the user mentions PDFs, forms,
  or document extraction.
```

**Bad examples:**
```yaml
description: Helps with documents       # Too vague
description: Processes data             # Too generic
description: I can help you with PDFs   # Wrong person (should be third person)
```

### Auto-Invoke Trigger Patterns (Community Patterns)

Some users add explicit triggers in descriptions:
```yaml
description: |
  Drafts professional emails and messages.
  Auto-invoke when: drafting emails, Slack messages, internal updates.
  Do NOT load for: unrelated discussions.
```

### Improving Reliability

A "forced eval hook" pattern achieves ~84% auto-activation success:
- Create a commitment mechanism where Claude evaluates each skill explicitly
- State YES/NO before proceeding with implementation

---

## Skill Discovery

### Discovery Locations (Priority Order)

| Location | Path | Scope |
|----------|------|-------|
| Enterprise | Managed settings | All organization users |
| Personal | `~/.claude/skills/<name>/SKILL.md` | All your projects |
| Project | `.claude/skills/<name>/SKILL.md` | This project only |
| Plugin | `<plugin>/skills/<name>/SKILL.md` | Where plugin enabled |

**Priority**: Enterprise > Personal > Project (same-name skills)

### Automatic Nested Discovery

When editing files in subdirectories, Claude Code discovers skills from nested `.claude/skills/` directories:

```
project/
├── .claude/skills/           # Project skills
├── packages/
│   └── frontend/
│       └── .claude/skills/   # Package-specific skills (auto-discovered)
```

### Discovery Scanning

Claude Code scans:
- User settings: `~/.config/claude/skills/`
- Project settings: `.claude/skills/`
- Plugin-provided skills
- Built-in skills

**Current Limitation**: Only top-level directories in `~/.claude/skills/` are discovered. Nested skill suites (e.g., `~/.claude/skills/my-suite/skill-a/`) require manual symlinks.

### Listing Skills

```bash
# Project skills
ls .claude/skills/*/SKILL.md

# Personal skills
ls ~/.claude/skills/*/SKILL.md
```

---

## Skill Hooks

Skills can define lifecycle hooks directly in YAML frontmatter. Hooks run only during that skill's execution.

### Hook Events

| Event | When | Use Case |
|-------|------|----------|
| `PreToolUse` | Before tool execution | Validation, security checks |
| `PostToolUse` | After tool completion | Formatting, logging, feedback |
| `Stop` | When agent finishes | Cleanup, verification |
| `SessionStart` | Session begins | Initialization (skills only, not agents) |

### Hook Configuration Syntax

```yaml
---
name: secure-editor
description: Edit files with validation
hooks:
  PreToolUse:
    - matcher: "Write|Edit"
      hooks:
        - type: command
          command: "./scripts/file-protector.sh"
  PostToolUse:
    - matcher: "Edit"
      hooks:
        - type: command
          command: "prettier --write $FILE"
  Stop:
    - matcher: "*"
      hooks:
        - type: prompt
          prompt: "Verify all changes complete"
---
```

### Hook Types

| Type | Description |
|------|-------------|
| `command` | Run shell command |
| `prompt` | Inject prompt to Claude |

### The `once` Option

```yaml
hooks:
  SessionStart:
    - matcher: "startup"
      hooks:
        - type: command
          command: "./scripts/load-env.sh"
          once: true  # Only runs once per session
```

### PreToolUse Input Modification

Starting in v2.0.10, PreToolUse hooks can modify tool inputs:
- Intercept tool calls
- Modify JSON input
- Let execution proceed with corrected parameters

---

## MCP Tool Integration

### Referencing MCP Tools

Use fully qualified tool names in skills:

```markdown
Use the BigQuery:bigquery_schema tool to retrieve table schemas.
Use the GitHub:create_issue tool to create issues.
```

Format: `ServerName:tool_name`

### Pre-Allowing MCP Tools

```yaml
---
name: asana-manager
description: Manage Asana tasks
allowed-tools:
  - "mcp__plugin_asana_asana__asana_create_task"
  - "mcp__plugin_asana_asana__asana_search_tasks"
---
```

**Best Practice**: Pre-allow specific tools, not wildcards, for security.

### YAML List Format

```yaml
allowed-tools:
  - Read
  - Grep
  - Glob
  - "mcp__plugin_github_github__create_pr"
```

### SDK Limitation

**Important**: The `allowed-tools` frontmatter is only supported in Claude Code CLI. When using the Agent SDK, tool access must be controlled through `allowedTools` in query configuration.

---

## Project vs User Scope

### Personal Skills (All Projects)

Location: `~/.claude/skills/<skill-name>/SKILL.md`

```bash
mkdir -p ~/.claude/skills/my-skill
# Create SKILL.md
```

### Project Skills (Single Project)

Location: `.claude/skills/<skill-name>/SKILL.md`

```bash
mkdir -p .claude/skills/my-skill
# Create SKILL.md
# Commit to version control
```

### Distribution Methods

| Method | Location | Audience |
|--------|----------|----------|
| Project | `.claude/skills/` + version control | Project contributors |
| Plugin | `skills/` in plugin directory | Plugin users |
| Managed | Enterprise settings | Organization |

---

## Best Practices

### 1. Keep It Concise

- SKILL.md body under **500 lines**
- Challenge each piece of information
- Claude is already very smart - only add what it doesn't know

**Good (50 tokens):**
```markdown
## Extract PDF text
Use pdfplumber for text extraction:
```python
import pdfplumber
with pdfplumber.open("file.pdf") as pdf:
    text = pdf.pages[0].extract_text()
```
```

**Bad (150 tokens):**
```markdown
## Extract PDF text
PDF (Portable Document Format) files are a common file format...
[excessive explanation]
```

### 2. Description is Critical

- Include WHAT the skill does
- Include WHEN to use it
- Write in **third person**
- Max 1024 characters

### 3. Progressive Disclosure

Three-level loading:
1. **Metadata** (~100 tokens): Name + description always loaded
2. **SKILL.md body** (<5k tokens): Loaded when triggered
3. **Bundled resources**: Loaded as needed

### 4. Reference File Organization

**One level deep** - avoid nested references:

```markdown
# SKILL.md

**Basic usage**: [instructions here]
**Advanced**: See [advanced.md](advanced.md)
**API Reference**: See [reference.md](reference.md)
```

### 5. Naming Conventions

Use **gerund form** (verb + -ing):
- `processing-pdfs`
- `analyzing-spreadsheets`
- `managing-databases`

Avoid:
- Vague names: `helper`, `utils`
- Reserved words: `anthropic-*`, `claude-*`

### 6. Set Appropriate Freedom

| Freedom Level | When to Use | Example |
|---------------|-------------|---------|
| High (text) | Multiple valid approaches | Code review guidelines |
| Medium (pseudocode) | Preferred pattern with variation | Report generation |
| Low (exact scripts) | Fragile/error-prone operations | Database migrations |

### 7. Use Workflows for Complex Tasks

```markdown
## Form filling workflow

Copy this checklist:

```
- [ ] Step 1: Analyze the form
- [ ] Step 2: Create field mapping
- [ ] Step 3: Validate mapping
- [ ] Step 4: Fill the form
- [ ] Step 5: Verify output
```

**Step 1: Analyze the form**
Run: `python scripts/analyze_form.py input.pdf`
...
```

### 8. Implement Feedback Loops

```markdown
## Editing process

1. Make edits
2. **Validate immediately**: `python scripts/validate.py`
3. If validation fails:
   - Review error
   - Fix issues
   - Validate again
4. **Only proceed when validation passes**
```

---

## Common Patterns

### Pattern 1: Reference Content

```yaml
---
name: api-conventions
description: API design patterns for this codebase
---

When writing API endpoints:
- Use RESTful naming conventions
- Return consistent error formats
- Include request validation
```

### Pattern 2: Task Workflow

```yaml
---
name: deploy
description: Deploy the application to production
context: fork
disable-model-invocation: true
---

Deploy the application:
1. Run the test suite
2. Build the application
3. Push to the deployment target
```

### Pattern 3: Dynamic Context Injection

```yaml
---
name: pr-summary
description: Summarize changes in a pull request
context: fork
agent: Explore
---

## Pull request context
- PR diff: !`gh pr diff`
- PR comments: !`gh pr view --comments`

## Your task
Summarize this pull request...
```

The `!`command`` syntax runs shell commands before sending to Claude.

### Pattern 4: Read-Only Mode

```yaml
---
name: safe-reader
description: Read files without making changes
allowed-tools: Read, Grep, Glob
---

Explore the codebase without modifying files.
```

### Pattern 5: Domain-Specific Organization

```
bigquery-skill/
├── SKILL.md (overview and navigation)
└── reference/
    ├── finance.md
    ├── sales.md
    └── product.md
```

```markdown
# SKILL.md

## Available datasets

**Finance**: See [reference/finance.md](reference/finance.md)
**Sales**: See [reference/sales.md](reference/sales.md)
```

### Pattern 6: Subagent Execution

```yaml
---
name: deep-research
description: Research a topic thoroughly
context: fork
agent: Explore
---

Research $ARGUMENTS thoroughly:

1. Find relevant files using Glob and Grep
2. Read and analyze the code
3. Summarize findings with specific file references
```

**Note**: `context: fork` runs in isolation WITHOUT conversation history.

---

## Troubleshooting

### Skill Not Triggering

1. Check description includes keywords users naturally say
2. Verify skill appears in "What skills are available?"
3. Rephrase request to match description
4. Invoke directly with `/skill-name`

### Skill Triggers Too Often

1. Make description more specific
2. Add `disable-model-invocation: true`

### Claude Doesn't See All Skills

Skill descriptions may exceed character budget (default 15,000 chars).

Fix: Set `SLASH_COMMAND_TOOL_CHAR_BUDGET` environment variable.

### `context: fork` Not Working

**Known Issue**: When invoked via Skill tool, `context: fork` and `agent:` fields may be ignored. The skill runs in main context instead.

### Deeply Nested Skills Not Discovered

**Limitation**: Only top-level directories in `~/.claude/skills/` are scanned.

**Workaround**: Create symlinks at top level for nested skills.

---

## Sources

### Official Documentation

- [Claude Code Skills Documentation](https://code.claude.com/docs/en/skills) - Primary reference
- [Skill Authoring Best Practices](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/best-practices) - Anthropic API docs
- [Agent Skills Overview](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/overview) - Conceptual overview
- [Claude Code Hooks Guide](https://code.claude.com/docs/en/hooks-guide) - Hooks documentation

### Community Resources

- [Claude Agent Skills: A First Principles Deep Dive](https://leehanchung.github.io/blogs/2025/10/26/claude-skills-deep-dive/) - Technical analysis
- [Inside Claude Code Skills](https://mikhail.io/2025/10/claude-code-skills/) - Structure and invocation
- [Anthropic Skills Repository](https://github.com/anthropics/skills) - Official skill examples
- [Awesome Claude Skills](https://github.com/travisvn/awesome-claude-skills) - Community curated list
- [Claude Code Skills Don't Auto-Activate](https://scottspence.com/posts/claude-code-skills-dont-auto-activate) - Workarounds

### GitHub Issues

- [Recursive skill discovery](https://github.com/anthropics/claude-code/issues/18192) - Feature request
- [context: fork behavior](https://github.com/anthropics/claude-code/issues/20492) - Documentation issue
- [allowed-tools SDK limitation](https://github.com/anthropics/claude-code/issues/18737) - SDK vs CLI differences

### Blog Posts

- [Equipping agents for the real world with Agent Skills](https://www.anthropic.com/engineering/equipping-agents-for-the-real-world-with-agent-skills) - Anthropic engineering blog
- [Claude Code customization guide](https://alexop.dev/posts/claude-code-customization-guide-claudemd-skills-subagents/) - Comprehensive guide

---

*Research compiled: January 2026*
*Claude Code version context: 2.1.x+*
