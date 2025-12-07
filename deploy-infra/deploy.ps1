#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploys the infrastructure for the Expense Management application.

.DESCRIPTION
    This script automates the deployment of all Azure infrastructure including:
    - Azure App Service (Standard S1)
    - Azure SQL Database with Entra ID authentication
    - User-Assigned Managed Identity
    - Application Insights and Log Analytics
    - Optional: Azure OpenAI and AI Search (with -DeployGenAI switch)
    
    The script also:
    - Imports the database schema
    - Creates stored procedures
    - Configures managed identity permissions
    - Sets up App Service configuration

.PARAMETER ResourceGroup
    The name of the Azure resource group (required). Use a unique name with date suffix.

.PARAMETER Location
    The Azure region for deployment (required). Example: 'uksouth', 'eastus'

.PARAMETER BaseName
    Base name for resources (optional). Defaults to 'expensemgmt'

.PARAMETER DeployGenAI
    Switch to deploy Azure OpenAI and AI Search resources

.PARAMETER SkipDatabaseSetup
    Switch to skip database schema import and stored procedure creation (for redeployments)

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251207" -Location "uksouth"

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251207" -Location "uksouth" -DeployGenAI
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
    [switch]$DeployGenAI,
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipDatabaseSetup
)

$ErrorActionPreference = "Stop"

# Check if running in CI/CD environment
$IsCI = $env:GITHUB_ACTIONS -eq "true" -or $env:TF_BUILD -eq "true" -or $env:CI -eq "true"

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Expense Management - Infrastructure Deployment" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Warn about PowerShell version
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Warning "You are using PowerShell $($PSVersionTable.PSVersion). PowerShell 7+ is recommended."
    Write-Warning "Download from: https://aka.ms/powershell-release"
    Write-Host ""
}

# Step 1: Check Azure CLI
Write-Host "Checking Azure CLI..." -ForegroundColor Yellow
try {
    $azVersion = az version --output json | ConvertFrom-Json
    Write-Host "✓ Azure CLI version $($azVersion.'azure-cli') found" -ForegroundColor Green
}
catch {
    Write-Error "Azure CLI is not installed. Download from: https://aka.ms/installazurecliwindows"
    exit 1
}

# Step 2: Check login status
Write-Host "Checking Azure login status..." -ForegroundColor Yellow
try {
    $account = az account show --output json | ConvertFrom-Json
    Write-Host "✓ Logged in as $($account.user.name)" -ForegroundColor Green
    Write-Host "✓ Subscription: $($account.name)" -ForegroundColor Green
}
catch {
    Write-Error "Not logged in to Azure. Run 'az login' first."
    exit 1
}

# Step 3: Get admin credentials
Write-Host ""
Write-Host "Retrieving administrator credentials..." -ForegroundColor Yellow

if ($IsCI) {
    Write-Host "Running in CI/CD mode" -ForegroundColor Cyan
    
    # In CI/CD, use the Service Principal
    $servicePrincipalClientId = $env:AZURE_CLIENT_ID
    if ([string]::IsNullOrEmpty($servicePrincipalClientId)) {
        Write-Error "AZURE_CLIENT_ID environment variable is not set"
        exit 1
    }
    
    # Get Service Principal details
    $spDetails = az ad sp show --id $servicePrincipalClientId --output json | ConvertFrom-Json
    $adminObjectId = $spDetails.id
    $adminUsername = $spDetails.displayName
    $adminPrincipalType = "Application"
    
    Write-Host "✓ Service Principal: $adminUsername" -ForegroundColor Green
    Write-Host "✓ Object ID: $adminObjectId" -ForegroundColor Green
}
else {
    Write-Host "Running in interactive mode" -ForegroundColor Cyan
    
    # Get current user details
    $user = az ad signed-in-user show --output json | ConvertFrom-Json
    $adminObjectId = $user.id
    $adminUsername = $user.userPrincipalName
    $adminPrincipalType = "User"
    
    Write-Host "✓ User: $adminUsername" -ForegroundColor Green
    Write-Host "✓ Object ID: $adminObjectId" -ForegroundColor Green
}

# Step 4: Create resource group
Write-Host ""
Write-Host "Creating resource group..." -ForegroundColor Yellow
$rgExists = az group exists --name $ResourceGroup
if ($rgExists -eq "false") {
    az group create --name $ResourceGroup --location $Location --output none
    Write-Host "✓ Resource group '$ResourceGroup' created" -ForegroundColor Green
}
else {
    Write-Host "✓ Resource group '$ResourceGroup' already exists" -ForegroundColor Green
}

# Step 5: Deploy Bicep templates
Write-Host ""
Write-Host "Deploying infrastructure..." -ForegroundColor Yellow
Write-Host "This may take 5-10 minutes..." -ForegroundColor Gray

$deployParams = @{
    location            = $Location
    baseName            = $BaseName
    adminObjectId       = $adminObjectId
    adminUsername       = $adminUsername
    adminPrincipalType  = $adminPrincipalType
    deployGenAI         = $DeployGenAI.IsPresent
}

$deploymentName = "infra-$(Get-Date -Format 'yyyyMMddHHmmss')"

try {
    $deployment = az deployment group create `
        --resource-group $ResourceGroup `
        --name $deploymentName `
        --template-file "$PSScriptRoot/main.bicep" `
        --parameters $deployParams `
        --output json | ConvertFrom-Json
    
    Write-Host "✓ Infrastructure deployed successfully" -ForegroundColor Green
}
catch {
    Write-Error "Infrastructure deployment failed: $_"
    exit 1
}

# Extract outputs
$outputs = $deployment.properties.outputs
$webAppName = $outputs.webAppName.value
$webAppHostName = $outputs.webAppHostName.value
$managedIdentityClientId = $outputs.managedIdentityClientId.value
$managedIdentityName = $outputs.managedIdentityName.value
$sqlServerFqdn = $outputs.sqlServerFqdn.value
$sqlServerName = $outputs.sqlServerName.value
$databaseName = $outputs.databaseName.value
$appInsightsConnectionString = $outputs.appInsightsConnectionString.value

Write-Host ""
Write-Host "Deployment outputs:" -ForegroundColor Cyan
Write-Host "  Web App: $webAppName" -ForegroundColor Gray
Write-Host "  SQL Server: $sqlServerFqdn" -ForegroundColor Gray
Write-Host "  Managed Identity: $managedIdentityName" -ForegroundColor Gray

# Step 6: Configure SQL Server firewall for current IP
if (-not $SkipDatabaseSetup) {
    Write-Host ""
    Write-Host "Configuring SQL Server firewall..." -ForegroundColor Yellow
    
    try {
        $myIp = (Invoke-RestMethod -Uri "https://api.ipify.org?format=json").ip
        az sql server firewall-rule create `
            --resource-group $ResourceGroup `
            --server $sqlServerName `
            --name "ClientIPAddress" `
            --start-ip-address $myIp `
            --end-ip-address $myIp `
            --output none
        
        Write-Host "✓ Added firewall rule for IP: $myIp" -ForegroundColor Green
    }
    catch {
        Write-Warning "Could not add firewall rule: $_"
    }
    
    # Wait for SQL Server to be ready
    Write-Host "Waiting for SQL Server to be ready..." -ForegroundColor Yellow
    Start-Sleep -Seconds 30
}

# Step 7: Import database schema
if (-not $SkipDatabaseSetup) {
    Write-Host ""
    Write-Host "Importing database schema..." -ForegroundColor Yellow
    
    $schemaFile = Join-Path $PSScriptRoot ".." "Database-Schema" "database_schema.sql"
    
    if (-not (Test-Path $schemaFile)) {
        Write-Warning "Schema file not found: $schemaFile"
    }
    else {
        $authMethod = if ($IsCI) { "ActiveDirectoryAzCli" } else { "ActiveDirectoryDefault" }
        
        try {
            sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $schemaFile
            Write-Host "✓ Database schema imported" -ForegroundColor Green
        }
        catch {
            Write-Warning "Failed to import schema: $_"
        }
    }
}

# Step 8: Create managed identity database user
if (-not $SkipDatabaseSetup) {
    Write-Host ""
    Write-Host "Configuring managed identity database access..." -ForegroundColor Yellow
    
    # Convert Client ID (GUID) to SID hex format
    $guidBytes = [System.Guid]::Parse($managedIdentityClientId).ToByteArray()
    $sidHex = "0x" + [System.BitConverter]::ToString($guidBytes).Replace("-", "")
    
    $createUserSql = @"
-- Drop existing user if exists
IF EXISTS (SELECT * FROM sys.database_principals WHERE name = '$managedIdentityName')
    DROP USER [$managedIdentityName];
GO

-- Create user with SID (no Directory Reader required)
CREATE USER [$managedIdentityName] WITH SID = $sidHex, TYPE = E;
GO

-- Grant permissions
ALTER ROLE db_datareader ADD MEMBER [$managedIdentityName];
ALTER ROLE db_datawriter ADD MEMBER [$managedIdentityName];
GRANT EXECUTE TO [$managedIdentityName];
GO
"@
    
    $tempFile = [System.IO.Path]::GetTempFileName() + ".sql"
    $createUserSql | Out-File -FilePath $tempFile -Encoding UTF8
    
    try {
        $authMethod = if ($IsCI) { "ActiveDirectoryAzCli" } else { "ActiveDirectoryDefault" }
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $tempFile
        Write-Host "✓ Managed identity granted database access" -ForegroundColor Green
    }
    catch {
        Write-Warning "Failed to create database user: $_"
    }
    finally {
        Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
    }
}

# Step 9: Import stored procedures
if (-not $SkipDatabaseSetup) {
    Write-Host ""
    Write-Host "Importing stored procedures..." -ForegroundColor Yellow
    
    $spFile = Join-Path $PSScriptRoot ".." "stored-procedures.sql"
    
    if (-not (Test-Path $spFile)) {
        Write-Warning "Stored procedures file not found: $spFile"
    }
    else {
        try {
            $authMethod = if ($IsCI) { "ActiveDirectoryAzCli" } else { "ActiveDirectoryDefault" }
            sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $spFile
            Write-Host "✓ Stored procedures imported" -ForegroundColor Green
        }
        catch {
            Write-Warning "Failed to import stored procedures: $_"
        }
    }
}

# Step 10: Configure App Service settings
Write-Host ""
Write-Host "Configuring App Service settings..." -ForegroundColor Yellow

$connectionString = "Server=tcp:$sqlServerFqdn,1433;Initial Catalog=$databaseName;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$managedIdentityClientId;"

$appSettings = @(
    "AZURE_CLIENT_ID=$managedIdentityClientId"
    "ManagedIdentityClientId=$managedIdentityClientId"
)

# Add connection string
az webapp config connection-string set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --connection-string-type SQLAzure `
    --settings "DefaultConnection=$connectionString" `
    --output none

# Add app settings
az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --settings @appSettings `
    --output none

Write-Host "✓ App Service configured with connection string and managed identity" -ForegroundColor Green

# Step 11: Configure GenAI settings if deployed
if ($DeployGenAI) {
    Write-Host ""
    Write-Host "Configuring GenAI settings..." -ForegroundColor Yellow
    
    $openAIEndpoint = $outputs.openAIEndpoint.value
    $openAIModelName = $outputs.openAIModelName.value
    $searchEndpoint = $outputs.searchEndpoint.value
    
    $genAISettings = @(
        "GenAISettings__OpenAIEndpoint=$openAIEndpoint"
        "GenAISettings__OpenAIModelName=$openAIModelName"
        "GenAISettings__SearchEndpoint=$searchEndpoint"
    )
    
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $webAppName `
        --settings @genAISettings `
        --output none
    
    Write-Host "✓ GenAI settings configured" -ForegroundColor Green
}

# Step 12: Save deployment context
Write-Host ""
Write-Host "Saving deployment context..." -ForegroundColor Yellow

$contextFile = Join-Path $PSScriptRoot ".." ".deployment-context.json"
$context = @{
    resourceGroup           = $ResourceGroup
    webAppName              = $webAppName
    webAppHostName          = $webAppHostName
    sqlServerFqdn           = $sqlServerFqdn
    managedIdentityClientId = $managedIdentityClientId
    deploymentDate          = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
}

$context | ConvertTo-Json | Out-File -FilePath $contextFile -Encoding UTF8
Write-Host "✓ Deployment context saved to: $contextFile" -ForegroundColor Green

# Summary
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Resources deployed:" -ForegroundColor White
Write-Host "  • App Service:        $webAppName" -ForegroundColor Gray
Write-Host "  • SQL Server:         $sqlServerFqdn" -ForegroundColor Gray
Write-Host "  • Database:           $databaseName" -ForegroundColor Gray
Write-Host "  • Managed Identity:   $managedIdentityName" -ForegroundColor Gray

if ($DeployGenAI) {
    Write-Host "  • Azure OpenAI:       $($outputs.openAIEndpoint.value)" -ForegroundColor Gray
    Write-Host "  • AI Search:          $($outputs.searchEndpoint.value)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Deploy application code: .\deploy-app\deploy.ps1" -ForegroundColor Gray
Write-Host "  2. Access the app at: https://$webAppHostName/Index" -ForegroundColor Gray
Write-Host ""

exit 0
