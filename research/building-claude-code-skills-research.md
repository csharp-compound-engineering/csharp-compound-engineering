# Building Custom Claude Code Skills: Complete Research Report

**Research Date**: January 2026
**Sources**: Official Anthropic Documentation, GitHub repositories, Claude Platform docs

---

## Table of Contents

1. [Skills Overview](#1-skills-overview)
2. [Skill File Structure](#2-skill-file-structure)
3. [Complete YAML Frontmatter Reference](#3-complete-yaml-frontmatter-reference)
4. [Skill Body Content](#4-skill-body-content)
5. [Skill Locations and Discovery](#5-skill-locations-and-discovery)
6. [Tool Permissions](#6-tool-permissions)
7. [Triggers and Activation](#7-triggers-and-activation)
8. [Skill Development Workflow](#8-skill-development-workflow)
9. [Advanced Features](#9-advanced-features)
10. [Integration with MCP](#10-integration-with-mcp)
11. [Complete Examples](#11-complete-examples)
12. [Best Practices](#12-best-practices)
13. [Distribution](#13-distribution)

---

## 1. Skills Overview

### What Are Claude Code Skills?

Agent Skills are organized folders of instructions, scripts, and resources that agents can discover and load dynamically to perform better at specific tasks. At its simplest, a skill is a directory that contains a SKILL.md file with YAML frontmatter and markdown instructions.

Skills are not separate processes, sub-agents, or external tools - they are **injected instructions** that guide Claude's behavior within the main conversation.

### How Skills Differ from Other Components

| Component | Purpose | Key Characteristics |
|-----------|---------|---------------------|
| **Skills** | Portable expertise and workflows | Reusable across agents, loaded on-demand, contain instructions + optional scripts |
| **MCP Tools** | External integrations | Real-time data access, API connections, file operations |
| **Prompts** | One-time instructions | No persistence, no discoverability |
| **Subagents** | Specialized task execution | Own context window, tool restrictions, independent operation |
| **Commands** | Manual triggers | Slash command invocation only |

### Key Analogy

> "Skills are like recipes, subagents are like specialized coworkers. Both extend Claude Code, but knowing when to use which saves you a ton of headache."

### Use Cases for Custom Skills

- **Workflow Automation**: Git commits, deployments, code reviews
- **Domain Expertise**: API conventions, coding standards, framework patterns
- **Multi-step Processes**: Testing workflows, refactoring patterns
- **Organization Knowledge**: Legacy system context, internal conventions
- **Tool Orchestration**: Combining multiple MCP servers with workflow logic

---

## 2. Skill File Structure

### Basic Structure

```
skill-name/
├── SKILL.md           # Required - Core instructions
├── scripts/           # Optional - Executable code (Python/Bash)
├── references/        # Optional - Documentation for context
└── assets/            # Optional - Templates, icons, binary files
```

### SKILL.md Format

Every SKILL.md file has two parts:

1. **YAML Frontmatter** (between `---` markers) - Metadata that tells Claude when to use the skill
2. **Markdown Body** - Instructions Claude follows when the skill is invoked

```markdown
---
name: my-skill-name
description: A clear description of what this skill does and when to use it
---

# My Skill Name

[Instructions Claude will follow when this skill is active]
```

### File Naming Conventions

- The skill directory name should match the `name` field in frontmatter
- Use lowercase letters, numbers, and hyphens only
- SKILL.md is case-insensitive but conventionally uppercase
- Maximum name length: 64 characters

### Bundled Resources

| Directory | Purpose | Example Contents |
|-----------|---------|------------------|
| `scripts/` | Executable code Claude runs via Bash tool | `validate.py`, `deploy.sh`, `helper.js` |
| `references/` | Documentation loaded into context as needed | API specs, style guides, examples |
| `assets/` | Files used in output | Templates, fonts, icons |

---

## 3. Complete YAML Frontmatter Reference

### Required Fields

| Field | Description | Constraints |
|-------|-------------|-------------|
| `name` | Skill identifier, becomes the /slash-command | Max 64 chars, lowercase letters/numbers/hyphens only, no XML tags, no reserved words |
| `description` | Human-readable description for discovery | Max 1024 chars, non-empty, no XML tags |

### Optional Invocation Control Fields

| Field | Type | Description |
|-------|------|-------------|
| `disable-model-invocation` | boolean | When `true`, only users can invoke via `/skill-name`. Use for workflows with side effects like `/deploy`, `/commit` |
| `user-invocable` | boolean | When `false`, only Claude can invoke. Use for background knowledge that isn't actionable as a command |
| `mode` | boolean | When `true`, categorizes as a "mode command" that modifies Claude's behavior. Appears in special "Mode Commands" section |

### Tool Restriction Fields

| Field | Type | Description |
|-------|------|-------------|
| `allowed-tools` | string (comma-separated) | Limits which tools Claude can use when skill is active. Example: `Read, Grep, Glob` |

### Context and Agent Fields

| Field | Type | Description |
|-------|------|-------------|
| `context` | string | Set to `fork` to run skill in isolation as a subagent |
| `agent` | string | Specifies which subagent to use. Options: `Explore`, `Plan`, `general-purpose`, or custom agent from `.claude/agents/` |
| `skills` | string (comma-separated) | Skills to auto-load into a subagent |

### Hooks Configuration

| Field | Type | Description |
|-------|------|-------------|
| `hooks` | object | Define PreToolUse, PostToolUse, or Stop hooks scoped to skill lifecycle |

### Metadata Fields

| Field | Type | Description |
|-------|------|-------------|
| `version` | string | Semantic version for tracking (e.g., `1.0.0`) |
| `license` | string | License information |
| `metadata` | object | Additional custom metadata |
| `argument-hint` | string | Hint for expected arguments (e.g., `[pr-number] [priority]`) |

### Officially Allowed Properties (per Anthropic spec)

According to the official Agent Skills specification at [agentskills.io/specification](https://agentskills.io/specification):

- `name` (required)
- `description` (required)
- `license` (optional)
- `allowed-tools` (optional)
- `metadata` (optional)

> **Note**: Claude Code extends the base specification with additional fields like `disable-model-invocation`, `user-invocable`, `context`, `agent`, `mode`, and `hooks`.

---

## 4. Skill Body Content

### Markdown Formatting

The body uses standard Markdown with clear, actionable instructions:

```markdown
---
name: code-review
description: Review code changes with best practices checklist
---

# Code Review

When reviewing code, follow these steps:

## 1. Check for Issues
- Look for security vulnerabilities
- Identify performance bottlenecks
- Check error handling

## 2. Provide Feedback
- Be constructive and specific
- Suggest improvements with examples
- Acknowledge good patterns
```

### Variable Interpolation

Skills support string substitution for dynamic values:

| Variable | Description |
|----------|-------------|
| `$ARGUMENTS` | User input after `/skill-name` |
| `$1`, `$2`, `$3`... | Positional arguments |
| `${CLAUDE_SESSION_ID}` | Current session identifier |

**Example**:
```markdown
---
name: fix-issue
description: Fix a GitHub issue
---

Fix GitHub issue $ARGUMENTS following our coding standards:
1. Read the issue description
2. Identify affected code
3. Implement the fix
4. Write tests
```

### Shell Command Syntax

The `!command` syntax runs shell commands **before** skill content is sent to Claude:

```markdown
---
name: smart-commit
description: Create a git commit with context
allowed-tools: Bash(git add:*), Bash(git status:*), Bash(git commit:*)
---

## Current Context
- Git status: !`git status`
- Current diff: !`git diff HEAD`
- Current branch: !`git branch --show-current`
- Recent commits: !`git log --oneline -10`

Based on the above context, create an appropriate commit message.
```

The command output replaces the placeholder, so Claude receives actual data.

### Content Types

**Reference Content**: Adds knowledge Claude applies to work - conventions, patterns, style guides:
```markdown
---
name: api-standards
description: Our REST API conventions and patterns
user-invocable: false
---

# API Standards

## Endpoint Naming
- Use plural nouns: `/users`, `/orders`
- Nest resources: `/users/{id}/orders`
...
```

**Task Content**: Step-by-step instructions for specific actions:
```markdown
---
name: deploy
description: Deploy application to production
disable-model-invocation: true
---

# Production Deployment

1. Run full test suite
2. Check for pending migrations
3. Create deployment tag
4. Execute deployment script
5. Verify health checks
```

---

## 5. Skill Locations and Discovery

### Storage Locations

| Location | Scope | Description |
|----------|-------|-------------|
| `~/.claude/skills/` | User-level | Personal skills available across all projects |
| `.claude/skills/` | Project-level | Shared with team via git |
| Plugin directories | Plugin | Skills bundled with installed plugins |

### Discovery Process (Progressive Disclosure)

1. **At Startup**: Claude scans all skill directories and loads only `name` and `description` from frontmatter (~30-50 tokens per skill)
2. **During Conversation**: Claude evaluates relevance based on user request and skill descriptions
3. **On Activation**: Full SKILL.md content is loaded into context
4. **As Needed**: Scripts and references are accessed on-demand

This architecture allows hundreds of skills without context penalty.

### Loading Priority

1. Project-level skills (`.claude/skills/`)
2. User-level skills (`~/.claude/skills/`)
3. Plugin skills

### Legacy Command Compatibility

A file at `.claude/commands/review.md` and a skill at `.claude/skills/review/SKILL.md` both create `/review` and work the same way.

---

## 6. Tool Permissions

### Using `allowed-tools`

Restrict which tools Claude can use when a skill is active:

```yaml
---
name: safe-explore
description: Explore codebase without modifications
allowed-tools: Read, Grep, Glob
---
```

### Tool Reference Patterns

| Pattern | Description | Example |
|---------|-------------|---------|
| Simple name | Exact tool match | `Read` |
| Comma-separated | Multiple tools | `Read, Grep, Glob` |
| MCP tool | External tool reference | `mcp__github__list_issues` |
| MCP wildcard | All tools from server | `mcp__claude-code-docs__*` |
| Bash with restriction | Limited Bash commands | `Bash(git add:*), Bash(git status:*)` |

### Built-in Tool Names

Common built-in tools:
- `Read` - Read files
- `Write` - Write files
- `Edit` - Edit files
- `Bash` - Execute shell commands
- `Grep` - Search file contents
- `Glob` - Find files by pattern
- `WebFetch` - Fetch web content
- `WebSearch` - Search the web

### Security Implications

- Skills with restricted `allowed-tools` create "safe modes"
- Use `disable-model-invocation: true` for dangerous operations
- Consider what scripts in `scripts/` can access

---

## 7. Triggers and Activation

### Slash Command Invocation

Users directly invoke skills:
```
/deploy production
/fix-issue 123
/code-review
```

Arguments after the command are available as `$ARGUMENTS` or positional `$1`, `$2`, etc.

### Automatic (Semantic) Activation

Claude automatically triggers skills based on:
1. User request content
2. Skill description matching
3. Task relevance assessment

> "Skills trigger through semantic matching: Claude reads your request, compares it against all available skill descriptions, and activates the right one automatically."

### Controlling Invocation

| Scenario | Configuration |
|----------|---------------|
| User-only invocation | `disable-model-invocation: true` |
| Claude-only invocation | `user-invocable: false` |
| Both can invoke | Default (no special fields) |

### Examples

**User-only (side effects)**:
```yaml
---
name: deploy
description: Deploy to production environment
disable-model-invocation: true
---
```

**Claude-only (background knowledge)**:
```yaml
---
name: legacy-system-context
description: Information about our legacy billing system architecture
user-invocable: false
---
```

---

## 8. Skill Development Workflow

### Creating a New Skill

1. **Choose Location**:
   - `~/.claude/skills/my-skill/` for personal use
   - `.claude/skills/my-skill/` for project/team use

2. **Create Directory Structure**:
   ```bash
   mkdir -p .claude/skills/my-skill
   touch .claude/skills/my-skill/SKILL.md
   ```

3. **Write SKILL.md**:
   ```markdown
   ---
   name: my-skill
   description: What it does and when to use it
   ---

   # Instructions

   Your detailed instructions here...
   ```

4. **Test the Skill**:
   - Restart Claude Code or use `/skill-name` to invoke
   - Iterate based on Claude's behavior

### Using Claude to Create Skills

Claude understands the Skill format natively:
> "Simply ask Claude to create a Skill and it will generate properly structured SKILL.md content with appropriate frontmatter and body content."

### Iterative Refinement

The recommended workflow:
1. **Claude A** generates initial skill
2. **You** provide domain expertise
3. **Claude B** uses skill in real tasks, revealing gaps
4. **Iterate** based on observed behavior

### Debugging Tips

- Check skill discovery with `/help` command
- Verify frontmatter syntax (YAML is whitespace-sensitive)
- Ensure description is clear about when to use
- Keep SKILL.md under 500 lines for optimal performance

---

## 9. Advanced Features

### Forked Context (Subagent Execution)

Run skills in isolation with their own context:

```yaml
---
name: deep-research
description: Research a topic thoroughly
context: fork
agent: Explore
---

Research the topic thoroughly:
1. Search for relevant sources
2. Analyze findings
3. Synthesize conclusions
```

> **Note**: As of late 2025, there's a known issue where `context: fork` and `agent:` fields may be ignored when invoked via the Skill tool.

### Multi-File Skills

For complex skills, split content:

```
complex-skill/
├── SKILL.md                 # Core instructions
├── references/
│   ├── api-spec.md         # Loaded when needed
│   └── examples.md
└── scripts/
    ├── validate.py
    └── generate.sh
```

Reference files from SKILL.md:
```markdown
For API details, see `references/api-spec.md`.
For validation, run `scripts/validate.py`.
```

### Hooks in Skills

Define hooks scoped to skill lifecycle:

```yaml
---
name: safe-edit
description: Edit with automatic linting
hooks:
  PostToolUse:
    - matcher: "Edit|Write"
      hooks:
        - type: command
          command: "./scripts/lint.sh"
---
```

Supported hook events:
- `PreToolUse` - Before tool execution
- `PostToolUse` - After tool completion
- `Stop` - When Claude finishes responding

### Skills with Subagents

Load skills into subagent context:

```yaml
---
name: frontend-reviewer
description: Review frontend code
skills: react-patterns, accessibility-standards
---
```

### Conditional Logic

While skills don't have built-in conditionals, you can:
1. Use shell commands with `!` syntax for dynamic content
2. Write scripts that return different outputs based on conditions
3. Include decision trees in markdown instructions

---

## 10. Integration with MCP

### Skills vs MCP: The Division

> "Model Context Protocol (MCP) connects Claude to third-party tools, and skills teach Claude how to use them well."

| Use MCP When | Use Skills When |
|--------------|-----------------|
| Need real-time data access | Need to define workflows |
| Actions in external systems | Need to teach best practices |
| API integrations | Need to orchestrate multiple tools |
| File operations | Need portable expertise |

### MCP Tool References in Skills

Reference MCP tools using the `mcp__` prefix:

```yaml
---
name: github-workflow
description: GitHub operations with best practices
allowed-tools: mcp__github__*, Read, Write
---
```

### Combining Skills and MCP

Best practices:
- Let MCP handle **connectivity** (data access, API calls)
- Let skills handle **presentation, sequencing, and workflow logic**

Example:
```markdown
---
name: issue-triager
description: Triage GitHub issues with project context
allowed-tools: mcp__github__list_issues, mcp__github__update_issue
---

# Issue Triage Process

1. List open issues using GitHub MCP
2. Categorize by severity and type
3. Assign appropriate labels
4. Update priority in project board
```

### Avoiding Conflicts

> "When combining MCP servers and skills, watch for conflicting instructions. If your MCP server says to return JSON and your skill says to format as markdown tables, Claude has to guess which one is right."

---

## 11. Complete Examples

### Minimal Skill Example

```markdown
---
name: explain-code
description: Explains code with visual diagrams and analogies. Use when explaining how code works, teaching about a codebase, or when the user asks "how does this work?"
---

When explaining code:
1. Start with a high-level overview
2. Break down into logical components
3. Use analogies for complex concepts
4. Provide visual diagrams when helpful
```

### Full-Featured Skill Example

```markdown
---
name: smart-deploy
description: Deploy application with safety checks and rollback capability
disable-model-invocation: true
allowed-tools: Bash, Read
argument-hint: [environment] [--dry-run]
version: 2.1.0
hooks:
  PreToolUse:
    - matcher: "Bash"
      hooks:
        - type: command
          command: "./scripts/pre-deploy-check.sh"
---

# Smart Deployment

## Current State
- Branch: !`git branch --show-current`
- Last commit: !`git log --oneline -1`
- Pending changes: !`git status --short`

## Pre-flight Checks

Before deploying to $1:
1. Ensure all tests pass
2. Verify no uncommitted changes
3. Confirm target environment exists

## Deployment Steps

1. Create deployment tag
2. Build application: `./scripts/build.sh`
3. Run database migrations if needed
4. Deploy to environment
5. Verify health checks
6. Update deployment log

## Rollback Procedure

If deployment fails:
1. Identify failure point
2. Execute rollback script
3. Notify team
4. Document incident
```

### Git Commit Skill with Context

```markdown
---
name: commit
description: Create a git commit with intelligent message generation
disable-model-invocation: true
allowed-tools: Bash(git add:*), Bash(git status:*), Bash(git commit:*), Bash(git diff:*)
argument-hint: [message]
---

## Context
- Current status: !`git status --short`
- Current diff: !`git diff HEAD`
- Branch: !`git branch --show-current`
- Recent commits: !`git log --oneline -5`

## Instructions

1. Analyze the changes shown above
2. Generate a clear, conventional commit message
3. If $ARGUMENTS provided, use as commit message
4. Stage relevant files
5. Create the commit
6. Show result
```

### Read-Only Exploration Skill

```markdown
---
name: explore
description: Explore codebase safely without making changes
allowed-tools: Read, Grep, Glob
---

# Safe Exploration Mode

You are in read-only mode. You can:
- Read any file
- Search file contents with Grep
- Find files by pattern with Glob

You cannot modify any files in this mode.

When exploring:
1. Start with project structure overview
2. Identify key components
3. Trace code paths as requested
4. Document findings clearly
```

---

## 12. Best Practices

### Skill Authoring Guidelines

1. **Keep SKILL.md Under 500 Lines**
   - Split large content into `references/` files
   - Use progressive disclosure

2. **Write Clear Descriptions**
   - Include what the skill does
   - Include when to use it
   - Use third person perspective
   - Be specific about triggers

3. **Use Gerund Form for Names**
   - `code-reviewing` not `code-review`
   - `test-running` not `run-tests`
   - Clearly describes the activity

4. **Restrict Tools Appropriately**
   - Use `allowed-tools` for safety-critical skills
   - Combine with `disable-model-invocation` for dangerous operations

### Security Considerations

1. **Review Script Permissions**
   - Scripts in `scripts/` run with user permissions
   - Validate inputs in scripts
   - Don't store secrets in skill files

2. **Control Invocation**
   - Use `disable-model-invocation: true` for deployment, deletion, or modification skills
   - Review what tools each skill can access

3. **Validate Frontmatter**
   - No XML tags in name or description
   - Don't use reserved words
   - Keep within character limits

### Performance Considerations

1. **Minimize Token Usage**
   - Only essential instructions in SKILL.md
   - Reference files for detailed documentation
   - Scripts output only consumes tokens, not script code

2. **Efficient Shell Commands**
   - Use `!` commands for dynamic context
   - Keep command output concise
   - Filter unnecessary data

### Maintainability Tips

1. **Version Your Skills**
   - Use semantic versioning in `version` field
   - Document changes in skill content

2. **Organize with Directories**
   - Group related skills
   - Consistent naming conventions

3. **Test Iteratively**
   - Observe Claude's actual usage
   - Refine based on behavior

---

## 13. Distribution

### Sharing Skills

**Within a Project**:
- Place in `.claude/skills/`
- Commit to git
- Team members get skills automatically

**Personal Skills**:
- Place in `~/.claude/skills/`
- Available across all your projects
- Not automatically shared

### Plugin Packaging

Skills can be distributed as part of plugins:

```
my-plugin/
├── plugin.json
├── skills/
│   ├── skill-one/
│   │   └── SKILL.md
│   └── skill-two/
│       └── SKILL.md
└── commands/
    └── my-command.md
```

### Plugin Marketplace

1. **Create marketplace.json** listing your plugins
2. **Host on Git** (GitHub, GitLab, Bitbucket)
3. **Users add marketplace**:
   ```
   /plugin marketplace add owner/repo
   ```
4. **Users install plugins**:
   ```
   /plugin install plugin-name@marketplace-name
   ```

### Private Repository Support

Claude Code supports installing from private repositories:
- Set appropriate authentication tokens in environment
- Claude Code uses tokens automatically when authentication required

### Reserved Marketplace Names

The following are reserved for official Anthropic use:
- claude-code-marketplace
- claude-code-plugins
- claude-plugins-official
- anthropic-marketplace
- anthropic-plugins
- agent-skills
- life-sciences

---

## Additional Resources

### Official Documentation

- [Claude Code Skills Documentation](https://code.claude.com/docs/en/skills)
- [Agent Skills Overview](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/overview)
- [Skill Authoring Best Practices](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/best-practices)
- [Agent Skills Specification](https://agentskills.io/specification)

### Official GitHub Repositories

- [anthropics/skills](https://github.com/anthropics/skills) - Official skills repository
- [anthropics/claude-code](https://github.com/anthropics/claude-code) - Claude Code source
- [anthropics/claude-cookbooks](https://github.com/anthropics/claude-cookbooks) - Skills notebooks and examples

### Community Resources

- [SkillsMP](https://skillsmp.com/) - Agent Skills Marketplace
- [awesome-claude-skills](https://github.com/travisvn/awesome-claude-skills) - Curated skill collections
- [obra/superpowers](https://github.com/obra/superpowers) - Battle-tested skills library

---

## Summary

Claude Code Skills provide a powerful, portable way to extend Claude's capabilities with specialized knowledge and workflows. Key points:

1. **Simple Structure**: SKILL.md with YAML frontmatter and markdown body
2. **Progressive Disclosure**: Only loaded when relevant, preserving context
3. **Flexible Control**: Configure who can invoke and what tools are available
4. **Integration Ready**: Works with MCP tools and subagents
5. **Easy Distribution**: Share via git, plugins, or marketplaces

Start with simple skills and iterate based on observed behavior. The skill system is designed to be composable and maintainable at scale.
