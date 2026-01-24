# Claude Code Plugin Installation Mechanism

**Research Date:** January 22, 2026
**Research Focus:** Native plugin installation, discovery, manifest format, MCP server registration, and custom marketplaces

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Plugin Discovery and Installation Flow](#1-plugin-discovery-and-installation-flow)
3. [Plugin Manifest Format (plugin.json)](#2-plugin-manifest-format-pluginjson)
4. [MCP Server Registration in Plugins](#3-mcp-server-registration-in-plugins)
5. [Marketplace Architecture](#4-marketplace-architecture)
6. [Custom Marketplace Integration](#5-custom-marketplace-integration)
7. [Configuration Files and Scopes](#6-configuration-files-and-scopes)
8. [CLI Commands Reference](#7-cli-commands-reference)
9. [Code Examples](#8-code-examples)
10. [Known Issues and Workarounds](#9-known-issues-and-workarounds)

---

## Executive Summary

Claude Code's native plugin installation mechanism provides a comprehensive system for discovering, installing, and managing plugins through both CLI commands and an interactive UI. The system supports:

- **Multi-scope installation** (user, project, local, managed)
- **Bundled MCP servers** that auto-register when plugins are enabled
- **Git-based marketplace discovery** with support for GitHub, GitLab, and Bitbucket
- **Automatic caching** of plugin files for consistent installations
- **Team configuration** via version-controlled settings files

**Key Insight:** There is no centralized marketplace API or registry. Claude Code's plugin discovery is based on Git repositories containing `marketplace.json` files that define available plugins and their sources.

---

## 1. Plugin Discovery and Installation Flow

### Step-by-Step Installation Process

```
1. DISCOVERY
   └── User searches for plugin via /plugin command
       └── Claude Code reads marketplace.json from registered marketplaces

2. MARKETPLACE RESOLUTION
   └── Git clone/fetch marketplace repository
       └── Parse .claude-plugin/marketplace.json
           └── Resolve plugin sources (relative paths, GitHub repos, git URLs)

3. INSTALLATION
   └── claude plugin install <plugin>@<marketplace> --scope <scope>
       └── Fetch plugin from source location
           └── Copy entire plugin directory to cache location

4. CACHE STORAGE
   └── Plugin files copied to:
       • User scope: ~/.claude/plugins/<plugin>/
       • Project scope: .claude/plugins/<plugin>/
       • Local scope: .claude/plugins/<plugin>/ (gitignored)

5. MANIFEST VALIDATION
   └── Validate .claude-plugin/plugin.json
       └── Check required fields (name)
           └── Validate component paths

6. COMPONENT REGISTRATION
   └── Auto-discover components:
       • commands/ directory
       • agents/ directory
       • skills/ directory
       • hooks/ directory or hooks.json
       • .mcp.json or inline mcpServers

7. MCP SERVER STARTUP
   └── When plugin is enabled:
       └── Start all MCP servers defined in plugin
           └── Register tools with Claude's toolkit

8. AVAILABILITY
   └── Plugin components available based on scope
       └── Commands appear in / menu
           └── MCP tools appear in tool list
```

### Installation Triggers

| Trigger | Action |
|---------|--------|
| `/plugin install <name>@<marketplace>` | Interactive UI installation |
| `claude plugin install <name>@<marketplace>` | CLI installation |
| `claude plugin install <name>@<marketplace> --scope project` | Team-shared installation |
| Startup | Enabled plugins auto-load components |

---

## 2. Plugin Manifest Format (plugin.json)

### Location

```
my-plugin/
├── .claude-plugin/
│   └── plugin.json     # Required manifest file
└── ...
```

### Schema Definition

```json
{
  "$schema": "https://code.claude.com/schemas/plugin.schema.json",

  // REQUIRED FIELDS
  "name": "plugin-name",          // kebab-case identifier, no spaces

  // METADATA FIELDS (Optional but Recommended)
  "version": "1.0.0",             // Semantic version
  "description": "Plugin description",
  "author": {
    "name": "Author Name",
    "email": "author@example.com",
    "url": "https://github.com/author"
  },
  "homepage": "https://docs.example.com/plugin",
  "repository": "https://github.com/author/plugin",
  "license": "MIT",
  "keywords": ["keyword1", "keyword2"],

  // COMPONENT PATH FIELDS (Optional)
  "commands": "./custom/commands/",       // String or array
  "agents": "./custom/agents/",           // String or array
  "skills": "./custom/skills/",           // String or array
  "hooks": "./config/hooks.json",         // String (path to hooks config)
  "mcpServers": "./mcp-config.json",      // String or inline object
  "lspServers": "./.lsp.json",            // String or inline object
  "outputStyles": "./styles/"             // String or array
}
```

### Required vs Optional Fields

| Field | Required | Type | Description |
|-------|----------|------|-------------|
| `name` | **Yes** | string | Unique plugin identifier |
| `version` | No | string | Semantic version (x.y.z) |
| `description` | No | string | Brief description |
| `author` | No | object | Author information |
| `homepage` | No | string | Documentation URL |
| `repository` | No | string | Source code URL |
| `license` | No | string | SPDX identifier |
| `keywords` | No | array | Search tags |
| `commands` | No | string/array | Command file paths |
| `agents` | No | string/array | Agent file paths |
| `skills` | No | string/array | Skill directory paths |
| `hooks` | No | string/object | Hooks config path or inline |
| `mcpServers` | No | string/object | MCP config path or inline |
| `lspServers` | No | string/object | LSP config path or inline |

### Path Resolution Rules

1. All paths **must** be relative to plugin root and start with `./`
2. Custom paths **supplement** default directories (they don't replace them)
3. If `commands/` exists at plugin root, it's loaded in addition to custom paths
4. Arrays allow multiple paths per component type

```json
{
  "name": "multi-path-plugin",
  "commands": [
    "./specialized/deploy.md",
    "./utilities/batch-process.md"
  ],
  "agents": [
    "./custom-agents/reviewer.md",
    "./custom-agents/tester.md"
  ]
}
```

---

## 3. MCP Server Registration in Plugins

### Auto-Registration Mechanism

When a plugin is enabled, Claude Code automatically:

1. Parses MCP server configuration from plugin
2. Starts all defined MCP servers
3. Registers server tools with Claude's toolkit
4. Makes tools available in the same session

### Method 1: Separate `.mcp.json` File (Recommended)

**Location:** Plugin root directory

```json
{
  "mcpServers": {
    "plugin-database": {
      "command": "${CLAUDE_PLUGIN_ROOT}/servers/db-server",
      "args": ["--config", "${CLAUDE_PLUGIN_ROOT}/config.json"],
      "env": {
        "DB_PATH": "${CLAUDE_PLUGIN_ROOT}/data"
      }
    },
    "plugin-api-client": {
      "command": "npx",
      "args": ["@company/mcp-server", "--plugin-mode"],
      "cwd": "${CLAUDE_PLUGIN_ROOT}"
    }
  }
}
```

### Method 2: Inline in plugin.json

```json
{
  "name": "my-plugin",
  "version": "1.0.0",
  "mcpServers": {
    "plugin-database": {
      "command": "${CLAUDE_PLUGIN_ROOT}/servers/db-server",
      "args": ["--config", "${CLAUDE_PLUGIN_ROOT}/config.json"],
      "env": {
        "DB_PATH": "${CLAUDE_PLUGIN_ROOT}/data"
      }
    }
  }
}
```

### Method 3: Reference External File

```json
{
  "name": "my-plugin",
  "mcpServers": "./mcp-config.json"
}
```

### MCP Server Configuration Schema

```json
{
  "mcpServers": {
    "<server-name>": {
      "command": "string",      // Executable command
      "args": ["array"],        // Command arguments
      "env": {                  // Environment variables
        "VAR_NAME": "value"
      },
      "cwd": "string"           // Working directory
    }
  }
}
```

### Environment Variable: `${CLAUDE_PLUGIN_ROOT}`

**Critical:** Always use `${CLAUDE_PLUGIN_ROOT}` for paths within plugins.

This variable resolves to the absolute path of the plugin's installation directory, ensuring correct path resolution regardless of:
- Installation method
- Operating system
- User preferences
- Scope (user/project/local)

**Example Usage:**
```json
{
  "mcpServers": {
    "my-server": {
      "command": "${CLAUDE_PLUGIN_ROOT}/bin/server",
      "args": [
        "--config", "${CLAUDE_PLUGIN_ROOT}/config/settings.json",
        "--data-dir", "${CLAUDE_PLUGIN_ROOT}/data"
      ],
      "env": {
        "PLUGIN_HOME": "${CLAUDE_PLUGIN_ROOT}",
        "LOG_PATH": "${CLAUDE_PLUGIN_ROOT}/logs"
      }
    }
  }
}
```

### Transport Types for Plugin MCP Servers

| Transport | Use Case | Example |
|-----------|----------|---------|
| stdio | Local executables | Bundled binaries |
| http | Remote services | Cloud APIs |
| npx | npm packages | `npx @org/server` |

---

## 4. Marketplace Architecture

### Marketplace File Structure

```
my-marketplace/
├── .claude-plugin/
│   └── marketplace.json        # Required: marketplace catalog
└── plugins/
    ├── plugin-a/
    │   ├── .claude-plugin/
    │   │   └── plugin.json
    │   ├── commands/
    │   ├── agents/
    │   └── .mcp.json
    └── plugin-b/
        └── ...
```

### marketplace.json Schema

```json
{
  // REQUIRED FIELDS
  "name": "marketplace-name",       // kebab-case identifier
  "owner": {
    "name": "Maintainer Name",      // Required
    "email": "contact@example.com"  // Optional
  },
  "plugins": [],                    // Array of plugin entries

  // OPTIONAL METADATA
  "metadata": {
    "description": "Marketplace description",
    "version": "1.0.0",
    "pluginRoot": "./plugins"
  }
}
```

### Plugin Entry Schema

```json
{
  "plugins": [
    {
      // REQUIRED
      "name": "plugin-name",
      "source": "./plugins/plugin-name",    // Or source object

      // OPTIONAL
      "description": "Plugin description",
      "version": "1.0.0",
      "author": {
        "name": "Author Name",
        "email": "author@example.com"
      },
      "homepage": "https://docs.example.com",
      "repository": "https://github.com/owner/repo",
      "license": "MIT",
      "keywords": ["tag1", "tag2"],
      "category": "productivity",
      "tags": ["search", "tag"],
      "strict": true,                       // Require plugin.json

      // COMPONENT OVERRIDES (optional)
      "commands": "./commands/",
      "agents": ["./agents/agent.md"],
      "hooks": {},
      "mcpServers": {},
      "lspServers": {}
    }
  ]
}
```

### Source Resolution Patterns

#### 1. Relative Paths (Same Repository)
```json
{
  "name": "local-plugin",
  "source": "./plugins/my-plugin"
}
```

#### 2. GitHub Repositories
```json
{
  "name": "github-plugin",
  "source": {
    "source": "github",
    "repo": "owner/plugin-repo",
    "ref": "v2.0.0",                        // Optional: branch/tag
    "sha": "a1b2c3d4e5f6..."               // Optional: commit hash
  }
}
```

#### 3. Git URLs (GitLab, Bitbucket, etc.)
```json
{
  "name": "git-plugin",
  "source": {
    "source": "url",
    "url": "https://gitlab.com/team/plugin.git",
    "ref": "main",
    "sha": "commit-hash"
  }
}
```

---

## 5. Custom Marketplace Integration

### How Custom Marketplaces Work

**There is no centralized marketplace API or registry.** Claude Code's marketplace system is entirely Git-based:

1. Marketplaces are Git repositories containing `marketplace.json`
2. Users add marketplaces by Git URL
3. Claude Code clones/fetches the repository
4. Plugin sources are resolved from the marketplace definition

### Adding a Custom Marketplace

```bash
# Add by GitHub repo
/plugin marketplace add username/marketplace-repo

# Add by Git URL
/plugin marketplace add https://gitlab.com/company/plugins.git

# Add local path (for testing)
/plugin marketplace add ./my-local-marketplace
```

### Marketplace Discovery Flow

```
1. User runs: /plugin marketplace add owner/repo

2. Claude Code:
   └── Clones repository (shallow clone)
       └── Locates .claude-plugin/marketplace.json
           └── Validates schema
               └── Stores marketplace reference in settings

3. User runs: /plugin (browse)
   └── Reads cached marketplace.json
       └── Displays available plugins

4. User selects plugin to install:
   └── Resolves plugin source
       └── Fetches plugin files
           └── Installs to scope
```

### Team Configuration

Add marketplace to version-controlled settings:

**.claude/settings.json**
```json
{
  "extraKnownMarketplaces": {
    "company-tools": {
      "source": {
        "source": "github",
        "repo": "your-org/claude-plugins"
      }
    }
  },
  "enabledPlugins": {
    "code-formatter@company-tools": true,
    "deployment-tools@company-tools": true
  }
}
```

### Private Repository Authentication

Set environment variables for private repositories:

| Provider | Variable | Token Type |
|----------|----------|------------|
| GitHub | `GITHUB_TOKEN` or `GH_TOKEN` | Personal access token |
| GitLab | `GITLAB_TOKEN` or `GL_TOKEN` | Personal access token |
| Bitbucket | `BITBUCKET_TOKEN` | App password |

```bash
export GITHUB_TOKEN=ghp_xxxxxxxxxxxxxxxxxxxx
```

### Enterprise Restrictions

Administrators can restrict marketplace sources:

**managed-settings.json**
```json
{
  "strictKnownMarketplaces": [
    {
      "source": "github",
      "repo": "acme-corp/approved-plugins"
    },
    {
      "source": "github",
      "repo": "acme-corp/security-tools",
      "ref": "v2.0"
    }
  ]
}
```

Empty array means complete lockdown (no marketplaces allowed).

### Reserved Marketplace Names

These names are blocked for third-party use:
- `claude-code-marketplace`
- `claude-code-plugins`
- `claude-plugins-official`
- `anthropic-marketplace`
- `anthropic-plugins`
- `agent-skills`
- `life-sciences`

---

## 6. Configuration Files and Scopes

### Installation Scopes

| Scope | Settings File | Use Case |
|-------|---------------|----------|
| `user` | `~/.claude/settings.json` | Personal plugins, all projects |
| `project` | `.claude/settings.json` | Team plugins (version controlled) |
| `local` | `.claude/settings.local.json` | Project-specific (gitignored) |
| `managed` | `managed-settings.json` | Admin-controlled (read-only) |

### File Locations

**User Configuration:**
```
~/.claude/
├── settings.json              # User settings
└── plugins/                   # User-scoped plugin cache
    └── <plugin-name>/
```

**Project Configuration:**
```
.claude/
├── settings.json              # Team settings (in git)
├── settings.local.json        # Local settings (gitignored)
└── plugins/                   # Project-scoped plugin cache
    └── <plugin-name>/
```

**Managed Configuration:**

| Platform | Path |
|----------|------|
| macOS | `/Library/Application Support/ClaudeCode/managed-settings.json` |
| Linux/WSL | `/etc/claude-code/managed-settings.json` |
| Windows | `C:\Program Files\ClaudeCode\managed-settings.json` |

### Settings File Structure

```json
{
  "extraKnownMarketplaces": {
    "marketplace-name": {
      "source": {
        "source": "github",
        "repo": "owner/repo"
      }
    }
  },
  "enabledPlugins": {
    "plugin-name@marketplace-name": true
  },
  "disabledPlugins": {
    "other-plugin@marketplace": true
  }
}
```

---

## 7. CLI Commands Reference

### Marketplace Commands

```bash
# Add a marketplace
claude plugin marketplace add <url-or-repo>
/plugin marketplace add <url-or-repo>

# List registered marketplaces
claude plugin marketplace list
/plugin marketplace list

# Update marketplace catalog
claude plugin marketplace update [index]
/plugin marketplace update [index]

# Remove a marketplace
claude plugin marketplace remove <index>
/plugin marketplace remove <index>
```

### Plugin Installation Commands

```bash
# Install plugin (default: user scope)
claude plugin install <plugin>@<marketplace>

# Install to specific scope
claude plugin install <plugin>@<marketplace> --scope user
claude plugin install <plugin>@<marketplace> --scope project
claude plugin install <plugin>@<marketplace> --scope local

# Aliases
claude plugin install <plugin>@<marketplace> -s project
```

### Plugin Management Commands

```bash
# List installed plugins
claude plugin list

# Enable/disable plugin
claude plugin enable <plugin>
claude plugin disable <plugin>

# Update plugin
claude plugin update <plugin>
claude plugin update              # Update all

# Remove plugin
claude plugin uninstall <plugin>
claude plugin remove <plugin>     # Alias
claude plugin rm <plugin>         # Alias
```

### Validation Commands

```bash
# Validate plugin structure
claude plugin validate .
/plugin validate .

# Validate marketplace
claude plugin validate ./marketplace-directory
```

### Interactive Commands

```bash
# Open plugin browser UI
/plugin

# Browse and install
/plugin > Discover

# Check MCP server status
/mcp
```

---

## 8. Code Examples

### Complete Plugin Example

**Directory Structure:**
```
enterprise-plugin/
├── .claude-plugin/
│   └── plugin.json
├── commands/
│   ├── deploy.md
│   └── rollback.md
├── agents/
│   └── security-reviewer.md
├── skills/
│   └── code-review/
│       └── SKILL.md
├── hooks/
│   └── hooks.json
├── servers/
│   └── db-server              # Compiled binary
├── scripts/
│   └── validate.sh
├── .mcp.json
├── LICENSE
├── CHANGELOG.md
└── README.md
```

**.claude-plugin/plugin.json:**
```json
{
  "name": "enterprise-plugin",
  "version": "2.1.0",
  "description": "Enterprise deployment and security tools",
  "author": {
    "name": "Enterprise Team",
    "email": "team@enterprise.com",
    "url": "https://github.com/enterprise"
  },
  "homepage": "https://docs.enterprise.com/plugin",
  "repository": "https://github.com/enterprise/plugin",
  "license": "MIT",
  "keywords": ["deployment", "security", "enterprise"],
  "commands": "./commands/",
  "agents": "./agents/",
  "skills": "./skills/",
  "hooks": "./hooks/hooks.json",
  "mcpServers": "./.mcp.json"
}
```

**.mcp.json:**
```json
{
  "mcpServers": {
    "enterprise-db": {
      "command": "${CLAUDE_PLUGIN_ROOT}/servers/db-server",
      "args": [
        "--config", "${CLAUDE_PLUGIN_ROOT}/config.json",
        "--port", "8080"
      ],
      "env": {
        "DB_PATH": "${CLAUDE_PLUGIN_ROOT}/data",
        "LOG_LEVEL": "info"
      }
    },
    "enterprise-api": {
      "command": "npx",
      "args": ["@enterprise/mcp-api-server", "--plugin-mode"],
      "cwd": "${CLAUDE_PLUGIN_ROOT}",
      "env": {
        "API_KEY": "${ENTERPRISE_API_KEY}"
      }
    }
  }
}
```

**hooks/hooks.json:**
```json
{
  "description": "Enterprise security hooks",
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "Write|Edit",
        "hooks": [
          {
            "type": "command",
            "command": "${CLAUDE_PLUGIN_ROOT}/scripts/validate.sh",
            "timeout": 30
          }
        ]
      }
    ],
    "PostToolUse": [
      {
        "matcher": "Bash",
        "hooks": [
          {
            "type": "command",
            "command": "${CLAUDE_PLUGIN_ROOT}/scripts/audit-log.sh"
          }
        ]
      }
    ]
  }
}
```

### Complete Marketplace Example

**.claude-plugin/marketplace.json:**
```json
{
  "name": "company-tools",
  "owner": {
    "name": "DevTools Team",
    "email": "devtools@company.com"
  },
  "metadata": {
    "description": "Company-wide development tools and MCP servers",
    "version": "2.0.0",
    "pluginRoot": "./plugins"
  },
  "plugins": [
    {
      "name": "code-formatter",
      "source": "./plugins/formatter",
      "description": "Automatic code formatting with company standards",
      "version": "2.1.0",
      "author": {
        "name": "DevTools Team"
      },
      "category": "code-quality",
      "keywords": ["formatting", "linting", "standards"],
      "mcpServers": {
        "formatter-server": {
          "command": "${CLAUDE_PLUGIN_ROOT}/bin/formatter",
          "args": ["--config", "${CLAUDE_PLUGIN_ROOT}/config.json"]
        }
      }
    },
    {
      "name": "deployment-tools",
      "source": {
        "source": "github",
        "repo": "company/deploy-plugin",
        "ref": "v3.0.0"
      },
      "description": "Kubernetes deployment automation",
      "version": "3.0.0",
      "category": "deployment",
      "keywords": ["deploy", "kubernetes", "automation"]
    },
    {
      "name": "security-scanner",
      "source": {
        "source": "url",
        "url": "https://gitlab.company.com/security/scanner-plugin.git",
        "ref": "main"
      },
      "description": "Security vulnerability scanning",
      "version": "1.5.0",
      "category": "security"
    }
  ]
}
```

### Team Settings Configuration

**.claude/settings.json (version controlled):**
```json
{
  "extraKnownMarketplaces": {
    "company-tools": {
      "source": {
        "source": "github",
        "repo": "company/claude-plugins"
      }
    },
    "security-tools": {
      "source": {
        "source": "github",
        "repo": "company/security-plugins",
        "ref": "v2.0"
      }
    }
  },
  "enabledPlugins": {
    "code-formatter@company-tools": true,
    "security-scanner@security-tools": true
  }
}
```

---

## 9. Known Issues and Workarounds

### Issue 1: Inline mcpServers in plugin.json Ignored

**Problem:** MCP servers defined inline in `plugin.json` using the `mcpServers` field may be ignored during manifest parsing.

**Workaround:** Use a separate `.mcp.json` file at the plugin root instead of inline configuration.

```json
// Instead of this in plugin.json:
{
  "name": "my-plugin",
  "mcpServers": { ... }  // May be ignored
}

// Use separate .mcp.json file:
{
  "mcpServers": { ... }  // Works correctly
}
```

### Issue 2: Path Traversal After Installation

**Problem:** Plugins cannot reference files outside their copied directory structure. Paths using `../` fail after installation because plugins are copied to a cache location.

**Workaround Options:**

1. **Use Symlinks:**
   ```bash
   # Inside plugin directory before distribution
   ln -s /path/to/shared-utils ./shared-utils
   ```
   Symlinked content is copied into the plugin cache.

2. **Restructure Marketplace:**
   ```json
   {
     "name": "my-plugin",
     "source": "./",
     "commands": ["./plugins/my-plugin/commands/"],
     "strict": false
   }
   ```

### Issue 3: npm-based MCP Servers Require Node.js

**Problem:** MCP servers using `npx` commands require Node.js to be installed.

**Solution:** Ensure Node.js is installed, or use compiled binaries instead of npm packages for wider compatibility.

### Issue 4: Plugin Updates Not Reflected

**Problem:** Changes to plugins may not be reflected immediately.

**Solution:**
```bash
# Force update
claude plugin update <plugin-name>

# Or reinstall
claude plugin uninstall <plugin-name>
claude plugin install <plugin-name>@<marketplace>
```

---

## Sources

### Official Documentation
- [Claude Code Plugins Reference](https://code.claude.com/docs/en/plugins-reference)
- [Plugin Marketplaces Documentation](https://code.claude.com/docs/en/plugin-marketplaces)
- [MCP Integration Guide](https://code.claude.com/docs/en/mcp)
- [Hooks Reference](https://code.claude.com/docs/en/hooks)

### GitHub Repositories
- [anthropics/claude-code](https://github.com/anthropics/claude-code)
- [anthropics/claude-plugins-official](https://github.com/anthropics/claude-plugins-official)
- [modelcontextprotocol/servers](https://github.com/modelcontextprotocol/servers)

### Community Resources
- [Claude Code Plugin CLI: The Missing Manual](https://medium.com/@garyjarrel/claude-code-plugin-cli-the-missing-manual-0a4d3a7c99ce)
- [Claude Plugins Dev](https://claude-plugins.dev/)
- [MCPcat - MCP Server Guide](https://mcpcat.io/guides/adding-an-mcp-server-to-claude-code/)
- [FastMCP - Claude Code Integration](https://gofastmcp.com/integrations/claude-code)

### Related Research
- [claude-code-plugin-architecture-research.md](./claude-code-plugin-architecture-research.md)
- [claude-plugin-marketplace-ecosystem-research.md](./claude-plugin-marketplace-ecosystem-research.md)
- [custom-claude-plugin-marketplace-research.md](./custom-claude-plugin-marketplace-research.md)
