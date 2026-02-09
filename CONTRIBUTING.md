# Contributing to App Mod Booster

This repository uses an **agent-driven development model** where AI agents generate the application code and infrastructure from prompts and guardrails. This guide explains how to contribute effectively.

## Repository Structure

```
Blueprint (Source of Truth)          Agent-Generated (Rebuilt)
─────────────────────────────        ─────────────────────────
.github/copilot-instructions.md  →   src/ExpenseManagement/
.github/agents/                  →   deploy-infra/
prompts/                         →   deploy-app/
COMMON-ERRORS.md                 →   deploy-all.ps1
Database-Schema/                 →   .github/workflows/
stored-procedures.sql            →   tests/
Legacy-Screenshots/              →   .deployment-context.json
```

**Key principle:** Never edit agent-generated code directly. Always trace issues back to prompts or guardrails.

## Branch Strategy

| Branch | Purpose | Contents |
|--------|---------|----------|
| `blueprint` | Source of truth | Prompts, guardrails, schema only |
| `release` | Deployable app | Full application built by agents |

### Workflow

```
blueprint branch (prompts only)
        │
        ├── Update prompts/guardrails
        │
        ↓
    Agent Build
        │
        ↓
release branch (full app)
        │
        ↓
    CI/CD Pipeline → Azure
```

## Bug Fix Procedure

When a bug is discovered in the running application:

### Step 1: Diagnose the Root Cause

Determine whether the bug is in:
- **Agent-generated code** → Fix requires updating prompts/guardrails
- **Blueprint content** → Fix directly in blueprint (rare)

### Step 2: Update the Blueprint

Edit the appropriate files:

| File | When to Update |
|------|----------------|
| `prompts/prompt-XXX` | Agent generated wrong code pattern |
| `.github/copilot-instructions.md` | Missing rule that would prevent the bug |
| `COMMON-ERRORS.md` | Document the pattern for future reference |
| `.github/agents/*.md` | Specialist agent needs domain-specific guidance |

### Step 3: Rebuild and Verify

```powershell
# 1. Clean agent-generated content from release branch
git checkout release
Remove-Item -Recurse -Force src, deploy-infra, deploy-app, deploy-all.ps1, tests, .github/workflows

# 2. Merge latest blueprint
git merge blueprint

# 3. Run agent build (follow prompt-order)

# 4. Commit and push
git add -A
git commit -m "rebuild: apply blueprint updates"
git push
```

---

## Concrete Example: SqlClient TLS Bug

This example shows the complete workflow for a real bug discovered during deployment.

### The Bug

**Symptom:** Application deployed successfully but failed to start within 10 minutes.

**Error in logs:**
```
Microsoft.Data.SqlClient.SqlException: Connection reset by peer
System.Net.Sockets.SocketException (104): Connection reset by peer
```

### Step 1: Diagnose

Investigation revealed:
- `Microsoft.Data.SqlClient` version 5.1.5 was being used
- Linux App Service uses OpenSSL 3.0
- SqlClient 5.1.x has a TLS handshake bug with OpenSSL 3.0
- Version 5.2.2+ fixes this issue

**Root cause:** The agent chose version 5.1.5 because no version was specified in the prompts.

### Step 2: Update Blueprint

Three files were updated:

#### prompts/prompt-004-create-app-code
```markdown
## NuGet Package Versions

Use these specific versions to avoid runtime issues on Linux App Service:

\`\`\`xml
<PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.2" />
\`\`\`

**Important:** Version 5.1.x fails on Linux with "Connection reset by peer" 
due to OpenSSL 3.0 TLS incompatibility.
```

#### .github/copilot-instructions.md
Added to Common Pitfalls:
```markdown
14. **Microsoft.Data.SqlClient version** → must be 5.2.2+; version 5.1.x 
    fails on Linux App Service with "Connection reset by peer" due to 
    OpenSSL 3.0 TLS incompatibility
```

#### COMMON-ERRORS.md
Added full error documentation with bad/good code examples.

### Step 3: Rebuild

After updating the blueprint, the agent-generated code was deleted and agents rebuilt the application. The new build used SqlClient 5.2.2 and deployed successfully.

### Why This Works

| Before | After |
|--------|-------|
| Agent picked 5.1.5 (common in training data) | Agent reads prompt specifying 5.2.2 |
| No guardrail warned about version | Pitfall #14 prevents wrong version |
| Bug could recur on rebuild | Fix is permanent across all builds |

---

## Files Reference

### Guardrail Files (Update for Bug Prevention)

| File | Purpose | Update When |
|------|---------|-------------|
| `.github/copilot-instructions.md` | Rules ALL agents follow | Any cross-cutting bug pattern |
| `COMMON-ERRORS.md` | Detailed error patterns with examples | Any bug worth documenting |
| `.github/agents/*.md` | Domain-specific agent rules | Specialist agent makes repeated mistakes |

### Prompt Files (Update for Feature/Behavior Changes)

| File | Controls |
|------|----------|
| `prompt-001-create-app-service` | App Service Bicep module |
| `prompt-002-create-azure-sql` | Azure SQL Bicep module |
| `prompt-004-create-app-code` | ASP.NET application code |
| `prompt-005-deploy-app-code` | Application deployment script |
| `prompt-007-add-api-code` | API controllers |
| `prompt-009-create-genai-resources` | Azure OpenAI infrastructure |
| `prompt-010-add-chat-ui` | Chat UI page |
| `prompt-028-github-actions-cicd` | CI/CD workflow |
| `prompt-029-unified-deployment-script` | deploy-all.ps1 |

See `prompts/prompt-order` for the full sequence.

---

## Quick Reference

### Cleanup Script (Before Rebuild)

```powershell
# Remove all agent-generated content
Remove-Item -Recurse -Force src, deploy-infra, deploy-app, tests -ErrorAction SilentlyContinue
Remove-Item -Force deploy-all.ps1, .deployment-context.json -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force .github/workflows -ErrorAction SilentlyContinue
```

### Common Bug Categories

| Symptom | Likely Fix Location |
|---------|---------------------|
| Wrong package version | `prompts/prompt-004-create-app-code` |
| Wrong Bicep config | `prompts/prompt-001-*` or `prompt-002-*` |
| CI/CD failure | `prompts/prompt-028-github-actions-cicd` |
| PowerShell syntax error | `.github/copilot-instructions.md` (pitfalls) |
| Column name mismatch | `prompts/prompt-004` or `.github/agents/dotnet-agent.md` |

---

## Questions?

If you're unsure whether a bug should be fixed in prompts vs guardrails:
- **Prompts** = "Build it this way" (positive instruction)
- **Guardrails** = "Never do this" (preventive rules)

When in doubt, add to both — prompts tell agents what to do, guardrails prevent them from doing the wrong thing.
