@description('The Azure region where resources will be deployed')
param location string

@description('Base name for resources')
param baseName string

@description('Unique suffix for resource names')
param uniqueSuffix string

@description('The Object ID of the Azure AD admin')
param adminObjectId string

@description('The User Principal Name or Display Name of the Azure AD admin')
param adminUsername string

@description('The principal type (User or Application) for the admin')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

var sqlServerName = toLower('sql-${baseName}-${uniqueSuffix}')
var databaseName = 'Northwind'

resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: {
      administratorType: 'ActiveDirectory'
      login: adminUsername
      sid: adminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
      principalType: adminPrincipalType
    }
  }
}

resource sqlServerFirewallRuleAzure 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
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
    maxSizeBytes: 2147483648 // 2GB
    catalogCollation: 'SQL_Latin1_General_CP1_CI_AS'
    zoneRedundant: false
  }
}

@description('The fully qualified domain name of the SQL server')
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('The name of the SQL server')
output sqlServerName string = sqlServer.name

@description('The name of the database')
output databaseName string = database.name
