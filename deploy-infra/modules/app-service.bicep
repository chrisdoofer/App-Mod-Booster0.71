@description('Azure region for resources')
param location string

@description('Base name for resources')
param baseName string

@description('Timestamp for unique naming')
param timestamp string

@description('Application Insights connection string from monitoring module')
param appInsightsConnectionString string

@description('Managed identity resource ID')
param managedIdentityId string

var appServicePlanName = 'asp-${baseName}-${timestamp}'
var webAppName = toLower('app-${baseName}-${timestamp}')

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
      ]
    }
  }
}

output webAppId string = webApp.id
output webAppName string = webApp.name
output webAppHostName string = webApp.properties.defaultHostName
output managedIdentityPrincipalId string = webApp.identity.userAssignedIdentities[managedIdentityId].principalId
