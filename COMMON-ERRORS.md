# Common Errors and How to Prevent Them

This document highlights the most common errors encountered during the App Mod Booster project and provides clear guidance on how to prevent them.

## Table of Contents
1. [PowerShell Errors](#powershell-errors)
2. [Testing Errors](#testing-errors)
3. [Bicep Template Errors](#bicep-template-errors)
4. [Database Errors](#database-errors)
5. [Configuration Errors](#configuration-errors)

---

## PowerShell Errors

### ❌ Error: "A positional parameter cannot be found"

**Cause:** Using array splatting instead of hashtable splatting when passing parameters between scripts.

**Bad Code:**
```powershell
$args = @("-ResourceGroup", $ResourceGroup, "-Location", $Location)
& $script @args  # ❌ FAILS!
```

**Good Code:**
```powershell
$args = @{
    ResourceGroup = $ResourceGroup
    Location      = $Location
}
& $script @args  # ✅ WORKS!
```

**Where This Applies:** `deploy-all.ps1` calling child scripts

**Reference:** `prompts/prompt-029-unified-deployment-script`, `prompts/prompt-006-baseline-script-instruction`

---

### ❌ Error: "Unable to parse parameter: System.Collections.Hashtable"

**Cause:** Trying to pass a PowerShell hashtable directly to Azure CLI parameters.

**Bad Code:**
```powershell
$params = @{ location = $Location; baseName = $BaseName }
az deployment group create --parameters $params  # ❌ FAILS!
```

**Good Code:**
```powershell
az deployment group create `
    --parameters location=$Location baseName=$BaseName deployGenAI=$($DeployGenAI.ToString().ToLower())
```

**Where This Applies:** All `az deployment group create` calls

**Reference:** `prompts/prompt-027-deployment-script`

---

### ❌ Error: "Conversion from JSON failed with error: Unexpected character..."

**Cause:** Bicep warnings in stderr corrupt JSON output when using `2>&1` to merge streams.

**Bad Code:**
```powershell
$output = az deployment group create --output json 2>&1  # ❌ Corrupts JSON!
$deployment = $output | ConvertFrom-Json  # FAILS!
```

**Good Code:**
```powershell
$output = az deployment group create --output json 2>$null  # ✅ Discards warnings
$deployment = $output | ConvertFrom-Json  # WORKS!
```

**Where This Applies:** All Azure CLI commands that output JSON for parsing

**Reference:** `prompts/prompt-027-deployment-script`

---

### ❌ Error: sqlcmd crashes with nil pointer panic

**Cause:** Piping SQL directly to sqlcmd instead of using a file.

**Bad Code:**
```powershell
$sql | sqlcmd -S $server -d $db ...  # ❌ CRASHES!
```

**Good Code:**
```powershell
$tempFile = [System.IO.Path]::GetTempFileName() + ".sql"
$sql | Out-File -FilePath $tempFile -Encoding UTF8
sqlcmd -S $server -d $db "--authentication-method=$authMethod" -i $tempFile
Remove-Item -Path $tempFile -Force -ErrorAction SilentlyContinue
```

**Where This Applies:** All sqlcmd invocations in deployment scripts

**Reference:** `prompts/prompt-016-sqlcmd-for-sql`, `prompts/prompt-024-sqlcmd-stored-procedures`

---

## Testing Errors

### ❌ Error: "Program is inaccessible due to its protection level"

**Cause:** The `Program` class is internal by default and cannot be accessed by test projects using `WebApplicationFactory<Program>`.

**Solution:** Add this line at the bottom of `Program.cs`:

```csharp
// Make Program class accessible to tests
public partial class Program { }
```

**Where This Applies:** `src/ExpenseManagement/Program.cs`

**Reference:** `prompts/prompt-031-testing-lessons-learned` (section 1)

---

### ❌ Error: Testing throws exception when service has graceful fallback

**Cause:** Testing for exceptions instead of testing the actual fallback behavior.

**Bad Test:**
```csharp
[Fact]
public void Service_WithMissingConfig_ThrowsException()
{
    var service = new ExpenseService(emptyConfig);
    Func<Task> act = async () => await service.GetExpensesAsync();
    act.Should().ThrowAsync<InvalidOperationException>();  // ❌ WRONG!
}
```

**Good Test:**
```csharp
[Fact]
public async Task Service_WithMissingConfig_FallsBackToDummyData()
{
    var service = new ExpenseService(emptyConfig, mockLogger);
    var result = await service.GetExpensesAsync();
    result.Should().NotBeNull("service should fall back to dummy data");  // ✅ RIGHT!
}
```

**Where This Applies:** All unit tests for services with fallback behavior

**Reference:** `prompts/prompt-031-testing-lessons-learned` (section 4)

---

### ❌ Error: Bicep validation test fails to detect utcNow() misuse

**Cause:** Checking the previous line for `param` keyword instead of checking the current line.

**Bad Detection:**
```csharp
var inParameterDefault = i > 0 && lines[i - 1].Contains("param ");  // ❌ Unreliable!
```

**Good Detection:**
```csharp
var isParameterLine = line.StartsWith("param ") && line.Contains("utcNow()");  // ✅ Reliable!
```

**Where This Applies:** `tests/ExpenseManagement.Tests/Infrastructure/BicepValidationTests.cs`

**Reference:** `prompts/prompt-031-testing-lessons-learned` (section 6)

---

## Bicep Template Errors

### ❌ Error: "The 'utcNow()' function is only valid in parameter default values"

**Cause:** Using `utcNow()` in variables or resource properties instead of parameter defaults.

**Bad Code:**
```bicep
var timestamp = utcNow()  // ❌ INVALID!
```

**Good Code:**
```bicep
param deploymentTimestamp string = utcNow()  // ✅ VALID!
```

**Where This Applies:** All Bicep templates

**Reference:** `prompts/prompt-030-bicep-best-practices`

---

### ❌ Error: "The 'newGuid()' function is only valid in parameter default values"

**Cause:** Using `newGuid()` directly in resource properties.

**Bad Code:**
```bicep
resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  properties: {
    administratorLoginPassword: newGuid()  // ❌ INVALID!
  }
}
```

**Good Code:**
```bicep
@secure()
param sqlAdminPassword string = newGuid()  // ✅ VALID!

resource sqlServer 'Microsoft.Sql/servers@2021-11-01' = {
  properties: {
    administratorLoginPassword: sqlAdminPassword
  }
}
```

**Where This Applies:** All Bicep templates

**Reference:** `prompts/prompt-030-bicep-best-practices`

---

### ❌ Error: Circular dependency detected in Bicep deployment

**Cause:** App Service diagnostic settings referencing Log Analytics workspace that also references the App Service.

**Solution:** Split diagnostic settings into a separate module that deploys after the App Service and monitoring resources.

**Bad Structure:**
```bicep
// In app-service.bicep
resource appServiceDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: appService
  properties: {
    workspaceId: logAnalyticsWorkspaceId  // ❌ Creates circular dependency
  }
}
```

**Good Structure:**
```bicep
// In main.bicep
module appServiceModule './modules/app-service.bicep' = { ... }
module diagnosticsModule './modules/app-service-diagnostics.bicep' = {
  dependsOn: [appServiceModule, monitoringModule]  // ✅ Explicit ordering
}
```

**Where This Applies:** `deploy-infra/modules/app-service-diagnostics.bicep`

**Reference:** `.github/agents/infra-agent.md` (section on deployment order)

---

## Database Errors

### ❌ Error: "Invalid column name" or "Unable to cast object" in C#

**Cause:** Column names in C# code (`GetOrdinal("ColumnName")`) don't match the stored procedure output column names.

**Example Problem:**
- Stored procedure returns: `Amount AS AmountDecimal`
- C# code tries to read: `reader.GetOrdinal("Amount")` ❌

**Solution:** Use the exact alias from the stored procedure:
```csharp
// Stored procedure: SELECT Amount AS AmountDecimal, Reviewer AS ReviewedByName ...
decimal amount = reader.GetDecimal(reader.GetOrdinal("AmountDecimal"));  // ✅
string reviewer = reader.GetString(reader.GetOrdinal("ReviewedByName"));  // ✅
```

**Where This Applies:** All service classes that read from stored procedures

**Reference:** `.github/agents/dotnet-agent.md` (column alignment section)

---

### ❌ Error: "CREATE USER failed because the 'FROM EXTERNAL PROVIDER' option is not supported"

**Cause:** Using old `FROM EXTERNAL PROVIDER` syntax instead of SID-based user creation for managed identities.

**Bad Code:**
```sql
CREATE USER [my-managed-identity] FROM EXTERNAL PROVIDER;  -- ❌ FAILS!
```

**Good Code:**
```powershell
$guidBytes = [System.Guid]::Parse($managedIdentityClientId).ToByteArray()
$sidHex = "0x" + [System.BitConverter]::ToString($guidBytes).Replace("-", "")

$createUserSql = @"
CREATE USER [$managedIdentityName] WITH SID = $sidHex, TYPE = E;  -- ✅ WORKS!
ALTER ROLE db_datareader ADD MEMBER [$managedIdentityName];
"@
```

**Where This Applies:** `deploy-infra/deploy.ps1` (managed identity user creation)

**Reference:** `prompts/prompt-027-deployment-script`

---

## Configuration Errors

### ❌ Error: Application cannot connect to database even though resources exist

**Cause:** Connection string not configured during infrastructure deployment.

**Solution:** Infrastructure deployment MUST set these App Service settings:

```powershell
az webapp config appsettings set `
    --name $appServiceName `
    --resource-group $ResourceGroup `
    --settings `
    "AZURE_CLIENT_ID=$managedIdentityClientId" `
    "ConnectionStrings__DefaultConnection=Server=$sqlServerFqdn;Database=$sqlDatabaseName;Authentication=Active Directory Managed Identity;User Id=$managedIdentityClientId;" `
    "APPLICATIONINSIGHTS_CONNECTION_STRING=$appInsightsConnectionString"
```

**Where This Applies:** `deploy-infra/deploy.ps1` (App Service configuration section)

**Reference:** `prompts/prompt-008-use-existing-db`, `prompts/prompt-027-deployment-script`

---

### ❌ Error: Chat page throws exception when GenAI not deployed

**Cause:** Chat page doesn't check if GenAI is configured before attempting to use it.

**Solution:** Chat service should expose an `IsConfigured` property:

```csharp
public class ChatService
{
    public bool IsConfigured => 
        !string.IsNullOrEmpty(_openAIEndpoint) && 
        !string.IsNullOrEmpty(_modelName);

    public async Task<string> SendMessageAsync(string message)
    {
        if (!IsConfigured)
        {
            return "AI Chat is not available yet. To enable it, redeploy using the -DeployGenAI switch.";
        }
        // ... actual chat logic
    }
}
```

**Where This Applies:** `src/ExpenseManagement/Services/ChatService.cs`, `src/ExpenseManagement/Pages/Chat.cshtml.cs`

**Reference:** `.github/copilot-instructions.md` (chat page availability rule)

---

## Quick Reference: Critical Files to Check

Before completing any phase, verify these patterns:

### ✅ PowerShell Scripts
- [ ] `deploy-all.ps1` uses hashtable splatting (not array splatting)
- [ ] `deploy-infra/deploy.ps1` uses `2>$null` for JSON output (not `2>&1`)
- [ ] All sqlcmd calls use temp files and `-i` flag (no piping)
- [ ] sqlcmd auth parameter is quoted: `"--authentication-method=..."`

### ✅ Bicep Templates
- [ ] `utcNow()` only in parameter defaults
- [ ] `newGuid()` only in parameter defaults
- [ ] No circular dependencies in diagnostic settings
- [ ] All Azure resource names use `toLower()`

### ✅ Application Code
- [ ] `Program.cs` has `public partial class Program { }` at the bottom
- [ ] Column names in C# match stored procedure aliases exactly
- [ ] ChatService has `IsConfigured` property
- [ ] Chat page shows graceful message when GenAI not configured

### ✅ Database
- [ ] Managed identity user created with SID (not FROM EXTERNAL PROVIDER)
- [ ] Connection string includes `Authentication=Active Directory Managed Identity`

### ✅ Tests
- [ ] WebApplicationFactory uses custom factory with in-memory config
- [ ] Tests check for graceful fallback (not exceptions)
- [ ] Smoke tests read from `.deployment-context.json` and skip if missing
- [ ] Bicep validation checks utcNow() on the current line

---

## How to Use This Document

**For Agents:** Before completing your work, review the sections relevant to your domain and verify you haven't introduced any of these common errors.

**For Developers:** If you encounter an error not listed here, document it following this format:
1. Error message (exact text)
2. Cause (why it happened)
3. Bad code example
4. Good code example
5. Where it applies
6. Reference to detailed documentation

**For Prompts:** Reference this document when instructing agents on error prevention. Each section links to detailed prompt files for complete implementation guidance.

---

**Last Updated:** Phase 4 completion  
**Total Errors Documented:** 15  
**Total Preventative Patterns:** 20+
