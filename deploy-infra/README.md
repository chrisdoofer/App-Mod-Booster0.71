# Infrastructure Deployment

This folder contains the Bicep templates and deployment scripts for the Expense Management Azure infrastructure.

## Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- [go-sqlcmd](https://github.com/microsoft/go-sqlcmd) installed (`winget install sqlcmd` on Windows)
- PowerShell 7+ recommended (works with 5.1)
- Azure subscription with appropriate permissions

## Quick Start

1. Log in to Azure:
   ```powershell
   az login
   ```

2. Deploy infrastructure:
   ```powershell
   .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth"
   ```

3. Deploy with GenAI features:
   ```powershell
   .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth" -DeployGenAI
   ```

## Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| ResourceGroup | Yes | - | Name of the Azure resource group |
| Location | Yes | - | Azure region (e.g., 'uksouth', 'eastus') |
| BaseName | No | 'expensemgmt' | Base name for all resources |
| DeployGenAI | No | false | Include Azure OpenAI and AI Search |
| SkipDatabaseSetup | No | false | Skip database schema import (for redeployments) |

## What Gets Deployed

### Core Infrastructure
- **App Service Plan** (Standard S1) - Hosts the web application
- **Web App** - ASP.NET 8 application with managed identity
- **User-Assigned Managed Identity** - For secure service authentication
- **Azure SQL Server** - With Entra ID-only authentication
- **Azure SQL Database** (Northwind) - Application database
- **Log Analytics Workspace** - Centralized logging
- **Application Insights** - Application performance monitoring

### GenAI Resources (Optional)
When `-DeployGenAI` is specified:
- **Azure OpenAI** (Sweden Central) - GPT-4o model
- **Azure AI Search** - For RAG scenarios

## Deployment Context

The script saves a `.deployment-context.json` file at the repository root containing all resource names and configuration values. The application deployment script reads this file automatically.

## Manual Bicep Deployment

If you prefer to deploy manually:

```powershell
# Get your Azure AD user info
$user = az ad signed-in-user show --output json | ConvertFrom-Json

# Deploy
az deployment group create `
    --resource-group "your-resource-group" `
    --template-file ./main.bicep `
    --parameters location="uksouth" `
    --parameters adminObjectId=$($user.id) `
    --parameters adminUpn=$($user.userPrincipalName)
```

## Troubleshooting

### sqlcmd errors
If you get errors about unrecognized arguments, you may have the legacy ODBC sqlcmd instead of go-sqlcmd. Install with:
```powershell
winget install sqlcmd
```

### Resource group reuse issues
Always use fresh resource group names. If a deployment fails partway through, delete the resource group and retry.

### "Could not retrieve Log Analytics workspace" error
This occurs when reusing a resource group. Create a new resource group instead.
