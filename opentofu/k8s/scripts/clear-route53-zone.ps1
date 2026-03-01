#!/usr/bin/env pwsh
#Requires -Version 7.0

# ==============================================================================
# Deletes all non-required (non-NS, non-SOA) records from a Route 53 hosted zone.
# Used as a pre-destroy hook to allow zone deletion when ExternalDNS or other
# controllers have created records outside of OpenTofu.
#
# Usage: ./clear-route53-zone.ps1 -ZoneId <hosted-zone-id>
# ==============================================================================

param(
    [Parameter(Mandatory)]
    [string]$ZoneId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Write-Host "Listing records in hosted zone $ZoneId..."

$response = & aws route53 list-resource-record-sets --hosted-zone-id $ZoneId --output json 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Warning: could not list records: $response" -ForegroundColor Yellow
    exit 0
}

$data = $response | ConvertFrom-Json
$toDelete = $data.ResourceRecordSets | Where-Object { $_.Type -notin @('NS', 'SOA') }

if (-not $toDelete -or $toDelete.Count -eq 0) {
    Write-Host 'No extra records to clean up.'
    exit 0
}

Write-Host "Deleting $($toDelete.Count) record(s) from zone $ZoneId..."

$changes = $toDelete | ForEach-Object {
    @{ Action = 'DELETE'; ResourceRecordSet = $_ }
}

$changeBatch = @{ Changes = @($changes) } | ConvertTo-Json -Depth 10 -Compress

& aws route53 change-resource-record-sets --hosted-zone-id $ZoneId --change-batch $changeBatch 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Warning: failed to delete records." -ForegroundColor Yellow
    exit 0
}

Write-Host 'Records deleted.'
