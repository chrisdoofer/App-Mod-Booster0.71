using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using ExpenseManagement.Models;
using OpenAI.Chat;
using System.Text.Json;

namespace ExpenseManagement.Services;

/// <summary>
/// Interface for chat operations with Azure OpenAI
/// </summary>
public interface IChatService
{
    bool IsConfigured { get; }
    Task<ChatResponse> SendMessageAsync(string message, List<Models.ChatMessage>? history = null);
}

/// <summary>
/// Service for handling chat interactions with Azure OpenAI including function calling
/// </summary>
public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly IExpenseService _expenseService;
    private readonly string? _openAIEndpoint;
    private readonly string? _modelName;

    public ChatService(
        IConfiguration configuration,
        ILogger<ChatService> logger,
        IExpenseService expenseService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
        _openAIEndpoint = configuration["GenAISettings:OpenAIEndpoint"];
        _modelName = configuration["GenAISettings:OpenAIModelName"] ?? "gpt-4o";
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_openAIEndpoint);

    public async Task<ChatResponse> SendMessageAsync(string message, List<Models.ChatMessage>? history = null)
    {
        if (!IsConfigured)
        {
            return new ChatResponse
            {
                Success = false,
                Error = "AI Chat is not configured. Deploy with the -DeployGenAI switch to enable this feature.",
                Response = string.Empty
            };
        }

        try
        {
            // Get credential based on environment
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

            var client = new AzureOpenAIClient(new Uri(_openAIEndpoint!), credential);
            var chatClient = client.GetChatClient(_modelName);

            // Build conversation messages
            var messages = new List<OpenAI.Chat.ChatMessage>
            {
                new SystemChatMessage(GetSystemPrompt())
            };

            // Add history if provided
            if (history != null)
            {
                foreach (var msg in history)
                {
                    if (msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                    {
                        messages.Add(new UserChatMessage(msg.Content));
                    }
                    else if (msg.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                    {
                        messages.Add(new AssistantChatMessage(msg.Content));
                    }
                }
            }

            // Add current message
            messages.Add(new UserChatMessage(message));

            // Define available functions
            var options = new ChatCompletionOptions
            {
                Tools =
                {
                    ChatTool.CreateFunctionTool(
                        "get_expenses",
                        "Retrieves all expenses from the database",
                        BinaryData.FromObjectAsJson(new { type = "object", properties = new { } })),
                    ChatTool.CreateFunctionTool(
                        "get_expenses_by_status",
                        "Retrieves expenses filtered by status (Draft, Submitted, Approved, Rejected)",
                        BinaryData.FromObjectAsJson(new
                        {
                            type = "object",
                            properties = new
                            {
                                status = new { type = "string", description = "The status to filter by: Draft, Submitted, Approved, or Rejected" }
                            },
                            required = new[] { "status" }
                        })),
                    ChatTool.CreateFunctionTool(
                        "get_expense_summary",
                        "Gets a summary of expenses grouped by status with totals",
                        BinaryData.FromObjectAsJson(new { type = "object", properties = new { } })),
                    ChatTool.CreateFunctionTool(
                        "create_expense",
                        "Creates a new expense record",
                        BinaryData.FromObjectAsJson(new
                        {
                            type = "object",
                            properties = new
                            {
                                userId = new { type = "integer", description = "The ID of the user submitting the expense" },
                                categoryId = new { type = "integer", description = "The category ID (1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other)" },
                                amount = new { type = "number", description = "The expense amount in pounds (e.g., 50.00)" },
                                expenseDate = new { type = "string", description = "The date of the expense in YYYY-MM-DD format" },
                                description = new { type = "string", description = "Description of the expense" }
                            },
                            required = new[] { "userId", "categoryId", "amount", "expenseDate" }
                        })),
                    ChatTool.CreateFunctionTool(
                        "approve_expense",
                        "Approves a submitted expense",
                        BinaryData.FromObjectAsJson(new
                        {
                            type = "object",
                            properties = new
                            {
                                expenseId = new { type = "integer", description = "The ID of the expense to approve" },
                                reviewerId = new { type = "integer", description = "The ID of the manager approving the expense" }
                            },
                            required = new[] { "expenseId", "reviewerId" }
                        })),
                    ChatTool.CreateFunctionTool(
                        "reject_expense",
                        "Rejects a submitted expense",
                        BinaryData.FromObjectAsJson(new
                        {
                            type = "object",
                            properties = new
                            {
                                expenseId = new { type = "integer", description = "The ID of the expense to reject" },
                                reviewerId = new { type = "integer", description = "The ID of the manager rejecting the expense" }
                            },
                            required = new[] { "expenseId", "reviewerId" }
                        })),
                    ChatTool.CreateFunctionTool(
                        "get_categories",
                        "Gets all available expense categories",
                        BinaryData.FromObjectAsJson(new { type = "object", properties = new { } })),
                    ChatTool.CreateFunctionTool(
                        "get_users",
                        "Gets all users in the system",
                        BinaryData.FromObjectAsJson(new { type = "object", properties = new { } }))
                }
            };

            // Send initial request
            var response = await chatClient.CompleteChatAsync(messages, options);
            var completion = response.Value;

            // Handle function calls in a loop
            while (completion.FinishReason == ChatFinishReason.ToolCalls)
            {
                var assistantMessage = new AssistantChatMessage(completion);
                messages.Add(assistantMessage);

                foreach (var toolCall in completion.ToolCalls)
                {
                    var functionResult = await ExecuteFunctionAsync(toolCall.FunctionName, toolCall.FunctionArguments);
                    messages.Add(new ToolChatMessage(toolCall.Id, functionResult));
                }

                response = await chatClient.CompleteChatAsync(messages, options);
                completion = response.Value;
            }

            var assistantResponse = completion.Content[0].Text;

            return new ChatResponse
            {
                Success = true,
                Response = assistantResponse
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat service: {Message}", ex.Message);
            return new ChatResponse
            {
                Success = false,
                Error = $"An error occurred: {ex.Message}",
                Response = string.Empty
            };
        }
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, BinaryData arguments)
    {
        try
        {
            var args = JsonDocument.Parse(arguments.ToString());
            
            switch (functionName)
            {
                case "get_expenses":
                    var (expenses, _) = await _expenseService.GetAllExpensesAsync();
                    return JsonSerializer.Serialize(expenses.Select(e => new
                    {
                        e.ExpenseId,
                        e.UserName,
                        e.CategoryName,
                        e.Amount,
                        e.Currency,
                        e.StatusName,
                        ExpenseDate = e.ExpenseDate.ToString("yyyy-MM-dd"),
                        e.Description
                    }));

                case "get_expenses_by_status":
                    var status = args.RootElement.GetProperty("status").GetString() ?? "Submitted";
                    var (statusExpenses, _) = await _expenseService.GetExpensesByStatusAsync(status);
                    return JsonSerializer.Serialize(statusExpenses.Select(e => new
                    {
                        e.ExpenseId,
                        e.UserName,
                        e.CategoryName,
                        e.Amount,
                        e.Currency,
                        e.StatusName,
                        ExpenseDate = e.ExpenseDate.ToString("yyyy-MM-dd"),
                        e.Description
                    }));

                case "get_expense_summary":
                    var (summary, _) = await _expenseService.GetExpenseSummaryAsync();
                    return JsonSerializer.Serialize(summary);

                case "create_expense":
                    var dateStr = args.RootElement.GetProperty("expenseDate").GetString();
                    if (!DateTime.TryParse(dateStr, out var expenseDate))
                    {
                        return JsonSerializer.Serialize(new { success = false, error = $"Invalid date format: {dateStr}" });
                    }
                    var createRequest = new CreateExpenseRequest
                    {
                        UserId = args.RootElement.GetProperty("userId").GetInt32(),
                        CategoryId = args.RootElement.GetProperty("categoryId").GetInt32(),
                        Amount = args.RootElement.GetProperty("amount").GetDecimal(),
                        ExpenseDate = expenseDate,
                        Description = args.RootElement.TryGetProperty("description", out var desc) ? desc.GetString() : null
                    };
                    var (expenseId, createError) = await _expenseService.CreateExpenseAsync(createRequest);
                    if (createError != null)
                    {
                        return JsonSerializer.Serialize(new { success = false, error = createError.Message });
                    }
                    return JsonSerializer.Serialize(new { success = true, expenseId });

                case "approve_expense":
                    var approveExpenseId = args.RootElement.GetProperty("expenseId").GetInt32();
                    var approveReviewerId = args.RootElement.GetProperty("reviewerId").GetInt32();
                    var (approveSuccess, approveError) = await _expenseService.ApproveExpenseAsync(approveExpenseId, approveReviewerId);
                    if (approveError != null)
                    {
                        return JsonSerializer.Serialize(new { success = false, error = approveError.Message });
                    }
                    return JsonSerializer.Serialize(new { success = approveSuccess });

                case "reject_expense":
                    var rejectExpenseId = args.RootElement.GetProperty("expenseId").GetInt32();
                    var rejectReviewerId = args.RootElement.GetProperty("reviewerId").GetInt32();
                    var (rejectSuccess, rejectError) = await _expenseService.RejectExpenseAsync(rejectExpenseId, rejectReviewerId);
                    if (rejectError != null)
                    {
                        return JsonSerializer.Serialize(new { success = false, error = rejectError.Message });
                    }
                    return JsonSerializer.Serialize(new { success = rejectSuccess });

                case "get_categories":
                    var (categories, _) = await _expenseService.GetCategoriesAsync();
                    return JsonSerializer.Serialize(categories);

                case "get_users":
                    var (users, _) = await _expenseService.GetUsersAsync();
                    return JsonSerializer.Serialize(users.Select(u => new
                    {
                        u.UserId,
                        u.UserName,
                        u.Email,
                        u.RoleName
                    }));

                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown function: {functionName}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {Function}: {Message}", functionName, ex.Message);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private static string GetSystemPrompt()
    {
        return @"You are a helpful assistant for the Expense Management System. You can help users:

1. **View expenses** - List all expenses, filter by status (Draft, Submitted, Approved, Rejected), or get summaries
2. **Create expenses** - Add new expense records for users
3. **Approve/Reject expenses** - Process submitted expenses (requires manager role)
4. **Get information** - List categories, users, and expense statistics

Available expense categories:
- 1: Travel
- 2: Meals
- 3: Supplies
- 4: Accommodation
- 5: Other

Available users:
- User ID 1: Alice Example (Employee)
- User ID 2: Bob Manager (Manager - can approve/reject expenses)

When creating expenses:
- Amount should be in pounds (e.g., 50.00 for £50)
- Date should be in YYYY-MM-DD format
- Always confirm important actions with the user before executing

Format your responses clearly. When listing expenses, format them nicely with relevant details.
Currency is GBP (British Pounds).";
    }
}
