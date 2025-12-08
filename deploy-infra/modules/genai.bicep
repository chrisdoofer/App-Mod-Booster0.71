@description('Azure region for GenAI resources')
param location string

@description('Base name for resources')
param baseName string

@description('Principal ID of the managed identity for role assignments')
param managedIdentityPrincipalId string

// Azure OpenAI should be in Sweden Central for better quota availability
var openAILocation = 'swedencentral'
var openAIName = 'oai-${baseName}-${uniqueString(resourceGroup().id)}'
var aiSearchName = 'search-${baseName}-${uniqueString(resourceGroup().id)}'
var modelDeploymentName = 'gpt-4o'

resource openAI 'Microsoft.CognitiveServices/accounts@2024-04-01-preview' = {
  name: toLower(openAIName)
  location: openAILocation
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: toLower(openAIName)
    publicNetworkAccess: 'Enabled'
  }
}

resource gpt4oDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-04-01-preview' = {
  parent: openAI
  name: modelDeploymentName
  sku: {
    name: 'Standard'
    capacity: 8
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4o'
      version: '2024-08-06'
    }
  }
}

resource aiSearch 'Microsoft.Search/searchServices@2024-03-01-preview' = {
  name: toLower(aiSearchName)
  location: location
  sku: {
    name: 'basic'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
  }
}

// Role assignment for Cognitive Services OpenAI User
var cognitiveServicesOpenAIUserRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd')

resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAI.id, managedIdentityPrincipalId, cognitiveServicesOpenAIUserRole)
  scope: openAI
  properties: {
    roleDefinitionId: cognitiveServicesOpenAIUserRole
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Role assignment for Search Index Data Contributor
var searchIndexDataContributorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '8ebe5a00-799e-43f5-93ac-243d3dce84a7')

resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiSearch.id, managedIdentityPrincipalId, searchIndexDataContributorRole)
  scope: aiSearch
  properties: {
    roleDefinitionId: searchIndexDataContributorRole
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

@description('The endpoint for Azure OpenAI')
output openAIEndpoint string = openAI.properties.endpoint

@description('The name of the OpenAI model deployment')
output openAIModelName string = gpt4oDeployment.name

@description('The name of the Azure OpenAI resource')
output openAIName string = openAI.name

@description('The endpoint for Azure AI Search')
output aiSearchEndpoint string = 'https://${aiSearch.name}.search.windows.net'

@description('The name of the Azure AI Search resource')
output aiSearchName string = aiSearch.name
