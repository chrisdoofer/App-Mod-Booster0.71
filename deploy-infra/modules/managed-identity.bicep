@description('The Azure region where resources will be deployed')
param location string

@description('Base name for resources')
param baseName string

@description('Timestamp for unique naming (must be passed as parameter)')
param timestamp string

var managedIdentityName = 'mid-${baseName}-${timestamp}'

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

@description('The resource ID of the managed identity')
output managedIdentityId string = managedIdentity.id

@description('The client ID of the managed identity')
output managedIdentityClientId string = managedIdentity.properties.clientId

@description('The principal ID of the managed identity')
output managedIdentityPrincipalId string = managedIdentity.properties.principalId

@description('The name of the managed identity')
output managedIdentityName string = managedIdentity.name
