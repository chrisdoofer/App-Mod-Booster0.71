# Azure Architecture

This document shows the Azure services deployed by the Expense Management application.

## Architecture Diagram

```
                                    ┌─────────────────────────────────────────────────────────┐
                                    │                    Azure Resource Group                  │
                                    │                                                         │
                                    │  ┌───────────────────────────────────────────────────┐  │
                                    │  │              App Service Plan (S1)                │  │
                                    │  │                                                   │  │
                                    │  │  ┌─────────────────────────────────────────────┐  │  │
                                    │  │  │           App Service (.NET 8)              │  │  │
                                    │  │  │                                             │  │  │
                                    │  │  │  • Razor Pages (Dashboard, Expenses, etc.) │  │  │
                                    │  │  │  • REST API (/api/expenses, /swagger)      │  │  │
        Users ──────────────────────┼──┼──│  • AI Chat Interface (/Chat)               │  │  │
                                    │  │  │                                             │  │  │
                                    │  │  └─────────────────────────────────────────────┘  │  │
                                    │  │           │                    │                  │  │
                                    │  └───────────┼────────────────────┼──────────────────┘  │
                                    │              │                    │                     │
                                    │              │ Managed Identity   │                     │
                                    │              ▼                    ▼                     │
                                    │  ┌───────────────────┐  ┌───────────────────┐          │
                                    │  │    Azure SQL      │  │  Azure OpenAI     │          │
                                    │  │    Server         │  │  (Sweden Central) │          │
                                    │  │                   │  │                   │          │
                                    │  │  ┌─────────────┐  │  │  • GPT-4o Model   │          │
                                    │  │  │ Northwind   │  │  │  • Function       │          │
                                    │  │  │ Database    │  │  │    Calling        │          │
                                    │  │  │             │  │  │                   │          │
                                    │  │  │ • Expenses  │  │  └───────────────────┘          │
                                    │  │  │ • Users     │  │            ▲                    │
                                    │  │  │ • Categories│  │            │                    │
                                    │  │  │ • Stored    │  │  ┌───────────────────┐          │
                                    │  │  │   Procs     │  │  │  Azure AI Search  │          │
                                    │  │  └─────────────┘  │  │  (Optional)       │          │
                                    │  └───────────────────┘  └───────────────────┘          │
                                    │                                                         │
                                    │  ┌───────────────────────────────────────────────────┐  │
                                    │  │                   Monitoring                       │  │
                                    │  │                                                   │  │
                                    │  │  ┌─────────────────┐  ┌─────────────────────────┐ │  │
                                    │  │  │ Application     │  │ Log Analytics           │ │  │
                                    │  │  │ Insights        │──│ Workspace               │ │  │
                                    │  │  │                 │  │                         │ │  │
                                    │  │  │ • Performance   │  │ • App Service Logs      │ │  │
                                    │  │  │ • Errors        │  │ • SQL Database Logs     │ │  │
                                    │  │  │ • Dependencies  │  │ • Metrics               │ │  │
                                    │  │  └─────────────────┘  └─────────────────────────┘ │  │
                                    │  └───────────────────────────────────────────────────┘  │
                                    │                                                         │
                                    │  ┌───────────────────────────────────────────────────┐  │
                                    │  │              User-Assigned Managed Identity        │  │
                                    │  │                                                   │  │
                                    │  │  Provides secure, passwordless authentication to: │  │
                                    │  │  • Azure SQL Database                             │  │
                                    │  │  • Azure OpenAI                                   │  │
                                    │  │  • Azure AI Search                                │  │
                                    │  └───────────────────────────────────────────────────┘  │
                                    │                                                         │
                                    └─────────────────────────────────────────────────────────┘
```

## Data Flow

### 1. User Request Flow
```
User → App Service → Razor Page/API Controller → ExpenseService → Stored Procedures → SQL Database
```

### 2. AI Chat Flow
```
User → Chat Page → ChatService → Azure OpenAI (GPT-4o) → Function Calling → ExpenseService → Database
```

### 3. Authentication Flow
```
App Service → Managed Identity → Entra ID → Azure Resource (SQL/OpenAI) → Access Granted
```

## Resource Summary

| Resource | SKU/Tier | Region | Purpose |
|----------|----------|--------|---------|
| App Service Plan | Standard S1 | UK South | Hosts the web application |
| App Service | - | UK South | .NET 8 web application |
| Azure SQL Server | - | UK South | Database server |
| Azure SQL Database | Basic | UK South | Expense data storage |
| Log Analytics | PerGB2018 | UK South | Centralized logging |
| Application Insights | - | UK South | APM and telemetry |
| Managed Identity | - | UK South | Secure authentication |
| Azure OpenAI* | S0 | Sweden Central | GPT-4o for chat |
| Azure AI Search* | Basic | UK South | Search capabilities |

*Only deployed with `-DeployGenAI` switch

## Security

- **No passwords stored**: All authentication uses Managed Identity
- **Entra ID only**: SQL Server uses Azure AD-only authentication
- **HTTPS enforced**: App Service configured for HTTPS only
- **TLS 1.2 minimum**: All services require TLS 1.2+
- **RBAC**: Minimal permissions granted to Managed Identity
