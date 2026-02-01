# Phase 128: Plugin Registry

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-4 hours
> **Category**: Marketplace & Deployment
> **Prerequisites**: Phase 127 (Marketplace Structure)

---

## Spec References

This phase implements the plugin registry defined in:

- **spec/marketplace.md** - [Plugin Registry](../spec/marketplace.md#plugin-registry) (lines 119-140)
- **spec/marketplace.md** - [Release Process](../spec/marketplace.md#release-process) (lines 413-471)

---

## Objectives

1. Create the `api/plugins.json` registry file format
2. Implement plugin listing with comprehensive metadata
3. Define download URL patterns for versioned releases
4. Establish version history tracking structure
5. Document the registry update process for releases

---

## Acceptance Criteria

- [ ] `marketplace/api/plugins.json` registry file exists with proper schema
- [ ] Registry format includes all required plugin metadata fields
- [ ] Download URLs follow consistent versioning pattern
- [ ] Version history is accessible via API structure
- [ ] Registry update process is documented and scriptable
- [ ] JSON schema validation is implemented for registry entries
- [ ] Registry supports future multi-plugin expansion

---

## Implementation Notes

### Registry File Structure

Create `marketplace/api/plugins.json`:

```json
{
  "$schema": "./plugins.schema.json",
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
      "repository_url": "https://github.com/username/csharp-compound-engineering",
      "author": {
        "name": "Your Name",
        "url": "https://github.com/username"
      },
      "license": "MIT",
      "keywords": [
        "knowledge-management",
        "rag",
        "semantic-search",
        "csharp",
        "dotnet"
      ],
      "claude_code_version": ">=1.0.0",
      "stars": 0,
      "downloads": 0,
      "created_at": "2025-01-22T00:00:00Z",
      "updated_at": "2025-01-22T00:00:00Z",
      "versions": [
        {
          "version": "1.0.0",
          "download_url": "/plugins/csharp-compounding-docs/versions/1.0.0/plugin.zip",
          "release_notes_url": "/plugins/csharp-compounding-docs/versions/1.0.0/CHANGELOG.md",
          "released_at": "2025-01-22T00:00:00Z",
          "checksum": "sha256:abc123..."
        }
      ]
    }
  ]
}
```

### Registry JSON Schema

Create `marketplace/api/plugins.schema.json` for validation:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "$id": "https://username.github.io/csharp-compound-engineering/api/plugins.schema.json",
  "title": "Plugin Registry",
  "description": "Schema for the plugin marketplace registry",
  "type": "object",
  "required": ["version", "updated", "plugins"],
  "properties": {
    "version": {
      "type": "string",
      "description": "Registry schema version",
      "pattern": "^\\d+\\.\\d+$"
    },
    "updated": {
      "type": "string",
      "format": "date-time",
      "description": "Last registry update timestamp"
    },
    "plugins": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/plugin"
      }
    }
  },
  "$defs": {
    "plugin": {
      "type": "object",
      "required": ["id", "name", "version", "description", "manifest_url", "download_url"],
      "properties": {
        "id": {
          "type": "string",
          "pattern": "^[a-z0-9-]+$",
          "description": "Unique plugin identifier"
        },
        "name": {
          "type": "string",
          "description": "Human-readable plugin name"
        },
        "version": {
          "type": "string",
          "pattern": "^\\d+\\.\\d+\\.\\d+(-[a-z0-9.]+)?$",
          "description": "Current version (semver)"
        },
        "description": {
          "type": "string",
          "maxLength": 500,
          "description": "Brief plugin description"
        },
        "manifest_url": {
          "type": "string",
          "description": "Relative URL to plugin manifest"
        },
        "download_url": {
          "type": "string",
          "description": "Relative URL to latest plugin package"
        },
        "repository_url": {
          "type": "string",
          "format": "uri",
          "description": "Source repository URL"
        },
        "author": {
          "$ref": "#/$defs/author"
        },
        "license": {
          "type": "string",
          "description": "SPDX license identifier"
        },
        "keywords": {
          "type": "array",
          "items": { "type": "string" },
          "description": "Searchable keywords"
        },
        "claude_code_version": {
          "type": "string",
          "description": "Compatible Claude Code version range"
        },
        "stars": {
          "type": "integer",
          "minimum": 0,
          "description": "GitHub stars count"
        },
        "downloads": {
          "type": "integer",
          "minimum": 0,
          "description": "Total download count"
        },
        "created_at": {
          "type": "string",
          "format": "date-time"
        },
        "updated_at": {
          "type": "string",
          "format": "date-time"
        },
        "versions": {
          "type": "array",
          "items": {
            "$ref": "#/$defs/version_entry"
          },
          "description": "Version history"
        }
      }
    },
    "author": {
      "type": "object",
      "required": ["name"],
      "properties": {
        "name": { "type": "string" },
        "url": { "type": "string", "format": "uri" }
      }
    },
    "version_entry": {
      "type": "object",
      "required": ["version", "download_url", "released_at"],
      "properties": {
        "version": {
          "type": "string",
          "pattern": "^\\d+\\.\\d+\\.\\d+(-[a-z0-9.]+)?$"
        },
        "download_url": {
          "type": "string",
          "description": "Version-specific download URL"
        },
        "release_notes_url": {
          "type": "string",
          "description": "Changelog or release notes URL"
        },
        "released_at": {
          "type": "string",
          "format": "date-time"
        },
        "checksum": {
          "type": "string",
          "pattern": "^sha256:[a-f0-9]{64}$",
          "description": "SHA256 checksum of plugin package"
        },
        "deprecated": {
          "type": "boolean",
          "default": false,
          "description": "Whether this version is deprecated"
        },
        "deprecation_message": {
          "type": "string",
          "description": "Reason for deprecation"
        }
      }
    }
  }
}
```

### Version History Directory Structure

Each plugin version maintains its own directory:

```
marketplace/plugins/csharp-compounding-docs/
├── manifest.json              # Current plugin manifest
├── README.md                  # Plugin documentation
└── versions/
    ├── 1.0.0/
    │   ├── plugin.zip         # Release package
    │   ├── CHANGELOG.md       # Version-specific release notes
    │   └── checksums.txt      # SHA256 checksums
    ├── 1.1.0/
    │   ├── plugin.zip
    │   ├── CHANGELOG.md
    │   └── checksums.txt
    └── latest -> 1.1.0        # Symlink to latest stable version
```

### Registry Update Script

Create `scripts/Update-PluginRegistry.ps1`:

```powershell
#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Updates the plugin registry with a new release.

.DESCRIPTION
    Adds a new version entry to plugins.json and updates metadata.
    Called by the release workflow after packaging.

.PARAMETER Version
    The version being released (e.g., "1.0.0")

.PARAMETER PackagePath
    Path to the plugin.zip package

.EXAMPLE
    ./scripts/Update-PluginRegistry.ps1 -Version "1.0.0" -PackagePath "release/plugin-1.0.0.zip"
#>

param(
    [Parameter(Mandatory)]
    [ValidatePattern("^\d+\.\d+\.\d+(-[a-z0-9.]+)?$")]
    [string]$Version,

    [Parameter(Mandatory)]
    [ValidateScript({ Test-Path $_ })]
    [string]$PackagePath
)

$ErrorActionPreference = "Stop"

$RegistryPath = Join-Path $PSScriptRoot ".." "marketplace" "api" "plugins.json"
$PluginId = "csharp-compounding-docs"

# Calculate checksum
$Hash = (Get-FileHash -Path $PackagePath -Algorithm SHA256).Hash.ToLower()
$Checksum = "sha256:$Hash"

Write-Host "Package checksum: $Checksum" -ForegroundColor Cyan

# Load current registry
$Registry = Get-Content $RegistryPath -Raw | ConvertFrom-Json

# Find the plugin entry
$Plugin = $Registry.plugins | Where-Object { $_.id -eq $PluginId }

if (-not $Plugin) {
    throw "Plugin '$PluginId' not found in registry"
}

# Create new version entry
$VersionEntry = [PSCustomObject]@{
    version = $Version
    download_url = "/plugins/$PluginId/versions/$Version/plugin.zip"
    release_notes_url = "/plugins/$PluginId/versions/$Version/CHANGELOG.md"
    released_at = (Get-Date -Format "o")
    checksum = $Checksum
}

# Check if version already exists
$ExistingVersion = $Plugin.versions | Where-Object { $_.version -eq $Version }
if ($ExistingVersion) {
    Write-Warning "Version $Version already exists. Updating entry."
    $Plugin.versions = @($Plugin.versions | Where-Object { $_.version -ne $Version })
}

# Add new version (prepend so latest is first)
$Plugin.versions = @($VersionEntry) + @($Plugin.versions)

# Update plugin metadata
$Plugin.version = $Version
$Plugin.download_url = "/plugins/$PluginId/versions/latest/plugin.zip"
$Plugin.updated_at = (Get-Date -Format "o")

# Update registry metadata
$Registry.updated = (Get-Date -Format "o")

# Write back to file
$Registry | ConvertTo-Json -Depth 10 | Set-Content $RegistryPath -Encoding utf8

Write-Host "Registry updated for version $Version" -ForegroundColor Green
```

### Registry Validation Script

Create `scripts/Validate-PluginRegistry.ps1`:

```powershell
#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates the plugin registry against the JSON schema.

.DESCRIPTION
    Uses the JSON schema to validate plugins.json structure and content.
    Run this before committing registry changes.

.EXAMPLE
    ./scripts/Validate-PluginRegistry.ps1
#>

$ErrorActionPreference = "Stop"

$RegistryPath = Join-Path $PSScriptRoot ".." "marketplace" "api" "plugins.json"
$SchemaPath = Join-Path $PSScriptRoot ".." "marketplace" "api" "plugins.schema.json"

Write-Host "Validating plugin registry..." -ForegroundColor Cyan

# Load registry
$Registry = Get-Content $RegistryPath -Raw | ConvertFrom-Json -AsHashtable

# Basic structure validation
$RequiredFields = @("version", "updated", "plugins")
foreach ($field in $RequiredFields) {
    if (-not $Registry.ContainsKey($field)) {
        throw "Missing required field: $field"
    }
}

# Plugin entry validation
foreach ($plugin in $Registry.plugins) {
    $PluginRequiredFields = @("id", "name", "version", "description", "manifest_url", "download_url")
    foreach ($field in $PluginRequiredFields) {
        if (-not $plugin.ContainsKey($field)) {
            throw "Plugin '$($plugin.id)' missing required field: $field"
        }
    }

    # Validate ID format
    if ($plugin.id -notmatch "^[a-z0-9-]+$") {
        throw "Plugin ID '$($plugin.id)' must contain only lowercase letters, numbers, and hyphens"
    }

    # Validate version format
    if ($plugin.version -notmatch "^\d+\.\d+\.\d+(-[a-z0-9.]+)?$") {
        throw "Plugin '$($plugin.id)' has invalid version format: $($plugin.version)"
    }

    # Validate versions array
    if ($plugin.versions) {
        foreach ($ver in $plugin.versions) {
            if (-not $ver.version -or -not $ver.download_url -or -not $ver.released_at) {
                throw "Plugin '$($plugin.id)' version entry missing required fields"
            }

            # Validate checksum format if present
            if ($ver.checksum -and $ver.checksum -notmatch "^sha256:[a-f0-9]{64}$") {
                throw "Invalid checksum format for $($plugin.id) v$($ver.version)"
            }
        }
    }
}

Write-Host "Registry validation passed!" -ForegroundColor Green
Write-Host "  - Registry version: $($Registry.version)"
Write-Host "  - Last updated: $($Registry.updated)"
Write-Host "  - Plugins: $($Registry.plugins.Count)"
```

### Download URL Patterns

The registry uses consistent URL patterns for plugin downloads:

| URL Type | Pattern | Example |
|----------|---------|---------|
| Latest | `/plugins/{id}/versions/latest/plugin.zip` | `/plugins/csharp-compounding-docs/versions/latest/plugin.zip` |
| Specific | `/plugins/{id}/versions/{version}/plugin.zip` | `/plugins/csharp-compounding-docs/versions/1.0.0/plugin.zip` |
| Manifest | `/plugins/{id}/manifest.json` | `/plugins/csharp-compounding-docs/manifest.json` |
| Changelog | `/plugins/{id}/versions/{version}/CHANGELOG.md` | `/plugins/csharp-compounding-docs/versions/1.0.0/CHANGELOG.md` |

### Version Listing Endpoint

For programmatic access, the registry supports version queries:

```
GET /api/plugins.json
```

Returns all plugins with their full version history. Clients can:
- Filter by plugin ID
- Sort versions by release date
- Check for newer versions against installed version
- Verify package integrity via checksums

### Checksum Generation

Each release package includes a SHA256 checksum for integrity verification:

```powershell
# Generate checksum file
$Hash = (Get-FileHash -Path "plugin.zip" -Algorithm SHA256).Hash.ToLower()
"$Hash  plugin.zip" | Set-Content "checksums.txt"
```

Verification during installation:

```powershell
# Verify downloaded package
$Expected = "abc123..."  # From registry
$Actual = (Get-FileHash -Path "plugin.zip" -Algorithm SHA256).Hash.ToLower()
if ($Expected -ne $Actual) {
    throw "Checksum mismatch! Package may be corrupted or tampered."
}
```

### Registry Update Process

The complete registry update workflow:

1. **Build Release Package** (Phase 129)
   - Package plugin files into `plugin.zip`
   - Generate checksums

2. **Update Registry**
   ```bash
   pwsh ./scripts/Update-PluginRegistry.ps1 -Version "1.0.0" -PackagePath "release/plugin-1.0.0.zip"
   ```

3. **Validate Registry**
   ```bash
   pwsh ./scripts/Validate-PluginRegistry.ps1
   ```

4. **Copy to Marketplace**
   ```bash
   mkdir -p marketplace/plugins/csharp-compounding-docs/versions/1.0.0/
   cp release/plugin-1.0.0.zip marketplace/plugins/csharp-compounding-docs/versions/1.0.0/plugin.zip
   cp CHANGELOG.md marketplace/plugins/csharp-compounding-docs/versions/1.0.0/
   ```

5. **Update Latest Symlink**
   ```bash
   ln -sf 1.0.0 marketplace/plugins/csharp-compounding-docs/versions/latest
   ```

6. **Deploy to GitHub Pages**
   - Commit changes to trigger deployment

### Future Multi-Plugin Support

The registry structure supports multiple plugins:

```json
{
  "version": "1.0",
  "updated": "2025-01-22T00:00:00Z",
  "plugins": [
    { "id": "csharp-compounding-docs", ... },
    { "id": "another-plugin", ... },
    { "id": "third-plugin", ... }
  ]
}
```

Each plugin maintains its own:
- Subdirectory in `marketplace/plugins/`
- Version history
- Independent release cycle

---

## Dependencies

### Depends On
- Phase 127: Marketplace Structure (provides directory layout and GitHub Pages setup)

### Blocks
- Phase 129: Release Packaging (uses registry for version publishing)
- Phase 130: GitHub Actions Release Workflow (automates registry updates)

---

## Verification Steps

After completing this phase, verify:

1. **Registry file exists**: `marketplace/api/plugins.json` is valid JSON
2. **Schema file exists**: `marketplace/api/plugins.schema.json` is valid JSON Schema
3. **Validation passes**: `./scripts/Validate-PluginRegistry.ps1` completes without errors
4. **Update script works**: Test with a mock version update
5. **URL patterns resolve**: Download URLs match actual file locations
6. **Checksum format valid**: All checksums follow `sha256:[64-char-hex]` pattern
7. **Version history sorted**: Versions are listed newest-first

---

## Notes

- The registry is a static JSON file served from GitHub Pages - no backend required
- Version checksums provide integrity verification but not cryptographic signing
- Download counts and stars are manually maintained (could be automated via GitHub API in future)
- The schema allows for future expansion with additional metadata fields
- Consider adding `deprecated: true` field for plugin deprecation without removal
- For large registries, consider pagination or separate per-plugin endpoint files
