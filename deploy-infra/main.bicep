@description('Location for all resources except Gen AI (which uses Sweden Central)')
param location string = 'uksouth'

@description('Base name for resource naming')
param baseName string = 'expensemgmt'

@description('SQL Administrator Object ID (from Entra ID)')
param sqlAdminObjectId string

@description('SQL Administrator login name (UPN or Service Principal name)')
param sqlAdminLogin string

@description('SQL Administrator principal type')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

@description('Deploy Gen AI resources (Azure OpenAI and AI Search)')
param deployGenAI bool = false

@description('Timestamp for unique naming')
param timestamp string = utcNow('yyyyMMddHHmm')

// Deploy Managed Identity first (needed by all other resources)
module managedIdentity 'modules/managed-identity.bicep' = {
  name: 'managed-identity-deployment'
  params: {
    location: location
    baseName: baseName
    timestamp: timestamp
  }
}

// Deploy Monitoring (Log Analytics and Application Insights)
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring-deployment'
  params: {
    location: location
    baseName: baseName
  }
}

// Deploy App Service
module appService 'modules/app-service.bicep' = {
  name: 'app-service-deployment'
  params: {
    location: location
    baseName: baseName
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    managedIdentityId: managedIdentity.outputs.id
    managedIdentityClientId: managedIdentity.outputs.clientId
  }
}

// Deploy App Service Diagnostics (separate to avoid circular dependency)
module appServiceDiagnostics 'modules/app-service-diagnostics.bicep' = {
  name: 'app-service-diagnostics-deployment'
  params: {
    appServiceName: appService.outputs.name
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

// Deploy Azure SQL
module azureSQL 'modules/azure-sql.bicep' = {
  name: 'azure-sql-deployment'
  params: {
    location: location
    baseName: baseName
    sqlAdminObjectId: sqlAdminObjectId
    sqlAdminLogin: sqlAdminLogin
    adminPrincipalType: adminPrincipalType
  }
}

// Deploy SQL Diagnostics
module sqlDiagnostics 'modules/sql-diagnostics.bicep' = {
  name: 'sql-diagnostics-deployment'
  params: {
    databaseName: azureSQL.outputs.databaseName
    sqlServerName: azureSQL.outputs.sqlServerName
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

// Deploy Gen AI resources (conditional)
module genAI 'modules/genai.bicep' = if (deployGenAI) {
  name: 'genai-deployment'
  params: {
    baseName: baseName
    managedIdentityPrincipalId: managedIdentity.outputs.principalId
  }
}

// Outputs required by deployment scripts
@description('The name of the App Service')
output webAppName string = appService.outputs.name

@description('The fully qualified domain name of the SQL Server')
output sqlServerFqdn string = azureSQL.outputs.sqlServerFqdn

@description('The name of the database')
output databaseName string = azureSQL.outputs.databaseName

@description('The name of the managed identity')
output managedIdentityName string = managedIdentity.outputs.name

@description('The client ID of the managed identity')
output managedIdentityClientId string = managedIdentity.outputs.clientId

@description('The principal ID (object ID) of the managed identity')
output managedIdentityPrincipalId string = managedIdentity.outputs.principalId

@description('The connection string for Application Insights')
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString

@description('The resource ID of the Log Analytics workspace')
output logAnalyticsWorkspaceId string = monitoring.outputs.logAnalyticsWorkspaceId

@description('The endpoint URL for Azure OpenAI (empty if not deployed)')
output openAIEndpoint string = deployGenAI ? genAI.?outputs.?openAIEndpoint ?? '' : ''

@description('The name of the deployed OpenAI model (empty if not deployed)')
output openAIModelName string = deployGenAI ? genAI.?outputs.?openAIModelName ?? '' : ''
