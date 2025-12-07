# Architecture

## Overview

The Expense Management System is a modern, cloud-native application built on Azure using a secure, scalable architecture. The system leverages Azure's managed services and follows security best practices including passwordless authentication and infrastructure as code.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                         Azure Cloud                              │
│                                                                   │
│  ┌────────────────┐                                             │
│  │   Azure App    │                                             │
│  │    Service     │◄────────┐                                   │
│  │  (Linux/.NET 8)│         │                                   │
│  └────────┬───────┘         │                                   │
│           │                 │                                   │
│           │                 │ Managed                           │
│           │                 │ Identity                          │
│           │                 │ (No Passwords!)                   │
│  ┌────────▼───────┐    ┌────┴──────┐                           │
│  │   Azure SQL    │    │  User-    │                           │
│  │   Database     │    │ Assigned  │                           │
│  │ (Northwind DB) │    │ Managed   │                           │
│  │  Entra ID Auth │    │ Identity  │                           │
│  └────────────────┘    └───┬───────┘                           │
│                            │                                   │
│  ┌────────────────┐        │                                   │
│  │  Application   │        │                                   │
│  │   Insights     │◄───────┤                                   │
│  └────────────────┘        │                                   │
│                            │                                   │
│  ┌────────────────┐        │                                   │
│  │  Log Analytics │        │                                   │
│  │   Workspace    │◄───────┤                                   │
│  └────────────────┘        │                                   │
│                            │                                   │
│  ┌────────────────┐        │ (Optional - with -DeployGenAI)   │
│  │  Azure OpenAI  │        │                                   │
│  │   (GPT-4o)     │◄───────┤                                   │
│  └────────────────┘        │                                   │
│                            │                                   │
│  ┌────────────────┐        │                                   │
│  │   AI Search    │◄───────┘                                   │
│  │   (RAG)        │                                            │
│  └────────────────┘                                            │
└─────────────────────────────────────────────────────────────────┘
```

## Components

### Application Layer

**Azure App Service (Linux)**
- Hosts the ASP.NET 8 Razor Pages application
- Standard S1 pricing tier for production readiness
- Always On enabled to prevent cold starts
- HTTPS only with TLS 1.2+ enforcement
- Integrated with Application Insights for telemetry

**Key Features:**
- REST API with Swagger documentation
- Responsive web UI for expense management
- Optional AI chat interface (with GenAI deployment)
- Graceful error handling with fallback to dummy data

### Data Layer

**Azure SQL Database**
- Northwind database with expense management schema
- Basic tier (suitable for development/testing)
- **Entra ID-only authentication** (no SQL passwords)
- Managed Identity authentication from App Service
- Automatic backups and point-in-time restore

**Security Configuration:**
- SQL username/password authentication **disabled**
- Azure AD administrator configured
- Firewall rules for Azure services
- TLS encrypted connections

### Identity & Security

**User-Assigned Managed Identity**
- Single identity for all Azure service authentication
- Eliminates need for connection strings with secrets
- Granted appropriate RBAC roles to access:
  - Azure SQL Database (db_datareader, db_datawriter, execute)
  - Azure OpenAI (Cognitive Services OpenAI User)
  - AI Search (Search Index Data Contributor)

**Connection String Format:**
```
Server=tcp:{server}.database.windows.net,1433;
Initial Catalog=Northwind;
Encrypt=True;
TrustServerCertificate=False;
Connection Timeout=30;
Authentication=Active Directory Managed Identity;
User Id={managed-identity-client-id};
```

### Monitoring & Observability

**Application Insights**
- Real-time application monitoring
- Performance metrics and slow query detection
- Exception tracking and diagnostics
- Custom telemetry for business events

**Log Analytics Workspace**
- Centralized log collection
- KQL queries for advanced analytics
- Diagnostic logs from:
  - App Service (HTTP logs, console output, application logs)
  - SQL Database (query stats, errors, deadlocks)

### GenAI Features (Optional)

**Azure OpenAI**
- GPT-4o model deployed in Sweden Central
- Capacity: 8 units for proof-of-concept
- Managed Identity authentication
- Function calling for database operations

**Azure AI Search**
- Basic tier search service
- RAG (Retrieval-Augmented Generation) support
- Indexes contextual information for enhanced chat responses

## Data Flow

### 1. User Requests

```
User Browser → App Service → ExpenseService → Stored Procedures → SQL Database
```

### 2. API Requests

```
API Client → Swagger/REST API → Controllers → Services → Database
```

### 3. AI Chat (with GenAI)

```
User → Chat UI → ChatService → Azure OpenAI (with function calling)
                    ↓
              App Service APIs → Database
```

### 4. Authentication Flow

```
App Service → Managed Identity Token → Azure AD
                    ↓
         Uses Token to Access:
              - SQL Database
              - Azure OpenAI
              - AI Search
```

## Deployment Model

### Infrastructure as Code (IaC)

All Azure resources are defined in Bicep templates:

```
deploy-infra/
├── main.bicep                    # Orchestration
├── main.bicepparam              # Parameters
└── modules/
    ├── managed-identity.bicep   # User-assigned identity
    ├── app-service.bicep        # App Service + plan
    ├── azure-sql.bicep          # SQL Server + database
    ├── monitoring.bicep         # App Insights + Log Analytics
    ├── app-service-diagnostics.bicep  # Diagnostic settings
    └── genai.bicep             # Azure OpenAI + AI Search (optional)
```

### Two-Phase Deployment

**Phase 1: Infrastructure** (`deploy-infra/deploy.ps1`)
1. Create all Azure resources via Bicep
2. Configure SQL Server with Entra ID admin
3. Import database schema
4. Create managed identity database user
5. Deploy stored procedures
6. Configure App Service settings
7. Save deployment context

**Phase 2: Application** (`deploy-app/deploy.ps1`)
1. Read deployment context
2. Build .NET application
3. Create deployment package
4. Deploy to App Service

**Unified Deployment** (`deploy-all.ps1`)
- Runs both phases sequentially
- Single command for complete deployment

## Security Architecture

### Zero Secrets Approach

✅ **No Passwords**
- SQL authentication via Managed Identity
- No database passwords stored anywhere

✅ **No API Keys**
- Azure OpenAI accessed via Managed Identity
- No API keys in configuration

✅ **No Connection String Secrets**
- Connection strings use Managed Identity authentication
- User Id is Managed Identity Client ID (not a secret)

✅ **No Secrets in Source Control**
- All sensitive configuration via Azure App Service settings
- Settings injected at runtime

✅ **No Secrets in CI/CD**
- GitHub Actions uses OIDC (OpenID Connect)
- Temporary tokens instead of persistent secrets

### Compliance Features

- **MCAPS [SFI-ID4.2.2] SQL DB - Safe Secrets Standard**: Azure AD-only authentication
- **TLS 1.2+ Encryption**: All connections encrypted in transit
- **HTTPS Only**: Web traffic forced to HTTPS
- **RBAC**: Role-based access control for all resources
- **Audit Logs**: All operations logged to Log Analytics

## Scaling Considerations

### Current Configuration
- **App Service**: Standard S1 (1 instance)
- **SQL Database**: Basic tier
- **AI Search**: Basic tier
- **Azure OpenAI**: 8 capacity units

### Scale-Out Options

**App Service**
- Horizontal scaling: Add instances (manual or auto-scale)
- Vertical scaling: Upgrade to higher SKU

**SQL Database**
- Scale to higher tier (Standard, Premium, Hyperscale)
- Add read replicas for read-heavy workloads
- Implement connection pooling

**Azure OpenAI**
- Increase capacity units
- Deploy multiple models for load distribution

## Cost Optimization

- Use **Basic/Standard tiers** for non-production
- **S1 App Service** balances cost and performance
- **Managed Identity** eliminates Key Vault costs
- **Shared Log Analytics** workspace across resources

## Disaster Recovery

**Built-in Protection:**
- SQL Database: Automated backups (7-35 days)
- App Service: Deploy to multiple regions for HA
- IaC Templates: Redeploy to any region quickly

**Backup Strategy:**
- Infrastructure: Version-controlled Bicep templates
- Database: Automated SQL backups
- Application: Source code in GitHub

## Monitoring Strategy

### Key Metrics

**Application Performance:**
- Response time (target < 500ms)
- Request rate and throughput
- Error rate (target < 1%)
- Dependency call duration

**Database Performance:**
- DTU/CPU utilization
- Query execution time
- Deadlocks and timeouts
- Connection pool usage

**Cost Monitoring:**
- Daily spend by resource
- Azure OpenAI token usage
- App Service compute hours

### Alerting

Recommended alerts:
- HTTP 5xx errors > threshold
- Response time > 2 seconds
- SQL Database DTU > 80%
- Failed login attempts
- High GenAI token consumption

## Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| **Runtime** | .NET | 8.0 (LTS) |
| **Web Framework** | ASP.NET Core Razor Pages | 8.0 |
| **Database** | Azure SQL Database | Latest |
| **ORM** | ADO.NET (SqlClient) | Latest |
| **API Documentation** | Swagger/OpenAPI | Swashbuckle 6.x |
| **AI** | Azure OpenAI | GPT-4o |
| **Search** | Azure AI Search | Latest |
| **Monitoring** | Application Insights | Latest |
| **IaC** | Bicep | Latest |
| **CI/CD** | GitHub Actions | OIDC |

## Development Workflow

```
┌─────────────┐
│  Developer  │
└──────┬──────┘
       │
       ▼
┌─────────────────┐
│  Local Development  │
│  - .NET 8 SDK       │
│  - Azure CLI        │
│  - Visual Studio    │
└──────┬──────────────┘
       │
       ▼
┌─────────────────┐
│   Git Commit    │
└──────┬──────────┘
       │
       ▼
┌─────────────────┐
│  GitHub Actions │
│  - Build        │
│  - Deploy Infra │
│  - Deploy App   │
└──────┬──────────┘
       │
       ▼
┌─────────────────┐
│  Azure App      │
│   Service       │
└─────────────────┘
```

## Best Practices Implemented

✅ **Infrastructure as Code**: All resources defined in Bicep  
✅ **Passwordless Authentication**: Managed Identity everywhere  
✅ **Separation of Concerns**: Layered architecture (UI → Controllers → Services → Data)  
✅ **Stored Procedures**: All data access via stored procedures  
✅ **API-First Design**: REST API with Swagger documentation  
✅ **Monitoring & Logging**: Application Insights + Log Analytics  
✅ **CI/CD Automation**: GitHub Actions with OIDC  
✅ **Error Handling**: Graceful degradation with fallback data  
✅ **Security by Default**: HTTPS, TLS 1.2+, Entra ID auth  
✅ **Documentation**: Comprehensive README, architecture docs, API docs

## References

- [Azure App Service Best Practices](https://learn.microsoft.com/en-us/azure/app-service/app-service-best-practices)
- [Azure SQL Security Best Practices](https://learn.microsoft.com/en-us/azure/azure-sql/database/security-best-practice)
- [Managed Identities Overview](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview)
- [Azure OpenAI Service](https://learn.microsoft.com/en-us/azure/ai-services/openai/overview)
