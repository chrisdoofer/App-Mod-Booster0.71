@description('Location for Gen AI resources - must be Sweden Central for GPT-4o')
param location string = 'swedencentral'

@description('Base name for resource naming')
param baseName string

@description('Managed Identity Principal ID for role assignments')
param managedIdentityPrincipalId string

var uniqueSuffix = uniqueString(resourceGroup().id)
var openAIName = toLower('oai-${baseName}-${uniqueSuffix}')
var searchServiceName = toLower('srch-${baseName}-${uniqueSuffix}')
var modelName = 'gpt-4o'
var modelDeploymentName = 'gpt-4o'

// Azure OpenAI Account
resource openAIAccount 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: openAIName
  location: location
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
  }
}

// Deploy GPT-4o model
resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: openAIAccount
  name: modelDeploymentName
  sku: {
    name: 'Standard'
    capacity: 8
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: '2024-08-06'
    }
  }
}

// Azure AI Search
resource searchService 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchServiceName
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

// Role assignment for Cognitive Services OpenAI User
var cognitiveServicesOpenAIUserRoleId = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'
resource openAIRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(openAIAccount.id, managedIdentityPrincipalId, cognitiveServicesOpenAIUserRoleId)
  scope: openAIAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', cognitiveServicesOpenAIUserRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// Role assignment for Search Index Data Contributor
var searchIndexDataContributorRoleId = '8ebe5a00-799e-43f5-93ac-243d3dce84a7'
resource searchRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(searchService.id, managedIdentityPrincipalId, searchIndexDataContributorRoleId)
  scope: searchService
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', searchIndexDataContributorRoleId)
    principalId: managedIdentityPrincipalId
    principalType: 'ServicePrincipal'
  }
}

@description('The endpoint URL for Azure OpenAI')
output openAIEndpoint string = openAIAccount.properties.endpoint

@description('The name of the deployed OpenAI model')
output openAIModelName string = modelDeploymentName

@description('The endpoint URL for Azure AI Search')
output searchServiceEndpoint string = 'https://${searchService.name}.search.windows.net'

@description('The name of the Azure AI Search service')
output searchServiceName string = searchService.name
