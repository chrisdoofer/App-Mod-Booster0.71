#Requires -Version 7.0
<#
.SYNOPSIS
    Deploys the Expense Management application to Azure App Service.

.DESCRIPTION
    Builds and deploys the .NET 8 Razor Pages application to Azure App Service.
    Reads resource details from .deployment-context.json created by deploy-infra/deploy.ps1.

    Supports both local interactive and CI/CD (GitHub Actions) execution.

.PARAMETER ResourceGroup
    Azure resource group name. Overrides value from .deployment-context.json if provided.

.PARAMETER WebAppName
    Azure App Service name. Overrides value from .deployment-context.json if provided.

.PARAMETER SkipBuild
    If specified, skips the dotnet publish step (assumes output already exists).

.PARAMETER ConfigureSettings
    If specified, re-configures App Service settings from context file after deployment.

.EXAMPLE
    .\deploy-app\deploy.ps1
    (No parameters needed when .deployment-context.json exists from infra deployment)

.EXAMPLE
    .\deploy-app\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251206" -WebAppName "app-expensemgmt-abc123"

.EXAMPLE
    .\deploy-app\deploy.ps1 -SkipBuild
#>

[CmdletBinding()]
param(
    [string] $ResourceGroup = "",
    [string] $WebAppName    = "",
    [switch] $SkipBuild,
    [switch] $ConfigureSettings
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Path resolution ──────────────────────────────────────────────────────────
# Use $PSScriptRoot so paths work whether called from repo root or deploy-app/
$scriptDir  = $PSScriptRoot
$repoRoot   = Split-Path -Parent $scriptDir
$projectPath = Join-Path $repoRoot "src/ExpenseManagement/ExpenseManagement.csproj"

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║        Expense Management — Application Deployment          ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# ── 1. Load deployment context ───────────────────────────────────────────────
Write-Host "► Loading deployment context..." -ForegroundColor Yellow

# Look in both current directory and parent directory (script may be called from either)
$contextPaths = @(
    (Join-Path $repoRoot ".deployment-context.json"),
    (Join-Path (Get-Location).Path ".deployment-context.json"),
    (Join-Path (Get-Location).Path "../.deployment-context.json")
)

$contextFile = $null
foreach ($path in $contextPaths) {
    $resolved = [System.IO.Path]::GetFullPath($path)
    if (Test-Path $resolved) {
        $contextFile = $resolved
        break
    }
}

if ($contextFile) {
    $context = Get-Content $contextFile -Raw | ConvertFrom-Json
    Write-Host "  ✓ Context loaded from: $contextFile" -ForegroundColor Green

    # Use context values unless overridden by parameters
    if ([string]::IsNullOrWhiteSpace($ResourceGroup)) { $ResourceGroup = $context.resourceGroup }
    if ([string]::IsNullOrWhiteSpace($WebAppName))    { $WebAppName    = $context.webAppName }
}
else {
    Write-Host "  ℹ No deployment context file found — using provided parameters" -ForegroundColor DarkYellow
}

# Validate required values
if ([string]::IsNullOrWhiteSpace($ResourceGroup)) {
    Write-Error "ResourceGroup is required. Either provide -ResourceGroup parameter or run deploy-infra/deploy.ps1 first."
    exit 1
}
if ([string]::IsNullOrWhiteSpace($WebAppName)) {
    Write-Error "WebAppName is required. Either provide -WebAppName parameter or run deploy-infra/deploy.ps1 first."
    exit 1
}

Write-Host "  Resource Group : $ResourceGroup" -ForegroundColor White
Write-Host "  Web App Name   : $WebAppName"    -ForegroundColor White

# ── 2. Validate Azure CLI ────────────────────────────────────────────────────
Write-Host ""
Write-Host "► Validating Azure CLI..." -ForegroundColor Yellow

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI is not installed. Install from: https://aka.ms/azure-cli"
    exit 1
}

$accountJson = az account show --output json 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($accountJson)) {
    Write-Error "Azure CLI is not logged in. Run: az login"
    exit 1
}

$account = $accountJson | ConvertFrom-Json
Write-Host "  ✓ Logged in to subscription: $($account.name)" -ForegroundColor Green

# ── 3. Build and publish application ─────────────────────────────────────────
$publishDir = Join-Path $repoRoot "deploy-app/.publish"

if ($SkipBuild) {
    Write-Host ""
    Write-Host "  ℹ Build skipped (SkipBuild flag set)" -ForegroundColor DarkYellow
    if (-not (Test-Path $publishDir)) {
        Write-Error "Publish directory not found: $publishDir. Cannot skip build — published output does not exist."
        exit 1
    }
}
else {
    Write-Host ""
    Write-Host "► Building application..." -ForegroundColor Yellow

    if (-not (Test-Path $projectPath)) {
        Write-Error "Project file not found: $projectPath"
        exit 1
    }

    if (Test-Path $publishDir) {
        Remove-Item -Path $publishDir -Recurse -Force
    }

    dotnet publish $projectPath -c Release -o $publishDir --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed."
        exit 1
    }
    Write-Host "  ✓ Application built successfully" -ForegroundColor Green
}

# ── 4. Create deployment zip ─────────────────────────────────────────────────
Write-Host ""
Write-Host "► Creating deployment package..." -ForegroundColor Yellow

$zipFile = Join-Path $repoRoot "deploy-app/.deploy-package.zip"
if (Test-Path $zipFile) {
    Remove-Item -Path $zipFile -Force
}

# DLL files must be at the ROOT of the zip (not in a subdirectory)
# Azure App Service expects this layout
$publishedFiles = Get-ChildItem -Path $publishDir -File -Recurse
if ($publishedFiles.Count -eq 0) {
    Write-Error "No files found in publish directory: $publishDir"
    exit 1
}

Compress-Archive -Path "$publishDir/*" -DestinationPath $zipFile -Force

$zipSizeMb = [math]::Round((Get-Item $zipFile).Length / 1MB, 1)
Write-Host "  ✓ Deployment package created: $zipSizeMb MB" -ForegroundColor Green

# ── 5. Configure settings (optional) ─────────────────────────────────────────
if ($ConfigureSettings -and $contextFile) {
    Write-Host ""
    Write-Host "► Configuring App Service settings..." -ForegroundColor Yellow

    $sqlConnectionString = "Server=tcp:$($context.sqlServerFqdn),1433;Initial Catalog=$($context.databaseName);Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$($context.managedIdentityClientId);"

    $settingsList = @(
        "AZURE_CLIENT_ID=$($context.managedIdentityClientId)",
        "ManagedIdentityClientId=$($context.managedIdentityClientId)",
        "APPLICATIONINSIGHTS_CONNECTION_STRING=$($context.appInsightsConnectionString)",
        "ConnectionStrings__DefaultConnection=$sqlConnectionString"
    )

    if (-not [string]::IsNullOrWhiteSpace($context.openAIEndpoint)) {
        $settingsList += "GenAISettings__OpenAIEndpoint=$($context.openAIEndpoint)"
        $settingsList += "GenAISettings__OpenAIModelName=$($context.openAIModelName)"
    }

    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $WebAppName `
        --settings @settingsList `
        --output none 2>$null

    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Could not configure App Service settings. Application may not function correctly."
    }
    else {
        Write-Host "  ✓ App settings configured" -ForegroundColor Green
    }
}

# ── 6. Deploy to Azure App Service ───────────────────────────────────────────
Write-Host ""
Write-Host "► Deploying to Azure App Service..." -ForegroundColor Yellow
Write-Host "  (This may take 2-3 minutes...)"    -ForegroundColor Gray

# Use 2>&1 to capture progress messages from stderr (az webapp deploy outputs to stderr normally)
$deployResult = az webapp deploy `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --src-path $zipFile `
    --type zip `
    --clean true `
    --restart true `
    --output json 2>&1

# Parse deployment result — check for success indicators in output
if ($deployResult -match '"status":\s*"RuntimeSuccessful"' -or
    $deployResult -match 'Deployment has completed successfully') {
    Write-Host "  ✓ Deployment completed successfully!" -ForegroundColor Green
}
elseif ($LASTEXITCODE -ne 0) {
    # Even if exit code is non-zero, the deployment may have succeeded
    if ($deployResult -match 'completed successfully' -or $deployResult -match 'RuntimeSuccessful') {
        Write-Host "  ✓ Deployment succeeded despite warnings" -ForegroundColor Green
    }
    else {
        Write-Host "  Deploy output: $deployResult" -ForegroundColor DarkGray
        Write-Error "Deployment failed. Check the Azure portal for details: https://portal.azure.com"
        exit 1
    }
}
else {
    Write-Host "  ✓ Deployment completed" -ForegroundColor Green
}

# ── 7. Cleanup ───────────────────────────────────────────────────────────────
if (Test-Path $zipFile) {
    Remove-Item -Path $zipFile -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ Temporary package removed" -ForegroundColor Gray
}

# ── 8. Summary ───────────────────────────────────────────────────────────────
$appUrl = if ($contextFile -and $context.appServiceUrl) { $context.appServiceUrl } else { "https://$WebAppName.azurewebsites.net" }

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║               Application Deployment Complete                ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "  Web App         : $WebAppName"             -ForegroundColor White
Write-Host "  App Service URL : $appUrl"                 -ForegroundColor Cyan
Write-Host "  Main Interface  : $appUrl/Index"           -ForegroundColor Cyan
Write-Host "  Swagger UI      : $appUrl/swagger"         -ForegroundColor Cyan
Write-Host "  Health Check    : $appUrl/health"          -ForegroundColor Cyan
Write-Host ""
Write-Host "  Note: If the app shows an error on first load, wait 60 seconds" -ForegroundColor Yellow
Write-Host "        and refresh. App Service may still be warming up."        -ForegroundColor Yellow
Write-Host ""
