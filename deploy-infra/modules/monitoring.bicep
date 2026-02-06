@description('Location for monitoring resources')
param location string

@description('Base name for resource naming')
param baseName string

var uniqueSuffix = uniqueString(resourceGroup().id)
var logAnalyticsName = toLower('law-${baseName}-${uniqueSuffix}')
var appInsightsName = toLower('ai-${baseName}-${uniqueSuffix}')

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
    RetentionInDays: 30
  }
}

@description('The resource ID of the Log Analytics workspace')
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id

@description('The connection string for Application Insights')
output appInsightsConnectionString string = appInsights.properties.ConnectionString

@description('The instrumentation key for Application Insights')
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
