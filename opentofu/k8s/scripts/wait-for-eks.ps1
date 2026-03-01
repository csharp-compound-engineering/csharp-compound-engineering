#!/usr/bin/env pwsh
#Requires -Version 7.0

################################################################################
# wait-for-eks.ps1 — Polls EKS until cluster is ACTIVE and API server is healthy
################################################################################

param(
    [Parameter(Mandatory)]
    [string]$ClusterName,

    [Parameter(Mandatory)]
    [string]$Region,

    [int]$Timeout = 600
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PollInterval = 15

function Show-Usage {
    Write-Host @"
Usage: wait-for-eks.ps1 -ClusterName NAME -Region REGION [-Timeout SECONDS]

Parameters:
  -ClusterName   Name of the EKS cluster (required)
  -Region        AWS region (required)
  -Timeout       Maximum wait time in seconds (default: 600)
"@
}

Write-Host "Waiting for EKS cluster '$ClusterName' in '$Region' to become ACTIVE..."
Write-Host "Timeout: ${Timeout}s, polling every ${PollInterval}s"

$elapsed = 0

# Phase 1: Wait for cluster status to be ACTIVE
while ($true) {
    if ($elapsed -ge $Timeout) {
        Write-Host "Error: Timed out after ${Timeout}s waiting for cluster to become ACTIVE."
        exit 1
    }

    try {
        $status = & aws eks describe-cluster `
            --name $ClusterName `
            --region $Region `
            --query 'cluster.status' `
            --output text 2>$null
    }
    catch {
        $status = 'NOT_FOUND'
    }

    if ([string]::IsNullOrWhiteSpace($status)) {
        $status = 'NOT_FOUND'
    }

    if ($status -eq 'ACTIVE') {
        Write-Host "Cluster status: ACTIVE"
        break
    }

    Write-Host "  Cluster status: $status (${elapsed}s elapsed)"
    Start-Sleep -Seconds $PollInterval
    $elapsed += $PollInterval
}

# Phase 2: Verify API server is reachable
Write-Host "Verifying API server health..."

$endpoint = & aws eks describe-cluster `
    --name $ClusterName `
    --region $Region `
    --query 'cluster.endpoint' `
    --output text

while ($true) {
    if ($elapsed -ge $Timeout) {
        Write-Host "Error: Timed out after ${Timeout}s waiting for API server to be healthy."
        exit 1
    }

    try {
        $response = Invoke-WebRequest -Uri "${endpoint}/healthz" `
            -SkipCertificateCheck `
            -TimeoutSec 5 `
            -ErrorAction SilentlyContinue
        $httpCode = $response.StatusCode
    }
    catch {
        if ($_.Exception.Response) {
            $httpCode = [int]$_.Exception.Response.StatusCode
        }
        else {
            $httpCode = 0
        }
    }

    if ($httpCode -in @(200, 401, 403)) {
        Write-Host "API server is reachable (HTTP $httpCode). Cluster is ready."
        exit 0
    }

    Write-Host "  API server HTTP: $httpCode (${elapsed}s elapsed)"
    Start-Sleep -Seconds $PollInterval
    $elapsed += $PollInterval
}
