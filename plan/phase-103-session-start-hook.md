# Phase 103: SessionStart Hook - MCP Prerequisite Checking

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-4 hours
> **Category**: Skills System
> **Prerequisites**: Phase 009 (Plugin Directory Structure)

---

## Spec References

This phase implements the SessionStart hook defined in:

- **spec/marketplace.md** - [External MCP Server Prerequisites](../spec/marketplace.md#external-mcp-server-prerequisites) (lines 238-408)
- **research/claude-code-hooks-skill-invocation.md** - [Hook configuration and SessionStart patterns](../research/claude-code-hooks-skill-invocation.md)

---

## Objectives

1. Create the SessionStart hook file structure within the plugin
2. Implement MCP server prerequisite checking with the "check-and-warn" pattern
3. Configure required MCP server detection (Context7, Microsoft Learn, Sequential Thinking)
4. Display user-friendly warnings with configuration instructions
5. Verify infrastructure availability (Docker containers, network connectivity)

---

## Core Design Principle: Check and Warn, Never Install

**CRITICAL**: This hook embodies a fundamental architectural decision:

1. **CHECK** - Verify if required MCP servers are configured in user's settings
2. **WARN** - Display clear, actionable messages if anything is missing
3. **NEVER INSTALL** - Do not modify user's MCP configuration or install anything

This separation of concerns means:
- Users are responsible for their own MCP server installations
- The plugin never modifies system configuration
- Skills can assume all MCP servers are available (no defensive checks needed)

---

## Acceptance Criteria

### Hook File Structure

- [ ] `hooks.json` located at `${CLAUDE_PLUGIN_ROOT}/.claude-plugin/hooks.json`
- [ ] `check-dependencies.ps1` located at `${CLAUDE_PLUGIN_ROOT}/hooks/check-dependencies.ps1`
- [ ] Hook registered for `SessionStart` event with `matcher: "*"`
- [ ] PowerShell script executable on all platforms (PowerShell 7+)

### Required MCP Server Checks

- [ ] **Context7** - Verify `context7` key exists in `mcpServers` configuration
- [ ] **Microsoft Learn** - Verify `microsoft-learn` key exists in `mcpServers`
- [ ] **Sequential Thinking** - Verify `sequential-thinking` key exists in `mcpServers`
- [ ] All three MCPs marked as REQUIRED (not optional)

### Settings File Detection

- [ ] Check user-level settings: `~/.claude/settings.json`
- [ ] Check project-level settings: `./.claude/settings.json`
- [ ] Merge configurations appropriately (project overrides user)
- [ ] Handle missing settings files gracefully

### Warning Message Format

- [ ] Clear visual header: `=== CSharp Compound Docs Plugin ===`
- [ ] Red color for missing REQUIRED dependencies
- [ ] Provide exact JSON snippets for configuration
- [ ] Include relevant documentation URLs
- [ ] Visual footer to bound the warning block

### Infrastructure Availability Check

- [ ] Optional: Check if Docker daemon is running
- [ ] Optional: Verify PostgreSQL container accessible (if checking infrastructure)
- [ ] Optional: Verify Ollama service accessible (if checking infrastructure)
- [ ] Infrastructure checks should WARN, not BLOCK

### Exit Behavior

- [ ] Exit code 0 when all requirements met (no output)
- [ ] Exit code 0 when requirements missing (show warnings, but don't block)
- [ ] Exit code 2 only for fatal script errors (not missing dependencies)

---

## Implementation Notes

### hooks.json Configuration

Located at `${CLAUDE_PLUGIN_ROOT}/.claude-plugin/hooks.json`:

```json
{
  "description": "CSharp Compound Docs Plugin hooks for session lifecycle",
  "hooks": {
    "SessionStart": [
      {
        "matcher": "*",
        "hooks": [
          {
            "type": "command",
            "command": "pwsh -NoProfile -NonInteractive -File \"${CLAUDE_PLUGIN_ROOT}/hooks/check-dependencies.ps1\"",
            "timeout": 10
          }
        ]
      }
    ]
  }
}
```

**Configuration Notes**:
- `matcher: "*"` - Runs on all session start types (startup, resume, clear, compact)
- `-NoProfile` - Faster startup, avoids user profile interference
- `-NonInteractive` - Ensures script doesn't prompt for input
- `timeout: 10` - 10 second timeout prevents hanging sessions

### check-dependencies.ps1 Script

Located at `${CLAUDE_PLUGIN_ROOT}/hooks/check-dependencies.ps1`:

```powershell
#!/usr/bin/env pwsh
#Requires -Version 7.0
<#
.SYNOPSIS
    CSharp Compound Docs Plugin - MCP Prerequisite Checker

.DESCRIPTION
    Checks for required MCP servers and displays warnings if missing.
    Implements the "check-and-warn, never install" pattern.

.NOTES
    This script runs on every Claude Code session start.
    Exit code 0 = success (warnings are informational, not blocking)
    Exit code 2 = script error (not dependency error)
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Continue"
$WarningPreference = "Continue"

# Required MCP servers
$RequiredMcpServers = @{
    'context7' = @{
        Name = 'Context7'
        Purpose = 'Framework documentation lookup'
        ConfigExample = @"
"context7": {
  "type": "http",
  "url": "https://mcp.context7.com/mcp"
}
"@
    }
    'microsoft-learn' = @{
        Name = 'Microsoft Learn'
        Purpose = '.NET/C# documentation lookup'
        ConfigExample = @"
"microsoft-learn": {
  "type": "sse",
  "url": "https://learn.microsoft.com/api/mcp"
}
"@
    }
    'sequential-thinking' = @{
        Name = 'Sequential Thinking'
        Purpose = 'Complex multi-step reasoning and analysis'
        ConfigExample = @"
"sequential-thinking": {
  "command": "npx",
  "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
}
"@
    }
}

function Get-ClaudeSettings {
    <#
    .SYNOPSIS
        Loads Claude settings from user and project locations
    #>
    [CmdletBinding()]
    param()

    $settings = @{
        mcpServers = @{}
        source = $null
    }

    # Possible settings locations (in precedence order: project > user)
    $settingsPaths = @(
        # Project-level settings
        (Join-Path $PWD ".claude" "settings.json"),
        (Join-Path $PWD ".claude" "settings.local.json"),
        # User-level settings
        (Join-Path $HOME ".claude" "settings.json")
    )

    foreach ($path in $settingsPaths) {
        if (Test-Path $path) {
            try {
                $content = Get-Content $path -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
                if ($content.mcpServers) {
                    # Merge MCP servers (first found wins per server name)
                    foreach ($serverName in $content.mcpServers.PSObject.Properties.Name) {
                        if (-not $settings.mcpServers.ContainsKey($serverName)) {
                            $settings.mcpServers[$serverName] = $content.mcpServers.$serverName
                        }
                    }
                    if (-not $settings.source) {
                        $settings.source = $path
                    }
                }
            }
            catch {
                Write-Verbose "Failed to parse settings at $path : $_"
            }
        }
    }

    return $settings
}

function Test-DockerRunning {
    <#
    .SYNOPSIS
        Checks if Docker daemon is running
    #>
    [CmdletBinding()]
    param()

    try {
        $null = docker info 2>&1
        return $LASTEXITCODE -eq 0
    }
    catch {
        return $false
    }
}

function Test-InfrastructureAvailable {
    <#
    .SYNOPSIS
        Checks if plugin infrastructure services are available
    #>
    [CmdletBinding()]
    param()

    $warnings = @()

    # Check Docker
    if (-not (Test-DockerRunning)) {
        $warnings += "Docker is not running. The MCP server requires Docker for PostgreSQL and Ollama services."
    }

    return $warnings
}

# Main execution
function Main {
    [CmdletBinding()]
    param()

    $errors = @()
    $warnings = @()

    # Load settings
    $settings = Get-ClaudeSettings

    # Check if any settings found
    if ($settings.mcpServers.Count -eq 0) {
        $errors += @"
No Claude settings.json found with MCP servers configured.

Please configure MCP servers in one of:
  - ~/.claude/settings.json (user-level)
  - ./.claude/settings.json (project-level)
"@
    }
    else {
        # Check each required MCP server
        foreach ($serverKey in $RequiredMcpServers.Keys) {
            $serverInfo = $RequiredMcpServers[$serverKey]

            if (-not $settings.mcpServers.ContainsKey($serverKey)) {
                $errors += @"
MISSING REQUIRED: $($serverInfo.Name) MCP server not configured.
Purpose: $($serverInfo.Purpose)

Add to your settings.json mcpServers section:
$($serverInfo.ConfigExample)
"@
            }
        }
    }

    # Check infrastructure (optional, warnings only)
    $infraWarnings = Test-InfrastructureAvailable
    $warnings += $infraWarnings

    # Display messages if any issues found
    if ($errors.Count -gt 0 -or $warnings.Count -gt 0) {
        Write-Host ""
        Write-Host "=== CSharp Compound Docs Plugin ===" -ForegroundColor Cyan

        if ($errors.Count -gt 0) {
            Write-Host ""
            Write-Host "MISSING PREREQUISITES:" -ForegroundColor Red
            Write-Host ""
            foreach ($err in $errors) {
                Write-Host $err -ForegroundColor Red
                Write-Host ""
            }
            Write-Host "Documentation: https://github.com/username/csharp-compound-engineering#prerequisites" -ForegroundColor Yellow
        }

        if ($warnings.Count -gt 0) {
            Write-Host ""
            Write-Host "WARNINGS:" -ForegroundColor Yellow
            Write-Host ""
            foreach ($warn in $warnings) {
                Write-Host "  - $warn" -ForegroundColor Yellow
            }
        }

        Write-Host ""
        Write-Host "===================================" -ForegroundColor Cyan
        Write-Host ""
    }

    # Always exit 0 - warnings don't block session start
    # Exit 2 only for script errors (handled by PowerShell automatically)
    exit 0
}

# Run main
Main
```

### Hook File Structure

```
${CLAUDE_PLUGIN_ROOT}/
├── .claude-plugin/
│   ├── plugin.json
│   └── hooks.json              # SessionStart hook registration
├── hooks/
│   ├── check-dependencies.ps1  # Main prerequisite checker
│   └── README.md               # Hook documentation
└── ...
```

### hooks/README.md Documentation

```markdown
# CSharp Compound Docs Plugin - Hooks

This directory contains Claude Code hooks for the plugin lifecycle.

## check-dependencies.ps1

**Purpose**: Verify required MCP servers are configured before using the plugin.

**When it runs**: Every session start (new, resume, clear, compact)

**What it checks**:
1. Context7 MCP - Framework documentation lookup
2. Microsoft Learn MCP - .NET/C# documentation
3. Sequential Thinking MCP - Complex reasoning tasks
4. Docker daemon running (warning only)

**Exit behavior**:
- Exit 0 with no output = All requirements met
- Exit 0 with warnings = Missing dependencies (doesn't block session)
- Exit 2 = Script error (rare)

## Design Philosophy

This hook implements the "check-and-warn, never install" pattern:
- It CHECKS for prerequisites
- It WARNS with actionable messages
- It NEVER installs or modifies configuration

Users are responsible for configuring their own MCP servers.
```

---

## Environment Variables

The hook script has access to these Claude Code environment variables:

| Variable | Description | Used For |
|----------|-------------|----------|
| `$CLAUDE_PLUGIN_ROOT` | Plugin installation directory | Resolving relative paths |
| `$CLAUDE_PROJECT_DIR` | Current project directory | Finding project settings |
| `$CLAUDE_ENV_FILE` | File for persisting env vars | Not used in this hook |
| `$PWD` | Current working directory | Finding project settings |
| `$HOME` | User home directory | Finding user settings |

---

## Testing the Hook

### Manual Testing

```powershell
# Test the script directly
pwsh -NoProfile -NonInteractive -File ./hooks/check-dependencies.ps1

# Test with verbose output
pwsh -NoProfile -NonInteractive -File ./hooks/check-dependencies.ps1 -Verbose

# Test with missing settings (simulate)
$env:HOME = "/nonexistent"; pwsh -NoProfile -File ./hooks/check-dependencies.ps1
```

### Integration Testing

```bash
# Start Claude Code in a project without MCP servers configured
# Verify warning messages appear

# Start Claude Code with all MCPs configured
# Verify no output (silent success)
```

### Unit Test Scenarios

```csharp
[Fact]
public void CheckDependencies_AllMcpsConfigured_NoOutput()
{
    // Arrange - settings.json with all required MCPs
    // Act - Run script
    // Assert - No stdout, exit code 0
}

[Fact]
public void CheckDependencies_MissingContext7_ShowsWarning()
{
    // Arrange - settings.json missing context7
    // Act - Run script
    // Assert - Warning mentions "Context7", exit code 0
}

[Fact]
public void CheckDependencies_NoSettingsFile_ShowsError()
{
    // Arrange - No settings.json exists
    // Act - Run script
    // Assert - Error about missing settings, exit code 0
}

[Fact]
public void CheckDependencies_DockerNotRunning_ShowsWarning()
{
    // Arrange - Docker not running
    // Act - Run script
    // Assert - Warning about Docker, exit code 0
}
```

---

## Dependencies

### Depends On

| Phase | Dependency Type | Description |
|-------|-----------------|-------------|
| Phase 009 | Hard | Plugin directory structure must exist |
| None | Runtime | PowerShell 7+ must be installed |

### Blocks

| Phase | Relationship | Description |
|-------|--------------|-------------|
| Phase 085+ | Soft | Skills assume MCPs are available |
| Phase 104+ | Soft | Other hooks may build on this pattern |

---

## Verification Checklist

After completing this phase, verify:

1. **Hook Registration**:
   ```bash
   cat plugins/csharp-compounding-docs/.claude-plugin/hooks.json | jq .
   # Should show SessionStart hook configuration
   ```

2. **Script Syntax**:
   ```bash
   pwsh -NoProfile -Command "Test-ScriptAnalyzer -Path plugins/csharp-compounding-docs/hooks/check-dependencies.ps1"
   # Or at minimum:
   pwsh -NoProfile -Command "\$null = [System.Management.Automation.Language.Parser]::ParseFile('plugins/csharp-compounding-docs/hooks/check-dependencies.ps1', [ref]\$null, [ref]\$errors); \$errors"
   ```

3. **Script Execution**:
   ```bash
   # Should show warnings if MCPs not configured
   pwsh -NoProfile -File plugins/csharp-compounding-docs/hooks/check-dependencies.ps1
   ```

4. **Exit Codes**:
   ```bash
   pwsh -NoProfile -File plugins/csharp-compounding-docs/hooks/check-dependencies.ps1
   echo "Exit code: $?"
   # Should be 0 regardless of warnings
   ```

5. **Warning Format**:
   - Visual header/footer present
   - Red color for errors
   - Yellow color for warnings
   - Config examples are valid JSON

---

## Security Considerations

1. **No External Calls**: Script does not make network requests
2. **Read-Only**: Script never modifies files or configuration
3. **No Sensitive Data**: Script does not read or log secrets
4. **Sandboxed**: Runs within Claude Code's hook sandbox
5. **Timeout**: 10 second timeout prevents hanging

---

## Error Handling

| Error Condition | Behavior |
|-----------------|----------|
| Settings file doesn't exist | Show warning, exit 0 |
| Settings file invalid JSON | Skip file, try next location |
| PowerShell not installed | Hook fails to run (Claude Code handles) |
| Script timeout | Hook terminates after 10 seconds |
| Unexpected error | PowerShell exits with error code |

---

## Future Enhancements

When Claude Code implements [Plugin Dependencies (Issue #9444)](https://github.com/anthropics/claude-code/issues/9444), this hook can be updated to use formal dependency declarations that can prompt or block installation.

Potential future additions:
- Version checking for MCP servers
- Connectivity testing (ping MCP endpoints)
- Configuration validation (not just existence)
- Automatic fix suggestions via `claude plugin` commands

---

## Notes

- This hook is the first line of defense for plugin prerequisites
- Per spec, skills assume all MCPs are available - they don't check defensively
- The hook uses PowerShell for cross-platform compatibility (macOS, Linux, Windows)
- Warning messages include copy-pasteable configuration snippets
- Exit code 0 ensures sessions start even with warnings (non-blocking)

---

## Change Log

| Date | Changes |
|------|---------|
| 2025-01-24 | Initial phase creation |
