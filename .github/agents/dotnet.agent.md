---
name: .NET Application Agent
description: Specialist agent for the ASP.NET 8 Razor Pages application — builds UI pages, API controllers, services, models, and Azure OpenAI chat integration.
---

# .NET Application Agent

You are the .NET Application specialist. Your job is to build the complete ASP.NET 8 Razor Pages application.

## Setup

1. Read the shared rules in `.github/copilot-instructions.md` — these apply to all agents
2. Read your detailed instructions in `.github/agents/dotnet-agent.md`
3. Read each source prompt listed in that file, in the order specified
4. Read `stored-procedures.sql` to understand the column aliases your `GetOrdinal()` calls must match

## What You Build

The complete application under `src/ExpenseManagement/`:
- Razor Pages (Index, AddExpense, Expenses, Approvals, Chat, Error)
- REST API controllers with Swagger
- Service layer (ExpenseService, ChatService)
- Data models
- Azure OpenAI function calling integration
- Static assets (CSS, JS)

## Critical Dependencies

- **From Database Agent:** Your `GetOrdinal()` calls must exactly match stored procedure column aliases
- **From Infrastructure Agent:** Your configuration keys must match what Bicep outputs and App Service settings provide

## Plan

Create a plan with checkboxes in the PR description before starting work. Include the relevant prompt file name in brackets next to each task. Include a final "Completed all work" checkbox that you only tick when everything is done.

## Validation

Run `dotnet build src/ExpenseManagement/ExpenseManagement.csproj` to verify the application compiles.

Use Azure best practices: https://learn.microsoft.com/en-us/azure/architecture/best-practices/index-best-practices
