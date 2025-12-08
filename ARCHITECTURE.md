# Architecture Diagram

## System Overview

```
┌────────────────────────────────────────────────────────────────────┐
│                         Azure Subscription                          │
│                                                                      │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │                     Resource Group                          │   │
│  │                                                              │   │
│  │  ┌──────────────────────────────────────────────────────┐ │   │
│  │  │              User / GitHub Actions                    │ │   │
│  │  │                      │                                 │ │   │
│  │  │                      ▼                                 │ │   │
│  │  │  ┌───────────────────────────────────────────┐       │ │   │
│  │  │  │         App Service (Linux, .NET 8)       │       │ │   │
│  │  │  │  - Dashboard (Razor Pages)                 │       │ │   │
│  │  │  │  - REST APIs (Swagger)                     │       │ │   │
│  │  │  │  - AI Chat Interface                       │       │ │   │
│  │  │  └───────────────────┬───────────────────────┘       │ │   │
│  │  │                      │                                 │ │   │
│  │  │                      │ Uses                            │ │   │
│  │  │                      ▼                                 │ │   │
│  │  │  ┌──────────────────────────────────┐                │ │   │
│  │  │  │   User-Assigned Managed Identity  │                │ │   │
│  │  │  │   (No passwords needed!)          │                │ │   │
│  │  │  └────┬──────────────┬───────────┬───┘                │ │   │
│  │  │       │              │           │                     │ │   │
│  │  │       │              │           │                     │ │   │
│  │  │  Authenticates  Authenticates   Authenticates         │ │   │
│  │  │       │              │           │                     │ │   │
│  │  │       ▼              ▼           ▼                     │ │   │
│  │  │  ┌─────────┐  ┌──────────┐  ┌───────────────┐       │ │   │
│  │  │  │Azure SQL│  │Azure     │  │Azure OpenAI   │       │ │   │
│  │  │  │Database │  │OpenAI    │  │(Sweden        │       │ │   │
│  │  │  │         │  │GPT-4o    │  │ Central)      │       │ │   │
│  │  │  │Northwind│  │          │  │               │       │ │   │
│  │  │  └─────────┘  └──────────┘  └───────────────┘       │ │   │
│  │  │                                                        │ │   │
│  │  │  ┌──────────────────────────────────────────┐       │ │   │
│  │  │  │      Application Insights                 │       │ │   │
│  │  │  │      (Telemetry & Monitoring)             │       │ │   │
│  │  │  │                   │                        │       │ │   │
│  │  │  │                   ▼                        │       │ │   │
│  │  │  │      Log Analytics Workspace              │       │ │   │
│  │  │  │      (Centralized Logging)                │       │ │   │
│  │  │  └──────────────────────────────────────────┘       │ │   │
│  │  │                                                        │ │   │
│  │  │  Optional GenAI Resources (deployed with -DeployGenAI):│ │
│  │  │  ┌──────────────────────────────────────────┐       │ │   │
│  │  │  │      Azure AI Search                      │       │ │   │
│  │  │  │      (Cognitive Search)                   │       │ │   │
│  │  │  └──────────────────────────────────────────┘       │ │   │
│  │  └────────────────────────────────────────────────────┘ │   │
│  └──────────────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────────────────┘
```

## Component Details

### App Service (Web Application)
- **Type**: Linux App Service Plan (Standard S1)
- **Runtime**: .NET 8.0
- **Features**:
  - Razor Pages dashboard for expense management
  - REST API with Swagger documentation
  - AI-powered chat interface
  - HTTPS only, Always On enabled

### User-Assigned Managed Identity
- **Purpose**: Passwordless authentication to Azure services
- **Eliminates**: Need for connection strings with passwords
- **Grants access to**:
  - Azure SQL Database (db_datareader, db_datawriter, EXECUTE)
  - Azure OpenAI (Cognitive Services OpenAI User role)
  - Azure AI Search (Search Index Data Reader role)

### Azure SQL Database
- **Server**: Entra ID (Azure AD) authentication ONLY
- **Database**: Northwind
- **Tier**: Basic (upgradable for production)
- **Schema**: 
  - Users, Roles, Expenses, Categories, Status
  - Stored procedures for all data operations
- **Security**: 
  - No SQL authentication enabled
  - Managed Identity access only
  - Firewall rules for Azure services

### Azure OpenAI (Optional)
- **Location**: Sweden Central (better quota availability)
- **Model**: GPT-4o
- **Capacity**: 8 units
- **Authentication**: Managed Identity (no API keys)
- **Features**: Function calling for database operations

### Azure AI Search (Optional)
- **Tier**: Basic
- **Purpose**: Vector search capabilities for AI features
- **Authentication**: Managed Identity

### Monitoring & Diagnostics
- **Application Insights**: Real-time application telemetry
- **Log Analytics Workspace**: Centralized log storage
- **Diagnostics Enabled**:
  - App Service: HTTP logs, console logs, application logs
  - SQL Database: Query statistics, errors, performance metrics

## Data Flow

### User Accessing Dashboard
1. User navigates to https://[app-name].azurewebsites.net/Index
2. App Service authenticates to SQL using Managed Identity
3. Stored procedures retrieve expense data
4. Dashboard displays expenses with error handling

### AI Chat Interaction
1. User sends message via Chat page
2. Chat service authenticates to Azure OpenAI using Managed Identity
3. OpenAI processes message and may call functions
4. Functions execute stored procedures via APIs
5. Results returned to user in conversational format

### API Access
1. Client calls REST API endpoint
2. API controller validates request
3. Service layer calls stored procedures
4. Data returned as JSON

## Security Highlights

✅ **No Passwords in Code**
- Managed Identity for all Azure service authentication
- Connection strings use "Active Directory Managed Identity" auth

✅ **Entra ID Only**
- SQL Server has `azureADOnlyAuthentication: true`
- No SQL username/password authentication

✅ **Role-Based Access**
- Minimal permissions granted to Managed Identity
- Database-level roles only (not server-level)

✅ **Secure Communication**
- HTTPS enforced on App Service
- TLS 1.2 minimum
- Encrypted SQL connections

✅ **Centralized Logging**
- All diagnostics sent to Log Analytics
- Audit trails maintained
- Performance monitoring enabled

## Deployment Flow

```
Developer/CI → Azure CLI → Bicep Templates → Azure Resources
                    ↓
            Infrastructure Script
                    ↓
        ┌───────────────────────────┐
        │ 1. Create Resource Group  │
        │ 2. Deploy Managed Identity│
        │ 3. Deploy App Service     │
        │ 4. Deploy SQL Database    │
        │ 5. Deploy Monitoring      │
        │ 6. Import Schema          │
        │ 7. Create MI User         │
        │ 8. Configure Settings     │
        └───────────────────────────┘
                    ↓
            Application Script
                    ↓
        ┌───────────────────────────┐
        │ 1. Build .NET App         │
        │ 2. Create Zip Package     │
        │ 3. Deploy to App Service  │
        └───────────────────────────┘
```

## Cost Estimation (UK South)

**Base Infrastructure** (without GenAI):
- App Service Plan S1: ~£60/month
- Azure SQL Basic: ~£4/month
- Log Analytics: ~£2/month (first 5GB free)
- Application Insights: Included
- **Total: ~£66/month**

**With GenAI**:
- Azure OpenAI (8 capacity): ~£300/month
- Azure AI Search Basic: ~£60/month
- **Total: ~£426/month**

*Prices are estimates and may vary by region and usage.*

## Scalability Considerations

**Current Setup**: Development/Test
- App Service: S1 (1 instance)
- SQL Database: Basic (5 DTUs)
- Suitable for: POC, development, small workloads

**Production Recommendations**:
- App Service: Scale to P1v2 or higher, enable autoscale
- SQL Database: Scale to Standard S2+ or Premium
- Enable zone redundancy for high availability
- Add Azure Front Door or Application Gateway
- Implement Azure Key Vault for additional secret management

## Next Steps

1. **Deploy**: Run `./deploy-all.ps1` to create everything
2. **Monitor**: Check Application Insights for telemetry
3. **Scale**: Adjust SKUs based on usage patterns
4. **Secure**: Review firewall rules and access controls
5. **Optimize**: Use Query Store insights to optimize SQL
