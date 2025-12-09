# Application Deployment

This folder contains the deployment script for the Expense Management application.

## Prerequisites

- Azure CLI installed and logged in (`az login`)
- .NET 8 SDK installed
- Infrastructure already deployed (run `deploy-infra/deploy.ps1` first)

## Quick Start

After deploying infrastructure, simply run:

```powershell
.\deploy.ps1
```

The script automatically reads the `.deployment-context.json` file created by the infrastructure deployment.

## Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| ResourceGroup | No | From context | Azure resource group name |
| WebAppName | No | From context | App Service name |
| SkipBuild | No | $false | Skip the build step |
| ConfigureSettings | No | $false | Configure app settings after deployment |

## What the Script Does

1. Reads deployment context from `.deployment-context.json`
2. Builds the .NET application (`dotnet publish`)
3. Creates a deployment zip package
4. Deploys to Azure App Service using `az webapp deploy`
5. Cleans up temporary files
6. Displays the application URLs

## Manual Deployment

If you prefer to deploy manually:

```powershell
# Build
cd src/ExpenseManagement
dotnet publish -c Release -o ./bin/publish

# Create zip (from publish folder)
cd bin/publish
Compress-Archive -Path * -DestinationPath app.zip

# Deploy
az webapp deploy `
    --resource-group "your-rg" `
    --name "your-webapp" `
    --src-path app.zip `
    --type zip
```

## Application URLs

After deployment:

- **Main Application**: `https://<webapp-name>.azurewebsites.net/Index`
- **API Documentation**: `https://<webapp-name>.azurewebsites.net/swagger`
- **Chat Interface**: `https://<webapp-name>.azurewebsites.net/Chat`

## Troubleshooting

### Build Errors
Make sure you have .NET 8 SDK installed: `dotnet --version`

### Deployment Errors
Check the Azure portal's App Service deployment logs for detailed error messages.

### Missing Context File
If `.deployment-context.json` is missing, run `deploy-infra/deploy.ps1` first.
