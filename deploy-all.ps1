<#
.SYNOPSIS
    Unified deployment script that deploys both infrastructure and application.

.DESCRIPTION
    This script orchestrates the full deployment process by running:
    1. Infrastructure deployment (deploy-infra/deploy.ps1)
    2. Application deployment (deploy-app/deploy.ps1)

.PARAMETER ResourceGroup
    The name of the Azure resource group (required).

.PARAMETER Location
    The Azure region for deployment (required).

.PARAMETER BaseName
    Base name for resources. Defaults to 'expensemgmt'.

.PARAMETER DeployGenAI
    Switch to deploy GenAI resources (Azure OpenAI and AI Search).

.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth"

.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth" -DeployGenAI
#>

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

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Expense Management - Full Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "Location: $Location" -ForegroundColor White
Write-Host "Base Name: $BaseName" -ForegroundColor White
Write-Host "Deploy GenAI: $($DeployGenAI.IsPresent)" -ForegroundColor White
Write-Host ""

# Validate scripts exist
$infraScript = Join-Path $PSScriptRoot "deploy-infra" "deploy.ps1"
$appScript = Join-Path $PSScriptRoot "deploy-app" "deploy.ps1"

if (-not (Test-Path $infraScript)) {
    Write-Host "Error: Infrastructure deployment script not found at $infraScript" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $appScript)) {
    Write-Host "Error: Application deployment script not found at $appScript" -ForegroundColor Red
    exit 1
}

# Phase 1: Infrastructure Deployment
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Phase 1: Infrastructure Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$infraParams = @{
    ResourceGroup = $ResourceGroup
    Location = $Location
    BaseName = $BaseName
}

if ($DeployGenAI) {
    $infraParams.DeployGenAI = $true
}

& $infraScript @infraParams

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Error: Infrastructure deployment failed." -ForegroundColor Red
    Write-Host "To retry, run: .\deploy-infra\deploy.ps1 -ResourceGroup '$ResourceGroup' -Location '$Location'" -ForegroundColor Yellow
    exit 1
}

# Brief pause for Azure resources to stabilize
Write-Host ""
Write-Host "Waiting for Azure resources to stabilize..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

# Phase 2: Application Deployment
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Phase 2: Application Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

& $appScript

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Error: Application deployment failed." -ForegroundColor Red
    Write-Host "To retry, run: .\deploy-app\deploy.ps1" -ForegroundColor Yellow
    exit 1
}

# Load context for final summary
$contextFile = Join-Path $PSScriptRoot ".deployment-context.json"
$context = Get-Content $contextFile | ConvertFrom-Json

$appUrl = "https://$($context.webAppName).azurewebsites.net"

# Final Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Full Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Resources deployed:" -ForegroundColor Yellow
Write-Host "  Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "  Web App: $($context.webAppName)" -ForegroundColor White
Write-Host "  SQL Server: $($context.sqlServerFqdn)" -ForegroundColor White
Write-Host "  Database: $($context.databaseName)" -ForegroundColor White
if ($context.deployedGenAI) {
    Write-Host "  OpenAI: Configured" -ForegroundColor White
    Write-Host "  AI Search: Configured" -ForegroundColor White
}
Write-Host ""
Write-Host "Application URLs:" -ForegroundColor Yellow
Write-Host "  Main App:  $appUrl/Index" -ForegroundColor White
Write-Host "  API Docs:  $appUrl/swagger" -ForegroundColor White
Write-Host "  Chat:      $appUrl/Chat" -ForegroundColor White
Write-Host ""
