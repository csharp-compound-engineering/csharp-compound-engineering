#!/usr/bin/env pwsh
#Requires -Version 7.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

################################################################################
# deploy.ps1 — Phase orchestration for OpenTofu deployment
################################################################################

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$PhasesDir = Resolve-Path (Join-Path $ScriptDir '../phases')
$TfVarsFile = Join-Path (Resolve-Path (Join-Path $ScriptDir '..')) 'terraform.tfvars'

# Phase directory mapping
$PhaseDirs = @{
    prereqs  = '00-prereqs'
    network  = '01-network'
    cluster  = '02-cluster'
    platform = '03-platform'
}

$PhasesForward = @('network', 'cluster', 'platform')
$PhasesReverse = @('platform', 'cluster', 'network')

function Show-Usage {
    $scriptName = Split-Path -Leaf $MyInvocation.ScriptName
    Write-Host @"
Usage: $scriptName <command> [phase]

Commands:
  init [phase]      Initialize phase (or all phases)
  plan [phase]      Plan phase (or all phases)
  apply [phase]     Apply phase (or all phases sequentially with health checks)
  destroy [phase]   Destroy phase (or all phases in reverse order)
  output [phase]    Show outputs for a phase
  scale             Sync application node group scaling config from tfvars

Phases: prereqs, network, cluster, platform, all (default)

  prereqs — IAM roles/policies and Secrets Manager resources. Runs independently
            and is NOT included in 'all'. Must be applied/destroyed explicitly.
  all     — Runs network -> cluster -> platform (forward) or reverse for destroy.

Examples:
  $scriptName apply prereqs          # Apply prereqs (one-time, before first deploy)
  $scriptName init                   # Initialize all phases (network/cluster/platform)
  $scriptName plan network           # Plan network phase only
  $scriptName apply all              # Apply all phases sequentially
  $scriptName destroy platform       # Destroy platform phase only
  $scriptName destroy prereqs        # Destroy prereqs (only when decommissioning)
  $scriptName scale                  # Sync node group scaling config from tfvars
  $scriptName scale dry-run          # Show what would change without applying

Scaling Workflow:
  When changing node_min_size, node_max_size, or node_desired_size in tfvars,
  run 'scale' BEFORE 'apply cluster' to avoid max < desired conflicts.
  The EKS module ignores desired_size changes (managed by autoscaler at runtime),
  so this command syncs the live scaling config via the AWS CLI first.
  Use 'scale dry-run' to preview changes without applying them.
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

function Get-Region {
    $region = 'us-east-2'
    if (Test-Path $TfVarsFile) {
        $match = Select-String -Path $TfVarsFile -Pattern '^\s*region\s*=\s*"([^"]+)"' | Select-Object -First 1
        if ($match) {
            $region = $match.Matches[0].Groups[1].Value
        }
    }
    return $region
}

function Wait-ForEks {
    $clusterDir = Get-PhaseDir 'cluster'
    Push-Location $clusterDir
    try {
        $clusterName = & tofu output -raw cluster_name 2>$null
    }
    catch {
        $clusterName = $null
    }
    finally {
        Pop-Location
    }

    if ([string]::IsNullOrWhiteSpace($clusterName)) {
        Write-LogWarn "Could not determine cluster name from outputs. Skipping health check."
        return
    }

    $region = Get-Region

    Write-LogInfo "Running EKS health check for cluster '$clusterName' in '$region'..."
    $waitScript = Join-Path $ScriptDir 'wait-for-eks.ps1'
    & $waitScript -ClusterName $clusterName -Region $region
    if ($LASTEXITCODE -ne 0) { throw "EKS health check failed" }
}

function Sync-NodeGroupScaling {
    param([switch]$DryRun)

    # Get cluster name from 02-cluster outputs
    $clusterDir = Get-PhaseDir 'cluster'
    Push-Location $clusterDir
    try {
        $clusterName = & tofu output -raw cluster_name 2>$null
    }
    catch {
        $clusterName = $null
    }
    finally {
        Pop-Location
    }

    if ([string]::IsNullOrWhiteSpace($clusterName)) {
        throw "Could not determine cluster name from 02-cluster outputs. Has the cluster phase been applied?"
    }

    $region = Get-Region

    # Parse node sizing from terraform.tfvars (fall back to defaults from 02-cluster/variables.tf)
    $desiredMin = 2
    $desiredMax = 6
    $desiredDesired = 2

    if (Test-Path $TfVarsFile) {
        $minMatch = Select-String -Path $TfVarsFile -Pattern '^\s*node_min_size\s*=\s*(\d+)' | Select-Object -First 1
        if ($minMatch) { $desiredMin = [int]$minMatch.Matches[0].Groups[1].Value }

        $maxMatch = Select-String -Path $TfVarsFile -Pattern '^\s*node_max_size\s*=\s*(\d+)' | Select-Object -First 1
        if ($maxMatch) { $desiredMax = [int]$maxMatch.Matches[0].Groups[1].Value }

        $desiredMatch = Select-String -Path $TfVarsFile -Pattern '^\s*node_desired_size\s*=\s*(\d+)' | Select-Object -First 1
        if ($desiredMatch) { $desiredDesired = [int]$desiredMatch.Matches[0].Groups[1].Value }
    }

    Write-LogInfo "Target scaling config: minSize=$desiredMin, maxSize=$desiredMax, desiredSize=$desiredDesired"

    # Find the application node group (label role=application)
    Write-LogInfo "Listing node groups for cluster '$clusterName' in '$region'..."
    $nodeGroupsJson = & aws eks list-nodegroups --cluster-name $clusterName --region $region --output json 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Failed to list node groups: $nodeGroupsJson" }

    $nodeGroups = ($nodeGroupsJson | ConvertFrom-Json).nodegroups
    if (-not $nodeGroups -or $nodeGroups.Count -eq 0) {
        throw "No node groups found for cluster '$clusterName'."
    }

    $targetNodeGroup = $null
    foreach ($ng in $nodeGroups) {
        $descJson = & aws eks describe-nodegroup --cluster-name $clusterName --nodegroup-name $ng --region $region --output json 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Failed to describe node group '$ng': $descJson" }

        $desc = $descJson | ConvertFrom-Json
        $role = $desc.nodegroup.labels.role
        if ($role -eq 'application') {
            $targetNodeGroup = $ng
            $currentScaling = $desc.nodegroup.scalingConfig
            break
        }
    }

    if (-not $targetNodeGroup) {
        throw "No node group with label role=application found in cluster '$clusterName'."
    }

    Write-LogInfo "Found application node group: $targetNodeGroup"

    $currentMin = $currentScaling.minSize
    $currentMax = $currentScaling.maxSize
    $currentDesired = $currentScaling.desiredSize

    Write-LogInfo "Current scaling config: minSize=$currentMin, maxSize=$currentMax, desiredSize=$currentDesired"

    # Compare current vs desired
    if ($currentMin -eq $desiredMin -and $currentMax -eq $desiredMax -and $currentDesired -eq $desiredDesired) {
        Write-LogSuccess "Scaling config already matches. No changes needed."
        return
    }

    Write-LogInfo "Scaling diff:"
    if ($currentMin -ne $desiredMin)       { Write-LogInfo "  minSize:     $currentMin -> $desiredMin" }
    if ($currentMax -ne $desiredMax)       { Write-LogInfo "  maxSize:     $currentMax -> $desiredMax" }
    if ($currentDesired -ne $desiredDesired) { Write-LogInfo "  desiredSize: $currentDesired -> $desiredDesired" }

    if ($DryRun) {
        Write-LogWarn "Dry run — no changes applied."
        return
    }

    # Apply the scaling update
    Write-LogInfo "Updating node group scaling config..."
    $updateResult = & aws eks update-nodegroup-config `
        --cluster-name $clusterName `
        --nodegroup-name $targetNodeGroup `
        --region $region `
        --scaling-config "minSize=$desiredMin,maxSize=$desiredMax,desiredSize=$desiredDesired" `
        --output json 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Failed to update node group scaling config: $updateResult" }

    # Poll until the node group is ACTIVE again
    Write-LogInfo "Waiting for node group to become ACTIVE..."
    $timeout = 300
    $interval = 15
    $elapsed = 0
    while ($elapsed -lt $timeout) {
        Start-Sleep -Seconds $interval
        $elapsed += $interval

        $statusJson = & aws eks describe-nodegroup `
            --cluster-name $clusterName `
            --nodegroup-name $targetNodeGroup `
            --region $region `
            --query 'nodegroup.status' `
            --output text 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-LogWarn "Failed to check node group status: $statusJson"
            continue
        }

        if ($statusJson.Trim() -eq 'ACTIVE') {
            Write-LogSuccess "Node group '$targetNodeGroup' is ACTIVE with updated scaling config."
            return
        }

        Write-LogInfo "Node group status: $($statusJson.Trim()) ($elapsed/${timeout}s)"
    }

    throw "Timed out waiting for node group '$targetNodeGroup' to become ACTIVE after ${timeout}s."
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
            if ($p -eq 'cluster' -and $Phase -eq 'all') {
                Wait-ForEks
            }
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
    'scale' {
        $dryRun = $Phase -eq 'dry-run'
        Sync-NodeGroupScaling -DryRun:$dryRun
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
