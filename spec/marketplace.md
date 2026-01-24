# Plugin Marketplace Specification

> **Status**: [DRAFT]
> **Parent**: [SPEC.md](../SPEC.md)

---

## Overview

The plugin will be published to a custom plugin marketplace hosted on GitHub Pages within the same repository.

---

## Marketplace Architecture

> **Background**: Comprehensive analysis of GitHub Pages hosting for plugin marketplaces, including CORS configuration, JSON API patterns, deployment workflows, and CI/CD automation. See [GitHub Pages Plugin Marketplace Research](../research/github-pages-plugin-marketplace-research.md).

### Hosting

- **Platform**: GitHub Pages
- **URL Pattern**: `https://{username}.github.io/csharp-compound-engineering/`
- **Source**: `./marketplace/` directory or `gh-pages` branch

### Structure

```
marketplace/
├── index.html              # Marketplace landing page
├── plugins/
│   └── csharp-compounding-docs/
│       ├── manifest.json   # Plugin metadata
│       ├── README.md       # Plugin documentation
│       └── versions/
│           ├── 1.0.0/
│           │   └── plugin.zip
│           └── latest -> 1.0.0
├── api/
│   └── plugins.json        # Plugin registry
└── assets/
    ├── css/
    └── images/
```

---

## Plugin Manifest

> **Background**: Detailed coverage of plugin manifest schemas, marketplace.json formats, plugin entry fields, and source resolution patterns used by Claude Code. See [Claude Plugin Marketplace Ecosystem Research](../research/claude-plugin-marketplace-ecosystem-research.md).

### `manifest.json`

```json
{
  "$schema": "https://claude.ai/plugin-schema/v1",
  "name": "csharp-compounding-docs",
  "display_name": "CSharp Compound Docs",
  "version": "1.0.0",
  "description": "Capture and retrieve institutional knowledge with RAG-powered semantic search for C#/.NET projects",
  "author": {
    "name": "Your Name",
    "url": "https://github.com/username"
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

## Plugin Registry

### `api/plugins.json`

```json
{
  "version": "1.0",
  "updated": "2025-01-22T00:00:00Z",
  "plugins": [
    {
      "id": "csharp-compounding-docs",
      "name": "C# Compound Engineering Docs",
      "version": "1.0.0",
      "description": "Capture and retrieve institutional knowledge with RAG-powered semantic search",
      "manifest_url": "/plugins/csharp-compounding-docs/manifest.json",
      "download_url": "/plugins/csharp-compounding-docs/versions/latest/plugin.zip",
      "stars": 0,
      "downloads": 0
    }
  ]
}
```

---

## Marketplace Landing Page

### Features

1. **Plugin listing** - Browse available plugins
2. **Search** - Filter by keyword, category
3. **Plugin details** - View manifest, README, version history
4. **Installation instructions** - Copy-paste commands

### Design Considerations

- Static site (no backend)
- Simple, clean interface
- Dark mode support (matches Claude Code aesthetic)
- Mobile-responsive

### Resolved Technology Decision

**Use Nextra** (Next.js-based static site generator)

**Rationale** (see [research/static-site-generator-marketplace-research.md](../research/static-site-generator-marketplace-research.md)):
- Modern React/MDX development experience
- Built-in full-text search
- MDX support for interactive plugin components
- Excellent dark mode support
- GitHub Pages compatible via static export

**Deployment**: Static export to GitHub Pages via `next export`

**Alternatives Considered**:
- Plain HTML/CSS/JS - Too manual for 50+ plugins
- Jekyll - Works but dated DX
- Hugo - Fast but Go ecosystem

---

## Installation Flow

### From Marketplace (Recommended)

Use Claude Code's native plugin installation command:

```bash
# Interactive UI installation
/plugin install csharp-compounding-docs@csharp-compound-marketplace

# CLI installation
claude plugin install csharp-compounding-docs@csharp-compound-marketplace

# Install at project scope (team-shared)
claude plugin install csharp-compounding-docs@csharp-compound-marketplace --scope project
```

### Plugin Updates

Use Claude Code's native update command:

```bash
# Update specific plugin
claude plugin update csharp-compounding-docs

# Update all plugins
claude plugin update
```

**Note**: Git clone is NOT supported or documented. Use the native plugin installation mechanism for proper version management and updates.

Reference: [Claude Code Plugin Installation Mechanism](../research/claude-code-plugin-installation-mechanism.md)

### MCP Configuration

> **Background**: In-depth documentation of MCP server registration, transport types, configuration scopes, and the `${CLAUDE_PLUGIN_ROOT}` environment variable. See [Claude Code Plugin Architecture Research](../research/claude-code-plugin-architecture-research.md).

The plugin includes a `.mcp.json` file at the plugin root that registers the plugin's own MCP server:

**Plugin `.mcp.json`**:
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

**Note**: `${CLAUDE_PLUGIN_ROOT}` resolves to the plugin's installation directory (e.g., `~/.claude/plugins/csharp-compounding-docs/` for user scope or `.claude/plugins/csharp-compounding-docs/` for project scope). See [Plugin Installation Research](../research/claude-code-plugin-installation-mechanism.md#environment-variable-claude_plugin_root) for details.

---

## External MCP Server Prerequisites

> **Background**: Full guide for building custom Claude Code plugin marketplaces, including plugin submission workflows, GitHub Actions automation, directory structures, and security considerations. See [Custom Claude Plugin Marketplace Research](../research/custom-claude-plugin-marketplace-research.md).

### Architectural Decision: Check and Warn, Never Install

**IMPORTANT**: This plugin does NOT install, manage, or bundle external MCP servers. The plugin:
1. **Checks** if required MCP servers are configured (via hook)
2. **Warns** the user if any are missing
3. **Assumes** all MCP servers are available throughout skill execution

This separation of concerns means:
- Users are responsible for installing and configuring their own MCP servers
- The plugin never modifies user's MCP configuration
- Skills can freely use MCP tools without defensive checks

### User Prerequisites

Before using this plugin, users must manually configure these MCP servers in their `~/.claude/settings.json`:

| MCP Server | Purpose | Required |
|------------|---------|----------|
| **Context7** | Framework documentation lookup | Yes |
| **Microsoft Docs** | .NET/C# documentation | Yes |
| **Sequential Thinking** | Complex multi-step reasoning and analysis | Yes |

### SessionStart Hook: Check and Warn

The plugin uses a `SessionStart` hook that runs on every session to verify prerequisites. If MCP servers are missing, it displays a warning with configuration instructions. **It does NOT attempt to install or configure them.**

**`hooks.json`**:
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

**`hooks/check-dependencies.ps1`**:
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

### Documentation in README

The plugin README prominently lists required dependencies:

```markdown
## Prerequisites

Before using this plugin, ensure the following MCP servers are configured in your
`~/.claude/settings.json` or project `.claude/settings.json`:

### Required

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

**Microsoft Learn MCP** - .NET/C# documentation lookup (remote HTTP endpoint)
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
> **Note**: Microsoft Learn MCP Server is a remote service - no npm package required. See [Microsoft Learn MCP Server](https://learn.microsoft.com/en-us/training/support/mcp-release-notes) for details.

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
```

### Future: Formal Dependency System

When Claude Code implements [Plugin Dependencies (Issue #9444)](https://github.com/anthropics/claude-code/issues/9444), the plugin will be updated to use a formal dependency declaration that can prompt or block installation.

---

## Release Process

### Versioning

Follow semantic versioning:
- **MAJOR**: Breaking changes to skills or MCP tools
- **MINOR**: New features, new doc-types
- **PATCH**: Bug fixes, documentation

### Release Steps

1. Update version in:
   - `manifest.json`
   - MCP server project
   - CHANGELOG.md

2. Create release package:
   ```bash
   # Package plugin for distribution
   ./scripts/package-release.ps1 -Version 1.0.0
   ```

3. Copy to marketplace:
   ```bash
   cp release/plugin-1.0.0.zip marketplace/plugins/csharp-compounding-docs/versions/1.0.0/
   ln -sf 1.0.0 marketplace/plugins/csharp-compounding-docs/versions/latest
   ```

4. Update `api/plugins.json` with new version

5. Commit and push to trigger GitHub Pages deploy

### GitHub Actions Automation

```yaml
# .github/workflows/release.yml
name: Release Plugin

on:
  push:
    tags:
      - 'v*'

jobs:
  release:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Build MCP Server
        run: dotnet publish -c Release

      - name: Package Plugin
        run: pwsh ./scripts/package-release.ps1 -Version ${{ github.ref_name }}

      - name: Update Marketplace
        run: |
          # Copy package to marketplace
          # Update plugins.json
          # Commit and push to gh-pages
```

---

## Future Enhancements

### Plugin Analytics

- Track downloads (via GitHub releases API)
- User ratings/reviews (would require backend)
- Usage statistics (opt-in telemetry)

### Plugin Discovery

- Categories (knowledge-management, productivity, etc.)
- Featured plugins
- Recently updated

### Multi-Plugin Marketplace

The marketplace structure supports hosting multiple plugins:

```
marketplace/
├── plugins/
│   ├── csharp-compounding-docs/
│   ├── another-plugin/
│   └── third-plugin/
└── api/
    └── plugins.json  # Lists all plugins
```

---

## Open Questions

1. ~~Should installation be automated via a Claude Code command?~~ **Resolved**: Yes - use native `claude plugin install` command
2. ~~How to handle plugin updates for users who cloned?~~ **Resolved**: Git clone not supported - use native `claude plugin update` command
3. Should there be a "plugin store" skill within Claude Code?
4. GitHub releases vs marketplace downloads - which to prioritize?

