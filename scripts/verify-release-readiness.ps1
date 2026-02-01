#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Verifies that the project is ready for release.

.DESCRIPTION
    This script performs a comprehensive check of release readiness including:
    - Build verification (zero errors, zero warnings)
    - Test execution (all tests passing)
    - Documentation completeness
    - Skills validation
    - Marketplace manifest verification

.EXAMPLE
    ./scripts/verify-release-readiness.ps1
#>

[CmdletBinding()]
param(
    [Parameter(HelpMessage = "Skip tests to speed up verification")]
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$script:ExitCode = 0
$script:CheckErrors = @()
$script:Warnings = @()

function Write-Check {
    param(
        [Parameter(Mandatory)]
        [string]$Message,

        [Parameter(Mandatory)]
        [bool]$Success,

        [string]$Details = ""
    )

    if ($Success) {
        Write-Host "  [PASS] $Message" -ForegroundColor Green
    }
    else {
        Write-Host "  [FAIL] $Message" -ForegroundColor Red
        if ($Details) {
            Write-Host "         $Details" -ForegroundColor Yellow
        }
        $script:ExitCode = 1
        $script:CheckErrors += $Message
    }
}

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "=== $Title ===" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Warning {
    param([string]$Message)
    Write-Host "  [WARN] $Message" -ForegroundColor Yellow
    $script:Warnings += $Message
}

# Header
Write-Host ""
Write-Host "Release Readiness Verification" -ForegroundColor Magenta
Write-Host "===============================" -ForegroundColor Magenta
Write-Host ""

# Get project root
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
Push-Location $ProjectRoot

try {
    # 1. Build Verification
    Write-Section "Build Verification"

    Write-Host "  Building solution..."
    $buildOutput = dotnet build csharp-compounding-docs.sln --configuration Release --no-incremental 2>&1

    $buildErrors = $buildOutput | Select-String "error " | Measure-Object
    $buildWarnings = $buildOutput | Select-String "warning " | Measure-Object

    Write-Check "Build completes successfully" ($LASTEXITCODE -eq 0) "Exit code: $LASTEXITCODE"
    Write-Check "Zero build errors" ($buildErrors.Count -eq 0) "Found $($buildErrors.Count) errors"
    Write-Check "Zero build warnings" ($buildWarnings.Count -eq 0) "Found $($buildWarnings.Count) warnings"

    # 2. Test Verification
    Write-Section "Test Verification"

    if (-not $SkipTests) {
        Write-Host "  Running unit tests..."
        $testOutput = dotnet test tests/CompoundDocs.Tests --configuration Release --no-build 2>&1
        $testsPassed = $LASTEXITCODE -eq 0

        Write-Check "All unit tests pass" $testsPassed

        # Extract test counts from output
        $testSummary = $testOutput | Select-String "Passed:" | Select-Object -Last 1
        if ($testSummary) {
            Write-Host "  Test summary: $($testSummary.Line.Trim())" -ForegroundColor Gray
        }
    }
    else {
        Write-Warning "Tests skipped (--SkipTests flag)"
    }

    # 3. Solution Structure
    Write-Section "Solution Structure"

    Write-Check "Solution file exists" (Test-Path "csharp-compounding-docs.sln")
    Write-Check "McpServer project exists" (Test-Path "src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj")
    Write-Check "Common library exists" (Test-Path "src/CompoundDocs.Common/CompoundDocs.Common.csproj")
    Write-Check "Unit tests project exists" (Test-Path "tests/CompoundDocs.Tests/CompoundDocs.Tests.csproj")
    Write-Check "Integration tests project exists" (Test-Path "tests/CompoundDocs.IntegrationTests/CompoundDocs.IntegrationTests.csproj")
    Write-Check "E2E tests project exists" (Test-Path "tests/CompoundDocs.E2ETests/CompoundDocs.E2ETests.csproj")

    # 4. MCP Tools Verification
    Write-Section "MCP Tools"

    $toolFiles = @(
        "src/CompoundDocs.McpServer/Tools/ActivateProjectTool.cs",
        "src/CompoundDocs.McpServer/Tools/RagQueryTool.cs",
        "src/CompoundDocs.McpServer/Tools/SemanticSearchTool.cs",
        "src/CompoundDocs.McpServer/Tools/IndexDocumentTool.cs",
        "src/CompoundDocs.McpServer/Tools/ListDocTypesTool.cs",
        "src/CompoundDocs.McpServer/Tools/DeleteDocumentsTool.cs",
        "src/CompoundDocs.McpServer/Tools/UpdatePromotionLevelTool.cs",
        "src/CompoundDocs.McpServer/Tools/SearchExternalDocsTool.cs",
        "src/CompoundDocs.McpServer/Tools/RagQueryExternalTool.cs"
    )

    $toolsExist = $true
    foreach ($tool in $toolFiles) {
        if (-not (Test-Path $tool)) {
            $toolsExist = $false
            Write-Check "Tool exists: $(Split-Path -Leaf $tool)" $false
        }
    }

    Write-Check "All 9 MCP tools exist" $toolsExist

    # 5. Skills Verification
    Write-Section "Skills"

    $skillFiles = Get-ChildItem "skills/*.yaml" -ErrorAction SilentlyContinue
    $skillCount = $skillFiles.Count

    Write-Check "Skills directory has YAML files" ($skillCount -gt 0) "Found $skillCount skills"

    $requiredSkillFields = @("name", "description")
    $validSkills = $true

    foreach ($skill in $skillFiles) {
        $content = Get-Content $skill.FullName -Raw
        foreach ($field in $requiredSkillFields) {
            # Match field: at start of line (multiline mode)
            if (-not ($content -match "(?m)^$field`:")) {
                Write-Check "Skill '$($skill.Name)' has '$field' field" $false
                $validSkills = $false
            }
        }
    }

    Write-Check "All skills have required frontmatter fields" $validSkills

    # 6. Agents Verification
    Write-Section "Agents"

    $agentFiles = Get-ChildItem "agents/*.yaml" -ErrorAction SilentlyContinue
    $agentCount = $agentFiles.Count

    Write-Check "Agents directory has YAML files" ($agentCount -gt 0) "Found $agentCount agents"

    # 7. Documentation
    Write-Section "Documentation"

    Write-Check "README.md exists" (Test-Path "README.md")
    Write-Check "CONTRIBUTING.md exists" (Test-Path "CONTRIBUTING.md")
    Write-Check "SECURITY.md exists" (Test-Path "SECURITY.md")

    # 8. CI/CD Configuration
    Write-Section "CI/CD Configuration"

    Write-Check "CI workflow exists" (Test-Path ".github/workflows/ci.yml")
    Write-Check "Release workflow exists" (Test-Path ".github/workflows/release.yml")
    Write-Check "Docker workflow exists" (Test-Path ".github/workflows/docker.yml")

    # 9. Docker Configuration
    Write-Section "Docker Configuration"

    Write-Check "Dockerfile exists" (Test-Path "Dockerfile")
    Write-Check "docker-compose.yml exists" (Test-Path "docker-compose.yml")

    # 10. Liquibase Migrations
    Write-Section "Database Migrations"

    Write-Check "Liquibase changelog exists" (Test-Path "docker/postgres/changelog/changelog.xml")

    $migrations = Get-ChildItem "docker/postgres/changelog/changes/*/change.xml" -ErrorAction SilentlyContinue
    Write-Check "Migrations exist" ($migrations.Count -gt 0) "Found $($migrations.Count) migrations"

    # 11. Marketplace
    Write-Section "Marketplace"

    if (Test-Path "marketplace/manifest.json") {
        Write-Check "Marketplace manifest exists" $true

        $manifest = Get-Content "marketplace/manifest.json" | ConvertFrom-Json
        Write-Check "Manifest has version" ($null -ne $manifest.version)
        Write-Check "Manifest has description" ($null -ne $manifest.description)
    }
    else {
        Write-Check "Marketplace manifest exists" $false "marketplace/manifest.json not found"
    }

    # Summary
    Write-Host ""
    Write-Host "===============================" -ForegroundColor Magenta
    Write-Host "Summary" -ForegroundColor Magenta
    Write-Host "===============================" -ForegroundColor Magenta
    Write-Host ""

    if ($script:CheckErrors.Count -gt 0) {
        Write-Host "  Errors: $($script:CheckErrors.Count)" -ForegroundColor Red
        foreach ($error in $script:CheckErrors) {
            Write-Host "    - $error" -ForegroundColor Red
        }
    }

    if ($script:Warnings.Count -gt 0) {
        Write-Host ""
        Write-Host "  Warnings: $($script:Warnings.Count)" -ForegroundColor Yellow
        foreach ($warning in $script:Warnings) {
            Write-Host "    - $warning" -ForegroundColor Yellow
        }
    }

    Write-Host ""
    if ($script:ExitCode -eq 0) {
        Write-Host "  RELEASE READY" -ForegroundColor Green
    }
    else {
        Write-Host "  NOT READY FOR RELEASE" -ForegroundColor Red
        Write-Host "  Fix the issues above before releasing." -ForegroundColor Yellow
    }

    Write-Host ""
}
finally {
    Pop-Location
}

exit $script:ExitCode
