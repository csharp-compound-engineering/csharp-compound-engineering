# Claude Code Agents Research

**Research Date:** 2026-01-24
**Scope:** Agent types, spawning, configuration, communication, coordination, and best practices

---

## Table of Contents

1. [Agent Types](#1-agent-types)
2. [Agent Spawning](#2-agent-spawning)
3. [Agent Configuration](#3-agent-configuration)
4. [Agent Communication](#4-agent-communication)
5. [Agent Coordination](#5-agent-coordination)
6. [Custom Agents](#6-custom-agents)
7. [Agent Permissions](#7-agent-permissions)
8. [Best Practices](#8-best-practices)
9. [Sources](#sources)

---

## 1. Agent Types

Claude Code includes several built-in subagents that Claude automatically uses when appropriate.

### Built-in Subagents

| Agent | Model | Tools | Purpose |
|:------|:------|:------|:--------|
| **Explore** | Haiku (fast) | Read-only (denied Write/Edit) | File discovery, code search, codebase exploration |
| **Plan** | Inherits | Read-only (denied Write/Edit) | Codebase research during plan mode |
| **General-purpose** | Inherits | All tools | Complex research, multi-step operations, code modifications |
| **Bash** | Inherits | Bash | Running terminal commands in separate context |
| **statusline-setup** | Sonnet | Read, Edit | Configuring status line display |
| **Claude Code Guide** | Haiku | - | Answering questions about Claude Code features |

### Explore Subagent

The Explore subagent is a fast, read-only agent optimized for searching and analyzing codebases. When invoking Explore, Claude specifies a thoroughness level:

- **quick**: Targeted lookups
- **medium**: Balanced exploration
- **very thorough**: Comprehensive analysis

The Explore agent starts with a fresh slate (not inheriting full context), which makes sense since search tasks are often independent.

### Plan Subagent

The Plan subagent is used during [plan mode](https://code.claude.com/docs/en/common-workflows#use-plan-mode-for-safe-code-analysis) to gather context before presenting a plan. It prevents infinite nesting (subagents cannot spawn other subagents) while still gathering necessary context.

### General-Purpose Subagent

Claude delegates to general-purpose when the task requires:
- Both exploration and modification
- Complex reasoning to interpret results
- Multiple dependent steps

---

## 2. Agent Spawning

Subagents are spawned via the **Task tool**. The Task tool launches specialized sub-agents to handle complex tasks autonomously.

### Task Tool Parameters

| Parameter | Required | Description |
|:----------|:---------|:------------|
| `subagent_type` | Yes | The type of agent to spawn (e.g., "general-purpose", "explore") |
| `description` | Yes | Short (3-5 word) description of the task |
| `prompt` | Yes | The task/instructions for the agent to perform |
| `run_in_background` | No | Whether to run concurrently (default: false) |
| `resume` | No | Agent ID to resume a previous agent's work |

### Spawning via SDK (Programmatic)

```python
from claude_agent_sdk import query, ClaudeAgentOptions, AgentDefinition

async for message in query(
    prompt="Review the authentication module for security issues",
    options=ClaudeAgentOptions(
        allowed_tools=["Read", "Grep", "Glob", "Task"],  # Task tool required!
        agents={
            "code-reviewer": AgentDefinition(
                description="Expert code review specialist",
                prompt="You are a code review specialist...",
                tools=["Read", "Grep", "Glob"],
                model="sonnet"
            )
        }
    )
):
    if hasattr(message, "result"):
        print(message.result)
```

### Spawning via CLI Flag

```bash
claude --agents '{
  "code-reviewer": {
    "description": "Expert code reviewer. Use proactively after code changes.",
    "prompt": "You are a senior code reviewer...",
    "tools": ["Read", "Grep", "Glob", "Bash"],
    "model": "sonnet"
  }
}'
```

### Key Constraints

- **Subagents cannot spawn other subagents** - This is a fundamental limitation
- The `Task` tool must be included in `allowedTools` for Claude to spawn subagents
- Maximum parallelism is capped at 10 concurrent agents

---

## 3. Agent Configuration

### Storage Locations (Priority Order)

| Location | Scope | Priority |
|:---------|:------|:---------|
| `--agents` CLI flag | Current session | 1 (highest) |
| `.claude/agents/` | Current project | 2 |
| `~/.claude/agents/` | All user projects | 3 |
| Plugin's `agents/` directory | Where plugin enabled | 4 (lowest) |

### YAML Frontmatter Structure

Subagent files use YAML frontmatter for configuration:

```markdown
---
name: code-reviewer
description: Reviews code for quality and best practices
tools: Read, Glob, Grep
model: sonnet
permissionMode: default
skills:
  - api-conventions
hooks:
  PreToolUse:
    - matcher: "Bash"
      hooks:
        - type: command
          command: "./scripts/validate.sh"
---

You are a code reviewer. When invoked, analyze the code and provide
specific, actionable feedback on quality, security, and best practices.
```

### Supported Frontmatter Fields

| Field | Required | Description |
|:------|:---------|:------------|
| `name` | Yes | Unique identifier (lowercase, hyphens) |
| `description` | Yes | When Claude should delegate to this agent |
| `tools` | No | Tools the agent can use (inherits all if omitted) |
| `disallowedTools` | No | Tools to explicitly deny |
| `model` | No | `sonnet`, `opus`, `haiku`, or `inherit` (default: inherit) |
| `permissionMode` | No | `default`, `acceptEdits`, `dontAsk`, `bypassPermissions`, `plan` |
| `skills` | No | Skills to preload into agent context |
| `hooks` | No | Lifecycle hooks for this agent |

### Model Selection

- **sonnet**: Balanced capability and speed
- **opus**: Most capable, for complex analysis
- **haiku**: Fastest and cheapest (90% of Sonnet's performance at 2x speed, 3x cost savings)
- **inherit**: Use same model as main conversation (default)

---

## 4. Agent Communication

### How Agents Report Results

When a subagent completes:
1. Claude receives the agent ID in the Task tool result
2. Results return to the main conversation context
3. Only relevant summaries return, not full context (context isolation)

### Detecting Subagent Invocation

```python
async for message in query(...):
    # Check for subagent invocation
    if hasattr(message, 'content') and message.content:
        for block in message.content:
            if getattr(block, 'type', None) == 'tool_use' and block.name == 'Task':
                print(f"Subagent invoked: {block.input.get('subagent_type')}")

    # Check if message is from within a subagent
    if hasattr(message, 'parent_tool_use_id') and message.parent_tool_use_id:
        print("(running inside subagent)")
```

### Resuming Agents

Subagents can be resumed to continue where they left off with full conversation history:

```python
# First query captures session_id and agent_id
async for message in query(prompt="Use Explore agent to find APIs", ...):
    if hasattr(message, "session_id"):
        session_id = message.session_id
    # Extract agent_id from content...

# Second query resumes
async for message in query(
    prompt=f"Resume agent {agent_id} and analyze complexity",
    options=ClaudeAgentOptions(resume=session_id, ...)
):
    ...
```

---

## 5. Agent Coordination

### Parallel vs Sequential Execution

#### Foreground (Sequential/Blocking)
- Blocks main conversation until complete
- Permission prompts passed through to user
- Full interactive capability

#### Background (Parallel/Concurrent)
- Runs concurrently while you continue working
- Inherits parent's permissions, auto-denies anything not pre-approved
- MCP tools are NOT available in background subagents
- If permission needed, tool call fails but agent continues

### Parallel Execution

```
"Explore the codebase using 4 tasks in parallel. Each agent should explore different directories."
```

- Claude executes tasks in parallel batches
- Waits for entire batch to complete before starting next batch
- Maximum parallelism: ~10 agents

### When to Use Parallel

Parallel execution works best when:
- Research paths are independent
- Tasks don't depend on each other's output
- You need to maximize context window capacity

### When to Use Sequential

Sequential workflows excel when:
- Output of one agent becomes input for next
- Complex reviews need ordered stages
- File-based communication between agents is needed

**Example: Sequential Multi-Stage Review**
```
style-checker -> writes findings.md
security-reviewer -> reads findings.md, appends
test-validator -> reads findings.md, finalizes
```

### Coordination Limitations

- Subagents cannot exchange information directly with each other
- Communication must go through main agent or file-based handoff
- Cannot create hierarchical agent structures (no sub-sub-agents)

---

## 6. Custom Agents

### Creating Custom Agents

#### Method 1: Interactive (`/agents` command)

```
/agents
```

Provides interface to:
- View all available subagents
- Create new agents with guided setup or Claude generation
- Edit existing agent configuration
- Delete custom agents

#### Method 2: Markdown Files

Create `.claude/agents/my-agent.md`:

```markdown
---
name: my-agent
description: Does specialized task X
tools: Read, Grep, Bash
model: sonnet
---

You are a specialist in X. When invoked:
1. Do step A
2. Do step B
3. Return findings
```

#### Method 3: Programmatic (SDK)

```python
agents={
    "my-agent": AgentDefinition(
        description="Specialist for X",
        prompt="You are a specialist...",
        tools=["Read", "Grep"],
        model="sonnet"
    )
}
```

### Tool SEO for Custom Agents

To encourage Claude to proactively use custom agents, include terms like:
- "use PROACTIVELY"
- "MUST BE USED"
- "Use immediately after..."

Example:
```yaml
description: "Expert code reviewer. Use PROACTIVELY after code changes."
```

### Token Efficiency Guidelines

| Agent Weight | Token Usage | When to Use |
|:-------------|:------------|:------------|
| Lightweight | <3k tokens | Frequent-use, simple tasks |
| Medium | 10-15k tokens | Standard specialized tasks |
| Heavy | 25k+ tokens | Rare, complex analysis |

---

## 7. Agent Permissions

### Permission Modes

| Mode | Behavior |
|:-----|:---------|
| `default` | Standard permission checking with prompts |
| `acceptEdits` | Auto-accept file edits |
| `dontAsk` | Auto-deny permission prompts |
| `bypassPermissions` | Skip ALL permission checks (dangerous) |
| `plan` | Plan mode (read-only exploration) |

### Tool Access Control

```yaml
# Allowlist approach
tools: Read, Glob, Grep, Bash

# Denylist approach
disallowedTools: Write, Edit
```

### Common Tool Combinations

| Use Case | Tools |
|:---------|:------|
| Read-only analysis | `Read`, `Grep`, `Glob` |
| Test execution | `Bash`, `Read`, `Grep` |
| Code modification | `Read`, `Edit`, `Write`, `Grep`, `Glob` |
| Full access | Omit `tools` field (inherits all) |

### MCP Tool Access

- Subagents inherit MCP tools from main conversation by default
- **MCP tools are NOT available in background subagents**
- Can be restricted via `tools` or `disallowedTools` fields

### Disabling Specific Agents

```json
{
  "permissions": {
    "deny": ["Task(Explore)", "Task(my-custom-agent)"]
  }
}
```

Or via CLI:
```bash
claude --disallowedTools "Task(Explore)"
```

---

## 8. Best Practices

### When to Use Agents vs Direct Tool Calls

**Use Main Conversation When:**
- Task needs frequent back-and-forth or iterative refinement
- Multiple phases share significant context
- Making quick, targeted changes
- Latency matters (subagents need time to gather context)

**Use Subagents When:**
- Task produces verbose output you don't need in main context
- Want to enforce specific tool restrictions
- Work is self-contained and can return a summary
- Need to run parallel independent tasks

### Agent Design Principles

1. **Create focused, single-purpose agents** - Each agent should excel at one specific task
2. **Write detailed descriptions** - Claude uses description to decide when to delegate
3. **Limit tool access** - Grant only necessary permissions
4. **Start lightweight** - Minimal tools for maximum composability
5. **Prioritize efficiency** - For frequent-use agents, keep token usage low

### Workflow Recommendations

#### Explore, Plan, Code, Commit

1. **Research**: Request Claude read relevant files without coding
2. **Plan**: Use plan mode or extended thinking ("think hard", "ultrathink")
3. **Document**: Create a plan document for checkpoint/rollback
4. **Implement**: Only begin coding after plan approval
5. **Commit**: Finalize with version control

#### Subagent Delegation

- Tell Claude to use subagents early in conversation to preserve context
- Isolate high-volume operations (tests, logs, docs) in subagents
- Use file-based communication for sequential multi-agent workflows

### Common Patterns

#### Pattern 1: Parallel Research
```
Research the authentication, database, and API modules in parallel using separate subagents
```

#### Pattern 2: Chained Subagents
```
Use the code-reviewer subagent to find performance issues, then use the optimizer subagent to fix them
```

#### Pattern 3: Isolate Verbose Operations
```
Use a subagent to run the test suite and report only the failing tests with their error messages
```

#### Pattern 4: Multi-Claude Verification
- Have one Claude write code
- Use another Claude to verify with isolated context
- Use git worktrees for independent parallel work

---

## Sources

### Official Documentation
- [Create custom subagents - Claude Code Docs](https://code.claude.com/docs/en/sub-agents)
- [Subagents in the SDK - Claude Docs](https://platform.claude.com/docs/en/agent-sdk/subagents)
- [Claude Code: Best practices for agentic coding](https://www.anthropic.com/engineering/claude-code-best-practices)
- [Building agents with the Claude Agent SDK](https://www.anthropic.com/engineering/building-agents-with-the-claude-agent-sdk)

### Community Resources
- [Task Tool vs. Subagents: How Agents Work in Claude Code](https://www.ibuildwith.ai/blog/task-tool-vs-subagents-how-agents-work-in-claude-code/)
- [Task/Agent Tools - ClaudeLog](https://claudelog.com/mechanics/task-agent-tools/)
- [Claude Code customization guide](https://alexop.dev/posts/claude-code-customization-guide-claudemd-skills-subagents/)
- [What Actually Is Claude Code's Plan Mode?](https://lucumr.pocoo.org/2025/12/17/what-is-plan-mode/)
- [How to Use Claude Code Subagents to Parallelize Development](https://zachwills.net/how-to-use-claude-code-subagents-to-parallelize-development/)
- [Claude Code System Prompts Repository](https://github.com/Piebald-AI/claude-code-system-prompts)

### GitHub Issues & Discussions
- [Sub-Agent Task Tool Not Exposed When Launching Nested Agents](https://github.com/anthropics/claude-code/issues/4182)
- [Feature Request: Background Agent Execution](https://github.com/anthropics/claude-code/issues/9905)
- [BUG: Sub-agents can't create sub-sub-agents](https://github.com/anthropics/claude-code/issues/19077)

---

## Appendix: Example Agent Definitions

### Code Reviewer (Read-Only)

```markdown
---
name: code-reviewer
description: Expert code review specialist. Use PROACTIVELY after code changes.
tools: Read, Grep, Glob, Bash
model: inherit
---

You are a senior code reviewer ensuring high standards of code quality and security.

When invoked:
1. Run git diff to see recent changes
2. Focus on modified files
3. Begin review immediately

Review checklist:
- Code is clear and readable
- Functions and variables are well-named
- No duplicated code
- Proper error handling
- No exposed secrets or API keys
- Input validation implemented

Provide feedback organized by priority:
- Critical issues (must fix)
- Warnings (should fix)
- Suggestions (consider improving)
```

### Debugger (Can Modify)

```markdown
---
name: debugger
description: Debugging specialist for errors and test failures. Use when encountering issues.
tools: Read, Edit, Bash, Grep, Glob
---

You are an expert debugger specializing in root cause analysis.

When invoked:
1. Capture error message and stack trace
2. Identify reproduction steps
3. Isolate the failure location
4. Implement minimal fix
5. Verify solution works

For each issue, provide:
- Root cause explanation
- Evidence supporting diagnosis
- Specific code fix
- Testing approach
```

### Database Query Validator (Conditional Access)

```markdown
---
name: db-reader
description: Execute read-only database queries. Use for data analysis.
tools: Bash
hooks:
  PreToolUse:
    - matcher: "Bash"
      hooks:
        - type: command
          command: "./scripts/validate-readonly-query.sh"
---

You are a database analyst with read-only access. Execute SELECT queries only.

If asked to INSERT, UPDATE, DELETE, or modify schema, explain that you only have read access.
```
