using './main.bicep'

param location = 'uksouth'
param baseName = 'expensemgmt'
param adminObjectId = '' // Will be set by deployment script
param adminLogin = '' // Will be set by deployment script
param adminPrincipalType = 'User'
param deployGenAI = false
