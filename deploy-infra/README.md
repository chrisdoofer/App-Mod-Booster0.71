# Infrastructure Deployment

This directory contains the infrastructure-as-code for the Expense Management application using Azure Bicep templates.

## Prerequisites

- [Azure CLI](https://aka.ms/azure-cli) installed
- [go-sqlcmd](https://github.com/microsoft/go-sqlcmd) installed (`winget install sqlcmd` on Windows)
- PowerShell 7+ recommended (works with 5.1 but 7+ is better)
- Azure subscription with appropriate permissions

## Quick Start

1. **Login to Azure:**
   ```powershell
   az login
   ```

2. **Deploy infrastructure:**
   ```powershell
   .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth"
   ```

3. **Deploy with GenAI resources:**
   ```powershell
   .\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth" -DeployGenAI
   ```

## What Gets Deployed

### Base Infrastructure
- **App Service Plan** (Standard S1 tier)
- **Web App** (Linux, .NET 8)
- **User-Assigned Managed Identity**
- **Azure SQL Server** (with Entra ID authentication only)
- **Azure SQL Database** (Basic tier, Northwind database)
- **Log Analytics Workspace**
- **Application Insights**
- **Diagnostic Settings** for monitoring

### Optional GenAI Resources (with -DeployGenAI)
- **Azure OpenAI** (Sweden Central region, GPT-4o model)
- **Azure AI Search** (Basic tier)
- Role assignments for managed identity access

## Architecture

The deployment follows security and Azure best practices:

- **No SQL passwords**: Uses Entra ID (Azure AD) authentication only
- **Managed Identity**: App Service uses user-assigned managed identity for all Azure service connections
- **No secrets in code**: All credentials managed by Azure
- **Centralized logging**: All resources send logs to Log Analytics

## Bicep Modules

- `main.bicep` - Main orchestration template
- `modules/managed-identity.bicep` - User-assigned managed identity
- `modules/app-service.bicep` - App Service Plan and Web App
- `modules/azure-sql.bicep` - SQL Server and Database with Entra ID auth
- `modules/monitoring.bicep` - Log Analytics and Application Insights
- `modules/app-service-diagnostics.bicep` - Diagnostic settings (deployed after App Service)
- `modules/genai.bicep` - Azure OpenAI and AI Search (optional)

## Database Setup

The deployment script automatically:

1. Imports the database schema from `Database-Schema/database_schema.sql`
2. Creates a database user for the managed identity using SID-based authentication
3. Grants read, write, and execute permissions
4. Imports stored procedures from `stored-procedures.sql` (if exists)

## App Service Configuration

The script configures critical App Service settings:

- `ConnectionStrings__DefaultConnection` - SQL connection string with Managed Identity auth
- `AZURE_CLIENT_ID` - Managed identity client ID for DefaultAzureCredential
- `ManagedIdentityClientId` - For explicit ManagedIdentityCredential usage
- `APPLICATIONINSIGHTS_CONNECTION_STRING` - Application Insights telemetry
- GenAI settings (if -DeployGenAI used)

## Deployment Context File

After successful deployment, a `.deployment-context.json` file is created at the repository root. This file contains all the resource names and configuration needed for application deployment, enabling:

```powershell
.\deploy-app\deploy.ps1  # No parameters needed!
```

## Redeployment

To redeploy without database setup:

```powershell
.\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth" -SkipDatabaseSetup
```

## Troubleshooting

### sqlcmd not found or wrong version

Make sure you have go-sqlcmd installed, not the legacy ODBC version:
```powershell
winget install sqlcmd
```

If running from VS Code, restart VS Code to refresh the PATH.

### Resource group reuse issues

Always use fresh resource group names with date/time suffix. ARM caching can cause issues when reusing partially deployed resource groups.

### Bicep validation

Validate templates before deployment:
```powershell
az deployment group validate `
    --resource-group $ResourceGroup `
    --template-file ./main.bicep `
    --parameters location=$Location
```

## CI/CD Support

This script supports both local interactive deployment and CI/CD via GitHub Actions. See `.github/workflows/deploy.yml` and `.github/CICD-SETUP.md` for CI/CD configuration.

## Next Steps

After infrastructure deployment completes, deploy the application code:

```powershell
.\deploy-app\deploy.ps1
```

Or use the unified deployment script to do both:

```powershell
.\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth"
```
