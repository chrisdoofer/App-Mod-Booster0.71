<#
.SYNOPSIS
    Deploys the infrastructure for the Expense Management application.

.DESCRIPTION
    This script deploys all Azure infrastructure including App Service, Azure SQL,
    Managed Identity, Monitoring, and optionally GenAI resources (Azure OpenAI and AI Search).

.PARAMETER ResourceGroup
    The name of the Azure resource group to deploy to (required).

.PARAMETER Location
    The Azure region for deployment (required).

.PARAMETER BaseName
    Base name for resources. Defaults to 'expensemgmt'.

.PARAMETER DeployGenAI
    Switch to deploy Azure OpenAI and AI Search resources.

.PARAMETER SkipDatabaseSetup
    Switch to skip database schema import (useful for redeployments).

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expense-20251209" -Location "uksouth"

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expense-20251209" -Location "uksouth" -DeployGenAI
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroup,

    [Parameter(Mandatory = $true)]
    [string]$Location,

    [string]$BaseName = "expensemgmt",

    [switch]$DeployGenAI,

    [switch]$SkipDatabaseSetup
)

$ErrorActionPreference = "Stop"

# Detect CI/CD environment
$IsCI = $env:GITHUB_ACTIONS -eq "true" -or $env:TF_BUILD -eq "true" -or $env:CI -eq "true"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Expense Management Infrastructure Deploy" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Resource Group: $ResourceGroup"
Write-Host "Location: $Location"
Write-Host "Base Name: $BaseName"
Write-Host "Deploy GenAI: $DeployGenAI"
Write-Host "CI/CD Mode: $IsCI"
Write-Host ""

# Check PowerShell version
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Warning "You are running PowerShell $($PSVersionTable.PSVersion). PowerShell 7+ is recommended."
}

# Check Azure CLI is installed and logged in
Write-Host "Checking Azure CLI..." -ForegroundColor Yellow
try {
    $account = az account show 2>$null | ConvertFrom-Json
    if (-not $account) {
        throw "Not logged in"
    }
    Write-Host "Logged in as: $($account.user.name)" -ForegroundColor Green
    Write-Host "Subscription: $($account.name)" -ForegroundColor Green
}
catch {
    Write-Error "Azure CLI is not installed or you are not logged in. Run 'az login' first."
    exit 1
}

# Get admin credentials based on environment
Write-Host ""
Write-Host "Retrieving admin credentials..." -ForegroundColor Yellow

if ($IsCI) {
    # CI/CD mode - use service principal
    $servicePrincipalClientId = $env:AZURE_CLIENT_ID
    if (-not $servicePrincipalClientId) {
        Write-Error "AZURE_CLIENT_ID environment variable not set. Required for CI/CD."
        exit 1
    }
    
    $spInfo = az ad sp show --id $servicePrincipalClientId 2>$null | ConvertFrom-Json
    $adminObjectId = $spInfo.id
    $adminLogin = $spInfo.displayName
    $adminPrincipalType = "Application"
    
    Write-Host "Using Service Principal: $adminLogin" -ForegroundColor Green
}
else {
    # Interactive mode - use current user
    $currentUser = az ad signed-in-user show 2>$null | ConvertFrom-Json
    $adminObjectId = $currentUser.id
    $adminLogin = $currentUser.userPrincipalName
    $adminPrincipalType = "User"
    
    Write-Host "Using User: $adminLogin" -ForegroundColor Green
}

Write-Host "Admin Object ID: $adminObjectId"

# Create resource group if it doesn't exist
Write-Host ""
Write-Host "Ensuring resource group exists..." -ForegroundColor Yellow
az group create --name $ResourceGroup --location $Location --output none 2>$null
Write-Host "Resource group '$ResourceGroup' is ready." -ForegroundColor Green

# Deploy Bicep templates
Write-Host ""
Write-Host "Deploying infrastructure..." -ForegroundColor Yellow
Write-Host "This may take several minutes..."

$scriptDir = $PSScriptRoot
$templateFile = Join-Path $scriptDir "main.bicep"

$deployOutput = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file $templateFile `
    --parameters location=$Location baseName=$BaseName adminObjectId=$adminObjectId adminLogin=$adminLogin adminPrincipalType=$adminPrincipalType deployGenAI=$($DeployGenAI.ToString().ToLower()) `
    --output json 2>$null

if ($LASTEXITCODE -ne 0) {
    Write-Error "Infrastructure deployment failed. Check Azure portal for details."
    exit 1
}

$deployment = $deployOutput | ConvertFrom-Json
$outputs = $deployment.properties.outputs

$webAppName = $outputs.webAppName.value
$webAppHostname = $outputs.webAppHostname.value
$sqlServerFqdn = $outputs.sqlServerFqdn.value
$sqlServerName = $outputs.sqlServerName.value
$databaseName = $outputs.databaseName.value
$managedIdentityClientId = $outputs.managedIdentityClientId.value
$managedIdentityName = $outputs.managedIdentityName.value
$appInsightsConnectionString = $outputs.appInsightsConnectionString.value

Write-Host "Infrastructure deployed successfully!" -ForegroundColor Green

# Add current IP to SQL Server firewall
Write-Host ""
Write-Host "Adding current IP to SQL Server firewall..." -ForegroundColor Yellow

$currentIp = (Invoke-RestMethod -Uri "https://api.ipify.org?format=text" -TimeoutSec 10)
az sql server firewall-rule create `
    --resource-group $ResourceGroup `
    --server $sqlServerName `
    --name "DeploymentClient-$(Get-Date -Format 'yyyyMMddHHmmss')" `
    --start-ip-address $currentIp `
    --end-ip-address $currentIp `
    --output none 2>$null

Write-Host "Firewall rule added for IP: $currentIp" -ForegroundColor Green

# Wait for SQL Server to be fully ready
Write-Host ""
Write-Host "Waiting for SQL Server to be ready..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

if (-not $SkipDatabaseSetup) {
    # Import database schema
    Write-Host ""
    Write-Host "Importing database schema..." -ForegroundColor Yellow
    
    $repoRoot = Split-Path -Parent $scriptDir
    $schemaFile = Join-Path $repoRoot "Database-Schema/database_schema.sql"
    
    if (Test-Path $schemaFile) {
        $authMethod = if ($IsCI) { "ActiveDirectoryAzCli" } else { "ActiveDirectoryDefault" }
        
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $schemaFile
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Database schema imported successfully!" -ForegroundColor Green
        }
        else {
            Write-Warning "Database schema import may have encountered issues. Check the output above."
        }
    }
    else {
        Write-Warning "Database schema file not found at: $schemaFile"
    }
    
    # Create managed identity database user using SID-based approach
    Write-Host ""
    Write-Host "Creating managed identity database user..." -ForegroundColor Yellow
    
    # Convert Client ID to SID hex format
    $guidBytes = [System.Guid]::Parse($managedIdentityClientId).ToByteArray()
    $sidHex = "0x" + [System.BitConverter]::ToString($guidBytes).Replace("-", "")
    
    $createUserSql = @"
IF EXISTS (SELECT * FROM sys.database_principals WHERE name = '$managedIdentityName')
    DROP USER [$managedIdentityName];

CREATE USER [$managedIdentityName] WITH SID = $sidHex, TYPE = E;

ALTER ROLE db_datareader ADD MEMBER [$managedIdentityName];
ALTER ROLE db_datawriter ADD MEMBER [$managedIdentityName];
GRANT EXECUTE TO [$managedIdentityName];

PRINT 'Managed identity user created and permissions granted.';
"@
    
    # Write SQL to temp file (avoid piping to sqlcmd)
    $tempFile = [System.IO.Path]::GetTempFileName() + ".sql"
    $createUserSql | Out-File -FilePath $tempFile -Encoding UTF8
    
    sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $tempFile
    
    Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Managed identity user created successfully!" -ForegroundColor Green
    }
    else {
        Write-Warning "Managed identity user creation may have encountered issues."
    }
    
    # Import stored procedures
    Write-Host ""
    Write-Host "Importing stored procedures..." -ForegroundColor Yellow
    
    $storedProcsFile = Join-Path $repoRoot "stored-procedures.sql"
    
    if (Test-Path $storedProcsFile) {
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $storedProcsFile
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Stored procedures imported successfully!" -ForegroundColor Green
        }
        else {
            Write-Warning "Stored procedures import may have encountered issues."
        }
    }
    else {
        Write-Warning "Stored procedures file not found at: $storedProcsFile"
    }
}
else {
    Write-Host "Skipping database setup as requested." -ForegroundColor Yellow
}

# Configure App Service settings
Write-Host ""
Write-Host "Configuring App Service settings..." -ForegroundColor Yellow

$connectionString = "Server=tcp:${sqlServerFqdn},1433;Initial Catalog=${databaseName};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=${managedIdentityClientId};"

az webapp config appsettings set `
    --name $webAppName `
    --resource-group $ResourceGroup `
    --settings "AZURE_CLIENT_ID=$managedIdentityClientId" "ManagedIdentityClientId=$managedIdentityClientId" "APPLICATIONINSIGHTS_CONNECTION_STRING=$appInsightsConnectionString" `
    --output none 2>$null

az webapp config connection-string set `
    --name $webAppName `
    --resource-group $ResourceGroup `
    --connection-string-type SQLAzure `
    --settings "DefaultConnection=$connectionString" `
    --output none 2>$null

Write-Host "App Service settings configured!" -ForegroundColor Green

# Configure GenAI settings if deployed
if ($DeployGenAI) {
    Write-Host ""
    Write-Host "Configuring GenAI settings..." -ForegroundColor Yellow
    
    $openAIEndpoint = $outputs.openAIEndpoint.value
    $openAIModelName = $outputs.openAIModelName.value
    $searchEndpoint = $outputs.searchEndpoint.value
    
    az webapp config appsettings set `
        --name $webAppName `
        --resource-group $ResourceGroup `
        --settings "GenAISettings__OpenAIEndpoint=$openAIEndpoint" "GenAISettings__OpenAIModelName=$openAIModelName" "GenAISettings__SearchEndpoint=$searchEndpoint" `
        --output none 2>$null
    
    Write-Host "GenAI settings configured!" -ForegroundColor Green
}

# Save deployment context for app deployment script
Write-Host ""
Write-Host "Saving deployment context..." -ForegroundColor Yellow

$context = @{
    resourceGroup = $ResourceGroup
    webAppName = $webAppName
    webAppHostname = $webAppHostname
    sqlServerFqdn = $sqlServerFqdn
    sqlServerName = $sqlServerName
    databaseName = $databaseName
    managedIdentityClientId = $managedIdentityClientId
    managedIdentityName = $managedIdentityName
    appInsightsConnectionString = $appInsightsConnectionString
    deployedGenAI = $DeployGenAI.IsPresent
    deployedAt = (Get-Date -Format "o")
}

if ($DeployGenAI) {
    $context.openAIEndpoint = $outputs.openAIEndpoint.value
    $context.openAIModelName = $outputs.openAIModelName.value
    $context.searchEndpoint = $outputs.searchEndpoint.value
}

$contextFile = Join-Path $repoRoot ".deployment-context.json"
$context | ConvertTo-Json -Depth 10 | Out-File -FilePath $contextFile -Encoding UTF8

Write-Host "Deployment context saved to: $contextFile" -ForegroundColor Green

# Print summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deployment Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Web App Name: $webAppName" -ForegroundColor White
Write-Host "Web App URL: https://$webAppHostname" -ForegroundColor White
Write-Host "SQL Server: $sqlServerFqdn" -ForegroundColor White
Write-Host "Database: $databaseName" -ForegroundColor White
Write-Host "Managed Identity: $managedIdentityName" -ForegroundColor White

if ($DeployGenAI) {
    Write-Host ""
    Write-Host "GenAI Resources:" -ForegroundColor White
    Write-Host "  OpenAI Endpoint: $($outputs.openAIEndpoint.value)" -ForegroundColor White
    Write-Host "  Model: $($outputs.openAIModelName.value)" -ForegroundColor White
    Write-Host "  Search Endpoint: $($outputs.searchEndpoint.value)" -ForegroundColor White
}

Write-Host ""
Write-Host "Next step: Run deploy-app/deploy.ps1 to deploy the application code." -ForegroundColor Yellow
Write-Host ""
