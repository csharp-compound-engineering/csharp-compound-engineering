# Phase 130: Plugin Installation Flow

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Marketplace & Deployment
> **Prerequisites**: Phase 127 (Marketplace Landing Page)

---

## Spec References

This phase implements the installation flow defined in:

- **spec/marketplace.md** - [Installation Flow](../spec/marketplace.md#installation-flow) - Native plugin installation commands
- **research/claude-code-plugin-installation-mechanism.md** - [Plugin Discovery and Installation Flow](../research/claude-code-plugin-installation-mechanism.md#1-plugin-discovery-and-installation-flow) - Complete installation mechanism documentation

---

## Background

> **Key Insight**: There is no centralized marketplace API or registry. Claude Code's plugin discovery is Git-based, using repositories containing `marketplace.json` files that define available plugins and their sources.

The CSharp Compound Docs plugin uses Claude Code's native plugin installation mechanism rather than manual git clone operations. This ensures:

- Proper version management through the plugin system
- Automatic MCP server registration when the plugin is enabled
- Consistent installation across user, project, and local scopes
- Built-in update capabilities via `claude plugin update`

**Critical Design Decision**: Git clone is explicitly NOT supported or documented. All installations must use the native `claude plugin install` command for proper plugin lifecycle management.

---

## Objectives

1. Document the official installation command sequence
2. Implement post-installation validation
3. Configure update mechanisms for the plugin
4. Create user-facing installation documentation
5. Ensure MCP server auto-registration works on install
6. Support both user-scope and project-scope installations

---

## Acceptance Criteria

### Installation Command Usage

- [ ] `claude plugin install csharp-compounding-docs@csharp-compound-marketplace` works for user scope (default)
- [ ] `claude plugin install csharp-compounding-docs@csharp-compound-marketplace --scope project` works for team-shared installation
- [ ] `claude plugin install csharp-compounding-docs@csharp-compound-marketplace --scope local` works for gitignored local installation
- [ ] Interactive UI installation via `/plugin install csharp-compounding-docs@csharp-compound-marketplace` works

### Update Command Usage

- [ ] `claude plugin update csharp-compounding-docs` updates the specific plugin
- [ ] `claude plugin update` updates all plugins including this one
- [ ] Version changes are reflected after update
- [ ] MCP servers restart with updated configuration after update

### Installation Validation

- [ ] Plugin files are copied to correct cache location:
  - User scope: `~/.claude/plugins/csharp-compounding-docs/`
  - Project scope: `.claude/plugins/csharp-compounding-docs/`
  - Local scope: `.claude/plugins/csharp-compounding-docs/` (gitignored)
- [ ] `.claude-plugin/plugin.json` is validated during installation
- [ ] All component paths resolve correctly via `${CLAUDE_PLUGIN_ROOT}`
- [ ] MCP server configuration in `.mcp.json` is registered automatically

### Git Clone NOT Supported

- [ ] README explicitly states "Do NOT use git clone"
- [ ] No git clone instructions appear in any documentation
- [ ] Troubleshooting section addresses users who incorrectly git cloned
- [ ] Migration path documented for users who previously git cloned

### Post-Installation Setup Steps

- [ ] SessionStart hook runs on first session after install
- [ ] Hook checks for required external MCP servers (Context7, Microsoft Learn, Sequential Thinking)
- [ ] Warning messages display for missing dependencies
- [ ] User can verify installation via `claude plugin list`
- [ ] Plugin appears as enabled in `/plugin` UI

### Documentation Requirements

- [ ] Installation section in README.md with exact commands
- [ ] Scope selection guidance (when to use user vs project vs local)
- [ ] Post-installation verification steps
- [ ] Troubleshooting common installation issues

---

## Implementation Notes

### Installation Commands Reference

```bash
# === PRIMARY INSTALLATION METHODS ===

# 1. User Scope (default) - Personal installation, all projects
claude plugin install csharp-compounding-docs@csharp-compound-marketplace

# 2. Project Scope - Team-shared, version controlled in .claude/settings.json
claude plugin install csharp-compounding-docs@csharp-compound-marketplace --scope project

# 3. Local Scope - Project-specific, gitignored (.claude/settings.local.json)
claude plugin install csharp-compounding-docs@csharp-compound-marketplace --scope local

# === INTERACTIVE INSTALLATION ===

# From within Claude Code session:
/plugin install csharp-compounding-docs@csharp-compound-marketplace

# Or browse and select:
/plugin
# Navigate to: Discover > csharp-compound-marketplace > csharp-compounding-docs
```

### Update Commands Reference

```bash
# === UPDATE COMMANDS ===

# Update specific plugin
claude plugin update csharp-compounding-docs

# Update all installed plugins
claude plugin update

# === VERIFICATION COMMANDS ===

# List installed plugins
claude plugin list

# Check plugin status (within Claude Code session)
/plugin

# Check MCP server status
/mcp
```

### Installation Flow Diagram

```
USER INITIATES INSTALL
        |
        v
claude plugin install csharp-compounding-docs@csharp-compound-marketplace
        |
        v
MARKETPLACE RESOLUTION
  1. Fetch marketplace.json from csharp-compound-marketplace
  2. Locate plugin entry for csharp-compounding-docs
  3. Resolve source (relative path in marketplace repo)
        |
        v
PLUGIN FETCH
  1. Copy plugin files from marketplace source
  2. Validate .claude-plugin/plugin.json exists
  3. Check required 'name' field present
        |
        v
CACHE STORAGE
  User scope:    ~/.claude/plugins/csharp-compounding-docs/
  Project scope: .claude/plugins/csharp-compounding-docs/
  Local scope:   .claude/plugins/csharp-compounding-docs/ (gitignored)
        |
        v
COMPONENT REGISTRATION
  1. Discover skills/ directory
  2. Parse hooks/hooks.json
  3. Load .mcp.json for MCP servers
        |
        v
SETTINGS UPDATE
  Add to enabledPlugins in appropriate settings file:
  {
    "enabledPlugins": {
      "csharp-compounding-docs@csharp-compound-marketplace": true
    }
  }
        |
        v
MCP SERVER STARTUP (on next session)
  1. Start csharp-compounding-docs MCP server
  2. Register tools with Claude's toolkit
        |
        v
SESSIONSTART HOOK (on next session)
  1. Check for required external MCP servers
  2. Display warnings for missing dependencies
        |
        v
INSTALLATION COMPLETE
```

### Cache Location by Scope

| Scope | Settings File | Plugin Cache | Use Case |
|-------|---------------|--------------|----------|
| `user` | `~/.claude/settings.json` | `~/.claude/plugins/` | Personal, all projects |
| `project` | `.claude/settings.json` | `.claude/plugins/` | Team-shared (in git) |
| `local` | `.claude/settings.local.json` | `.claude/plugins/` | Personal, gitignored |

### Why NOT Git Clone

Git clone is explicitly not supported because:

1. **No Version Management**: Direct clones bypass Claude Code's plugin versioning
2. **No Auto-Registration**: MCP servers won't be registered with Claude's toolkit
3. **No Update Path**: `claude plugin update` won't work
4. **Path Resolution Fails**: `${CLAUDE_PLUGIN_ROOT}` won't resolve correctly
5. **No Scope Support**: Can't choose user/project/local installation

### Migration from Git Clone

For users who incorrectly used git clone:

```bash
# 1. Remove the cloned directory
rm -rf ~/.claude/plugins/csharp-compounding-docs  # or wherever cloned

# 2. Remove any manual MCP configuration in settings.json

# 3. Install properly via plugin command
claude plugin install csharp-compounding-docs@csharp-compound-marketplace
```

### Post-Installation Verification Checklist

```bash
# 1. Verify plugin is installed
claude plugin list
# Should show: csharp-compounding-docs@csharp-compound-marketplace [enabled]

# 2. Start Claude Code session
claude

# 3. Verify MCP server is running
/mcp
# Should show: csharp-compounding-docs [running]

# 4. Verify skills are available
/cdocs:activate
# Should not show "skill not found" error

# 5. Check for dependency warnings
# SessionStart hook should run and display any missing MCP servers
```

### README Installation Section

```markdown
## Installation

### Requirements

- Claude Code CLI installed and authenticated
- .NET 10 SDK (for MCP server compilation)
- Docker Desktop (for PostgreSQL + pgvector)
- PowerShell 7+ (for launcher scripts)

### Install the Plugin

**User Scope (Recommended for personal use)**
```bash
claude plugin install csharp-compounding-docs@csharp-compound-marketplace
```

**Project Scope (Recommended for teams)**
```bash
claude plugin install csharp-compounding-docs@csharp-compound-marketplace --scope project
```

This adds the plugin to `.claude/settings.json` which can be committed to version control.

### Verify Installation

```bash
# Check plugin is installed
claude plugin list

# Start Claude Code and verify
claude
/mcp  # MCP server should be running
/cdocs:activate  # Should work without errors
```

### Configure External Dependencies

The plugin requires these MCP servers to be configured in your `~/.claude/settings.json`:

```json
{
  "mcpServers": {
    "context7": {
      "type": "http",
      "url": "https://mcp.context7.com/mcp"
    },
    "microsoft-learn": {
      "type": "sse",
      "url": "https://learn.microsoft.com/api/mcp"
    },
    "sequential-thinking": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
    }
  }
}
```

### Important: Do NOT Use Git Clone

The plugin MUST be installed via `claude plugin install`. Direct git clone is not supported because:
- MCP servers won't auto-register
- `${CLAUDE_PLUGIN_ROOT}` paths won't resolve
- Updates via `claude plugin update` won't work

If you previously used git clone, remove the cloned directory and reinstall via the plugin command.
```

### Troubleshooting Installation Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| "Marketplace not found" | Marketplace not registered | Add marketplace first: `claude plugin marketplace add owner/csharp-compound-marketplace` |
| "Plugin not found" | Typo in plugin name | Use exact name: `csharp-compounding-docs` |
| MCP server not starting | Missing .NET runtime | Install .NET 10 SDK |
| MCP tools not appearing | Plugin not enabled | Run `claude plugin enable csharp-compounding-docs` |
| Skills not found | Plugin files not copied | Reinstall: `claude plugin uninstall csharp-compounding-docs && claude plugin install ...` |
| Path resolution errors | Git clone instead of plugin install | Remove cloned dir, reinstall via plugin command |

---

## Dependencies

### Depends On
- Phase 127: Marketplace Landing Page (marketplace must exist for installation)

### Blocks
- Phase 131: Release Automation (release process depends on installation being defined)

---

## Verification Steps

After completing this phase, verify:

1. **Fresh Installation**: Install plugin on clean system, verify all components work
2. **Update Flow**: Make change, release new version, verify `claude plugin update` pulls changes
3. **Scope Behavior**: Test user, project, and local scope installations
4. **MCP Registration**: Verify MCP server starts automatically after install
5. **SessionStart Hook**: Verify dependency warnings display for missing MCP servers
6. **Documentation Accuracy**: README installation steps work exactly as documented

### Manual Verification Script

```bash
#!/bin/bash
# Installation verification script

echo "=== CSharp Compound Docs Installation Verification ==="

# Step 1: Verify marketplace is registered
echo "1. Checking marketplace registration..."
claude plugin marketplace list | grep -q "csharp-compound-marketplace"
if [ $? -eq 0 ]; then
    echo "   [PASS] Marketplace registered"
else
    echo "   [FAIL] Marketplace not found"
    echo "   Run: claude plugin marketplace add owner/csharp-compound-marketplace"
    exit 1
fi

# Step 2: Install plugin
echo "2. Installing plugin..."
claude plugin install csharp-compounding-docs@csharp-compound-marketplace
if [ $? -eq 0 ]; then
    echo "   [PASS] Plugin installed"
else
    echo "   [FAIL] Installation failed"
    exit 1
fi

# Step 3: Verify plugin in list
echo "3. Verifying plugin in list..."
claude plugin list | grep -q "csharp-compounding-docs"
if [ $? -eq 0 ]; then
    echo "   [PASS] Plugin appears in list"
else
    echo "   [FAIL] Plugin not in list"
    exit 1
fi

# Step 4: Verify files exist
echo "4. Checking plugin files..."
PLUGIN_PATH="$HOME/.claude/plugins/csharp-compounding-docs"
if [ -d "$PLUGIN_PATH" ]; then
    echo "   [PASS] Plugin directory exists"

    if [ -f "$PLUGIN_PATH/.claude-plugin/plugin.json" ]; then
        echo "   [PASS] plugin.json exists"
    else
        echo "   [FAIL] plugin.json missing"
    fi

    if [ -f "$PLUGIN_PATH/.mcp.json" ]; then
        echo "   [PASS] .mcp.json exists"
    else
        echo "   [FAIL] .mcp.json missing"
    fi

    if [ -d "$PLUGIN_PATH/skills" ]; then
        echo "   [PASS] skills directory exists"
    else
        echo "   [FAIL] skills directory missing"
    fi
else
    echo "   [FAIL] Plugin directory not found at $PLUGIN_PATH"
    exit 1
fi

echo ""
echo "=== Installation Verification Complete ==="
echo "Next: Start Claude Code and run /mcp to verify MCP server is running"
```

### Integration Test

```csharp
[Fact]
public async Task PluginInstall_CreatesExpectedDirectoryStructure()
{
    // This test would require mocking the Claude CLI
    // In practice, verify manually or via shell script

    // Arrange
    var expectedPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude",
        "plugins",
        "csharp-compounding-docs");

    // Assert expected structure
    Assert.True(Directory.Exists(expectedPath));
    Assert.True(File.Exists(Path.Combine(expectedPath, ".claude-plugin", "plugin.json")));
    Assert.True(File.Exists(Path.Combine(expectedPath, ".mcp.json")));
    Assert.True(Directory.Exists(Path.Combine(expectedPath, "skills")));
    Assert.True(Directory.Exists(Path.Combine(expectedPath, "hooks")));
}

[Fact]
public async Task PluginUpdate_RefreshesPluginFiles()
{
    // Verify that plugin update replaces files correctly
    var pluginPath = GetPluginPath();
    var versionBefore = GetPluginVersion(pluginPath);

    // Simulate update
    // await RunCommand("claude plugin update csharp-compounding-docs");

    var versionAfter = GetPluginVersion(pluginPath);

    // Version should change if update available
    // (or stay same if already latest)
}
```

---

## Files to Create/Modify

### New Files

| File | Purpose |
|------|---------|
| `docs/installation.md` | Detailed installation guide for users |
| `scripts/verify-installation.ps1` | PowerShell verification script |
| `scripts/verify-installation.sh` | Bash verification script |

### Modified Files

| File | Changes |
|------|---------|
| `README.md` | Add installation section with exact commands |
| `marketplace/plugins/csharp-compounding-docs/README.md` | Include installation commands |

---

## Notes

- Installation must always use `claude plugin install` - never git clone
- The `${CLAUDE_PLUGIN_ROOT}` environment variable is critical for path resolution
- MCP server auto-registration only works with proper plugin installation
- Project scope installations are ideal for teams (committed to version control)
- Local scope is useful for testing or personal customizations that shouldn't be shared
- The SessionStart hook provides graceful degradation by warning about missing dependencies rather than failing
