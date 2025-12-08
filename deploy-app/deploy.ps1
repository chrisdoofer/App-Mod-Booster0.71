<#
.SYNOPSIS
    Deploys the Expense Management application to Azure App Service.

.DESCRIPTION
    This script automates the deployment of the ASP.NET application to Azure App Service.
    It reads configuration from the deployment context file created by the infrastructure deployment.

.PARAMETER ResourceGroup
    Optional. Override the resource group from the deployment context.

.PARAMETER WebAppName
    Optional. Override the web app name from the deployment context.

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

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Expense Management App Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Load deployment context if available
$contextFile = Join-Path $PSScriptRoot ".." ".deployment-context.json"
if (Test-Path $contextFile) {
    Write-Host "Loading deployment context from $contextFile" -ForegroundColor Yellow
    $context = Get-Content $contextFile | ConvertFrom-Json
    
    if (-not $ResourceGroup) { $ResourceGroup = $context.resourceGroup }
    if (-not $WebAppName) { $WebAppName = $context.webAppName }
    
    Write-Host "Resource Group: $ResourceGroup" -ForegroundColor Green
    Write-Host "Web App: $WebAppName" -ForegroundColor Green
} else {
    if (-not $ResourceGroup -or -not $WebAppName) {
        Write-Host "Error: No deployment context found and ResourceGroup/WebAppName not provided." -ForegroundColor Red
        Write-Host "Run deploy-infra/deploy.ps1 first, or provide -ResourceGroup and -WebAppName parameters." -ForegroundColor Red
        exit 1
    }
}

# Check Azure CLI
Write-Host ""
Write-Host "Checking Azure CLI..." -ForegroundColor Yellow
$account = az account show --output json 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Error: Not logged in to Azure. Run 'az login' first." -ForegroundColor Red
    exit 1
}
Write-Host "Logged in as: $($account.user.name)" -ForegroundColor Green

# Build the application
$appPath = Join-Path $PSScriptRoot ".." "src" "ExpenseManagement"
$publishPath = Join-Path $PSScriptRoot ".." "publish"

if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "Building application..." -ForegroundColor Yellow
    
    Push-Location $appPath
    try {
        dotnet publish -c Release -o $publishPath --nologo
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Error: Build failed." -ForegroundColor Red
            exit 1
        }
        Write-Host "Build completed successfully." -ForegroundColor Green
    } finally {
        Pop-Location
    }
} else {
    Write-Host "Skipping build step." -ForegroundColor Yellow
}

# Create deployment package
Write-Host ""
Write-Host "Creating deployment package..." -ForegroundColor Yellow

$zipPath = Join-Path $PSScriptRoot ".." "deploy-package.zip"

# Remove existing zip if present
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

# Create zip with files at root level (not in subdirectory)
Push-Location $publishPath
try {
    Compress-Archive -Path "*" -DestinationPath $zipPath -Force
    Write-Host "Deployment package created: $zipPath" -ForegroundColor Green
} finally {
    Pop-Location
}

# Deploy to Azure
Write-Host ""
Write-Host "Deploying to Azure App Service..." -ForegroundColor Yellow
Write-Host "This may take a few minutes..." -ForegroundColor Gray

az webapp deploy `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --src-path $zipPath `
    --type zip `
    --clean true `
    --restart true `
    --output none

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Deployment failed." -ForegroundColor Red
    exit 1
}

Write-Host "Deployment completed successfully." -ForegroundColor Green

# Clean up
Write-Host ""
Write-Host "Cleaning up..." -ForegroundColor Yellow
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "Cleanup complete." -ForegroundColor Green

# Configure settings if requested
if ($ConfigureSettings -and (Test-Path $contextFile)) {
    Write-Host ""
    Write-Host "Configuring app settings..." -ForegroundColor Yellow
    
    $connectionString = "Server=tcp:$($context.sqlServerFqdn),1433;Initial Catalog=$($context.databaseName);Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$($context.managedIdentityClientId);"
    
    az webapp config connection-string set `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --connection-string-type SQLAzure `
        --settings "DefaultConnection=$connectionString" `
        --output none
    
    Write-Host "Settings configured." -ForegroundColor Green
}

# Get app URL
$webAppDetails = az webapp show --resource-group $ResourceGroup --name $WebAppName --output json | ConvertFrom-Json
$appUrl = "https://$($webAppDetails.defaultHostName)"

# Final summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Application Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Application URLs:" -ForegroundColor Yellow
Write-Host "  Main App:  $appUrl/Index" -ForegroundColor White
Write-Host "  API Docs:  $appUrl/swagger" -ForegroundColor White
Write-Host "  Chat:      $appUrl/Chat" -ForegroundColor White
Write-Host ""
