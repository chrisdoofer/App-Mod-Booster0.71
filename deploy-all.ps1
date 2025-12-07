#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Unified deployment script for infrastructure and application.

.DESCRIPTION
    This script orchestrates both infrastructure and application deployment in a single command.
    It calls the individual deployment scripts in sequence.

.PARAMETER ResourceGroup
    The name of the Azure resource group (required).

.PARAMETER Location
    The Azure region for deployment (required).

.PARAMETER BaseName
    Base name for resources. Default is 'expensemgmt'.

.PARAMETER DeployGenAI
    Switch to deploy Azure OpenAI and AI Search resources.

.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth"
    
.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth" -DeployGenAI
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

Write-Host "========================================"  -ForegroundColor Magenta
Write-Host "    UNIFIED DEPLOYMENT - ALL COMPONENTS" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "This will deploy:" -ForegroundColor Cyan
Write-Host "  1. Infrastructure (App Service, SQL, Monitoring, etc.)"
if ($DeployGenAI) {
    Write-Host "  2. GenAI Resources (Azure OpenAI, AI Search)"
}
Write-Host "  3. Application Code"
Write-Host ""

# Validate scripts exist
$infraScript = Join-Path $PSScriptRoot "deploy-infra" "deploy.ps1"
$appScript = Join-Path $PSScriptRoot "deploy-app" "deploy.ps1"

if (-not (Test-Path $infraScript)) {
    Write-Error "Infrastructure deployment script not found at: $infraScript"
    exit 1
}

if (-not (Test-Path $appScript)) {
    Write-Error "Application deployment script not found at: $appScript"
    exit 1
}

Write-Host "✓ Deployment scripts validated" -ForegroundColor Green
Write-Host ""

# Step 1: Deploy Infrastructure
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "STEP 1: Infrastructure Deployment" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
Write-Host ""

$infraArgs = @(
    "-ResourceGroup", $ResourceGroup,
    "-Location", $Location,
    "-BaseName", $BaseName
)

if ($DeployGenAI) {
    $infraArgs += "-DeployGenAI"
}

try {
    & $infraScript @infraArgs
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Infrastructure deployment failed with exit code $LASTEXITCODE"
        exit 1
    }
}
catch {
    Write-Error "Infrastructure deployment failed: $_"
    Write-Host ""
    Write-Host "To retry infrastructure only, run:" -ForegroundColor Yellow
    Write-Host "  .\deploy-infra\deploy.ps1 -ResourceGroup '$ResourceGroup' -Location '$Location'"
    exit 1
}

Write-Host ""
Write-Host "✓ Infrastructure deployment complete" -ForegroundColor Green
Write-Host ""

# Wait for Azure resources to stabilize
Write-Host "Waiting 15 seconds for Azure resources to stabilize..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

# Step 2: Deploy Application
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "STEP 2: Application Deployment" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
Write-Host ""

try {
    & $appScript
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Application deployment failed with exit code $LASTEXITCODE"
        exit 1
    }
}
catch {
    Write-Error "Application deployment failed: $_"
    Write-Host ""
    Write-Host "Infrastructure is deployed. To retry application deployment only, run:" -ForegroundColor Yellow
    Write-Host "  .\deploy-app\deploy.ps1"
    exit 1
}

Write-Host ""
Write-Host "✓ Application deployment complete" -ForegroundColor Green
Write-Host ""

# Final Summary
$contextFilePath = Join-Path $PSScriptRoot ".deployment-context.json"
if (Test-Path $contextFilePath) {
    $context = Get-Content $contextFilePath | ConvertFrom-Json
    
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "    DEPLOYMENT COMPLETE!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "🎉 Your application is ready!" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Access your application:" -ForegroundColor Yellow
    Write-Host "  Main App: https://$($context.webAppName).azurewebsites.net/Index"
    Write-Host "  API Docs: https://$($context.webAppName).azurewebsites.net/swagger"
    
    if ($context.genAIDeployed) {
        Write-Host "  AI Chat:  https://$($context.webAppName).azurewebsites.net/Chat"
    }
    else {
        Write-Host ""
        Write-Host "To add AI Chat functionality, redeploy with:" -ForegroundColor Cyan
        Write-Host "  .\deploy-all.ps1 -ResourceGroup '$ResourceGroup' -Location '$Location' -DeployGenAI"
    }
    
    Write-Host ""
    Write-Host "Deployed Resources:" -ForegroundColor Yellow
    Write-Host "  Resource Group: $($context.resourceGroup)"
    Write-Host "  Web App: $($context.webAppName)"
    Write-Host "  SQL Server: $($context.sqlServerName)"
    Write-Host "  Database: $($context.databaseName)"
    
    Write-Host ""
    Write-Host "Note: It may take 30-60 seconds for the app to fully start." -ForegroundColor Cyan
}

Write-Host ""
exit 0
