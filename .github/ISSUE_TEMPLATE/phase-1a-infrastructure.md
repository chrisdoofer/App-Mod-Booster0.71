---
name: "🏗️ Phase 1a — Infrastructure Agent"
about: "Build all Azure infrastructure Bicep modules (App Service, SQL, Identity, Monitoring, GenAI)"
title: "🏗️ Phase 1a — Build Azure Infrastructure (Bicep)"
labels: ["agent:infra", "phase:1"]
---

> **Phase 1a** — Can run in parallel with Phase 1b (Database). No dependencies on other agents.
> 
> **To start:** Assign this issue to **Copilot**.

---

## Instructions

1. Read the shared rules in `.github/copilot-instructions.md` — these apply to all agents
2. Read your detailed instructions in `.github/agents/infra-agent.md`
3. Read each source prompt listed in that file, in the order specified:
   - `prompts/prompt-030-bicep-best-practices`
   - `prompts/prompt-001-create-app-service`
   - `prompts/prompt-017-create-managed-identity`
   - `prompts/prompt-002-create-azure-sql`
   - `prompts/prompt-026-Monitoring`
   - `prompts/prompt-009-create-genai-resources`

## Deliverables

Create the following files:

- [ ] `deploy-infra/main.bicep` — orchestration template
- [ ] `deploy-infra/main.bicepparam` — parameters file (using `./main.bicep`)
- [ ] `deploy-infra/modules/app-service.bicep` — App Service + Plan (S1, UK South)
- [ ] `deploy-infra/modules/managed-identity.bicep` — User-Assigned Managed Identity
- [ ] `deploy-infra/modules/azure-sql.bicep` — SQL Server + Northwind DB (Entra ID-only auth)
- [ ] `deploy-infra/modules/monitoring.bicep` — Log Analytics + Application Insights
- [ ] `deploy-infra/modules/app-service-diagnostics.bicep` — App Service diagnostic settings
- [ ] `deploy-infra/modules/sql-diagnostics.bicep` — SQL Database diagnostic settings
- [ ] `deploy-infra/modules/genai.bicep` — Azure OpenAI + AI Search (conditional on `deployGenAI`)

## Key Rules

- All resource names must use `toLower()` — never uppercase in resource names
- `utcNow()` and `newGuid()` are **only** valid as parameter defaults, never in variables
- GenAI module must be conditional: `= if (deployGenAI) { ... }`
- SQL diagnostics only at database level, never server level
- Split App Service diagnostics into a separate module to avoid circular dependencies
- Use `.bicepparam` format, not `parameters.json`

## Required Outputs from `main.bicep`

The DevOps Agent's deployment script depends on these outputs:

| Output | Type | Description |
|--------|------|-------------|
| `webAppName` | string | App Service name |
| `sqlServerFqdn` | string | SQL Server FQDN |
| `databaseName` | string | Database name |
| `managedIdentityName` | string | Identity resource name |
| `managedIdentityClientId` | string | Identity Client ID |
| `managedIdentityPrincipalId` | string | Identity Principal/Object ID |
| `appInsightsConnectionString` | string | App Insights connection string |
| `logAnalyticsWorkspaceId` | string | Log Analytics resource ID |
| `openAIEndpoint` | string | OpenAI endpoint (empty if not deployed) |
| `openAIModelName` | string | Model name (empty if not deployed) |

## Validation

- [ ] `az bicep build --file deploy-infra/main.bicep` succeeds with no errors
- [ ] No `utcNow()` or `newGuid()` used outside parameter defaults
- [ ] All resource names use `toLower()`
- [ ] No circular dependencies between modules
- [ ] SQL diagnostics only at database level
- [ ] GenAI module is conditional
- [ ] All outputs listed above are present
- [ ] Completed all work
