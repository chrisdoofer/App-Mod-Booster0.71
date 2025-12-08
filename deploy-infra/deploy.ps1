#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Deploys the infrastructure for the Expense Management application.

.DESCRIPTION
    This script deploys all Azure infrastructure including App Service, SQL Database,
    Managed Identity, and optionally GenAI resources (Azure OpenAI and AI Search).

.PARAMETER ResourceGroup
    Name of the Azure resource group to create or use.

.PARAMETER Location
    Azure region for the resources (e.g., 'uksouth', 'eastus').

.PARAMETER BaseName
    Base name for resources. Defaults to 'expensemgmt'.

.PARAMETER DeployGenAI
    Switch to deploy GenAI resources (Azure OpenAI and AI Search).

.PARAMETER SkipDatabaseSetup
    Switch to skip database schema import and stored procedures (for redeployments).

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth"
    
.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth" -DeployGenAI
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

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Infrastructure Deployment" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Check PowerShell version
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Warning "You are using PowerShell $($PSVersionTable.PSVersion). PowerShell 7+ is recommended for best compatibility."
}

# Check if Azure CLI is installed
Write-Host "Checking prerequisites..." -ForegroundColor Yellow
try {
    $azVersion = az version --output json 2>$null | ConvertFrom-Json
    Write-Host "✓ Azure CLI version: $($azVersion.'azure-cli')" -ForegroundColor Green
}
catch {
    Write-Error "Azure CLI is not installed. Please install it from https://aka.ms/azure-cli"
    exit 1
}

# Check if logged in to Azure
Write-Host "Checking Azure login status..." -ForegroundColor Yellow
try {
    $accountInfo = az account show 2>$null | ConvertFrom-Json
    if (-not $accountInfo) {
        Write-Error "Not logged in to Azure. Please run 'az login' first."
        exit 1
    }
    Write-Host "✓ Logged in as: $($accountInfo.user.name)" -ForegroundColor Green
    Write-Host "✓ Subscription: $($accountInfo.name)" -ForegroundColor Green
}
catch {
    Write-Error "Not logged in to Azure. Please run 'az login' first."
    exit 1
}

# Detect if running in CI/CD
$IsCI = $env:GITHUB_ACTIONS -eq "true" -or $env:TF_BUILD -eq "true" -or $env:CI -eq "true"
Write-Host "Running in CI/CD mode: $IsCI" -ForegroundColor $(if ($IsCI) { "Yellow" } else { "Green" })

# Get administrator credentials based on environment
Write-Host "`nRetrieving administrator credentials..." -ForegroundColor Yellow
if ($IsCI) {
    # Running in CI/CD (GitHub Actions with OIDC)
    Write-Host "Detected CI/CD environment - using Service Principal" -ForegroundColor Yellow
    
    $servicePrincipalClientId = $env:AZURE_CLIENT_ID
    if (-not $servicePrincipalClientId) {
        Write-Error "AZURE_CLIENT_ID environment variable not found. Ensure GitHub Actions OIDC is configured."
        exit 1
    }
    
    # Get Service Principal details
    $spInfo = az ad sp show --id $servicePrincipalClientId 2>$null | ConvertFrom-Json
    if (-not $spInfo) {
        Write-Error "Failed to retrieve Service Principal information."
        exit 1
    }
    
    $adminObjectId = $spInfo.id
    $adminLogin = $spInfo.displayName
    $adminPrincipalType = "Application"
    
    Write-Host "✓ Service Principal: $adminLogin" -ForegroundColor Green
    Write-Host "✓ Object ID: $adminObjectId" -ForegroundColor Green
}
else {
    # Running locally - use signed-in user
    Write-Host "Detected interactive environment - using current user" -ForegroundColor Yellow
    
    $userInfo = az ad signed-in-user show 2>$null | ConvertFrom-Json
    if (-not $userInfo) {
        Write-Error "Failed to retrieve signed-in user information."
        exit 1
    }
    
    $adminObjectId = $userInfo.id
    $adminLogin = $userInfo.userPrincipalName
    $adminPrincipalType = "User"
    
    Write-Host "✓ User: $adminLogin" -ForegroundColor Green
    Write-Host "✓ Object ID: $adminObjectId" -ForegroundColor Green
}

# Create resource group
Write-Host "`nCreating resource group..." -ForegroundColor Yellow
az group create --name $ResourceGroup --location $Location --output none
Write-Host "✓ Resource group created: $ResourceGroup" -ForegroundColor Green

# Deploy Bicep template
Write-Host "`nDeploying infrastructure..." -ForegroundColor Yellow
Write-Host "This may take 5-10 minutes..." -ForegroundColor Gray

$deploymentName = "infra-deployment-$(Get-Date -Format 'yyyyMMddHHmmss')"

try {
    $deploymentOutput = az deployment group create `
        --resource-group $ResourceGroup `
        --name $deploymentName `
        --template-file "./deploy-infra/main.bicep" `
        --parameters location=$Location baseName=$BaseName adminObjectId=$adminObjectId adminLogin=$adminLogin adminPrincipalType=$adminPrincipalType deployGenAI=$($DeployGenAI.ToString().ToLower()) `
        --output json 2>$null
    
    if (-not $deploymentOutput) {
        Write-Error "Deployment failed. Check Azure Portal for details."
        exit 1
    }
    
    $deployment = $deploymentOutput | ConvertFrom-Json
    Write-Host "✓ Infrastructure deployed successfully" -ForegroundColor Green
}
catch {
    Write-Error "Deployment failed: $_"
    exit 1
}

# Extract outputs
$outputs = $deployment.properties.outputs
$webAppName = $outputs.webAppName.value
$sqlServerFqdn = $outputs.sqlServerFqdn.value
$sqlServerName = $outputs.sqlServerName.value
$databaseName = $outputs.databaseName.value
$managedIdentityClientId = $outputs.managedIdentityClientId.value
$managedIdentityName = $outputs.managedIdentityName.value
$appInsightsConnectionString = $outputs.appInsightsConnectionString.value

Write-Host "`nDeployment Outputs:" -ForegroundColor Cyan
Write-Host "  Web App: $webAppName" -ForegroundColor White
Write-Host "  SQL Server: $sqlServerFqdn" -ForegroundColor White
Write-Host "  Database: $databaseName" -ForegroundColor White
Write-Host "  Managed Identity: $managedIdentityName" -ForegroundColor White

# Configure database
if (-not $SkipDatabaseSetup) {
    Write-Host "`nConfiguring database..." -ForegroundColor Yellow
    
    # Wait for SQL Server to be ready
    Write-Host "Waiting for SQL Server to become available..." -ForegroundColor Gray
    Start-Sleep -Seconds 30
    
    # Add current IP to firewall for local execution
    if (-not $IsCI) {
        Write-Host "Adding your IP address to SQL Server firewall..." -ForegroundColor Yellow
        $myIp = (Invoke-WebRequest -Uri "https://api.ipify.org" -UseBasicParsing).Content.Trim()
        az sql server firewall-rule create `
            --resource-group $ResourceGroup `
            --server $sqlServerName `
            --name "LocalDevelopment" `
            --start-ip-address $myIp `
            --end-ip-address $myIp `
            --output none
        Write-Host "✓ Firewall rule added for IP: $myIp" -ForegroundColor Green
    }
    
    # Import database schema
    Write-Host "Importing database schema..." -ForegroundColor Yellow
    $schemaFile = "./Database-Schema/database_schema.sql"
    if (-not (Test-Path $schemaFile)) {
        Write-Error "Schema file not found: $schemaFile"
        exit 1
    }
    
    $authMethod = if ($IsCI) { "ActiveDirectoryAzCli" } else { "ActiveDirectoryDefault" }
    
    try {
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $schemaFile
        Write-Host "✓ Database schema imported" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to import database schema: $_"
        exit 1
    }
    
    # Create managed identity database user
    Write-Host "Creating managed identity database user..." -ForegroundColor Yellow
    
    # Convert Client ID to SID
    $guidBytes = [System.Guid]::Parse($managedIdentityClientId).ToByteArray()
    $sidHex = "0x" + [System.BitConverter]::ToString($guidBytes).Replace("-", "")
    
    $createUserSql = @"
IF EXISTS (SELECT * FROM sys.database_principals WHERE name = '$managedIdentityName')
    DROP USER [$managedIdentityName];

CREATE USER [$managedIdentityName] WITH SID = $sidHex, TYPE = E;

ALTER ROLE db_datareader ADD MEMBER [$managedIdentityName];
ALTER ROLE db_datawriter ADD MEMBER [$managedIdentityName];
GRANT EXECUTE TO [$managedIdentityName];
"@
    
    $tempFile = [System.IO.Path]::GetTempFileName() + ".sql"
    $createUserSql | Out-File -FilePath $tempFile -Encoding UTF8
    
    try {
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $tempFile
        Write-Host "✓ Managed identity user created and permissions granted" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to create managed identity user: $_"
    }
    finally {
        Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
    }
    
    # Import stored procedures
    Write-Host "Importing stored procedures..." -ForegroundColor Yellow
    $storedProcsFile = "./stored-procedures.sql"
    if (Test-Path $storedProcsFile) {
        try {
            sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $storedProcsFile
            Write-Host "✓ Stored procedures imported" -ForegroundColor Green
        }
        catch {
            Write-Warning "Failed to import stored procedures. This may be expected if the file doesn't exist yet."
        }
    }
    else {
        Write-Warning "Stored procedures file not found: $storedProcsFile"
    }
}

# Configure App Service settings
Write-Host "`nConfiguring App Service settings..." -ForegroundColor Yellow

$connectionString = "Server=tcp:$sqlServerFqdn,1433;Initial Catalog=$databaseName;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$managedIdentityClientId;"

az webapp config connection-string set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --connection-string-type SQLAzure `
    --settings DefaultConnection="$connectionString" `
    --output none

az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --settings "AZURE_CLIENT_ID=$managedIdentityClientId" "ManagedIdentityClientId=$managedIdentityClientId" `
    --output none

Write-Host "✓ App Service connection string configured" -ForegroundColor Green
Write-Host "✓ Managed identity client ID configured" -ForegroundColor Green

# Configure GenAI settings if deployed
if ($DeployGenAI) {
    Write-Host "`nConfiguring GenAI settings..." -ForegroundColor Yellow
    
    $openAIEndpoint = $outputs.openAIEndpoint.value
    $openAIModelName = $outputs.openAIModelName.value
    
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $webAppName `
        --settings "GenAISettings__OpenAIEndpoint=$openAIEndpoint" "GenAISettings__OpenAIModelName=$openAIModelName" `
        --output none
    
    Write-Host "✓ GenAI settings configured" -ForegroundColor Green
    Write-Host "  OpenAI Endpoint: $openAIEndpoint" -ForegroundColor White
    Write-Host "  Model: $openAIModelName" -ForegroundColor White
}

# Save deployment context
Write-Host "`nSaving deployment context..." -ForegroundColor Yellow
$contextPath = "./.deployment-context.json"
$context = @{
    resourceGroup            = $ResourceGroup
    webAppName               = $webAppName
    sqlServerFqdn            = $sqlServerFqdn
    databaseName             = $databaseName
    managedIdentityClientId  = $managedIdentityClientId
    deployedAt               = (Get-Date).ToString("yyyy-MM-ddTHH:mm:ssZ")
    genAIEnabled             = $DeployGenAI.IsPresent
}

if ($DeployGenAI) {
    $context.openAIEndpoint = $outputs.openAIEndpoint.value
    $context.openAIModelName = $outputs.openAIModelName.value
}

$context | ConvertTo-Json | Out-File -FilePath $contextPath -Encoding UTF8
Write-Host "✓ Deployment context saved to: $contextPath" -ForegroundColor Green

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Cyan
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Run the application deployment: .\deploy-app\deploy.ps1" -ForegroundColor White
Write-Host "2. Navigate to: https://$($webAppName).azurewebsites.net/Index" -ForegroundColor White
Write-Host ""
