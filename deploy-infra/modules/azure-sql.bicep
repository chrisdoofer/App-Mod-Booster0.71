// ============================================================
// Azure SQL Module
// Creates an Azure SQL Server and the Northwind database using
// Entra ID-only authentication (no SQL passwords).
//
// The sqlAdminPassword parameter is required by the ARM API
// even when Entra ID-only authentication is enabled; it is
// never used for actual access.
// ============================================================

@description('Azure region for the resources.')
param location string

@description('Base name used to generate resource names.')
param baseName string

@description('Object ID (principal ID) of the Entra ID admin user or service principal.')
param adminObjectId string

@description('UPN or display name of the Entra ID administrator.')
param adminUserPrincipalName string

@description('Type of the Entra ID principal being set as administrator.')
@allowed(['User', 'Application'])
param adminPrincipalType string = 'User'

@description('Dummy password required by the SQL Server API even when using Entra ID-only auth. Never used for access.')
@secure()
param sqlAdminPassword string = newGuid()

// ── Resource Names ───────────────────────────────────────────
var uniqueSuffix  = uniqueString(resourceGroup().id)
var sqlServerName = toLower('sql-${baseName}-${uniqueSuffix}')
var databaseName  = 'Northwind'

// ── SQL Server ───────────────────────────────────────────────
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    // Dummy credentials — Entra ID-only auth means these are never used
    administratorLogin: 'sqladmin'
    administratorLoginPassword: sqlAdminPassword
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: adminPrincipalType
      login: adminUserPrincipalName
      sid: adminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// ── Firewall: Allow Azure Services ───────────────────────────
resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = {
  name: 'AllowAllWindowsAzureIps'
  parent: sqlServer
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// ── Northwind Database (Basic tier) ──────────────────────────
resource northwindDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  name: databaseName
  parent: sqlServer
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: 2147483648
    zoneRedundant: false
  }
}

// ── Outputs ──────────────────────────────────────────────────
@description('Fully-qualified domain name of the SQL Server.')
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('Resource name of the SQL Server.')
output sqlServerName string = sqlServer.name

@description('Name of the Northwind database.')
output databaseName string = northwindDatabase.name
