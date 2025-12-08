@description('Azure region for the SQL resources')
param location string

@description('Base name for resources')
param baseName string

@description('The Object ID of the Azure AD administrator')
param adminObjectId string

@description('The User Principal Name of the Azure AD administrator')
param adminUpn string

@description('The principal type of the administrator (User or Application)')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

var sqlServerName = 'sql-${baseName}-${uniqueString(resourceGroup().id)}'
var databaseName = 'Northwind'

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: toLower(sqlServerName)
  location: location
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: adminPrincipalType
      login: adminUpn
      sid: adminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
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

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

@description('The fully qualified domain name of the SQL server')
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('The name of the SQL server')
output sqlServerName string = sqlServer.name

@description('The name of the database')
output databaseName string = sqlDatabase.name

@description('The resource ID of the SQL database')
output databaseId string = sqlDatabase.id
