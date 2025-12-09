# Infrastructure Deployment

This folder contains the Bicep templates and deployment script for the Expense Management infrastructure.

## Prerequisites

- Azure CLI installed and logged in (`az login`)
- PowerShell 7+ recommended (PowerShell 5.1 works but 7+ is preferred)
- go-sqlcmd installed (`winget install sqlcmd` on Windows)

## Quick Start

Deploy all infrastructure with a single command:

```powershell
.\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251209" -Location "uksouth"
```

To include Azure OpenAI and AI Search for the chat feature:

```powershell
.\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251209" -Location "uksouth" -DeployGenAI
```

## Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| ResourceGroup | Yes | - | Name of the Azure resource group |
| Location | Yes | - | Azure region (e.g., 'uksouth', 'eastus') |
| BaseName | No | 'expensemgmt' | Base name for resources |
| DeployGenAI | No | $false | Include Azure OpenAI and AI Search |
| SkipDatabaseSetup | No | $false | Skip database schema import |

## What Gets Deployed

### Core Infrastructure
- **App Service Plan** (Standard S1) - Hosts the web application
- **App Service** - The web application with .NET 8
- **User-Assigned Managed Identity** - For secure authentication
- **Azure SQL Server** - Database server with Entra ID-only authentication
- **Azure SQL Database** - Northwind database (Basic tier)
- **Log Analytics Workspace** - Centralised logging
- **Application Insights** - Application monitoring

### Optional GenAI Resources (with -DeployGenAI)
- **Azure OpenAI** (Sweden Central) - GPT-4o model deployment
- **Azure AI Search** - For enhanced search capabilities

## Architecture

```
                    ┌─────────────────────┐
                    │   App Service       │
                    │   (.NET 8 App)      │
                    └──────────┬──────────┘
                               │
              ┌────────────────┼────────────────┐
              │                │                │
              ▼                ▼                ▼
    ┌─────────────────┐ ┌────────────┐ ┌─────────────────┐
    │ Azure SQL       │ │ App        │ │ Azure OpenAI    │
    │ (Northwind DB)  │ │ Insights   │ │ (Optional)      │
    └─────────────────┘ └────────────┘ └─────────────────┘
              ▲                │
              │                ▼
    ┌─────────────────┐ ┌────────────────────┐
    │ Managed         │ │ Log Analytics      │
    │ Identity        │ │ Workspace          │
    └─────────────────┘ └────────────────────┘
```

## Files

- `main.bicep` - Main orchestration template
- `main.bicepparam` - Parameter file
- `deploy.ps1` - Deployment automation script
- `modules/` - Individual Bicep modules:
  - `app-service.bicep` - App Service and Plan
  - `azure-sql.bicep` - SQL Server and Database
  - `managed-identity.bicep` - User-assigned managed identity
  - `monitoring.bicep` - Log Analytics and App Insights
  - `app-service-diagnostics.bicep` - App Service diagnostic settings
  - `sql-diagnostics.bicep` - SQL Database diagnostic settings
  - `genai.bicep` - Azure OpenAI and AI Search

## After Deployment

The script creates a `.deployment-context.json` file in the repository root. This file is used by the application deployment script (`deploy-app/deploy.ps1`) so you don't need to re-enter any values.

## Troubleshooting

### sqlcmd errors
If you get errors about unrecognized arguments, you may have the legacy ODBC sqlcmd instead of modern go-sqlcmd. Restart VS Code or run from a standalone PowerShell terminal.

### Resource group reuse
Always use a fresh resource group name. Reusing groups with partially deployed resources can cause ARM caching issues.

### Firewall issues
The script automatically adds your current IP to the SQL Server firewall. If you're behind a VPN or proxy, you may need to add your IP manually.
