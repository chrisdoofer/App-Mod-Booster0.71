# CI/CD Setup with GitHub Actions

This document describes how to set up automated deployments using GitHub Actions with OIDC authentication.

## Overview

The CI/CD pipeline uses:
- **GitHub Actions** for workflow automation
- **OIDC (OpenID Connect)** for secure, passwordless authentication to Azure
- **Service Principal** with federated credentials (no secrets needed)
- **Separate jobs** for infrastructure and application deployment

## Prerequisites

- Azure subscription with appropriate permissions
- GitHub repository with this code
- PowerShell 7+ for running setup commands

## One-Time Setup

### Step 1: Create Service Principal with OIDC

Run these PowerShell commands from your local machine:

```powershell
# Set variables
$subscriptionId = "your-subscription-id"
$resourceGroupName = "rg-cicd-sp"
$spName = "sp-expensemgmt-cicd"
$repoOwner = "YourGitHubUsername"
$repoName = "YourRepoName"

# Login to Azure
az login
az account set --subscription $subscriptionId

# Create resource group for the Service Principal (optional, for organization)
az group create --name $resourceGroupName --location "uksouth"

# Create Service Principal
$sp = az ad sp create-for-rbac `
    --name $spName `
    --role Contributor `
    --scopes "/subscriptions/$subscriptionId" `
    --sdk-auth `
    --output json | ConvertFrom-Json

# Save the Application (client) ID
$clientId = $sp.clientId
$tenantId = $sp.tenant

Write-Host "Service Principal created successfully!" -ForegroundColor Green
Write-Host "Application (Client) ID: $clientId" -ForegroundColor Yellow
Write-Host "Tenant ID: $tenantId" -ForegroundColor Yellow
```

### Step 2: Assign Additional Roles

The Service Principal needs two roles:

```powershell
# 1. Contributor - for creating/managing resources
az role assignment create `
    --assignee $clientId `
    --role "Contributor" `
    --scope "/subscriptions/$subscriptionId"

# 2. User Access Administrator - for creating role assignments in Bicep
az role assignment create `
    --assignee $clientId `
    --role "User Access Administrator" `
    --scope "/subscriptions/$subscriptionId"

Write-Host "Roles assigned successfully!" -ForegroundColor Green
```

**Why User Access Administrator?** The Bicep templates assign roles to the Managed Identity (e.g., access to Azure OpenAI). Without this role, the deployment will fail with:
> "The client does not have permission to perform action 'Microsoft.Authorization/roleAssignments/write'"

### Step 3: Create Federated Credentials

Create federated credentials for the GitHub repository:

```powershell
# Get Service Principal Object ID
$spObjectId = az ad sp show --id $clientId --query id --output tsv

# Create federated credential for 'main' branch
az ad app federated-credential create `
    --id $clientId `
    --parameters @"
{
    \"name\": \"github-main-branch\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:$repoOwner/${repoName}:ref:refs/heads/main\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
}
"@

# Create federated credential for pull requests
az ad app federated-credential create `
    --id $clientId `
    --parameters @"
{
    \"name\": \"github-pull-requests\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:$repoOwner/${repoName}:pull_request\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
}
"@

# Create federated credential for workflow dispatch (any branch)
az ad app federated-credential create `
    --id $clientId `
    --parameters @"
{
    \"name\": \"github-workflow-dispatch\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:$repoOwner/${repoName}:environment:production\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
}
"@

Write-Host "Federated credentials created successfully!" -ForegroundColor Green
```

### Step 4: Configure GitHub Repository

#### Create GitHub Variables

Go to your GitHub repository → Settings → Secrets and variables → Actions → Variables tab

Create these **Variables** (not secrets):

| Name | Value | Example |
|------|-------|---------|
| `AZURE_CLIENT_ID` | Application (Client) ID from Step 1 | `12345678-1234-1234-1234-123456789012` |
| `AZURE_TENANT_ID` | Tenant ID from Step 1 | `87654321-4321-4321-4321-210987654321` |
| `AZURE_SUBSCRIPTION_ID` | Your subscription ID | `11111111-2222-3333-4444-555555555555` |

**Note**: These are **Variables**, not Secrets. OIDC uses federated credentials, so no secrets are needed.

#### Create GitHub Environment (Optional)

If you used the `environment:production` federated credential:

1. Go to Settings → Environments
2. Create new environment named `production`
3. (Optional) Add protection rules like required reviewers

### Step 5: Verify Setup

Check that everything is configured correctly:

```powershell
# Verify Service Principal exists
az ad sp show --id $clientId --query displayName

# Verify role assignments
az role assignment list --assignee $clientId --output table

# Verify federated credentials
az ad app federated-credential list --id $clientId --output table
```

## Running the Workflow

### Manual Trigger (Workflow Dispatch)

1. Go to your GitHub repository
2. Click **Actions** tab
3. Select **Deploy to Azure** workflow
4. Click **Run workflow**
5. Fill in the inputs:
   - Resource Group Name: `rg-expensemgmt-cicd`
   - Azure Region: `uksouth`
   - Deploy GenAI Resources: `true` or `false`
6. Click **Run workflow**

### Workflow Inputs

| Input | Required | Default | Description |
|-------|----------|---------|-------------|
| `resourceGroup` | Yes | `rg-expensemgmt-cicd` | Name of the resource group to create |
| `location` | Yes | `uksouth` | Azure region for resources |
| `deployGenAI` | No | `false` | Whether to deploy Azure OpenAI and AI Search |

## Workflow Details

The workflow has two jobs:

### Job 1: Deploy Infrastructure

1. ✅ Checkout code
2. ✅ Authenticate to Azure using OIDC
3. ✅ Install sqlcmd (go-sqlcmd)
4. ✅ Run `deploy-infra/deploy.ps1`
5. ✅ Upload deployment context as artifact

### Job 2: Deploy Application

1. ✅ Checkout code
2. ✅ Setup .NET 8 SDK
3. ✅ Download deployment context from previous job
4. ✅ Authenticate to Azure using OIDC
5. ✅ Wait 60 seconds for App Service to stabilize
6. ✅ Run `deploy-app/deploy.ps1`
7. ✅ Display application URLs

## Troubleshooting

### Issue: "Login failed"

**Cause**: OIDC federated credentials not configured correctly.

**Solution**:
1. Verify `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` variables are set correctly
2. Verify federated credentials exist: `az ad app federated-credential list --id $clientId`
3. Ensure the subject matches your repository: `repo:owner/repo:ref:refs/heads/main`

### Issue: "The client does not have permission to perform action 'Microsoft.Authorization/roleAssignments/write'"

**Cause**: Service Principal doesn't have User Access Administrator role.

**Solution**:
```powershell
az role assignment create `
    --assignee $clientId `
    --role "User Access Administrator" `
    --scope "/subscriptions/$subscriptionId"
```

### Issue: "sqlcmd: command not found"

**Cause**: sqlcmd installation failed in the workflow.

**Solution**: Check the "Install sqlcmd" step logs. The workflow installs it automatically from GitHub releases.

### Issue: "Unable to load the proper Managed Identity"

**Cause**: This usually means the workflow succeeded but the application can't connect to the database.

**Solution**:
1. Check that `AZURE_CLIENT_ID` is configured in App Service settings
2. Verify the managed identity has database permissions
3. Review the infrastructure deployment script logs

## Security Benefits of OIDC

✅ **No secrets stored in GitHub** - OIDC uses short-lived tokens  
✅ **Automatic credential rotation** - No need to update secrets  
✅ **Better audit trail** - All actions are tied to the Service Principal  
✅ **Follows Azure best practices** - Recommended by Microsoft

## Local vs CI/CD Differences

| Aspect | Local Deployment | CI/CD Deployment |
|--------|------------------|------------------|
| Authentication | `az login` (interactive) | OIDC (Service Principal) |
| Admin Principal Type | `User` | `Application` |
| sqlcmd Auth Method | `ActiveDirectoryDefault` | `ActiveDirectoryAzCli` |
| Managed Identity Creation | SID-based (no Directory Reader) | SID-based (no Directory Reader) |

## Alternative: Using Secrets (Not Recommended)

If you cannot use OIDC, you can use a Service Principal with a client secret:

1. Create SP: `az ad sp create-for-rbac --sdk-auth`
2. Copy the entire JSON output
3. Create GitHub Secret named `AZURE_CREDENTIALS` with the JSON
4. Modify workflow to use `azure/login@v2` with: `creds: ${{ secrets.AZURE_CREDENTIALS }}`

**Note**: This approach requires manually rotating secrets every 1-2 years.

## Next Steps

After setting up CI/CD:

1. Trigger a manual workflow run to test
2. Monitor the Actions tab for progress
3. Check Azure Portal for deployed resources
4. Visit the application URL displayed at the end

## References

- [Azure Login Action with OIDC](https://github.com/Azure/login#configure-a-service-principal-with-a-federated-credential-to-use-oidc-based-authentication)
- [GitHub OIDC with Azure](https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/configuring-openid-connect-in-azure)
- [Azure RBAC Roles](https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles)
