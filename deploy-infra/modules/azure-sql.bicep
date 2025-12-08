@description('Azure region for resources')
param location string

@description('Base name for resources')
param baseName string

@description('Timestamp for unique naming')
param timestamp string

@description('SQL Server administrator Object ID')
param adminObjectId string

@description('SQL Server administrator login name')
param adminLogin string

@description('SQL Server administrator principal type (User or Application)')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

var sqlServerName = toLower('sql-${baseName}-${timestamp}')
var databaseName = 'Northwind'

resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: adminLogin
      sid: adminObjectId
      tenantId: subscription().tenantId
      principalType: adminPrincipalType
    }
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
    catalogCollation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: false
    readScale: 'Disabled'
  }
}

resource firewallRule 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = sqlDatabase.name
