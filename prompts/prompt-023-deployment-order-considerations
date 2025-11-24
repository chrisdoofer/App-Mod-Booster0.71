Follow this deployment order including the 30 second wait to ensure each step has the resource before it ready:

1. Deploy the resource group, App Service, and SQL Database (currently running)
2. Configure App Service settings
3. Wait 30 seconds for SQL Server to be fully ready
4. Add IP for the local user’s machine to the firewall
5. Install Python dependencies
6. Import the database schema
7. Configure database roles for the managed identity
8. Deploy the application code

Also use uniqueString(resourceGroup().id) to make the resources uniue and do not use timestamps such as utcNow for uniqueness.

Also bear in mind these items:

The Bicep deployment was failing due to several issues: First, the utcNow() function was being used in a variable declaration in app-service.bicep, which isn't allowed - it can only be used as a parameter default value, so I switched to static naming instead. Second, the main.bicep file was missing the adminObjectId and adminLogin parameters that the deployment script was trying to pass for SQL Server Entra ID authentication, and these weren't being forwarded to the azure-sql module. Third, the deployment script had incorrect syntax trying to pass azure-sql.bicep as a parameter rather than passing the actual parameter values to the main template. Fourth, the conditional GenAI module outputs needed null-safe operators (?. and ??) to prevent errors when the module isn't deployed. Finally, azure-sql.bicep was missing the managedIdentityPrincipalId parameter declaration that main.bicep was trying to pass. These fixes ensure proper parameter flow, deterministic resource naming, and safe handling of conditional resources.

