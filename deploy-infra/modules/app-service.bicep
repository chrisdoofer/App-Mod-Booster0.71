// ============================================================
// App Service Module
// Creates an Azure App Service Plan (Standard S1) and Web App
// in UK South. Assigns the user-assigned managed identity so
// the application can authenticate to Azure SQL and other
// Azure services without storing secrets.
// ============================================================

@description('Azure region for the resources.')
param location string

@description('Base name used to generate resource names.')
param baseName string

@description('Resource ID of the user-assigned managed identity.')
param managedIdentityId string

@description('Application Insights connection string.')
param appInsightsConnectionString string

@description('Client ID of the user-assigned managed identity (AZURE_CLIENT_ID).')
param azureClientId string

// ── Resource Names ───────────────────────────────────────────
var uniqueSuffix  = uniqueString(resourceGroup().id)
var planName      = toLower('plan-${baseName}-${uniqueSuffix}')
var webAppName    = toLower('app-${baseName}-${uniqueSuffix}')

// ── App Service Plan (Standard S1) ───────────────────────────
resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: planName
  location: location
  sku: {
    name: 'S1'
    tier: 'Standard'
    size: 'S1'
    family: 'S'
    capacity: 1
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

// ── App Service (Web App) ─────────────────────────────────────
resource webApp 'Microsoft.Web/sites@2022-09-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityId}': {}
    }
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      healthCheckPath: '/health'
      http20Enabled: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'AZURE_CLIENT_ID'
          value: azureClientId
        }
      ]
    }
  }
}

// ── Outputs ──────────────────────────────────────────────────
@description('Resource ID of the App Service.')
output webAppId string = webApp.id

@description('Resource name of the App Service.')
output webAppName string = webApp.name

@description('Default hostname of the App Service.')
output defaultHostName string = webApp.properties.defaultHostName

@description('Principal ID of the user-assigned managed identity on the App Service (for role assignments).')
output managedIdentityPrincipalId string = webApp.identity.userAssignedIdentities[managedIdentityId].principalId
