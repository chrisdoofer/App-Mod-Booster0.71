# Azure Architecture Diagram

This diagram shows the Azure services deployed by the Expense Management solution.

```
┌─────────────────────────────────────────────────────────────────────────────────────┐
│                                    Azure Subscription                                │
│                                                                                      │
│  ┌───────────────────────────────────────────────────────────────────────────────┐  │
│  │                              Resource Group                                    │  │
│  │                                                                                │  │
│  │  ┌─────────────────┐         ┌─────────────────────────────────────────────┐  │  │
│  │  │                 │         │              Azure App Service              │  │  │
│  │  │  User-Assigned  │◄───────►│                                             │  │  │
│  │  │ Managed Identity│         │  ┌─────────────────────────────────────┐    │  │  │
│  │  │                 │         │  │    ASP.NET 8 Razor Pages App        │    │  │  │
│  │  └────────┬────────┘         │  │                                     │    │  │  │
│  │           │                  │  │  • Dashboard                        │    │  │  │
│  │           │                  │  │  • Expense Management               │    │  │  │
│  │           │                  │  │  • Approval Workflow                │    │  │  │
│  │           │                  │  │  • REST API (Swagger)               │    │  │  │
│  │           │                  │  │  • AI Chat Assistant                │    │  │  │
│  │           │                  │  └─────────────────────────────────────┘    │  │  │
│  │           │                  └──────────────────┬──────────────────────────┘  │  │
│  │           │                                     │                             │  │
│  │           │                                     │ Entra ID Auth               │  │
│  │           │                                     │ (no passwords)              │  │
│  │           │                                     ▼                             │  │
│  │           │                  ┌─────────────────────────────────────────────┐  │  │
│  │           │                  │             Azure SQL Database              │  │  │
│  │           └─────────────────►│                                             │  │  │
│  │                              │  • Database: Northwind                      │  │  │
│  │                              │  • Entra ID Only Authentication             │  │  │
│  │                              │  • Stored Procedures for Data Access        │  │  │
│  │                              │  • Tables: Expenses, Users, Categories...   │  │  │
│  │                              └─────────────────────────────────────────────┘  │  │
│  │                                                                                │  │
│  │  ┌─────────────────────────────────────────────────────────────────────────┐  │  │
│  │  │                          Monitoring                                     │  │  │
│  │  │                                                                         │  │  │
│  │  │  ┌─────────────────────┐    ┌─────────────────────────────────────┐    │  │  │
│  │  │  │  Log Analytics      │◄───│       Application Insights          │    │  │  │
│  │  │  │  Workspace          │    │                                     │    │  │  │
│  │  │  │                     │    │  • Request tracing                  │    │  │  │
│  │  │  │  • Centralized logs │    │  • Performance monitoring           │    │  │  │
│  │  │  │  • SQL diagnostics  │    │  • Error tracking                   │    │  │  │
│  │  │  │  • App Service logs │    │  • Custom metrics                   │    │  │  │
│  │  │  └─────────────────────┘    └─────────────────────────────────────┘    │  │  │
│  │  └─────────────────────────────────────────────────────────────────────────┘  │  │
│  │                                                                                │  │
│  │  ┌─────────────────────────────────────────────────────────────────────────┐  │  │
│  │  │                    GenAI Resources (Optional)                           │  │  │
│  │  │                    (Deployed with -DeployGenAI)                         │  │  │
│  │  │                                                                         │  │  │
│  │  │  ┌─────────────────────┐    ┌─────────────────────────────────────┐    │  │  │
│  │  │  │  Azure OpenAI       │    │       Azure AI Search               │    │  │  │
│  │  │  │  (Sweden Central)   │    │                                     │    │  │  │
│  │  │  │                     │    │  • Index for RAG scenarios          │    │  │  │
│  │  │  │  • GPT-4o Model     │    │  • Semantic search                  │    │  │  │
│  │  │  │  • Function calling │    │  • Vector search support            │    │  │  │
│  │  │  │  • Managed Identity │    │                                     │    │  │  │
│  │  │  └─────────────────────┘    └─────────────────────────────────────┘    │  │  │
│  │  └─────────────────────────────────────────────────────────────────────────┘  │  │
│  │                                                                                │  │
│  └────────────────────────────────────────────────────────────────────────────────┘  │
│                                                                                      │
└──────────────────────────────────────────────────────────────────────────────────────┘
```

## Data Flow

1. **User Requests** → App Service receives HTTP requests
2. **Authentication** → Managed Identity authenticates to Azure SQL (no passwords)
3. **Data Operations** → Stored procedures handle all database operations
4. **AI Chat** → Chat requests go to Azure OpenAI for natural language processing
5. **Function Calling** → AI can execute database operations via function calling
6. **Logging** → All telemetry flows to Application Insights and Log Analytics

## Security Features

- **No Passwords**: All authentication uses Managed Identity
- **Entra ID Only**: SQL Server rejects username/password authentication
- **Encrypted Connections**: TLS 1.2 minimum for all connections
- **HTTPS Only**: App Service redirects all HTTP to HTTPS
- **RBAC**: Role-based access control for Azure resources

## Deployment Scripts

| Script | Purpose |
|--------|---------|
| `deploy-infra/deploy.ps1` | Deploys all Azure infrastructure |
| `deploy-app/deploy.ps1` | Deploys the ASP.NET application |
| `deploy-all.ps1` | Runs both scripts in sequence |
