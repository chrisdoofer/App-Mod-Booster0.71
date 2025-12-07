using './main.bicep'

param location = 'uksouth'
param baseName = 'expensemgmt'
// adminObjectId and adminUsername must be provided at deployment time
// adminPrincipalType defaults to 'User' but can be overridden for CI/CD
// deployGenAI defaults to false
