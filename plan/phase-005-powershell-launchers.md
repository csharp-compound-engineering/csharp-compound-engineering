# Phase 005: PowerShell Launcher Scripts

> **Status**: [PLANNED]
> **Estimated Effort**: M
> **Prerequisites**: Phase 003 (Docker Compose Stack), Phase 004 (Ollama Configuration)
> **Category**: Infrastructure Setup

---

## Spec References

- [SPEC.md - Infrastructure Overview](../SPEC.md#infrastructure)
- [spec/infrastructure.md - PowerShell Launcher Script](../spec/infrastructure.md#powershell-launcher-script)
- [spec/infrastructure.md - MCP Server Launch](../spec/infrastructure.md#mcp-server-launch)
- [spec/infrastructure.md - First-Run Experience](../spec/infrastructure.md#first-run-experience)
- [spec/infrastructure.md - Shutdown Behavior](../spec/infrastructure.md#shutdown-behavior)

---

## Objectives

1. Implement `scripts/start-infrastructure.ps1` for Docker Compose stack management
2. Implement `scripts/launch-mcp-server.ps1` for MCP server process launching
3. Implement `scripts/stop-infrastructure.ps1` for optional cleanup
4. Ensure cross-platform compatibility (Windows, macOS, Linux)
5. Provide clear error messages for missing prerequisites

---

## Acceptance Criteria

- [ ] `start-infrastructure.ps1` creates `~/.claude/.csharp-compounding-docs/` directory if missing
- [ ] `start-infrastructure.ps1` copies template docker-compose.yml if missing
- [ ] `start-infrastructure.ps1` creates default ollama-config.json if missing
- [ ] `start-infrastructure.ps1` reads ollama-config.json and injects GPU configuration into compose
- [ ] `start-infrastructure.ps1` checks Docker availability and provides clear error if not installed
- [ ] `start-infrastructure.ps1` starts Docker Compose stack if not running
- [ ] `start-infrastructure.ps1` waits for PostgreSQL health check to pass
- [ ] `start-infrastructure.ps1` waits for Ollama health check to pass
- [ ] `start-infrastructure.ps1` auto-pulls required Ollama models (`mxbai-embed-large`, configured generation model)
- [ ] `start-infrastructure.ps1` outputs JSON connection parameters to stdout
- [ ] `launch-mcp-server.ps1` calls `start-infrastructure.ps1` and parses output
- [ ] `launch-mcp-server.ps1` launches MCP server with correct connection parameters
- [ ] `launch-mcp-server.ps1` handles Apple Silicon by detecting native Ollama at default port
- [ ] `stop-infrastructure.ps1` tears down Docker Compose stack
- [ ] All scripts use PowerShell 7+ cross-platform shebang (`#!/usr/bin/env pwsh`)
- [ ] All scripts handle errors gracefully with descriptive messages

---

## Implementation Notes

### Directory Structure

```
scripts/
├── start-infrastructure.ps1    # Infrastructure management
├── launch-mcp-server.ps1       # MCP server launcher (called by Claude Code)
├── stop-infrastructure.ps1     # Optional cleanup script
└── templates/                  # Template files to copy on first run
    ├── docker-compose.yml
    └── ollama-config.json
```

### start-infrastructure.ps1

#### Shebang and Script Header

```powershell
#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Starts the CSharp Compounding Docs infrastructure (PostgreSQL + Ollama).

.DESCRIPTION
    Manages the Docker Compose stack for the CSharp Compounding Docs plugin.
    Creates configuration directory, copies templates, handles GPU configuration,
    starts containers, and outputs connection parameters as JSON.

.OUTPUTS
    JSON object with postgres and ollama connection parameters.
#>
```

#### Configuration Variables

```powershell
$ErrorActionPreference = 'Stop'

$CompoundingDocsDir = Join-Path $HOME ".claude/.csharp-compounding-docs"
$DockerComposeFile = Join-Path $CompoundingDocsDir "docker-compose.yml"
$OllamaConfigFile = Join-Path $CompoundingDocsDir "ollama-config.json"
$DataDir = Join-Path $CompoundingDocsDir "data/pgdata"
$OllamaModelsDir = Join-Path $CompoundingDocsDir "ollama/models"
$ComposeProjectName = "csharp-compounding-docs"

# Template locations (relative to script)
$TemplateDir = Join-Path $PSScriptRoot "templates"
```

#### Docker Check Function

```powershell
function Test-DockerAvailable {
    try {
        $null = docker version 2>&1
        if ($LASTEXITCODE -ne 0) {
            return $false
        }
        return $true
    }
    catch {
        return $false
    }
}

function Assert-DockerAvailable {
    if (-not (Test-DockerAvailable)) {
        Write-Error @"
ERROR: Docker is required but not installed or not running.

Please install Docker Desktop from:
  https://www.docker.com/products/docker-desktop/

After installation, ensure Docker is running and try again.
"@
        exit 1
    }
}
```

#### Directory and Template Setup

```powershell
function Initialize-ConfigDirectory {
    # Create main directory
    if (-not (Test-Path $CompoundingDocsDir)) {
        New-Item -ItemType Directory -Path $CompoundingDocsDir -Force | Out-Null
    }

    # Create data directories
    if (-not (Test-Path $DataDir)) {
        New-Item -ItemType Directory -Path $DataDir -Force | Out-Null
    }
    if (-not (Test-Path $OllamaModelsDir)) {
        New-Item -ItemType Directory -Path $OllamaModelsDir -Force | Out-Null
    }

    # Copy docker-compose.yml template if missing
    if (-not (Test-Path $DockerComposeFile)) {
        $templateCompose = Join-Path $TemplateDir "docker-compose.yml"
        Copy-Item -Path $templateCompose -Destination $DockerComposeFile
    }

    # Copy ollama-config.json template if missing
    if (-not (Test-Path $OllamaConfigFile)) {
        $templateConfig = Join-Path $TemplateDir "ollama-config.json"
        Copy-Item -Path $templateConfig -Destination $OllamaConfigFile
    }
}
```

#### GPU Configuration Injection

```powershell
function Get-OllamaConfig {
    $config = Get-Content $OllamaConfigFile | ConvertFrom-Json
    return $config
}

function Update-ComposeForGpu {
    param (
        [Parameter(Mandatory)]
        [PSCustomObject]$OllamaConfig
    )

    if (-not $OllamaConfig.gpu.enabled) {
        return
    }

    $composeContent = Get-Content $DockerComposeFile -Raw
    $gpuType = $OllamaConfig.gpu.type

    switch ($gpuType) {
        "nvidia" {
            # Inject NVIDIA GPU configuration
            $gpuConfig = @"
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
"@
            # Insert after ollama service definition
            # Implementation: Parse YAML and inject, or use string manipulation
        }
        "amd" {
            # Inject AMD GPU configuration
            $gpuConfig = @"
    devices:
      - /dev/kfd
      - /dev/dri
    group_add:
      - video
"@
        }
        "apple" {
            # Apple Silicon: Skip Docker Ollama, use native
            # This is handled in the connection output phase
            Write-Host "Apple Silicon detected - will use native Ollama" -ForegroundColor Yellow
        }
    }

    # Note: Actual YAML manipulation requires careful implementation
    # Consider using a YAML library or template substitution approach
}
```

#### Health Check Functions

```powershell
function Wait-ForPostgresHealth {
    param (
        [int]$TimeoutSeconds = 120,
        [int]$IntervalSeconds = 5
    )

    $elapsed = 0
    Write-Host "Waiting for PostgreSQL to be healthy..." -ForegroundColor Cyan

    while ($elapsed -lt $TimeoutSeconds) {
        $health = docker inspect --format='{{.State.Health.Status}}' `
            "${ComposeProjectName}-postgres-1" 2>$null

        if ($health -eq "healthy") {
            Write-Host "PostgreSQL is healthy" -ForegroundColor Green
            return $true
        }

        Start-Sleep -Seconds $IntervalSeconds
        $elapsed += $IntervalSeconds
        Write-Host "  Still waiting... ($elapsed s)" -ForegroundColor Gray
    }

    Write-Error "PostgreSQL failed to become healthy within $TimeoutSeconds seconds"
    return $false
}

function Wait-ForOllamaHealth {
    param (
        [int]$Port = 11435,
        [int]$TimeoutSeconds = 60,
        [int]$IntervalSeconds = 5
    )

    $elapsed = 0
    Write-Host "Waiting for Ollama to be ready..." -ForegroundColor Cyan

    while ($elapsed -lt $TimeoutSeconds) {
        try {
            $response = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/api/tags" `
                -Method Get -TimeoutSec 5 -ErrorAction SilentlyContinue
            Write-Host "Ollama is ready" -ForegroundColor Green
            return $true
        }
        catch {
            Start-Sleep -Seconds $IntervalSeconds
            $elapsed += $IntervalSeconds
            Write-Host "  Still waiting... ($elapsed s)" -ForegroundColor Gray
        }
    }

    Write-Error "Ollama failed to become ready within $TimeoutSeconds seconds"
    return $false
}
```

#### Model Pre-Pull Logic

```powershell
function Invoke-OllamaModelPull {
    param (
        [Parameter(Mandatory)]
        [string]$ModelName,
        [int]$Port = 11435
    )

    Write-Host "Checking for model: $ModelName" -ForegroundColor Cyan

    # Check if model exists
    try {
        $tags = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/api/tags" -Method Get
        $existingModels = $tags.models | ForEach-Object { $_.name }

        if ($ModelName -in $existingModels) {
            Write-Host "  Model $ModelName already available" -ForegroundColor Green
            return
        }
    }
    catch {
        Write-Warning "Could not check existing models: $_"
    }

    # Pull the model
    Write-Host "  Pulling model $ModelName (this may take a while)..." -ForegroundColor Yellow

    $pullBody = @{ name = $ModelName } | ConvertTo-Json
    try {
        # Use streaming endpoint for progress
        Invoke-RestMethod -Uri "http://127.0.0.1:$Port/api/pull" `
            -Method Post -Body $pullBody -ContentType "application/json"
        Write-Host "  Model $ModelName pulled successfully" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to pull model $ModelName : $_"
    }
}

function Initialize-OllamaModels {
    param (
        [Parameter(Mandatory)]
        [PSCustomObject]$OllamaConfig,
        [int]$Port = 11435
    )

    # Always pull the embedding model (fixed)
    Invoke-OllamaModelPull -ModelName "mxbai-embed-large" -Port $Port

    # Pull the configured generation model
    $generationModel = $OllamaConfig.generation_model
    if ($generationModel) {
        Invoke-OllamaModelPull -ModelName $generationModel -Port $Port
    }
}
```

#### Stack Management

```powershell
function Test-StackRunning {
    try {
        $status = docker compose -p $ComposeProjectName ps --format json 2>$null
        if (-not $status) {
            return $false
        }

        $containers = $status | ConvertFrom-Json
        # Check if all expected containers are running
        $allRunning = $containers | Where-Object { $_.State -eq "running" }
        return ($allRunning.Count -ge 2)  # postgres + ollama
    }
    catch {
        return $false
    }
}

function Start-InfrastructureStack {
    Write-Host "Starting Docker Compose stack..." -ForegroundColor Cyan

    Push-Location $CompoundingDocsDir
    try {
        docker compose -p $ComposeProjectName -f $DockerComposeFile up -d
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to start Docker Compose stack"
            exit 1
        }
    }
    finally {
        Pop-Location
    }

    Write-Host "Docker Compose stack started" -ForegroundColor Green
}
```

#### Apple Silicon Detection

```powershell
function Test-AppleSilicon {
    if ($IsMacOS) {
        $arch = uname -m
        return ($arch -eq "arm64")
    }
    return $false
}

function Get-NativeOllamaPort {
    # Check if native Ollama is running on default port
    try {
        $response = Invoke-RestMethod -Uri "http://127.0.0.1:11434/api/tags" `
            -Method Get -TimeoutSec 2 -ErrorAction SilentlyContinue
        return 11434  # Native Ollama default port
    }
    catch {
        return $null
    }
}
```

#### Main Script Flow

```powershell
function Main {
    # 1. Check Docker
    Assert-DockerAvailable

    # 2. Initialize directories and templates
    Initialize-ConfigDirectory

    # 3. Read Ollama config
    $ollamaConfig = Get-OllamaConfig

    # 4. Handle GPU configuration
    Update-ComposeForGpu -OllamaConfig $ollamaConfig

    # 5. Determine Ollama port (native vs Docker)
    $ollamaPort = 11435  # Docker default
    $useNativeOllama = $false

    if (Test-AppleSilicon) {
        $nativePort = Get-NativeOllamaPort
        if ($nativePort) {
            $ollamaPort = $nativePort
            $useNativeOllama = $true
            Write-Host "Using native Ollama on Apple Silicon (port $ollamaPort)" -ForegroundColor Cyan
        }
        else {
            Write-Warning "Apple Silicon detected but native Ollama not running. Attempting Docker..."
        }
    }

    # 6. Start stack if not running
    if (-not (Test-StackRunning)) {
        Start-InfrastructureStack
    }
    else {
        Write-Host "Docker Compose stack already running" -ForegroundColor Green
    }

    # 7. Wait for health
    Wait-ForPostgresHealth
    if (-not $useNativeOllama) {
        Wait-ForOllamaHealth -Port $ollamaPort
    }

    # 8. Initialize Ollama models
    Initialize-OllamaModels -OllamaConfig $ollamaConfig -Port $ollamaPort

    # 9. Output connection parameters as JSON
    $connectionInfo = @{
        postgres = @{
            host = "127.0.0.1"
            port = 5433
            database = "compounding_docs"
            username = "compounding"
            password = "compounding"
        }
        ollama = @{
            host = "127.0.0.1"
            port = $ollamaPort
        }
    }

    # Output only JSON (no other stdout)
    $connectionInfo | ConvertTo-Json -Compress
}

# Execute main
Main
```

### launch-mcp-server.ps1

```powershell
#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Launches the CSharp Compounding Docs MCP server.

.DESCRIPTION
    Starts infrastructure if needed, then launches the MCP server
    with the appropriate connection parameters. This script is
    called by Claude Code via the MCP configuration.
#>

$ErrorActionPreference = 'Stop'

# 1. Start infrastructure and capture connection info
$startScript = Join-Path $PSScriptRoot "start-infrastructure.ps1"

# Redirect stderr to capture progress messages, stdout for JSON
$infraOutput = & $startScript 2>&1 | ForEach-Object {
    if ($_ -is [System.Management.Automation.ErrorRecord]) {
        # Write progress to stderr for user visibility
        [Console]::Error.WriteLine($_)
    }
    else {
        # Collect stdout (the JSON output)
        $_
    }
}

# Parse the JSON output (last line should be the JSON)
$jsonLine = $infraOutput | Select-Object -Last 1
try {
    $infraConfig = $jsonLine | ConvertFrom-Json
}
catch {
    Write-Error "Failed to parse infrastructure configuration: $jsonLine"
    exit 1
}

# 2. Determine MCP server executable path
$mcpServerPath = Join-Path $PSScriptRoot "../src/CompoundDocs.McpServer/bin/Release/net10.0/CompoundDocs.McpServer"

# Platform-specific executable extension
if ($IsWindows) {
    $mcpServerPath += ".exe"
}

# Verify executable exists
if (-not (Test-Path $mcpServerPath)) {
    # Try Debug build as fallback
    $mcpServerPath = $mcpServerPath -replace "Release", "Debug"
    if (-not (Test-Path $mcpServerPath)) {
        Write-Error "MCP Server executable not found. Please build the project first."
        exit 1
    }
}

# 3. Launch MCP server with connection parameters
& $mcpServerPath `
    --postgres-host $infraConfig.postgres.host `
    --postgres-port $infraConfig.postgres.port `
    --postgres-database $infraConfig.postgres.database `
    --postgres-user $infraConfig.postgres.username `
    --postgres-password $infraConfig.postgres.password `
    --ollama-host $infraConfig.ollama.host `
    --ollama-port $infraConfig.ollama.port
```

### stop-infrastructure.ps1

```powershell
#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Stops the CSharp Compounding Docs infrastructure.

.DESCRIPTION
    Tears down the Docker Compose stack. Data is preserved
    in the mounted volumes.
#>

$ComposeProjectName = "csharp-compounding-docs"
$CompoundingDocsDir = Join-Path $HOME ".claude/.csharp-compounding-docs"
$DockerComposeFile = Join-Path $CompoundingDocsDir "docker-compose.yml"

Write-Host "Stopping CSharp Compounding Docs infrastructure..." -ForegroundColor Cyan

if (Test-Path $DockerComposeFile) {
    Push-Location $CompoundingDocsDir
    try {
        docker compose -p $ComposeProjectName down
        Write-Host "Infrastructure stopped successfully" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Host "No infrastructure configuration found" -ForegroundColor Yellow
}
```

### Template Files

#### templates/docker-compose.yml

This file should match the spec exactly - see Phase 003 for the full docker-compose.yml content.

#### templates/ollama-config.json

```json
{
  "generation_model": "mistral",
  "gpu": {
    "enabled": false,
    "type": null
  }
}
```

### Cross-Platform Considerations

1. **Path Handling**: Use `Join-Path` consistently instead of string concatenation
2. **Line Endings**: PowerShell 7 handles this automatically
3. **Executable Extensions**: Check `$IsWindows` and append `.exe` when needed
4. **Environment Variables**: Use `$HOME` instead of `$env:USERPROFILE` for cross-platform
5. **Process Execution**: Use `&` operator with proper quoting

### Error Handling Strategy

1. **Docker Not Installed**: Clear message with download link
2. **Docker Not Running**: Prompt user to start Docker Desktop
3. **Port Conflicts**: Let Docker Compose fail and show its error message
4. **Health Check Timeout**: Detailed timeout message with troubleshooting hints
5. **Model Pull Failure**: Warning with retry suggestion (don't block startup)

### Testing Notes

- Test on Windows PowerShell 7, macOS, and Linux
- Test with Docker not installed
- Test with Docker not running
- Test first-run experience (empty ~/.claude/.csharp-compounding-docs/)
- Test subsequent runs (containers already running)
- Test Apple Silicon with native Ollama
- Test Apple Silicon without native Ollama (fallback to Docker)
- Test GPU configurations (NVIDIA, AMD) where hardware available

---

## Dependencies

### Depends On
- **Phase 003 (Docker Compose Stack)**: The docker-compose.yml template and container configuration
- **Phase 004 (Ollama Configuration)**: The ollama-config.json schema and GPU configuration options

### Blocks
- **Phase 006**: MCP Server Host Configuration (needs launcher scripts to pass connection params)
- **All MCP Server phases**: Cannot run server without infrastructure launcher
- **Testing phases**: Integration tests depend on infrastructure startup

---

## Sub-Tasks

This phase is within the 500-line limit. No decomposition required.

---

## Verification Checklist

Before marking this phase complete:

1. [ ] All three scripts created and tested
2. [ ] Template files in scripts/templates/ directory
3. [ ] Cross-platform testing completed
4. [ ] Error messages are clear and actionable
5. [ ] JSON output format matches spec exactly
6. [ ] Health checks work reliably
7. [ ] Model pre-pull works for both embedding and generation models
8. [ ] Apple Silicon detection and native Ollama support works
