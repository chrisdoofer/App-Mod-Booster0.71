@description('The Azure region where resources will be deployed')
param location string = 'uksouth'

@description('Base name for resources')
param baseName string = 'expensemgmt'

@description('Timestamp for unique naming (must be used as parameter)')
param timestamp string = utcNow('yyyyMMddHHmm')

@description('The Object ID of the Azure AD admin for SQL Server')
param adminObjectId string

@description('The User Principal Name or Display Name of the Azure AD admin')
param adminUsername string

@description('The principal type (User or Application) for SQL Server admin')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

@description('Whether to deploy GenAI resources (Azure OpenAI and AI Search)')
param deployGenAI bool = false

var uniqueSuffix = uniqueString(resourceGroup().id)

// Deploy managed identity first
module managedIdentity 'modules/managed-identity.bicep' = {
  name: 'managedIdentityDeployment'
  params: {
    location: location
    baseName: baseName
    timestamp: timestamp
  }
}

// Deploy monitoring resources
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoringDeployment'
  params: {
    location: location
    baseName: baseName
    uniqueSuffix: uniqueSuffix
  }
}

// Deploy App Service
module appService 'modules/app-service.bicep' = {
  name: 'appServiceDeployment'
  params: {
    location: location
    baseName: baseName
    uniqueSuffix: uniqueSuffix
    managedIdentityId: managedIdentity.outputs.managedIdentityId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
  }
}

// Deploy App Service diagnostics after App Service is created
module appServiceDiagnostics 'modules/app-service-diagnostics.bicep' = {
  name: 'appServiceDiagnosticsDeployment'
  params: {
    appServiceName: appService.outputs.webAppName
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

// Deploy Azure SQL
module azureSQL 'modules/azure-sql.bicep' = {
  name: 'azureSQLDeployment'
  params: {
    location: location
    baseName: baseName
    uniqueSuffix: uniqueSuffix
    adminObjectId: adminObjectId
    adminUsername: adminUsername
    adminPrincipalType: adminPrincipalType
  }
}

// Deploy SQL diagnostics after database is created
module sqlDiagnostics 'modules/sql-diagnostics.bicep' = {
  name: 'sqlDiagnosticsDeployment'
  params: {
    sqlServerName: azureSQL.outputs.sqlServerName
    databaseName: azureSQL.outputs.databaseName
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
  }
}

// Conditionally deploy GenAI resources
module genAI 'modules/genai.bicep' = if (deployGenAI) {
  name: 'genAIDeployment'
  params: {
    location: location
    baseName: baseName
    uniqueSuffix: uniqueSuffix
    managedIdentityPrincipalId: appService.outputs.managedIdentityPrincipalId
  }
}

// Outputs
@description('The name of the web app')
output webAppName string = appService.outputs.webAppName

@description('The default hostname of the web app')
output webAppHostName string = appService.outputs.webAppHostName

@description('The client ID of the managed identity')
output managedIdentityClientId string = managedIdentity.outputs.managedIdentityClientId

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

@description('The endpoint for Azure OpenAI (empty if not deployed)')
output openAIEndpoint string = deployGenAI ? genAI.?outputs.?openAIEndpoint ?? '' : ''

@description('The name of the OpenAI model deployment (empty if not deployed)')
output openAIModelName string = deployGenAI ? genAI.?outputs.?openAIModelName ?? '' : ''

@description('The endpoint for Azure AI Search (empty if not deployed)')
output searchEndpoint string = deployGenAI ? genAI.?outputs.?searchEndpoint ?? '' : ''
