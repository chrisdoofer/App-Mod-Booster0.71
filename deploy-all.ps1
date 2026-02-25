#Requires -Version 7.0
<#
.SYNOPSIS
    Deploys both infrastructure and application with a single command.

.DESCRIPTION
    Unified orchestrator that calls deploy-infra/deploy.ps1 and then deploy-app/deploy.ps1
    in sequence. Infrastructure deployment creates .deployment-context.json which is
    automatically consumed by the application deployment step.

    This script is a thin orchestrator — all deployment logic lives in the child scripts.

.PARAMETER ResourceGroup
    Name of the Azure resource group (required).

.PARAMETER Location
    Azure region for resource deployment, e.g. 'uksouth' (required).

.PARAMETER BaseName
    Base name used to generate resource names. Defaults to 'expensemgmt'.

.PARAMETER DeployGenAI
    If specified, deploys Azure OpenAI and AI Search resources in addition to core infrastructure.

.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth"

.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth" -DeployGenAI

.EXAMPLE
    .\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "eastus" -BaseName "myexpenses"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string] $Location,

    [string] $BaseName = "expensemgmt",

    [switch] $DeployGenAI
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Script paths ─────────────────────────────────────────────────────────────
$repoRoot   = $PSScriptRoot
$infraScript = Join-Path $repoRoot "deploy-infra/deploy.ps1"
$appScript   = Join-Path $repoRoot "deploy-app/deploy.ps1"

# ── Header ───────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║       Expense Management — Full Deployment Orchestrator      ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Resource Group : $ResourceGroup" -ForegroundColor White
Write-Host "  Location       : $Location"      -ForegroundColor White
Write-Host "  Base Name      : $BaseName"      -ForegroundColor White
Write-Host "  Deploy GenAI   : $($DeployGenAI.IsPresent)" -ForegroundColor White
Write-Host ""
Write-Host "  This script will:"                                                             -ForegroundColor Gray
Write-Host "    1. Deploy Azure infrastructure (Bicep + database setup)"                     -ForegroundColor Gray
Write-Host "    2. Wait 15 seconds for resources to stabilise"                               -ForegroundColor Gray
Write-Host "    3. Deploy the .NET application to App Service"                               -ForegroundColor Gray
Write-Host ""

# ── Validate child scripts exist ─────────────────────────────────────────────
Write-Host "► Validating deployment scripts..." -ForegroundColor Yellow

if (-not (Test-Path $infraScript)) {
    Write-Error "Infrastructure deployment script not found: $infraScript"
    exit 1
}

if (-not (Test-Path $appScript)) {
    Write-Error "Application deployment script not found: $appScript"
    exit 1
}

Write-Host "  ✓ deploy-infra/deploy.ps1 found" -ForegroundColor Green
Write-Host "  ✓ deploy-app/deploy.ps1 found"   -ForegroundColor Green

# ── Phase 1: Infrastructure deployment ───────────────────────────────────────
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkCyan
Write-Host "  PHASE 1 — Infrastructure Deployment"                            -ForegroundColor DarkCyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkCyan
Write-Host ""

# MUST use hashtable splatting — array splatting causes "positional parameter cannot be found" errors
$infraArgs = @{
    ResourceGroup = $ResourceGroup
    Location      = $Location
    BaseName      = $BaseName
}

# Add switch parameters only when set (passing $false for a [switch] doesn't work reliably)
if ($DeployGenAI) {
    $infraArgs["DeployGenAI"] = $true
}

& $infraScript @infraArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "✗ Infrastructure deployment FAILED." -ForegroundColor Red
    Write-Host ""
    Write-Host "  To retry infrastructure only:" -ForegroundColor Yellow
    Write-Host "    .\deploy-infra\deploy.ps1 -ResourceGroup `"$ResourceGroup`" -Location `"$Location`"" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Host ""
Write-Host "  ✓ Phase 1 complete — infrastructure deployed successfully" -ForegroundColor Green

# ── Wait for resources to stabilise ─────────────────────────────────────────
Write-Host ""
Write-Host "► Waiting 15 seconds for Azure resources to fully stabilise..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

# ── Phase 2: Application deployment ──────────────────────────────────────────
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkCyan
Write-Host "  PHASE 2 — Application Deployment"                               -ForegroundColor DarkCyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkCyan
Write-Host ""

# No parameters needed — app script reads .deployment-context.json automatically
& $appScript

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "✗ Application deployment FAILED." -ForegroundColor Red
    Write-Host ""
    Write-Host "  Infrastructure was deployed successfully. To retry application only:" -ForegroundColor Yellow
    Write-Host "    .\deploy-app\deploy.ps1" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

Write-Host ""
Write-Host "  ✓ Phase 2 complete — application deployed successfully" -ForegroundColor Green

# ── Final summary ─────────────────────────────────────────────────────────────
$contextFile = Join-Path $repoRoot ".deployment-context.json"
$appUrl = ""
if (Test-Path $contextFile) {
    $context = Get-Content $contextFile -Raw | ConvertFrom-Json
    $appUrl  = $context.appServiceUrl
}

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                 Full Deployment Complete! 🎉                 ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""

if (-not [string]::IsNullOrWhiteSpace($appUrl)) {
    Write-Host "  Resource Group  : $ResourceGroup"       -ForegroundColor White
    Write-Host "  App Service URL : $appUrl"              -ForegroundColor Cyan
    Write-Host "  Main Interface  : $appUrl/Index"        -ForegroundColor Cyan
    Write-Host "  Swagger UI      : $appUrl/swagger"      -ForegroundColor Cyan
    Write-Host "  Health Check    : $appUrl/health"       -ForegroundColor Cyan
}
else {
    Write-Host "  Resource Group  : $ResourceGroup"       -ForegroundColor White
    Write-Host "  (Check .deployment-context.json for app URL)" -ForegroundColor DarkYellow
}

Write-Host ""
Write-Host "  Note: If the app shows an error on first load, wait 60 seconds" -ForegroundColor Yellow
Write-Host "        and refresh. App Service may still be warming up."         -ForegroundColor Yellow
Write-Host ""
