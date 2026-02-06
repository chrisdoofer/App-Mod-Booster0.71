---
name: "🚀 Phase 3 — DevOps Agent"
about: "Build PowerShell deployment scripts, unified orchestrator, and GitHub Actions CI/CD"
title: "🚀 Phase 3 — Build Deployment Scripts & CI/CD"
labels: ["agent:devops", "phase:3"]
---

> **Phase 3** — Depends on Phase 1 (Infrastructure + Database) and Phase 2 (.NET App). **Merge all prior PRs before starting this phase.**
> 
> **To start:** Assign this issue to **Copilot**.

---

## Instructions

1. Read the shared rules in `.github/copilot-instructions.md` — these apply to all agents
2. Read your detailed instructions in `.github/agents/devops-agent.md`
3. Read each source prompt listed in that file, in the order specified:
   - `prompts/prompt-006-baseline-script-instruction`
   - `prompts/prompt-027-deployment-script`
   - `prompts/prompt-005-deploy-app-code`
   - `prompts/prompt-029-unified-deployment-script`
   - `prompts/prompt-028-github-actions-cicd`
   - `prompts/prompt-016-sqlcmd-for-sql`
   - `prompts/prompt-019-chatui-deploy-file`

## Deliverables

- [ ] `deploy-infra/deploy.ps1` — full infrastructure deployment automation
- [ ] `deploy-infra/README.md` — infrastructure deployment documentation
- [ ] `deploy-app/deploy.ps1` — application deployment automation
- [ ] `deploy-app/README.md` — app deployment documentation
- [ ] `deploy-all.ps1` — unified single-command orchestrator
- [ ] `.github/workflows/deploy.yml` — GitHub Actions CI/CD with OIDC
- [ ] `.github/CICD-SETUP.md` — one-time OIDC federation setup guide

## Key Rules

### PowerShell Only — NEVER create `.sh` or `.bash` files

### Hashtable splatting (not array splatting)
```powershell
# ✅ CORRECT
$infraArgs = @{ ResourceGroup = $ResourceGroup; Location = $Location }
& $script @infraArgs

# ❌ WRONG
$infraArgs = @("-ResourceGroup", $ResourceGroup, "-Location", $Location)
```

### Azure CLI JSON output — redirect stderr
```powershell
# ✅ CORRECT
$output = az deployment group create --output json 2>$null

# ❌ WRONG — Bicep warnings corrupt JSON
$output = az deployment group create --output json 2>&1
```

### sqlcmd — temp files, never piping
```powershell
# ✅ CORRECT
$sql | Out-File -Path $tempFile -Encoding UTF8
sqlcmd -S $server -d $db -i $tempFile

# ❌ WRONG — causes go-sqlcmd crash
$sql | sqlcmd -S $server ...
```

## Infrastructure Script Requirements

`deploy-infra/deploy.ps1` must:

1. Detect CI/CD: `$IsCI = $env:GITHUB_ACTIONS -eq "true"`
2. Retrieve credentials (local: `az ad signed-in-user show` / CI: `$env:AZURE_CLIENT_ID`)
3. Deploy Bicep with Azure Policy resilience (retry pattern)
4. Import `Database-Schema/database_schema.sql` via sqlcmd
5. Create managed identity DB user (SID-based, not `FROM EXTERNAL PROVIDER`)
6. Import `stored-procedures.sql` via sqlcmd
7. Configure App Service settings: `AZURE_CLIENT_ID`, `ConnectionStrings__DefaultConnection`, `APPLICATIONINSIGHTS_CONNECTION_STRING`
8. If GenAI: configure `GenAISettings__OpenAIEndpoint`, `GenAISettings__OpenAIModelName`, `ManagedIdentityClientId`
9. Write `.deployment-context.json` at repo root

## Validation

- [ ] No `.sh` or `.bash` files created anywhere in the repo
- [ ] All scripts use hashtable splatting for parameter passing
- [ ] `deploy-infra/deploy.ps1` writes `.deployment-context.json`
- [ ] `deploy-app/deploy.ps1` reads `.deployment-context.json` from both `.` and `..`
- [ ] All required App Service settings are configured during infra deployment
- [ ] sqlcmd uses temp files (never piping)
- [ ] CI/CD detection (`$IsCI`) switches between `User`/`Application` and auth methods
- [ ] GitHub Actions workflow uses OIDC (no stored secrets)
- [ ] `deploy-all.ps1` uses hashtable splatting to call child scripts
- [ ] GenAI deployed via `-DeployGenAI` switch, not a separate script
- [ ] Completed all work
