# Application Deployment — Expense Management

This folder contains the script to deploy the Expense Management .NET 8 application to Azure App Service.

## Prerequisites

- [Azure CLI](https://aka.ms/azure-cli) — installed and logged in (`az login`)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8) — for building the application
- `.deployment-context.json` at the repository root — created automatically by `deploy-infra/deploy.ps1`

## Automated Deployment (Recommended)

After running `deploy-infra/deploy.ps1`, simply run:

```powershell
.\deploy-app\deploy.ps1
```

No parameters are required — the script reads all resource details from `.deployment-context.json`.

### What the Script Does

1. Loads resource details from `.deployment-context.json`
2. Validates Azure CLI login
3. Builds the .NET application with `dotnet publish -c Release`
4. Creates a deployment ZIP package (DLLs at root level, not in a subdirectory)
5. Deploys to Azure App Service using `az webapp deploy --type zip`
6. Cleans up the temporary package
7. Displays the application URL

### Application URLs

| URL | Purpose |
|-----|---------|
| `https://<app-name>.azurewebsites.net/Index` | **Main interface** (use this, not the root URL) |
| `https://<app-name>.azurewebsites.net/swagger` | Swagger API documentation |
| `https://<app-name>.azurewebsites.net/health` | Health check endpoint |

> **Important:** The main application interface is at `/Index`, not the root URL `/`.
> The root URL redirects to `/Index` automatically, but bookmarking `/Index` directly is recommended.

## Parameters

```powershell
.\deploy-app\deploy.ps1 [[-ResourceGroup] <string>] [[-WebAppName] <string>] [-SkipBuild] [-ConfigureSettings]
```

| Parameter | Required | Description |
|-----------|----------|-------------|
| `ResourceGroup` | No* | Azure resource group name. Read from context file if omitted. |
| `WebAppName` | No* | App Service name. Read from context file if omitted. |
| `SkipBuild` | No | Skip `dotnet publish` (use existing published output). |
| `ConfigureSettings` | No | Re-configure App Service settings from context file. |

*Required if `.deployment-context.json` does not exist.

## Examples

### Standard deployment (after infrastructure is deployed)

```powershell
.\deploy-app\deploy.ps1
```

### Deployment with explicit parameters

```powershell
.\deploy-app\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251206" -WebAppName "app-expensemgmt-abc123"
```

### Redeploy without rebuilding

```powershell
.\deploy-app\deploy.ps1 -SkipBuild
```

### Redeploy and reconfigure settings

```powershell
.\deploy-app\deploy.ps1 -ConfigureSettings
```

## Manual Deployment Steps

If you prefer manual deployment or need to troubleshoot:

### Step 1: Build the application

```powershell
dotnet publish src/ExpenseManagement/ExpenseManagement.csproj -c Release -o ./publish
```

### Step 2: Create a deployment ZIP

```powershell
Compress-Archive -Path "./publish/*" -DestinationPath "./app.zip" -Force
```

> **Critical:** Ensure DLL files are at the **root** of the ZIP, not inside a subdirectory.
> Azure App Service expects `ExpenseManagement.dll` at the root, not `publish/ExpenseManagement.dll`.

### Step 3: Deploy to App Service

```powershell
$resourceGroup = "rg-expensemgmt-20251206"
$webAppName    = "app-expensemgmt-abc123"

az webapp deploy `
    --resource-group $resourceGroup `
    --name $webAppName `
    --src-path ./app.zip `
    --type zip `
    --clean true `
    --restart true
```

### Step 4: Open the application

```powershell
$webAppName = "app-expensemgmt-abc123"
Start-Process "https://$webAppName.azurewebsites.net/Index"
```

## Troubleshooting

### App shows error on first load

App Service may still be warming up after deployment. Wait 60 seconds and refresh the page.

### "Azure CLI is not logged in"

Run `az login` and re-authenticate.

### Application cannot connect to database

Ensure `deploy-infra/deploy.ps1` completed successfully and App Service settings include:
- `AZURE_CLIENT_ID` — managed identity client ID
- `ConnectionStrings__DefaultConnection` — SQL connection string with Managed Identity auth
- `APPLICATIONINSIGHTS_CONNECTION_STRING` — Application Insights

Run with `-ConfigureSettings` to re-apply settings from the context file.

### Deployment fails with "RuntimeFailed"

1. Check App Service logs: `az webapp log tail --resource-group <rg> --name <app>`
2. Check the Azure portal → App Service → Deployment Center
3. Ensure the .NET 8 runtime is configured in App Service

### VS Code terminal PATH issues with sqlcmd

If sqlcmd errors occur, run the deployment from a standalone PowerShell terminal (not VS Code's integrated terminal).

## Related Documentation

- [Infrastructure Deployment](../deploy-infra/README.md)
- [CI/CD Setup Guide](../.github/CICD-SETUP.md)
- [Azure App Service Deployment](https://learn.microsoft.com/en-us/azure/app-service/deploy-zip)
