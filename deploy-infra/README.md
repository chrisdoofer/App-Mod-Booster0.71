# Infrastructure Deployment — Expense Management

This folder contains all Bicep templates for deploying the Expense Management Azure infrastructure.

## Architecture Overview

```
deploy-infra/
├── main.bicep              ← Orchestration template (entry point)
├── main.bicepparam         ← Parameter file (location, baseName, deployGenAI)
└── modules/
    ├── managed-identity.bicep        ← User-Assigned Managed Identity
    ├── monitoring.bicep              ← Log Analytics Workspace + App Insights
    ├── app-service.bicep             ← App Service Plan (S1) + Web App
    ├── app-service-diagnostics.bicep ← App Service diagnostic settings
    ├── azure-sql.bicep               ← SQL Server + Northwind DB (Entra ID-only)
    ├── sql-diagnostics.bicep         ← SQL Database diagnostic settings
    └── genai.bicep                   ← Azure OpenAI + AI Search (conditional)
```

### Resources Created

| Resource | SKU / Tier | Region |
|----------|-----------|--------|
| User-Assigned Managed Identity | — | UK South |
| Log Analytics Workspace | PerGB2018 | UK South |
| Application Insights | web | UK South |
| App Service Plan | Standard S1 | UK South |
| App Service (Web App) | Linux / .NET 8 | UK South |
| Azure SQL Server | Entra ID-only | UK South |
| Azure SQL Database (Northwind) | Basic (5 DTU) | UK South |
| Azure OpenAI *(optional)* | S0 — GPT-4o | Sweden Central |
| Azure AI Search *(optional)* | Basic | UK South |

### Deployment Order

Bicep resolves most ordering automatically via parameter references. The explicit sequence is:

1. **managed-identity** — no dependencies
2. **monitoring** — no dependencies (App Service diagnostics skipped at this stage)
3. **app-service** — depends on managed-identity + monitoring outputs
4. **app-service-diagnostics** — depends on app-service + monitoring (avoids circular dependency)
5. **azure-sql** — depends on managed-identity
6. **sql-diagnostics** — depends on azure-sql + monitoring
7. **genai** *(conditional)* — depends on managed-identity

---

## Prerequisites

- Azure CLI 2.50+ with Bicep CLI 0.20+
- Authenticated to the target subscription: `az login`
- A resource group already created (the templates deploy into an existing group)

```powershell
# Create a resource group
az group create --name rg-expensemgmt-dev --location uksouth
```

---

## Deployment Commands

### Standard Deployment (no GenAI)

```powershell
# Get your Entra ID details
$adminObjectId = az ad signed-in-user show --query id -o tsv
$adminUPN      = az ad signed-in-user show --query userPrincipalName -o tsv

# Deploy infrastructure
az deployment group create `
    --resource-group rg-expensemgmt-dev `
    --template-file deploy-infra/main.bicep `
    --parameters deploy-infra/main.bicepparam `
    --parameters adminObjectId=$adminObjectId adminUserPrincipalName=$adminUPN `
    --output json
```

### Deployment with GenAI (Azure OpenAI + AI Search)

```powershell
$adminObjectId = az ad signed-in-user show --query id -o tsv
$adminUPN      = az ad signed-in-user show --query userPrincipalName -o tsv

az deployment group create `
    --resource-group rg-expensemgmt-dev `
    --template-file deploy-infra/main.bicep `
    --parameters deploy-infra/main.bicepparam `
    --parameters adminObjectId=$adminObjectId adminUserPrincipalName=$adminUPN deployGenAI=true `
    --output json
```

### CI/CD Deployment (Service Principal)

When deploying from a GitHub Actions pipeline, the caller is a Service Principal.
Pass `adminPrincipalType=Application` to configure the SQL admin correctly:

```powershell
az deployment group create `
    --resource-group rg-expensemgmt-dev `
    --template-file deploy-infra/main.bicep `
    --parameters deploy-infra/main.bicepparam `
    --parameters `
        adminObjectId=$env:AZURE_OBJECT_ID `
        adminUserPrincipalName=$env:AZURE_SP_NAME `
        adminPrincipalType=Application `
    --output json 2>$null
```

### Custom Base Name or Location

```powershell
az deployment group create `
    --resource-group rg-myapp-prod `
    --template-file deploy-infra/main.bicep `
    --parameters location=uksouth baseName=myapp deployGenAI=false `
    --parameters adminObjectId=$adminObjectId adminUserPrincipalName=$adminUPN `
    --output json
```

---

## Validate Before Deploying

```bash
# Compile check (no Azure connection required)
az bicep build --file deploy-infra/main.bicep

# What-if (requires Azure connection)
az deployment group what-if \
    --resource-group rg-expensemgmt-dev \
    --template-file deploy-infra/main.bicep \
    --parameters deploy-infra/main.bicepparam \
    --parameters adminObjectId=<your-object-id> adminUserPrincipalName=<your-upn>
```

---

## Outputs

The deployment emits these outputs, consumed by `deploy-infra/deploy.ps1` to configure
App Service settings and write the `.deployment-context.json` handoff file:

| Output | Description |
|--------|-------------|
| `webAppName` | App Service resource name |
| `defaultHostName` | App Service default hostname |
| `sqlServerFqdn` | SQL Server fully-qualified domain name |
| `sqlServerName` | SQL Server resource name |
| `databaseName` | `Northwind` |
| `managedIdentityName` | Identity resource name |
| `managedIdentityClientId` | Identity Client ID (used as `AZURE_CLIENT_ID`) |
| `managedIdentityPrincipalId` | Identity Principal/Object ID |
| `appInsightsConnectionString` | Application Insights connection string |
| `logAnalyticsWorkspaceId` | Log Analytics Workspace resource ID |
| `openAIEndpoint` | Azure OpenAI endpoint (empty if GenAI not deployed) |
| `openAIModelName` | GPT-4o deployment name (empty if GenAI not deployed) |
| `openAIName` | Azure OpenAI resource name (empty if GenAI not deployed) |
| `searchEndpoint` | AI Search endpoint URL (empty if GenAI not deployed) |

---

## Security Notes

- **Zero secrets** — all authentication uses User-Assigned Managed Identity
- **Entra ID-only SQL auth** — `azureADOnlyAuthentication: true`, SQL login is disabled
- **HTTPS only** — App Service enforces HTTPS and TLS 1.2+
- **FTPS disabled** — FTP access is turned off on the App Service
- **Health check** — `/health` endpoint configured to avoid false start failures

---

## Troubleshooting

### `BCP065` — utcNow / newGuid in wrong context
Ensure `utcNow()` and `newGuid()` are only used as parameter default values, never in variable declarations or resource properties.

### `BCP318` — Value may be null
Use null-safe operators for conditional module outputs:
```bicep
output openAIEndpoint string = deployGenAI ? genai.?outputs.?openAIEndpoint ?? '' : ''
```

### SQL Admin not set
If `adminObjectId` is empty, the SQL Server will deploy without an Entra ID admin and you will not be able to connect. Always pass this parameter.

### Circular dependency error
App Service and monitoring diagnostics must be in separate modules. Never put App Service diagnostic settings inside `monitoring.bicep` or `app-service.bicep`.
