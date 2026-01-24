# Claude Code Skills YAML Frontmatter Research Report

**Research Date:** January 22, 2026
**Sources:** Official Anthropic documentation, Claude Code docs, GitHub repositories

---

## Table of Contents

1. [Skills Overview](#1-skills-overview)
2. [Skill File Locations](#2-skill-file-locations)
3. [YAML Frontmatter Structure](#3-yaml-frontmatter-structure)
4. [Core Frontmatter Fields](#4-core-frontmatter-fields)
5. [Tool Configuration Fields](#5-tool-configuration-fields)
6. [Execution Control Fields](#6-execution-control-fields)
7. [Trigger Configuration](#7-trigger-configuration)
8. [Agent-Specific Fields](#8-agent-specific-fields)
9. [Hook Integration](#9-hook-integration)
10. [Dependencies and Requirements](#10-dependencies-and-requirements)
11. [Metadata Fields](#11-metadata-fields)
12. [Complete Examples](#12-complete-examples)
13. [Validation and Best Practices](#13-validation-and-best-practices)

---

## 1. Skills Overview

### What Are Claude Code Skills?

Skills are folders of instructions, scripts, and resources that Claude loads dynamically to improve performance on specialized tasks. They teach Claude how to complete specific tasks in a repeatable way, whether that's creating documents with company brand guidelines, analyzing data using organization-specific workflows, or automating personal tasks.

Claude Code skills follow the **Agent Skills open standard**, which works across multiple AI tools. Claude Code extends the standard with additional features like invocation control, subagent execution, and dynamic context injection.

### How Skills Extend Claude Code Functionality

Skills provide:
- **Portable expertise** that can be shared across projects and teams
- **Progressive disclosure** - only loading full content when relevant (approximately 100 tokens for metadata, under 5k tokens for full instructions)
- **Automatic invocation** when Claude determines the skill matches the current task
- **Tool restrictions** to create specialized, safe execution environments
- **Subagent execution** for isolated processing

### Skill File Format

Every skill needs a `SKILL.md` file with two parts:
1. **YAML frontmatter** (between `---` markers) - tells Claude when to use the skill
2. **Markdown content** - instructions Claude follows when the skill is invoked

The frontmatter configures HOW the skill runs (permissions, model, metadata), while the markdown content tells Claude WHAT to do.

### Skill Discovery and Loading

At startup, the name and description from all Skills' YAML frontmatter are loaded into the system prompt. When Claude encounters a task:

1. Claude scans available skill descriptions to find relevant ones
2. If a skill's description field matches the task context, Claude loads the full skill instructions
3. Multiple skills can be loaded and composed together automatically
4. Bundled resources only load as needed

---

## 2. Skill File Locations

### User-Level Skills Directory

**Location:** `~/.claude/skills/`

Personal skills are available across all your projects. Structure:
```
~/.claude/skills/
└── my-skill/
    └── SKILL.md
```

### Project-Level Skills Directory

**Location:** `.claude/skills/` (in project root)

Project-specific skills with highest priority. Structure:
```
.claude/skills/
└── project-skill/
    └── SKILL.md
```

### Plugin Skills Directories

Plugins can include skills in their structure:
```
plugin-name/
├── .claude-plugin/
│   └── plugin.json
├── skills/
│   └── plugin-skill/
│       └── SKILL.md
└── README.md
```

### Legacy Commands Directory

Files in `.claude/commands/` still work and support the same frontmatter. A file at `.claude/commands/review.md` and a skill at `.claude/skills/review/SKILL.md` both create `/review` and work the same way.

### Search Order and Precedence

1. **Project-level** (`.claude/skills/`) - Highest priority
2. **User-level** (`~/.claude/skills/`) - Available globally
3. **Plugin skills** - Loaded from installed plugins

Project-level commands take precedence over user-level commands, allowing project-specific overrides of user-level configurations.

---

## 3. YAML Frontmatter Structure

### Basic Structure

```yaml
---
name: skill-name
description: Brief description of what this skill does and when to use it
---

# Skill Instructions

Markdown content with detailed instructions...
```

### Syntax Requirements

- Frontmatter must start with `---` on line 1 (no blank lines before)
- End with `---` before markdown content
- Use spaces for indentation (not tabs)
- Field values containing special characters should be quoted

### Important Formatting Notes

**Critical Warning:** If you format the SKILL.md with prettier (with `proseWrap: true`) and the description becomes wrapped across multiple lines, the skill will stop being discovered. This is a known footgun with no error message.

---

## 4. Core Frontmatter Fields

### `name` (Required)

The skill identifier that becomes the `/slash-command`.

**Validation Rules:**
- Maximum 64 characters
- Lowercase letters, numbers, and hyphens only
- No XML tags
- No reserved words
- Recommended: Use gerund form (verb + -ing) for clarity

**Example:**
```yaml
name: code-review
```

### `description` (Required)

Helps Claude decide when to load the skill automatically. This is the primary triggering mechanism.

**Validation Rules:**
- Maximum 1024 characters
- Non-empty
- No XML tags
- Write in third person
- Include both what the skill does AND when to use it

**Best Practice:** Include all "when to use" information in the description, not in the body. The body is only loaded after triggering, so "When to Use This Skill" sections in the body are not helpful to Claude.

**Example:**
```yaml
description: Reviews code changes for security vulnerabilities, style violations, and maintainability issues. Use when committing code, creating pull requests, or when explicitly asked to review code.
```

### `version` (Optional)

For tracking skill versions.

**Example:**
```yaml
version: "1.0.0"
```

### `license` (Optional)

License information for the skill.

**Example:**
```yaml
license: MIT
```

---

## 5. Tool Configuration Fields

### `allowed-tools` (Optional)

Restricts which tools Claude can use when the skill is active. Creates a whitelist of permitted tools.

**Format Options:**
- Comma-separated string: `allowed-tools: Read, Grep, Glob`
- YAML-style list:
  ```yaml
  allowed-tools:
    - Read
    - Grep
    - Glob
  ```

**Pattern Support:**
```yaml
allowed-tools: Bash(gh:*)  # Allow only gh commands in Bash
```

**Example - Read-Only Skill:**
```yaml
---
name: safe-reader
description: Read files without making changes
allowed-tools: Read, Grep, Glob
---
```

**Important Limitation:** The `allowed-tools` frontmatter field is only supported when using Claude Code CLI directly. It does not apply when using Skills through the SDK.

### Tool Whitelist Approach

Claude Code uses a whitelist approach for tool restrictions. There is no documented `disallowed-tools` field for skills (though subagents have `disallowedTools`).

---

## 6. Execution Control Fields

### `disable-model-invocation` (Optional)

When `true`, only the user can invoke the skill via slash command. Claude cannot auto-invoke it.

**Use Cases:**
- Workflows with side effects
- Timing-sensitive operations
- Commands like `/commit`, `/deploy`, `/send-slack-message`

**Example:**
```yaml
disable-model-invocation: true
```

### `user-invocable` (Optional)

When `false`, only Claude can invoke the skill. The skill won't appear in the slash command menu.

**Use Cases:**
- Background knowledge that isn't actionable as a command
- Context skills (e.g., `legacy-system-context`)

**Example:**
```yaml
user-invocable: false
```

### `model` (Optional)

Specify which model to use for the skill.

**Values:**
- `sonnet` - Use Claude Sonnet
- `opus` - Use Claude Opus (for complex tasks)
- `haiku` - Use Claude Haiku (faster, cheaper)
- `inherit` - Use the same model as the main conversation (default)

**Example:**
```yaml
model: opus
```

### `context` (Optional)

Controls execution context isolation.

**Value:** `fork`

When set to `fork`, the skill runs in a forked sub-agent context:
- Runs in isolation from your main conversation
- The skill content becomes the prompt that drives the subagent
- No access to conversation history

**Example:**
```yaml
context: fork
```

**Note:** `context: fork` only makes sense for skills with explicit instructions. If your skill contains only guidelines without a task, the subagent receives the guidelines but no actionable prompt.

### `agent` (Optional)

Specifies the agent type when `context: fork` is used.

**Requires:** `context: fork` to be set

**Example:**
```yaml
context: fork
agent: Explore
```

### `mode` (Optional)

Boolean that categorizes a skill as a "mode command" that modifies Claude's behavior or context.

When `true`:
- Appears in a special "Mode Commands" section at the top of the skills list
- Useful for skills like `debug-mode`, `expert-mode`, `review-mode`

**Example:**
```yaml
mode: true
```

---

## 7. Trigger Configuration

### Description-Based Triggering

Skills activate automatically when their description matches the task context. Unlike slash commands (which run when YOU invoke them), skills run when CLAUDE decides they're relevant.

### Effective Trigger Patterns

Structure descriptions with a **WHEN + WHEN NOT** pattern for better auto-invocation accuracy:

```yaml
description: >
  Stakeholder context for Project X when discussing product features,
  UX research, or stakeholder interviews. Auto-invoke when user mentions
  Project X, product lead, or UX research. Do NOT load for general
  stakeholder discussions unrelated to Project X.
```

### Controlling Invocation

| Field | Effect |
|-------|--------|
| `disable-model-invocation: true` | Only user can invoke (via slash command) |
| `user-invocable: false` | Only Claude can invoke (no slash command) |
| Neither set | Both user and Claude can invoke |

### Slash Command Triggers

The `name` field automatically creates a slash command:
- `name: code-review` creates `/code-review`
- `name: deploy-staging` creates `/deploy-staging`

---

## 8. Agent-Specific Fields

### Subagent vs Skill Differences

| Aspect | Skills | Subagents |
|--------|--------|-----------|
| Location | `.claude/skills/` | `.claude/agents/` |
| File | `SKILL.md` in directory | `*.md` file |
| Execution | Main conversation or forked | Always independent context |
| Purpose | Expertise and instructions | Delegated task execution |

### Subagent YAML Frontmatter Fields

Subagents (stored in `.claude/agents/`) support additional fields:

```yaml
---
name: code-reviewer
description: Expert code review specialist
tools: Read, Grep, Glob, Bash
disallowedTools: Write, Edit
model: sonnet
permissionMode: default
skills: skill1, skill2
---
```

**Subagent-Specific Fields:**

| Field | Description |
|-------|-------------|
| `tools` | Allowlist of tools (inherits all if omitted) |
| `disallowedTools` | Denylist of tools |
| `permissionMode` | `default`, `acceptEdits`, `bypassPermissions`, or `plan` |
| `skills` | Skills to auto-load for the subagent |

### Skills Field for Subagents

You can declare skills to auto-load for subagents:

```yaml
---
name: research-agent
description: Research specialist
skills:
  - web-search
  - document-analysis
---
```

---

## 9. Hook Integration

### Hooks in Skill Frontmatter

Skills can define lifecycle-scoped hooks that only run when that skill is active.

**Supported Hook Events:**
- `PreToolUse` - Before a tool executes
- `PostToolUse` - After a tool executes
- `Stop` - When skill execution stops

### Hook Configuration Structure

```yaml
---
name: secure-operations
description: Perform operations with security checks
hooks:
  PreToolUse:
    - matcher: "Bash"
      hooks:
        - type: command
          command: "./scripts/security-check.sh"
  PostToolUse:
    - matcher: "Edit|Write"
      hooks:
        - type: command
          command: "./scripts/run-linter.sh"
  Stop:
    - matcher: "*"
      hooks:
        - type: prompt
          prompt: "Verify checklist complete"
---
```

### Hook Behavior Details

- Matchers apply to tool-based hooks (`PreToolUse`, `PostToolUse`)
- For lifecycle hooks like `Stop`, matchers are ignored
- Hooks are automatically cleaned up when the skill finishes

### PreToolUse Hook Control

PreToolUse hooks can return:
- `"allow"` - Bypasses the permission system
- `"deny"` - Prevents tool call execution
- `"ask"` - Asks user to confirm in UI

### Input Modification

Starting in v2.0.10, PreToolUse hooks can modify tool inputs before execution, enabling:
- Transparent sandboxing
- Automatic security enforcement
- Team convention adherence

### Skills-Only Option

For skills, there's an additional hook option:
- `once: true` - Run the hook only once per session

---

## 10. Dependencies and Requirements

### Package Installation

Claude can install packages from standard repositories (Python PyPI, JavaScript npm) when loading skills. However, all dependencies must be pre-installed for API Skills.

### MCP Server Integration

Skills and MCP servers work together:
- **MCP handles connectivity** - secure, standardized access to external systems
- **Skills handle expertise** - domain knowledge and workflow logic

A single skill can orchestrate multiple MCP servers, while a single MCP server can support dozens of different skills.

### Plugin Manifest for Dependencies

When packaging skills in plugins, the `plugin.json` manifest references components:

```json
{
  "name": "plugin-name",
  "version": "1.2.0",
  "skills": "./skills/",
  "mcpServers": "./mcp-config.json"
}
```

### Skill Directory Structure with Dependencies

```
my-skill/
├── SKILL.md           # Required: Instructions with frontmatter
├── scripts/           # Optional: Helper scripts
│   └── helper.py
├── resources/         # Optional: Supporting files
│   └── template.json
├── references/        # Optional: Documentation for context
│   └── api-docs.md
└── assets/            # Optional: Output files (templates, icons)
    └── template.docx
```

---

## 11. Metadata Fields

### Standard Metadata Fields

Based on official documentation, the supported metadata fields are:

| Field | Required | Description |
|-------|----------|-------------|
| `name` | Yes | Skill identifier, becomes slash command |
| `description` | Yes | Triggers auto-invocation, explains purpose |
| `version` | No | Semantic version for tracking |
| `license` | No | License information |
| `mode` | No | Boolean for mode commands |

### Fields NOT in Standard Frontmatter

The following are **NOT** standard Claude Code skill frontmatter fields according to official documentation:
- `author`
- `tags`
- `keywords`
- `category`
- `icon`
- `examples` (examples go in the markdown body, not frontmatter)

### Custom Frontmatter Fields

Custom frontmatter fields beyond the standard ones are stripped before being injected into the model's context, according to issue reports.

---

## 12. Complete Examples

### Minimal Skill Frontmatter

```yaml
---
name: quick-review
description: Performs quick code review on staged changes
---

Review the staged changes and provide feedback on:
1. Code quality
2. Potential bugs
3. Style consistency
```

### Full-Featured Skill Frontmatter

```yaml
---
name: secure-deploy
description: >
  Deploys application to staging/production environments with security checks.
  Use when user requests deployment or mentions going live. Do NOT use for
  local development or testing deployments.
version: "2.1.0"
license: MIT
disable-model-invocation: true
model: opus
allowed-tools: Bash, Read, Grep
hooks:
  PreToolUse:
    - matcher: "Bash"
      hooks:
        - type: command
          command: "./scripts/pre-deploy-check.sh"
  Stop:
    - matcher: "*"
      hooks:
        - type: prompt
          prompt: "Verify deployment checklist is complete"
---

# Secure Deployment Skill

## Pre-Deployment Checklist
1. Verify all tests pass
2. Check security scan results
3. Confirm environment variables

## Deployment Steps
...
```

### Context Fork Skill Example

```yaml
---
name: codebase-explorer
description: >
  Deep exploration of unfamiliar codebases. Use when user asks to understand
  how a system works or to map out architecture.
context: fork
agent: Explore
allowed-tools: Read, Grep, Glob
---

# Codebase Explorer

Systematically explore and document the codebase structure:

1. Map directory structure
2. Identify key entry points
3. Document component relationships
4. Create architecture diagram
```

### Subagent Frontmatter Example

```yaml
---
name: test-runner
description: >
  Executes test suites and reports results. Use PROACTIVELY after code
  changes to verify functionality.
tools: Bash, Read, Grep
model: sonnet
permissionMode: acceptEdits
skills:
  - code-coverage
  - test-reporting
---

# Test Runner Agent

You are a specialized test execution agent. Your responsibilities:

1. Run appropriate test suites based on changed files
2. Report test results clearly
3. Suggest fixes for failing tests
4. Track code coverage changes
```

### Real-World Example: PR Summary Skill

```yaml
---
name: pr-summary
description: Summarize changes in a pull request
context: fork
agent: Explore
allowed-tools: Bash(gh:*)
---

Analyze the current PR and provide:
1. Summary of changes
2. Files modified
3. Potential impact areas
4. Suggested reviewers
```

### Real-World Example: Safe Reader Skill

```yaml
---
name: safe-reader
description: Read and analyze files without making changes
allowed-tools: Read, Grep, Glob
---

You are in read-only mode. Explore the codebase to answer questions
but do not modify any files.
```

---

## 13. Validation and Best Practices

### Frontmatter Validation Rules

| Rule | Requirement |
|------|-------------|
| Name length | Maximum 64 characters |
| Name format | Lowercase letters, numbers, hyphens only |
| Description length | Maximum 1024 characters |
| Description content | Non-empty, no XML tags |
| File location | Must be named `SKILL.md` in skill directory |
| Frontmatter delimiters | Must start with `---` on line 1 |
| Indentation | Use spaces, not tabs |

### Common Mistakes to Avoid

1. **Multi-line description wrapping** - Using prettier with `proseWrap: true` breaks skill discovery
2. **Over 500 lines in SKILL.md** - Split into reference files
3. **Vague descriptions** - Add specific triggers and use cases
4. **No examples** - Add input/output pairs in markdown body
5. **Deep file nesting** - Keep references one level deep
6. **"When to use" in body** - Put all triggering info in description field
7. **Inconsistent point of view** - Write descriptions in third person

### Best Practices for Skill Authors

#### Description Writing

```yaml
# Good - Specific with clear triggers
description: >
  Analyzes React components for accessibility issues. Use when creating
  new components, reviewing UI code, or when user mentions a11y/accessibility.
  Do NOT use for backend code or API development.

# Bad - Too vague
description: Helps with code
```

#### Progressive Disclosure

Structure skills with progressive disclosure:
1. **Frontmatter** - Minimal metadata (name, description)
2. **SKILL.md body** - Core instructions (under 500 lines)
3. **Reference files** - Detailed documentation loaded as needed
4. **Assets** - Templates, scripts loaded when executing

#### File Organization

- Use forward slashes: `reference/guide.md`
- Name files descriptively: `form_validation_rules.md` not `doc2.md`
- Organize directories by domain or feature
- Keep references one level deep from SKILL.md

#### Plan-Validate-Execute Pattern

For complex tasks, implement the plan-validate-execute pattern:
1. Have Claude create a plan in structured format
2. Validate the plan with a script
3. Execute only after validation passes

#### Iterating with Claude

- Ask Claude to capture successful approaches into skills
- If Claude goes off track, ask for self-reflection
- Refine descriptions based on invocation accuracy

---

## Summary: Complete Frontmatter Field Reference

### Skills (SKILL.md)

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `name` | Yes | string | Skill identifier (max 64 chars, lowercase/numbers/hyphens) |
| `description` | Yes | string | Trigger description (max 1024 chars) |
| `version` | No | string | Semantic version |
| `license` | No | string | License information |
| `mode` | No | boolean | Mode command categorization |
| `disable-model-invocation` | No | boolean | Prevent Claude auto-invocation |
| `user-invocable` | No | boolean | Show in slash command menu |
| `model` | No | string | `sonnet`, `opus`, `haiku`, or `inherit` |
| `context` | No | string | `fork` for isolated execution |
| `agent` | No | string | Agent type when context is fork |
| `allowed-tools` | No | string/list | Tool whitelist (CLI only) |
| `hooks` | No | object | PreToolUse, PostToolUse, Stop hooks |

### Subagents (*.md in .claude/agents/)

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `name` | Yes | string | Agent identifier |
| `description` | Yes | string | When to delegate to this agent |
| `tools` | No | string/list | Tool allowlist |
| `disallowedTools` | No | string/list | Tool denylist |
| `model` | No | string | `sonnet`, `opus`, `haiku`, or `inherit` |
| `permissionMode` | No | string | `default`, `acceptEdits`, `bypassPermissions`, `plan` |
| `skills` | No | list | Skills to auto-load |
| `hooks` | No | object | Agent-scoped hooks |

---

## Sources

### Official Documentation
- [Claude Code Skills Documentation](https://code.claude.com/docs/en/skills)
- [Claude Code Sub-agents Documentation](https://code.claude.com/docs/en/sub-agents)
- [Claude Code Hooks Reference](https://code.claude.com/docs/en/hooks)
- [Claude Code Plugins Reference](https://code.claude.com/docs/en/plugins-reference)
- [Skill Authoring Best Practices](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/best-practices)

### GitHub Repositories
- [anthropics/skills](https://github.com/anthropics/skills) - Official skills repository
- [anthropics/claude-code](https://github.com/anthropics/claude-code) - Claude Code repository
- [anthropics/claude-plugins-official](https://github.com/anthropics/claude-plugins-official) - Official plugins

### Anthropic Engineering Blog
- [Equipping agents for the real world with Agent Skills](https://www.anthropic.com/engineering/equipping-agents-for-the-real-world-with-agent-skills)
- [Claude Code Best Practices](https://www.anthropic.com/engineering/claude-code-best-practices)

### Community Resources
- [Claude Agent Skills: A First Principles Deep Dive](https://leehanchung.github.io/blogs/2025/10/26/claude-skills-deep-dive/)
- [Inside Claude Code Skills: Structure, prompts, invocation](https://mikhail.io/2025/10/claude-code-skills/)
- [When to Use Claude Code Skills vs Commands vs Agents](https://danielmiessler.com/blog/when-to-use-skills-vs-commands-vs-agents)
