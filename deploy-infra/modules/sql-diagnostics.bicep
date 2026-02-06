@description('SQL Database name to configure diagnostics for')
param databaseName string

@description('SQL Server name')
param sqlServerName string

@description('Log Analytics Workspace ID')
param logAnalyticsWorkspaceId string

resource database 'Microsoft.Sql/servers/databases@2023-05-01-preview' existing = {
  name: '${sqlServerName}/${databaseName}'
}

resource sqlDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'sql-diagnostics'
  scope: database
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'SQLInsights'
        enabled: true
      }
      {
        category: 'AutomaticTuning'
        enabled: true
      }
      {
        category: 'QueryStoreRuntimeStatistics'
        enabled: true
      }
      {
        category: 'QueryStoreWaitStatistics'
        enabled: true
      }
      {
        category: 'Errors'
        enabled: true
      }
      {
        category: 'DatabaseWaitStatistics'
        enabled: true
      }
      {
        category: 'Timeouts'
        enabled: true
      }
      {
        category: 'Blocks'
        enabled: true
      }
      {
        category: 'Deadlocks'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'Basic'
        enabled: true
      }
      {
        category: 'InstanceAndAppAdvanced'
        enabled: true
      }
      {
        category: 'WorkloadManagement'
        enabled: true
      }
    ]
  }
}
