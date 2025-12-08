@description('Azure region for the managed identity')
param location string

@description('Base name for resources')
param baseName string

@description('Timestamp for unique naming')
param timestamp string

var managedIdentityName = 'mid-${baseName}-${timestamp}'

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: toLower(managedIdentityName)
  location: location
}

@description('The resource ID of the managed identity')
output managedIdentityId string = managedIdentity.id

@description('The principal ID of the managed identity')
output managedIdentityPrincipalId string = managedIdentity.properties.principalId

@description('The client ID of the managed identity')
output managedIdentityClientId string = managedIdentity.properties.clientId

@description('The name of the managed identity')
output managedIdentityName string = managedIdentity.name
