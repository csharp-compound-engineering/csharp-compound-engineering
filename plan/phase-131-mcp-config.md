# Phase 131: Plugin .mcp.json Configuration

> **Category**: Marketplace & Deployment
> **Prerequisites**: Phase 009 (Plugin Directory Structure)
> **Estimated Effort**: 1-2 hours
> **Status**: Pending

---

## Objective

Create and validate the `.mcp.json` configuration file that registers the plugin's MCP server with Claude Code. This configuration enables Claude Code to discover, launch, and communicate with the CSharp Compounding Docs MCP server using stdio transport.

---

## Success Criteria

- [ ] `.mcp.json` file exists at plugin root with valid JSON syntax
- [ ] `mcpServers` object correctly declares the `csharp-compounding-docs` server
- [ ] Server command uses `pwsh` for cross-platform PowerShell execution
- [ ] `${CLAUDE_PLUGIN_ROOT}` variable correctly references the launch script
- [ ] Configuration validates against Claude Code's expected schema
- [ ] MCP server launches successfully when Claude Code processes the configuration
- [ ] Environment variable expansion works correctly at runtime

---

## Specification References

| Document | Section | Relevance |
|----------|---------|-----------|
| [spec/marketplace.md](../spec/marketplace.md) | MCP Configuration | Defines `.mcp.json` format and `${CLAUDE_PLUGIN_ROOT}` usage |
| [research/claude-code-plugin-architecture-research.md](../research/claude-code-plugin-architecture-research.md) | Plugin Configuration Files | Details on configuration file structure and scopes |
| [research/claude-code-plugin-architecture-research.md](../research/claude-code-plugin-architecture-research.md) | MCP Servers as Plugins | Transport types and server registration |

---

## Background

### What is `.mcp.json`?

The `.mcp.json` file is a project-level configuration file that declares MCP servers to Claude Code. When placed at the root of a plugin, it registers the plugin's MCP server(s) for automatic discovery and launch.

### File Location and Scope

| Scope | Location | Description |
|-------|----------|-------------|
| **Plugin** | `plugins/csharp-compounding-docs/.mcp.json` | Plugin-bundled server (this phase) |
| **Project** | `.mcp.json` (project root) | Team-shared servers (version controlled) |
| **User** | `~/.claude.json` | Personal cross-project servers |
| **Managed** | System directories | Enterprise-deployed servers |

### Transport Types

The plugin uses **stdio transport** which:
- Launches a local process
- Communicates via stdin/stdout streams
- Provides lowest latency for local tools
- Requires the executable to be available in the plugin directory

---

## Tasks

### Task 131.1: Create the .mcp.json Configuration File

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

**Configuration Fields**:

| Field | Value | Description |
|-------|-------|-------------|
| `mcpServers` | Object | Container for all MCP server declarations |
| `csharp-compounding-docs` | Object | Server name (used as identifier in Claude Code) |
| `command` | `"pwsh"` | PowerShell 7+ executable (cross-platform) |
| `args` | Array | Arguments passed to the command |
| `args[0]` | `"-File"` | PowerShell flag to execute a script file |
| `args[1]` | `"${CLAUDE_PLUGIN_ROOT}/scripts/launch-mcp-server.ps1"` | Path to launch script with variable |

---

### Task 131.2: Understand ${CLAUDE_PLUGIN_ROOT} Resolution

The `${CLAUDE_PLUGIN_ROOT}` environment variable is resolved by Claude Code at runtime to the plugin's actual installation directory.

**Resolution Examples**:

| Installation Scope | Resolved Path |
|-------------------|---------------|
| User scope | `~/.claude/plugins/csharp-compounding-docs/` |
| Project scope | `.claude/plugins/csharp-compounding-docs/` |

**Important Considerations**:

1. **Never hardcode paths** - Always use `${CLAUDE_PLUGIN_ROOT}` for portability
2. **Variable is resolved before process spawn** - The MCP server receives the expanded path
3. **Works with nested paths** - `${CLAUDE_PLUGIN_ROOT}/scripts/launch-mcp-server.ps1` expands correctly
4. **Case-sensitive** - Must be exactly `${CLAUDE_PLUGIN_ROOT}` (not lowercase)

---

### Task 131.3: Validate JSON Syntax

Validate the configuration file is well-formed JSON:

```bash
# Using jq
cat plugins/csharp-compounding-docs/.mcp.json | jq .

# Using Python
python3 -c "import json; json.load(open('plugins/csharp-compounding-docs/.mcp.json'))"

# Using PowerShell
Get-Content plugins/csharp-compounding-docs/.mcp.json | ConvertFrom-Json
```

**Expected Output** (formatted):

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

---

### Task 131.4: Document Environment Variable Support

The `.mcp.json` format supports environment variable expansion with optional defaults:

**Syntax Reference**:

| Syntax | Description | Example |
|--------|-------------|---------|
| `${VAR}` | Expand variable | `${CLAUDE_PLUGIN_ROOT}` |
| `${VAR:-default}` | Use default if unset | `${DB_HOST:-localhost}` |

**Plugin-Specific Variables**:

| Variable | Description |
|----------|-------------|
| `${CLAUDE_PLUGIN_ROOT}` | Plugin installation directory |
| `${CLAUDE_PROJECT_DIR}` | Current project directory (available in hooks) |
| `${CLAUDE_SESSION_ID}` | Current session ID (available in skills) |

**Example with Optional Environment Variables**:

```json
{
  "mcpServers": {
    "csharp-compounding-docs": {
      "command": "pwsh",
      "args": [
        "-File",
        "${CLAUDE_PLUGIN_ROOT}/scripts/launch-mcp-server.ps1"
      ],
      "env": {
        "CDOCS_LOG_LEVEL": "${CDOCS_LOG_LEVEL:-Information}",
        "CDOCS_CONFIG_PATH": "${CLAUDE_PLUGIN_ROOT}/config/default.json"
      }
    }
  }
}
```

**Note**: The `env` block is optional. For this phase, we use only the base configuration without additional environment variables.

---

### Task 131.5: Alternative Configuration Patterns

Document alternative configuration patterns for reference:

**Pattern A: Direct Executable (No PowerShell)**

If the MCP server is compiled to a native executable:

```json
{
  "mcpServers": {
    "csharp-compounding-docs": {
      "command": "${CLAUDE_PLUGIN_ROOT}/bin/CompoundDocs.McpServer",
      "args": []
    }
  }
}
```

**Pattern B: Dotnet Run**

For development/debug scenarios:

```json
{
  "mcpServers": {
    "csharp-compounding-docs": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "${CLAUDE_PLUGIN_ROOT}/../../../src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj",
        "--no-build"
      ]
    }
  }
}
```

**Pattern C: With Explicit Working Directory**

Note: Claude Code does not support `cwd` in `.mcp.json`. The launch script must handle directory changes if needed.

**Chosen Pattern**: PowerShell launcher script (Task 131.1) provides flexibility and cross-platform compatibility.

---

### Task 131.6: Verify Launch Script Exists

Ensure the referenced launch script exists (created in Phase 009):

```bash
# Check script exists
test -f plugins/csharp-compounding-docs/scripts/launch-mcp-server.ps1 && echo "Script exists" || echo "Script missing"

# Verify script is executable (Unix/macOS)
chmod +x plugins/csharp-compounding-docs/scripts/launch-mcp-server.ps1 2>/dev/null || true
```

**Launch Script Requirements**:

The `launch-mcp-server.ps1` script (created in Phase 009, Task 9.6) must:

1. Locate the compiled MCP server executable
2. Handle both Release and Debug build configurations
3. Exit with error if server not found
4. Launch the server process (which connects via stdio)

---

### Task 131.7: Integration Test with Claude Code

After plugin installation, verify MCP server registration:

```bash
# List all configured MCP servers
claude mcp list

# Check for the plugin's server
claude mcp list | grep csharp-compounding-docs

# Get detailed server configuration
claude mcp get csharp-compounding-docs
```

**Expected Output**:

```
csharp-compounding-docs (stdio)
  Command: pwsh
  Args: ["-File", "/path/to/plugin/scripts/launch-mcp-server.ps1"]
  Status: Configured
```

**Interactive Verification**:

In a Claude Code session:
1. Type `/mcp` to open the MCP status menu
2. Verify `csharp-compounding-docs` appears in the list
3. Check status shows "Connected" or "Ready"

---

## Verification Checklist

After completing all tasks, verify:

1. **File Exists**:
   ```bash
   ls -la plugins/csharp-compounding-docs/.mcp.json
   ```

2. **Valid JSON**:
   ```bash
   jq . plugins/csharp-compounding-docs/.mcp.json
   ```

3. **Correct Structure**:
   ```bash
   jq '.mcpServers["csharp-compounding-docs"]' plugins/csharp-compounding-docs/.mcp.json
   ```
   Expected output:
   ```json
   {
     "command": "pwsh",
     "args": [
       "-File",
       "${CLAUDE_PLUGIN_ROOT}/scripts/launch-mcp-server.ps1"
     ]
   }
   ```

4. **Variable Not Prematurely Expanded**:
   ```bash
   grep '${CLAUDE_PLUGIN_ROOT}' plugins/csharp-compounding-docs/.mcp.json && echo "Variable present"
   ```

5. **Launch Script Referenced**:
   ```bash
   jq -r '.mcpServers["csharp-compounding-docs"].args[1]' plugins/csharp-compounding-docs/.mcp.json
   ```
   Expected: `${CLAUDE_PLUGIN_ROOT}/scripts/launch-mcp-server.ps1`

---

## Dependencies

| Phase | Dependency Type | Description |
|-------|-----------------|-------------|
| Phase 009 | Hard | Plugin directory and launch script must exist |
| Phase 021 | Soft | MCP server project must be built for runtime testing |
| Phase 022 | Soft | stdio transport implementation enables actual communication |

---

## Troubleshooting

### Server Not Appearing in `claude mcp list`

1. Verify `.mcp.json` is at plugin root, not nested
2. Check JSON syntax with `jq`
3. Ensure plugin is properly installed/registered

### Server Shows "Disconnected" Status

1. Verify `pwsh` is available in PATH: `which pwsh`
2. Check launch script exists at referenced path
3. Run launch script manually to check for errors:
   ```bash
   pwsh -File plugins/csharp-compounding-docs/scripts/launch-mcp-server.ps1
   ```

### Variable Not Expanding

1. Ensure variable syntax is exactly `${CLAUDE_PLUGIN_ROOT}`
2. Variable only expands when Claude Code processes the config
3. For debugging, temporarily hardcode path and test

### Permission Errors

1. On Unix/macOS, ensure launch script is executable
2. Check file permissions: `ls -la plugins/csharp-compounding-docs/scripts/`
3. Add execute permission: `chmod +x scripts/launch-mcp-server.ps1`

---

## Notes

- The `.mcp.json` file is discovered automatically when Claude Code loads the plugin
- Server name (`csharp-compounding-docs`) must be unique across all installed plugins
- The configuration is read-only; runtime modifications require editing the file
- For development, use `claude mcp add` to temporarily override configuration
- The stdio transport requires the MCP server to remain running for the session duration

---

## Change Log

| Date | Changes |
|------|---------|
| 2025-01-24 | Initial phase creation |
