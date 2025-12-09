// App Service Diagnostics Module
// Configures diagnostic settings for App Service after it's created

@description('Name of the App Service to configure diagnostics for')
param appServiceName string

@description('Resource ID of the Log Analytics workspace')
param logAnalyticsWorkspaceId string

resource appService 'Microsoft.Web/sites@2023-01-01' existing = {
  name: appServiceName
}

resource appServiceDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'AppServiceDiagnostics'
  scope: appService
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
      }
      {
        category: 'AppServiceAuditLogs'
        enabled: true
      }
      {
        category: 'AppServicePlatformLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}
