<#
.SYNOPSIS
    Deploys the infrastructure for the Expense Management application.

.DESCRIPTION
    This script automates the deployment of all Azure infrastructure including:
    - Managed Identity
    - App Service
    - Azure SQL Database
    - Monitoring (Log Analytics + Application Insights)
    - Optionally: Azure OpenAI and AI Search (GenAI resources)

.PARAMETER ResourceGroup
    The name of the Azure resource group to deploy to.

.PARAMETER Location
    The Azure region for deployment (e.g., 'uksouth', 'eastus').

.PARAMETER BaseName
    Base name for resources. Defaults to 'expensemgmt'.

.PARAMETER DeployGenAI
    Switch to deploy Azure OpenAI and AI Search resources.

.PARAMETER SkipDatabaseSetup
    Switch to skip database schema import and stored procedure creation.

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

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Expense Management Infrastructure Deploy  " -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check PowerShell version
if ($PSVersionTable.PSVersion.Major -lt 7) {
    Write-Host "Warning: You are running PowerShell $($PSVersionTable.PSVersion). PowerShell 7+ is recommended." -ForegroundColor Yellow
}

# Check Azure CLI
Write-Host "Checking Azure CLI..." -ForegroundColor Gray
try {
    $azVersion = az version --output json 2>$null | ConvertFrom-Json
    Write-Host "Azure CLI version: $($azVersion.'azure-cli')" -ForegroundColor Green
} catch {
    Write-Host "Error: Azure CLI is not installed or not in PATH." -ForegroundColor Red
    exit 1
}

# Check if logged in
Write-Host "Checking Azure login status..." -ForegroundColor Gray
$account = az account show --output json 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Error: Not logged in to Azure. Run 'az login' first." -ForegroundColor Red
    exit 1
}
Write-Host "Logged in as: $($account.user.name)" -ForegroundColor Green
Write-Host "Subscription: $($account.name)" -ForegroundColor Green

# Get admin credentials based on environment
Write-Host ""
Write-Host "Retrieving administrator credentials..." -ForegroundColor Gray

if ($IsCI) {
    Write-Host "Running in CI/CD mode" -ForegroundColor Yellow
    $servicePrincipalClientId = $env:AZURE_CLIENT_ID
    if (-not $servicePrincipalClientId) {
        Write-Host "Error: AZURE_CLIENT_ID environment variable not set." -ForegroundColor Red
        exit 1
    }
    
    $spInfo = az ad sp show --id $servicePrincipalClientId --output json 2>$null | ConvertFrom-Json
    $adminObjectId = $spInfo.id
    $adminLogin = $spInfo.displayName
    $adminPrincipalType = "Application"
    Write-Host "Service Principal: $adminLogin" -ForegroundColor Green
} else {
    Write-Host "Running in interactive mode" -ForegroundColor Yellow
    $userInfo = az ad signed-in-user show --output json 2>$null | ConvertFrom-Json
    $adminObjectId = $userInfo.id
    $adminLogin = $userInfo.userPrincipalName
    $adminPrincipalType = "User"
    Write-Host "User: $adminLogin" -ForegroundColor Green
}

# Create resource group if needed
Write-Host ""
Write-Host "Creating resource group '$ResourceGroup' in '$Location'..." -ForegroundColor Gray
az group create --name $ResourceGroup --location $Location --output none 2>$null
Write-Host "Resource group ready." -ForegroundColor Green

# Deploy Bicep templates
Write-Host ""
Write-Host "Deploying infrastructure..." -ForegroundColor Cyan
Write-Host "This may take several minutes..." -ForegroundColor Gray

$scriptDir = $PSScriptRoot
$templateFile = Join-Path $scriptDir "main.bicep"

$deployGenAIValue = $DeployGenAI.ToString().ToLower()

$deploymentOutput = az deployment group create `
    --resource-group $ResourceGroup `
    --template-file $templateFile `
    --parameters location=$Location baseName=$BaseName adminObjectId=$adminObjectId adminLogin=$adminLogin adminPrincipalType=$adminPrincipalType deployGenAI=$deployGenAIValue `
    --output json 2>$null

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Bicep deployment failed." -ForegroundColor Red
    exit 1
}

$deployment = $deploymentOutput | ConvertFrom-Json
$outputs = $deployment.properties.outputs

$webAppName = $outputs.webAppName.value
$sqlServerFqdn = $outputs.sqlServerFqdn.value
$sqlServerName = $outputs.sqlServerName.value
$databaseName = $outputs.databaseName.value
$managedIdentityClientId = $outputs.managedIdentityClientId.value
$managedIdentityName = $outputs.managedIdentityName.value
$appInsightsConnectionString = $outputs.appInsightsConnectionString.value

Write-Host "Infrastructure deployed successfully!" -ForegroundColor Green

if (-not $SkipDatabaseSetup) {
    # Wait for SQL Server to be ready
    Write-Host ""
    Write-Host "Waiting for SQL Server to be ready..." -ForegroundColor Gray
    Start-Sleep -Seconds 30

    # Add current IP to firewall
    Write-Host "Adding your IP to SQL Server firewall..." -ForegroundColor Gray
    try {
        $myIp = (Invoke-RestMethod -Uri "https://api.ipify.org" -UseBasicParsing -TimeoutSec 10)
    } catch {
        Write-Host "Warning: Could not detect IP from api.ipify.org. Trying fallback..." -ForegroundColor Yellow
        try {
            $myIp = (Invoke-RestMethod -Uri "https://checkip.amazonaws.com" -UseBasicParsing -TimeoutSec 10).Trim()
        } catch {
            Write-Host "Warning: Could not detect public IP. Please add your IP manually to SQL firewall." -ForegroundColor Yellow
            $myIp = $null
        }
    }
    
    if ($myIp) {
        az sql server firewall-rule create `
            --resource-group $ResourceGroup `
            --server $sqlServerName `
            --name "DeploymentClient" `
            --start-ip-address $myIp `
            --end-ip-address $myIp `
            --output none 2>$null
        Write-Host "Firewall rule added for IP: $myIp" -ForegroundColor Green
    }

    # Set authentication method based on environment
    $authMethod = if ($IsCI) { "ActiveDirectoryAzCli" } else { "ActiveDirectoryDefault" }

    # Import database schema
    Write-Host ""
    Write-Host "Importing database schema..." -ForegroundColor Gray
    $repoRoot = Split-Path -Parent $scriptDir
    $schemaFile = Join-Path $repoRoot "Database-Schema/database_schema.sql"
    
    if (Test-Path $schemaFile) {
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $schemaFile
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Database schema imported successfully." -ForegroundColor Green
        } else {
            Write-Host "Warning: Database schema import may have encountered issues." -ForegroundColor Yellow
        }
    } else {
        Write-Host "Warning: Schema file not found at $schemaFile" -ForegroundColor Yellow
    }

    # Create managed identity database user using SID-based approach
    Write-Host ""
    Write-Host "Creating database user for managed identity..." -ForegroundColor Gray
    
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
    sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $tempFile
    Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Database user created successfully." -ForegroundColor Green
    } else {
        Write-Host "Warning: Database user creation may have encountered issues." -ForegroundColor Yellow
    }

    # Create stored procedures
    Write-Host ""
    Write-Host "Creating stored procedures..." -ForegroundColor Gray
    $storedProcFile = Join-Path $repoRoot "stored-procedures.sql"
    
    if (Test-Path $storedProcFile) {
        sqlcmd -S $sqlServerFqdn -d $databaseName "--authentication-method=$authMethod" -i $storedProcFile
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Stored procedures created successfully." -ForegroundColor Green
        } else {
            Write-Host "Warning: Stored procedure creation may have encountered issues." -ForegroundColor Yellow
        }
    } else {
        Write-Host "Warning: Stored procedures file not found at $storedProcFile" -ForegroundColor Yellow
    }
}

# Configure App Service settings
Write-Host ""
Write-Host "Configuring App Service settings..." -ForegroundColor Gray

$connectionString = "Server=tcp:${sqlServerFqdn},1433;Initial Catalog=${databaseName};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id=${managedIdentityClientId};"

az webapp config connection-string set `
    --resource-group $ResourceGroup `
    --name $webAppName `
    --connection-string-type SQLAzure `
    --settings DefaultConnection=$connectionString `
    --output none 2>$null

Write-Host "Connection string configured." -ForegroundColor Green

# Configure GenAI settings if deployed
if ($DeployGenAI) {
    $openAIEndpoint = $outputs.openAIEndpoint.value
    $openAIModelName = $outputs.openAIModelName.value
    $searchEndpoint = $outputs.searchEndpoint.value
    
    Write-Host "Configuring GenAI settings..." -ForegroundColor Gray
    az webapp config appsettings set `
        --resource-group $ResourceGroup `
        --name $webAppName `
        --settings "GenAISettings__OpenAIEndpoint=$openAIEndpoint" "GenAISettings__OpenAIModelName=$openAIModelName" "GenAISettings__SearchEndpoint=$searchEndpoint" `
        --output none 2>$null
    Write-Host "GenAI settings configured." -ForegroundColor Green
}

# Save deployment context
Write-Host ""
Write-Host "Saving deployment context..." -ForegroundColor Gray

$context = @{
    resourceGroup = $ResourceGroup
    webAppName = $webAppName
    webAppUrl = "https://$($outputs.webAppHostName.value)"
    sqlServerFqdn = $sqlServerFqdn
    databaseName = $databaseName
    managedIdentityClientId = $managedIdentityClientId
    managedIdentityName = $managedIdentityName
    deployedGenAI = $DeployGenAI.IsPresent
}

if ($DeployGenAI) {
    $context.openAIEndpoint = $outputs.openAIEndpoint.value
    $context.openAIModelName = $outputs.openAIModelName.value
    $context.searchEndpoint = $outputs.searchEndpoint.value
}

$contextFile = Join-Path (Split-Path -Parent $scriptDir) ".deployment-context.json"
$context | ConvertTo-Json -Depth 10 | Out-File -FilePath $contextFile -Encoding UTF8

Write-Host "Deployment context saved to: $contextFile" -ForegroundColor Green

# Summary
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Deployment Complete!                      " -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Resources deployed:" -ForegroundColor White
Write-Host "  Web App:        $webAppName" -ForegroundColor Gray
Write-Host "  SQL Server:     $sqlServerName" -ForegroundColor Gray
Write-Host "  Database:       $databaseName" -ForegroundColor Gray
Write-Host "  Managed ID:     $managedIdentityName" -ForegroundColor Gray
if ($DeployGenAI) {
    Write-Host "  OpenAI:         Deployed" -ForegroundColor Gray
    Write-Host "  AI Search:      Deployed" -ForegroundColor Gray
}
Write-Host ""
Write-Host "Application URL: https://$($outputs.webAppHostName.value)/Index" -ForegroundColor Green
Write-Host ""
Write-Host "Next step: Run .\deploy-app\deploy.ps1 to deploy the application code." -ForegroundColor Yellow
