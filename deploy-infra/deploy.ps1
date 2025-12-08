<#
.SYNOPSIS
    Deploys the Expense Management infrastructure to Azure.

.DESCRIPTION
    This script automates the deployment of all Azure infrastructure including:
    - Azure App Service with Managed Identity
    - Azure SQL Database with Entra ID authentication
    - Azure Monitor (Log Analytics and Application Insights)
    - Optionally, Azure OpenAI and AI Search for GenAI features

.PARAMETER ResourceGroup
    The name of the Azure resource group to deploy to (will be created if it doesn't exist).

.PARAMETER Location
    The Azure region for deployment (e.g., 'uksouth', 'eastus').

.PARAMETER BaseName
    Base name for resources. Defaults to 'expensemgmt'.

.PARAMETER DeployGenAI
    Switch to deploy GenAI resources (Azure OpenAI and AI Search).

.PARAMETER SkipDatabaseSetup
    Switch to skip database schema import and user creation (useful for redeployments).

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth"

.EXAMPLE
    .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth" -DeployGenAI
#>

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

# Detect CI/CD environment
$IsCI = $env:GITHUB_ACTIONS -eq "true" -or $env:TF_BUILD -eq "true" -or $env:CI -eq "true"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Expense Management Infrastructure Deploy" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check PowerShell version
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Host "Warning: You are running PowerShell $($PSVersionTable.PSVersion). PowerShell 7+ is recommended." -ForegroundColor Yellow
}

# Check Azure CLI is installed
Write-Host "Checking Azure CLI..." -ForegroundColor Yellow
try {
    $azVersion = az version --output json | ConvertFrom-Json
    Write-Host "Azure CLI version: $($azVersion.'azure-cli')" -ForegroundColor Green
} catch {
    Write-Host "Error: Azure CLI is not installed or not in PATH." -ForegroundColor Red
    Write-Host "Install from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli" -ForegroundColor Red
    exit 1
}

# Check user is logged in
Write-Host "Checking Azure login status..." -ForegroundColor Yellow
$account = az account show --output json 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Error: Not logged in to Azure. Run 'az login' first." -ForegroundColor Red
    exit 1
}
Write-Host "Logged in as: $($account.user.name)" -ForegroundColor Green
Write-Host "Subscription: $($account.name)" -ForegroundColor Green

# Get current user's credentials
Write-Host ""
Write-Host "Retrieving Azure AD credentials..." -ForegroundColor Yellow

if ($IsCI) {
    # CI/CD mode - use Service Principal
    Write-Host "Running in CI/CD mode" -ForegroundColor Cyan
    $servicePrincipalClientId = $env:AZURE_CLIENT_ID
    
    if (-not $servicePrincipalClientId) {
        Write-Host "Error: AZURE_CLIENT_ID environment variable not set." -ForegroundColor Red
        exit 1
    }
    
    $spInfo = az ad sp show --id $servicePrincipalClientId --output json | ConvertFrom-Json
    $adminObjectId = $spInfo.id
    $adminUpn = $spInfo.displayName
    $adminPrincipalType = "Application"
    
    Write-Host "Service Principal: $adminUpn" -ForegroundColor Green
} else {
    # Interactive mode - use current user
    Write-Host "Running in interactive mode" -ForegroundColor Cyan
    $currentUser = az ad signed-in-user show --output json | ConvertFrom-Json
    $adminObjectId = $currentUser.id
    $adminUpn = $currentUser.userPrincipalName
    $adminPrincipalType = "User"
    
    Write-Host "User: $adminUpn" -ForegroundColor Green
}

Write-Host "Object ID: $adminObjectId" -ForegroundColor Green

# Create resource group if needed
Write-Host ""
Write-Host "Creating resource group '$ResourceGroup' in '$Location'..." -ForegroundColor Yellow
az group create --name $ResourceGroup --location $Location --output none
Write-Host "Resource group ready." -ForegroundColor Green

# Deploy infrastructure with Bicep
Write-Host ""
Write-Host "Deploying infrastructure with Bicep..." -ForegroundColor Yellow
Write-Host "This may take several minutes..." -ForegroundColor Gray

$deploymentName = "infra-$(Get-Date -Format 'yyyyMMddHHmmss')"
$templateFile = Join-Path $PSScriptRoot "main.bicep"

$deployParams = @(
    "--resource-group", $ResourceGroup,
    "--template-file", $templateFile,
    "--name", $deploymentName,
    "--parameters", "location=$Location",
    "--parameters", "baseName=$BaseName",
    "--parameters", "adminObjectId=$adminObjectId",
    "--parameters", "adminUpn=$adminUpn",
    "--parameters", "adminPrincipalType=$adminPrincipalType",
    "--parameters", "deployGenAI=$($DeployGenAI.ToString().ToLower())"
)

$deploymentResult = az deployment group create @deployParams --output json | ConvertFrom-Json

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Bicep deployment failed." -ForegroundColor Red
    exit 1
}

Write-Host "Bicep deployment completed successfully." -ForegroundColor Green

# Get deployment outputs
$outputs = $deploymentResult.properties.outputs
$webAppName = $outputs.webAppName.value
$sqlServerFqdn = $outputs.sqlServerFqdn.value
$sqlServerName = $outputs.sqlServerName.value
$databaseName = $outputs.databaseName.value
$managedIdentityClientId = $outputs.managedIdentityClientId.value
$managedIdentityName = $outputs.managedIdentityName.value
$appInsightsConnectionString = $outputs.appInsightsConnectionString.value

Write-Host ""
Write-Host "Deployment Outputs:" -ForegroundColor Cyan
Write-Host "  Web App: $webAppName" -ForegroundColor White
Write-Host "  SQL Server: $sqlServerFqdn" -ForegroundColor White
Write-Host "  Database: $databaseName" -ForegroundColor White
Write-Host "  Managed Identity: $managedIdentityName" -ForegroundColor White

# Wait for SQL Server to be ready
Write-Host ""
Write-Host "Waiting for SQL Server to be ready..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

# Add current IP to SQL Server firewall
if (-not $IsCI) {
    Write-Host ""
    Write-Host "Adding current IP to SQL Server firewall..." -ForegroundColor Yellow
    $currentIp = (Invoke-RestMethod -Uri "https://api.ipify.org?format=text" -TimeoutSec 10)
    az sql server firewall-rule create `
        --resource-group $ResourceGroup `
        --server $sqlServerName `
        --name "DeploymentClient" `
        --start-ip-address $currentIp `
        --end-ip-address $currentIp `
        --output none
    Write-Host "Firewall rule added for IP: $currentIp" -ForegroundColor Green
}

if (-not $SkipDatabaseSetup) {
    # Determine authentication method for sqlcmd
    $authMethod = if ($IsCI) { "ActiveDirectoryAzCli" } else { "ActiveDirectoryDefault" }
    
    # Import database schema
    Write-Host ""
    Write-Host "Importing database schema..." -ForegroundColor Yellow
    $schemaFile = Join-Path $PSScriptRoot ".." "Database-Schema" "database_schema.sql"
    
    if (Test-Path $schemaFile) {
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $schemaFile
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Warning: Database schema import may have had issues." -ForegroundColor Yellow
        } else {
            Write-Host "Database schema imported successfully." -ForegroundColor Green
        }
    } else {
        Write-Host "Warning: Database schema file not found at $schemaFile" -ForegroundColor Yellow
    }
    
    # Create managed identity user in database using SID-based approach
    Write-Host ""
    Write-Host "Creating managed identity database user..." -ForegroundColor Yellow
    
    # Convert Client ID (GUID) to SID hex format
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
    
    $tempFile = [System.IO.Path]::GetTempFileName() + ".sql"
    $createUserSql | Out-File -FilePath $tempFile -Encoding UTF8
    
    sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $tempFile
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Warning: Managed identity user creation may have had issues." -ForegroundColor Yellow
    } else {
        Write-Host "Managed identity user created successfully." -ForegroundColor Green
    }
    
    Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
    
    # Import stored procedures
    Write-Host ""
    Write-Host "Importing stored procedures..." -ForegroundColor Yellow
    $storedProcFile = Join-Path $PSScriptRoot ".." "stored-procedures.sql"
    
    if (Test-Path $storedProcFile) {
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $storedProcFile
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Warning: Stored procedures import may have had issues." -ForegroundColor Yellow
        } else {
            Write-Host "Stored procedures imported successfully." -ForegroundColor Green
        }
    } else {
        Write-Host "Warning: Stored procedures file not found at $storedProcFile" -ForegroundColor Yellow
    }
}

# Configure App Service settings
Write-Host ""
Write-Host "Configuring App Service settings..." -ForegroundColor Yellow

$connectionString = "Server=tcp:$sqlServerFqdn,1433;Initial Catalog=$databaseName;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=$managedIdentityClientId;"

$appSettings = @(
    "AZURE_CLIENT_ID=$managedIdentityClientId",
    "ManagedIdentityClientId=$managedIdentityClientId"
)

az webapp config appsettings set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --settings @appSettings `
    --output none

az webapp config connection-string set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --connection-string-type SQLAzure `
    --settings "DefaultConnection=$connectionString" `
    --output none

Write-Host "App Service settings configured." -ForegroundColor Green

# Configure GenAI settings if deployed
if ($DeployGenAI) {
    Write-Host ""
    Write-Host "Configuring GenAI settings..." -ForegroundColor Yellow
    
    $openAIEndpoint = $outputs.openAIEndpoint.value
    $openAIModelName = $outputs.openAIModelName.value
    $aiSearchEndpoint = $outputs.aiSearchEndpoint.value
    
    $genAISettings = @(
        "GenAISettings__OpenAIEndpoint=$openAIEndpoint",
        "GenAISettings__OpenAIModelName=$openAIModelName",
        "GenAISettings__AISearchEndpoint=$aiSearchEndpoint"
    )
    
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $webAppName `
        --settings @genAISettings `
        --output none
    
    Write-Host "GenAI settings configured." -ForegroundColor Green
}

# Save deployment context for app deployment
Write-Host ""
Write-Host "Saving deployment context..." -ForegroundColor Yellow

$contextFile = Join-Path $PSScriptRoot ".." ".deployment-context.json"
$context = @{
    resourceGroup = $ResourceGroup
    webAppName = $webAppName
    sqlServerFqdn = $sqlServerFqdn
    databaseName = $databaseName
    managedIdentityClientId = $managedIdentityClientId
    managedIdentityName = $managedIdentityName
    appInsightsConnectionString = $appInsightsConnectionString
    deployedGenAI = $DeployGenAI.IsPresent
}

if ($DeployGenAI) {
    $context.openAIEndpoint = $outputs.openAIEndpoint.value
    $context.openAIModelName = $outputs.openAIModelName.value
    $context.aiSearchEndpoint = $outputs.aiSearchEndpoint.value
}

$context | ConvertTo-Json -Depth 10 | Out-File -FilePath $contextFile -Encoding UTF8
Write-Host "Deployment context saved to: $contextFile" -ForegroundColor Green

# Final summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Infrastructure Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Resources deployed:" -ForegroundColor Yellow
Write-Host "  Resource Group: $ResourceGroup" -ForegroundColor White
Write-Host "  Web App: $webAppName" -ForegroundColor White
Write-Host "  SQL Server: $sqlServerFqdn" -ForegroundColor White
Write-Host "  Database: $databaseName" -ForegroundColor White
Write-Host "  Managed Identity: $managedIdentityName" -ForegroundColor White
if ($DeployGenAI) {
    Write-Host "  OpenAI Endpoint: $($outputs.openAIEndpoint.value)" -ForegroundColor White
    Write-Host "  AI Search Endpoint: $($outputs.aiSearchEndpoint.value)" -ForegroundColor White
}
Write-Host ""
Write-Host "Next step: Run deploy-app/deploy.ps1 to deploy the application code." -ForegroundColor Cyan
Write-Host ""
