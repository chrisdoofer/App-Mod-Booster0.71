#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploys the Expense Management application code to Azure App Service.

.DESCRIPTION
    This script builds and deploys the .NET application to Azure App Service.
    It reads deployment context from the infrastructure deployment automatically.

.PARAMETER ResourceGroup
    The Azure resource group (optional - reads from deployment context if not specified).

.PARAMETER WebAppName
    The web app name (optional - reads from deployment context if not specified).

.PARAMETER SkipBuild
    Skip the dotnet build/publish step (for redeployments).

.EXAMPLE
    .\deploy.ps1
    
.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251206" -WebAppName "app-expensemgmt-abc123"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroup,
    
    [Parameter(Mandatory = $false)]
    [string]$WebAppName,
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Application Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Try to load deployment context
$contextFilePath = Join-Path $PSScriptRoot ".." ".deployment-context.json"

if (Test-Path $contextFilePath) {
    Write-Host "Loading deployment context..." -ForegroundColor Yellow
    $context = Get-Content $contextFilePath | ConvertFrom-Json
    
    if (-not $ResourceGroup) {
        $ResourceGroup = $context.resourceGroup
    }
    if (-not $WebAppName) {
        $WebAppName = $context.webAppName
    }
    
    Write-Host "✓ Context loaded" -ForegroundColor Green
    Write-Host "  Resource Group: $ResourceGroup"
    Write-Host "  Web App: $WebAppName"
}

# Validate parameters
if (-not $ResourceGroup -or -not $WebAppName) {
    Write-Error "Resource group and web app name are required. Either provide them as parameters or ensure .deployment-context.json exists."
    exit 1
}

# Check Azure CLI
Write-Host "`nChecking Azure CLI..." -ForegroundColor Yellow
try {
    az version --output none
    Write-Host "✓ Azure CLI available" -ForegroundColor Green
}
catch {
    Write-Error "Azure CLI is not installed. Please install it from https://aka.ms/installazurecliwindows"
    exit 1
}

# Check login
Write-Host "Checking Azure login..." -ForegroundColor Yellow
try {
    $account = az account show --output json | ConvertFrom-Json
    Write-Host "✓ Logged in as $($account.user.name)" -ForegroundColor Green
}
catch {
    Write-Error "Not logged in to Azure. Please run 'az login' first."
    exit 1
}

# Build and publish the application
if (-not $SkipBuild) {
    Write-Host "`nBuilding application..." -ForegroundColor Yellow
    
    $projectPath = Join-Path $PSScriptRoot ".." "src" "ExpenseManagement" "ExpenseManagement.csproj"
    $publishPath = Join-Path $PSScriptRoot ".." "src" "ExpenseManagement" "publish"
    
    # Clean previous publish
    if (Test-Path $publishPath) {
        Remove-Item -Path $publishPath -Recurse -Force
    }
    
    # Build and publish
    dotnet publish $projectPath -c Release -o $publishPath --no-restore
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }
    
    Write-Host "✓ Application built" -ForegroundColor Green
}
else {
    Write-Host "`nSkipping build (using existing publish folder)..." -ForegroundColor Yellow
    $publishPath = Join-Path $PSScriptRoot ".." "src" "ExpenseManagement" "publish"
}

# Create deployment package
Write-Host "`nCreating deployment package..." -ForegroundColor Yellow

$zipPath = Join-Path $PSScriptRoot ".." "app-deployment.zip"

# Remove old zip if exists
if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

# Create zip with files at root level (not in subdirectory)
$currentLocation = Get-Location
try {
    Set-Location $publishPath
    Compress-Archive -Path "*" -DestinationPath $zipPath -Force
}
finally {
    Set-Location $currentLocation
}

Write-Host "✓ Deployment package created" -ForegroundColor Green

# Deploy to Azure App Service
Write-Host "`nDeploying to Azure App Service..." -ForegroundColor Yellow
Write-Host "This may take 2-3 minutes..." -ForegroundColor Cyan

az webapp deploy `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --src-path $zipPath `
    --type zip `
    --clean true `
    --restart true `
    --output none

if ($LASTEXITCODE -ne 0) {
    Write-Error "Deployment failed"
    exit 1
}

Write-Host "✓ Application deployed" -ForegroundColor Green

# Clean up
Write-Host "`nCleaning up..." -ForegroundColor Yellow
Remove-Item -Path $zipPath -Force -ErrorAction SilentlyContinue
Write-Host "✓ Cleanup complete" -ForegroundColor Green

# Summary
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Application Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "`nAccess your application:" -ForegroundColor Cyan
Write-Host "  Main App: https://$WebAppName.azurewebsites.net/Index"
Write-Host "  API Docs: https://$WebAppName.azurewebsites.net/swagger"
Write-Host "  AI Chat:  https://$WebAppName.azurewebsites.net/Chat"
Write-Host ""
Write-Host "Note: It may take 30-60 seconds for the app to fully start." -ForegroundColor Yellow
Write-Host ""

exit 0
