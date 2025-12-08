@description('Azure region for all resources')
param location string

@description('Base name for all resources')
param baseName string = 'expensemgmt'

@description('Timestamp for unique naming (automatically set)')
param timestamp string = utcNow('yyyyMMddHHmm')

@description('Object ID of the Azure AD administrator for SQL Server')
param adminObjectId string

@description('User Principal Name of the Azure AD administrator')
param adminUpn string

@description('Principal type for SQL admin (User for interactive, Application for CI/CD)')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

@description('Deploy GenAI resources (Azure OpenAI and AI Search)')
param deployGenAI bool = false

// Deploy Managed Identity first
module managedIdentity 'modules/managed-identity.bicep' = {
  name: 'managedIdentityDeployment'
  params: {
    location: location
    baseName: baseName
    timestamp: timestamp
  }
}

// Deploy Monitoring (without App Service diagnostics initially)
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoringDeployment'
  params: {
    location: location
    baseName: baseName
  }
}

// Deploy App Service with the managed identity
module appService 'modules/app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
    baseName: baseName
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
  }
}

// Deploy Azure SQL
module azureSql 'modules/azure-sql.bicep' = {
  name: 'azureSqlDeployment'
  params: {
    location: location
    baseName: baseName
    adminObjectId: adminObjectId
    adminUpn: adminUpn
    adminPrincipalType: adminPrincipalType
  }
}

// Deploy App Service diagnostics after App Service exists
module appServiceDiagnostics 'modules/app-service-diagnostics.bicep' = {
  name: 'appServiceDiagnosticsDeployment'
  params: {
    appServiceName: appService.outputs.webAppName
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

// Conditionally deploy GenAI resources
module genAI 'modules/genai.bicep' = if (deployGenAI) {
  name: 'genAIDeployment'
  params: {
    location: location
    baseName: baseName
    managedIdentityPrincipalId: managedIdentity.outputs.managedIdentityPrincipalId
  }
}

// Outputs
@description('Name of the web app')
output webAppName string = appService.outputs.webAppName

@description('Hostname of the web app')
output webAppHostName string = appService.outputs.webAppHostName

@description('Fully qualified domain name of the SQL server')
output sqlServerFqdn string = azureSql.outputs.sqlServerFqdn

@description('Name of the SQL server')
output sqlServerName string = azureSql.outputs.sqlServerName

@description('Name of the database')
output databaseName string = azureSql.outputs.databaseName

@description('Client ID of the managed identity')
output managedIdentityClientId string = managedIdentity.outputs.managedIdentityClientId

@description('Name of the managed identity')
output managedIdentityName string = managedIdentity.outputs.managedIdentityName

@description('Application Insights connection string')
output appInsightsConnectionString string = monitoring.outputs.appInsightsConnectionString

@description('OpenAI endpoint (only when GenAI is deployed)')
output openAIEndpoint string = deployGenAI ? genAI.?outputs.?openAIEndpoint ?? '' : ''

@description('OpenAI model name (only when GenAI is deployed)')
output openAIModelName string = deployGenAI ? genAI.?outputs.?openAIModelName ?? '' : ''

@description('AI Search endpoint (only when GenAI is deployed)')
output aiSearchEndpoint string = deployGenAI ? genAI.?outputs.?aiSearchEndpoint ?? '' : ''
