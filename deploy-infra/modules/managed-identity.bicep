@description('Azure region for resources')
param location string

@description('Base name for resources')
param baseName string

@description('Timestamp for unique naming')
param timestamp string

var managedIdentityName = 'mid-${baseName}-${timestamp}'

resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: managedIdentityName
  location: location
}

output managedIdentityId string = managedIdentity.id
output managedIdentityClientId string = managedIdentity.properties.clientId
output managedIdentityPrincipalId string = managedIdentity.properties.principalId
output managedIdentityName string = managedIdentity.name
