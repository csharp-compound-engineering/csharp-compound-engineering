# Claude Code Plugin Architecture Research

**Research Date:** January 22, 2026
**Research Focus:** Claude Code plugin system, MCP architecture, and extension mechanisms

---

## Table of Contents

1. [Claude Code Overview](#1-claude-code-overview)
2. [Official Documentation Sources](#2-official-documentation-sources)
3. [MCP Servers as Plugins](#3-mcp-servers-as-plugins)
4. [Plugin Configuration Files](#4-plugin-configuration-files)
5. [Plugin Components](#5-plugin-components)
6. [Skills and Slash Commands](#6-skills-and-slash-commands)
7. [Hooks System](#7-hooks-system)
8. [Plugin Installation and Distribution](#8-plugin-installation-and-distribution)

---

## 1. Claude Code Overview

### What is Claude Code?

Claude Code is Anthropic's agentic coding tool that lives in your terminal. It understands your codebase and helps you code faster through natural language commands. Key capabilities include:

- **Code Analysis**: Deep understanding of project structure and code logic
- **Feature Development**: Create new features from scratch
- **Git Workflows**: Handle git operations through natural language
- **Multi-Model Support**: Works with various Claude models
- **MCP Server Integration**: Connects to hundreds of external tools and data sources

### Relationship Between Claude Code and MCP

The Model Context Protocol (MCP) is an open standard introduced by Anthropic in November 2024 to standardize how AI systems integrate with external tools, systems, and data sources. Claude Code acts as an **MCP Client** (or Host) that connects to **MCP Servers**.

**Architecture:**
```
[Claude Code (Host/Client)] <---> [MCP Servers] <---> [Tools, Databases, APIs]
```

Key architectural components:
- **Hosts**: Applications like Claude Code that access data/tools through MCP
- **MCP Clients**: Connectors within hosts maintaining stateful sessions with MCP servers
- **MCP Servers**: Programs providing specific capabilities through standardized protocol

MCP was donated to the Agentic AI Foundation (AAIF) under the Linux Foundation in December 2025, co-founded by Anthropic, Block, and OpenAI.

---

## 2. Official Documentation Sources

### Primary Documentation

| Resource | URL |
|----------|-----|
| **Claude Code Documentation** | https://code.claude.com/docs/en/overview |
| **CLI Reference** | https://code.claude.com/docs/en/cli-reference |
| **MCP Integration Guide** | https://code.claude.com/docs/en/mcp |
| **Hooks Reference** | https://code.claude.com/docs/en/hooks |
| **Slash Commands** | https://code.claude.com/docs/en/slash-commands |
| **Plugins Documentation** | https://code.claude.com/docs/en/plugins |

### GitHub Repositories

| Repository | Purpose |
|------------|---------|
| [anthropics/claude-code](https://github.com/anthropics/claude-code) | Main Claude Code repository |
| [anthropics/claude-plugins-official](https://github.com/anthropics/claude-plugins-official) | Official plugin directory |
| [anthropics/skills](https://github.com/anthropics/skills) | Public repository for Agent Skills |
| [anthropics/claude-code-action](https://github.com/anthropics/claude-code-action) | GitHub Actions integration |

### MCP Specification Resources

| Resource | URL |
|----------|-----|
| **MCP Documentation** | https://modelcontextprotocol.io |
| **MCP Specification** | https://modelcontextprotocol.io/specification |
| **MCP Server Registry** | https://registry.modelcontextprotocol.io |
| **MCP GitHub Organization** | https://github.com/modelcontextprotocol |
| [modelcontextprotocol/servers](https://github.com/modelcontextprotocol/servers) | Pre-built MCP servers |

### SDK Documentation

- **Python SDK**: https://modelcontextprotocol.github.io/python-sdk/
- **TypeScript SDK**: `@modelcontextprotocol/sdk`
- **C# SDK**: `ModelContextProtocol` NuGet package (maintained with Microsoft)
- **Java SDK**: Maintained with Spring AI
- **Swift SDK**: Official Apple platform support

---

## 3. MCP Servers as Plugins

### How MCP Servers Function as Extensions

MCP servers extend Claude Code by providing:
- **Tools**: Callable functions the LLM can execute
- **Resources**: Data/content providers (read-only)
- **Prompts**: Pre-written templates for common tasks

### Transport Types

#### 1. HTTP Transport (Recommended)

```bash
# Basic syntax
claude mcp add --transport http <name> <url>

# With authentication
claude mcp add --transport http stripe https://mcp.stripe.com \
  --header "Authorization: Bearer your-token"
```

**Best for:** Cloud-based services, remote APIs

#### 2. SSE Transport (Deprecated)

```bash
claude mcp add --transport sse <name> <url>
```

**Note:** Use HTTP servers instead when available.

#### 3. Stdio Transport (Local)

```bash
# Basic syntax
claude mcp add --transport stdio <name> -- <command> [args...]

# With environment variables
claude mcp add --transport stdio --env AIRTABLE_API_KEY=YOUR_KEY airtable \
  -- npx -y airtable-mcp-server
```

**Best for:** Local tools, custom scripts, direct system access

### Configuration in Claude Code

MCP servers can be configured at three scope levels:

| Scope | Location | Purpose |
|-------|----------|---------|
| **Local** | `~/.claude.json` (project path) | Personal, project-specific servers |
| **Project** | `.mcp.json` (project root) | Team-shared servers (version controlled) |
| **User** | `~/.claude.json` | Cross-project personal servers |
| **Managed** | System directories | Organization-wide control (admin-deployed) |

---

## 4. Plugin Configuration Files

### `.mcp.json` - Project-Level MCP Configuration

Located at project root, shared via version control:

```json
{
  "mcpServers": {
    "server-name": {
      "type": "http|stdio|sse",
      "command": "/path/to/executable",
      "args": ["--arg1", "value1"],
      "env": {
        "VAR_NAME": "value"
      },
      "url": "https://api.example.com/mcp",
      "headers": {
        "Authorization": "Bearer token"
      }
    }
  }
}
```

### `~/.claude.json` - User Configuration

Personal configuration for MCP servers and settings.

### Settings Files Hierarchy

```
~/.claude/settings.json         # User settings (all projects)
.claude/settings.json           # Project settings (shared, in git)
.claude/settings.local.json     # Local project settings (personal, gitignored)
```

### Managed Configuration Paths (Enterprise)

| Platform | Path |
|----------|------|
| macOS | `/Library/Application Support/ClaudeCode/managed-mcp.json` |
| Linux/WSL | `/etc/claude-code/managed-mcp.json` |
| Windows | `C:\Program Files\ClaudeCode\managed-mcp.json` |

### Environment Variable Expansion

```json
{
  "mcpServers": {
    "api-server": {
      "type": "http",
      "url": "${API_BASE_URL:-https://api.example.com}/mcp",
      "headers": {
        "Authorization": "Bearer ${API_KEY}"
      }
    }
  }
}
```

**Syntax:**
- `${VAR}` - Expands to environment variable
- `${VAR:-default}` - Uses default if not set

---

## 5. Plugin Components

### Standard Plugin Structure

```
plugin-name/
├── .claude-plugin/
│   └── plugin.json          # Plugin metadata (required)
├── .mcp.json                # MCP server configuration (optional)
├── commands/                # Slash commands (optional)
├── agents/                  # Agent definitions (optional)
├── skills/                  # Skill definitions (optional)
├── hooks/
│   └── hooks.json          # Hook definitions (optional)
└── README.md                # Documentation
```

### 5.1 MCP Tools

Tools are functions that can be called by the LLM (with user approval):

**Python Example (FastMCP):**
```python
@mcp.tool()
async def get_alerts(state: str) -> str:
    """Get weather alerts for a US state.

    Args:
        state: Two-letter US state code (e.g. CA, NY)
    """
    # Implementation
```

**TypeScript Example:**
```typescript
server.registerTool(
  "get_alerts",
  {
    description: "Get alerts for a state",
    inputSchema: {
      state: z.string().length(2).describe("Two-letter state code"),
    },
  },
  async ({ state }) => {
    // Implementation
  }
);
```

### 5.2 MCP Resources

Resources are data entities exposed by servers - file-like data that can be read:

- Static content (configuration, messages)
- Dynamic content (user profiles, database records)
- Identified by URIs
- Can contain text or binary content

**Key difference from Tools:** Resources are passively made available; Tools are actions that execute logic.

### 5.3 MCP Prompts

Pre-written templates that help users accomplish specific tasks:

- Reusable, structured message templates
- Return predefined message lists for consistent behavior
- Can be versioned and updated centrally
- Typically exposed through slash commands or menu options

### 5.4 Skills

Skills are folders of instructions, scripts, and resources that Claude loads dynamically:

```
my-skill/
├── SKILL.md           # Main instructions (required)
├── template.md        # Template for Claude to fill in
├── examples/
│   └── sample.md      # Example output
└── scripts/
    └── validate.sh    # Executable script
```

See [Section 6](#6-skills-and-slash-commands) for detailed information.

### 5.5 Hooks

Deterministic triggers that run shell commands at specific lifecycle points:

See [Section 7](#7-hooks-system) for detailed information.

---

## 6. Skills and Slash Commands

### Overview

Skills extend Claude's capabilities by creating custom slash commands and automated behaviors. They follow the [Agent Skills](https://agentskills.io) open standard with Claude-specific extensions.

### SKILL.md Format

Every skill requires a `SKILL.md` file with YAML frontmatter:

```markdown
---
name: explain-code
description: Explains code with visual diagrams and analogies
disable-model-invocation: false
user-invocable: true
allowed-tools: Read, Grep
argument-hint: [filename]
context: fork
agent: Explore
model: claude-opus
---

Your skill instructions here...
```

### Frontmatter Reference

| Field | Required | Description |
|-------|----------|-------------|
| `name` | No | Display name (lowercase, hyphens, max 64 chars). Becomes `/skill-name` |
| `description` | Recommended | What the skill does; used for automatic invocation |
| `argument-hint` | No | Autocomplete hint, e.g., `[issue-number]` |
| `disable-model-invocation` | No | `true` prevents Claude from auto-loading (manual only) |
| `user-invocable` | No | `false` hides from `/` menu (Claude-only) |
| `allowed-tools` | No | Tools Claude can use: `Read`, `Grep`, `Bash(python:*)` |
| `model` | No | Override model when skill is active |
| `context` | No | `fork` to run in isolated subagent context |
| `agent` | No | Subagent type: `Explore`, `Plan`, `general-purpose` |
| `hooks` | No | Lifecycle hooks for automation |

### Skill Locations

| Location | Path | Scope |
|----------|------|-------|
| **Enterprise** | Managed settings | All organization users |
| **Personal** | `~/.claude/skills/<skill-name>/SKILL.md` | All your projects |
| **Project** | `.claude/skills/<skill-name>/SKILL.md` | This project only |
| **Plugin** | `<plugin>/skills/<skill-name>/SKILL.md` | Where plugin enabled |

### String Substitutions

```markdown
$ARGUMENTS              # All arguments passed to skill
${CLAUDE_SESSION_ID}   # Current session ID
```

### Dynamic Context Injection

Commands execute before Claude sees content:

```markdown
---
name: pr-summary
context: fork
agent: Explore
allowed-tools: Bash(gh:*)
---

## Pull Request Context
- PR diff: !`gh pr diff`
- PR comments: !`gh pr view --comments`
```

### Backward Compatibility

Files in `.claude/commands/` still work with the same frontmatter. If both exist with the same name, the skill takes precedence.

---

## 7. Hooks System

### Overview

Hooks are deterministic triggers that run shell commands at specific points in Claude Code's lifecycle.

### Hook Lifecycle Events

| Hook | When it fires |
|------|---------------|
| `SessionStart` | Session begins or resumes |
| `UserPromptSubmit` | User submits a prompt |
| `PreToolUse` | Before tool execution |
| `PermissionRequest` | When permission dialog appears |
| `PostToolUse` | After tool succeeds |
| `PostToolUseFailure` | After tool fails |
| `SubagentStart` | When spawning a subagent |
| `SubagentStop` | When subagent finishes |
| `Stop` | Claude finishes responding |
| `PreCompact` | Before context compaction |
| `SessionEnd` | Session terminates |
| `Notification` | Claude Code sends notifications |
| `Setup` | Runs with `--init` or `--maintenance` flags |

### Configuration Structure

```json
{
  "hooks": {
    "EventName": [
      {
        "matcher": "ToolPattern",
        "hooks": [
          {
            "type": "command",
            "command": "your-command-here",
            "timeout": 30
          }
        ]
      }
    ]
  }
}
```

### Matchers

- **Simple strings**: `Write` matches only the Write tool
- **Regex patterns**: `Edit|Write` or `Notebook.*`
- **Wildcard**: `*` matches all tools
- **MCP tools**: `mcp__<server>__<tool>`

### Hook Types

#### Command Hooks

```json
{
  "type": "command",
  "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/check-style.sh",
  "timeout": 30
}
```

**Environment Variables:**
- `$CLAUDE_PROJECT_DIR` - Project root directory
- `$CLAUDE_PLUGIN_ROOT` - Plugin directory (for plugin hooks)
- `$CLAUDE_ENV_FILE` - File for persisting environment variables

#### Prompt-Based Hooks

```json
{
  "type": "prompt",
  "prompt": "Evaluate if Claude should stop: $ARGUMENTS",
  "timeout": 30
}
```

### PreToolUse Hook Responses

| Response | Effect |
|----------|--------|
| `"permissionDecision": "allow"` | Bypass permissions |
| `"permissionDecision": "deny"` | Block tool call |
| `"permissionDecision": "ask"` | Request user confirmation |
| `"updatedInput"` | Modify tool inputs before execution |
| `"additionalContext"` | Add context to Claude |

### Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success - Process JSON if present |
| 2 | Blocking error - stderr used as error message |
| Other | Non-blocking error - stderr in verbose mode |

### Plugin Hooks

Plugins can provide hooks via `hooks/hooks.json`:

```json
{
  "description": "Automatic code formatting",
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Write|Edit",
        "hooks": [
          {
            "type": "command",
            "command": "${CLAUDE_PLUGIN_ROOT}/scripts/format.sh"
          }
        ]
      }
    ]
  }
}
```

### Component-Scoped Hooks

Define hooks in skill frontmatter:

```yaml
---
name: secure-operations
hooks:
  PreToolUse:
    - matcher: "Bash"
      hooks:
        - type: command
          command: "./scripts/security-check.sh"
---
```

---

## 8. Plugin Installation and Distribution

### Installation Methods

#### CLI Commands

```bash
# Add MCP server
claude mcp add --transport http <name> <url>
claude mcp add --transport stdio --scope project myserver -- npx server

# List all configured servers
claude mcp list

# Remove a server
claude mcp remove <server-name>

# Check server status
/mcp

# Import from Claude Desktop
claude mcp add-from-claude-desktop
```

#### Plugin Installation

```bash
# Via slash command
/plugin install {plugin-name}@claude-plugin-directory

# Via UI
Navigate to /plugin > Discover
```

#### npm/npx Method

```bash
claude mcp add-json github '{"command":"npx","args":["-y","@modelcontextprotocol/server-github"]}'
```

### Desktop Extensions (.mcpb format)

One-click MCP server installation:

```bash
# Install bundler
npm install -g @anthropic-ai/mcpb

# In your MCP server directory
mcpb init
mcpb pack
```

**Note:** Uses `.mcpb` (MCP Bundle) file extension. Older `.dxt` extensions still work.

### Plugin Marketplace

```bash
# Add marketplace
/plugin marketplace add anthropics/skills

# Browse and install
/plugin > Browse and install plugins
```

### Common MCP Commands Reference

| Command | Description |
|---------|-------------|
| `claude mcp add` | Add a new MCP server |
| `claude mcp list` | List all configured servers |
| `claude mcp get <name>` | Get server details |
| `claude mcp remove <name>` | Remove a server |
| `claude mcp reset-project-choices` | Reset approval choices |
| `claude mcp add-json <name> '<json>'` | Add from JSON config |
| `claude mcp serve` | Run Claude as MCP server |

### Configuration Scopes

| Scope | Flag | Description |
|-------|------|-------------|
| Local | `--scope local` | Default, project-specific |
| Project | `--scope project` | Shared via `.mcp.json` |
| User | `--scope user` | Cross-project personal |

### Security Considerations

- MCP servers execute arbitrary code with user permissions
- Verify trust before installing third-party servers
- Be careful with servers that fetch untrusted content (prompt injection risk)
- Hooks can modify, delete, or access any files your user account can access
- Direct edits to hooks require review in `/hooks` menu

---

## Summary

Claude Code's plugin architecture is built on multiple interconnected systems:

1. **MCP Servers**: The foundation for tool integration via standardized protocol
2. **Skills**: Markdown-based instructions with YAML frontmatter for extending capabilities
3. **Hooks**: Deterministic shell triggers for automation at lifecycle events
4. **Configuration Files**: Hierarchical JSON configuration at user, project, and managed levels

The system provides flexibility from simple slash commands to complex enterprise-managed deployments, with security considerations baked into the permission and approval systems.

---

## Sources

### Claude Code Documentation
- [CLI Reference](https://code.claude.com/docs/en/cli-reference)
- [MCP Integration](https://code.claude.com/docs/en/mcp)
- [Hooks Reference](https://code.claude.com/docs/en/hooks)
- [Slash Commands](https://code.claude.com/docs/en/slash-commands)

### GitHub Repositories
- [anthropics/claude-code](https://github.com/anthropics/claude-code)
- [anthropics/claude-plugins-official](https://github.com/anthropics/claude-plugins-official)
- [anthropics/skills](https://github.com/anthropics/skills)
- [modelcontextprotocol/servers](https://github.com/modelcontextprotocol/servers)

### MCP Specification
- [Model Context Protocol](https://modelcontextprotocol.io)
- [MCP Server Registry](https://registry.modelcontextprotocol.io)
- [Build an MCP Server](https://modelcontextprotocol.io/docs/develop/build-server)

### Additional Resources
- [Claude Code Best Practices](https://www.anthropic.com/engineering/claude-code-best-practices)
- [Desktop Extensions](https://www.anthropic.com/engineering/desktop-extensions)
- [Understanding MCP Features - WorkOS](https://workos.com/blog/mcp-features-guide)
- [Inside Claude Code Skills](https://mikhail.io/2025/10/claude-code-skills/)
