---
name: "🧪 Phase 4 — Tester Agent"
about: "Build unit tests, integration tests, API tests, and post-deployment smoke tests"
title: "🧪 Phase 4 — Build End-to-End Tests"
---

> **Phase 4** — Depends on all prior phases. **Merge all Phase 1–3 PRs before starting this phase.**
> 
> **To start:** Assign this issue to **Copilot**.

---

## Instructions

1. Read the shared rules in `.github/copilot-instructions.md` — these apply to all agents
2. Read your detailed instructions in `.github/agents/tester-agent.md`
3. Read the other agents' instruction files to understand what needs testing:
   - `.github/agents/infra-agent.md` — infrastructure outputs to validate
   - `.github/agents/database-agent.md` — schema and stored procedure contracts
   - `.github/agents/dotnet-agent.md` — API endpoints and pages to test
   - `.github/agents/devops-agent.md` — deployment scripts to validate

## Deliverables

Test project under `tests/ExpenseManagement.Tests/`:

- [ ] `ExpenseManagement.Tests.csproj` — xUnit test project
- [ ] `Unit/ExpenseServiceTests.cs` — service layer unit tests with mocks
- [ ] `Unit/ChatServiceTests.cs` — chat service unit tests
- [ ] `Integration/ApiEndpointTests.cs` — REST API tests via `WebApplicationFactory`
- [ ] `Integration/DatabaseConnectionTests.cs` — SQL connectivity tests
- [ ] `E2E/PageNavigationTests.cs` — Razor Page rendering tests
- [ ] `E2E/ChatFlowTests.cs` — chat UI E2E tests
- [ ] `Smoke/DeploymentSmokeTests.cs` — post-deployment live validation
- [ ] `Smoke/HealthCheckTests.cs` — endpoint availability checks
- [ ] `Infrastructure/BicepValidationTests.cs` — Bicep compilation checks
- [ ] `Helpers/TestFixtures.cs` — shared test setup
- [ ] `Helpers/TestData.cs` — test data generators

## What to Test

### API Endpoints
- `GET /api/expenses` — list all expenses
- `GET /api/expenses?status={status}` — filter by status
- `POST /api/expenses` — create expense
- `PUT /api/expenses/{id}/approve` — approve expense
- `GET /swagger/index.html` — Swagger docs accessible

### Razor Pages
- `/Index` — dashboard loads
- `/AddExpense` — form renders
- `/Expenses` — expense list loads
- `/Approvals` — approval page loads
- `/Chat` — chat page loads and shows "not configured" when GenAI is off

### Deployment Scripts
- All `.ps1` files pass PSScriptAnalyzer with no errors
- `deploy-all.ps1` uses hashtable splatting (not array)
- No `.sh` or `.bash` files exist anywhere in the repo

### Smoke Tests (Post-Deployment)
- Read app URL from `.deployment-context.json`
- Verify `/Index`, `/swagger`, `/Chat` return HTTP 200 against the live app

## Key Rules

- Use **xUnit** as the test framework
- Use `WebApplicationFactory<Program>` for in-process integration tests
- Use **Moq** for mocking in unit tests
- Unit tests must **not** require a real database connection
- Smoke tests must read the app URL from `.deployment-context.json`
- No hardcoded URLs, connection strings, or credentials in any test

## Validation

- [ ] `dotnet test tests/ExpenseManagement.Tests/` passes
- [ ] Unit tests run without any external dependencies
- [ ] Integration tests use `WebApplicationFactory` (in-process)
- [ ] All API endpoints are covered
- [ ] All Razor Pages are covered
- [ ] Chat page test verifies "not configured" message when GenAI is off
- [ ] Smoke tests read URLs from `.deployment-context.json`
- [ ] PSScriptAnalyzer checks included for all `.ps1` files
- [ ] No `.sh` or `.bash` file existence check included
- [ ] No hardcoded URLs or credentials in any test
- [ ] Completed all work
