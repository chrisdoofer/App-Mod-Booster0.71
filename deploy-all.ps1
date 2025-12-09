<#
.SYNOPSIS
    Unified deployment script that deploys both infrastructure and application.

.DESCRIPTION
    This script orchestrates the full deployment process by calling the individual
    infrastructure and application deployment scripts in sequence.

.PARAMETER ResourceGroup
    The name of the Azure resource group (required).

.PARAMETER Location
    The Azure region for deployment (required).

.PARAMETER BaseName
    Base name for resources. Defaults to 'expensemgmt'.

.PARAMETER DeployGenAI
    Switch to deploy Azure OpenAI and AI Search resources.

.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251209" -Location "uksouth"

.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251209" -Location "uksouth" -DeployGenAI
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$Location,

    [string]$BaseName = "expensemgmt",

    [switch]$DeployGenAI
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Expense Management Full Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "This script will deploy:" -ForegroundColor White
Write-Host "  1. Azure Infrastructure (App Service, SQL, Monitoring)" -ForegroundColor White
if ($DeployGenAI) {
    Write-Host "  2. GenAI Resources (Azure OpenAI, AI Search)" -ForegroundColor White
    Write-Host "  3. Application Code" -ForegroundColor White
}
else {
    Write-Host "  2. Application Code" -ForegroundColor White
}
Write-Host ""

# Validate scripts exist
$scriptDir = $PSScriptRoot
$infraScript = Join-Path $scriptDir "deploy-infra/deploy.ps1"
$appScript = Join-Path $scriptDir "deploy-app/deploy.ps1"

if (-not (Test-Path $infraScript)) {
    Write-Error "Infrastructure deployment script not found at: $infraScript"
    exit 1
}

if (-not (Test-Path $appScript)) {
    Write-Error "Application deployment script not found at: $appScript"
    exit 1
}

# Deploy infrastructure
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "Phase 1: Infrastructure Deployment" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
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
    Write-Host ""
    Write-Error "Infrastructure deployment failed."
    Write-Host ""
    Write-Host "To retry, run:" -ForegroundColor Yellow
    Write-Host "  .\deploy-infra\deploy.ps1 -ResourceGroup `"$ResourceGroup`" -Location `"$Location`"" -ForegroundColor White
    exit 1
}

# Wait for Azure resources to stabilize
Write-Host ""
Write-Host "Waiting for Azure resources to stabilize..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

# Deploy application
Write-Host ""
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "Phase 2: Application Deployment" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
Write-Host ""

& $appScript

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Error "Application deployment failed."
    Write-Host ""
    Write-Host "To retry, run:" -ForegroundColor Yellow
    Write-Host "  .\deploy-app\deploy.ps1" -ForegroundColor White
    exit 1
}

# Read context for final summary
$contextFile = Join-Path $scriptDir ".deployment-context.json"
$context = $null
if (Test-Path $contextFile) {
    $context = Get-Content $contextFile | ConvertFrom-Json
}

# Final summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Full Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

if ($context) {
    Write-Host "Application URLs:" -ForegroundColor White
    Write-Host "  Dashboard: https://$($context.webAppHostname)/Index" -ForegroundColor Cyan
    Write-Host "  API Docs:  https://$($context.webAppHostname)/swagger" -ForegroundColor Cyan
    Write-Host "  Chat:      https://$($context.webAppHostname)/Chat" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Resources Deployed:" -ForegroundColor White
    Write-Host "  Resource Group: $($context.resourceGroup)" -ForegroundColor White
    Write-Host "  Web App: $($context.webAppName)" -ForegroundColor White
    Write-Host "  SQL Server: $($context.sqlServerName)" -ForegroundColor White
    Write-Host "  Database: $($context.databaseName)" -ForegroundColor White
    
    if ($context.deployedGenAI) {
        Write-Host ""
        Write-Host "GenAI Resources:" -ForegroundColor White
        Write-Host "  OpenAI Endpoint: $($context.openAIEndpoint)" -ForegroundColor White
        Write-Host "  Model: $($context.openAIModelName)" -ForegroundColor White
    }
}

Write-Host ""
