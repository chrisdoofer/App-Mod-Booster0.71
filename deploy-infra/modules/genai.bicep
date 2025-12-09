// GenAI Resources Module
// Creates Azure OpenAI and AI Search resources

@description('Azure region for the GenAI resources')
param location string

@description('Base name for the resources')
param baseName string

@description('Principal ID of the managed identity for role assignments')
param managedIdentityPrincipalId string

var uniqueSuffix = uniqueString(resourceGroup().id)
var openAIName = toLower('oai-${baseName}-${uniqueSuffix}')
var searchName = toLower('srch-${baseName}-${uniqueSuffix}')
var modelDeploymentName = 'gpt-4o'

// Azure OpenAI - deployed in Sweden Central for better GPT-4o quota availability
resource openAI 'Microsoft.CognitiveServices/accounts@2023-10-01-preview' = {
  name: openAIName
  location: 'swedencentral' // Sweden Central for GPT-4o availability
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
  }
}

// GPT-4o model deployment
resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-10-01-preview' = {
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

// Azure AI Search
resource search 'Microsoft.Search/searchServices@2023-11-01' = {
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

// Role assignment: Cognitive Services OpenAI User for the managed identity
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

// Role assignment: Search Index Data Reader for the managed identity
var searchIndexDataReaderRoleId = '1407120a-92aa-4202-b7e9-c0e197c71c8f'

resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(search.id, managedIdentityPrincipalId, searchIndexDataReaderRoleId)
  scope: search
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataReaderRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

@description('The endpoint of the Azure OpenAI service')
output openAIEndpoint string = openAI.properties.endpoint

@description('The name of the model deployment')
output openAIModelName string = modelDeployment.name

@description('The name of the Azure OpenAI resource')
output openAIName string = openAI.name

@description('The endpoint of the Azure AI Search service')
output searchEndpoint string = 'https://${search.name}.search.windows.net'

@description('The name of the Azure AI Search resource')
output searchName string = search.name
