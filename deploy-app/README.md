# Application Deployment Guide

This directory contains the deployment automation for the Expense Management application code.

## Quick Start

After infrastructure deployment, deploy the application with:

```powershell
.\deploy-app\deploy.ps1
```

That's it! The script automatically reads the deployment context from the infrastructure deployment.

## Prerequisites

### Required Software

- **Azure CLI**: [Download](https://aka.ms/installazurecliwindows)
- **.NET 8 SDK**: [Download](https://dot.net)
- **PowerShell 7+**: [Download](https://aka.ms/powershell-release)

### Azure Login

```powershell
az login
az account set --subscription "Your Subscription Name"
```

### Infrastructure Must Be Deployed First

The application deployment requires infrastructure to exist. Run this first:

```powershell
.\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251207" -Location "uksouth"
```

## Deployment Script

### Parameters

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `-ResourceGroup` | No | From context | Azure resource group name |
| `-WebAppName` | No | From context | Web app name |
| `-SkipBuild` | No | false | Skip dotnet build/publish step |

### Automatic Context Loading

The script looks for `.deployment-context.json` at the repository root. This file is created by `deploy-infra/deploy.ps1` and contains:

```json
{
  "resourceGroup": "rg-expensemgmt-20251207",
  "webAppName": "app-expensemgmt-abc123",
  "sqlServerFqdn": "sql-expensemgmt-abc123.database.windows.net",
  "managedIdentityClientId": "guid...",
  ...
}
```

With this context file, you don't need to specify any parameters!

### What the Script Does

1. **Loads Deployment Context**
   - Reads `.deployment-context.json` if available
   - Extracts resource group and web app name

2. **Validates Prerequisites**
   - Checks Azure CLI is installed
   - Verifies you're logged in to Azure

3. **Builds Application**
   - Runs `dotnet publish` with Release configuration
   - Publishes to `src/ExpenseManagement/publish/`

4. **Creates Deployment Package**
   - Creates a zip file with DLL files at the root level
   - **Important**: Files must be at zip root, not in subdirectory

5. **Deploys to Azure**
   - Uses `az webapp deploy` with the zip package
   - Cleans previous deployment
   - Restarts the app

6. **Displays URLs**
   - Main application endpoint
   - Swagger API documentation
   - AI Chat interface (if GenAI deployed)

### Example Usage

**Standard deployment (with automatic context):**
```powershell
.\deploy-app\deploy.ps1
```

**Override resource group and app name:**
```powershell
.\deploy-app\deploy.ps1 `
  -ResourceGroup "rg-expensemgmt-20251207" `
  -WebAppName "app-expensemgmt-abc123"
```

**Skip build (for redeployments):**
```powershell
.\deploy-app\deploy.ps1 -SkipBuild
```

## Manual Deployment

If you prefer to deploy manually:

### 1. Build the Application

```powershell
cd src/ExpenseManagement
dotnet publish -c Release -o publish
```

### 2. Create Deployment Package

**Important**: Zip must have DLL files at root level.

```powershell
cd publish
Compress-Archive -Path * -DestinationPath ../../app-deployment.zip -Force
cd ../..
```

### 3. Deploy to Azure

```powershell
az webapp deploy `
  --resource-group "rg-expensemgmt-20251207" `
  --name "app-expensemgmt-abc123" `
  --src-path app-deployment.zip `
  --type zip `
  --clean true `
  --restart true
```

## Application Structure

The deployed application contains:

```
ExpenseManagement.dll           # Main application assembly
ExpenseManagement.deps.json     # Dependency manifest
ExpenseManagement.runtimeconfig.json
appsettings.json               # Base configuration
appsettings.Production.json    # Production overrides
wwwroot/                       # Static files (CSS, JS)
```

## Configuration

### Connection String

Set by infrastructure deployment in App Service configuration:

```
ConnectionStrings__DefaultConnection = "Server=tcp:...;Authentication=Active Directory Managed Identity;User Id={clientId};"
```

### Environment Variables

Set by infrastructure deployment:
- `AZURE_CLIENT_ID`: Managed Identity client ID
- `APPLICATIONINSIGHTS_CONNECTION_STRING`: Application Insights
- `GenAISettings__OpenAIEndpoint`: Azure OpenAI endpoint (if deployed)
- `GenAISettings__OpenAIModelName`: Model deployment name (if deployed)

### Local Development

For local development, create `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:your-server.database.windows.net,1433;Initial Catalog=Northwind;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default;"
  }
}
```

Note: Use `Active Directory Default` (not `Managed Identity`) for local development.

## Deployment Package Requirements

### Critical: Zip File Structure

Azure App Service expects DLL files at the **root** of the zip, not in a subdirectory.

❌ **Incorrect** (won't work):
```
app-deployment.zip
└── publish/
    ├── ExpenseManagement.dll
    ├── appsettings.json
    └── ...
```

✅ **Correct**:
```
app-deployment.zip
├── ExpenseManagement.dll
├── appsettings.json
└── ...
```

The deployment script handles this correctly by:
```powershell
Set-Location $publishPath  # Enter publish directory
Compress-Archive -Path "*" -DestinationPath $zipPath -Force  # Zip from current dir
```

## Accessing the Application

After deployment, access:

| Endpoint | URL |
|----------|-----|
| **Main App** | `https://{app-name}.azurewebsites.net/Index` |
| **API Documentation** | `https://{app-name}.azurewebsites.net/swagger` |
| **AI Chat** | `https://{app-name}.azurewebsites.net/Chat` |

**Note**: The application may take 30-60 seconds to start after deployment.

## Verifying Deployment

### 1. Check Application Health

```powershell
$webAppName = "app-expensemgmt-abc123"
curl "https://$webAppName.azurewebsites.net/Index"
```

### 2. Check Logs

View logs in Azure Portal:
- Navigate to App Service → Monitoring → Log stream
- Or use Application Insights → Live Metrics

### 3. Test API

```powershell
curl "https://$webAppName.azurewebsites.net/api/expenses"
```

## Troubleshooting

### Common Issues

#### 1. "Application Error" when accessing the site

**Symptoms**: HTTP 500 errors, blank page

**Solutions**:
1. Wait 30-60 seconds for the app to fully start
2. Check Application Insights for errors:
   - Azure Portal → App Insights → Failures
3. Verify connection string is set:
   ```powershell
   az webapp config connection-string list --resource-group "rg-..." --name "app-..."
   ```

#### 2. Database connection failures

**Symptoms**: "Unable to connect to database" errors

**Solutions**:
- Ensure `AZURE_CLIENT_ID` is set in App Service configuration
- Verify managed identity has database permissions
- Check connection string format includes `User Id={clientId}`

#### 3. Build failures

**Symptoms**: `dotnet publish` fails

**Solutions**:
- Ensure .NET 8 SDK is installed: `dotnet --version`
- Clear nuget cache: `dotnet nuget locals all --clear`
- Restore packages: `dotnet restore`

#### 4. Deployment hangs or times out

**Symptoms**: `az webapp deploy` never completes

**Solutions**:
- Check App Service isn't in a stopped state
- Restart the App Service:
  ```powershell
  az webapp restart --resource-group "rg-..." --name "app-..."
  ```
- Try deployment again

#### 5. Old application version still running

**Symptoms**: Changes don't appear after deployment

**Solutions**:
The deployment script uses `--clean true` which should clear old files. If issues persist:
```powershell
# Stop the app
az webapp stop --resource-group "rg-..." --name "app-..."

# Deploy
.\deploy-app\deploy.ps1

# Start the app
az webapp start --resource-group "rg-..." --name "app-..."
```

## CI/CD Integration

The deployment script works in CI/CD pipelines:

```yaml
- name: Deploy Application
  shell: pwsh
  run: |
    ./deploy-app/deploy.ps1
```

The script automatically detects CI/CD environment and adjusts behavior.

See [.github/workflows/deploy.yml](../.github/workflows/deploy.yml) for the complete workflow.

## Redeployment

To redeploy after code changes:

```powershell
# Quick redeployment
.\deploy-app\deploy.ps1

# Or just run from repo root
.\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251207" -Location "uksouth"
```

## Performance Tips

### First Deployment
- Takes 2-3 minutes (build + deploy)
- App startup: 30-60 seconds

### Subsequent Deployments
- Use `-SkipBuild` if code hasn't changed
- Deployment only: ~1 minute

### Always On
The App Service is configured with "Always On" to prevent cold starts in production.

## Cleanup

Application deployment doesn't create any new resources. To clean up, delete the resource group:

```powershell
az group delete --name "rg-expensemgmt-20251207" --yes --no-wait
```

## Additional Resources

- [Main README](../README.md)
- [Architecture Documentation](../ARCHITECTURE.md)
- [Infrastructure Deployment Guide](../deploy-infra/README.md)
- [CI/CD Setup Guide](../.github/CICD-SETUP.md)
