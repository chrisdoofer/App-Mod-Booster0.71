// App Service Module
// Creates an Azure App Service with Standard S1 pricing tier

@description('Azure region for the App Service')
param location string

@description('Base name for the resources')
param baseName string

@description('Resource ID of the user-assigned managed identity')
param managedIdentityId string

@description('Application Insights connection string')
param appInsightsConnectionString string = ''

var uniqueSuffix = uniqueString(resourceGroup().id)
var appServicePlanName = toLower('asp-${baseName}-${uniqueSuffix}')
var webAppName = toLower('app-${baseName}-${uniqueSuffix}')

resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'S1'
    tier: 'Standard'
  }
  properties: {
    reserved: true // Linux
  }
}

resource webApp 'Microsoft.Web/sites@2023-01-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: !empty(appInsightsConnectionString) ? [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
      ] : []
    }
  }
}

@description('The resource ID of the web app')
output webAppId string = webApp.id

@description('The name of the web app')
output webAppName string = webApp.name

@description('The default hostname of the web app')
output webAppHostname string = webApp.properties.defaultHostName

@description('The name of the App Service Plan')
output appServicePlanName string = appServicePlan.name
