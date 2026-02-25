// ============================================================
// GenAI Module (Conditional)
// Creates Azure OpenAI (Sweden Central) and Azure AI Search.
// Deploys a GPT-4o model deployment with Standard SKU.
// Grants the managed identity the "Cognitive Services OpenAI
// User" role so it can call OpenAI without API keys.
//
// This module is deployed conditionally via:
//   module genai '...' = if (deployGenAI) { ... }
// ============================================================

@description('Base name used to generate resource names.')
param baseName string

@description('Principal ID of the user-assigned managed identity (for role assignments).')
param managedIdentityPrincipalId string

@description('Azure region for AI Search (defaults to UK South to match other resources).')
param searchLocation string = 'uksouth'

// ── Resource Names ────────────────────────────────────────────
// toLower() is required — uniqueString() can produce uppercase characters,
// and Azure OpenAI requires customSubDomainName to be lowercase.
var uniqueSuffix   = uniqueString(resourceGroup().id)
var openAIName     = toLower('oai-${baseName}-${uniqueSuffix}')
var searchName     = toLower('srch-${baseName}-${uniqueSuffix}')
var modelName      = 'gpt-4o'
var modelVersion   = '2024-11-20'
var deploymentName = 'gpt-4o'

// ── Azure OpenAI (Sweden Central) ────────────────────────────
resource openAI 'Microsoft.CognitiveServices/accounts@2024-04-01-preview' = {
  name: openAIName
  // Sweden Central has better GPT-4o quota for proof-of-concept projects
  location: 'swedencentral'
  sku: {
    name: 'S0'
  }
  kind: 'OpenAI'
  properties: {
    customSubDomainName: openAIName
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
    }
  }
}

// ── GPT-4o Model Deployment ───────────────────────────────────
resource openAIDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-04-01-preview' = {
  name: deploymentName
  parent: openAI
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

// ── Azure AI Search ───────────────────────────────────────────
resource aiSearch 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchName
  location: searchLocation
  sku: {
    name: 'basic'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
    publicNetworkAccess: 'enabled'
    authOptions: {
      aadOrApiKey: {
        aadAuthFailureMode: 'http401WithBearerChallenge'
      }
    }
  }
}

// ── Role Assignment: Cognitive Services OpenAI User ───────────
// Grants the managed identity permission to call Azure OpenAI
// without an API key (uses Managed Identity auth instead).
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

// ── Outputs ──────────────────────────────────────────────────
@description('Endpoint URL of the Azure OpenAI instance.')
output openAIEndpoint string = openAI.properties.endpoint

@description('Name of the deployed GPT-4o model (used as the deployment name in API calls).')
output openAIModelName string = openAIDeployment.name

@description('Resource name of the Azure OpenAI account.')
output openAIName string = openAI.name

@description('Endpoint URL of the Azure AI Search service.')
output searchEndpoint string = 'https://${aiSearch.name}.search.windows.net'
