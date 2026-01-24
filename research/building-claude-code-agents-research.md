# Building Custom Claude Code Agents: Complete Research Report

> **Research Date:** January 22, 2026
> **Sources:** Official Anthropic documentation, Claude Code docs, GitHub repositories

---

## Table of Contents

1. [Agents Overview](#1-agents-overview)
2. [Agent File Structure](#2-agent-file-structure)
3. [Complete YAML Frontmatter Reference for Subagents](#3-complete-yaml-frontmatter-reference-for-subagents)
4. [Agent Skills YAML Frontmatter Reference](#4-agent-skills-yaml-frontmatter-reference)
5. [Agent Instructions and Prompts](#5-agent-instructions-and-prompts)
6. [Agent Locations and Discovery](#6-agent-locations-and-discovery)
7. [Agent Capabilities](#7-agent-capabilities)
8. [Agent Invocation](#8-agent-invocation)
9. [Multi-Agent Systems](#9-multi-agent-systems)
10. [Agent Development Workflow](#10-agent-development-workflow)
11. [Agent State and Memory](#11-agent-state-and-memory)
12. [Integration with MCP](#12-integration-with-mcp)
13. [Complete Examples](#13-complete-examples)
14. [Best Practices](#14-best-practices)
15. [Distribution](#15-distribution)
16. [Sources](#16-sources)

---

## 1. Agents Overview

### What Are Claude Code Agents?

Claude Code includes multiple agent-related concepts that work together to extend Claude's capabilities:

| Concept | Description | Primary Use |
|---------|-------------|-------------|
| **Subagents** | Specialized AI assistants with custom system prompts, tools, and isolated context windows | Task delegation, parallel execution |
| **Skills** | Folders of instructions, scripts, and resources that Claude loads dynamically | Context injection, workflow automation |
| **Plugins** | Packages that bundle skills, subagents, MCP servers, and hooks | Distribution and sharing |

### How Subagents Differ from Skills

| Feature | Subagents | Skills |
|---------|-----------|--------|
| **Context** | Isolated context window | Injected into current context |
| **Execution** | Independent Claude instance | Same Claude instance |
| **Primary Trigger** | Task delegation via `Task` tool | Description matching or slash command |
| **System Prompt** | Custom system prompt | Adds instructions to existing prompt |
| **Tools** | Can restrict/customize tools | Can specify allowed tools |
| **Return Value** | Summarized results to parent | Inline content modification |

### How Agents Differ from MCP Servers

| Feature | Agents/Subagents | MCP Servers |
|---------|------------------|-------------|
| **Purpose** | AI reasoning and task execution | External tool/data integration |
| **Nature** | LLM-based processing | Deterministic services |
| **Context** | Have their own context windows | Stateless tool providers |
| **Protocol** | Native Claude Code feature | Model Context Protocol standard |
| **Use Case** | Complex reasoning tasks | Database queries, API calls |

### Use Cases for Custom Agents

- **Code Review**: Specialized reviewers for security, performance, accessibility
- **Documentation**: Generate and maintain documentation
- **Testing**: Write and run tests, analyze coverage
- **Research**: Deep codebase exploration without context pollution
- **Refactoring**: Large-scale code modifications
- **DevOps**: Deployment automation, infrastructure management

### Agent Architecture

```
Main Claude Code Session
    |
    +-- Built-in Subagents (Explore, Plan, general-purpose)
    |
    +-- Custom Subagents (.claude/agents/)
    |       |
    |       +-- Each has: name, description, tools, model, system prompt
    |       +-- Isolated context window
    |       +-- Returns summarized results
    |
    +-- Skills (.claude/skills/)
    |       |
    |       +-- Auto-loaded based on description matching
    |       +-- Injected into conversation context
    |       +-- Can include scripts and resources
    |
    +-- MCP Servers (configured in settings)
            |
            +-- External tools and data sources
            +-- Available to main session and subagents
```

---

## 2. Agent File Structure

### Subagent File Format

Subagents are Markdown files (`.md`) with YAML frontmatter:

```markdown
---
name: my-agent
description: Description of what this agent does
tools: Read, Glob, Grep
model: sonnet
---

# System Prompt Content

You are a specialized agent that...

## Your Responsibilities

1. First responsibility
2. Second responsibility

## Guidelines

- Follow these rules...
```

### Skill File Format (SKILL.md)

Skills follow the Agent Skills specification:

```markdown
---
name: my-skill
description: Description of what this skill does and when to use it
---

# Skill Instructions

When this skill is invoked, follow these steps...

## Step 1: Analysis
...

## Step 2: Execution
...
```

### Skill Directory Structure

```
skill-name/
├── SKILL.md              # Required: Skill definition
├── scripts/              # Optional: Executable scripts
│   ├── analyze.py
│   └── generate.sh
├── references/           # Optional: Reference documentation
│   └── style-guide.md
└── assets/               # Optional: Templates, resources
    └── template.json
```

### Multi-File Agent Structures

For complex agents, organize supporting files:

```
.claude/
├── agents/
│   └── code-reviewer.md
├── skills/
│   └── review-skill/
│       ├── SKILL.md
│       ├── scripts/
│       │   └── lint-check.sh
│       └── references/
│           └── coding-standards.md
└── settings.json
```

---

## 3. Complete YAML Frontmatter Reference for Subagents

### Required Fields

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Unique identifier. Lowercase letters, numbers, hyphens. Max 64 chars. |
| `description` | string | When Claude should use this agent. Max 1024 chars. This is the primary signal for automatic invocation. |

### Model Configuration

| Field | Type | Values | Default |
|-------|------|--------|---------|
| `model` | string | `sonnet`, `opus`, `haiku`, `inherit` | `inherit` |

- **sonnet**: Claude Sonnet 4 - balanced performance
- **opus**: Claude Opus 4.5 - highest capability
- **haiku**: Claude Haiku 4 - fastest, most economical
- **inherit**: Use same model as parent conversation

### Tool Configuration

| Field | Type | Description |
|-------|------|-------------|
| `tools` | string | Comma-separated allowlist of tools. If omitted, inherits all tools. |
| `disallowedTools` | string | Comma-separated denylist of tools. Takes precedence over tools. |

**Available Tools:**

| Tool | Purpose | Category |
|------|---------|----------|
| `Read` | Read files (text, images, PDFs, notebooks) | Read-only |
| `Write` | Create or overwrite files | Write |
| `Edit` | Exact string replacements in files | Write |
| `MultiEdit` | Batch edits to single file | Write |
| `Glob` | File pattern matching | Read-only |
| `Grep` | Content search with regex | Read-only |
| `Bash` | Execute shell commands | Execute |
| `Task` | Delegate to subagents | Delegation |
| `WebFetch` | Fetch web content | Network |
| `WebSearch` | Search the web | Network |
| `Skill` | Invoke skills | Meta |
| `TodoWrite` | Manage task lists | Utility |
| `NotebookEdit` | Edit Jupyter notebooks | Write |
| MCP tools | Any configured MCP server tools | External |

### Permission Configuration

| Field | Type | Values | Description |
|-------|------|--------|-------------|
| `permissionMode` | string | See below | How subagent handles permission prompts |

**Permission Mode Values:**

| Value | Description |
|-------|-------------|
| `default` | Normal permission prompts |
| `acceptEdits` | Auto-accept file edits |
| `dontAsk` | Skip permission prompts where safe |
| `bypassPermissions` | Skip ALL permission checks (use with caution) |
| `plan` | Planning mode - analyze without executing |
| `ignore` | Ignore permission context |

**Warning:** When parent uses `bypassPermissions`, all subagents inherit this and it cannot be overridden.

### Visual Configuration

| Field | Type | Values | Description |
|-------|------|--------|-------------|
| `color` | string | `red`, `blue`, `green`, `yellow`, `purple`, `orange`, `pink`, `cyan`, `automatic` | Background color in UI |

### Skills Integration

| Field | Type | Description |
|-------|------|-------------|
| `skills` | string | Comma-separated skills to auto-load. Subagents don't inherit parent skills. |

### Hooks Configuration

| Field | Type | Description |
|-------|------|-------------|
| `hooks` | object | Event handlers for subagent lifecycle |

**Supported Hook Events in Frontmatter:**

| Event | Trigger |
|-------|---------|
| `PreToolUse` | Before tool execution |
| `PostToolUse` | After tool completion |
| `Stop` | When subagent finishes |

**Hook Structure:**

```yaml
hooks:
  PreToolUse:
    - matcher: "Bash"
      hooks:
        - type: command
          command: "./scripts/validate.sh"
  PostToolUse:
    - matcher: "Edit|Write"
      hooks:
        - type: command
          command: "./scripts/lint.sh"
  Stop:
    - type: command
      command: "./scripts/cleanup.sh"
```

### Complete Subagent Frontmatter Example

```yaml
---
name: security-reviewer
description: Security code reviewer. Analyze code for vulnerabilities, injection attacks, authentication flaws, and insecure data handling.
model: opus
tools: Read, Glob, Grep, Bash
disallowedTools: Write, Edit, WebFetch
permissionMode: default
color: red
skills: security-checklist
hooks:
  PostToolUse:
    - matcher: "Grep"
      hooks:
        - type: command
          command: "./scripts/log-search.sh"
  Stop:
    - type: command
      command: "./scripts/generate-report.sh"
---
```

---

## 4. Agent Skills YAML Frontmatter Reference

### Required Fields (Agent Skills Specification)

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| `name` | string | Max 64 chars, lowercase, letters/numbers/hyphens only, no XML tags | Skill identifier and slash command |
| `description` | string | Max 1024 chars, non-empty, no XML tags | What skill does and when to use it. Primary trigger signal. |

### Optional Fields

| Field | Type | Description |
|-------|------|-------------|
| `version` | string | Semantic version (e.g., "1.0.0") for tracking |
| `disable-model-invocation` | boolean | `true` prevents Claude from auto-invoking. Only user can invoke via `/skill-name`. |
| `user-invocable` | boolean | `false` means only Claude can invoke (background knowledge). Default: `true` |
| `mode` | boolean | `true` categorizes as "mode command" (e.g., debug-mode). Appears in special section. |
| `allowed-tools` | string | Comma-separated tool allowlist (CLI only, not SDK) |

### Claude Code Extensions to Agent Skills

Claude Code extends the standard specification with additional features:

| Field | Type | Description |
|-------|------|-------------|
| `context` | string | `fork` runs skill in a subagent |
| `agent` | string | Which subagent to use: `Explore`, `Plan`, `general-purpose`, or custom |
| `hooks` | object | PreToolUse, PostToolUse, Stop handlers |

### Dynamic Content Features

**Shell Command Injection:**

Use `!command` syntax to run shell commands and inject output:

```markdown
---
name: pr-summary
description: Summarize a pull request
---

## Current PR Diff
!gh pr diff

## PR Comments
!gh pr view --comments

Analyze the above and provide a summary.
```

**Arguments Placeholder:**

Use `$ARGUMENTS` to inject user-provided arguments:

```markdown
---
name: fix-issue
description: Fix a GitHub issue by number
---

Fix GitHub issue $ARGUMENTS following our coding standards.
```

### Complete Skill Frontmatter Example

```yaml
---
name: deep-research
description: Perform deep codebase research. Use when exploring unfamiliar code, investigating bugs, or understanding architecture.
version: "2.0.0"
disable-model-invocation: false
mode: false
context: fork
agent: Explore
---
```

---

## 5. Agent Instructions and Prompts

### System Prompt Design

The markdown body after frontmatter becomes the system prompt. Key principles:

**Structure:**

```markdown
---
name: agent-name
description: ...
---

# Role Definition
You are a [specific role] specialized in [domain].

## Primary Responsibilities
1. First responsibility
2. Second responsibility

## Capabilities
- What you can do
- Tools you have access to

## Constraints
- What you should NOT do
- Boundaries to respect

## Workflow
1. Step one
2. Step two
3. Step three

## Output Format
Describe expected output structure.

## Examples
Show concrete examples when helpful.
```

### Instruction Writing Best Practices

1. **Be Specific**: Clear, actionable instructions
2. **Define Scope**: What's in/out of scope
3. **Provide Context**: Background the agent needs
4. **Include Examples**: Show expected behavior
5. **Set Constraints**: Explicit limitations
6. **Define Output**: Expected format/structure

### Goal Specification

```markdown
## Goals
1. **Primary Goal**: [Main objective]
2. **Secondary Goals**:
   - [Supporting objective 1]
   - [Supporting objective 2]

## Success Criteria
- [Measurable outcome 1]
- [Measurable outcome 2]
```

### Constraint Definition

```markdown
## Constraints

### Must Do
- Always validate input before processing
- Include tests for any code changes
- Follow project coding standards

### Must Not Do
- Never modify configuration files
- Don't delete files without confirmation
- Avoid changes outside the specified directory

### Boundaries
- Only work within the /src directory
- Maximum 10 files per operation
- Time limit: 5 minutes per task
```

### Persona Development

```markdown
---
name: senior-architect
description: Senior software architect for design decisions
---

# Persona: Senior Software Architect

You are a senior software architect with 15+ years of experience in distributed systems. You:

- Think in terms of scalability, maintainability, and reliability
- Always consider trade-offs before making recommendations
- Communicate clearly with both technical and non-technical stakeholders
- Prioritize simplicity over cleverness
- Value documentation and knowledge sharing

When reviewing designs, you:
1. First understand the requirements fully
2. Identify potential failure modes
3. Consider operational complexity
4. Recommend pragmatic solutions
```

---

## 6. Agent Locations and Discovery

### Storage Locations

| Type | Location | Scope | Priority |
|------|----------|-------|----------|
| Project subagents | `.claude/agents/` | Current project only | Highest |
| User subagents | `~/.claude/agents/` | All projects | Lower |
| Plugin subagents | Plugin's `agents/` directory | When plugin installed | Varies |
| CLI-defined | `--agents '{...}'` flag | Current session | Specified at runtime |
| SDK-defined | `agents` parameter in `query()` | Programmatic | Highest (overrides filesystem) |

| Type | Location | Scope |
|------|----------|-------|
| Project skills | `.claude/skills/` | Current project |
| User skills | `~/.claude/skills/` | All projects |
| Plugin skills | Plugin's `skills/` directory | When plugin installed |

### Discovery Mechanism

**Subagent Discovery:**
1. Claude Code scans agent directories at session start
2. Loads all `.md` files with valid frontmatter
3. Indexes by name and description
4. When name collision occurs, higher-priority location wins

**Skill Discovery:**
1. Claude Code scans skill directories at startup
2. Extracts YAML frontmatter (name, description) from each SKILL.md
3. Pre-loads names and descriptions into system prompt
4. Full skill content only loaded when skill is invoked

**Automatic Invocation:**
- Claude reads the `description` field to determine relevance
- When a task matches a subagent's expertise, Claude delegates via `Task` tool
- When context matches a skill's description, Claude loads the skill content

### Loading and Initialization

**Session Start:**
1. Load settings from `.claude/settings.json` and `~/.claude/settings.json`
2. Discover and index all subagents
3. Discover and index all skills
4. Initialize MCP servers
5. Load hooks configuration

**Hot Reload:**
- New subagents: Use `/agents` command or restart session
- New skills: Automatically discovered on next relevant context
- Settings changes: Require session restart

---

## 7. Agent Capabilities

### Tool Access and Permissions

**Default Behavior:**
- Subagents inherit all tools from parent conversation
- Includes MCP tools configured for the session
- Use `tools` field to restrict to specific tools
- Use `disallowedTools` to block specific tools

**Tool Inheritance Rules:**

```yaml
# Inherit all tools (default)
---
name: full-access
description: ...
---

# Only these tools
---
name: read-only
description: ...
tools: Read, Glob, Grep
---

# All except these
---
name: no-writes
description: ...
disallowedTools: Write, Edit, MultiEdit
---
```

### Resource Access

**File System:**
- Governed by tool permissions (Read, Write, Edit)
- Subagents respect `.gitignore` patterns
- Can be further restricted by project settings

**Network:**
- Controlled via WebFetch, WebSearch tools
- MCP servers can provide additional network access
- No arbitrary network access by default

**Execution:**
- Bash tool provides shell command execution
- Can be restricted or monitored via hooks
- Sandboxing recommended for production

### Execution Scope

**Context Isolation:**
- Each subagent has its own context window
- Main conversation context not polluted
- Only summarized results return to parent

**Parallel Execution:**
- Multiple subagents can run simultaneously
- Each has independent context
- Results aggregated by orchestrator

### Built-in Subagents

| Name | Purpose | Tools |
|------|---------|-------|
| **Explore** | Read-only codebase exploration | Glob, Grep, Read, limited Bash |
| **Plan** | Architecture and implementation planning | Analysis tools, no modifications |
| **general-purpose** | Complex multi-step tasks | Full tool access |

---

## 8. Agent Invocation

### Automatic Invocation

Claude automatically invokes subagents based on:
1. Task complexity matching agent description
2. Need for isolated context (long operations)
3. Specialized expertise requirements

**Example Flow:**
```
User: "Review the authentication module for security issues"
Claude: [Recognizes match with security-reviewer subagent]
Claude: [Invokes security-reviewer via Task tool]
Subagent: [Performs analysis with isolated context]
Subagent: [Returns summarized findings]
Claude: [Presents results to user]
```

### Slash Commands for Skills

Skills can be invoked via slash commands:

```
/skill-name              # Basic invocation
/skill-name arguments    # With arguments
/fix-issue 123           # Example with issue number
```

**Argument Handling:**
- If skill includes `$ARGUMENTS`, value is substituted
- If skill doesn't include `$ARGUMENTS`, appended as `ARGUMENTS: <value>`

### Programmatic Invocation (SDK)

**TypeScript:**

```typescript
import { query, AgentDefinition } from '@anthropic-ai/claude-agent-sdk';

const agents: Record<string, AgentDefinition> = {
  researcher: {
    description: "Research specialist for codebase exploration",
    prompt: "You are a research specialist...",
    tools: ["Read", "Glob", "Grep"],
    model: "sonnet"
  }
};

const response = query("Investigate the auth system", {
  agents,
  allowedTools: ["Read", "Glob", "Grep", "Task"]
});
```

**Python:**

```python
from claude_agent_sdk import query, AgentDefinition

agents = {
    "researcher": AgentDefinition(
        description="Research specialist for codebase exploration",
        prompt="You are a research specialist...",
        tools=["Read", "Glob", "Grep"],
        model="sonnet"
    )
}

response = await query("Investigate the auth system",
    agents=agents,
    allowed_tools=["Read", "Glob", "Grep", "Task"]
)
```

### Agent Chaining

Subagents can invoke other subagents if they have the `Task` tool:

```yaml
---
name: orchestrator
description: Coordinates multiple specialized agents
tools: Read, Task
---

You coordinate work across specialists:
1. Use the security-reviewer for security analysis
2. Use the performance-reviewer for optimization
3. Synthesize findings into final report
```

**Best Practice:** Limit chain depth to prevent infinite loops.

---

## 9. Multi-Agent Systems

### Multiple Agents Working Together

**Parallel Execution Pattern:**

```
Main Agent
    |
    +-- Task: "Research auth module" --> Subagent A
    |
    +-- Task: "Research database layer" --> Subagent B
    |
    +-- Task: "Research API endpoints" --> Subagent C
    |
    [All three run in parallel]
    |
    +-- Aggregate results from A, B, C
    |
    +-- Synthesize final response
```

### Coordination Patterns

**Fan-Out Pattern:**
- Decompose task into independent subtasks
- Spawn subagents for each subtask
- Aggregate results

**Pipeline Pattern:**
- Sequential processing through specialized agents
- Each agent builds on previous output
- Final agent produces deliverable

**Map-Reduce Pattern:**
- Map: Distribute work across multiple agents
- Process: Each agent works independently
- Reduce: Combine results into final output

### Agent Communication

**Current Limitations:**
- Subagents return summarized results to parent
- No direct inter-agent communication
- Parent orchestrates all coordination

**Feature Request (GitHub #1770):**
- Parent visibility into subagent activities
- Real-time monitoring of subagent progress
- Direct subagent-to-subagent communication

### Orchestration Implementation

**Using CC Mirror (Open Source):**

```yaml
---
name: conductor
description: Orchestrate multi-agent workflows
---

You are "The Conductor" that decomposes work:
1. Break complex tasks into dependency graphs
2. Spawn background agents for parallel work
3. Monitor progress and handle failures
4. Synthesize results when complete
```

**Using claude-flow:**
- Enterprise-grade orchestration
- Swarm intelligence with queen agents
- Consensus mechanisms
- Failure handling

---

## 10. Agent Development Workflow

### Creating a New Agent

**Option 1: Interactive Creation**

```bash
# Run in Claude Code
/agents
# Select "Create new agent"
# Choose scope (project or user)
# Follow guided setup
```

**Option 2: Manual Creation**

1. Create file in appropriate location:
   ```bash
   touch .claude/agents/my-agent.md
   ```

2. Add frontmatter and content:
   ```markdown
   ---
   name: my-agent
   description: What this agent does
   tools: Read, Glob, Grep
   model: sonnet
   ---

   System prompt content here...
   ```

3. Reload agents:
   ```bash
   /agents  # Loads new agents
   ```

### Testing Agents Locally

**Manual Testing:**

1. Create test scenarios
2. Invoke agent via Task tool or directly
3. Observe behavior and results
4. Iterate on system prompt

**Testing Checklist:**

- [ ] Agent invoked for correct scenarios
- [ ] Tools work as expected
- [ ] Constraints are respected
- [ ] Output format is correct
- [ ] Error handling works
- [ ] Performance is acceptable

### Debugging Agents

**Enable Debug Mode:**

```bash
claude --debug
```

**Debug Checklist:**

1. **Check frontmatter parsing**: Ensure valid YAML
2. **Verify tool access**: Confirm tools are available
3. **Review context**: Check what information agent receives
4. **Inspect hooks**: Confirm hooks execute correctly
5. **Check logs**: Review Claude Code logs for errors

**Common Issues:**

| Issue | Cause | Solution |
|-------|-------|----------|
| Agent not found | Invalid frontmatter | Fix YAML syntax |
| Tools not working | Wrong tool names | Use exact tool names |
| Wrong model | Typo in model field | Use: sonnet, opus, haiku, inherit |
| Hooks not running | Invalid command path | Use absolute paths or check permissions |

### Iteration and Refinement

**Refinement Process:**

1. **Observe**: Watch agent behavior in real tasks
2. **Identify**: Note where agent underperforms
3. **Hypothesize**: Determine probable cause
4. **Modify**: Update system prompt or configuration
5. **Test**: Verify improvement
6. **Document**: Record successful patterns

**Prompt Refinement Tips:**

- Add specific examples for ambiguous cases
- Clarify constraints that were violated
- Include edge cases in instructions
- Add validation steps to workflow

---

## 11. Agent State and Memory

### Conversation History

**Subagent Context:**
- Each subagent has isolated context window
- History starts fresh for each invocation
- Only summarized results return to parent
- Parent context not shared with subagent

**Skills Context:**
- Skill content injected into main conversation
- Becomes part of ongoing context
- No separate memory space

### Persistent State

**Claude Code does not provide persistent state across sessions.**

**Workarounds:**

1. **File-based State:**
   ```markdown
   ## State Management
   - Read state from `.claude/state/agent-state.json`
   - Write updated state before finishing
   ```

2. **MCP Server State:**
   - Use memory MCP server
   - Store state in external database

### Context Management

**Context Window Limits:**
- Each model has token limits
- Subagents help manage by isolating context
- Use Explore for read-heavy tasks

**Best Practices:**
- Use subagents for large operations
- Return summarized results, not full details
- Clean up intermediate state

### Session Handling

**Session Lifecycle:**

1. **Start**: Load settings, discover agents/skills
2. **Active**: Process user requests, invoke agents
3. **End**: Cleanup, save state if configured

**Hooks for Session Events:**

```json
{
  "hooks": {
    "SessionStart": [...],
    "SessionEnd": [...]
  }
}
```

---

## 12. Integration with MCP

### Agents Using MCP Tools

**Configuration:**
Subagents inherit MCP tools from the main session by default.

```yaml
---
name: db-analyst
description: Database analysis using MCP tools
tools: Read, mcp__postgres__query
---
```

**Limitations:**
- MCP tools not available in background subagents
- Tool names follow pattern: `mcp__<server>__<tool>`

### MCP Server Configuration

**In `.mcp.json`:**

```json
{
  "mcpServers": {
    "postgres": {
      "command": "mcp-postgres",
      "args": ["--connection-string", "..."],
      "env": {}
    }
  }
}
```

**In `settings.json`:**

```json
{
  "mcpServers": {
    "github": {
      "command": "mcp-github",
      "args": []
    }
  }
}
```

### Agent + MCP Patterns

**Pattern 1: Specialized Database Agent**

```yaml
---
name: sql-analyst
description: Analyze database structure and query performance
tools: Read, Glob, mcp__postgres__query, mcp__postgres__schema
---

You are a database analyst with access to PostgreSQL.
Use mcp tools to:
1. Explore schema
2. Analyze query performance
3. Suggest optimizations
```

**Pattern 2: API Integration Agent**

```yaml
---
name: github-manager
description: Manage GitHub issues and PRs
tools: Read, mcp__github__list_issues, mcp__github__create_pr
---

You manage GitHub workflows.
```

**Pattern 3: Multi-Service Agent**

```yaml
---
name: full-stack-deployer
description: Deploy across multiple services
tools: Bash, mcp__aws__deploy, mcp__datadog__create_monitor
---
```

### Best Practices for MCP Integration

1. **Principle of Least Privilege**: Only grant needed MCP tools
2. **Error Handling**: MCP calls can fail; include fallbacks
3. **Security**: Don't expose sensitive MCP servers to untrusted agents
4. **Documentation**: Document which MCP tools each agent uses

---

## 13. Complete Examples

### Minimal Subagent Example

```markdown
---
name: greeter
description: Simple greeting agent for testing
---

You are a friendly greeter. When invoked, greet the user warmly and ask how you can help.
```

### Full-Featured Subagent Example

```markdown
---
name: security-auditor
description: Comprehensive security code auditor. Analyze code for vulnerabilities including injection attacks (SQL, XSS, command), authentication/authorization flaws, insecure data handling, secrets exposure, and dependency vulnerabilities.
model: opus
tools: Read, Glob, Grep, Bash
disallowedTools: Write, Edit, WebFetch
permissionMode: default
color: red
skills: security-checklist, owasp-top-10
hooks:
  PostToolUse:
    - matcher: "Grep"
      hooks:
        - type: command
          command: "./scripts/log-security-search.sh"
  Stop:
    - type: command
      command: "./scripts/generate-security-report.sh"
---

# Security Auditor

You are a senior security engineer performing code audits.

## Responsibilities

1. **Vulnerability Detection**: Find security flaws in code
2. **Risk Assessment**: Evaluate severity and exploitability
3. **Remediation Guidance**: Provide actionable fix recommendations
4. **Compliance Check**: Verify against security standards

## Methodology

### Phase 1: Reconnaissance
- Map the codebase structure
- Identify entry points (APIs, forms, file uploads)
- Catalog data flows

### Phase 2: Analysis
- Scan for vulnerability patterns
- Check authentication/authorization logic
- Review cryptographic implementations
- Examine input validation

### Phase 3: Reporting
- Document findings with severity ratings
- Provide proof-of-concept where safe
- Recommend specific remediation steps

## Vulnerability Categories

1. **Injection**: SQL, NoSQL, OS command, LDAP
2. **Broken Auth**: Weak passwords, session issues
3. **Sensitive Data**: Exposure, weak encryption
4. **XXE**: XML external entity attacks
5. **Broken Access Control**: Privilege escalation
6. **Misconfig**: Default credentials, verbose errors
7. **XSS**: Reflected, stored, DOM-based
8. **Deserialization**: Insecure deserialization
9. **Components**: Known vulnerable dependencies
10. **Logging**: Insufficient logging/monitoring

## Output Format

For each finding:
```
### [SEVERITY] Finding Title
**Location**: file:line
**Category**: OWASP category
**Description**: What the vulnerability is
**Impact**: What an attacker could do
**Proof**: Code snippet or reproduction steps
**Remediation**: How to fix it
```

## Constraints

- Never exploit vulnerabilities
- Don't access production systems
- Report critical findings immediately
- Maintain confidentiality
```

### Skill Example with Dynamic Content

```markdown
---
name: pr-summary
description: Generate comprehensive pull request summaries. Use when reviewing PRs or preparing merge documentation.
version: "1.0.0"
context: fork
agent: Explore
---

# Pull Request Summary Generator

## Current PR Information

### Diff
!gh pr diff

### PR Details
!gh pr view --json title,body,author,labels,reviews

### Comments
!gh pr view --comments

## Your Task

Analyze the PR above and generate a summary:

1. **Overview**: One-paragraph summary
2. **Changes**: Bullet list of key changes
3. **Impact**: What this affects
4. **Testing**: What should be tested
5. **Concerns**: Any issues or risks
6. **Recommendation**: Approve/request changes
```

### Multi-Agent Orchestrator Example

```markdown
---
name: full-review
description: Comprehensive code review using multiple specialized reviewers
tools: Read, Task
model: opus
---

# Full Review Orchestrator

You coordinate comprehensive code reviews across multiple dimensions.

## Review Process

1. **Security Review**
   - Delegate to security-auditor subagent
   - Wait for security findings

2. **Performance Review**
   - Delegate to performance-analyzer subagent
   - Get optimization recommendations

3. **Code Quality Review**
   - Delegate to code-quality-reviewer subagent
   - Collect style and maintainability issues

4. **Test Coverage Review**
   - Delegate to test-coverage-analyzer subagent
   - Identify missing test cases

## Synthesis

After all reviews complete:
1. Aggregate findings by severity
2. Identify overlapping concerns
3. Prioritize recommendations
4. Generate unified report

## Report Format

# Comprehensive Review Report

## Executive Summary
[High-level overview]

## Critical Issues
[Must fix before merge]

## Important Findings
[Should address soon]

## Suggestions
[Nice to have improvements]

## Metrics
- Security Score: X/10
- Performance Score: X/10
- Quality Score: X/10
- Test Coverage: X%
```

---

## 14. Best Practices

### Agent Design Patterns

**Single Responsibility:**
- Each agent should do one thing well
- Avoid "god agents" that do everything
- Compose multiple agents for complex tasks

**Clear Boundaries:**
- Define explicit scope in description
- List what agent should NOT do
- Set tool restrictions appropriately

**Descriptive Names:**
- Use kebab-case: `security-reviewer`
- Be specific: `react-component-generator` not `code-generator`

### Security Considerations

**Tool Restrictions:**

```yaml
# Read-only agent
tools: Read, Glob, Grep

# No network access
disallowedTools: WebFetch, WebSearch

# No execution
disallowedTools: Bash
```

**Permission Modes:**
- Use `default` for most agents
- Use `plan` for analysis-only agents
- Avoid `bypassPermissions` in production

**Sensitive Data:**
- Don't include secrets in agent files
- Use environment variables for credentials
- Restrict access to sensitive directories

**Sandboxing:**
- Run in containers when possible
- Use filesystem restrictions
- Monitor for unusual behavior

### Performance Optimization

**Context Management:**
- Use subagents to isolate large operations
- Return summaries, not full data
- Use Explore for read-heavy tasks

**Model Selection:**
- Use `haiku` for simple, fast tasks
- Use `sonnet` for balanced performance
- Reserve `opus` for complex reasoning

**Tool Selection:**
- Minimize tool set for faster loading
- Use specific tools over general ones
- Avoid unnecessary MCP tools

### Error Handling

**In System Prompts:**

```markdown
## Error Handling

If you encounter errors:
1. Log the error context
2. Attempt recovery if safe
3. Report failure clearly if unrecoverable
4. Never leave system in inconsistent state
```

**Using Hooks:**

```yaml
hooks:
  PostToolUse:
    - matcher: "Bash"
      hooks:
        - type: command
          command: "./scripts/check-exit-code.sh"
```

**Validation Patterns:**
- Validate inputs before processing
- Check preconditions before operations
- Verify postconditions after operations

---

## 15. Distribution

### Packaging Agents as Plugins

**Plugin Structure:**

```
my-plugin/
├── plugin.json           # Required
├── README.md             # Recommended
├── agents/               # Subagents
│   ├── reviewer.md
│   └── generator.md
├── skills/               # Skills
│   └── my-skill/
│       └── SKILL.md
├── commands/             # Slash commands
├── mcp-servers/          # MCP configuration
└── hooks/                # Hook scripts
```

**plugin.json:**

```json
{
  "name": "my-awesome-plugin",
  "version": "1.0.0",
  "description": "A collection of useful agents and skills",
  "author": "Your Name",
  "repository": "https://github.com/you/my-awesome-plugin",
  "license": "MIT"
}
```

### Sharing Agents

**Via Git Repository:**

1. Create repository with plugin structure
2. Share repository URL
3. Users install: `/plugin install github.com/you/repo`

**Via Plugin Marketplace:**

1. Create marketplace.json in your repository:
   ```json
   {
     "name": "My Marketplace",
     "plugins": [
       {
         "name": "plugin-a",
         "path": "./plugins/plugin-a"
       }
     ]
   }
   ```

2. Users add marketplace:
   ```
   /plugin marketplace add github.com/you/marketplace
   ```

3. Users browse and install:
   ```
   /plugin  # Opens plugin menu
   ```

### Marketplace Submission

**Creating a Marketplace:**

1. Create git repository
2. Add `.claude-plugin/marketplace.json`
3. Structure plugins in repository
4. Host on GitHub, GitLab, or other git host

**marketplace.json:**

```json
{
  "name": "My Organization's Plugins",
  "description": "Official plugins for our team",
  "plugins": [
    {
      "name": "security-tools",
      "description": "Security analysis agents",
      "path": "plugins/security-tools",
      "version": "2.0.0"
    },
    {
      "name": "testing-suite",
      "description": "Automated testing agents",
      "path": "plugins/testing-suite",
      "version": "1.5.0"
    }
  ]
}
```

### Distribution Best Practices

1. **Version Semantically**: Use semver for plugins
2. **Document Thoroughly**: Include comprehensive README
3. **Provide Examples**: Show usage examples
4. **Test Before Publishing**: Verify on clean environment
5. **Handle Dependencies**: Document required MCP servers
6. **License Clearly**: Include LICENSE file

---

## 16. Sources

### Official Anthropic Documentation

- [Create custom subagents - Claude Code Docs](https://code.claude.com/docs/en/sub-agents)
- [Extend Claude with skills - Claude Code Docs](https://code.claude.com/docs/en/skills)
- [Agent SDK overview - Claude Docs](https://platform.claude.com/docs/en/agent-sdk/overview)
- [Subagents in the SDK - Claude Docs](https://platform.claude.com/docs/en/agent-sdk/subagents)
- [Agent Skills in the SDK - Claude Docs](https://platform.claude.com/docs/en/agent-sdk/skills)
- [Hooks reference - Claude Code Docs](https://code.claude.com/docs/en/hooks)
- [Best Practices for Claude Code](https://code.claude.com/docs/en/best-practices)
- [Create and distribute a plugin marketplace](https://code.claude.com/docs/en/plugin-marketplaces)
- [Connect to external tools with MCP](https://platform.claude.com/docs/en/agent-sdk/mcp)

### Anthropic Engineering Blog

- [Equipping agents for the real world with Agent Skills](https://www.anthropic.com/engineering/equipping-agents-for-the-real-world-with-agent-skills)
- [Building agents with the Claude Agent SDK](https://www.anthropic.com/engineering/building-agents-with-the-claude-agent-sdk)
- [Claude Code: Best practices for agentic coding](https://www.anthropic.com/engineering/claude-code-best-practices)
- [How we built our multi-agent research system](https://www.anthropic.com/engineering/multi-agent-research-system)

### GitHub Repositories

- [anthropics/skills - Public repository for Agent Skills](https://github.com/anthropics/skills)
- [Agent Skills Specification](https://github.com/anthropics/skills/blob/main/spec/agent-skills-spec.md)
- [agentskills/agentskills - Specification and documentation](https://github.com/agentskills/agentskills)
- [VoltAgent/awesome-claude-code-subagents](https://github.com/VoltAgent/awesome-claude-code-subagents)

### Agent Skills Specification

- [Agent Skills Specification - agentskills.io](https://agentskills.io/specification)

### Additional Resources

- [Claude Agent Skills: A First Principles Deep Dive](https://leehanchung.github.io/blogs/2025/10/26/claude-skills-deep-dive/)
- [Inside Claude Code Skills: Structure, prompts, invocation](https://mikhail.io/2025/10/claude-code-skills/)
- [Understanding Skills, Agents, Subagents, and MCP in Claude Code](https://colinmcnamara.com/blog/understanding-skills-agents-and-mcp-in-claude-code)
- [Claude Code customization guide](https://alexop.dev/posts/claude-code-customization-guide-claudemd-skills-subagents/)

---

## Appendix A: Quick Reference Card

### Subagent Frontmatter Quick Reference

```yaml
---
# Required
name: agent-name                    # lowercase, hyphens, max 64 chars
description: When to use this       # max 1024 chars

# Model
model: sonnet                       # sonnet | opus | haiku | inherit

# Tools
tools: Read, Glob, Grep             # allowlist (omit = inherit all)
disallowedTools: Write, Edit        # denylist

# Permissions
permissionMode: default             # default | acceptEdits | dontAsk | bypassPermissions | plan

# Visual
color: blue                         # red | blue | green | yellow | purple | orange | pink | cyan

# Integration
skills: skill-a, skill-b            # auto-load these skills

# Hooks
hooks:
  PreToolUse: [...]
  PostToolUse: [...]
  Stop: [...]
---
```

### Skill Frontmatter Quick Reference

```yaml
---
# Required
name: skill-name                    # becomes /skill-name command
description: What and when          # primary trigger signal

# Optional
version: "1.0.0"                    # semantic version
disable-model-invocation: false     # true = user-only invocation
user-invocable: true                # false = Claude-only
mode: false                         # true = mode command

# Claude Code Extensions
context: fork                       # run in subagent
agent: Explore                      # which subagent
allowed-tools: Read, Glob           # tool restrictions
---
```

### Common Tool Names

| Tool | Purpose |
|------|---------|
| Read | Read files |
| Write | Create/overwrite files |
| Edit | String replacements |
| MultiEdit | Batch edits |
| Glob | Pattern matching |
| Grep | Content search |
| Bash | Shell commands |
| Task | Delegate to subagents |
| WebFetch | Fetch URLs |
| WebSearch | Search web |
| Skill | Invoke skills |
| TodoWrite | Task management |

### File Locations

| Type | Project | User |
|------|---------|------|
| Subagents | `.claude/agents/` | `~/.claude/agents/` |
| Skills | `.claude/skills/` | `~/.claude/skills/` |
| Settings | `.claude/settings.json` | `~/.claude/settings.json` |

---

*Report generated from official Anthropic documentation and community resources. For the most current information, always refer to the official documentation.*
