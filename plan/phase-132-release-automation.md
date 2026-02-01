# Phase 132: Release Automation

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-10 hours
> **Category**: Marketplace & Deployment
> **Prerequisites**: Phase 125 (GitHub Pages Setup), Phase 127 (Plugin Manifest)

---

## Spec References

This phase implements the release automation defined in:

- **spec/marketplace.md** - [Release Process](../spec/marketplace.md#release-process) (lines 411-473)
- **spec/marketplace.md** - [Versioning](../spec/marketplace.md#versioning) (lines 413-418)
- **spec/marketplace.md** - [GitHub Actions Automation](../spec/marketplace.md#github-actions-automation) (lines 443-471)

---

## Objectives

1. Establish semantic versioning strategy and version management across all artifacts
2. Create the `package-release.ps1` script for building release packages
3. Implement GitHub Actions workflow for automated releases on version tags
4. Automate registry (`api/plugins.json`) updates during release
5. Configure GitHub Releases integration with downloadable assets
6. Set up CHANGELOG automation and version synchronization

---

## Acceptance Criteria

### Semantic Versioning
- [ ] Version format follows `MAJOR.MINOR.PATCH` semantic versioning
- [ ] MAJOR version increments for breaking changes to skills or MCP tools
- [ ] MINOR version increments for new features and doc-types
- [ ] PATCH version increments for bug fixes and documentation
- [ ] Version synchronization across all version-bearing files documented

### Package Release Script
- [ ] `scripts/package-release.ps1` exists and accepts `-Version` parameter
- [ ] Script validates version format (semver compliance)
- [ ] Script updates version in `manifest.json`
- [ ] Script updates version in MCP server project (`*.csproj`)
- [ ] Script builds MCP server in Release configuration
- [ ] Script creates `plugin-{version}.zip` package with correct structure
- [ ] Script generates release notes from CHANGELOG.md
- [ ] Script validates package contents before finalizing

### GitHub Actions Release Workflow
- [ ] `.github/workflows/release.yml` exists
- [ ] Workflow triggers on `v*` tag push (e.g., `v1.0.0`)
- [ ] Workflow builds and tests MCP server
- [ ] Workflow runs `package-release.ps1` script
- [ ] Workflow updates marketplace directory structure
- [ ] Workflow updates `api/plugins.json` registry
- [ ] Workflow commits and pushes to `gh-pages` branch
- [ ] Workflow creates GitHub Release with assets

### Registry Update Automation
- [ ] `scripts/update-registry.ps1` script exists
- [ ] Script updates `api/plugins.json` with new version
- [ ] Script updates `updated` timestamp field
- [ ] Script validates JSON schema after update
- [ ] Script updates `latest` symlink in marketplace

### GitHub Release Creation
- [ ] Release created with tag name as title
- [ ] Release body contains version changelog section
- [ ] Release includes `plugin-{version}.zip` as downloadable asset
- [ ] Release marked as latest (unless pre-release)
- [ ] Pre-release versions (e.g., `v1.0.0-beta.1`) marked accordingly

### CHANGELOG Management
- [ ] `CHANGELOG.md` follows Keep a Changelog format
- [ ] Unreleased section automatically moved to version section on release
- [ ] Links to GitHub compare/release pages included
- [ ] Script extracts relevant changelog section for release notes

---

## Implementation Notes

### Version Files to Synchronize

The following files contain version information that must be synchronized:

| File | Version Location | Update Method |
|------|------------------|---------------|
| `plugins/csharp-compounding-docs/manifest.json` | `$.version` | JSON update |
| `src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj` | `<Version>` | XML update |
| `marketplace/plugins/csharp-compounding-docs/manifest.json` | `$.version` | Copy from plugin |
| `marketplace/api/plugins.json` | `$.plugins[0].version` | JSON update |
| `CHANGELOG.md` | `## [x.y.z]` header | Text manipulation |

### Package Release Script

Create `scripts/package-release.ps1`:

```powershell
#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Packages the CSharp Compound Docs plugin for release.

.DESCRIPTION
    Builds the MCP server, updates version numbers, and creates a distributable
    plugin package for the marketplace.

.PARAMETER Version
    The semantic version to release (e.g., "1.0.0", "2.1.0-beta.1").

.PARAMETER OutputPath
    Optional output directory for the release package. Defaults to ./release/

.PARAMETER SkipBuild
    Skip building the MCP server (useful for re-packaging).

.PARAMETER DryRun
    Show what would be done without making changes.

.EXAMPLE
    ./package-release.ps1 -Version 1.0.0

.EXAMPLE
    ./package-release.ps1 -Version 1.1.0-beta.1 -DryRun
#>

param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-((0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(\.(0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(\+([0-9a-zA-Z-]+(\.[0-9a-zA-Z-]+)*))?$')]
    [string]$Version,

    [string]$OutputPath = "./release",

    [switch]$SkipBuild,

    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=== CSharp Compound Docs Release Packager ===" -ForegroundColor Cyan
Write-Host "Version: $Version" -ForegroundColor Green

if ($DryRun) {
    Write-Host "[DRY RUN] No changes will be made" -ForegroundColor Yellow
}

# Step 1: Validate version format
Write-Host "`n[1/7] Validating version format..." -ForegroundColor Cyan
$isPreRelease = $Version -match '-'
if ($isPreRelease) {
    Write-Host "  Pre-release version detected" -ForegroundColor Yellow
}

# Step 2: Update manifest.json
Write-Host "`n[2/7] Updating manifest.json..." -ForegroundColor Cyan
$manifestPath = Join-Path $RepoRoot "plugins/csharp-compounding-docs/manifest.json"
if (-not $DryRun) {
    $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
    $manifest.version = $Version
    $manifest | ConvertTo-Json -Depth 10 | Set-Content $manifestPath -NoNewline
}
Write-Host "  Updated: $manifestPath"

# Step 3: Update .csproj version
Write-Host "`n[3/7] Updating MCP server project version..." -ForegroundColor Cyan
$csprojPath = Join-Path $RepoRoot "src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj"
if (-not $DryRun -and (Test-Path $csprojPath)) {
    $csproj = Get-Content $csprojPath -Raw
    $csproj = $csproj -replace '<Version>.*?</Version>', "<Version>$Version</Version>"
    Set-Content $csprojPath $csproj -NoNewline
}
Write-Host "  Updated: $csprojPath"

# Step 4: Build MCP server
if (-not $SkipBuild) {
    Write-Host "`n[4/7] Building MCP server in Release configuration..." -ForegroundColor Cyan
    if (-not $DryRun) {
        $buildResult = & dotnet build (Join-Path $RepoRoot "src/CompoundDocs.McpServer") -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with exit code $LASTEXITCODE"
        }
        & dotnet publish (Join-Path $RepoRoot "src/CompoundDocs.McpServer") -c Release -o (Join-Path $RepoRoot "publish")
    }
    Write-Host "  Build completed successfully"
} else {
    Write-Host "`n[4/7] Skipping build (--SkipBuild specified)" -ForegroundColor Yellow
}

# Step 5: Create package structure
Write-Host "`n[5/7] Creating package structure..." -ForegroundColor Cyan
$packageDir = Join-Path $RepoRoot "$OutputPath/package"
$packageZip = Join-Path $RepoRoot "$OutputPath/plugin-$Version.zip"

if (-not $DryRun) {
    # Clean and create output directory
    if (Test-Path $packageDir) { Remove-Item $packageDir -Recurse -Force }
    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

    # Copy plugin structure
    $pluginSource = Join-Path $RepoRoot "plugins/csharp-compounding-docs"
    Copy-Item -Path $pluginSource -Destination $packageDir -Recurse

    # Copy published MCP server
    $publishDir = Join-Path $RepoRoot "publish"
    if (Test-Path $publishDir) {
        $mcpServerDest = Join-Path $packageDir "csharp-compounding-docs/bin"
        New-Item -ItemType Directory -Path $mcpServerDest -Force | Out-Null
        Copy-Item -Path "$publishDir/*" -Destination $mcpServerDest -Recurse
    }

    # Copy scripts
    $scriptsDest = Join-Path $packageDir "csharp-compounding-docs/scripts"
    New-Item -ItemType Directory -Path $scriptsDest -Force | Out-Null
    Copy-Item -Path (Join-Path $RepoRoot "scripts/launch-mcp-server.ps1") -Destination $scriptsDest
}
Write-Host "  Package directory created: $packageDir"

# Step 6: Create zip archive
Write-Host "`n[6/7] Creating zip archive..." -ForegroundColor Cyan
if (-not $DryRun) {
    if (Test-Path $packageZip) { Remove-Item $packageZip -Force }
    Compress-Archive -Path "$packageDir/*" -DestinationPath $packageZip -CompressionLevel Optimal
}
Write-Host "  Created: $packageZip"

# Step 7: Validate package
Write-Host "`n[7/7] Validating package contents..." -ForegroundColor Cyan
if (-not $DryRun) {
    $zipContents = [System.IO.Compression.ZipFile]::OpenRead($packageZip)
    $requiredFiles = @(
        "csharp-compounding-docs/manifest.json",
        "csharp-compounding-docs/.claude-plugin/",
        "csharp-compounding-docs/skills/",
        "csharp-compounding-docs/scripts/launch-mcp-server.ps1"
    )

    $missingFiles = @()
    foreach ($required in $requiredFiles) {
        $found = $zipContents.Entries | Where-Object { $_.FullName -like "*$required*" }
        if (-not $found) {
            $missingFiles += $required
        }
    }
    $zipContents.Dispose()

    if ($missingFiles.Count -gt 0) {
        Write-Host "  WARNING: Missing required files:" -ForegroundColor Yellow
        $missingFiles | ForEach-Object { Write-Host "    - $_" -ForegroundColor Yellow }
    } else {
        Write-Host "  Package validation passed" -ForegroundColor Green
    }
}

Write-Host "`n=== Release Package Complete ===" -ForegroundColor Cyan
Write-Host "Output: $packageZip" -ForegroundColor Green
Write-Host "Size: $([math]::Round((Get-Item $packageZip).Length / 1KB, 2)) KB" -ForegroundColor Green
```

### Registry Update Script

Create `scripts/update-registry.ps1`:

```powershell
#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Updates the marketplace plugin registry with a new version.

.PARAMETER Version
    The new version being released.

.PARAMETER MarketplacePath
    Path to the marketplace directory. Defaults to ./marketplace/
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$MarketplacePath = "./marketplace"
)

$ErrorActionPreference = "Stop"

Write-Host "Updating plugin registry for version $Version..." -ForegroundColor Cyan

# Update api/plugins.json
$registryPath = Join-Path $MarketplacePath "api/plugins.json"
$registry = Get-Content $registryPath -Raw | ConvertFrom-Json

# Find and update our plugin
$plugin = $registry.plugins | Where-Object { $_.id -eq "csharp-compounding-docs" }
if ($plugin) {
    $plugin.version = $Version
    $plugin.download_url = "/plugins/csharp-compounding-docs/versions/$Version/plugin.zip"
}

# Update registry metadata
$registry.updated = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

# Write back
$registry | ConvertTo-Json -Depth 10 | Set-Content $registryPath -NoNewline

Write-Host "Registry updated successfully" -ForegroundColor Green

# Create/update latest symlink (or copy for Windows compatibility)
$versionsDir = Join-Path $MarketplacePath "plugins/csharp-compounding-docs/versions"
$latestDir = Join-Path $versionsDir "latest"
$versionDir = Join-Path $versionsDir $Version

if (Test-Path $latestDir) {
    Remove-Item $latestDir -Recurse -Force
}

# On Windows, create a directory junction; on Unix, create symlink
if ($IsWindows) {
    cmd /c mklink /J "$latestDir" "$versionDir"
} else {
    New-Item -ItemType SymbolicLink -Path $latestDir -Target $versionDir -Force
}

Write-Host "Updated 'latest' symlink to point to $Version" -ForegroundColor Green
```

### GitHub Actions Release Workflow

Create `.github/workflows/release.yml`:

```yaml
name: Release Plugin

on:
  push:
    tags:
      - 'v*'

permissions:
  contents: write
  pages: write
  id-token: write

env:
  DOTNET_VERSION: '9.0.x'
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true
  DOTNET_CLI_TELEMETRY_OPTOUT: true

jobs:
  validate:
    name: Validate Release
    runs-on: ubuntu-latest
    outputs:
      version: ${{ steps.version.outputs.version }}
      is_prerelease: ${{ steps.version.outputs.is_prerelease }}
    steps:
      - name: Checkout code
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Extract version from tag
        id: version
        run: |
          VERSION="${GITHUB_REF#refs/tags/v}"
          echo "version=$VERSION" >> $GITHUB_OUTPUT

          if [[ "$VERSION" == *-* ]]; then
            echo "is_prerelease=true" >> $GITHUB_OUTPUT
          else
            echo "is_prerelease=false" >> $GITHUB_OUTPUT
          fi

          echo "Releasing version: $VERSION"

      - name: Validate CHANGELOG entry exists
        run: |
          VERSION="${{ steps.version.outputs.version }}"
          if ! grep -q "## \[$VERSION\]" CHANGELOG.md; then
            echo "ERROR: No CHANGELOG entry found for version $VERSION"
            exit 1
          fi

  build:
    name: Build and Test
    runs-on: ubuntu-latest
    needs: validate
    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      - name: Restore dependencies
        run: dotnet restore

      - name: Build solution
        run: dotnet build -c Release --no-restore

      - name: Run tests
        run: dotnet test -c Release --no-build --verbosity normal

      - name: Upload build artifacts
        uses: actions/upload-artifact@v4
        with:
          name: build-output
          path: |
            src/CompoundDocs.McpServer/bin/Release/
            src/CompoundDocs.Common/bin/Release/

  package:
    name: Create Release Package
    runs-on: ubuntu-latest
    needs: [validate, build]
    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Download build artifacts
        uses: actions/download-artifact@v4
        with:
          name: build-output
          path: ./artifacts

      - name: Setup PowerShell
        uses: bjompen/Install-PSModules@v1
        with:
          modules: PSScriptAnalyzer

      - name: Run package script
        shell: pwsh
        run: |
          ./scripts/package-release.ps1 -Version "${{ needs.validate.outputs.version }}" -SkipBuild

      - name: Upload release package
        uses: actions/upload-artifact@v4
        with:
          name: release-package
          path: release/plugin-${{ needs.validate.outputs.version }}.zip

  update-marketplace:
    name: Update Marketplace
    runs-on: ubuntu-latest
    needs: [validate, package]
    steps:
      - name: Checkout code
        uses: actions/checkout@v4
        with:
          ref: gh-pages
          fetch-depth: 0

      - name: Download release package
        uses: actions/download-artifact@v4
        with:
          name: release-package
          path: ./release

      - name: Setup PowerShell
        shell: bash
        run: |
          # PowerShell is pre-installed on ubuntu-latest

      - name: Create version directory
        run: |
          VERSION="${{ needs.validate.outputs.version }}"
          mkdir -p "marketplace/plugins/csharp-compounding-docs/versions/$VERSION"
          cp "release/plugin-$VERSION.zip" "marketplace/plugins/csharp-compounding-docs/versions/$VERSION/"

      - name: Update manifest in marketplace
        shell: pwsh
        run: |
          $VERSION = "${{ needs.validate.outputs.version }}"
          $manifestSrc = "plugins/csharp-compounding-docs/manifest.json"
          $manifestDest = "marketplace/plugins/csharp-compounding-docs/manifest.json"

          # Checkout main branch manifest temporarily
          git checkout main -- $manifestSrc
          Copy-Item $manifestSrc $manifestDest -Force
          git checkout gh-pages

      - name: Update registry
        shell: pwsh
        run: |
          ./scripts/update-registry.ps1 -Version "${{ needs.validate.outputs.version }}"

      - name: Update latest symlink
        run: |
          VERSION="${{ needs.validate.outputs.version }}"
          cd marketplace/plugins/csharp-compounding-docs/versions
          rm -f latest
          ln -s "$VERSION" latest

      - name: Commit and push marketplace updates
        run: |
          VERSION="${{ needs.validate.outputs.version }}"
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"

          git add marketplace/
          git commit -m "Release v$VERSION to marketplace"
          git push origin gh-pages

  create-release:
    name: Create GitHub Release
    runs-on: ubuntu-latest
    needs: [validate, package, update-marketplace]
    steps:
      - name: Checkout code
        uses: actions/checkout@v4

      - name: Download release package
        uses: actions/download-artifact@v4
        with:
          name: release-package
          path: ./release

      - name: Extract changelog section
        id: changelog
        run: |
          VERSION="${{ needs.validate.outputs.version }}"
          # Extract the section for this version from CHANGELOG.md
          CHANGELOG=$(awk "/^## \[$VERSION\]/{flag=1; next} /^## \[/{flag=0} flag" CHANGELOG.md)

          # Escape for GitHub Actions output
          CHANGELOG="${CHANGELOG//'%'/'%25'}"
          CHANGELOG="${CHANGELOG//$'\n'/'%0A'}"
          CHANGELOG="${CHANGELOG//$'\r'/'%0D'}"

          echo "content=$CHANGELOG" >> $GITHUB_OUTPUT

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v1
        with:
          name: v${{ needs.validate.outputs.version }}
          body: |
            ## What's Changed

            ${{ steps.changelog.outputs.content }}

            ## Installation

            ```bash
            claude plugin install csharp-compounding-docs@csharp-compound-marketplace
            ```

            ## Downloads

            - [Plugin Package](https://github.com/${{ github.repository }}/releases/download/v${{ needs.validate.outputs.version }}/plugin-${{ needs.validate.outputs.version }}.zip)
            - [Marketplace](https://${{ github.repository_owner }}.github.io/csharp-compound-engineering/)
          files: |
            release/plugin-${{ needs.validate.outputs.version }}.zip
          prerelease: ${{ needs.validate.outputs.is_prerelease }}
          generate_release_notes: false
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

  notify:
    name: Post-Release Notifications
    runs-on: ubuntu-latest
    needs: [validate, create-release]
    if: ${{ needs.validate.outputs.is_prerelease == 'false' }}
    steps:
      - name: Release summary
        run: |
          echo "## Release v${{ needs.validate.outputs.version }} Complete!" >> $GITHUB_STEP_SUMMARY
          echo "" >> $GITHUB_STEP_SUMMARY
          echo "- GitHub Release: https://github.com/${{ github.repository }}/releases/tag/v${{ needs.validate.outputs.version }}" >> $GITHUB_STEP_SUMMARY
          echo "- Marketplace: https://${{ github.repository_owner }}.github.io/csharp-compound-engineering/" >> $GITHUB_STEP_SUMMARY
```

### CHANGELOG Template

Create `CHANGELOG.md` following Keep a Changelog format:

```markdown
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- _New features go here_

### Changed
- _Changes to existing functionality go here_

### Deprecated
- _Soon-to-be removed features go here_

### Removed
- _Removed features go here_

### Fixed
- _Bug fixes go here_

### Security
- _Security fixes go here_

## [1.0.0] - YYYY-MM-DD

### Added
- Initial release of CSharp Compound Docs plugin
- RAG-powered semantic search for institutional knowledge
- 18 skills for knowledge capture and retrieval
- MCP server with PostgreSQL + pgvector backend
- Ollama integration for embeddings and generation
- Multi-tenant support with project isolation
- GitHub Pages marketplace hosting

[Unreleased]: https://github.com/username/csharp-compound-engineering/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/username/csharp-compound-engineering/releases/tag/v1.0.0
```

### Release Checklist Script

Create `scripts/prepare-release.ps1` for manual release preparation:

```powershell
#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Interactive release preparation checklist.

.PARAMETER Version
    The version to prepare for release.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

Write-Host "=== Release Preparation Checklist ===" -ForegroundColor Cyan
Write-Host "Version: $Version`n" -ForegroundColor Green

$checklist = @(
    @{ Task = "CHANGELOG.md has entry for [$Version]"; Auto = $false }
    @{ Task = "All tests passing"; Auto = $true; Check = { (dotnet test --no-build -q 2>&1) -match "Passed!" } }
    @{ Task = "No uncommitted changes"; Auto = $true; Check = { -not (git status --porcelain) } }
    @{ Task = "On main/master branch"; Auto = $true; Check = { (git branch --show-current) -in @("main", "master") } }
    @{ Task = "Version not already released"; Auto = $true; Check = { -not (git tag -l "v$Version") } }
)

$failed = @()

foreach ($item in $checklist) {
    Write-Host -NoNewline "[ ] $($item.Task)... "

    if ($item.Auto -and $item.Check) {
        $result = & $item.Check
        if ($result) {
            Write-Host "[OK]" -ForegroundColor Green
        } else {
            Write-Host "[FAIL]" -ForegroundColor Red
            $failed += $item.Task
        }
    } else {
        Write-Host "[MANUAL CHECK]" -ForegroundColor Yellow
    }
}

if ($failed.Count -gt 0) {
    Write-Host "`nRelease blocked by:" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "`nAll automated checks passed!" -ForegroundColor Green
Write-Host "`nTo create the release, run:" -ForegroundColor Cyan
Write-Host "  git tag -a v$Version -m `"Release v$Version`"" -ForegroundColor White
Write-Host "  git push origin v$Version" -ForegroundColor White
```

---

## Dependencies

### Depends On
- **Phase 125**: GitHub Pages Setup (marketplace hosting infrastructure)
- **Phase 127**: Plugin Manifest (manifest.json format and location)

### Blocks
- **Phase 133**: Marketplace Landing Page (needs release mechanism)
- **Phase 134**: Plugin Update Mechanism (uses release artifacts)

---

## Verification Steps

After completing this phase, verify:

1. **Package script works**:
   ```bash
   ./scripts/package-release.ps1 -Version 0.1.0-test -DryRun
   ```

2. **Version validation**:
   ```bash
   # Should fail with invalid version
   ./scripts/package-release.ps1 -Version "not-a-version"
   ```

3. **Workflow syntax valid**:
   ```bash
   # Using GitHub CLI
   gh workflow view release.yml
   ```

4. **CHANGELOG format correct**:
   - Verify markdown renders properly
   - Confirm links are valid

5. **Test release (pre-release)**:
   ```bash
   git tag -a v0.1.0-alpha.1 -m "Test release"
   git push origin v0.1.0-alpha.1
   # Verify workflow runs and creates GitHub pre-release
   ```

---

## Notes

- The release workflow requires `contents: write` permission to create GitHub releases
- Pre-release versions (containing `-`) are automatically marked as pre-release in GitHub
- The `gh-pages` branch must exist before the first release (created in Phase 125)
- Consider adding branch protection rules to prevent accidental pushes to release tags
- The `update-registry.ps1` script handles both Windows (junction) and Unix (symlink) for the `latest` pointer
- Secrets are not required as `GITHUB_TOKEN` is automatically provided by GitHub Actions
