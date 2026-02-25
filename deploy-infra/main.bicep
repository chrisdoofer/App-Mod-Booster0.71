// ============================================================
// Main Orchestration Template — Expense Management
//
// Deployment order:
//   1. managed-identity   (no dependencies)
//   2. monitoring         (no dependencies, appServiceName left empty)
//   3. app-service        (depends on: managed-identity, monitoring)
//   4. app-service-diagnostics (depends on: app-service, monitoring)
//   5. azure-sql          (depends on: managed-identity)
//   6. sql-diagnostics    (depends on: azure-sql, monitoring)
//   7. genai              (conditional, depends on: managed-identity)
// ============================================================

// ── Global Parameters ─────────────────────────────────────────
@description('Azure region for all resources (except Azure OpenAI which deploys to Sweden Central).')
param location string = 'uksouth'

@description('Base name used to generate unique resource names across all modules.')
@minLength(3)
@maxLength(20)
param baseName string

@description('Deployment timestamp — makes the managed identity name unique per deployment.')
param timestamp string = utcNow('yyyyMMddHHmm')

// ── SQL Admin Parameters ──────────────────────────────────────
@description('Object ID of the Entra ID principal that will be the SQL Server administrator.')
param adminObjectId string = ''

@description('UPN or display name of the Entra ID SQL Server administrator.')
param adminUserPrincipalName string = ''

@description('Type of the Entra ID principal (User for interactive logins, Application for CI/CD service principals).')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

@description('Dummy SQL admin password — required by the ARM API even with Entra ID-only auth; never used for access.')
@secure()
param sqlAdminPassword string = newGuid()

// ── Feature Flags ─────────────────────────────────────────────
@description('Set to true to deploy Azure OpenAI and AI Search resources.')
param deployGenAI bool = false

// ── 1. Managed Identity ───────────────────────────────────────
module managedIdentity 'modules/managed-identity.bicep' = {
  name: 'deploy-managed-identity'
  params: {
    location: location
    baseName: baseName
    timestamp: timestamp
  }
}

// ── 2. Monitoring (Log Analytics + App Insights) ──────────────
// Deploy without appServiceName to avoid circular dependency.
// App Service diagnostics are wired up in step 4.
module monitoring 'modules/monitoring.bicep' = {
  name: 'deploy-monitoring'
  params: {
    location: location
    baseName: baseName
    appServiceName: ''
  }
}

// ── 3. App Service ────────────────────────────────────────────
// Bicep infers dependencies from the param references below.
module appService 'modules/app-service.bicep' = {
  name: 'deploy-app-service'
  params: {
    location: location
    baseName: baseName
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    azureClientId: managedIdentity.outputs.managedIdentityClientId
  }
}

// ── 4. App Service Diagnostics ────────────────────────────────
// Separate module to avoid circular dependency between
// app-service.bicep and monitoring.bicep.
// Bicep infers dependencies from the param references below.
module appServiceDiagnostics 'modules/app-service-diagnostics.bicep' = {
  name: 'deploy-app-service-diagnostics'
  params: {
    appServiceName: appService.outputs.webAppName
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

// ── 5. Azure SQL ──────────────────────────────────────────────
module azureSQL 'modules/azure-sql.bicep' = {
  name: 'deploy-azure-sql'
  params: {
    location: location
    baseName: baseName
    adminObjectId: adminObjectId
    adminUserPrincipalName: adminUserPrincipalName
    adminPrincipalType: adminPrincipalType
    sqlAdminPassword: sqlAdminPassword
  }
}

// ── 6. SQL Diagnostics ────────────────────────────────────────
// Database-level diagnostics only (server level is not supported).
// Bicep infers dependencies from the param references below.
module sqlDiagnostics 'modules/sql-diagnostics.bicep' = {
  name: 'deploy-sql-diagnostics'
  params: {
    sqlServerName: azureSQL.outputs.sqlServerName
    databaseName: azureSQL.outputs.databaseName
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

// ── 7. GenAI (Conditional) ────────────────────────────────────
// Bicep infers the managedIdentity dependency from param references below.
module genai 'modules/genai.bicep' = if (deployGenAI) {
  name: 'deploy-genai'
  params: {
    baseName: baseName
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
}

// ── Outputs ───────────────────────────────────────────────────
// These values are consumed by deploy.ps1 to configure App Service
// settings and write the .deployment-context.json handoff file.

@description('App Service resource name.')
output webAppName string = appService.outputs.webAppName

@description('Default hostname of the App Service (without https://).')
output defaultHostName string = appService.outputs.defaultHostName

@description('Fully-qualified domain name of the SQL Server.')
output sqlServerFqdn string = azureSQL.outputs.sqlServerFqdn

@description('Name of the SQL Server resource.')
output sqlServerName string = azureSQL.outputs.sqlServerName

@description('Name of the Northwind database.')
output databaseName string = azureSQL.outputs.databaseName

@description('Resource name of the user-assigned managed identity.')
output managedIdentityName string = managedIdentity.outputs.managedIdentityName

@description('Client ID of the user-assigned managed identity.')
output managedIdentityClientId string = managedIdentity.outputs.managedIdentityClientId

@description('Principal ID of the user-assigned managed identity.')
output managedIdentityPrincipalId string = managedIdentity.outputs.managedIdentityPrincipalId

@description('Application Insights connection string.')
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString

@description('Resource ID of the Log Analytics Workspace.')
output logAnalyticsWorkspaceId string = monitoring.outputs.logAnalyticsWorkspaceId

@description('Azure OpenAI endpoint URL (empty string if GenAI was not deployed).')
output openAIEndpoint string = deployGenAI ? genai.?outputs.?openAIEndpoint ?? '' : ''

@description('GPT-4o deployment name used in API calls (empty string if GenAI was not deployed).')
output openAIModelName string = deployGenAI ? genai.?outputs.?openAIModelName ?? '' : ''

@description('Azure OpenAI resource name (empty string if GenAI was not deployed).')
output openAIName string = deployGenAI ? genai.?outputs.?openAIName ?? '' : ''

@description('Azure AI Search endpoint URL (empty string if GenAI was not deployed).')
output searchEndpoint string = deployGenAI ? genai.?outputs.?searchEndpoint ?? '' : ''
