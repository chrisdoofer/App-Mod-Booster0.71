using './main.bicep'

param location = 'uksouth'
param baseName = 'expensemgmt'
param sqlAdminObjectId = ''  // Set at deployment time
param sqlAdminLogin = ''      // Set at deployment time
param adminPrincipalType = 'User'
param deployGenAI = false
