# Azure Architecture Diagram

## Expense Management System — Cloud-Native Azure Solution

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Azure Resource Group                               │
│                                                                               │
│  ┌──────────────────┐    ┌──────────────────────────────────────────────┐   │
│  │  User-Assigned   │    │              App Service (S1)                │   │
│  │ Managed Identity │◄───│         ASP.NET 8 Razor Pages                │   │
│  │                  │    │         + REST API + Swagger                  │   │
│  │  mid-AppMod-*    │    │                                              │   │
│  └──────────────────┘    │   ┌─────────────────────────────────────┐   │   │
│           │               │   │ App Settings                        │   │   │
│           │               │   │ • AZURE_CLIENT_ID                   │   │   │
│           │               │   │ • ConnectionStrings__DefaultConn    │   │   │
│           │               │   │ • APPLICATIONINSIGHTS_CONN_STRING  │   │   │
│           │               │   │ • GenAISettings__OpenAIEndpoint    │   │   │
│           │               │   └─────────────────────────────────────┘   │   │
│           │               └───────────────┬──────────────────────────────┘   │
│           │                               │                                   │
│           │               ┌───────────────▼──────────────────────────────┐   │
│           │               │           Azure SQL Database                  │   │
│           │               │              (Northwind, Basic)               │   │
│           │               │                                               │   │
│           │               │  • Entra ID Only Authentication               │   │
│           ├───────────────►  • Managed Identity DB User (SID-based)       │   │
│           │               │  • Stored Procedures (usp_GetExpenses, etc.)  │   │
│           │               └──────────────────────────────────────────────┘   │
│           │                                                                   │
│           │               ┌──────────────────────────────────────────────┐   │
│           │               │           Azure Monitor                      │   │
│           │               │                                               │   │
│           │               │  ┌─────────────────────┐  ┌───────────────┐  │   │
│           │               │  │  Log Analytics       │  │  Application  │  │   │
│           │               │  │  Workspace           │  │  Insights     │  │   │
│           │               │  └──────────┬──────────┘  └───────┬───────┘  │   │
│           │               │             │                      │          │   │
│           │               │    Diagnostic settings:            │          │   │
│           │               │    • App Service logs              │          │   │
│           │               │    • SQL Database logs             │          │   │
│           │               └──────────────────────────────────────────────┘   │
│           │                                                                   │
│           │  [Optional - with -DeployGenAI switch]                           │
│           │               ┌──────────────────────────────────────────────┐   │
│           │               │        Azure OpenAI (Sweden Central)         │   │
│           │               │           Model: GPT-4o (capacity 8)         │   │
│           ├───────────────►  Role: Cognitive Services OpenAI User         │   │
│           │               └──────────────────────────────────────────────┘   │
│           │                                                                   │
│           │               ┌──────────────────────────────────────────────┐   │
│           │               │            Azure AI Search                   │   │
│           └───────────────►                                               │   │
│                           └──────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

## How Services Connect

| Connection | Authentication | Notes |
|------------|---------------|-------|
| App Service → Azure SQL | Managed Identity (User-Assigned) | `Authentication=Active Directory Managed Identity;User Id={clientId}` |
| App Service → Azure OpenAI | Managed Identity (User-Assigned) | `ManagedIdentityCredential(clientId)` |
| App Service → Application Insights | Connection String | Set via App Service config |
| CI/CD → Azure | OIDC Federation (no secrets) | Service Principal with Federated Credentials |

## Deployment Flow

```
Phase 1: Infrastructure
  deploy-infra/deploy.ps1
       │
       ├── Bicep: main.bicep
       │     ├── managed-identity.bicep
       │     ├── monitoring.bicep
       │     ├── app-service.bicep
       │     ├── app-service-diagnostics.bicep
       │     ├── azure-sql.bicep
       │     ├── sql-diagnostics.bicep
       │     └── genai.bicep (optional, -DeployGenAI)
       │
       ├── sqlcmd: Import database schema
       ├── sqlcmd: Create managed identity DB user (SID-based)
       ├── sqlcmd: Import stored procedures
       ├── az webapp config: Set app settings
       └── Save .deployment-context.json

Phase 2: Application
  deploy-app/deploy.ps1
       │
       ├── Read .deployment-context.json
       ├── dotnet publish
       ├── Create deployment zip
       └── az webapp deploy
```

## Key Architecture Decisions

1. **Zero Secrets** — All authentication uses User-Assigned Managed Identity; no passwords stored anywhere
2. **Entra ID Only SQL** — Azure AD-only authentication on SQL Server (`azureADOnlyAuthentication: true`)
3. **Health Check Endpoint** — `/health` returns 200 without DB dependency for App Service probes
4. **Graceful Degradation** — App shows dummy data if DB unavailable; Chat shows friendly message if GenAI not deployed
5. **Separated Concerns** — Infrastructure and application deployment are independent phases
6. **Circular Dependency Prevention** — App Service diagnostics deployed in a separate module after App Service
