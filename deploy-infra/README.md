# Infrastructure Deployment

This folder contains the Bicep templates and deployment script for provisioning Azure resources.

## What Gets Deployed

### Core Resources (Always Deployed)

- **Managed Identity**: User-assigned managed identity for secure authentication
- **App Service**: Standard S1 tier (Linux, .NET 8) for hosting the web application
- **Azure SQL Database**: Basic tier with Entra ID-only authentication
- **Log Analytics Workspace**: Centralized logging and diagnostics
- **Application Insights**: Application performance monitoring

### Optional GenAI Resources

When you use the `-DeployGenAI` switch:

- **Azure OpenAI**: GPT-4o model in Sweden Central region
- **Azure AI Search**: Basic tier for enhanced search capabilities

## Quick Start

### Prerequisites

- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli) installed
- [go-sqlcmd](https://github.com/microsoft/go-sqlcmd) installed (`winget install sqlcmd` on Windows)
- PowerShell 7+ recommended (PowerShell 5.1 works but with warnings)
- Azure subscription with appropriate permissions

### Basic Deployment

```powershell
# Login to Azure
az login

# Deploy infrastructure (without GenAI)
.\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth"
```

### Deployment with GenAI

```powershell
# Deploy infrastructure including Azure OpenAI and AI Search
.\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -Location "uksouth" -DeployGenAI
```

### Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `ResourceGroup` | Yes | - | Name of the Azure resource group (use unique name with date) |
| `Location` | Yes | - | Azure region (e.g., 'uksouth', 'eastus') |
| `BaseName` | No | 'expensemgmt' | Base name for all resources |
| `DeployGenAI` | No | false | Deploy Azure OpenAI and AI Search |
| `SkipDatabaseSetup` | No | false | Skip database schema and stored procedures (for redeployments) |

## What the Script Does

1. ✅ Validates Azure CLI installation and login
2. ✅ Retrieves current user/service principal credentials
3. ✅ Creates resource group if needed
4. ✅ Deploys Bicep templates
5. ✅ Waits for SQL Server to be ready
6. ✅ Adds your IP to SQL firewall
7. ✅ Imports database schema
8. ✅ Creates database user for managed identity with SID-based approach
9. ✅ Grants database permissions (read, write, execute)
10. ✅ Creates stored procedures
11. ✅ Configures App Service settings (connection string, managed identity)
12. ✅ Optionally configures GenAI settings
13. ✅ Saves deployment context to `.deployment-context.json`

## Architecture

The Bicep templates are organized as modules:

```
deploy-infra/
├── main.bicep                      # Orchestration template
├── main.bicepparam                 # Parameters file
├── modules/
│   ├── managed-identity.bicep      # User-assigned managed identity
│   ├── monitoring.bicep            # Log Analytics + App Insights
│   ├── app-service.bicep           # App Service + Plan
│   ├── azure-sql.bicep             # SQL Server + Database
│   ├── app-service-diagnostics.bicep  # Diagnostic settings
│   └── genai.bicep                 # Azure OpenAI + AI Search (conditional)
└── deploy.ps1                      # Deployment automation script
```

## Important Notes

### Resource Naming

- All resource names are lowercase (required by Azure services)
- Unique suffix is generated using `uniqueString(resourceGroup().id)`
- Azure OpenAI and AI Search names use `toLower()` to ensure compliance

### SQL Server Authentication

- Uses **Entra ID-only authentication** (no SQL passwords)
- In interactive mode: Uses your Azure CLI credentials
- In CI/CD mode: Uses Service Principal with `adminPrincipalType="Application"`

### Managed Identity Database Access

The script uses **SID-based user creation** instead of `FROM EXTERNAL PROVIDER`:

```sql
CREATE USER [identity-name] WITH SID = 0x<guid-as-hex>, TYPE = E;
```

This approach:
- ✅ Works without Directory Reader permissions
- ✅ Compatible with both interactive and CI/CD deployments
- ✅ No dependency on SQL Server's Entra ID integration

### CI/CD Support

The script automatically detects CI/CD environments and adjusts:

- Authentication method for sqlcmd (`ActiveDirectoryAzCli` instead of `ActiveDirectoryDefault`)
- Principal type for SQL admin (`Application` instead of `User`)
- Credentials source (Service Principal instead of signed-in user)

## Troubleshooting

### "sqlcmd: command not found"

**Solution:** Install go-sqlcmd:

```powershell
# Windows
winget install sqlcmd

# Linux/macOS
curl -sSL -o sqlcmd.tar.bz2 https://github.com/microsoft/go-sqlcmd/releases/download/v1.8.0/sqlcmd-linux-amd64.tar.bz2
sudo tar -xjf sqlcmd.tar.bz2 -C /usr/local/bin
```

### VS Code Terminal Issues

If sqlcmd fails with "unrecognized argument", the terminal may be using cached PATH with legacy ODBC sqlcmd:

**Solution:** Restart VS Code or run from a standalone PowerShell terminal

### "Could not retrieve the Log Analytics workspace"

**Solution:** Use a fresh resource group name. Never reuse resource groups with partial deployments.

### Firewall Connection Issues

The script adds your current IP automatically. If you're behind a corporate proxy or VPN:

**Solution:** Manually add your IP range in Azure Portal → SQL Server → Networking

## Next Steps

After infrastructure deployment:

1. The script creates `.deployment-context.json` in the repository root
2. Run `.\deploy-app\deploy.ps1` to deploy the application code
3. Or use `.\deploy-all.ps1` to do both steps automatically

## Azure Best Practices

This deployment follows Azure best practices:

✅ **Managed identities** instead of connection strings with passwords
✅ **Entra ID authentication** for all services
✅ **Least privilege access** with scoped role assignments
✅ **HTTPS only** for all web endpoints
✅ **TLS 1.2+** minimum for SQL connections
✅ **Centralized logging** with Log Analytics
✅ **Resource tagging** for cost management
✅ **Infrastructure as Code** with Bicep

## Resources

- [Azure App Service Best Practices](https://learn.microsoft.com/azure/app-service/app-service-best-practices)
- [Azure SQL Database Security](https://learn.microsoft.com/azure/azure-sql/database/security-best-practice)
- [Managed Identities Best Practices](https://learn.microsoft.com/azure/active-directory/managed-identities-azure-resources/managed-identity-best-practice-recommendations)
