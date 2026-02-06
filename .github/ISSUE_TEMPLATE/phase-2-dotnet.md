---
name: "💻 Phase 2 — .NET Application Agent"
about: "Build the complete ASP.NET 8 Razor Pages application with API, services, and chat"
title: "💻 Phase 2 — Build .NET Application"
---

> **Phase 2** — Depends on Phase 1a (Infrastructure) and Phase 1b (Database). **Merge both Phase 1 PRs before starting this phase.**
> 
> **To start:** Assign this issue to **Copilot**.

---

## Instructions

1. Read the shared rules in `.github/copilot-instructions.md` — these apply to all agents
2. Read your detailed instructions in `.github/agents/dotnet-agent.md`
3. Read each source prompt listed in that file, in the order specified:
   - `prompts/prompt-004-create-app-code`
   - `prompts/prompt-008-use-existing-db`
   - `prompts/prompt-022-display-error-messages`
   - `prompts/prompt-007-add-api-code`
   - `prompts/prompt-010-add-chat-ui`
   - `prompts/prompt-020-model-function-calling`
   - `prompts/prompt-025-clientid-for-chat`
   - `prompts/prompt-018-extra-genai-instructions`
4. **Read `stored-procedures.sql`** — your `GetOrdinal()` calls must match column aliases exactly

## Deliverables

Complete application under `src/ExpenseManagement/`:

- [ ] `Program.cs` — DI registration, middleware, Swagger config
- [ ] `ExpenseManagement.csproj` — targeting `net8.0` with required NuGet packages
- [ ] `appsettings.json` — configuration placeholders (no real secrets)
- [ ] `appsettings.Development.json` — local dev settings with `Active Directory Default`
- [ ] `Models/ExpenseModels.cs` — data models
- [ ] `Services/ExpenseService.cs` — database operations via stored procedures
- [ ] `Services/ChatService.cs` — Azure OpenAI integration with function calling
- [ ] `Pages/Index.cshtml` + `.cs` — dashboard / navigation
- [ ] `Pages/AddExpense.cshtml` + `.cs` — create new expense
- [ ] `Pages/Expenses.cshtml` + `.cs` — view / filter expenses
- [ ] `Pages/Approvals.cshtml` + `.cs` — approve / reject expenses
- [ ] `Pages/Chat.cshtml` + `.cs` — AI chat interface
- [ ] `Pages/Error.cshtml` + `.cs` — error page
- [ ] `Controllers/ApiControllers.cs` — REST API with Swagger
- [ ] `wwwroot/` — CSS and JavaScript assets

## Critical: Column Name Alignment

Your `GetOrdinal()` calls **must exactly match** the stored procedure column aliases:

```csharp
// ✅ CORRECT
Amount = reader.GetDecimal(reader.GetOrdinal("AmountDecimal")),
ReviewerName = reader.IsDBNull(reader.GetOrdinal("ReviewedByName"))
    ? null : reader.GetString(reader.GetOrdinal("ReviewedByName")),

// ❌ WRONG — these cause runtime crashes
Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
ReviewerName = reader.GetString(reader.GetOrdinal("ReviewerName")),
```

## Key Rules

- **Data access**: Always through stored procedures via the service layer — never direct SQL
- **Chat page**: Must **always** exist, even when GenAI is not deployed
- **Chat fallback**: Show "AI Chat is not available yet. To enable it, redeploy using the -DeployGenAI switch."
- **Auth**: Use `ManagedIdentityCredential` for OpenAI, never API keys
- **No secrets**: Placeholder connection strings only — real values set by App Service config
- **Function calling**: Chat should execute real DB operations (get/create/approve expenses)
- **Error handling**: Display errors in header bar, fall back to dummy data when DB unavailable

## Validation

- [ ] `dotnet build src/ExpenseManagement/ExpenseManagement.csproj` succeeds
- [ ] All `GetOrdinal()` calls match stored procedure column aliases exactly
- [ ] `Chat.cshtml`, `Chat.cshtml.cs`, and `ChatService.cs` all exist
- [ ] `ChatService.IsConfigured` returns false when `GenAISettings:OpenAIEndpoint` is empty
- [ ] No hardcoded connection strings or API keys anywhere
- [ ] Swagger accessible at `/swagger`
- [ ] Error handling shows user-friendly messages with dummy data fallback
- [ ] Function calling tools match available service methods
- [ ] Completed all work
