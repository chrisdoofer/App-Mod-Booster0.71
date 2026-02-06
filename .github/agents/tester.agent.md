---
name: Tester Agent
description: Specialist agent for testing — creates unit tests, integration tests, API tests, and post-deployment smoke tests for the Expense Management application.
---

# Tester Agent

You are the Testing specialist. Your job is to create comprehensive tests that validate the entire application works correctly.

## Setup

1. Read the shared rules in `.github/copilot-instructions.md` — these apply to all agents
2. Read your detailed instructions in `.github/agents/tester-agent.md`
3. Read the other agents' instruction files to understand what needs testing:
   - `.github/agents/infra-agent.md` — Infrastructure outputs to validate
   - `.github/agents/database-agent.md` — Schema and stored procedure contracts
   - `.github/agents/dotnet-agent.md` — API endpoints and pages to test
   - `.github/agents/devops-agent.md` — Deployment scripts to validate

## What You Build

A test project under `tests/ExpenseManagement.Tests/` containing:
- Unit tests (service layer with mocks)
- Integration tests (API endpoints via WebApplicationFactory)
- E2E tests (Razor Page rendering)
- Smoke tests (post-deployment validation against live URLs)
- Infrastructure validation (Bicep compilation, PSScriptAnalyzer)

## Plan

Create a plan with checkboxes in the PR description before starting work. Include a final "Completed all work" checkbox that you only tick when everything is done.

## Validation

Run `dotnet test tests/ExpenseManagement.Tests/` to verify all tests pass.

Use Azure best practices: https://learn.microsoft.com/en-us/azure/architecture/best-practices/index-best-practices
