@description('Azure region for resources')
param location string = 'uksouth'

@description('Base name for resources')
param baseName string = 'expensemgmt'

@description('Timestamp for unique naming')
param timestamp string = utcNow('yyyyMMddHHmm')

@description('SQL Server administrator Object ID')
param adminObjectId string

@description('SQL Server administrator login name')
param adminLogin string

@description('SQL Server administrator principal type (User or Application)')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

@description('Deploy GenAI resources (Azure OpenAI and AI Search)')
param deployGenAI bool = false

// Deploy monitoring first (without App Service diagnostics to avoid circular dependency)
module monitoring './modules/monitoring.bicep' = {
  name: 'monitoring-deployment'
  params: {
    location: location
    baseName: baseName
    timestamp: timestamp
  }
}

// Deploy managed identity
module managedIdentity './modules/managed-identity.bicep' = {
  name: 'managed-identity-deployment'
  params: {
    location: location
    baseName: baseName
    timestamp: timestamp
  }
}

// Deploy App Service
module appService './modules/app-service.bicep' = {
  name: 'app-service-deployment'
  params: {
    location: location
    baseName: baseName
    timestamp: timestamp
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    managedIdentityId: managedIdentity.outputs.managedIdentityId
  }
}

// Deploy Azure SQL
module azureSQL './modules/azure-sql.bicep' = {
  name: 'azure-sql-deployment'
  params: {
    location: location
    baseName: baseName
    timestamp: timestamp
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    adminPrincipalType: adminPrincipalType
  }
}

// Deploy App Service diagnostics after App Service is created
module appServiceDiagnostics './modules/app-service-diagnostics.bicep' = {
  name: 'app-service-diagnostics-deployment'
  params: {
    appServiceName: appService.outputs.webAppName
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    sqlServerName: azureSQL.outputs.sqlServerName
    databaseName: azureSQL.outputs.databaseName
  }
}

// Conditionally deploy GenAI resources
module genAI './modules/genai.bicep' = if (deployGenAI) {
  name: 'genai-deployment'
  params: {
    location: location
    baseName: baseName
    timestamp: timestamp
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// Outputs
output webAppName string = appService.outputs.webAppName
output webAppHostName string = appService.outputs.webAppHostName
output sqlServerFqdn string = azureSQL.outputs.sqlServerFqdn
output sqlServerName string = azureSQL.outputs.sqlServerName
output databaseName string = azureSQL.outputs.databaseName
output managedIdentityClientId string = managedIdentity.outputs.managedIdentityClientId
output managedIdentityName string = managedIdentity.outputs.managedIdentityName
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString
output openAIEndpoint string = deployGenAI ? genAI.?outputs.?openAIEndpoint ?? '' : ''
output openAIModelName string = deployGenAI ? genAI.?outputs.?openAIModelName ?? '' : ''
output searchEndpoint string = deployGenAI ? genAI.?outputs.?searchEndpoint ?? '' : ''
