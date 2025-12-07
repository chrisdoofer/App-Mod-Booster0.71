# Infrastructure Deployment Guide

This directory contains all infrastructure-as-code (IaC) templates and deployment automation for the Expense Management application.

## Quick Start

Deploy all infrastructure with a single command:

```powershell
.\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251207" -Location "uksouth"
```

With GenAI resources:

```powershell
.\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251207" -Location "uksouth" -DeployGenAI
```

## What Gets Deployed

### Core Infrastructure

- **Azure App Service** (Standard S1, Linux, .NET 8)
- **Azure SQL Database** (Basic tier, Northwind database)
- **User-Assigned Managed Identity** (for passwordless authentication)
- **Application Insights** (application monitoring)
- **Log Analytics Workspace** (centralized logging)

### Optional GenAI Resources (with `-DeployGenAI`)

- **Azure OpenAI** (GPT-4o model in Sweden Central)
- **Azure AI Search** (Basic tier for RAG)

## Prerequisites

### Required Software

- **Azure CLI**: [Download](https://aka.ms/installazurecliwindows)
- **PowerShell 7+**: [Download](https://aka.ms/powershell-release)
- **sqlcmd (go-sqlcmd)**: Install with `winget install sqlcmd`

### Azure Permissions

You need:
- Subscription Contributor role (to create resources)
- Permission to create Azure AD applications (for CI/CD setup)

### Login to Azure

```powershell
az login
az account set --subscription "Your Subscription Name"
```

## Deployment Script

### Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `-ResourceGroup` | Yes | - | Azure resource group name |
| `-Location` | Yes | - | Azure region (e.g., 'uksouth', 'eastus') |
| `-BaseName` | No | 'expensemgmt' | Base name for resources |
| `-DeployGenAI` | No | false | Deploy Azure OpenAI and AI Search |
| `-SkipDatabaseSetup` | No | false | Skip database schema and stored procedures |

### What the Script Does

1. **Validates Prerequisites**
   - Checks Azure CLI is installed
   - Verifies you're logged in to Azure
   - Retrieves your Azure AD credentials

2. **Deploys Bicep Templates**
   - Creates resource group if needed
   - Deploys all infrastructure resources
   - Waits for SQL Server to become ready

3. **Configures SQL Database**
   - Adds your IP to firewall (for local development)
   - Imports database schema from `Database-Schema/database_schema.sql`
   - Creates managed identity database user (SID-based, no Directory Reader required)
   - Grants db_datareader, db_datawriter, and execute permissions
   - Deploys stored procedures from `stored-procedures.sql`

4. **Configures App Service**
   - Sets connection string with Managed Identity authentication
   - Configures `AZURE_CLIENT_ID` environment variable
   - Configures Application Insights
   - Configures GenAI endpoints (if deployed)

5. **Saves Deployment Context**
   - Creates `.deployment-context.json` at repository root
   - Contains resource names and configuration
   - Used by application deployment script

### Example Usage

**Basic deployment:**
```powershell
.\deploy-infra\deploy.ps1 `
  -ResourceGroup "rg-expensemgmt-20251207" `
  -Location "uksouth"
```

**With GenAI resources:**
```powershell
.\deploy-infra\deploy.ps1 `
  -ResourceGroup "rg-expensemgmt-20251207" `
  -Location "uksouth" `
  -DeployGenAI
```

**Redeployment (skip database setup):**
```powershell
.\deploy-infra\deploy.ps1 `
  -ResourceGroup "rg-expensemgmt-20251207" `
  -Location "uksouth" `
  -SkipDatabaseSetup
```

## Bicep Templates

### Structure

```
deploy-infra/
├── main.bicep                    # Orchestration template
├── main.bicepparam              # Parameter file
├── deploy.ps1                   # Deployment script
└── modules/
    ├── managed-identity.bicep   # User-assigned identity
    ├── app-service.bicep        # App Service + plan
    ├── azure-sql.bicep          # SQL Server + database
    ├── monitoring.bicep         # App Insights + Log Analytics
    ├── app-service-diagnostics.bicep  # Diagnostic settings
    └── genai.bicep             # Azure OpenAI + AI Search
```

### Validating Templates

Before deploying, validate the Bicep templates:

```powershell
az deployment group validate `
  --resource-group "rg-test" `
  --template-file ./deploy-infra/main.bicep `
  --parameters location=uksouth baseName=expensemgmt adminObjectId="your-object-id" adminUsername="user@domain.com"
```

### Manual Deployment

If you prefer to deploy manually without the script:

```powershell
# 1. Create resource group
az group create --name "rg-expensemgmt-20251207" --location "uksouth"

# 2. Get your Azure AD credentials
$user = az ad signed-in-user show | ConvertFrom-Json

# 3. Deploy Bicep
az deployment group create `
  --resource-group "rg-expensemgmt-20251207" `
  --template-file ./deploy-infra/main.bicep `
  --parameters location=uksouth baseName=expensemgmt adminObjectId=$user.id adminUsername=$user.userPrincipalName deployGenAI=false
```

## Resource Naming Convention

Resources follow this naming pattern:

| Resource Type | Pattern | Example |
|--------------|---------|---------|
| App Service Plan | `asp-{baseName}` | `asp-expensemgmt` |
| App Service | `app-{baseName}-{unique}` | `app-expensemgmt-abc123` |
| SQL Server | `sql-{baseName}-{unique}` | `sql-expensemgmt-abc123` |
| Managed Identity | `mid-{baseName}-{timestamp}` | `mid-expensemgmt-202512071430` |
| Log Analytics | `log-{baseName}-{unique}` | `log-expensemgmt-abc123` |
| App Insights | `appi-{baseName}-{unique}` | `appi-expensemgmt-abc123` |
| Azure OpenAI | `oai-{baseName}-{unique}` | `oai-expensemgmt-abc123` |
| AI Search | `srch-{baseName}-{unique}` | `srch-expensemgmt-abc123` |

**Note**: `{unique}` is generated using `uniqueString(resourceGroup().id)` for deterministic uniqueness.

## Security Configuration

### SQL Database

- **Entra ID-only authentication** (SQL logins disabled)
- Firewall configured to allow Azure services
- Your IP automatically added for local development
- TLS 1.2+ required

### Managed Identity

- Created with unique timestamp
- Granted database-level permissions (not server-level)
- SID-based user creation (no Directory Reader required)
- Used by App Service for all Azure resource access

### Connection String

Format set in App Service configuration:
```
Server=tcp:{server}.database.windows.net,1433;
Initial Catalog=Northwind;
Encrypt=True;
TrustServerCertificate=False;
Connection Timeout=30;
Authentication=Active Directory Managed Identity;
User Id={managedIdentityClientId};
```

## CI/CD Support

The deployment script automatically detects CI/CD environments and adjusts:

**Local Development:**
- Uses `az ad signed-in-user show` for credentials
- Sets `adminPrincipalType = "User"`
- Uses `ActiveDirectoryDefault` for sqlcmd

**CI/CD (GitHub Actions):**
- Uses Service Principal credentials from OIDC
- Sets `adminPrincipalType = "Application"`
- Uses `ActiveDirectoryAzCli` for sqlcmd

See [.github/CICD-SETUP.md](../.github/CICD-SETUP.md) for CI/CD configuration.

## Troubleshooting

### Common Issues

#### 1. sqlcmd not found

**Symptom**: `sqlcmd: command not found`

**Solution**: Install go-sqlcmd:
```powershell
winget install sqlcmd
```

Restart your terminal or VS Code after installation.

#### 2. SQL connection failures

**Symptom**: "Cannot open server" or "Login failed"

**Solutions**:
- Ensure your IP is in the firewall (script does this automatically)
- Wait 30 seconds for SQL Server to become ready
- Verify you're logged in to Azure: `az account show`

#### 3. Managed identity user creation fails

**Symptom**: "CREATE USER failed" or "Permission denied"

**Solution**: The script uses SID-based user creation which doesn't require Directory Reader permissions. If it still fails, ensure you're the SQL Server administrator.

#### 4. Resource group already exists with different resources

**Symptom**: Deployment errors due to existing resources

**Solution**: Use a fresh resource group name. Always include date/timestamp:
```powershell
-ResourceGroup "rg-expensemgmt-20251207-1430"
```

#### 5. VS Code terminal PATH issues

**Symptom**: sqlcmd errors about unrecognized arguments

**Solution**: The integrated terminal may use cached PATH. Either:
- Restart VS Code completely
- Run from a standalone PowerShell terminal

## Next Steps

After infrastructure deployment:

1. **Deploy Application Code**:
   ```powershell
   .\deploy-app\deploy.ps1
   ```

2. **Access Your Application**:
   - Main App: `https://{app-name}.azurewebsites.net/Index`
   - API Docs: `https://{app-name}.azurewebsites.net/swagger`

3. **Monitor Your Application**:
   - View Application Insights in Azure Portal
   - Query logs in Log Analytics

## Cleanup

To delete all resources:

```powershell
az group delete --name "rg-expensemgmt-20251207" --yes --no-wait
```

## Additional Resources

- [Main README](../README.md)
- [Architecture Documentation](../ARCHITECTURE.md)
- [Application Deployment Guide](../deploy-app/README.md)
- [CI/CD Setup Guide](../.github/CICD-SETUP.md)
