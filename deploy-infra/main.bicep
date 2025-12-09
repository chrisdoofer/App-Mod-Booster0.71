// Main Bicep Template
// Orchestrates deployment of all infrastructure resources

@description('Azure region for resources')
param location string

@description('Base name for resources')
param baseName string = 'expensemgmt'

@description('Admin Object ID from Entra ID')
param adminObjectId string

@description('Admin User Principal Name or App Display Name')
param adminLogin string

@description('Principal type for SQL admin')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

@description('Whether to deploy GenAI resources (Azure OpenAI and AI Search)')
param deployGenAI bool = false

@description('Timestamp for unique naming - use as parameter default only')
param timestamp string = utcNow('yyyyMMddHHmm')

// Deploy monitoring first (without App Service diagnostics)
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring-${timestamp}'
  params: {
    location: location
    baseName: baseName
  }
}

// Deploy managed identity
module managedIdentity 'modules/managed-identity.bicep' = {
  name: 'managed-identity-${timestamp}'
  params: {
    location: location
    baseName: baseName
    timestamp: timestamp
  }
}

// Deploy App Service with managed identity
module appService 'modules/app-service.bicep' = {
  name: 'app-service-${timestamp}'
  params: {
    location: location
    baseName: baseName
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
  }
}

// Deploy App Service diagnostics after App Service is created
module appServiceDiagnostics 'modules/app-service-diagnostics.bicep' = {
  name: 'app-service-diagnostics-${timestamp}'
  params: {
    appServiceName: appService.outputs.webAppName
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

// Deploy Azure SQL
module azureSql 'modules/azure-sql.bicep' = {
  name: 'azure-sql-${timestamp}'
  params: {
    location: location
    baseName: baseName
    adminObjectId: adminObjectId
    adminLogin: adminLogin
    adminPrincipalType: adminPrincipalType
  }
}

// Deploy SQL Database diagnostics
module sqlDiagnostics 'modules/sql-diagnostics.bicep' = {
  name: 'sql-diagnostics-${timestamp}'
  params: {
    sqlServerName: azureSql.outputs.sqlServerName
    databaseName: azureSql.outputs.databaseName
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

// Conditionally deploy GenAI resources
module genAI 'modules/genai.bicep' = if (deployGenAI) {
  name: 'genai-${timestamp}'
  params: {
    location: location
    baseName: baseName
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
}

// Outputs
@description('Web App name')
output webAppName string = appService.outputs.webAppName

@description('Web App hostname')
output webAppHostname string = appService.outputs.webAppHostname

@description('SQL Server FQDN')
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn

@description('SQL Server name')
output sqlServerName string = azureSql.outputs.sqlServerName

@description('Database name')
output databaseName string = azureSql.outputs.databaseName

@description('Managed Identity Client ID')
output managedIdentityClientId string = managedIdentity.outputs.managedIdentityClientId

@description('Managed Identity Name')
output managedIdentityName string = managedIdentity.outputs.managedIdentityName

@description('Application Insights Connection String')
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString

@description('Log Analytics Workspace ID')
output logAnalyticsWorkspaceId string = monitoring.outputs.logAnalyticsWorkspaceId

// Conditional GenAI outputs - use null-safe operators
@description('Azure OpenAI Endpoint (empty if GenAI not deployed)')
output openAIEndpoint string = deployGenAI && genAI != null ? genAI.outputs.openAIEndpoint : ''

@description('Azure OpenAI Model Name (empty if GenAI not deployed)')
output openAIModelName string = deployGenAI && genAI != null ? genAI.outputs.openAIModelName : ''

@description('Azure AI Search Endpoint (empty if GenAI not deployed)')
output searchEndpoint string = deployGenAI && genAI != null ? genAI.outputs.searchEndpoint : ''
