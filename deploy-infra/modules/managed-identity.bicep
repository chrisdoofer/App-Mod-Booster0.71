@description('Location for the managed identity')
param location string

@description('Base name for resource naming')
param baseName string

@description('Timestamp for unique naming')
param timestamp string

var managedIdentityName = toLower('mid-${baseName}-${timestamp}')

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

@description('The name of the managed identity')
output name string = managedIdentity.name

@description('The resource ID of the managed identity')
output id string = managedIdentity.id

@description('The principal ID (object ID) of the managed identity')
output principalId string = managedIdentity.properties.principalId

@description('The client ID of the managed identity')
output clientId string = managedIdentity.properties.clientId
