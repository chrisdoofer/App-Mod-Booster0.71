@description('Location for Azure SQL resources')
param location string

@description('Base name for resource naming')
param baseName string

@description('SQL Administrator Object ID (from Entra ID)')
param sqlAdminObjectId string

@description('SQL Administrator login name (UPN or Service Principal name)')
param sqlAdminLogin string

@description('SQL Administrator principal type')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

@description('SQL Server administrator password (required by API even with AD-only auth)')
@secure()
param sqlAdminPassword string = newGuid()

var uniqueSuffix = uniqueString(resourceGroup().id)
var sqlServerName = toLower('sql-${baseName}-${uniqueSuffix}')
var databaseName = 'Northwind'

resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: 'sqladmin'
    administratorLoginPassword: sqlAdminPassword
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: adminPrincipalType
      login: sqlAdminLogin
      sid: sqlAdminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlFirewallRule 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
  }
}

@description('The fully qualified domain name of the SQL Server')
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('The name of the SQL Server')
output sqlServerName string = sqlServer.name

@description('The name of the database')
output databaseName string = database.name

@description('The resource ID of the SQL Server')
output sqlServerId string = sqlServer.id

@description('The resource ID of the database')
output databaseId string = database.id
