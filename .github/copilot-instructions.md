# App Mod Booster — Shared Copilot Instructions

These rules apply to **every** agent working on this repository. Individual specialist agents extend these rules with domain-specific guidance.

## Project Overview

This repository modernises a legacy Expense Management application into a cloud-native Azure solution. The tech stack is:

| Layer | Technology |
|-------|-----------|
| Application | ASP.NET 8 Razor Pages (C#) |
| API | REST controllers with Swagger |
| Database | Azure SQL Database (Basic tier, Entra ID-only auth) |
| Infrastructure | Bicep (modular, with `.bicepparam`) |
| Deployment | PowerShell (.ps1) scripts only |
| CI/CD | GitHub Actions with OIDC federation |
| GenAI | Azure OpenAI (GPT-4o, Sweden Central), Azure AI Search |
| Identity | User-Assigned Managed Identity (zero secrets) |
| Monitoring | Application Insights + Log Analytics Workspace |

## Repository Structure

```
deploy-infra/          ← Bicep modules + infra deployment script
deploy-app/            ← App deployment script
src/ExpenseManagement/ ← .NET 8 Razor Pages application
Database-Schema/       ← SQL schema file
stored-procedures.sql  ← Application stored procedures
deploy-all.ps1         ← Unified orchestrator script
.github/workflows/     ← GitHub Actions CI/CD
.github/agents/        ← Specialist agent instructions
prompts/               ← Original prompt files (reference only)
```

## Absolute Rules (Never Break These)

### 1. PowerShell Only
- **NEVER** create `.sh`, `.bash`, or any shell script files
- All automation uses PowerShell `.ps1` scripts compatible with PowerShell 7+
- Use hashtable splatting for script-to-script parameter passing (never array splatting)

### 2. Zero Secrets
- All authentication uses User-Assigned Managed Identity
- No passwords, API keys, or connection strings with secrets anywhere
- SQL uses `Authentication=Active Directory Managed Identity`
- Azure OpenAI uses `ManagedIdentityCredential`

### 3. Plan Before Working
- Before making changes, create a plan with checkbox items in the PR description
- Include the relevant prompt file name in brackets next to each task

### 4. Error Prevention
- **ALWAYS** consult `COMMON-ERRORS.md` before making changes
- Review `prompts/prompt-031-testing-lessons-learned` when creating tests
- Common pitfalls documented at the repo root - prevention is easier than debugging
- Include a final "Completed all work" checkbox that is only ticked when everything is done

### 4. Agent Handoff via Context File
- Infrastructure deployment creates `.deployment-context.json` at the repo root
- Application deployment reads this file — no manual parameter re-entry
- Agents pass data via this shared context file, not by duplicating logic

### 5. Naming Conventions
- All Azure resource names must be **lowercase**
- Apply `toLower()` with `uniqueString()` in Bicep
- Follow standard ASP.NET folder conventions for application code

## Common Pitfalls (All Agents Must Know)

1. **PowerShell splatting** → use hashtables `@{ Key = $Value }`, not arrays
2. **Azure CLI JSON output** → redirect stderr with `2>$null`, not `2>&1`
3. **Bicep `utcNow()` / `newGuid()`** → only valid as parameter defaults
4. **sqlcmd piping** → write to temp file first, use `-i` flag
5. **sqlcmd auth quoting** → `"--authentication-method=ActiveDirectoryDefault"`
6. **Column name alignment** → C# `GetOrdinal()` names must match stored procedure aliases exactly
7. **Chat page** → must always exist, even when GenAI is not deployed
8. **Circular dependencies in Bicep** → split App Service diagnostics into a separate module
9. **SQL diagnostics** → only at database level, never at server level
10. **Resource group reuse** → always use fresh names with date/time suffix
11. **GenAI deployment** → uses `-DeployGenAI` switch, not a separate script

## Shared Context Schema

Agents communicate via `.deployment-context.json`. See `.github/agents/agent-context-schema.json` for the full schema. Every agent must read from and write to this contract.

## Reference Links

- [Azure Architecture Best Practices](https://learn.microsoft.com/en-us/azure/architecture/best-practices/index-best-practices)
- [Bicep Documentation](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/)
- [ASP.NET 8 Documentation](https://learn.microsoft.com/en-us/aspnet/core/?view=aspnetcore-8.0)
