#Requires -Version 7.0
<#
.SYNOPSIS
    Build script for csharp-compounding-docs solution.

.DESCRIPTION
    This script builds the solution, runs tests, and generates code coverage reports.
    It supports various build configurations and can be used in CI/CD pipelines.

.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Release

.PARAMETER Clean
    Clean the solution before building.

.PARAMETER RunTests
    Run tests after building. Default: $true

.PARAMETER GenerateCoverage
    Generate code coverage report. Default: $true

.PARAMETER CoverageThreshold
    Minimum code coverage percentage required. Default: 80

.PARAMETER Pack
    Create NuGet packages after building.

.PARAMETER Version
    Version to use for NuGet packages. Default: 0.0.0-local

.PARAMETER OutputDirectory
    Directory for build artifacts. Default: ./artifacts

.EXAMPLE
    ./build.ps1 -Configuration Release -RunTests -GenerateCoverage

.EXAMPLE
    ./build.ps1 -Clean -Pack -Version "1.0.0"

.NOTES
    Requires .NET SDK 9.0 or later.
#>

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [switch]$Clean,

    [bool]$RunTests = $true,

    [bool]$GenerateCoverage = $true,

    [ValidateRange(0, 100)]
    [int]$CoverageThreshold = 80,

    [switch]$Pack,

    [string]$Version = "0.0.0-local",

    [string]$OutputDirectory = "./artifacts"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Script variables
$SolutionFile = "csharp-compounding-docs.sln"
$TestResultsDirectory = "./TestResults"
$CoverageReportDirectory = "./CoverageReport"

# Colors for output
function Write-Header {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host " $Message" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor White
}

# Verify prerequisites
function Test-Prerequisites {
    Write-Header "Checking Prerequisites"

    # Check .NET SDK
    $dotnetVersion = dotnet --version 2>$null
    if (-not $dotnetVersion) {
        Write-Error ".NET SDK is not installed or not in PATH"
        exit 1
    }
    Write-Info ".NET SDK version: $dotnetVersion"

    # Check if solution file exists
    if (-not (Test-Path $SolutionFile)) {
        Write-Error "Solution file not found: $SolutionFile"
        exit 1
    }
    Write-Success "Prerequisites check passed"
}

# Clean the solution
function Invoke-Clean {
    Write-Header "Cleaning Solution"

    dotnet clean $SolutionFile --configuration $Configuration --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Clean failed"
        exit $LASTEXITCODE
    }

    # Remove additional directories
    $dirsToRemove = @($TestResultsDirectory, $CoverageReportDirectory, $OutputDirectory)
    foreach ($dir in $dirsToRemove) {
        if (Test-Path $dir) {
            Remove-Item $dir -Recurse -Force
            Write-Info "Removed: $dir"
        }
    }

    Write-Success "Clean completed"
}

# Restore dependencies
function Invoke-Restore {
    Write-Header "Restoring Dependencies"

    dotnet restore $SolutionFile
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Restore failed"
        exit $LASTEXITCODE
    }

    Write-Success "Restore completed"
}

# Build the solution
function Invoke-Build {
    Write-Header "Building Solution"

    $buildArgs = @(
        "build",
        $SolutionFile,
        "--configuration", $Configuration,
        "--no-restore",
        "-p:TreatWarningsAsErrors=true"
    )

    dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit $LASTEXITCODE
    }

    Write-Success "Build completed"
}

# Run tests
function Invoke-Tests {
    Write-Header "Running Tests"

    # Create test results directory
    if (-not (Test-Path $TestResultsDirectory)) {
        New-Item -ItemType Directory -Path $TestResultsDirectory -Force | Out-Null
    }

    $testArgs = @(
        "test",
        $SolutionFile,
        "--configuration", $Configuration,
        "--no-build",
        "--verbosity", "normal",
        "--logger", "trx;LogFileName=test-results.trx",
        "--results-directory", $TestResultsDirectory
    )

    if ($GenerateCoverage) {
        $testArgs += @(
            "--collect:XPlat Code Coverage",
            "--",
            "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura,opencover"
        )
    }

    dotnet @testArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed"
        exit $LASTEXITCODE
    }

    Write-Success "Tests completed"
}

# Generate coverage report
function Invoke-CoverageReport {
    Write-Header "Generating Coverage Report"

    # Find coverage files
    $coverageFiles = Get-ChildItem -Path $TestResultsDirectory -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue

    if ($coverageFiles.Count -eq 0) {
        Write-Warning "No coverage files found"
        return
    }

    # Check if reportgenerator is installed
    $reportGenerator = Get-Command reportgenerator -ErrorAction SilentlyContinue
    if (-not $reportGenerator) {
        Write-Info "Installing ReportGenerator tool..."
        dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.*
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to install ReportGenerator, skipping coverage report"
            return
        }
    }

    # Create coverage report directory
    if (-not (Test-Path $CoverageReportDirectory)) {
        New-Item -ItemType Directory -Path $CoverageReportDirectory -Force | Out-Null
    }

    # Generate report
    $coverageFileList = ($coverageFiles | ForEach-Object { $_.FullName }) -join ";"

    reportgenerator `
        -reports:$coverageFileList `
        -targetdir:$CoverageReportDirectory `
        -reporttypes:"Html;Cobertura;Badges;TextSummary"

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Coverage report generation failed"
        return
    }

    # Parse and check coverage threshold
    $summaryFile = Join-Path $CoverageReportDirectory "Summary.txt"
    if (Test-Path $summaryFile) {
        $summaryContent = Get-Content $summaryFile -Raw
        if ($summaryContent -match "Line coverage:\s*(\d+(?:\.\d+)?)%") {
            $lineCoverage = [decimal]$Matches[1]
            Write-Info "Line coverage: $lineCoverage%"

            if ($lineCoverage -lt $CoverageThreshold) {
                Write-Warning "Code coverage ($lineCoverage%) is below threshold ($CoverageThreshold%)"
            }
            else {
                Write-Success "Code coverage ($lineCoverage%) meets threshold ($CoverageThreshold%)"
            }
        }
    }

    Write-Success "Coverage report generated at: $CoverageReportDirectory"
}

# Create NuGet packages
function Invoke-Pack {
    Write-Header "Creating NuGet Packages"

    # Create output directory
    if (-not (Test-Path $OutputDirectory)) {
        New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    }

    $packArgs = @(
        "pack",
        $SolutionFile,
        "--configuration", $Configuration,
        "--no-build",
        "--output", $OutputDirectory,
        "-p:PackageVersion=$Version"
    )

    dotnet @packArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Pack failed"
        exit $LASTEXITCODE
    }

    $packages = Get-ChildItem -Path $OutputDirectory -Filter "*.nupkg"
    Write-Info "Created packages:"
    foreach ($package in $packages) {
        Write-Info "  - $($package.Name)"
    }

    Write-Success "NuGet packages created at: $OutputDirectory"
}

# Main execution
function Main {
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    Write-Header "Build Script for csharp-compounding-docs"
    Write-Info "Configuration: $Configuration"
    Write-Info "Run Tests: $RunTests"
    Write-Info "Generate Coverage: $GenerateCoverage"
    Write-Info "Pack: $Pack"
    Write-Info "Version: $Version"

    Test-Prerequisites

    if ($Clean) {
        Invoke-Clean
    }

    Invoke-Restore
    Invoke-Build

    if ($RunTests) {
        Invoke-Tests

        if ($GenerateCoverage) {
            Invoke-CoverageReport
        }
    }

    if ($Pack) {
        Invoke-Pack
    }

    $stopwatch.Stop()

    Write-Header "Build Completed"
    Write-Success "Total time: $($stopwatch.Elapsed.ToString('mm\:ss\.fff'))"
}

# Run main function
Main
