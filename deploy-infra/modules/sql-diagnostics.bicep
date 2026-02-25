// ============================================================
// SQL Diagnostics Module
// Configures diagnostic settings at the SQL DATABASE level
// (NOT server level) to send logs to Log Analytics.
//
// Server-level categories (e.g. SQLSecurityAuditEvents) are
// NOT supported and will cause deployment failures — this
// module deliberately targets the database resource only.
// ============================================================

@description('Name of the existing SQL Server.')
param sqlServerName string

@description('Name of the existing SQL Database to configure diagnostics for.')
param databaseName string

@description('Resource ID of the Log Analytics Workspace to send logs to.')
param logAnalyticsWorkspaceId string

// ── Existing SQL Database Reference ──────────────────────────
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' existing = {
  name: '${sqlServerName}/${databaseName}'
}

// ── Diagnostic Settings (Database Level) ─────────────────────
resource sqlDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-${databaseName}'
  scope: sqlDatabase
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'SQLInsights'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'AutomaticTuning'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'QueryStoreRuntimeStatistics'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'QueryStoreWaitStatistics'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'Errors'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'DatabaseWaitStatistics'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'Timeouts'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'Blocks'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'Deadlocks'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
    ]
    metrics: [
      {
        category: 'Basic'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
      {
        category: 'InstanceAndAppAdvanced'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
    ]
  }
}
