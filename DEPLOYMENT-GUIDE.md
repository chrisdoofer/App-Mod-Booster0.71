# Deployment Order and Troubleshooting Guide

This guide documents important deployment considerations, common issues, and their solutions.

## ✅ Pre-Deployment Checklist

Before starting deployment, ensure:

- [ ] Azure CLI is installed and up to date (`az --version`)
- [ ] PowerShell 7+ is installed (recommended, works with 5.1)
- [ ] go-sqlcmd is installed (`winget install sqlcmd`)
- [ ] .NET 8 SDK is installed (`dotnet --version`)
- [ ] You're logged in to Azure (`az login`)
- [ ] You have appropriate Azure permissions (Contributor or Owner)

## 🎯 Deployment Phases

### Phase 1: Infrastructure Deployment

**What it does:**
1. Creates Azure resource group
2. Deploys Bicep templates (managed identity, App Service, SQL, monitoring)
3. Waits for SQL Server to be ready
4. Adds your IP to SQL firewall (local deployment only)
5. Imports database schema
6. Creates managed identity database user with SID-based authentication
7. Grants database permissions (db_datareader, db_datawriter, EXECUTE)
8. Imports stored procedures
9. Configures App Service with critical settings
10. Saves deployment context file

**Critical Settings Configured:**
- `ConnectionStrings__DefaultConnection` - SQL connection string with MI auth
- `AZURE_CLIENT_ID` - Managed identity client ID
- `ManagedIdentityClientId` - For explicit MI credential usage
- `APPLICATIONINSIGHTS_CONNECTION_STRING` - Telemetry
- GenAI settings (if `-DeployGenAI` used)

**Why this matters:** Without these settings, the application cannot connect to the database even if all resources exist.

### Phase 2: Application Deployment

**What it does:**
1. Reads deployment context file
2. Builds .NET 8 application
3. Creates deployment package with correct structure
4. Deploys to Azure App Service
5. Restarts the application

**Package Structure:** DLL files must be at the root of the zip, not in a subdirectory.

## 🔥 Common Pitfalls and Solutions

### 1. Resource Group Reuse Issues

**Problem:** Reusing a resource group with partially deployed resources can cause ARM caching issues, especially with Log Analytics Workspace references.

**Error Message:**
```
Could not retrieve the Log Analytics workspace from ARM
```

**Solution:**
- Always use fresh resource group names
- Include date/time suffix: `rg-expensemgmt-20251208`
- If deployment fails, delete the entire resource group before retrying

### 2. PowerShell Script-to-Script Parameter Passing

**Problem:** Using array splatting instead of hashtable splatting causes parameter binding errors.

**Wrong:**
```powershell
$args = @("-ResourceGroup", $ResourceGroup, "-Location", $Location)
& $script @args
```

**Correct:**
```powershell
$args = @{
    ResourceGroup = $ResourceGroup
    Location = $Location
}
& $script @args
```

**Error Message:**
```
A positional parameter cannot be found that accepts argument
```

### 3. Azure CLI Parameter Passing

**Problem:** Cannot pass PowerShell hashtables directly to `az deployment group create --parameters`.

**Wrong:**
```powershell
$params = @{ location = $Location; baseName = $BaseName }
az deployment group create --parameters $params
```

**Correct:**
```powershell
az deployment group create --parameters location=$Location baseName=$BaseName
```

**Error Message:**
```
Unable to parse parameter: System.Collections.Hashtable
```

### 4. Azure CLI JSON Output with Warnings

**Problem:** Merging stderr with stdout corrupts JSON output when Bicep has warnings.

**Wrong:**
```powershell
$output = az deployment group create --output json 2>&1
$deployment = $output | ConvertFrom-Json
```

**Correct:**
```powershell
$output = az deployment group create --output json 2>$null
$deployment = $output | ConvertFrom-Json
```

**Error Message:**
```
Conversion from JSON failed with error
```

### 5. Missing Chat Page Files

**Problem:** The Chat page files are not created, causing 404 errors.

**Files Required:**
- `src/ExpenseManagement/Pages/Chat.cshtml`
- `src/ExpenseManagement/Pages/Chat.cshtml.cs`
- `src/ExpenseManagement/Services/ChatService.cs`

**Solution:** These files must always exist, even without GenAI. They should show a friendly "not configured" message when GenAI isn't deployed.

### 6. Bicep utcNow() Usage

**Problem:** Using `utcNow()` in variable declarations causes Bicep compilation errors.

**Wrong:**
```bicep
var timestamp = utcNow('yyyyMMddHHmm')
```

**Correct:**
```bicep
param timestamp string = utcNow('yyyyMMddHHmm')
```

**Error Message:**
```
BCP065: Function "utcNow" is not valid at this location
```

### 7. SQL Connection String Format

**Problem:** Incorrect connection string authentication method.

**Wrong:**
```
Server=...;Trusted_Connection=True;
```

**Correct:**
```
Server=tcp:{server}.database.windows.net,1433;Initial Catalog=Northwind;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity;User Id={managedIdentityClientId};
```

**Critical:** The `User Id` parameter must be the managed identity **Client ID**, not the Object ID or Principal ID.

### 8. Azure Resource Naming

**Problem:** Some Azure services require lowercase names.

**Services Requiring Lowercase:**
- Azure OpenAI
- Azure AI Search
- Storage accounts

**Solution:**
```bicep
var openAIName = toLower('oai-${baseName}-${uniqueSuffix}')
```

**Error Message:**
```
Azure OpenAI requires the customSubDomainName property to be lowercase
```

### 9. Sqlcmd Piping Issues

**Problem:** Piping SQL directly to sqlcmd causes go-sqlcmd to crash with nil pointer errors.

**Wrong:**
```powershell
$sql | sqlcmd -S $server -d $database ...
```

**Correct:**
```powershell
$tempFile = [System.IO.Path]::GetTempFileName() + ".sql"
$sql | Out-File -FilePath $tempFile -Encoding UTF8
sqlcmd -S $server -d $database -i $tempFile
Remove-Item -Path $tempFile -Force
```

### 10. C# Column Name Alignment

**Problem:** Mismatched column names between stored procedures and C# code.

**Example:** If stored procedure returns `AmountDecimal`, C# must use:
```csharp
Amount = reader.GetDecimal(reader.GetOrdinal("AmountDecimal"))
```

Not:
```csharp
Amount = reader.GetDecimal(reader.GetOrdinal("Amount"))
```

**Error Message:**
```
Unable to cast object of type 'System.DBNull' to type 'System.Decimal'
```
or
```
Column not found: Amount
```

## 🔧 Environment-Specific Considerations

### Local Development

**Authentication:** Uses `ActiveDirectoryDefault` which authenticates via:
1. Azure CLI (`az login`)
2. Visual Studio credentials
3. Environment variables

**Firewall:** Script automatically adds your IP to SQL Server firewall.

### CI/CD (GitHub Actions)

**Authentication:** Uses `ActiveDirectoryAzCli` to avoid conflicts with OIDC environment variables.

**Why?** `ActiveDirectoryDefault` tries `EnvironmentCredential` first, which sees `AZURE_CLIENT_ID` and `AZURE_TENANT_ID` but no `AZURE_CLIENT_SECRET`, causing "Identity not found" errors.

**Service Principal Roles Required:**
1. **Contributor** - Create and manage resources
2. **User Access Administrator** - Create role assignments in Bicep (for MI access to OpenAI)

## 📋 Deployment Order

### Correct Order:

1. **Managed Identity** - Created first, needed by everything
2. **Monitoring** - Log Analytics and App Insights (without App Service diagnostics)
3. **App Service** - References Application Insights connection string
4. **Azure SQL** - Created with admin credentials
5. **App Service Diagnostics** - Separate module to avoid circular dependency
6. **GenAI Resources** (optional) - Requires MI principal ID for role assignments

### Circular Dependency Avoidance:

**Problem:** App Service needs App Insights connection string, but Monitoring wants App Service name for diagnostics.

**Solution:**
1. Deploy Monitoring module WITHOUT App Service diagnostics
2. Deploy App Service with App Insights connection string
3. Deploy separate diagnostics module AFTER App Service exists

## 🕐 Timing Considerations

### Wait After Infrastructure

Wait 15-60 seconds between infrastructure and application deployment to allow:
- SQL Server to fully initialize
- Firewall rules to propagate
- Managed identity assignments to replicate
- App Service settings to apply

### CI/CD Timing

GitHub Actions workflow includes 60-second delay between phases to avoid SCM restart conflicts during application deployment.

## 🐛 Troubleshooting Tools

### Check Deployment Status

```powershell
az deployment group list --resource-group $ResourceGroup --output table
```

### View Deployment Errors

```powershell
az deployment group show `
    --resource-group $ResourceGroup `
    --name $DeploymentName `
    --query "properties.error"
```

### Test SQL Connection

```powershell
sqlcmd -S $serverFqdn -d Northwind "--authentication-method=ActiveDirectoryDefault" -Q "SELECT @@VERSION"
```

### Check App Service Logs

```powershell
az webapp log tail --resource-group $ResourceGroup --name $WebAppName
```

### View Managed Identity Permissions

```powershell
az role assignment list `
    --assignee $managedIdentityPrincipalId `
    --output table
```

## 📚 Reference: Working Scripts

If uncertain about implementation patterns, check these reference files:

- `deploy-all.ps1` - Correct hashtable splatting
- `deploy-infra/deploy.ps1` - Azure CLI parameter passing and stderr handling
- `src/ExpenseManagement/Pages/Chat.cshtml` - Graceful GenAI degradation
- `stored-procedures.sql` - Column naming conventions

## 🎯 Best Practices

1. **Always use unique resource group names** with timestamps
2. **Never skip the deployment context file** - it's essential for app deployment
3. **Validate Bicep templates** before deploying
4. **Check logs immediately** after deployment for any warnings
5. **Test locally first** before CI/CD deployment
6. **Use fresh terminals** if PATH issues occur with sqlcmd
7. **Document any custom changes** you make to the scripts

## 🚨 When Things Go Wrong

If deployment fails:

1. **Check the error message carefully** - most errors are explicit
2. **Review the troubleshooting section** for that specific error
3. **Delete the resource group** if partially deployed
4. **Use a new resource group name** for retry
5. **Check Azure Portal** for any resources that weren't cleaned up
6. **Review deployment logs** in Azure Portal under Deployments
7. **Enable verbose logging** in PowerShell: `$VerbosePreference = 'Continue'`

## ✅ Success Indicators

Deployment succeeded if:

- [ ] All Bicep deployments show "Succeeded" status
- [ ] `.deployment-context.json` file exists at repo root
- [ ] App Service shows "Running" status in Azure Portal
- [ ] Can access `/Index` page in browser
- [ ] Swagger UI loads at `/swagger`
- [ ] Database connection works (no error message on dashboard)
- [ ] Application Insights shows telemetry within 5 minutes

---

**Remember:** Most issues stem from incorrect timing, wrong parameter passing, or resource group reuse. Following the patterns in this guide will help avoid 90% of common problems.
