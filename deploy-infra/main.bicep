targetScope = 'resourceGroup'

@description('Azure region for the resources')
param location string

@description('Base name for resources')
param baseName string = 'expensemgmt'

@description('SQL Server administrator Object ID')
param adminObjectId string

@description('SQL Server administrator User Principal Name')
param adminUsername string

@description('Principal type for SQL Server admin - User for interactive, Application for CI/CD')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

@description('Deploy GenAI resources (Azure OpenAI and AI Search)')
param deployGenAI bool = false

@description('Timestamp for unique naming')
param timestamp string = utcNow('yyyyMMddHHmm')

// Deploy monitoring first (without App Service diagnostics to avoid circular dependency)
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    baseName: baseName
  }
}

// Deploy managed identity
module managedIdentity 'modules/managed-identity.bicep' = {
  name: 'managedIdentity'
  params: {
    location: location
    baseName: baseName
    timestamp: timestamp
  }
}

// Deploy App Service
module appService 'modules/app-service.bicep' = {
  name: 'appService'
  params: {
    location: location
    baseName: baseName
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
  }
}

// Deploy Azure SQL
module azureSQL 'modules/azure-sql.bicep' = {
  name: 'azureSQL'
  params: {
    location: location
    baseName: baseName
    adminObjectId: adminObjectId
    adminUsername: adminUsername
    adminPrincipalType: adminPrincipalType
  }
}

// Deploy diagnostics after App Service and SQL are created
module diagnostics 'modules/app-service-diagnostics.bicep' = {
  name: 'diagnostics'
  params: {
    location: location
    appServiceName: appService.outputs.webAppName
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    sqlServerName: azureSQL.outputs.sqlServerName
    databaseName: azureSQL.outputs.databaseName
  }
}

// Conditionally deploy GenAI resources
module genai 'modules/genai.bicep' = if (deployGenAI) {
  name: 'genai'
  params: {
    location: 'swedencentral'
    baseName: baseName
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
}

// Outputs
@description('The name of the web app')
output webAppName string = appService.outputs.webAppName

@description('The default hostname of the web app')
output webAppHostName string = appService.outputs.webAppHostName

@description('The client ID of the managed identity')
output managedIdentityClientId string = managedIdentity.outputs.managedIdentityClientId

@description('The principal ID of the managed identity')
output managedIdentityPrincipalId string = managedIdentity.outputs.managedIdentityPrincipalId

@description('The name of the managed identity')
output managedIdentityName string = managedIdentity.outputs.managedIdentityName

@description('The fully qualified domain name of the SQL server')
output sqlServerFqdn string = azureSQL.outputs.sqlServerFqdn

@description('The name of the SQL server')
output sqlServerName string = azureSQL.outputs.sqlServerName

@description('The name of the database')
output databaseName string = azureSQL.outputs.databaseName

@description('The Application Insights connection string')
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString

@description('The OpenAI endpoint (empty if GenAI not deployed)')
output openAIEndpoint string = deployGenAI && genai != null ? genai.outputs.openAIEndpoint : ''

@description('The OpenAI model deployment name (empty if GenAI not deployed)')
output openAIModelName string = deployGenAI && genai != null ? genai.outputs.openAIModelName : ''

@description('The Search Service endpoint (empty if GenAI not deployed)')
output searchServiceEndpoint string = deployGenAI && genai != null ? genai.outputs.searchServiceEndpoint : ''
