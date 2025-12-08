@description('Azure region for monitoring resources')
param location string

@description('Base name for resources')
param baseName string

var logAnalyticsName = 'log-${baseName}-${uniqueString(resourceGroup().id)}'
var appInsightsName = 'appi-${baseName}-${uniqueString(resourceGroup().id)}'

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: toLower(logAnalyticsName)
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: toLower(appInsightsName)
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

@description('The resource ID of the Log Analytics workspace')
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id

@description('The connection string for Application Insights')
output appInsightsConnectionString string = appInsights.properties.ConnectionString

@description('The instrumentation key for Application Insights')
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey

@description('The name of Application Insights')
output appInsightsName string = appInsights.name
