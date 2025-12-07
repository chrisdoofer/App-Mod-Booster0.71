using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Core;
using ExpenseManagement.Models;
using System.Text.Json;
using OpenAI.Chat;

namespace ExpenseManagement.Services;

public interface IChatService
{
    Task<string> SendMessageAsync(string userMessage, List<ChatMessage> conversationHistory);
}

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ChatService> _logger;
    private readonly string? _openAIEndpoint;
    private readonly string? _openAIModelName;

    public ChatService(
        IConfiguration configuration,
        IExpenseService expenseService,
        ILogger<ChatService> logger)
    {
        _configuration = configuration;
        _expenseService = expenseService;
        _logger = logger;
        _openAIEndpoint = _configuration["GenAISettings:OpenAIEndpoint"];
        _openAIModelName = _configuration["GenAISettings:OpenAIModelName"];
    }

    public async Task<string> SendMessageAsync(string userMessage, List<ChatMessage> conversationHistory)
    {
        // Check if GenAI resources are configured
        if (string.IsNullOrEmpty(_openAIEndpoint) || string.IsNullOrEmpty(_openAIModelName))
        {
            return "Chat functionality requires Azure OpenAI to be deployed. Please run the deployment with the -DeployGenAI switch.";
        }

        try
        {
            // Create OpenAI client with Managed Identity authentication
            var credential = CreateCredential();
            var client = new AzureOpenAIClient(new Uri(_openAIEndpoint), credential);
            var chatClient = client.GetChatClient(_openAIModelName);

            // Build the conversation with system message
            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(GetSystemPrompt())
            };

            // Add conversation history
            messages.AddRange(conversationHistory);

            // Add the new user message
            messages.Add(ChatMessage.CreateUserMessage(userMessage));

            // Define available functions
            var options = new ChatCompletionOptions();
            AddFunctionTools(options);

            // Execute the chat completion with function calling
            var response = await chatClient.CompleteChatAsync(messages, options);

            // Handle function calls
            while (response.Value.FinishReason == ChatFinishReason.ToolCalls)
            {
                // Add assistant's response with tool calls to history
                messages.Add(ChatMessage.CreateAssistantMessage(response.Value.ToolCalls));

                // Execute each function call
                foreach (var toolCall in response.Value.ToolCalls)
                {
                    var functionResult = await ExecuteFunctionAsync(toolCall.FunctionName, toolCall.FunctionArguments);
                    messages.Add(ChatMessage.CreateToolMessage(toolCall.Id, functionResult));
                }

                // Get the next response
                response = await chatClient.CompleteChatAsync(messages, options);
            }

            return response.Value.Content[0].Text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat service");
            return $"I encountered an error: {ex.Message}. Please check the Application Insights logs for more details.";
        }
    }

    private TokenCredential CreateCredential()
    {
        var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
        
        if (!string.IsNullOrEmpty(managedIdentityClientId))
        {
            _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
            return new ManagedIdentityCredential(managedIdentityClientId);
        }
        else
        {
            _logger.LogInformation("Using DefaultAzureCredential");
            return new DefaultAzureCredential();
        }
    }

    private string GetSystemPrompt()
    {
        return @"You are an AI assistant for an Expense Management System. You help users manage their business expenses.

Available functions:
- get_all_expenses: Retrieve all expenses from the database
- get_expenses_by_status: Get expenses filtered by status (Draft, Submitted, Approved, Rejected)
- get_expense_by_id: Get details of a specific expense by ID
- create_expense: Create a new expense record (requires amount, date, categoryId, description, userId)
- submit_expense: Submit an expense for approval (changes status from Draft to Submitted)
- approve_expense: Approve an expense (requires expenseId and reviewerId)
- reject_expense: Reject an expense (requires expenseId and reviewerId)
- search_expenses: Search expenses by description, category, or user
- get_all_categories: Get list of available expense categories
- get_all_users: Get list of users in the system
- get_all_statuses: Get list of available expense statuses

When a user asks to perform an action (like ""add an expense"" or ""approve expense #5""), use the appropriate function to execute it.

When displaying lists of data, format them nicely using markdown syntax:
- Use **bold** for emphasis
- Use numbered lists (1., 2., 3.) for ordered items
- Use bullet points (-, *) for unordered items
- Be concise and clear

Always confirm successful operations and provide helpful feedback.";
    }

    private void AddFunctionTools(ChatCompletionOptions options)
    {
        // Get all expenses
        options.Tools.Add(ChatTool.CreateFunctionTool(
            "get_all_expenses",
            "Retrieves all expenses from the database",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
        ));

        // Get expenses by status
        options.Tools.Add(ChatTool.CreateFunctionTool(
            "get_expenses_by_status",
            "Get expenses filtered by status",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"statusName\":{\"type\":\"string\",\"description\":\"The status to filter by (Draft, Submitted, Approved, Rejected)\"}},\"required\":[\"statusName\"]}")
        ));

        // Get expense by ID
        options.Tools.Add(ChatTool.CreateFunctionTool(
            "get_expense_by_id",
            "Get details of a specific expense by its ID",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\",\"description\":\"The ID of the expense\"}},\"required\":[\"expenseId\"]}")
        ));

        // Create expense
        options.Tools.Add(ChatTool.CreateFunctionTool(
            "create_expense",
            "Create a new expense record",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"amount\":{\"type\":\"number\",\"description\":\"The expense amount\"},\"expenseDate\":{\"type\":\"string\",\"description\":\"The date of the expense in ISO format (YYYY-MM-DD)\"},\"categoryId\":{\"type\":\"integer\",\"description\":\"The category ID\"},\"description\":{\"type\":\"string\",\"description\":\"Description of the expense\"},\"userId\":{\"type\":\"integer\",\"description\":\"The user ID who created the expense\"}},\"required\":[\"amount\",\"expenseDate\",\"categoryId\",\"description\",\"userId\"]}")
        ));

        // Submit expense
        options.Tools.Add(ChatTool.CreateFunctionTool(
            "submit_expense",
            "Submit an expense for approval (changes status from Draft to Submitted)",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\",\"description\":\"The ID of the expense to submit\"}},\"required\":[\"expenseId\"]}")
        ));

        // Approve expense
        options.Tools.Add(ChatTool.CreateFunctionTool(
            "approve_expense",
            "Approve an expense",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\",\"description\":\"The ID of the expense to approve\"},\"reviewerId\":{\"type\":\"integer\",\"description\":\"The ID of the reviewer approving the expense\"}},\"required\":[\"expenseId\",\"reviewerId\"]}")
        ));

        // Reject expense
        options.Tools.Add(ChatTool.CreateFunctionTool(
            "reject_expense",
            "Reject an expense",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"expenseId\":{\"type\":\"integer\",\"description\":\"The ID of the expense to reject\"},\"reviewerId\":{\"type\":\"integer\",\"description\":\"The ID of the reviewer rejecting the expense\"}},\"required\":[\"expenseId\",\"reviewerId\"]}")
        ));

        // Search expenses
        options.Tools.Add(ChatTool.CreateFunctionTool(
            "search_expenses",
            "Search expenses by description, category, or user",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{\"filterText\":{\"type\":\"string\",\"description\":\"Text to search for in expense descriptions\"}},\"required\":[\"filterText\"]}")
        ));

        // Get categories
        options.Tools.Add(ChatTool.CreateFunctionTool(
            "get_all_categories",
            "Get list of all available expense categories",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
        ));

        // Get users
        options.Tools.Add(ChatTool.CreateFunctionTool(
            "get_all_users",
            "Get list of all users in the system",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
        ));

        // Get statuses
        options.Tools.Add(ChatTool.CreateFunctionTool(
            "get_all_statuses",
            "Get list of all available expense statuses",
            BinaryData.FromString("{\"type\":\"object\",\"properties\":{},\"required\":[]}")
        ));
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, BinaryData functionArguments)
    {
        try
        {
            _logger.LogInformation("Executing function: {FunctionName} with arguments: {Arguments}", 
                functionName, functionArguments.ToString());

            switch (functionName)
            {
                case "get_all_expenses":
                    {
                        var expenses = await _expenseService.GetAllExpensesAsync();
                        return JsonSerializer.Serialize(expenses);
                    }

                case "get_expenses_by_status":
                    {
                        var args = DeserializeArguments(functionArguments);
                        var statusName = args?["statusName"].GetString() ?? "";
                        var expenses = await _expenseService.GetExpensesByStatusAsync(statusName);
                        return JsonSerializer.Serialize(expenses);
                    }

                case "get_expense_by_id":
                    {
                        var args = DeserializeArguments(functionArguments);
                        var expenseId = args?["expenseId"].GetInt32() ?? 0;
                        var expense = await _expenseService.GetExpenseByIdAsync(expenseId);
                        return JsonSerializer.Serialize(expense);
                    }

                case "create_expense":
                    {
                        var args = DeserializeArguments(functionArguments);
                        var dateString = args?["expenseDate"].GetString() ?? DateTime.Now.ToString("yyyy-MM-dd");
                        
                        if (!DateTime.TryParse(dateString, out var expenseDate))
                        {
                            expenseDate = DateTime.Now;
                            _logger.LogWarning("Invalid date format: {DateString}, using current date", dateString);
                        }
                        
                        var request = new CreateExpenseRequest
                        {
                            Amount = args?["amount"].GetDecimal() ?? 0,
                            ExpenseDate = expenseDate,
                            CategoryId = args?["categoryId"].GetInt32() ?? 0,
                            Description = args?["description"].GetString() ?? "",
                            UserId = args?["userId"].GetInt32() ?? 0
                        };
                        var expense = await _expenseService.CreateExpenseAsync(request);
                        return JsonSerializer.Serialize(new { success = true, expense });
                    }

                case "submit_expense":
                    {
                        var args = DeserializeArguments(functionArguments);
                        var expenseId = args?["expenseId"].GetInt32() ?? 0;
                        var expense = await _expenseService.SubmitExpenseAsync(expenseId);
                        return JsonSerializer.Serialize(new { success = true, expense });
                    }

                case "approve_expense":
                    {
                        var args = DeserializeArguments(functionArguments);
                        var request = new ApproveExpenseRequest
                        {
                            ExpenseId = args?["expenseId"].GetInt32() ?? 0,
                            ReviewedBy = args?["reviewerId"].GetInt32() ?? 0
                        };
                        var expense = await _expenseService.ApproveExpenseAsync(request);
                        return JsonSerializer.Serialize(new { success = true, expense });
                    }

                case "reject_expense":
                    {
                        var args = DeserializeArguments(functionArguments);
                        var request = new ApproveExpenseRequest
                        {
                            ExpenseId = args?["expenseId"].GetInt32() ?? 0,
                            ReviewedBy = args?["reviewerId"].GetInt32() ?? 0
                        };
                        var expense = await _expenseService.RejectExpenseAsync(request);
                        return JsonSerializer.Serialize(new { success = true, expense });
                    }

                case "search_expenses":
                    {
                        var args = DeserializeArguments(functionArguments);
                        var filterText = args?["filterText"].GetString() ?? "";
                        var expenses = await _expenseService.SearchExpensesAsync(filterText);
                        return JsonSerializer.Serialize(expenses);
                    }

                case "get_all_categories":
                    {
                        var categories = await _expenseService.GetAllCategoriesAsync();
                        return JsonSerializer.Serialize(categories);
                    }

                case "get_all_users":
                    {
                        var users = await _expenseService.GetAllUsersAsync();
                        return JsonSerializer.Serialize(users);
                    }

                case "get_all_statuses":
                    {
                        var statuses = await _expenseService.GetAllStatusesAsync();
                        return JsonSerializer.Serialize(statuses);
                    }

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

    private Dictionary<string, JsonElement>? DeserializeArguments(BinaryData functionArguments)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(functionArguments);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize function arguments: {Arguments}", functionArguments.ToString());
            return null;
        }
    }
}
