# Phase 009: Plugin Directory Structure

> **Category**: Infrastructure Setup
> **Prerequisites**: Phase 001 (Repository Initialization)
> **Estimated Effort**: 2-3 hours
> **Status**: Pending

---

## Objective

Create the complete directory structure for the `csharp-compounding-docs` Claude Code plugin, including all required configuration files, manifest files, and subdirectory scaffolding for skills, agents, and hooks.

---

## Success Criteria

- [ ] `plugins/csharp-compounding-docs/` directory exists with correct structure
- [ ] `plugin.json` manifest file is valid and complete
- [ ] `.mcp.json` configuration correctly uses `${CLAUDE_PLUGIN_ROOT}`
- [ ] `skills/` directory created with placeholder for skill files
- [ ] `agents/` directory created with `research/` subdirectory
- [ ] `hooks/` directory created with `check-dependencies.ps1` script
- [ ] `CLAUDE.md` plugin-specific instructions created
- [ ] `README.md` with prerequisites and installation instructions

---

## Specification References

| Document | Section | Relevance |
|----------|---------|-----------|
| [SPEC.md](../SPEC.md) | Repository Structure | Defines plugin location at `plugins/csharp-compounding-docs/` |
| [spec/marketplace.md](../spec/marketplace.md) | Plugin Manifest | `manifest.json` schema with skills, MCP servers, dependencies |
| [spec/marketplace.md](../spec/marketplace.md) | MCP Configuration | `.mcp.json` format and `${CLAUDE_PLUGIN_ROOT}` usage |
| [spec/marketplace.md](../spec/marketplace.md) | External MCP Prerequisites | `hooks/check-dependencies.ps1` implementation |

---

## Tasks

### Task 9.1: Create Base Plugin Directory Structure

Create the following directory hierarchy:

```
plugins/
└── csharp-compounding-docs/
    ├── .claude-plugin/
    │   ├── plugin.json
    │   └── hooks.json
    ├── .mcp.json
    ├── skills/
    │   └── .gitkeep
    ├── agents/
    │   └── research/
    │       └── .gitkeep
    ├── hooks/
    │   └── check-dependencies.ps1
    ├── scripts/
    │   └── launch-mcp-server.ps1
    ├── CLAUDE.md
    └── README.md
```

**Commands**:
```bash
mkdir -p plugins/csharp-compounding-docs/.claude-plugin
mkdir -p plugins/csharp-compounding-docs/skills
mkdir -p plugins/csharp-compounding-docs/agents/research
mkdir -p plugins/csharp-compounding-docs/hooks
mkdir -p plugins/csharp-compounding-docs/scripts
```

---

### Task 9.2: Create Plugin Manifest (plugin.json)

Create `plugins/csharp-compounding-docs/.claude-plugin/plugin.json`:

```json
{
  "$schema": "https://claude.ai/plugin-schema/v1",
  "name": "csharp-compounding-docs",
  "display_name": "CSharp Compound Docs",
  "version": "0.1.0",
  "description": "Capture and retrieve institutional knowledge with RAG-powered semantic search for C#/.NET projects",
  "author": {
    "name": "CSharp Compound Engineering",
    "url": "https://github.com/username/csharp-compound-engineering"
  },
  "repository": "https://github.com/username/csharp-compound-engineering",
  "license": "MIT",
  "keywords": [
    "csharp-compounding-docs",
    "knowledge-management",
    "rag",
    "semantic-search",
    "csharp",
    "dotnet"
  ],
  "claude_code_version": ">=1.0.0",
  "components": {
    "skills": [
      "cdocs:activate",
      "cdocs:problem",
      "cdocs:insight",
      "cdocs:codebase",
      "cdocs:tool",
      "cdocs:style",
      "cdocs:query",
      "cdocs:search",
      "cdocs:search-external",
      "cdocs:query-external",
      "cdocs:delete",
      "cdocs:promote",
      "cdocs:research",
      "cdocs:create-type",
      "cdocs:capture-select",
      "cdocs:todo",
      "cdocs:worktree"
    ],
    "mcp_servers": [
      {
        "name": "csharp-compounding-docs",
        "transport": "stdio",
        "executable": "scripts/launch-mcp-server.ps1"
      }
    ]
  },
  "dependencies": {
    "runtime": [
      "dotnet-10.0",
      "docker",
      "powershell-7"
    ]
  },
  "install": {
    "type": "git-clone",
    "url": "https://github.com/username/csharp-compound-engineering.git",
    "path": "plugins/csharp-compounding-docs"
  }
}
```

---

### Task 9.3: Create Hooks Configuration (hooks.json)

Create `plugins/csharp-compounding-docs/.claude-plugin/hooks.json`:

```json
{
  "hooks": {
    "SessionStart": [{
      "matcher": "*",
      "hooks": [{
        "type": "command",
        "command": "pwsh -File ${CLAUDE_PLUGIN_ROOT}/hooks/check-dependencies.ps1"
      }]
    }]
  }
}
```

---

### Task 9.4: Create MCP Configuration (.mcp.json)

Create `plugins/csharp-compounding-docs/.mcp.json`:

```json
{
  "mcpServers": {
    "csharp-compounding-docs": {
      "command": "pwsh",
      "args": [
        "-File",
        "${CLAUDE_PLUGIN_ROOT}/scripts/launch-mcp-server.ps1"
      ]
    }
  }
}
```

**Note**: The `${CLAUDE_PLUGIN_ROOT}` environment variable is resolved by Claude Code at runtime to the plugin's installation directory:
- User scope: `~/.claude/plugins/csharp-compounding-docs/`
- Project scope: `.claude/plugins/csharp-compounding-docs/`

---

### Task 9.5: Create Dependency Check Hook Script

Create `plugins/csharp-compounding-docs/hooks/check-dependencies.ps1`:

```powershell
#!/usr/bin/env pwsh
# Check for required MCP servers and warn if missing

$warnings = @()
$errors = @()

# Possible locations for Claude settings
$settingsPaths = @(
    (Join-Path $env:HOME ".claude" "settings.json"),
    (Join-Path $PWD ".claude" "settings.json")
)

$settings = $null
foreach ($path in $settingsPaths) {
    if (Test-Path $path) {
        $content = Get-Content $path -Raw | ConvertFrom-Json
        if ($content.mcpServers) {
            $settings = $content
            break
        }
    }
}

if ($null -eq $settings) {
    $errors += "No Claude settings.json found with MCP servers configured."
} else {
    # Check for Context7 (required)
    if (-not $settings.mcpServers.'context7') {
        $errors += @"
MISSING REQUIRED: Context7 MCP server not configured.
Add to your settings.json:
  "context7": { "type": "http", "url": "https://mcp.context7.com/mcp" }
"@
    }

    # Check for Microsoft Learn (required)
    if (-not $settings.mcpServers.'microsoft-learn') {
        $errors += @"
MISSING REQUIRED: Microsoft Learn MCP server not configured.
Add to your settings.json:
  "microsoft-learn": { "type": "sse", "url": "https://learn.microsoft.com/api/mcp" }
"@
    }

    # Check for Sequential Thinking (required)
    if (-not $settings.mcpServers.'sequential-thinking') {
        $errors += @"
MISSING REQUIRED: Sequential Thinking MCP server not configured.
Add to your settings.json:
  "sequential-thinking": { "command": "npx", "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"] }
"@
    }
}

# Display messages
if ($errors.Count -gt 0 -or $warnings.Count -gt 0) {
    Write-Host "`n=== CSharp Compound Docs Plugin ===" -ForegroundColor Cyan

    foreach ($err in $errors) {
        Write-Host $err -ForegroundColor Red
    }

    foreach ($warn in $warnings) {
        Write-Host $warn -ForegroundColor Yellow
    }

    Write-Host "==================================`n" -ForegroundColor Cyan
}
```

---

### Task 9.6: Create MCP Server Launch Script Placeholder

Create `plugins/csharp-compounding-docs/scripts/launch-mcp-server.ps1`:

```powershell
#!/usr/bin/env pwsh
# Launch the CSharp Compounding Docs MCP Server
# This script is invoked via ${CLAUDE_PLUGIN_ROOT}/scripts/launch-mcp-server.ps1

$ErrorActionPreference = "Stop"

# Determine the MCP server executable path
# The compiled server will be in the src/CompoundDocs.McpServer/bin directory
$scriptRoot = $PSScriptRoot
$pluginRoot = Split-Path -Parent $scriptRoot
$repoRoot = Split-Path -Parent (Split-Path -Parent $pluginRoot)
$serverPath = Join-Path $repoRoot "src" "CompoundDocs.McpServer" "bin" "Release" "net10.0" "CompoundDocs.McpServer"

if (-not (Test-Path $serverPath)) {
    # Try debug build
    $serverPath = Join-Path $repoRoot "src" "CompoundDocs.McpServer" "bin" "Debug" "net10.0" "CompoundDocs.McpServer"
}

if (-not (Test-Path $serverPath)) {
    Write-Error "MCP Server not found. Please build the solution first: dotnet build"
    exit 1
}

# Launch the MCP server with stdio transport
& $serverPath
```

---

### Task 9.7: Create Plugin CLAUDE.md

Create `plugins/csharp-compounding-docs/CLAUDE.md`:

```markdown
# CSharp Compound Docs Plugin - Claude Instructions

This file provides guidance to Claude Code when working within the csharp-compounding-docs plugin context.

## Plugin Overview

This plugin implements knowledge capture and retrieval for C#/.NET projects using:
- Markdown documentation stored in `./csharp-compounding-docs/`
- RAG-powered semantic search via bundled MCP server
- PostgreSQL + pgvector for vector storage
- Ollama for embeddings and generation

## Available Skills

| Skill | Purpose |
|-------|---------|
| `/cdocs:activate` | Initialize plugin for a project |
| `/cdocs:problem` | Capture problem/solution documentation |
| `/cdocs:insight` | Capture product/project insights |
| `/cdocs:codebase` | Document codebase knowledge |
| `/cdocs:tool` | Document tools and libraries |
| `/cdocs:style` | Capture coding styles and preferences |
| `/cdocs:query` | RAG query for synthesized answers |
| `/cdocs:search` | Semantic search for specific documents |
| `/cdocs:search-external` | Search external project documentation |
| `/cdocs:query-external` | RAG query against external docs |
| `/cdocs:delete` | Delete documents by project/branch |
| `/cdocs:promote` | Change document visibility level |
| `/cdocs:research` | Orchestrate research agents |
| `/cdocs:create-type` | Create new doc-type skills |
| `/cdocs:capture-select` | Resolve multi-trigger conflicts |
| `/cdocs:todo` | File-based todo tracking |
| `/cdocs:worktree` | Git worktree management |

## MCP Server Tools

The plugin's MCP server provides:
- `activate` - Initialize project context
- `rag_query` - Synthesize answers from documents
- `semantic_search` - Find relevant documents

## File Structure

- `skills/` - Skill YAML files with trigger patterns
- `agents/research/` - Research agent definitions
- `hooks/` - Session lifecycle hooks
- `scripts/` - PowerShell utility scripts
```

---

### Task 9.8: Create Plugin README.md

Create `plugins/csharp-compounding-docs/README.md`:

```markdown
# CSharp Compound Docs Plugin

Capture and retrieve institutional knowledge with RAG-powered semantic search for C#/.NET projects.

## Prerequisites

Before using this plugin, ensure the following MCP servers are configured in your
`~/.claude/settings.json` or project `.claude/settings.json`:

### Required MCP Servers

**Context7** - Framework documentation lookup
```json
{
  "mcpServers": {
    "context7": {
      "type": "http",
      "url": "https://mcp.context7.com/mcp"
    }
  }
}
```

**Microsoft Learn MCP** - .NET/C# documentation lookup
```json
{
  "mcpServers": {
    "microsoft-learn": {
      "type": "sse",
      "url": "https://learn.microsoft.com/api/mcp"
    }
  }
}
```

**Sequential Thinking MCP** - Complex multi-step reasoning
```json
{
  "mcpServers": {
    "sequential-thinking": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
    }
  }
}
```

### Runtime Dependencies

- .NET 10.0 SDK
- Docker Desktop (for PostgreSQL + Ollama infrastructure)
- PowerShell 7+

## Installation

### From Marketplace (Recommended)

```bash
# Interactive installation
/plugin install csharp-compounding-docs@csharp-compound-marketplace

# CLI installation
claude plugin install csharp-compounding-docs@csharp-compound-marketplace

# Project-scoped installation (team-shared)
claude plugin install csharp-compounding-docs@csharp-compound-marketplace --scope project
```

## Quick Start

1. Install the plugin (see above)
2. Configure required MCP servers in your settings.json
3. Start a Claude Code session in your C#/.NET project
4. Run `/cdocs:activate` to initialize the plugin
5. Use `/cdocs:problem`, `/cdocs:insight`, etc. to capture knowledge
6. Use `/cdocs:query` or `/cdocs:search` to retrieve knowledge

## Documentation

For full documentation, see the [SPEC.md](../../SPEC.md) in the repository root.

## License

MIT
```

---

### Task 9.9: Create .gitkeep Files

Create placeholder files to ensure empty directories are tracked:

```bash
touch plugins/csharp-compounding-docs/skills/.gitkeep
touch plugins/csharp-compounding-docs/agents/research/.gitkeep
```

---

## Verification Checklist

After completing all tasks, verify:

1. **Directory Structure**:
   ```bash
   tree plugins/csharp-compounding-docs/
   ```
   Expected output:
   ```
   plugins/csharp-compounding-docs/
   ├── .claude-plugin/
   │   ├── plugin.json
   │   └── hooks.json
   ├── .mcp.json
   ├── skills/
   │   └── .gitkeep
   ├── agents/
   │   └── research/
   │       └── .gitkeep
   ├── hooks/
   │   └── check-dependencies.ps1
   ├── scripts/
   │   └── launch-mcp-server.ps1
   ├── CLAUDE.md
   └── README.md
   ```

2. **JSON Validation**:
   ```bash
   # Validate plugin.json
   cat plugins/csharp-compounding-docs/.claude-plugin/plugin.json | jq .

   # Validate hooks.json
   cat plugins/csharp-compounding-docs/.claude-plugin/hooks.json | jq .

   # Validate .mcp.json
   cat plugins/csharp-compounding-docs/.mcp.json | jq .
   ```

3. **PowerShell Syntax**:
   ```bash
   pwsh -c "Test-Path plugins/csharp-compounding-docs/hooks/check-dependencies.ps1"
   pwsh -c "Test-Path plugins/csharp-compounding-docs/scripts/launch-mcp-server.ps1"
   ```

---

## Dependencies

| Phase | Dependency Type | Description |
|-------|-----------------|-------------|
| Phase 001 | Hard | Repository must be initialized first |
| Phase 010+ | Provides | Skills phases will populate `skills/` directory |
| Phase 011+ | Provides | Agent phases will populate `agents/research/` directory |

---

## Notes

- The `${CLAUDE_PLUGIN_ROOT}` environment variable is critical for path resolution in `.mcp.json` and `hooks.json`
- This phase creates the structure only; actual skill/agent content is created in subsequent phases
- The `check-dependencies.ps1` hook implements the "check and warn, never install" pattern per spec
- MCP server launch script assumes the compiled .NET server will be in the standard build output location

---

## Change Log

| Date | Changes |
|------|---------|
| 2025-01-24 | Initial phase creation |
