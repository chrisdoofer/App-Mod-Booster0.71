<#
.SYNOPSIS
    Deploys the Expense Management application to Azure App Service.

.DESCRIPTION
    This script builds and deploys the .NET application to Azure App Service.
    It reads deployment context from .deployment-context.json created by the infrastructure deployment.

.PARAMETER ResourceGroup
    Optional. Overrides the resource group from context file.

.PARAMETER WebAppName
    Optional. Overrides the web app name from context file.

.PARAMETER SkipBuild
    Switch to skip the build step (useful for redeployments).

.PARAMETER ConfigureSettings
    Switch to configure app settings after deployment.

.EXAMPLE
    .\deploy.ps1

.EXAMPLE
    .\deploy.ps1 -SkipBuild
#>

param(
    [string]$ResourceGroup,
    [string]$WebAppName,
    [switch]$SkipBuild,
    [switch]$ConfigureSettings
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Expense Management Application Deploy" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Determine script and repo locations
$scriptDir = $PSScriptRoot
$repoRoot = Split-Path -Parent $scriptDir

# Look for deployment context file in both locations
$contextFile = Join-Path $repoRoot ".deployment-context.json"
if (-not (Test-Path $contextFile)) {
    $contextFile = Join-Path $scriptDir ".deployment-context.json"
}
if (-not (Test-Path $contextFile)) {
    $contextFile = "./.deployment-context.json"
}
if (-not (Test-Path $contextFile)) {
    $contextFile = "../.deployment-context.json"
}

$context = $null
if (Test-Path $contextFile) {
    Write-Host "Reading deployment context from: $contextFile" -ForegroundColor Yellow
    $context = Get-Content $contextFile | ConvertFrom-Json
}

# Use parameters or context values
if ([string]::IsNullOrEmpty($ResourceGroup)) {
    if ($context) {
        $ResourceGroup = $context.resourceGroup
    }
    else {
        Write-Error "ResourceGroup is required. Either provide it as a parameter or run deploy-infra/deploy.ps1 first."
        exit 1
    }
}

if ([string]::IsNullOrEmpty($WebAppName)) {
    if ($context) {
        $WebAppName = $context.webAppName
    }
    else {
        Write-Error "WebAppName is required. Either provide it as a parameter or run deploy-infra/deploy.ps1 first."
        exit 1
    }
}

$webAppHostname = if ($context) { $context.webAppHostname } else { "" }

Write-Host "Resource Group: $ResourceGroup"
Write-Host "Web App Name: $WebAppName"
Write-Host ""

# Check Azure CLI
Write-Host "Checking Azure CLI..." -ForegroundColor Yellow
try {
    $account = az account show 2>$null | ConvertFrom-Json
    if (-not $account) {
        throw "Not logged in"
    }
    Write-Host "Logged in as: $($account.user.name)" -ForegroundColor Green
}
catch {
    Write-Error "Azure CLI is not installed or you are not logged in. Run 'az login' first."
    exit 1
}

# Build the application
$projectPath = Join-Path $repoRoot "src/ExpenseManagement/ExpenseManagement.csproj"
$publishPath = Join-Path $repoRoot "src/ExpenseManagement/bin/publish"

if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "Building application..." -ForegroundColor Yellow
    
    if (-not (Test-Path $projectPath)) {
        Write-Error "Project file not found at: $projectPath"
        exit 1
    }
    
    # Clean and publish
    dotnet publish $projectPath -c Release -o $publishPath --nologo
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed."
        exit 1
    }
    
    Write-Host "Build completed successfully!" -ForegroundColor Green
}
else {
    Write-Host "Skipping build as requested." -ForegroundColor Yellow
    
    if (-not (Test-Path $publishPath)) {
        Write-Error "Publish folder not found. Run without -SkipBuild first."
        exit 1
    }
}

# Create deployment zip
Write-Host ""
Write-Host "Creating deployment package..." -ForegroundColor Yellow

$zipFile = Join-Path $repoRoot "deploy-app/app.zip"

# Remove existing zip if present
if (Test-Path $zipFile) {
    Remove-Item $zipFile -Force
}

# Create zip with files at root level (not in subdirectory)
Push-Location $publishPath
Compress-Archive -Path * -DestinationPath $zipFile -Force
Pop-Location

Write-Host "Deployment package created: $zipFile" -ForegroundColor Green

# Deploy to Azure
Write-Host ""
Write-Host "Deploying to Azure App Service..." -ForegroundColor Yellow

az webapp deploy `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --src-path $zipFile `
    --type zip `
    --clean true `
    --restart true `
    --output none 2>$null

if ($LASTEXITCODE -ne 0) {
    Write-Error "Deployment failed. Check Azure portal for details."
    exit 1
}

Write-Host "Deployment completed successfully!" -ForegroundColor Green

# Clean up zip file
Remove-Item $zipFile -Force -ErrorAction SilentlyContinue

# Configure settings if requested
if ($ConfigureSettings -and $context) {
    Write-Host ""
    Write-Host "Configuring app settings..." -ForegroundColor Yellow
    
    $connectionString = "Server=tcp:$($context.sqlServerFqdn),1433;Initial Catalog=$($context.databaseName);Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$($context.managedIdentityClientId);"
    
    az webapp config appsettings set `
        --name $WebAppName `
        --resource-group $ResourceGroup `
        --settings "AZURE_CLIENT_ID=$($context.managedIdentityClientId)" "ManagedIdentityClientId=$($context.managedIdentityClientId)" `
        --output none 2>$null
    
    az webapp config connection-string set `
        --name $WebAppName `
        --resource-group $ResourceGroup `
        --connection-string-type SQLAzure `
        --settings "DefaultConnection=$connectionString" `
        --output none 2>$null
    
    Write-Host "App settings configured!" -ForegroundColor Green
}

# Get the hostname if not available
if ([string]::IsNullOrEmpty($webAppHostname)) {
    $webApp = az webapp show --name $WebAppName --resource-group $ResourceGroup 2>$null | ConvertFrom-Json
    $webAppHostname = $webApp.defaultHostName
}

# Print summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Application URLs:" -ForegroundColor White
Write-Host "  Main App: https://$webAppHostname/Index" -ForegroundColor Green
Write-Host "  API Docs: https://$webAppHostname/swagger" -ForegroundColor Green
Write-Host ""
