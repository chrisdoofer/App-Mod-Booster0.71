# CI/CD Setup Guide - GitHub Actions with OIDC

This guide explains how to set up GitHub Actions CI/CD with OpenID Connect (OIDC) federation for the Expense Management application.

## Overview

The workflow uses **OIDC authentication** (no stored secrets) to deploy infrastructure and application code to Azure automatically when code is pushed to the `main` branch.

## One-Time Setup Steps

These steps are performed **once** to configure the Azure Service Principal and GitHub repository.

### 1. Prerequisites

- **Azure CLI** - [Install Azure CLI](https://aka.ms/azure-cli)
- **Azure subscription** - With Owner or User Access Administrator role
- **GitHub repository** - Admin access to configure variables

### 2. Login to Azure

```powershell
az login
```

### 3. Set Variables

```powershell
$subscriptionId = "YOUR_SUBSCRIPTION_ID"
$resourceGroup = "rg-github-oidc"
$appName = "expense-mgmt-cicd"
$githubOrg = "YOUR_GITHUB_ORG"       # e.g., "chrisdoofer"
$githubRepo = "YOUR_GITHUB_REPO"     # e.g., "App-Mod-Booster0.71"

# Set the subscription
az account set --subscription $subscriptionId
```

### 4. Create Azure Service Principal

```powershell
# Create App Registration
$app = az ad app create --display-name $appName --output json | ConvertFrom-Json
$appId = $app.appId

Write-Host "Application (Client) ID: $appId" -ForegroundColor Cyan

# Create Service Principal
$sp = az ad sp create --id $appId --output json | ConvertFrom-Json
$spObjectId = $sp.id

Write-Host "Service Principal Object ID: $spObjectId" -ForegroundColor Cyan
```

### 5. Assign Azure Roles

The Service Principal needs **two roles** at the subscription level:

```powershell
# Assign Contributor role (create/manage resources)
az role assignment create `
    --assignee $appId `
    --role "Contributor" `
    --scope "/subscriptions/$subscriptionId"

Write-Host "✓ Contributor role assigned" -ForegroundColor Green

# Assign User Access Administrator role (create role assignments in Bicep)
az role assignment create `
    --assignee $appId `
    --role "User Access Administrator" `
    --scope "/subscriptions/$subscriptionId"

Write-Host "✓ User Access Administrator role assigned" -ForegroundColor Green
```

**Why Two Roles?**

| Role | Purpose |
|------|---------|
| **Contributor** | Create and manage Azure resources (App Service, SQL, OpenAI, etc.) |
| **User Access Administrator** | Create role assignments when Bicep assigns Managed Identity access to resources like Azure OpenAI |

Without User Access Administrator, deployments with role assignments in Bicep will fail with:
> "The client does not have permission to perform action 'Microsoft.Authorization/roleAssignments/write'"

### 6. Create Federated Credentials

Create federated credentials for the `main` branch:

```powershell
# For main branch deployments
# Write credential JSON to a temp file (never use bash <<EOF or @- syntax in PowerShell)
$mainBranchCred = @{
    name      = "github-main-branch"
    issuer    = "https://token.actions.githubusercontent.com"
    subject   = "repo:$githubOrg/${githubRepo}:ref:refs/heads/main"
    audiences = @("api://AzureADTokenExchange")
} | ConvertTo-Json -Depth 5

$tempCredFile = [System.IO.Path]::GetTempFileName() + ".json"
$mainBranchCred | Out-File -FilePath $tempCredFile -Encoding UTF8

az ad app federated-credential create `
    --id $appId `
    --parameters "@$tempCredFile"

Remove-Item -Path $tempCredFile -Force -ErrorAction SilentlyContinue
Write-Host "✓ Federated credential created for main branch" -ForegroundColor Green
```

**Optional:** Create federated credential for pull requests (for testing):

```powershell
# For pull request deployments (optional)
$prCred = @{
    name      = "github-pull-requests"
    issuer    = "https://token.actions.githubusercontent.com"
    subject   = "repo:$githubOrg/${githubRepo}:pull_request"
    audiences = @("api://AzureADTokenExchange")
} | ConvertTo-Json -Depth 5

$tempCredFile = [System.IO.Path]::GetTempFileName() + ".json"
$prCred | Out-File -FilePath $tempCredFile -Encoding UTF8

az ad app federated-credential create `
    --id $appId `
    --parameters "@$tempCredFile"

Remove-Item -Path $tempCredFile -Force -ErrorAction SilentlyContinue
Write-Host "✓ Federated credential created for pull requests" -ForegroundColor Green
```

### 7. Get Tenant ID

```powershell
$tenant = az account show --query tenantId --output tsv
Write-Host "Tenant ID: $tenant" -ForegroundColor Cyan
```

### 8. Configure GitHub Repository Variables

Go to your GitHub repository:

1. Navigate to **Settings** → **Secrets and variables** → **Actions** → **Variables**
2. Add the following **repository variables**:

| Variable Name | Value | Description |
|---------------|-------|-------------|
| `AZURE_CLIENT_ID` | `$appId` from step 4 | Service Principal Application ID |
| `AZURE_TENANT_ID` | `$tenant` from step 7 | Azure AD Tenant ID |
| `AZURE_SUBSCRIPTION_ID` | `$subscriptionId` | Azure Subscription ID |

**Note:** These are **variables**, not secrets. OIDC doesn't use secrets.

### 9. Create GitHub Environment

1. Go to **Settings** → **Environments**
2. Click **New environment**
3. Name it `production`
4. **Optional:** Add protection rules (require approvals, restrict branches)

## Workflow Configuration

The workflow file is located at `.github/workflows/deploy.yml` and is already configured.

### Trigger Conditions

The workflow triggers on:

- **Push to `main` branch** - Automatic deployment
- **Manual trigger** - Via "Actions" tab with optional GenAI deployment

### Workflow Jobs

1. **deploy-infrastructure** - Deploys Azure resources via `deploy-infra/deploy.ps1`
   - Uses OIDC authentication
   - Installs go-sqlcmd for database operations
   - Creates `.deployment-context.json`
   - Uploads context as artifact

2. **deploy-application** - Deploys .NET application via `deploy-app/deploy.ps1`
   - Downloads deployment context artifact
   - Builds and publishes .NET 8 application
   - Deploys to Azure App Service

### Manual Workflow Dispatch

To manually trigger a deployment:

1. Go to **Actions** tab in GitHub
2. Select **Deploy to Azure** workflow
3. Click **Run workflow**
4. Choose branch (main)
5. **Optional:** Check "Deploy GenAI resources" checkbox
6. Click **Run workflow**

## Authentication Differences

The deployment scripts automatically detect CI/CD mode and adjust authentication:

| Aspect | Local (Interactive) | CI/CD (GitHub Actions) |
|--------|-------------------|----------------------|
| **Detection** | `$env:GITHUB_ACTIONS -ne "true"` | `$env:GITHUB_ACTIONS -eq "true"` |
| **Get user info** | `az ad signed-in-user show` | `az ad sp show --id $env:AZURE_CLIENT_ID` |
| **Admin principal type** | `User` | `Application` |
| **SQL admin type** | User (UPN) | Service Principal |
| **sqlcmd auth** | `ActiveDirectoryDefault` | `ActiveDirectoryAzCli` |

The scripts handle these differences automatically - no code changes needed.

## Resource Naming

The workflow uses `github.run_number` for unique resource group names:

```yaml
env:
  RESOURCE_GROUP: rg-expensemgmt-${{ github.run_number }}
```

This ensures each deployment creates a fresh resource group, avoiding ARM caching issues.

## Monitoring Deployments

### View Workflow Runs

1. Go to **Actions** tab in GitHub
2. Click on a workflow run to see details
3. Expand job steps to see logs

### View Azure Resources

```powershell
# List resource groups
az group list --query "[?starts_with(name, 'rg-expensemgmt')].name" --output table

# List resources in a group
az resource list --resource-group "rg-expensemgmt-123" --output table
```

### View App Service Logs

```powershell
az webapp log tail --resource-group "rg-expensemgmt-123" --name "app-expensemgmt-abc123"
```

## Troubleshooting

### "Azure CLI is not logged in"

The workflow uses OIDC, not `az login`. Check:

1. GitHub variables are set correctly (AZURE_CLIENT_ID, AZURE_TENANT_ID, AZURE_SUBSCRIPTION_ID)
2. Federated credentials are created correctly
3. Service Principal has required roles

### "The client does not have permission to perform action"

The Service Principal needs **two roles**:

1. **Contributor** - for resource creation
2. **User Access Administrator** - for role assignments in Bicep

Re-run step 5 to assign both roles.

### "Infrastructure deployment failed"

Check Azure Portal deployment history:

```powershell
az deployment group list --resource-group "rg-expensemgmt-123" --output table
```

Look for failed deployments and check error messages.

### "Application deployment failed"

1. Check App Service is running:
   ```powershell
   az webapp show --resource-group "rg-expensemgmt-123" --name "app-expensemgmt-abc123" --query state
   ```

2. Check App Service logs:
   ```powershell
   az webapp log tail --resource-group "rg-expensemgmt-123" --name "app-expensemgmt-abc123"
   ```

## Security Best Practices

1. **Use OIDC** - No stored secrets in GitHub
2. **Least privilege** - Service Principal has only required roles
3. **Environment protection** - Require approvals for production deployments
4. **Branch protection** - Require PR reviews before merging to main
5. **Regular rotation** - Rotate federated credentials periodically (recommended annually)

## Cleanup

To remove the Service Principal and federated credentials:

```powershell
# Delete App Registration (also deletes Service Principal and federated credentials)
az ad app delete --id $appId

# Remove role assignments (if needed)
az role assignment delete --assignee $appId --scope "/subscriptions/$subscriptionId"
```

## Related Documentation

- [Azure OIDC with GitHub Actions](https://learn.microsoft.com/en-us/azure/developer/github/connect-from-azure)
- [GitHub Actions Environments](https://docs.github.com/en/actions/deployment/targeting-different-environments/using-environments-for-deployment)
- [Infrastructure Deployment](../deploy-infra/README.md)
- [Application Deployment](../deploy-app/README.md)
