@description('Azure region for resources')
param location string

@description('Base name for resources')
param baseName string

@description('Timestamp for unique naming')
param timestamp string

@description('App Service name for diagnostics (optional, leave empty for first deployment)')
param appServiceName string = ''

var logAnalyticsName = 'log-${baseName}-${timestamp}'
var appInsightsName = 'appi-${baseName}-${timestamp}'

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id
output appInsightsConnectionString string = appInsights.properties.ConnectionString
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
