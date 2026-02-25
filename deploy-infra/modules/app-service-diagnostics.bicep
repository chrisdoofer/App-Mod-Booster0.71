// ============================================================
// App Service Diagnostics Module
// Configures diagnostic settings for the App Service to send
// logs to the Log Analytics Workspace.
//
// This is a SEPARATE module (not inside monitoring.bicep or
// app-service.bicep) to avoid circular dependency errors.
// Deploy this module AFTER both monitoring and app-service.
// ============================================================

@description('Name of the existing App Service to configure diagnostics for.')
param appServiceName string

@description('Resource ID of the Log Analytics Workspace to send logs to.')
param logAnalyticsWorkspaceId string

// ── Existing App Service Reference ───────────────────────────
resource appService 'Microsoft.Web/sites@2022-09-01' existing = {
  name: appServiceName
}

// ── Diagnostic Settings ───────────────────────────────────────
resource appServiceDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-${appServiceName}'
  scope: appService
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'AppServiceAuditLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'AppServicePlatformLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
    ]
  }
}
