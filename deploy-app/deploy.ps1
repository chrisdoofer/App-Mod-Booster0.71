#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploys the Expense Management application code to Azure App Service.

.DESCRIPTION
    This script builds and deploys the .NET application to Azure App Service.
    It can read deployment context from the infrastructure deployment or accept parameters directly.

.PARAMETER ResourceGroup
    The name of the Azure resource group (optional, read from context file if not provided)

.PARAMETER WebAppName
    The name of the web app (optional, read from context file if not provided)

.PARAMETER SkipBuild
    Skip the build step and use existing published files

.PARAMETER ConfigureSettings
    Configure app settings after deployment

.EXAMPLE
    .\deploy.ps1

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -WebAppName "app-expensemgmt-abc123"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $false)]
    [string]$WebAppName,

    [Parameter(Mandatory = $false)]
    [switch]$SkipBuild,

    [Parameter(Mandatory = $false)]
    [switch]$ConfigureSettings
)

$ErrorActionPreference = "Stop"

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Expense Management - Application Deployment" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Find the deployment context file
$scriptDir = $PSScriptRoot
$repoRoot = Split-Path -Parent $scriptDir

$contextPath = $null
$possiblePaths = @(
    Join-Path $scriptDir ".deployment-context.json",
    Join-Path $repoRoot ".deployment-context.json"
)

foreach ($path in $possiblePaths) {
    if (Test-Path $path) {
        $contextPath = $path
        Write-Host "Found deployment context: $contextPath" -ForegroundColor Green
        break
    }
}

# Load context if available
$context = $null
if ($contextPath) {
    try {
        $context = Get-Content $contextPath | ConvertFrom-Json
        Write-Host "Loaded deployment context from infrastructure deployment" -ForegroundColor Green
        
        if ([string]::IsNullOrEmpty($ResourceGroup)) {
            $ResourceGroup = $context.resourceGroup
        }
        if ([string]::IsNullOrEmpty($WebAppName)) {
            $WebAppName = $context.webAppName
        }
    }
    catch {
        Write-Warning "Failed to load deployment context: $($_.Exception.Message)"
    }
}

# Validate required parameters
if ([string]::IsNullOrEmpty($ResourceGroup) -or [string]::IsNullOrEmpty($WebAppName)) {
    Write-Error "ResourceGroup and WebAppName are required. Either provide them as parameters or ensure .deployment-context.json exists."
    exit 1
}

Write-Host "Target:" -ForegroundColor Yellow
Write-Host "  - Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "  - Web App: $WebAppName" -ForegroundColor White
Write-Host ""

# Check Azure CLI
Write-Host "Checking Azure CLI..." -ForegroundColor Yellow
try {
    $azVersion = az version --output json 2>$null | ConvertFrom-Json
    Write-Host "✓ Azure CLI version: $($azVersion.'azure-cli')" -ForegroundColor Green
}
catch {
    Write-Error "Azure CLI is not installed or not in PATH"
    exit 1
}

# Check login
Write-Host "Checking Azure login..." -ForegroundColor Yellow
try {
    $account = az account show --output json 2>$null | ConvertFrom-Json
    Write-Host "✓ Logged in as: $($account.user.name)" -ForegroundColor Green
}
catch {
    Write-Error "Not logged in to Azure. Please run 'az login' first."
    exit 1
}

# Build the application
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "Building application..." -ForegroundColor Yellow
    
    $projectPath = Join-Path $repoRoot "src/ExpenseManagement/ExpenseManagement.csproj"
    
    if (-not (Test-Path $projectPath)) {
        Write-Error "Project file not found: $projectPath"
        exit 1
    }
    
    $publishPath = Join-Path $repoRoot "publish"
    
    # Clean previous publish
    if (Test-Path $publishPath) {
        Remove-Item -Path $publishPath -Recurse -Force
    }
    
    # Build and publish
    dotnet publish $projectPath --configuration Release --output $publishPath --no-self-contained
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed"
        exit 1
    }
    
    Write-Host "✓ Application built successfully" -ForegroundColor Green
}
else {
    Write-Host "Skipping build as requested" -ForegroundColor Yellow
    $publishPath = Join-Path $repoRoot "publish"
}

# Create deployment package
Write-Host ""
Write-Host "Creating deployment package..." -ForegroundColor Yellow

$zipPath = Join-Path $repoRoot "app-deployment.zip"

# Remove existing zip
if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

# Create zip with files at root level (not in a subdirectory)
Push-Location $publishPath
try {
    if ($IsWindows -or $PSVersionTable.PSVersion.Major -le 5) {
        # Windows or PowerShell 5.1
        Compress-Archive -Path * -DestinationPath $zipPath -Force
    }
    else {
        # PowerShell Core on Linux/macOS
        zip -r $zipPath . > $null
    }
}
finally {
    Pop-Location
}

Write-Host "✓ Deployment package created: $zipPath" -ForegroundColor Green

# Deploy to Azure
Write-Host ""
Write-Host "Deploying to Azure App Service..." -ForegroundColor Yellow

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

Write-Host "✓ Application deployed successfully" -ForegroundColor Green

# Clean up
Write-Host ""
Write-Host "Cleaning up..." -ForegroundColor Yellow
Remove-Item -Path $zipPath -Force -ErrorAction SilentlyContinue
Write-Host "✓ Cleanup complete" -ForegroundColor Green

# Configure settings if requested
if ($ConfigureSettings -and $context) {
    Write-Host ""
    Write-Host "Configuring app settings..." -ForegroundColor Yellow
    
    $connectionString = "Server=tcp:$($context.sqlServerFqdn),1433;Initial Catalog=$($context.databaseName);Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$($context.managedIdentityClientId);"
    
    az webapp config connection-string set `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --connection-string-type SQLAzure `
        --settings DefaultConnection=$connectionString `
        --output none
    
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --settings "AZURE_CLIENT_ID=$($context.managedIdentityClientId)" "ManagedIdentityClientId=$($context.managedIdentityClientId)" `
        --output none
    
    Write-Host "✓ App settings configured" -ForegroundColor Green
}

# Display URLs
Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Your application is now running at:" -ForegroundColor Yellow
Write-Host "  Main Page: https://$WebAppName.azurewebsites.net/Index" -ForegroundColor Cyan
Write-Host "  Swagger API: https://$WebAppName.azurewebsites.net/swagger" -ForegroundColor Cyan
Write-Host "  Chat: https://$WebAppName.azurewebsites.net/Chat" -ForegroundColor Cyan
Write-Host ""

exit 0
