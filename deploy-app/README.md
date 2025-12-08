# Application Deployment

This folder contains the deployment script for building and deploying the .NET application code to Azure App Service.

## Quick Start

### Prerequisites

- [Azure CLI](https://docs.microsoft.com/cli/azure/install-azure-cli) installed
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed
- Infrastructure already deployed (run `.\deploy-infra\deploy.ps1` first)

### Automatic Deployment (Recommended)

After running the infrastructure deployment, simply run:

```powershell
.\deploy-app\deploy.ps1
```

The script automatically reads the `.deployment-context.json` file created by the infrastructure deployment, so you don't need to specify any parameters.

### Manual Deployment

If you need to specify parameters explicitly:

```powershell
.\deploy-app\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251208" -WebAppName "app-expensemgmt-abc123"
```

## What the Script Does

1. ✅ Reads deployment context from `.deployment-context.json`
2. ✅ Validates Azure CLI installation and login
3. ✅ Builds the .NET application (`dotnet publish`)
4. ✅ Creates a deployment ZIP package
5. ✅ Deploys to Azure App Service with clean restart
6. ✅ Displays application URLs

## Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `ResourceGroup` | No* | From context | Azure resource group name |
| `WebAppName` | No* | From context | Azure App Service name |
| `SkipBuild` | No | false | Skip build step and use existing published files |
| `ConfigureSettings` | No | false | Configure app settings after deployment |

*Not required if `.deployment-context.json` exists

## Deployment Package Structure

The script creates a ZIP file with the compiled application at the root level (not in a subdirectory):

```
app-deployment.zip
├── ExpenseManagement.dll
├── appsettings.json
├── web.config
└── ...other DLLs and assets
```

This structure is critical for Azure App Service to correctly identify the application entry point.

## Application URLs

After deployment, the application is accessible at:

- **Main Application**: `https://<app-name>.azurewebsites.net/Index`
- **Swagger API**: `https://<app-name>.azurewebsites.net/swagger`
- **AI Chat**: `https://<app-name>.azurewebsites.net/Chat` (if GenAI deployed)

⚠️ **Note**: The root URL (`/`) redirects to the Index page. Always use `/Index` for the main interface.

## Connection to Infrastructure

The application uses these settings configured during infrastructure deployment:

### Required App Settings

- `AZURE_CLIENT_ID`: Managed identity client ID
- `ManagedIdentityClientId`: Managed identity client ID (for OpenAI)
- `ConnectionStrings__DefaultConnection`: SQL connection string with Managed Identity auth
- `APPLICATIONINSIGHTS_CONNECTION_STRING`: Application Insights connection string

### Optional GenAI Settings

If deployed with `-DeployGenAI`:

- `GenAISettings__OpenAIEndpoint`: Azure OpenAI endpoint
- `GenAISettings__OpenAIModelName`: Deployed model name
- `GenAISettings__SearchEndpoint`: Azure AI Search endpoint

## Local Development

For local development, you can run the application locally against Azure resources:

1. **Update `appsettings.Development.json`**:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:your-sql-server.database.windows.net,1433;Initial Catalog=Northwind;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default;"
  }
}
```

2. **Login to Azure CLI**:

```powershell
az login
```

3. **Run the application**:

```powershell
cd src/ExpenseManagement
dotnet run
```

The application will use your Azure CLI credentials to authenticate to SQL Server.

## Path Handling

The script uses `$PSScriptRoot` to build paths relative to the script location. This ensures it works correctly whether called:

- From the repository root (by `deploy-all.ps1`)
- From within the `deploy-app` folder directly

## Build Configuration

The application is built with:

- **Configuration**: Release
- **Framework**: .NET 8
- **Runtime**: Portable (no self-contained)
- **Output**: `./publish` directory

## Deployment Method

Uses `az webapp deploy` with:

- `--type zip`: ZIP deployment
- `--clean true`: Clean deployment (removes existing files)
- `--restart true`: Restart app after deployment

This ensures a clean deployment without leftover files from previous versions.

## Redeployment

To redeploy the application code without rebuilding:

```powershell
.\deploy-app\deploy.ps1 -SkipBuild
```

This is useful for quick redeployments during development.

## Troubleshooting

### "Project file not found"

**Solution:** Ensure you're running from the repository root or that the relative path to the project is correct.

### "Deployment failed"

**Solution:** Check Azure Portal → App Service → Deployment Center for detailed logs.

### Application shows errors after deployment

**Solution:** Check Application Insights or App Service logs:

```powershell
# Stream logs
az webapp log tail --resource-group <rg> --name <app-name>
```

### Connection string errors

**Solution:** Verify the infrastructure deployment configured the App Service settings correctly:

```powershell
az webapp config connection-string list --resource-group <rg> --name <app-name>
az webapp config appsettings list --resource-group <rg> --name <app-name>
```

## Integration with CI/CD

This script is used by the GitHub Actions workflow (`.github/workflows/deploy.yml`). In CI/CD:

1. Infrastructure job deploys resources and uploads `.deployment-context.json` as artifact
2. Application job downloads the artifact and runs this script
3. A 60-second wait is added between jobs for App Service settings to propagate

## Application Architecture

The deployed application includes:

- **Razor Pages**: Index, AddExpense, Chat
- **API Controllers**: Expenses, Categories, Users, Chat endpoints
- **Services**: ExpenseService (data access), ChatService (OpenAI integration)
- **Models**: Data models matching database schema

All database operations use stored procedures (defined in `stored-procedures.sql`).

## Monitoring

After deployment, monitor the application via:

- **Application Insights**: Performance, errors, requests
- **App Service Logs**: Application logs, HTTP logs
- **Log Analytics**: Centralized query across all resources

## Next Steps

After application deployment:

1. Visit the application at the URLs displayed by the script
2. Test the main functionality (view expenses, add expense)
3. Test the API endpoints via Swagger
4. If GenAI is deployed, try the Chat feature

## Resources

- [Azure App Service Deployment Best Practices](https://learn.microsoft.com/azure/app-service/deploy-best-practices)
- [.NET on Azure App Service](https://learn.microsoft.com/azure/app-service/quickstart-dotnetcore)
- [Monitor ASP.NET Core with Application Insights](https://learn.microsoft.com/azure/azure-monitor/app/asp-net-core)
