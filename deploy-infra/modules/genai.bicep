@description('Azure region for main resources')
param location string

@description('Base name for resources')
param baseName string

@description('Timestamp for unique naming')
param timestamp string

@description('Managed identity principal ID for role assignments')
param managedIdentityPrincipalId string

var uniqueSuffix = uniqueString(resourceGroup().id)
var openAIName = toLower('oai-${baseName}-${uniqueSuffix}')
var searchName = toLower('srch-${baseName}-${uniqueSuffix}')
var openAILocation = 'swedencentral'
var modelDeploymentName = 'gpt-4o'
var modelName = 'gpt-4o'
var modelVersion = '2024-05-13'

resource openAI 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: openAIName
  location: openAILocation
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
  }
}

resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAI
  name: modelDeploymentName
  sku: {
    name: 'Standard'
    capacity: 8
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: modelVersion
    }
  }
}

resource aiSearch 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchName
  location: location
  sku: {
    name: 'basic'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
  }
}

// Assign Cognitive Services OpenAI User role to managed identity
resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: openAI
  name: guid(openAI.id, managedIdentityPrincipalId, 'CognitiveServicesOpenAIUser')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Assign Search Index Data Reader role to managed identity
resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: aiSearch
  name: guid(aiSearch.id, managedIdentityPrincipalId, 'SearchIndexDataReader')
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '1407120a-92aa-4202-b7e9-c0e197c71c8f')
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

output openAIEndpoint string = openAI.properties.endpoint
output openAIName string = openAI.name
output openAIModelName string = modelDeploymentName
output searchEndpoint string = 'https://${aiSearch.name}.search.windows.net'
output searchName string = aiSearch.name
