#Requires -Version 7.0
<#
.SYNOPSIS
    Launches the MCP server for csharp-compounding-docs.

.DESCRIPTION
    This script launches the CompoundDocs MCP server with configurable options
    for connecting to the infrastructure (PostgreSQL, Ollama) and various
    operational modes.

.PARAMETER Configuration
    Build configuration to use (Debug or Release). Default: Debug

.PARAMETER Build
    Build the project before launching. Default: $false

.PARAMETER ConnectionString
    PostgreSQL connection string. Default: from environment or localhost.

.PARAMETER OllamaUrl
    Ollama API URL. Default: from environment or localhost.

.PARAMETER EmbeddingModel
    Ollama model to use for embeddings. Default: nomic-embed-text

.PARAMETER ChatModel
    Ollama model to use for chat. Default: llama3.2

.PARAMETER LogLevel
    Logging level (Verbose, Debug, Information, Warning, Error, Fatal).
    Default: Information

.PARAMETER TransportMode
    MCP transport mode (stdio, sse). Default: stdio

.PARAMETER Port
    Port for SSE transport mode. Default: 3000

.PARAMETER WaitForInfrastructure
    Wait for infrastructure to be available before starting. Default: $true

.PARAMETER InfrastructureTimeout
    Timeout in seconds to wait for infrastructure. Default: 60

.PARAMETER Environment
    Environment name (Development, Staging, Production). Default: Development

.PARAMETER Watch
    Enable hot reload / watch mode (Debug only). Default: $false

.PARAMETER DryRun
    Show the command that would be executed without running it. Default: $false

.EXAMPLE
    ./launch-mcp-server.ps1
    Launches the MCP server with default settings.

.EXAMPLE
    ./launch-mcp-server.ps1 -Build -Watch
    Builds and launches with hot reload enabled.

.EXAMPLE
    ./launch-mcp-server.ps1 -Configuration Release -TransportMode sse -Port 3001
    Launches in release mode with SSE transport on port 3001.

.EXAMPLE
    ./launch-mcp-server.ps1 -LogLevel Debug -EmbeddingModel mxbai-embed-large
    Launches with debug logging and a different embedding model.

.NOTES
    Requires .NET SDK 9.0 or later.
    Requires infrastructure to be running (use start-infrastructure.ps1).
#>

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$Build,

    [string]$ConnectionString = "",

    [string]$OllamaUrl = "",

    [string]$EmbeddingModel = "nomic-embed-text",

    [string]$ChatModel = "llama3.2",

    [ValidateSet("Verbose", "Debug", "Information", "Warning", "Error", "Fatal")]
    [string]$LogLevel = "Information",

    [ValidateSet("stdio", "sse")]
    [string]$TransportMode = "stdio",

    [int]$Port = 3000,

    [bool]$WaitForInfrastructure = $true,

    [int]$InfrastructureTimeout = 60,

    [ValidateSet("Development", "Staging", "Production")]
    [string]$Environment = "Development",

    [switch]$Watch,

    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Script variables
$ProjectPath = "src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj"

# Default infrastructure settings
$DefaultPostgresConnectionString = "Host=localhost;Port=5433;Database=compounding_docs;Username=compounding;Password=compounding"
$DefaultOllamaUrl = "http://localhost:11435"

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
        if (Test-Path (Join-Path $currentPath "csharp-compounding-docs.sln")) {
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

    # Check .NET SDK
    $dotnetVersion = dotnet --version 2>$null
    if (-not $dotnetVersion) {
        Write-Error ".NET SDK is not installed or not in PATH"
        exit 1
    }
    Write-Info ".NET SDK version: $dotnetVersion"

    # Check if project exists
    if (-not (Test-Path $ProjectPath)) {
        Write-Error "Project file not found: $ProjectPath"
        exit 1
    }

    Write-Success "Prerequisites check passed"
}

# Test database connection
function Test-DatabaseConnection {
    param([string]$ConnString)

    Write-Info "Testing database connection..."

    try {
        # Use a simple TCP connection test
        $uri = [System.Uri]::new("tcp://localhost:5433")
        $tcpClient = New-Object System.Net.Sockets.TcpClient
        $tcpClient.Connect("localhost", 5433)
        $tcpClient.Close()
        Write-Success "Database is reachable"
        return $true
    }
    catch {
        Write-Warning "Database is not reachable: $($_.Exception.Message)"
        return $false
    }
}

# Test Ollama connection
function Test-OllamaConnection {
    param([string]$Url)

    Write-Info "Testing Ollama connection..."

    try {
        $response = Invoke-WebRequest -Uri "$Url/api/tags" -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
        Write-Success "Ollama is reachable"
        return $true
    }
    catch {
        Write-Warning "Ollama is not reachable: $($_.Exception.Message)"
        return $false
    }
}

# Wait for infrastructure
function Wait-ForInfrastructure {
    Write-Header "Waiting for Infrastructure"

    $connString = if ($ConnectionString) { $ConnectionString } else { $DefaultPostgresConnectionString }
    $ollamaUrl = if ($OllamaUrl) { $OllamaUrl } else { $DefaultOllamaUrl }

    $startTime = Get-Date
    $dbReady = $false
    $ollamaReady = $false

    while (-not ($dbReady -and $ollamaReady)) {
        $elapsed = (Get-Date) - $startTime
        if ($elapsed.TotalSeconds -gt $InfrastructureTimeout) {
            Write-Warning "Timeout waiting for infrastructure"
            if (-not $dbReady) {
                Write-Warning "Database is not available"
            }
            if (-not $ollamaReady) {
                Write-Warning "Ollama is not available"
            }
            Write-Warning "Server may not function correctly without infrastructure"
            break
        }

        if (-not $dbReady) {
            $dbReady = Test-DatabaseConnection -ConnString $connString
        }

        if (-not $ollamaReady) {
            $ollamaReady = Test-OllamaConnection -Url $ollamaUrl
        }

        if (-not ($dbReady -and $ollamaReady)) {
            Write-Host "." -NoNewline
            Start-Sleep -Seconds 2
        }
    }

    Write-Host ""

    if ($dbReady -and $ollamaReady) {
        Write-Success "Infrastructure is ready"
    }
}

# Build the project
function Invoke-Build {
    Write-Header "Building Project"

    $buildArgs = @(
        "build",
        $ProjectPath,
        "--configuration", $Configuration
    )

    dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit $LASTEXITCODE
    }

    Write-Success "Build completed"
}

# Launch the server
function Start-Server {
    Write-Header "Launching MCP Server"

    # Resolve connection settings
    $connString = if ($ConnectionString) { $ConnectionString }
                  elseif ($env:COMPOUNDDOCS_CONNECTION_STRING) { $env:COMPOUNDDOCS_CONNECTION_STRING }
                  else { $DefaultPostgresConnectionString }

    $ollamaUrl = if ($OllamaUrl) { $OllamaUrl }
                 elseif ($env:COMPOUNDDOCS_OLLAMA_URL) { $env:COMPOUNDDOCS_OLLAMA_URL }
                 else { $DefaultOllamaUrl }

    # Set environment variables
    $env:ASPNETCORE_ENVIRONMENT = $Environment
    $env:COMPOUNDDOCS_CONNECTION_STRING = $connString
    $env:COMPOUNDDOCS_OLLAMA_URL = $ollamaUrl
    $env:COMPOUNDDOCS_EMBEDDING_MODEL = $EmbeddingModel
    $env:COMPOUNDDOCS_CHAT_MODEL = $ChatModel
    $env:COMPOUNDDOCS_LOG_LEVEL = $LogLevel
    $env:COMPOUNDDOCS_TRANSPORT = $TransportMode
    $env:COMPOUNDDOCS_PORT = $Port.ToString()

    Write-Info "Configuration:"
    Write-Info "  Environment: $Environment"
    Write-Info "  Configuration: $Configuration"
    Write-Info "  Transport: $TransportMode"
    if ($TransportMode -eq "sse") {
        Write-Info "  Port: $Port"
    }
    Write-Info "  Log Level: $LogLevel"
    Write-Info "  Embedding Model: $EmbeddingModel"
    Write-Info "  Chat Model: $ChatModel"
    Write-Info "  Database: localhost:5433"
    Write-Info "  Ollama: $ollamaUrl"

    # Build command arguments
    $runArgs = @()

    if ($Watch -and $Configuration -eq "Debug") {
        $runArgs += "watch"
    }

    $runArgs += @("run", "--project", $ProjectPath, "--configuration", $Configuration)

    if (-not $Build) {
        $runArgs += "--no-build"
    }

    # Add application arguments after --
    $runArgs += "--"
    $runArgs += @("--transport", $TransportMode)

    if ($TransportMode -eq "sse") {
        $runArgs += @("--port", $Port.ToString())
    }

    if ($DryRun) {
        Write-Header "Dry Run - Command Preview"
        Write-Info "dotnet $($runArgs -join ' ')"
        Write-Info ""
        Write-Info "Environment variables that would be set:"
        Write-Info "  ASPNETCORE_ENVIRONMENT=$Environment"
        Write-Info "  COMPOUNDDOCS_CONNECTION_STRING=$connString"
        Write-Info "  COMPOUNDDOCS_OLLAMA_URL=$ollamaUrl"
        Write-Info "  COMPOUNDDOCS_EMBEDDING_MODEL=$EmbeddingModel"
        Write-Info "  COMPOUNDDOCS_CHAT_MODEL=$ChatModel"
        Write-Info "  COMPOUNDDOCS_LOG_LEVEL=$LogLevel"
        Write-Info "  COMPOUNDDOCS_TRANSPORT=$TransportMode"
        Write-Info "  COMPOUNDDOCS_PORT=$Port"
        return
    }

    Write-Success "Starting MCP server..."
    Write-Host ""

    # Launch the server
    dotnet @runArgs
}

# Main execution
function Main {
    # Find and change to project root
    $projectRoot = Get-ProjectRoot
    if (-not $projectRoot) {
        Write-Error "Could not find project root"
        exit 1
    }
    Push-Location $projectRoot

    try {
        Test-Prerequisites

        if ($Build) {
            Invoke-Build
        }

        if ($WaitForInfrastructure -and -not $DryRun) {
            Wait-ForInfrastructure
        }

        Start-Server
    }
    finally {
        Pop-Location
    }
}

# Run main function
Main
