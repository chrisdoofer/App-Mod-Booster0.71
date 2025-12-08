#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Unified deployment script for complete infrastructure and application deployment.

.DESCRIPTION
    This script orchestrates the complete deployment by calling the infrastructure and
    application deployment scripts in sequence. It provides a single command to deploy
    everything from scratch.

.PARAMETER ResourceGroup
    The name of the Azure resource group (required)

.PARAMETER Location
    The Azure region for deployment (required)

.PARAMETER BaseName
    Base name for resources (optional, defaults to 'expensemgmt')

.PARAMETER DeployGenAI
    Switch to deploy GenAI resources (Azure OpenAI and AI Search)

.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth"

.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth" -DeployGenAI
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$Location,

    [Parameter(Mandatory = $false)]
    [string]$BaseName = "expensemgmt",

    [Parameter(Mandatory = $false)]
    [switch]$DeployGenAI
)

$ErrorActionPreference = "Stop"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Expense Management - Complete Deployment" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "This script will deploy:" -ForegroundColor Yellow
Write-Host "  1. Infrastructure (Azure resources)" -ForegroundColor White
Write-Host "  2. Application code" -ForegroundColor White
Write-Host ""

# Validate that deployment scripts exist
$scriptDir = $PSScriptRoot
$infraScript = Join-Path $scriptDir "deploy-infra/deploy.ps1"
$appScript = Join-Path $scriptDir "deploy-app/deploy.ps1"

if (-not (Test-Path $infraScript)) {
    Write-Error "Infrastructure deployment script not found: $infraScript"
    exit 1
}

if (-not (Test-Path $appScript)) {
    Write-Error "Application deployment script not found: $appScript"
    exit 1
}

Write-Host "✓ Deployment scripts validated" -ForegroundColor Green
Write-Host ""

# Phase 1: Deploy Infrastructure
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Phase 1: Deploying Infrastructure" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

$infraArgs = @{
    ResourceGroup = $ResourceGroup
    Location = $Location
    BaseName = $BaseName
}

if ($DeployGenAI) {
    $infraArgs["DeployGenAI"] = $true
}

& $infraScript @infraArgs

if ($LASTEXITCODE -ne 0) {
    Write-Error "Infrastructure deployment failed. Please fix the issues and try again."
    Write-Host ""
    Write-Host "To retry just the infrastructure deployment, run:" -ForegroundColor Yellow
    Write-Host "  .\deploy-infra\deploy.ps1 -ResourceGroup `"$ResourceGroup`" -Location `"$Location`"" -ForegroundColor Cyan
    exit 1
}

Write-Host ""
Write-Host "✓ Infrastructure deployment completed successfully" -ForegroundColor Green
Write-Host ""

# Wait for Azure resources to stabilize
Write-Host "Waiting for Azure resources to stabilize..." -ForegroundColor Yellow
Start-Sleep -Seconds 15
Write-Host "✓ Ready to proceed" -ForegroundColor Green
Write-Host ""

# Phase 2: Deploy Application
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Phase 2: Deploying Application" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

& $appScript

if ($LASTEXITCODE -ne 0) {
    Write-Error "Application deployment failed. The infrastructure is deployed, but the application code failed."
    Write-Host ""
    Write-Host "To retry just the application deployment, run:" -ForegroundColor Yellow
    Write-Host "  .\deploy-app\deploy.ps1" -ForegroundColor Cyan
    exit 1
}

Write-Host ""
Write-Host "✓ Application deployment completed successfully" -ForegroundColor Green
Write-Host ""

# Final Summary
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Deployment Complete!" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Your Expense Management application is now fully deployed." -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Visit the application at the URL shown above" -ForegroundColor White
Write-Host "  2. Explore the Swagger API documentation" -ForegroundColor White
if ($DeployGenAI) {
    Write-Host "  3. Try the AI Chat feature" -ForegroundColor White
}
Write-Host ""

exit 0
