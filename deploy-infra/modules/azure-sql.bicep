// Azure SQL Module
// Creates Azure SQL Server and Database with Entra ID-only authentication

@description('Azure region for the SQL resources')
param location string

@description('Base name for the resources')
param baseName string

@description('Admin User Object ID (from Entra ID)')
param adminObjectId string

@description('Admin User Principal Name or App Display Name')
param adminLogin string

@description('Principal type for SQL admin (User for interactive, Application for Service Principal)')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

@description('Placeholder password required by API even with AD-only auth')
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
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      login: adminLogin
      sid: adminObjectId
      principalType: adminPrincipalType
      azureADOnlyAuthentication: true
      tenantId: subscription().tenantId
    }
  }
}

// Allow Azure services to access the SQL Server
resource firewallRuleAzure 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
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

@description('The fully qualified domain name of the SQL server')
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('The name of the SQL server')
output sqlServerName string = sqlServer.name

@description('The name of the database')
output databaseName string = database.name

@description('The resource ID of the database')
output databaseId string = database.id
