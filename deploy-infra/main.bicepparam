using './main.bicep'

@description('Azure region for all resources')
param location = 'uksouth'

@description('Base name for all resources')
param baseName = 'expensemgmt'

@description('Object ID of the Azure AD administrator - set by deployment script')
param adminObjectId = ''

@description('User Principal Name of the Azure AD administrator - set by deployment script')
param adminUpn = ''

@description('Principal type for SQL admin (User for interactive, Application for CI/CD)')
param adminPrincipalType = 'User'

@description('Deploy GenAI resources (Azure OpenAI and AI Search)')
param deployGenAI = false
