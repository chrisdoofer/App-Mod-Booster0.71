![Header image](https://github.com/DougChisholm/App-Mod-Booster/blob/main/repo-header-booster.png)

# Expense Management System

A modern, cloud-native expense management application built with ASP.NET 8 and deployed on Azure. This application demonstrates best practices for Azure App Service, Azure SQL Database with managed identity, and optional Azure OpenAI integration.

## 🌟 Features

- **📝 Expense Management**: Create, submit, and track business expenses
- **✅ Approval Workflow**: Managers can approve or reject submitted expenses
- **🔍 Search & Filter**: Easily find expenses by description, category, or user
- **📊 Dashboard**: View expense statistics at a glance
- **🔐 Secure Authentication**: Uses Azure Managed Identity (no passwords or keys)
- **📚 REST API**: Complete REST API with Swagger documentation
- **🤖 AI Chat (Optional)**: Natural language interface powered by Azure OpenAI
- **📈 Monitoring**: Application Insights and Log Analytics integration

---

## 🤖 Building with AI Agents

This repository is designed to be built by **AI coding agents**. Instead of a single agent handling everything, the work is split across **specialist agents** that each focus on one domain, delivering better accuracy and reliability.

### Agent Architecture

```
┌──────────────────────────────────────────────────────────┐
│               🎯 Orchestrator Agent                       │
│  Manages sequencing, validates contracts between agents   │
└─────┬──────┬──────────┬──────────┬──────────┬────────────┘
      │      │          │          │          │
  ┌───▼──┐ ┌─▼────┐ ┌──▼───┐ ┌───▼──┐ ┌────▼───┐
  │Infra │ │  DB  │ │ .NET │ │DevOps│ │ Tester │
  │Agent │ │Agent │ │Agent │ │Agent │ │ Agent  │
  └──────┘ └──────┘ └──────┘ └──────┘ └────────┘
```

| Agent | Specialty | What It Builds |
|-------|-----------|----------------|
| 🏗️ **Infrastructure** | Bicep / Azure resources | `deploy-infra/modules/`, `main.bicep`, `main.bicepparam` |
| 🗃️ **Database** | SQL schema & stored procedures | `Database-Schema/`, `stored-procedures.sql` |
| 💻 **.NET Application** | ASP.NET 8 Razor Pages | `src/ExpenseManagement/` (pages, APIs, services, chat) |
| 🚀 **DevOps** | Deployment & CI/CD | `deploy-*.ps1`, `.github/workflows/` |
| 🧪 **Tester** | End-to-end testing | `tests/` (unit, integration, smoke tests) |
| 🎯 **Orchestrator** | Coordination | Manages sequencing and validates contracts |

### How to Use (Step-by-Step)

The agents are triggered by creating **GitHub Issues** from pre-built templates. Each template is pre-filled with full instructions, deliverables, and a validation checklist.

#### Prerequisites

- GitHub Copilot coding agent enabled on your repository
- The `.github/` folder from this repo (agents, issue templates, and shared instructions)

#### Workflow

```
  ┌───────────────┐     ┌────────────────┐     ┌──────────────┐     ┌───────────┐
  │ 1. New Issue   │────▶│ 2. Pick a      │────▶│ 3. Submit    │────▶│ 4. Assign │
  │    button      │     │    template    │     │    issue     │     │ to Copilot│
  └───────────────┘     └────────────────┘     └──────────────┘     └─────┬─────┘
                                                                          │
  ┌───────────────┐     ┌────────────────┐     ┌──────────────┐          │
  │ 7. Next phase │◀────│ 6. Merge PR    │◀────│ 5. Review PR │◀─────────┘
  └───────────────┘     └────────────────┘     └──────────────┘
```

1. Go to the **Issues** tab → click **New Issue**
2. Choose the template for the phase you want to run
3. Submit the issue (the body is already pre-filled — no editing needed)
4. **Assign the issue to Copilot** to trigger the coding agent
5. Copilot reads the instructions, creates a branch, and opens a PR
6. Review the PR, verify the validation checklist, and merge
7. Move to the next phase

#### Phase Execution Order

| Step | Template to Use | Dependencies | Can Parallel? |
|------|----------------|--------------|---------------|
| **Phase 1a** | 🏗️ Infrastructure Agent | None | ✅ Yes (with 1b) |
| **Phase 1b** | 🗃️ Database Agent | None | ✅ Yes (with 1a) |
| **Phase 2** | 💻 .NET Application Agent | Merge Phase 1a + 1b first | ❌ Sequential |
| **Phase 3** | 🚀 DevOps Agent | Merge Phase 2 first | ❌ Sequential |
| **Phase 4** | 🧪 Tester Agent | Merge Phase 3 first | ❌ Sequential |

> **Important:** Always merge the PR from the current phase before creating the issue for the next phase. Each agent depends on files created by the previous agents being present in the default branch.

#### What's in Each Template

Each issue template includes:
- **Instructions** — which files to read and in what order
- **Source prompts** — the specific prompt files the agent should follow
- **Deliverables checklist** — every file the agent must create
- **Key rules** — domain-specific pitfalls to avoid
- **Validation checklist** — how to verify the work is correct

### Contract Validation Between Phases

Agents communicate through shared contracts. The most critical alignment points to check at each merge:

| Merge Point | What to Verify |
|-------------|---------------|
| After Phase 1 | Bicep output names are documented; column mapping table is complete |
| After Phase 2 | All `GetOrdinal()` calls match stored procedure aliases; config keys match Bicep outputs |
| After Phase 3 | Deploy scripts read the correct Bicep output names; App Service settings match `appsettings.json` keys |
| After Phase 4 | Tests cover all endpoints, pages, and scripts from prior phases |

See `.github/agents/orchestrator-agent.md` for the full validation checklist.

### Alternative: Single-Agent Mode

If you prefer the original single-agent approach, create one issue and assign it to Copilot:

```
Title: Modernise my app
Body:  Read the prompt-order file and complete all tasks.
```

This uses the original `app-mod-booster.agent.md` which reads all prompts sequentially.

---

## 🏗️ Architecture

The application uses a modern, secure Azure architecture:

- **Azure App Service**: Hosts the ASP.NET 8 Razor Pages application
- **Azure SQL Database**: Stores expense data with Entra ID-only authentication
- **Managed Identity**: Passwordless authentication to all Azure services
- **Application Insights**: Telemetry and performance monitoring
- **Azure OpenAI** (optional): GPT-4o model for chat interface
- **Azure AI Search** (optional): RAG support for enhanced AI responses

See [ARCHITECTURE.md](./ARCHITECTURE.md) for detailed architecture diagrams and explanations.

## 🚀 Quick Start

### Prerequisites

- **Azure Subscription**: With permissions to create resources
- **Azure CLI**: [Install Azure CLI](https://aka.ms/installazurecliwindows)
- **.NET 8 SDK**: [Download .NET 8](https://dot.net)
- **PowerShell 7+**: [Download PowerShell](https://aka.ms/powershell-release)
- **sqlcmd (go-sqlcmd)**: Install with `winget install sqlcmd`

### Step 1: Clone the Repository

```powershell
git clone https://github.com/YourOrg/App-Mod-Booster0.7.git
cd App-Mod-Booster0.7
```

### Step 2: Login to Azure

```powershell
az login
az account set --subscription "Your Subscription Name"
```

### Step 3: Deploy Everything (Single Command)

The easiest way to deploy is with the unified deployment script:

```powershell
.\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth"
```

**With GenAI (Optional)**:
```powershell
.\deploy-all.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth" -DeployGenAI
```

This deploys infrastructure and application in one step. For more control, you can deploy them separately (see below).

### Alternative: Deploy Separately

#### Step 3a: Deploy Infrastructure

```powershell
.\deploy-infra\deploy.ps1 -ResourceGroup "rg-expensemgmt-20251206" -Location "uksouth"
```

This deploys:
- Azure App Service (Standard S1)
- Azure SQL Database (Basic tier) with sample data
- User-Assigned Managed Identity
- Application Insights & Log Analytics
- Azure OpenAI + AI Search (if -DeployGenAI specified)

#### Step 3b: Deploy Application

```powershell
.\deploy-app\deploy.ps1
```

The script automatically reads the deployment context from the infrastructure deployment.

### Step 4: Access the Application

Open your browser to:
- **Main App**: `https://your-app-name.azurewebsites.net/Index`
- **AI Chat**: `https://your-app-name.azurewebsites.net/Chat` (if GenAI deployed)
- **API Docs**: `https://your-app-name.azurewebsites.net/swagger`

## 📖 Documentation

### Agent and Architecture Documentation
- **[Agent Instructions](./.github/agents/)**: Specialist agent instruction files
- **[Orchestrator Guide](./.github/agents/orchestrator-agent.md)**: Multi-agent sequencing and validation
- **[Infrastructure Deployment](./deploy-infra/README.md)**: Detailed infrastructure deployment guide
- **[Application Deployment](./deploy-app/README.md)**: Application deployment instructions
- **[Architecture](./ARCHITECTURE.md)**: System architecture and design
- **[CI/CD Setup](./.github/CICD-SETUP.md)**: GitHub Actions with OIDC authentication

### Database Documentation
- **[Database Schema](./Database-Schema/database_schema.sql)**: SQL schema and sample data
- **[Stored Procedures](./stored-procedures.sql)**: Database stored procedures

### Testing Documentation
- **[Test Suite](./tests/ExpenseManagement.Tests/README.md)**: Comprehensive test suite documentation
- **[Testing Lessons Learned](./prompts/prompt-031-testing-lessons-learned)**: Patterns and pitfalls from test development

### Error Prevention
- **[Common Errors](./COMMON-ERRORS.md)**: ⚠️ Critical errors and how to prevent them
  - PowerShell scripting errors (splatting, JSON parsing, sqlcmd)
  - Testing configuration errors (Program class, WebApplicationFactory)
  - Bicep template errors (utcNow, newGuid, circular dependencies)
  - Database connection errors (column mapping, managed identity)
  - Configuration errors (connection strings, GenAI settings)

## 🎯 Usage

### Add an Expense

1. Navigate to **Add Expense**
2. Fill in the amount, date, category, and description
3. Click **Submit**

### View Expenses

1. Navigate to **View Expenses**
2. Use the filter box to search expenses
3. View expense details in the table

### Approve Expenses (Manager)

1. Navigate to **Approve Expenses**
2. See all submitted expenses pending approval
3. Click **Approve** or **Reject** for each expense

### AI Chat Assistant (Optional - requires GenAI deployment)

1. Navigate to **AI Chat**
2. Type natural language questions or commands like:
   - "Show me all submitted expenses"
   - "Create an expense for £50 for lunch today"
   - "What categories are available?"
   - "Approve expense #5"
3. The AI assistant will execute operations and respond with formatted results

**Note**: The chat feature requires Azure OpenAI to be deployed. Use the `-DeployGenAI` switch during infrastructure deployment to enable this feature.

### API Access

Access the REST API at `/api/expenses`, `/api/categories`, etc.

Full API documentation available at `/swagger`.

## 🔐 Security Features

### Zero Secrets Architecture

✅ **No passwords** - Managed identity for SQL authentication  
✅ **No API keys** - Managed identity for OpenAI access  
✅ **No connection strings with secrets** - All use managed identity  
✅ **No secrets in code** - Configuration via Azure App Service settings  
✅ **No secrets in CI/CD** - OIDC authentication for GitHub Actions

### Compliance

- **Entra ID-only authentication** for SQL Server
- **TLS 1.2+** encryption for all connections
- **HTTPS only** for web traffic
- **Managed identities** for all Azure service authentication
- **RBAC** for fine-grained access control

## 🛠️ Development

### Local Development

1. Install prerequisites (.NET 8 SDK, Azure CLI)
2. Login to Azure: `az login`
3. Update `appsettings.Development.json` with your SQL server details
4. Run the application:

```powershell
cd src/ExpenseManagement
dotnet run
```

Navigate to `https://localhost:5001/Index`

### Project Structure

```
App-Mod-Booster0.7/
├── .github/
│   ├── agents/             # Specialist agent instructions
│   │   ├── infra-agent.md      # 🏗️ Infrastructure (Bicep)
│   │   ├── database-agent.md   # 🗃️ Database (SQL)
│   │   ├── dotnet-agent.md     # 💻 .NET Application
│   │   ├── devops-agent.md     # 🚀 DevOps (deployment)
│   │   ├── tester-agent.md     # 🧪 Tester (E2E tests)
│   │   ├── orchestrator-agent.md # 🎯 Orchestrator
│   │   └── agent-context-schema.json # Shared contract
│   ├── workflows/          # GitHub Actions workflows
│   └── copilot-instructions.md # Shared rules for all agents
├── deploy-infra/           # Infrastructure as Code
│   ├── modules/            # Bicep modules
│   ├── main.bicep          # Main orchestration template
│   ├── deploy.ps1          # Deployment script
│   └── README.md           # Infrastructure guide
├── deploy-app/             # Application deployment
│   ├── deploy.ps1          # App deployment script
│   └── README.md           # Deployment guide
├── src/ExpenseManagement/  # Application code
│   ├── Controllers/        # API controllers
│   ├── Models/             # Data models
│   ├── Pages/              # Razor Pages
│   ├── Services/           # Business logic
│   └── wwwroot/            # Static files
├── Database-Schema/        # SQL schema
├── Legacy-Screenshots/     # Original app screenshots
├── ARCHITECTURE.md         # Architecture documentation
├── stored-procedures.sql   # Database stored procedures
└── README.md               # This file
```

## 🔄 CI/CD

The project includes a GitHub Actions workflow for automated deployments.

### Setup CI/CD

1. Follow the instructions in [.github/CICD-SETUP.md](./.github/CICD-SETUP.md)
2. Create Azure Service Principal with OIDC
3. Configure GitHub repository variables
4. Trigger workflow from Actions tab

### Manual Workflow

Go to Actions → Deploy to Azure → Run workflow

## 🧪 Testing

### Validate Bicep Templates

```powershell
az deployment group validate `
  --resource-group "rg-test" `
  --template-file ./deploy-infra/main.bicep `
  --parameters location=uksouth baseName=expensemgmt adminObjectId="guid" adminUsername="user@domain.com"
```

### Build Application

```powershell
cd src/ExpenseManagement
dotnet build
```

### Run Tests (if available)

```powershell
dotnet test
```

## 📊 Monitoring

### Application Insights

View telemetry in Azure Portal:
1. Navigate to Application Insights resource
2. View **Live Metrics** for real-time monitoring
3. Check **Failures** for errors
4. Review **Performance** for slow requests

### Log Analytics

Query logs using KQL:

```kusto
// Recent errors
AppTraces
| where SeverityLevel >= 3
| order by TimeGenerated desc
| take 50
```

## 🤝 Contributing

This is a demonstration project. For production use:

1. Add comprehensive tests
2. Implement proper error handling
3. Add user authentication (Azure AD B2C)
4. Implement file upload for receipts
5. Add email notifications
6. Configure backup and DR

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🎓 Learning Resources

- [Azure App Service Documentation](https://learn.microsoft.com/en-us/azure/app-service/)
- [Azure SQL Database Security](https://learn.microsoft.com/en-us/azure/azure-sql/database/security-best-practice)
- [Managed Identities](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview)
- [ASP.NET Core Documentation](https://learn.microsoft.com/en-us/aspnet/core/)

## 🐛 Troubleshooting

### Database Connection Issues

**Symptom**: "Unable to connect to database" error

**Solutions**:
1. Check `AZURE_CLIENT_ID` is set in App Service configuration
2. Verify managed identity has database permissions
3. Ensure connection string includes `User Id={managedIdentityClientId}`

### Application Won't Start

**Solutions**:
1. Check Application Insights for startup errors
2. Verify all App Service settings are configured
3. Check deployment logs in Azure Portal
4. Wait 1-2 minutes for app to fully start

### CI/CD Failures

**Solutions**:
1. Review workflow logs in GitHub Actions
2. Verify OIDC configuration is correct
3. Check Service Principal has required roles
4. See [.github/CICD-SETUP.md](./.github/CICD-SETUP.md) for detailed troubleshooting

## 📞 Support

For issues or questions:
- Review documentation in this repository
- Check Application Insights logs
- Review deployment script output
- Open an issue on GitHub

---

**Built with** ❤️ **using Azure best practices and modern .NET**
