@description('Azure region for the App Service')
param location string

@description('Base name for resources')
param baseName string

@description('The resource ID of the user-assigned managed identity')
param managedIdentityId string

@description('The Application Insights connection string')
param appInsightsConnectionString string = ''

var appServicePlanName = 'asp-${baseName}-${uniqueString(resourceGroup().id)}'
var webAppName = 'app-${baseName}-${uniqueString(resourceGroup().id)}'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: toLower(appServicePlanName)
  location: location
  sku: {
    name: 'S1'
    tier: 'Standard'
  }
  properties: {
    reserved: false
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: toLower(webAppName)
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
      netFrameworkVersion: 'v8.0'
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

@description('The name of the web app')
output webAppName string = webApp.name

@description('The default hostname of the web app')
output webAppHostName string = webApp.properties.defaultHostName

@description('The resource ID of the web app')
output webAppId string = webApp.id
