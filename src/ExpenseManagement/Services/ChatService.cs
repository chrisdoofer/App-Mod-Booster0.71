using Azure.AI.OpenAI;
using Azure;
using Azure.Identity;
using ExpenseManagement.Models;
using System.Text.Json;
using OpenAI.Chat;

namespace ExpenseManagement.Services;

public class ChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly ExpenseService _expenseService;
    private readonly string? _openAIEndpoint;
    private readonly string? _modelName;

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, ExpenseService expenseService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
        _openAIEndpoint = _configuration["GenAISettings:OpenAIEndpoint"];
        _modelName = _configuration["GenAISettings:OpenAIModelName"];
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_openAIEndpoint) && !string.IsNullOrEmpty(_modelName);

    public async Task<string> SendMessageAsync(string userMessage, List<ChatMessage> conversationHistory)
    {
        if (!IsConfigured)
        {
            return "AI Chat is not configured. To enable this feature, redeploy the infrastructure using the -DeployGenAI switch.";
        }

        try
        {
            var client = CreateOpenAIClient();
            var chatClient = client.GetChatClient(_modelName!);

            // Build the conversation with system prompt
            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(GetSystemPrompt())
            };

            messages.AddRange(conversationHistory);
            messages.Add(ChatMessage.CreateUserMessage(userMessage));

            // Define available functions
            var chatOptions = new ChatCompletionOptions();
            DefineTools(chatOptions);

            // Send the initial request
            var response = await chatClient.CompleteChatAsync(messages, chatOptions);
            var responseMessage = response.Value;

            // Handle function calls if needed
            while (responseMessage.FinishReason == ChatFinishReason.ToolCalls && responseMessage.ToolCalls.Count > 0)
            {
                messages.Add(ChatMessage.CreateAssistantMessage(responseMessage.ToolCalls, responseMessage.Content));

                foreach (var toolCall in responseMessage.ToolCalls)
                {
                    var functionName = toolCall.FunctionName;
                    var functionArgs = toolCall.FunctionArguments.ToString();

                    _logger.LogInformation("AI requested function call: {FunctionName} with args: {Args}", functionName, functionArgs);

                    var functionResult = await ExecuteFunctionAsync(functionName, functionArgs);
                    messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, functionResult));
                }

                // Get the next response
                response = await chatClient.CompleteChatAsync(messages, chatOptions);
                responseMessage = response.Value;
            }

            return responseMessage.Content[0].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat service");
            return $"I encountered an error: {ex.Message}";
        }
    }

    private AzureOpenAIClient CreateOpenAIClient()
    {
        var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
        TokenCredential credential;

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

        return new AzureOpenAIClient(new Uri(_openAIEndpoint!), credential);
    }

    private string GetSystemPrompt()
    {
        return @"You are a helpful AI assistant for an Expense Management System. You can help users with the following tasks:

1. View expenses (all, by status, or by user)
2. Create new expenses
3. Get expense summaries
4. List available categories and users

When displaying lists of expenses, format them nicely with bullet points or numbered lists.
When creating expenses, confirm the details with the user before proceeding.
Always be polite and helpful. If you need more information to complete a task, ask the user.

Important: Amounts are stored in the database as pence (minor units), but you should always display and accept them as pounds (e.g., £25.40).";
    }

    private void DefineTools(ChatCompletionOptions options)
    {
        // Get all expenses
        options.Tools.Add(ChatTool.CreateFunctionTool(
            name: "get_expenses",
            description: "Retrieves all expenses from the database",
            parameters: BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
        ));

        // Get expenses by status
        options.Tools.Add(ChatTool.CreateFunctionTool(
            name: "get_expenses_by_status",
            description: "Retrieves expenses filtered by status (Draft, Submitted, Approved, or Rejected)",
            parameters: BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"status\":{\"type\":\"string\",\"description\":\"The status to filter by: Draft, Submitted, Approved, or Rejected\"}},\"required\":[\"status\"]}")
        ));

        // Get expenses by user
        options.Tools.Add(ChatTool.CreateFunctionTool(
            name: "get_expenses_by_user",
            description: "Retrieves expenses for a specific user",
            parameters: BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"userId\":{\"type\":\"integer\",\"description\":\"The user ID\"}},\"required\":[\"userId\"]}")
        ));

        // Create expense
        options.Tools.Add(ChatTool.CreateFunctionTool(
            name: "create_expense",
            description: "Creates a new expense record",
            parameters: BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"userId\":{\"type\":\"integer\",\"description\":\"The user ID\"},\"categoryId\":{\"type\":\"integer\",\"description\":\"The category ID\"},\"amount\":{\"type\":\"number\",\"description\":\"The amount in pounds (e.g., 25.40)\"},\"expenseDate\":{\"type\":\"string\",\"description\":\"The expense date in YYYY-MM-DD format\"},\"description\":{\"type\":\"string\",\"description\":\"Description of the expense\"}},\"required\":[\"userId\",\"categoryId\",\"amount\",\"expenseDate\"]}")
        ));

        // Get summary
        options.Tools.Add(ChatTool.CreateFunctionTool(
            name: "get_expense_summary",
            description: "Gets a summary of expenses grouped by status with counts and totals",
            parameters: BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
        ));

        // Get categories
        options.Tools.Add(ChatTool.CreateFunctionTool(
            name: "get_categories",
            description: "Lists all available expense categories",
            parameters: BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
        ));

        // Get users
        options.Tools.Add(ChatTool.CreateFunctionTool(
            name: "get_users",
            description: "Lists all users in the system",
            parameters: BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
        ));
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string functionArgs)
    {
        try
        {
            switch (functionName)
            {
                case "get_expenses":
                    var expenses = await _expenseService.GetAllExpensesAsync();
                    return JsonSerializer.Serialize(expenses);

                case "get_expenses_by_status":
                    var statusArgs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(functionArgs);
                    var status = statusArgs!["status"].GetString()!;
                    var expensesByStatus = await _expenseService.GetExpensesByStatusAsync(status);
                    return JsonSerializer.Serialize(expensesByStatus);

                case "get_expenses_by_user":
                    var userArgs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(functionArgs);
                    var userId = userArgs!["userId"].GetInt32();
                    var expensesByUser = await _expenseService.GetExpensesByUserIdAsync(userId);
                    return JsonSerializer.Serialize(expensesByUser);

                case "create_expense":
                    var createArgs = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(functionArgs);
                    var request = new CreateExpenseRequest
                    {
                        UserId = createArgs!["userId"].GetInt32(),
                        CategoryId = createArgs["categoryId"].GetInt32(),
                        Amount = createArgs["amount"].GetDecimal(),
                        ExpenseDate = DateTime.Parse(createArgs["expenseDate"].GetString()!),
                        Description = createArgs.ContainsKey("description") ? createArgs["description"].GetString() : null,
                        Currency = "GBP"
                    };
                    var expenseId = await _expenseService.CreateExpenseAsync(request);
                    return JsonSerializer.Serialize(new { expenseId, message = "Expense created successfully" });

                case "get_expense_summary":
                    var summary = await _expenseService.GetExpenseSummaryAsync();
                    return JsonSerializer.Serialize(summary);

                case "get_categories":
                    var categories = await _expenseService.GetAllCategoriesAsync();
                    return JsonSerializer.Serialize(categories);

                case "get_users":
                    var users = await _expenseService.GetAllUsersAsync();
                    return JsonSerializer.Serialize(users);

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
}
