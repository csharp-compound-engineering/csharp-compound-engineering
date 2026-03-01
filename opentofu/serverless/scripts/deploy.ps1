#!/usr/bin/env pwsh
#Requires -Version 7.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

################################################################################
# deploy.ps1 — Phase orchestration for serverless OpenTofu deployment
################################################################################

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$PhasesDir = Resolve-Path (Join-Path $ScriptDir '../phases')
$TfVarsFile = Join-Path (Resolve-Path (Join-Path $ScriptDir '..')) 'terraform.tfvars'

# Phase directory mapping
$PhaseDirs = @{
    prereqs = '00-prereqs'
    network = '01-network'
    data    = '02-data'
    compute = '03-compute'
}

$PhasesForward = @('network', 'data', 'compute')
$PhasesReverse = @('compute', 'data', 'network')

function Show-Usage {
    $scriptName = Split-Path -Leaf $MyInvocation.ScriptName
    Write-Host @"
Usage: $scriptName <command> [phase]

Commands:
  init [phase]      Initialize phase (or all phases)
  plan [phase]      Plan phase (or all phases)
  apply [phase]     Apply phase (or all phases sequentially)
  destroy [phase]   Destroy phase (or all phases in reverse order)
  output [phase]    Show outputs for a phase

Phases: prereqs, network, data, compute, all (default)

  prereqs — IAM roles/policies and Secrets Manager resources. Runs independently
            and is NOT included in 'all'. Must be applied/destroyed explicitly.
  all     — Runs network -> data -> compute (forward) or reverse for destroy.

Examples:
  $scriptName apply prereqs          # Apply prereqs (one-time, before first deploy)
  $scriptName init                   # Initialize all phases (network/data/compute)
  $scriptName plan network           # Plan network phase only
  $scriptName apply all              # Apply all phases sequentially
  $scriptName destroy compute        # Destroy compute phase only
  $scriptName destroy prereqs        # Destroy prereqs (only when decommissioning)
"@
}

function Write-LogInfo    { param([string]$Msg) Write-Host "[INFO] $Msg" -ForegroundColor Blue }
function Write-LogSuccess { param([string]$Msg) Write-Host "[OK] $Msg" -ForegroundColor Green }
function Write-LogWarn    { param([string]$Msg) Write-Host "[WARN] $Msg" -ForegroundColor Yellow }
function Write-LogError   { param([string]$Msg) Write-Host "[ERROR] $Msg" -ForegroundColor Red }

function Get-PhaseDir {
    param([string]$Phase)
    if (-not $PhaseDirs.ContainsKey($Phase)) {
        Write-LogError "Unknown phase: $Phase"
        exit 1
    }
    return Join-Path $PhasesDir $PhaseDirs[$Phase]
}

function Get-VarFileArgs {
    if (Test-Path $TfVarsFile) {
        return @("-var-file=$TfVarsFile")
    }
    return @()
}

function Invoke-Tofu {
    param(
        [string]$Phase,
        [string[]]$Arguments
    )
    $dir = Get-PhaseDir $Phase
    Write-LogInfo "[$Phase] Running: tofu $($Arguments -join ' ')"
    Push-Location $dir
    try {
        & tofu @Arguments
        if ($LASTEXITCODE -ne 0) { throw "tofu exited with code $LASTEXITCODE" }
    }
    finally {
        Pop-Location
    }
}

function Invoke-Init {
    param([string]$Phase)
    Write-LogInfo "[$Phase] Initializing..."
    Invoke-Tofu -Phase $Phase -Arguments @('init')
    Write-LogSuccess "[$Phase] Initialized."
}

function Invoke-Plan {
    param([string]$Phase)
    $varArgs = Get-VarFileArgs
    Write-LogInfo "[$Phase] Planning..."
    Invoke-Tofu -Phase $Phase -Arguments (@('plan') + $varArgs)
    Write-LogSuccess "[$Phase] Plan complete."
}

function Invoke-Apply {
    param([string]$Phase)
    $varArgs = Get-VarFileArgs
    Write-LogInfo "[$Phase] Applying..."
    Invoke-Tofu -Phase $Phase -Arguments (@('apply', '-auto-approve') + $varArgs)
    Write-LogSuccess "[$Phase] Applied."
}

function Invoke-Destroy {
    param([string]$Phase)
    $varArgs = Get-VarFileArgs
    Write-LogInfo "[$Phase] Destroying..."
    Invoke-Tofu -Phase $Phase -Arguments (@('destroy', '-auto-approve') + $varArgs)
    Write-LogSuccess "[$Phase] Destroyed."
}

function Invoke-Output {
    param([string]$Phase)
    Write-LogInfo "[$Phase] Outputs:"
    Invoke-Tofu -Phase $Phase -Arguments @('output')
}

function Resolve-Phases {
    param(
        [string]$Phase,
        [string]$Direction = 'forward'
    )
    if ($Phase -eq 'all') {
        if ($Direction -eq 'reverse') { return $PhasesReverse }
        return $PhasesForward
    }
    return @($Phase)
}

# --- Main ---

if ($args.Count -lt 1) {
    Show-Usage
    exit 1
}

$Command = $args[0]
$Phase = if ($args.Count -ge 2) { $args[1] } else { 'all' }

switch ($Command) {
    'init' {
        foreach ($p in (Resolve-Phases -Phase $Phase -Direction forward)) {
            Invoke-Init -Phase $p
        }
    }
    'plan' {
        foreach ($p in (Resolve-Phases -Phase $Phase -Direction forward)) {
            Invoke-Plan -Phase $p
        }
    }
    'apply' {
        foreach ($p in (Resolve-Phases -Phase $Phase -Direction forward)) {
            Invoke-Init -Phase $p
            Invoke-Apply -Phase $p
        }
    }
    'destroy' {
        foreach ($p in (Resolve-Phases -Phase $Phase -Direction reverse)) {
            Invoke-Destroy -Phase $p
        }
    }
    'output' {
        foreach ($p in (Resolve-Phases -Phase $Phase -Direction forward)) {
            Invoke-Output -Phase $p
        }
    }
    { $_ -in @('-h', '--help', 'help') } {
        Show-Usage
        exit 0
    }
    default {
        Write-LogError "Unknown command: $Command"
        Show-Usage
        exit 1
    }
}
