// ============================================================
// Managed Identity Module
// Creates a User-Assigned Managed Identity for the application
// to authenticate to Azure SQL and other Azure services
// without storing passwords or secrets.
// ============================================================

@description('Azure region for the resource.')
param location string

@description('Base name used to generate the identity name.')
param baseName string

@description('Deployment timestamp used to create a unique identity name.')
param timestamp string = utcNow('yyyyMMddHHmm')

// ── Resource Names ───────────────────────────────────────────
var identityName = 'mid-AppModAssist-${timestamp}'

// ── User-Assigned Managed Identity ──────────────────────────
// utcNow-based name is intentionally non-deterministic — a new identity
// is created per deployment to avoid resource name collisions across runs.
#disable-next-line use-stable-resource-identifiers
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
  tags: {
    application: baseName
    deployedAt: timestamp
  }
}

// ── Outputs ──────────────────────────────────────────────────
@description('Resource ID of the managed identity.')
output managedIdentityId string = managedIdentity.id

@description('Client ID (Application ID) of the managed identity.')
output managedIdentityClientId string = managedIdentity.properties.clientId

@description('Principal ID (Object ID) of the managed identity — used for role assignments.')
output managedIdentityPrincipalId string = managedIdentity.properties.principalId

@description('Resource name of the managed identity.')
output managedIdentityName string = managedIdentity.name
