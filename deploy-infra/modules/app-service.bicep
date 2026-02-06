@description('Location for the App Service')
param location string

@description('Base name for resource naming')
param baseName string

@description('Application Insights connection string')
param appInsightsConnectionString string

@description('Managed Identity resource ID')
param managedIdentityId string

@description('Managed Identity client ID')
param managedIdentityClientId string

var uniqueSuffix = uniqueString(resourceGroup().id)
var appServicePlanName = toLower('asp-${baseName}-${uniqueSuffix}')
var webAppName = toLower('app-${baseName}-${uniqueSuffix}')

resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: 'S1'
    tier: 'Standard'
    capacity: 1
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2022-09-01' = {
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
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'XDT_MicrosoftApplicationInsights_Mode'
          value: 'recommended'
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: managedIdentityClientId
        }
      ]
    }
  }
}

@description('The name of the App Service')
output name string = webApp.name

@description('The resource ID of the App Service')
output id string = webApp.id

@description('The default hostname of the App Service')
output defaultHostname string = webApp.properties.defaultHostName
