// ============================================================
// Monitoring Module
// Creates a Log Analytics Workspace and Application Insights
// instance for centralised logging and telemetry.
//
// NOTE: App Service diagnostic settings are intentionally NOT
// configured here to avoid circular dependencies. Deploy
// app-service-diagnostics.bicep after both this module and
// app-service.bicep have completed.
// ============================================================

@description('Azure region for the resources.')
param location string

@description('Base name used to generate resource names.')
param baseName string

@description('App Service name for diagnostic settings (leave empty on first pass to avoid circular dependency).')
param appServiceName string = ''

// ── Resource Names ───────────────────────────────────────────
var uniqueSuffix    = uniqueString(resourceGroup().id)
var workspaceName   = toLower('log-${baseName}-${uniqueSuffix}')
var appInsightsName = toLower('appi-${baseName}-${uniqueSuffix}')

// ── Log Analytics Workspace ──────────────────────────────────
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: workspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// ── Application Insights ─────────────────────────────────────
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// ── Optional: App Service Reference (for diagnostic settings) ─
// Conditionally reference an existing App Service when appServiceName is provided.
// This avoids circular dependencies — use the dedicated app-service-diagnostics.bicep
// module for the preferred deployment pattern.
resource existingAppService 'Microsoft.Web/sites@2022-09-01' existing = if (!empty(appServiceName)) {
  name: !empty(appServiceName) ? appServiceName : 'placeholder'
}

resource appServiceDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (!empty(appServiceName)) {
  name: 'diag-appservice'
  scope: existingAppService
  properties: {
    workspaceId: logAnalyticsWorkspace.id
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
  }
}

// ── Outputs ──────────────────────────────────────────────────
@description('Resource ID of the Log Analytics Workspace.')
output logAnalyticsWorkspaceId string = logAnalyticsWorkspace.id

@description('Application Insights connection string.')
output appInsightsConnectionString string = appInsights.properties.ConnectionString

@description('Application Insights resource name.')
output appInsightsName string = appInsights.name
