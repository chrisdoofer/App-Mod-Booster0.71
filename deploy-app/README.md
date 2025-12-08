# Application Deployment

This folder contains the deployment script for the Expense Management ASP.NET application.

## Prerequisites

- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) installed
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed
- Infrastructure already deployed (run `deploy-infra/deploy.ps1` first)

## Quick Start

After deploying infrastructure, simply run:

```powershell
.\deploy.ps1
```

The script automatically reads the deployment context file created by the infrastructure deployment.

## Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| ResourceGroup | No | From context | Override resource group |
| WebAppName | No | From context | Override web app name |
| SkipBuild | No | false | Skip the dotnet publish step |
| ConfigureSettings | No | false | Configure app settings after deployment |

## What the Script Does

1. Loads configuration from `.deployment-context.json`
2. Builds the .NET application using `dotnet publish`
3. Creates a deployment ZIP package
4. Deploys to Azure App Service using `az webapp deploy`
5. Cleans up temporary files
6. Displays the application URLs

## Application URLs

After deployment, the application is available at:

- **Main App**: `https://<webapp-name>.azurewebsites.net/Index`
- **API Docs**: `https://<webapp-name>.azurewebsites.net/swagger`
- **Chat**: `https://<webapp-name>.azurewebsites.net/Chat`

## Manual Deployment

If you prefer manual deployment:

```powershell
# Build
cd src/ExpenseManagement
dotnet publish -c Release -o ../../publish

# Create ZIP (files at root level)
cd ../../publish
Compress-Archive -Path * -DestinationPath ../deploy.zip

# Deploy
az webapp deploy --resource-group "your-rg" --name "your-webapp" --src-path ../deploy.zip --type zip --clean true --restart true
```

## Troubleshooting

### Application shows error connecting to database
Ensure the infrastructure deployment completed successfully and configured the connection string. Check App Service Configuration for `ConnectionStrings__DefaultConnection`.

### 500 Internal Server Error
Check the Application Insights logs or enable detailed errors in App Service to see the actual error message.

### Managed Identity errors
Ensure `AZURE_CLIENT_ID` is set in App Service configuration and the managed identity has database permissions.
