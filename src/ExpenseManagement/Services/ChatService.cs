using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using ExpenseManagement.Models;
using OpenAI.Chat;
using System.Text.Json;

namespace ExpenseManagement.Services;

public interface IChatService
{
    Task<string> ProcessMessageAsync(string userMessage);
    bool IsConfigured { get; }
}

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ChatService> _logger;
    private readonly AzureOpenAIClient? _openAIClient;
    private readonly string _modelName;

    public bool IsConfigured => _openAIClient != null;

    public ChatService(IConfiguration configuration, IExpenseService expenseService, ILogger<ChatService> logger)
    {
        _configuration = configuration;
        _expenseService = expenseService;
        _logger = logger;
        _modelName = configuration["GenAISettings:OpenAIModelName"] ?? "gpt-4o";

        var endpoint = configuration["GenAISettings:OpenAIEndpoint"];
        if (!string.IsNullOrEmpty(endpoint))
        {
            try
            {
                var managedIdentityClientId = configuration["ManagedIdentityClientId"];
                Azure.Core.TokenCredential credential;

                if (!string.IsNullOrEmpty(managedIdentityClientId))
                {
                    _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                    credential = new ManagedIdentityCredential(managedIdentityClientId);
                }
                else
                {
                    _logger.LogInformation("Using DefaultAzureCredential");
                    credential = new DefaultAzureCredential();
                }

                _openAIClient = new AzureOpenAIClient(new Uri(endpoint), credential);
                _logger.LogInformation("Azure OpenAI client initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Azure OpenAI client");
            }
        }
        else
        {
            _logger.LogWarning("GenAI settings not configured. Chat functionality will be limited.");
        }
    }

    public async Task<string> ProcessMessageAsync(string userMessage)
    {
        if (_openAIClient == null)
        {
            return "Chat functionality is not available. To enable it, deploy with the -DeployGenAI switch:\n\n" +
                   "```powershell\n.\\deploy-infra\\deploy.ps1 -ResourceGroup \"your-rg\" -Location \"uksouth\" -DeployGenAI\n```";
        }

        try
        {
            var chatClient = _openAIClient.GetChatClient(_modelName);

            var tools = new List<ChatTool>
            {
                ChatTool.CreateFunctionTool(
                    "get_expenses",
                    "Retrieves all expenses from the database",
                    BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")),
                ChatTool.CreateFunctionTool(
                    "get_expenses_by_status",
                    "Retrieves expenses filtered by status (Draft, Submitted, Approved, Rejected)",
                    BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"status\":{\"type\":\"string\",\"description\":\"Status to filter by: Draft, Submitted, Approved, or Rejected\"}},\"required\":[\"status\"]}")),
                ChatTool.CreateFunctionTool(
                    "get_dashboard_stats",
                    "Retrieves dashboard statistics including total expenses, pending approvals, and approved amounts",
                    BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")),
                ChatTool.CreateFunctionTool(
                    "create_expense",
                    "Creates a new expense record",
                    BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"amount\":{\"type\":\"number\",\"description\":\"Amount in GBP\"},\"category\":{\"type\":\"string\",\"description\":\"Category: Travel, Meals, Supplies, Accommodation, or Other\"},\"description\":{\"type\":\"string\",\"description\":\"Description of the expense\"},\"date\":{\"type\":\"string\",\"description\":\"Date of expense in YYYY-MM-DD format\"}},\"required\":[\"amount\",\"category\",\"description\"]}")),
                ChatTool.CreateFunctionTool(
                    "approve_expense",
                    "Approves a submitted expense",
                    BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\",\"description\":\"The ID of the expense to approve\"}},\"required\":[\"expenseId\"]}")),
                ChatTool.CreateFunctionTool(
                    "get_categories",
                    "Retrieves all expense categories",
                    BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}"))
            };

            var systemPrompt = @"You are a helpful assistant for the Expense Management System. You can help users:
- View their expenses and statistics
- Create new expense records
- Approve or reject submitted expenses (if they have manager permissions)
- Answer questions about the expense management process

When showing expense data, format it nicely with amounts in British Pounds (£).
When creating expenses, confirm the details before proceeding.
Be concise but helpful in your responses.";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userMessage)
            };

            var options = new ChatCompletionOptions();
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }

            var response = await chatClient.CompleteChatAsync(messages, options);
            var completion = response.Value;

            // Handle tool calls
            while (completion.FinishReason == ChatFinishReason.ToolCalls)
            {
                messages.Add(new AssistantChatMessage(completion));

                foreach (var toolCall in completion.ToolCalls)
                {
                    var result = await ExecuteToolAsync(toolCall);
                    messages.Add(new ToolChatMessage(toolCall.Id, result));
                }

                response = await chatClient.CompleteChatAsync(messages, options);
                completion = response.Value;
            }

            return completion.Content[0].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return $"I encountered an error processing your request: {ex.Message}";
        }
    }

    private async Task<string> ExecuteToolAsync(ChatToolCall toolCall)
    {
        var arguments = JsonDocument.Parse(toolCall.FunctionArguments.ToString());

        try
        {
            return toolCall.FunctionName switch
            {
                "get_expenses" => await GetExpensesToolAsync(),
                "get_expenses_by_status" => await GetExpensesByStatusToolAsync(arguments),
                "get_dashboard_stats" => await GetDashboardStatsToolAsync(),
                "create_expense" => await CreateExpenseToolAsync(arguments),
                "approve_expense" => await ApproveExpenseToolAsync(arguments),
                "get_categories" => await GetCategoriesToolAsync(),
                _ => JsonSerializer.Serialize(new { error = $"Unknown function: {toolCall.FunctionName}" })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolCall.FunctionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private async Task<string> GetExpensesToolAsync()
    {
        var (expenses, error) = await _expenseService.GetAllExpensesAsync();
        if (error != null)
        {
            return JsonSerializer.Serialize(new { error = error.Message });
        }
        return JsonSerializer.Serialize(expenses.Select(e => new
        {
            e.ExpenseId,
            e.UserName,
            e.CategoryName,
            e.FormattedAmount,
            e.StatusName,
            e.Description,
            Date = e.FormattedDate
        }));
    }

    private async Task<string> GetExpensesByStatusToolAsync(JsonDocument arguments)
    {
        var status = arguments.RootElement.GetProperty("status").GetString() ?? "Submitted";
        var (expenses, error) = await _expenseService.GetExpensesByStatusAsync(status);
        if (error != null)
        {
            return JsonSerializer.Serialize(new { error = error.Message });
        }
        return JsonSerializer.Serialize(expenses.Select(e => new
        {
            e.ExpenseId,
            e.UserName,
            e.CategoryName,
            e.FormattedAmount,
            e.StatusName,
            e.Description,
            Date = e.FormattedDate
        }));
    }

    private async Task<string> GetDashboardStatsToolAsync()
    {
        var (stats, error) = await _expenseService.GetDashboardStatsAsync();
        if (error != null)
        {
            return JsonSerializer.Serialize(new { error = error.Message });
        }
        return JsonSerializer.Serialize(new
        {
            stats?.TotalExpenses,
            stats?.PendingApprovals,
            ApprovedAmount = stats?.FormattedApprovedAmount,
            stats?.ApprovedCount
        });
    }

    private async Task<string> CreateExpenseToolAsync(JsonDocument arguments)
    {
        var root = arguments.RootElement;
        var amount = root.GetProperty("amount").GetDecimal();
        var categoryName = root.GetProperty("category").GetString() ?? "Other";
        var description = root.GetProperty("description").GetString() ?? "";
        var dateStr = root.TryGetProperty("date", out var dateElement) ? dateElement.GetString() : null;
        var date = !string.IsNullOrEmpty(dateStr) ? DateTime.Parse(dateStr) : DateTime.Today;

        // Get category ID
        var (categories, _) = await _expenseService.GetCategoriesAsync();
        var category = categories.FirstOrDefault(c => c.CategoryName.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
        if (category == null)
        {
            return JsonSerializer.Serialize(new { error = $"Category '{categoryName}' not found. Available categories: {string.Join(", ", categories.Select(c => c.CategoryName))}" });
        }

        // Use default user (Alice) for demo
        var request = new CreateExpenseRequest
        {
            UserId = 1,
            CategoryId = category.CategoryId,
            Amount = amount,
            Currency = "GBP",
            ExpenseDate = date,
            Description = description
        };

        var (expenseId, error) = await _expenseService.CreateExpenseAsync(request);
        if (error != null)
        {
            return JsonSerializer.Serialize(new { error = error.Message });
        }

        return JsonSerializer.Serialize(new
        {
            success = true,
            expenseId,
            message = $"Created expense #{expenseId}: £{amount:N2} for {categoryName}"
        });
    }

    private async Task<string> ApproveExpenseToolAsync(JsonDocument arguments)
    {
        var expenseId = arguments.RootElement.GetProperty("expenseId").GetInt32();
        
        // Use Bob Manager (ID 2) as the reviewer
        var (success, error) = await _expenseService.ApproveExpenseAsync(expenseId, 2);
        if (error != null)
        {
            return JsonSerializer.Serialize(new { error = error.Message });
        }

        return JsonSerializer.Serialize(new
        {
            success,
            message = success ? $"Expense #{expenseId} has been approved" : $"Could not approve expense #{expenseId}"
        });
    }

    private async Task<string> GetCategoriesToolAsync()
    {
        var (categories, error) = await _expenseService.GetCategoriesAsync();
        if (error != null)
        {
            return JsonSerializer.Serialize(new { error = error.Message });
        }
        return JsonSerializer.Serialize(categories.Select(c => c.CategoryName));
    }
}
