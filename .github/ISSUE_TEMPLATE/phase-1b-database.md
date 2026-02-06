---
name: "🗃️ Phase 1b — Database Agent"
about: "Build the SQL schema and all stored procedures with correct column mappings"
title: "🗃️ Phase 1b — Build Database Schema & Stored Procedures"
labels: ["agent:database", "phase:1"]
---

> **Phase 1b** — Can run in parallel with Phase 1a (Infrastructure). No dependencies on other agents.
> 
> **To start:** Assign this issue to **Copilot**.

---

## Instructions

1. Read the shared rules in `.github/copilot-instructions.md` — these apply to all agents
2. Read your detailed instructions in `.github/agents/database-agent.md`
3. Read each source prompt listed in that file, in the order specified:
   - `prompts/prompt-008-use-existing-db`
   - `prompts/prompt-016-sqlcmd-for-sql`
   - `prompts/prompt-024-sqlcmd-stored-procedures`

## Deliverables

- [ ] `Database-Schema/database_schema.sql` — table definitions, indexes, seed data
- [ ] `stored-procedures.sql` — all application stored procedures

## Key Rules

- Use `CREATE OR ALTER PROCEDURE` so scripts are idempotent
- Use `GO` batch separators between procedures
- Store monetary amounts as `INT` in minor units (pence/cents)
- Provide calculated `DECIMAL(10,2)` columns in stored procedure output
- Use `NVARCHAR` for all text columns

## Critical: Column Mapping Contract

The .NET Application Agent depends on these exact column aliases. Getting them wrong causes runtime crashes.

| Stored Procedure Column | C# Model Property | SQL Type | Notes |
|------------------------|-------------------|----------|-------|
| `AmountMinor` | `AmountMinor` | `INT` | Raw amount in minor units |
| `AmountDecimal` | `Amount` | `DECIMAL(10,2)` | Calculated via `CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2))` |
| `ReviewedByName` | `ReviewerName` | `NVARCHAR` | Aliased from `reviewer.UserName` |
| `ExpenseCount` | `Count` | `INT` | In `GetExpenseSummary` only |
| `TotalAmount` | `TotalAmount` | `DECIMAL` | In `GetExpenseSummary` — NOT INT |

**`GetExpenseSummary` returns exactly 3 columns:** `StatusName`, `ExpenseCount`, `TotalAmount`

## Validation

- [ ] All procedures use `CREATE OR ALTER PROCEDURE`
- [ ] `GO` batch separators between every procedure
- [ ] Column aliases match the mapping table above exactly
- [ ] `GetExpenseSummary` returns exactly 3 columns (not 4)
- [ ] Schema file runs cleanly through sqlcmd
- [ ] No `DROP TABLE` without `IF EXISTS` guard
- [ ] Monetary columns: `INT` in storage, `DECIMAL(10,2)` in procedure output
- [ ] Completed all work
