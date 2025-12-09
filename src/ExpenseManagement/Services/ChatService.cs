using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using System.Text.Json;
using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public interface IChatService
{
    bool IsConfigured { get; }
    Task<string> SendMessageAsync(string message, List<ChatMessageModel>? history = null);
}

public class ChatMessageModel
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly IExpenseService _expenseService;
    private readonly string? _openAIEndpoint;
    private readonly string? _modelName;

    public bool IsConfigured => !string.IsNullOrEmpty(_openAIEndpoint);

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, IExpenseService expenseService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
        _openAIEndpoint = configuration["GenAISettings:OpenAIEndpoint"];
        _modelName = configuration["GenAISettings:OpenAIModelName"] ?? "gpt-4o";
    }

    public async Task<string> SendMessageAsync(string message, List<ChatMessageModel>? history = null)
    {
        if (!IsConfigured)
        {
            return "AI Chat is not available. To enable it, redeploy using the -DeployGenAI switch.";
        }

        try
        {
            // Get credential for Azure OpenAI
            var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
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

            // Create OpenAI client
            var azureClient = new AzureOpenAIClient(new Uri(_openAIEndpoint!), credential);
            var chatClient = azureClient.GetChatClient(_modelName);

            // Build messages
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(GetSystemPrompt())
            };

            // Add history if provided
            if (history != null)
            {
                foreach (var msg in history)
                {
                    if (msg.Role == "user")
                        messages.Add(new UserChatMessage(msg.Content));
                    else if (msg.Role == "assistant")
                        messages.Add(new AssistantChatMessage(msg.Content));
                }
            }

            // Add current message
            messages.Add(new UserChatMessage(message));

            // Define available functions
            var tools = GetAvailableTools();

            var options = new ChatCompletionOptions();
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }

            // Send request and handle function calls
            var response = await chatClient.CompleteChatAsync(messages, options);

            // Process response, handling function calls if needed
            return await ProcessResponseAsync(chatClient, messages, response.Value, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to Azure OpenAI");
            return $"Sorry, I encountered an error: {ex.Message}";
        }
    }

    private async Task<string> ProcessResponseAsync(ChatClient chatClient, List<ChatMessage> messages, ChatCompletion response, ChatCompletionOptions options)
    {
        const int maxIterations = 10;
        int iteration = 0;

        while (response.FinishReason == ChatFinishReason.ToolCalls && iteration < maxIterations)
        {
            iteration++;
            var assistantMessage = new AssistantChatMessage(response);
            messages.Add(assistantMessage);

            foreach (var toolCall in response.ToolCalls)
            {
                _logger.LogInformation("Function call: {FunctionName}", toolCall.FunctionName);
                
                var result = await ExecuteFunctionAsync(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
                messages.Add(new ToolChatMessage(toolCall.Id, result));
            }

            var nextResponse = await chatClient.CompleteChatAsync(messages, options);
            response = nextResponse.Value;
        }

        // Return the final text response
        return response.Content?.FirstOrDefault()?.Text ?? "I'm sorry, I couldn't generate a response.";
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string argumentsJson)
    {
        try
        {
            var args = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

            switch (functionName)
            {
                case "get_all_expenses":
                    var expenses = await _expenseService.GetAllExpensesAsync();
                    return JsonSerializer.Serialize(expenses.Select(e => new
                    {
                        e.ExpenseId,
                        e.EmployeeName,
                        e.CategoryName,
                        e.Amount,
                        e.Currency,
                        e.ExpenseDate,
                        e.StatusName,
                        e.Description
                    }));

                case "get_expenses_by_status":
                    var status = args.GetProperty("status").GetString() ?? "Submitted";
                    var statusExpenses = await _expenseService.GetExpensesByStatusAsync(status);
                    return JsonSerializer.Serialize(statusExpenses.Select(e => new
                    {
                        e.ExpenseId,
                        e.EmployeeName,
                        e.CategoryName,
                        e.Amount,
                        e.Currency,
                        e.ExpenseDate,
                        e.StatusName,
                        e.Description
                    }));

                case "get_pending_approvals":
                    var pending = await _expenseService.GetPendingApprovalsAsync();
                    return JsonSerializer.Serialize(pending.Select(e => new
                    {
                        e.ExpenseId,
                        e.EmployeeName,
                        e.CategoryName,
                        e.Amount,
                        e.Currency,
                        e.ExpenseDate,
                        e.Description
                    }));

                case "get_dashboard_stats":
                    var stats = await _expenseService.GetDashboardStatsAsync();
                    return JsonSerializer.Serialize(stats);

                case "get_categories":
                    var categories = await _expenseService.GetCategoriesAsync();
                    return JsonSerializer.Serialize(categories);

                case "create_expense":
                    var userId = args.TryGetProperty("userId", out var userIdProp) ? userIdProp.GetInt32() : 1;
                    var categoryId = args.GetProperty("categoryId").GetInt32();
                    var amount = args.GetProperty("amount").GetDecimal();
                    var expenseDate = args.TryGetProperty("expenseDate", out var dateProp) 
                        ? DateTime.Parse(dateProp.GetString()!) 
                        : DateTime.Today;
                    var description = args.TryGetProperty("description", out var descProp) 
                        ? descProp.GetString() 
                        : null;
                    var submit = args.TryGetProperty("submit", out var submitProp) && submitProp.GetBoolean();

                    var createRequest = new CreateExpenseRequest
                    {
                        UserId = userId,
                        CategoryId = categoryId,
                        Amount = amount,
                        ExpenseDate = expenseDate,
                        Description = description,
                        Submit = submit
                    };
                    var newId = await _expenseService.CreateExpenseAsync(createRequest);
                    return newId > 0 
                        ? JsonSerializer.Serialize(new { success = true, expenseId = newId, message = $"Expense created with ID {newId}" })
                        : JsonSerializer.Serialize(new { success = false, message = "Failed to create expense" });

                case "approve_expense":
                    var approveId = args.GetProperty("expenseId").GetInt32();
                    var reviewerId = args.TryGetProperty("reviewerId", out var reviewerProp) ? reviewerProp.GetInt32() : 2;
                    var approved = await _expenseService.ApproveExpenseAsync(approveId, reviewerId);
                    return JsonSerializer.Serialize(new { success = approved, message = approved ? "Expense approved" : "Failed to approve expense" });

                case "reject_expense":
                    var rejectId = args.GetProperty("expenseId").GetInt32();
                    var rejectReviewerId = args.TryGetProperty("reviewerId", out var rejectReviewerProp) ? rejectReviewerProp.GetInt32() : 2;
                    var rejected = await _expenseService.RejectExpenseAsync(rejectId, rejectReviewerId);
                    return JsonSerializer.Serialize(new { success = rejected, message = rejected ? "Expense rejected" : "Failed to reject expense" });

                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private List<ChatTool> GetAvailableTools()
    {
        return new List<ChatTool>
        {
            ChatTool.CreateFunctionTool(
                "get_all_expenses",
                "Retrieves all expenses from the database",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
            ),
            ChatTool.CreateFunctionTool(
                "get_expenses_by_status",
                "Retrieves expenses filtered by status (Draft, Submitted, Approved, Rejected)",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"status\":{\"type\":\"string\",\"description\":\"The status to filter by: Draft, Submitted, Approved, or Rejected\"}},\"required\":[\"status\"]}")
            ),
            ChatTool.CreateFunctionTool(
                "get_pending_approvals",
                "Retrieves all expenses waiting for approval (status = Submitted)",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
            ),
            ChatTool.CreateFunctionTool(
                "get_dashboard_stats",
                "Gets summary statistics: total expenses, pending approvals, approved amount, and approved count",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
            ),
            ChatTool.CreateFunctionTool(
                "get_categories",
                "Gets the list of expense categories (Travel, Meals, Supplies, Accommodation, Other)",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
            ),
            ChatTool.CreateFunctionTool(
                "create_expense",
                "Creates a new expense record",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"categoryId\":{\"type\":\"integer\",\"description\":\"Category ID (1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other)\"},\"amount\":{\"type\":\"number\",\"description\":\"Amount in GBP (e.g., 25.50)\"},\"expenseDate\":{\"type\":\"string\",\"description\":\"Date of expense in ISO format (YYYY-MM-DD)\"},\"description\":{\"type\":\"string\",\"description\":\"Description of the expense\"},\"submit\":{\"type\":\"boolean\",\"description\":\"If true, immediately submit for approval\"}},\"required\":[\"categoryId\",\"amount\"]}")
            ),
            ChatTool.CreateFunctionTool(
                "approve_expense",
                "Approves an expense (manager action)",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\",\"description\":\"The ID of the expense to approve\"}},\"required\":[\"expenseId\"]}")
            ),
            ChatTool.CreateFunctionTool(
                "reject_expense",
                "Rejects an expense (manager action)",
                BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\",\"description\":\"The ID of the expense to reject\"}},\"required\":[\"expenseId\"]}")
            )
        };
    }

    private string GetSystemPrompt()
    {
        return @"You are an AI assistant for the Expense Management application. You help users manage their expenses by:

1. **Viewing Expenses**: You can retrieve all expenses, filter by status, or show pending approvals.
2. **Creating Expenses**: You can create new expense records. Ask for category, amount, date, and description.
3. **Approving/Rejecting**: As a manager, you can approve or reject submitted expenses.
4. **Dashboard Stats**: You can provide summary statistics about expenses.

Available expense categories:
- Travel (ID: 1)
- Meals (ID: 2)
- Supplies (ID: 3)
- Accommodation (ID: 4)
- Other (ID: 5)

Expense statuses:
- Draft: Not yet submitted
- Submitted: Waiting for approval
- Approved: Approved by manager
- Rejected: Rejected by manager

When showing expenses, format amounts with the £ symbol (e.g., £25.40).
When creating expenses, confirm the details with the user before proceeding.
Be helpful, concise, and professional.";
    }
}
