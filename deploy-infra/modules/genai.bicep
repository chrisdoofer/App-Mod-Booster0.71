@description('The Azure region where resources will be deployed')
param location string

@description('Base name for resources')
param baseName string

@description('Unique suffix for resource names')
param uniqueSuffix string

@description('The principal ID of the managed identity for role assignments')
param managedIdentityPrincipalId string

// Azure OpenAI must be lowercase
var openAIName = toLower('oai-${baseName}-${uniqueSuffix}')
// AI Search must be lowercase
var searchName = toLower('srch-${baseName}-${uniqueSuffix}')
var openAIModelName = 'gpt-4o'
var openAIDeploymentName = 'gpt-4o'

resource openAI 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: openAIName
  location: 'swedencentral' // Sweden Central has better quota availability
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
  }
}

resource openAIDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAI
  name: openAIDeploymentName
  sku: {
    name: 'Standard'
    capacity: 8
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: openAIModelName
      version: '2024-08-06'
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
    publicNetworkAccess: 'enabled'
  }
}

// Role assignment for Cognitive Services OpenAI User
var cognitiveServicesOpenAIUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAI.id, managedIdentityPrincipalId, cognitiveServicesOpenAIUserRoleId)
  scope: openAI
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAIUserRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Role assignment for Search Index Data Contributor
var searchIndexDataContributorRoleId = '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiSearch.id, managedIdentityPrincipalId, searchIndexDataContributorRoleId)
  scope: aiSearch
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataContributorRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

@description('The endpoint for Azure OpenAI')
output openAIEndpoint string = openAI.properties.endpoint

@description('The name of the OpenAI model deployment')
output openAIModelName string = openAIDeploymentName

@description('The name of the Azure OpenAI resource')
output openAIName string = openAI.name

@description('The endpoint for Azure AI Search')
output searchEndpoint string = 'https://${aiSearch.name}.search.windows.net'

@description('The name of the Azure AI Search resource')
output searchName string = aiSearch.name
