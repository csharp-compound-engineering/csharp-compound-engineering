#Requires -Version 7.0
<#
.SYNOPSIS
    Starts the Docker infrastructure for csharp-compounding-docs.

.DESCRIPTION
    This script manages the Docker Compose infrastructure including PostgreSQL
    (with pgvector and Liquibase) and Ollama for local development.

.PARAMETER Action
    The action to perform: start, stop, restart, status, logs, or pull.
    Default: start

.PARAMETER Profile
    Docker Compose profile to use. Default: default (no profile)

.PARAMETER Detached
    Run containers in detached mode. Default: $true

.PARAMETER Pull
    Pull latest images before starting. Default: $false

.PARAMETER Build
    Build images before starting. Default: $false

.PARAMETER Remove
    Remove volumes when stopping. Default: $false

.PARAMETER Service
    Specific service to operate on (postgres, ollama). Leave empty for all services.

.PARAMETER Follow
    Follow log output when viewing logs. Default: $false

.PARAMETER Tail
    Number of log lines to show. Default: 100

.PARAMETER WaitForHealthy
    Wait for services to be healthy before returning. Default: $true

.PARAMETER Timeout
    Timeout in seconds to wait for services to be healthy. Default: 120

.EXAMPLE
    ./start-infrastructure.ps1 -Action start
    Starts all infrastructure services.

.EXAMPLE
    ./start-infrastructure.ps1 -Action stop -Remove
    Stops all services and removes volumes.

.EXAMPLE
    ./start-infrastructure.ps1 -Action logs -Service postgres -Follow
    Shows and follows PostgreSQL logs.

.EXAMPLE
    ./start-infrastructure.ps1 -Action pull
    Pulls the latest images.

.NOTES
    Requires Docker and Docker Compose to be installed.
#>

[CmdletBinding()]
param(
    [ValidateSet("start", "stop", "restart", "status", "logs", "pull", "reset")]
    [string]$Action = "start",

    [string]$Profile = "",

    [bool]$Detached = $true,

    [switch]$Pull,

    [switch]$Build,

    [switch]$Remove,

    [ValidateSet("", "postgres", "ollama")]
    [string]$Service = "",

    [switch]$Follow,

    [int]$Tail = 100,

    [bool]$WaitForHealthy = $true,

    [int]$Timeout = 120
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Script variables
$ComposeFile = "docker-compose.yml"
$ProjectName = "csharp-compounding-docs"

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

# Find project root
function Get-ProjectRoot {
    $currentPath = Get-Location
    while ($currentPath -ne "") {
        if (Test-Path (Join-Path $currentPath $ComposeFile)) {
            return $currentPath
        }
        $parentPath = Split-Path $currentPath -Parent
        if ($parentPath -eq $currentPath) {
            break
        }
        $currentPath = $parentPath
    }
    return $null
}

# Verify prerequisites
function Test-Prerequisites {
    Write-Info "Checking prerequisites..."

    # Check Docker
    $dockerVersion = docker --version 2>$null
    if (-not $dockerVersion) {
        Write-Error "Docker is not installed or not in PATH"
        exit 1
    }
    Write-Info "Docker version: $dockerVersion"

    # Check if Docker daemon is running
    $dockerInfo = docker info 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Docker daemon is not running"
        exit 1
    }

    # Check Docker Compose
    $composeVersion = docker compose version 2>$null
    if (-not $composeVersion) {
        Write-Error "Docker Compose is not installed"
        exit 1
    }
    Write-Info "Docker Compose version: $composeVersion"

    Write-Success "Prerequisites check passed"
}

# Build compose command arguments
function Get-ComposeArgs {
    $args = @("compose", "-f", $ComposeFile, "-p", $ProjectName)

    if ($Profile) {
        $args += @("--profile", $Profile)
    }

    return $args
}

# Start infrastructure
function Start-Infrastructure {
    Write-Header "Starting Infrastructure"

    $composeArgs = Get-ComposeArgs

    if ($Pull) {
        Write-Info "Pulling latest images..."
        $pullArgs = $composeArgs + @("pull")
        if ($Service) {
            $pullArgs += $Service
        }
        docker @pullArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Failed to pull some images, continuing with existing images"
        }
    }

    $upArgs = $composeArgs + @("up")

    if ($Detached) {
        $upArgs += "-d"
    }

    if ($Build) {
        $upArgs += "--build"
    }

    if ($Service) {
        $upArgs += $Service
    }

    Write-Info "Starting services..."
    docker @upArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to start infrastructure"
        exit $LASTEXITCODE
    }

    if ($Detached -and $WaitForHealthy) {
        Wait-ForHealthy
    }

    Write-Success "Infrastructure started"
    Show-Status
}

# Stop infrastructure
function Stop-Infrastructure {
    Write-Header "Stopping Infrastructure"

    $composeArgs = Get-ComposeArgs
    $downArgs = $composeArgs + @("down")

    if ($Remove) {
        $downArgs += @("--volumes", "--remove-orphans")
        Write-Warning "Volumes will be removed!"
    }

    if ($Service) {
        # For stopping a single service, use stop instead of down
        $stopArgs = $composeArgs + @("stop", $Service)
        docker @stopArgs
    }
    else {
        docker @downArgs
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to stop infrastructure"
        exit $LASTEXITCODE
    }

    Write-Success "Infrastructure stopped"
}

# Restart infrastructure
function Restart-Infrastructure {
    Write-Header "Restarting Infrastructure"

    $composeArgs = Get-ComposeArgs
    $restartArgs = $composeArgs + @("restart")

    if ($Service) {
        $restartArgs += $Service
    }

    docker @restartArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to restart infrastructure"
        exit $LASTEXITCODE
    }

    if ($WaitForHealthy) {
        Wait-ForHealthy
    }

    Write-Success "Infrastructure restarted"
    Show-Status
}

# Show status
function Show-Status {
    Write-Header "Infrastructure Status"

    $composeArgs = Get-ComposeArgs
    $psArgs = $composeArgs + @("ps", "--format", "table {{.Name}}\t{{.Status}}\t{{.Ports}}")

    docker @psArgs

    Write-Host ""

    # Show container health
    $containers = @(
        @{ Name = "csharp-compounding-docs-postgres"; Port = "5433" },
        @{ Name = "csharp-compounding-docs-ollama"; Port = "11435" }
    )

    Write-Info "Service endpoints:"
    foreach ($container in $containers) {
        $health = docker inspect --format='{{.State.Health.Status}}' $container.Name 2>$null
        $status = if ($health) { $health } else { "unknown" }
        $color = switch ($status) {
            "healthy" { "Green" }
            "starting" { "Yellow" }
            "unhealthy" { "Red" }
            default { "Gray" }
        }
        Write-Host "  - $($container.Name): localhost:$($container.Port) [$status]" -ForegroundColor $color
    }
}

# Show logs
function Show-Logs {
    Write-Header "Infrastructure Logs"

    $composeArgs = Get-ComposeArgs
    $logsArgs = $composeArgs + @("logs", "--tail", $Tail.ToString())

    if ($Follow) {
        $logsArgs += "-f"
    }

    if ($Service) {
        $logsArgs += $Service
    }

    docker @logsArgs
}

# Pull images
function Pull-Images {
    Write-Header "Pulling Docker Images"

    $composeArgs = Get-ComposeArgs
    $pullArgs = $composeArgs + @("pull")

    if ($Service) {
        $pullArgs += $Service
    }

    docker @pullArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to pull images"
        exit $LASTEXITCODE
    }

    Write-Success "Images pulled"
}

# Reset infrastructure (stop, remove volumes, start fresh)
function Reset-Infrastructure {
    Write-Header "Resetting Infrastructure"

    Write-Warning "This will remove all data and volumes!"
    $confirmation = Read-Host "Are you sure you want to continue? (y/N)"
    if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
        Write-Info "Reset cancelled"
        return
    }

    # Stop and remove
    $Remove = $true
    Stop-Infrastructure

    # Start fresh
    $Pull = $true
    Start-Infrastructure

    Write-Success "Infrastructure reset completed"
}

# Wait for services to be healthy
function Wait-ForHealthy {
    Write-Info "Waiting for services to be healthy (timeout: ${Timeout}s)..."

    $startTime = Get-Date
    $healthyServices = @{}

    $servicesToCheck = if ($Service) { @($Service) } else { @("postgres") }

    foreach ($svc in $servicesToCheck) {
        $healthyServices[$svc] = $false
    }

    while (($healthyServices.Values | Where-Object { -not $_ }).Count -gt 0) {
        $elapsed = (Get-Date) - $startTime
        if ($elapsed.TotalSeconds -gt $Timeout) {
            Write-Warning "Timeout waiting for services to be healthy"
            break
        }

        foreach ($svc in $servicesToCheck) {
            if ($healthyServices[$svc]) { continue }

            $containerName = "${ProjectName}-${svc}-1"
            if ($svc -eq "postgres") {
                $containerName = "csharp-compounding-docs-postgres"
            }
            elseif ($svc -eq "ollama") {
                $containerName = "csharp-compounding-docs-ollama"
            }

            $health = docker inspect --format='{{.State.Health.Status}}' $containerName 2>$null
            if ($health -eq "healthy") {
                $healthyServices[$svc] = $true
                Write-Success "$svc is healthy"
            }
        }

        if (($healthyServices.Values | Where-Object { -not $_ }).Count -gt 0) {
            Start-Sleep -Seconds 2
            Write-Host "." -NoNewline
        }
    }

    Write-Host ""
}

# Main execution
function Main {
    # Find and change to project root
    $projectRoot = Get-ProjectRoot
    if (-not $projectRoot) {
        Write-Error "Could not find project root (looking for $ComposeFile)"
        exit 1
    }
    Push-Location $projectRoot

    try {
        Test-Prerequisites

        switch ($Action) {
            "start" { Start-Infrastructure }
            "stop" { Stop-Infrastructure }
            "restart" { Restart-Infrastructure }
            "status" { Show-Status }
            "logs" { Show-Logs }
            "pull" { Pull-Images }
            "reset" { Reset-Infrastructure }
        }
    }
    finally {
        Pop-Location
    }
}

# Run main function
Main
