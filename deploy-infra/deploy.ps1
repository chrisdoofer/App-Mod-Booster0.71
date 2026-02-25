#Requires -Version 7.0
<#
.SYNOPSIS
    Deploys Expense Management infrastructure to Azure using Bicep.

.DESCRIPTION
    Fully automates infrastructure deployment including:
    - Azure resource provisioning via Bicep
    - SQL schema and stored procedure import via sqlcmd
    - Managed identity database user creation (SID-based)
    - App Service configuration
    - Deployment context file creation

    Supports both local interactive and CI/CD (GitHub Actions / Azure DevOps) execution.

.PARAMETER ResourceGroup
    Name of the Azure resource group to deploy into (required).

.PARAMETER Location
    Azure region for resource deployment, e.g. 'uksouth' (required).

.PARAMETER BaseName
    Base name used to generate resource names. Defaults to 'expensemgmt'.

.PARAMETER DeployGenAI
    If specified, deploys Azure OpenAI and AI Search resources.

.PARAMETER SkipDatabase
    If specified, skips database schema import and user creation (useful for redeployments).

.EXAMPLE
    .\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth"

.EXAMPLE
    .\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth" -DeployGenAI

.EXAMPLE
    .\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth" -SkipDatabase
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string] $Location,

    [string] $BaseName = "expensemgmt",

    [switch] $DeployGenAI,

    [switch] $SkipDatabase
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── PowerShell version check ─────────────────────────────────────────────────
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Warning "PowerShell 5.1 detected. PowerShell 7+ is strongly recommended."
    Write-Warning "Install from: https://aka.ms/powershell"
}

# ── CI/CD detection ──────────────────────────────────────────────────────────
$IsCI = $env:GITHUB_ACTIONS -eq "true" -or $env:TF_BUILD -eq "true" -or $env:CI -eq "true"

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║        Expense Management — Infrastructure Deployment        ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Resource Group : $ResourceGroup"    -ForegroundColor White
Write-Host "  Location       : $Location"         -ForegroundColor White
Write-Host "  Base Name      : $BaseName"         -ForegroundColor White
Write-Host "  Deploy GenAI   : $($DeployGenAI.IsPresent)" -ForegroundColor White
Write-Host "  Skip Database  : $($SkipDatabase.IsPresent)" -ForegroundColor White
Write-Host "  CI/CD Mode     : $IsCI"             -ForegroundColor White
Write-Host ""

# ── 1. Validate Azure CLI ────────────────────────────────────────────────────
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

# ── 2. Get admin credentials ─────────────────────────────────────────────────
Write-Host ""
Write-Host "► Retrieving admin credentials..." -ForegroundColor Yellow

$adminObjectId    = ""
$adminUpn         = ""
$adminPrincipalType = ""

if ($IsCI) {
    # CI/CD: use the Service Principal from environment
    $servicePrincipalClientId = $env:AZURE_CLIENT_ID
    if ([string]::IsNullOrWhiteSpace($servicePrincipalClientId)) {
        Write-Error "AZURE_CLIENT_ID environment variable is not set. Ensure it is mapped in the workflow step's 'env:' block."
        exit 1
    }

    $spJson = az ad sp show --id $servicePrincipalClientId --output json 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($spJson)) {
        Write-Error "Failed to retrieve Service Principal info for client ID: $servicePrincipalClientId"
        exit 1
    }

    $sp              = $spJson | ConvertFrom-Json
    $adminObjectId   = $sp.id
    $adminUpn        = $sp.displayName
    $adminPrincipalType = "Application"

    Write-Host "  ✓ CI/CD Service Principal: $adminUpn ($adminObjectId)" -ForegroundColor Green
}
else {
    # Local interactive: use the signed-in user
    $userJson = az ad signed-in-user show --output json 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($userJson)) {
        Write-Error "Failed to retrieve signed-in user info. Ensure you are logged in with: az login"
        exit 1
    }

    $user            = $userJson | ConvertFrom-Json
    $adminObjectId   = $user.id
    $adminUpn        = $user.userPrincipalName
    $adminPrincipalType = "User"

    Write-Host "  ✓ Local User: $adminUpn ($adminObjectId)" -ForegroundColor Green
}

# ── 3. Create resource group ─────────────────────────────────────────────────
Write-Host ""
Write-Host "► Creating resource group '$ResourceGroup' in '$Location'..." -ForegroundColor Yellow

az group create `
    --name $ResourceGroup `
    --location $Location `
    --output none 2>$null

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create resource group '$ResourceGroup'."
    exit 1
}
Write-Host "  ✓ Resource group ready" -ForegroundColor Green

# ── 4. Deploy Bicep infrastructure ───────────────────────────────────────────
Write-Host ""
Write-Host "► Deploying Bicep infrastructure (this may take 5-10 minutes)..." -ForegroundColor Yellow

$bicepFile      = Join-Path $PSScriptRoot "main.bicep"
$deployGenAIStr = $DeployGenAI.ToString().ToLower()

$deployOutput = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file $bicepFile `
    --parameters location=$Location baseName=$BaseName deployGenAI=$deployGenAIStr adminObjectId=$adminObjectId adminUserPrincipalName=$adminUpn adminPrincipalType=$adminPrincipalType `
    --output json 2>$null

# ── 4a. Azure Policy resilience ───────────────────────────────────────────────
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($deployOutput)) {
    Write-Warning "Deployment command returned an error. This can happen when Azure Policies are being applied in the background."
    Write-Host "  Waiting 15 seconds for policy deployments to settle..." -ForegroundColor Yellow
    Start-Sleep -Seconds 15

    # Find the main deployment (not policy-related deployments)
    $allDeploymentsJson = az deployment group list `
        --resource-group $ResourceGroup `
        --output json 2>$null

    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($allDeploymentsJson)) {
        $allDeployments = $allDeploymentsJson | ConvertFrom-Json
        $mainDeployment = $allDeployments | Where-Object {
            $_.name -notlike "PolicyDeployment_*"  -and
            $_.name -notlike "Failure-Anomalies-*" -and
            $_.name -notlike "*-diagnostics-*"     -and
            $_.properties.provisioningState -eq "Succeeded"
        } | Sort-Object -Property @{ Expression = { [datetime]$_.properties.timestamp }; Descending = $true } |
            Select-Object -First 1

        if ($mainDeployment) {
            Write-Host "  ✓ Found successful deployment: $($mainDeployment.name)" -ForegroundColor Green
            $deployOutput = az deployment group show `
                --resource-group $ResourceGroup `
                --name $mainDeployment.name `
                --output json 2>$null
        }
        else {
            Write-Error "Infrastructure deployment failed. No successful main deployment found. Check the Azure portal for details."
            exit 1
        }
    }
    else {
        Write-Error "Infrastructure deployment failed and could not list deployments. Check the Azure portal."
        exit 1
    }
}

if ([string]::IsNullOrWhiteSpace($deployOutput)) {
    Write-Error "Deployment output is empty. Cannot proceed."
    exit 1
}

$deployment = $deployOutput | ConvertFrom-Json
$outputs    = $deployment.properties.outputs

# ── 4b. Extract Bicep outputs ────────────────────────────────────────────────
$webAppName                   = $outputs.webAppName.value
$defaultHostName              = $outputs.defaultHostName.value
$sqlServerFqdn                = $outputs.sqlServerFqdn.value
$sqlServerName                = $outputs.sqlServerName.value
$databaseName                 = $outputs.databaseName.value
$managedIdentityName          = $outputs.managedIdentityName.value
$managedIdentityClientId      = $outputs.managedIdentityClientId.value
$managedIdentityPrincipalId   = $outputs.managedIdentityPrincipalId.value
$appInsightsConnectionString  = $outputs.appInsightsConnectionString.value
$openAIEndpoint               = $outputs.openAIEndpoint.value
$openAIModelName              = $outputs.openAIModelName.value

Write-Host "  ✓ Bicep deployment succeeded" -ForegroundColor Green
Write-Host "    Web App     : $webAppName"      -ForegroundColor Gray
Write-Host "    SQL Server  : $sqlServerFqdn"   -ForegroundColor Gray
Write-Host "    MI Name     : $managedIdentityName" -ForegroundColor Gray

# ── 5. Add current IP to SQL firewall ────────────────────────────────────────
Write-Host ""
Write-Host "► Adding current IP to SQL Server firewall..." -ForegroundColor Yellow

$currentIp = (Invoke-RestMethod -Uri "https://api.ipify.org" -TimeoutSec 10 -ErrorAction SilentlyContinue)
if ([string]::IsNullOrWhiteSpace($currentIp)) {
    Write-Warning "Could not detect public IP. Skipping firewall rule."
}
else {
    az sql server firewall-rule create `
        --resource-group $ResourceGroup `
        --server $sqlServerName `
        --name "DeployScript-$(Get-Date -Format 'yyyyMMddHHmm')" `
        --start-ip-address $currentIp `
        --end-ip-address   $currentIp `
        --output none 2>$null

    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Firewall rule added for IP: $currentIp" -ForegroundColor Green
    }
    else {
        Write-Warning "Could not add firewall rule. SQL operations may fail if your IP is blocked."
    }
}

# ── 6. Database setup ────────────────────────────────────────────────────────
if (-not $SkipDatabase) {
    $authMethod = if ($IsCI) { "ActiveDirectoryAzCli" } else { "ActiveDirectoryDefault" }

    Write-Host ""
    Write-Host "► Importing database schema..." -ForegroundColor Yellow
    Write-Host "  Auth method : $authMethod" -ForegroundColor Gray
    Write-Host "  Server      : $sqlServerFqdn" -ForegroundColor Gray
    Write-Host "  Database    : $databaseName" -ForegroundColor Gray

    # ── 6a. Validate sqlcmd ──────────────────────────────────────────────────
    if (-not (Get-Command sqlcmd -ErrorAction SilentlyContinue)) {
        Write-Error "sqlcmd is not installed.`n  Windows: winget install sqlcmd`n  Linux:   See .github/CICD-SETUP.md"
        exit 1
    }

    # Wait briefly for SQL Server to be reachable after firewall change
    Write-Host "  Waiting 10 seconds for SQL Server to be reachable..." -ForegroundColor Gray
    Start-Sleep -Seconds 10

    # ── 6b. Import schema ────────────────────────────────────────────────────
    $schemaFile = Join-Path $PSScriptRoot "../Database-Schema/database_schema.sql"
    $schemaFile = [System.IO.Path]::GetFullPath($schemaFile)

    if (-not (Test-Path $schemaFile)) {
        Write-Error "Schema file not found: $schemaFile"
        exit 1
    }

    sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $schemaFile
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to import database schema. Check SQL Server connectivity and permissions."
        exit 1
    }
    Write-Host "  ✓ Schema imported successfully" -ForegroundColor Green

    # ── 6c. Create managed identity database user (SID-based) ────────────────
    Write-Host ""
    Write-Host "► Creating managed identity database user..." -ForegroundColor Yellow

    # Convert Client ID GUID to SID hex (no Directory Reader permission required)
    $guidBytes = [System.Guid]::Parse($managedIdentityClientId).ToByteArray()
    $sidHex    = "0x" + [System.BitConverter]::ToString($guidBytes).Replace("-", "")

    $createUserSql = @"
IF EXISTS (SELECT * FROM sys.database_principals WHERE name = '$managedIdentityName')
    DROP USER [$managedIdentityName];

CREATE USER [$managedIdentityName] WITH SID = $sidHex, TYPE = E;

ALTER ROLE db_datareader ADD MEMBER [$managedIdentityName];
ALTER ROLE db_datawriter ADD MEMBER [$managedIdentityName];
GRANT EXECUTE TO [$managedIdentityName];
"@

    $tempUserSql = [System.IO.Path]::GetTempFileName() + ".sql"
    try {
        $createUserSql | Out-File -FilePath $tempUserSql -Encoding UTF8
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $tempUserSql
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to create managed identity database user."
            exit 1
        }
        Write-Host "  ✓ Managed identity user '$managedIdentityName' created" -ForegroundColor Green
    }
    finally {
        Remove-Item -Path $tempUserSql -Force -ErrorAction SilentlyContinue
    }

    # ── 6d. Import stored procedures ─────────────────────────────────────────
    Write-Host ""
    Write-Host "► Importing stored procedures..." -ForegroundColor Yellow

    $storedProcFile = Join-Path $PSScriptRoot "../stored-procedures.sql"
    $storedProcFile = [System.IO.Path]::GetFullPath($storedProcFile)

    if (-not (Test-Path $storedProcFile)) {
        Write-Warning "Stored procedures file not found: $storedProcFile — skipping."
    }
    else {
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $storedProcFile
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to import stored procedures."
            exit 1
        }
        Write-Host "  ✓ Stored procedures imported successfully" -ForegroundColor Green
    }
}
else {
    Write-Host ""
    Write-Host "  ℹ Database setup skipped (SkipDatabase flag set)" -ForegroundColor DarkYellow
}

# ── 7. Configure App Service settings ────────────────────────────────────────
Write-Host ""
Write-Host "► Configuring App Service settings..." -ForegroundColor Yellow

$sqlConnectionString = "Server=tcp:$sqlServerFqdn,1433;Initial Catalog=$databaseName;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$managedIdentityClientId;"

# Build settings array as JSON via temp file (never use @- bash syntax)
$appSettings = [ordered]@{
    "AZURE_CLIENT_ID"                       = $managedIdentityClientId
    "ManagedIdentityClientId"               = $managedIdentityClientId
    "APPLICATIONINSIGHTS_CONNECTION_STRING" = $appInsightsConnectionString
    "ApplicationInsightsAgent_EXTENSION_VERSION" = "~3"
}

if ($DeployGenAI -and -not [string]::IsNullOrWhiteSpace($openAIEndpoint)) {
    $appSettings["GenAISettings__OpenAIEndpoint"]  = $openAIEndpoint
    $appSettings["GenAISettings__OpenAIModelName"] = $openAIModelName
    Write-Host "  → GenAI settings included (OpenAI endpoint: $openAIEndpoint)" -ForegroundColor Gray
}

# Convert to the flat key=value format Azure CLI expects
$settingsList = $appSettings.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }

az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --settings @settingsList `
    --output none 2>$null

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to configure App Service app settings."
    exit 1
}
Write-Host "  ✓ App settings configured" -ForegroundColor Green

# Configure connection string separately (avoids quoting edge cases)
az webapp config connection-string set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --connection-string-type SQLAzure `
    --settings "DefaultConnection=$sqlConnectionString" `
    --output none 2>$null

if ($LASTEXITCODE -ne 0) {
    Write-Warning "Could not set connection string via connection-string command. Attempting via appsettings..."
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $webAppName `
        --settings "ConnectionStrings__DefaultConnection=$sqlConnectionString" `
        --output none 2>$null
}
Write-Host "  ✓ Connection string configured" -ForegroundColor Green

# ── 8. Save deployment context ───────────────────────────────────────────────
Write-Host ""
Write-Host "► Saving deployment context..." -ForegroundColor Yellow

$repoRoot = Split-Path -Parent $PSScriptRoot
$contextFile = Join-Path $repoRoot ".deployment-context.json"

$context = [ordered]@{
    resourceGroup              = $ResourceGroup
    location                   = $Location
    webAppName                 = $webAppName
    defaultHostName            = $defaultHostName
    appServiceUrl              = "https://$defaultHostName"
    sqlServerFqdn              = $sqlServerFqdn
    sqlServerName              = $sqlServerName
    databaseName               = $databaseName
    managedIdentityName        = $managedIdentityName
    managedIdentityClientId    = $managedIdentityClientId
    managedIdentityPrincipalId = $managedIdentityPrincipalId
    appInsightsConnectionString = $appInsightsConnectionString
    openAIEndpoint             = $openAIEndpoint
    openAIModelName            = $openAIModelName
    deployedAt                 = (Get-Date -Format "o")
}

$context | ConvertTo-Json -Depth 5 | Out-File -FilePath $contextFile -Encoding UTF8
Write-Host "  ✓ Context saved to: $contextFile" -ForegroundColor Green

# ── 9. Summary ───────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║              Infrastructure Deployment Complete              ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "  Resource Group  : $ResourceGroup"                     -ForegroundColor White
Write-Host "  Web App         : $webAppName"                        -ForegroundColor White
Write-Host "  App Service URL : https://$defaultHostName"           -ForegroundColor Cyan
Write-Host "  Main Interface  : https://$defaultHostName/Index"     -ForegroundColor Cyan
Write-Host "  Swagger UI      : https://$defaultHostName/swagger"   -ForegroundColor Cyan
Write-Host "  SQL Server      : $sqlServerFqdn"                     -ForegroundColor White
Write-Host ""
Write-Host "  Next step: run .\deploy-app\deploy.ps1 to deploy the application code." -ForegroundColor Yellow
Write-Host ""
