using Azure.AI.OpenAI;
using Azure.Identity;
using ExpenseManagement.Models;
using OpenAI.Chat;
using System.Text.Json;

namespace ExpenseManagement.Services;

/// <summary>
/// Azure OpenAI chat service with function calling.
/// Uses ManagedIdentityCredential (or DefaultAzureCredential as fallback).
/// When not configured, IsConfigured returns false and SendMessageAsync returns a friendly message.
/// </summary>
public class ChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly ExpenseService _expenseService;

    public ChatService(
        IConfiguration configuration,
        ILogger<ChatService> logger,
        ExpenseService expenseService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
    }

    /// <summary>
    /// Returns true only when the OpenAI endpoint is configured.
    /// Used to conditionally render the chat UI.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrEmpty(_configuration["GenAISettings:OpenAIEndpoint"]);

    /// <summary>
    /// Sends a user message to Azure OpenAI and returns the AI's response.
    /// If not configured, returns a friendly fallback message.
    /// Supports multi-turn function calling loop.
    /// </summary>
    public async Task<string> SendMessageAsync(string userMessage, List<ChatMessageDto>? history = null)
    {
        if (!IsConfigured)
        {
            return "AI Chat is not available yet. To enable it, redeploy using the -DeployGenAI switch.";
        }

        try
        {
            var endpoint = _configuration["GenAISettings:OpenAIEndpoint"]!;
            var modelName = _configuration["GenAISettings:OpenAIModelName"] ?? "gpt-4o";
            var managedIdentityClientId = _configuration["ManagedIdentityClientId"];

            // Build credential — prefer explicit Managed Identity client ID
            Azure.Core.TokenCredential credential;
            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                _logger.LogInformation("ChatService: Using ManagedIdentityCredential with client ID.");
                credential = new ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                _logger.LogInformation("ChatService: Using DefaultAzureCredential.");
                credential = new DefaultAzureCredential();
            }

            var azureClient = new AzureOpenAIClient(new Uri(endpoint), credential);
            var chatClient = azureClient.GetChatClient(modelName);

            // Build message list
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(GetSystemPrompt())
            };

            // Add conversation history
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

            messages.Add(new UserChatMessage(userMessage));

            // Define function calling tools
            var options = new ChatCompletionOptions();
            foreach (var tool in GetTools())
            {
                options.Tools.Add(tool);
            }

            // Function calling loop — AI may call multiple tools before responding
            const int maxIterations = 10;
            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                var response = await chatClient.CompleteChatAsync(messages, options);
                var completion = response.Value;

                if (completion.FinishReason == ChatFinishReason.ToolCalls)
                {
                    // Add the assistant's tool call message to history
                    messages.Add(new AssistantChatMessage(completion));

                    // Execute each tool call and add results
                    foreach (var toolCall in completion.ToolCalls)
                    {
                        _logger.LogInformation("ChatService: Executing tool '{Tool}'.", toolCall.FunctionName);
                        var result = await ExecuteToolCallAsync(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
                        messages.Add(new ToolChatMessage(toolCall.Id, result));
                    }
                    // Continue loop to get AI's next response
                    continue;
                }

                // Finished with a regular message
                var content = completion.Content.FirstOrDefault()?.Text ?? string.Empty;
                return content;
            }

            return "I was unable to complete the request after multiple steps. Please try again.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChatService: Error calling Azure OpenAI.");
            return $"An error occurred while processing your request: {ex.Message}. Please check the application logs for details.";
        }
    }

    // =====================================================================
    // System prompt
    // =====================================================================

    private static string GetSystemPrompt() =>
        """
        You are a helpful expense management assistant. You help users manage their expenses by:
        - Querying existing expenses and providing summaries
        - Creating new expense records when asked
        - Approving or rejecting submitted expenses
        - Answering questions about expense categories and statuses

        When displaying lists of expenses, format them clearly with amounts, dates and descriptions.
        Always confirm destructive actions (approve, reject, create) before proceeding.
        Use GBP (£) as the default currency unless the user specifies otherwise.
        When creating expenses, amounts should be provided in pounds (e.g. 45.00), and you will convert to minor units (pence) automatically by multiplying by 100.

        Available functions:
        - get_expenses: Retrieve a list of expenses (optionally filtered by user or status)
        - get_expense_summary: Get summary statistics grouped by status
        - get_categories: List available expense categories
        - create_expense: Create a new draft expense
        - approve_expense: Approve a submitted expense (requires expense ID and reviewer ID)
        - reject_expense: Reject a submitted expense (requires expense ID and reviewer ID)
        """;

    // =====================================================================
    // Tool definitions
    // =====================================================================

    private static List<ChatTool> GetTools() =>
    [
        ChatTool.CreateFunctionTool(
            functionName: "get_expenses",
            functionDescription: "Retrieves expenses from the database. Optionally filter by user ID or status ID.",
            functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "userId": {
                            "type": "integer",
                            "description": "Optional user ID to filter expenses by a specific user"
                        },
                        "statusId": {
                            "type": "integer",
                            "description": "Optional status ID to filter expenses by status (1=Draft, 2=Submitted, 3=Approved, 4=Rejected)"
                        }
                    },
                    "required": []
                }
                """)
        ),
        ChatTool.CreateFunctionTool(
            functionName: "get_expense_summary",
            functionDescription: "Returns summary statistics of expenses grouped by status (count and total amount per status).",
            functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "userId": {
                            "type": "integer",
                            "description": "Optional user ID to get summary for a specific user only"
                        }
                    },
                    "required": []
                }
                """)
        ),
        ChatTool.CreateFunctionTool(
            functionName: "get_categories",
            functionDescription: "Retrieves all available expense categories.",
            functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {},
                    "required": []
                }
                """)
        ),
        ChatTool.CreateFunctionTool(
            functionName: "create_expense",
            functionDescription: "Creates a new draft expense record in the database.",
            functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "userId": {
                            "type": "integer",
                            "description": "The ID of the user creating the expense"
                        },
                        "categoryId": {
                            "type": "integer",
                            "description": "The ID of the expense category"
                        },
                        "amountPounds": {
                            "type": "number",
                            "description": "The expense amount in pounds (e.g. 45.00). Will be converted to minor units automatically."
                        },
                        "currency": {
                            "type": "string",
                            "description": "Currency code, defaults to GBP",
                            "default": "GBP"
                        },
                        "expenseDate": {
                            "type": "string",
                            "format": "date",
                            "description": "The date of the expense in YYYY-MM-DD format"
                        },
                        "description": {
                            "type": "string",
                            "description": "A description of the expense"
                        }
                    },
                    "required": ["userId", "categoryId", "amountPounds", "expenseDate"]
                }
                """)
        ),
        ChatTool.CreateFunctionTool(
            functionName: "approve_expense",
            functionDescription: "Approves a submitted expense. The expense must be in 'Submitted' status.",
            functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "expenseId": {
                            "type": "integer",
                            "description": "The ID of the expense to approve"
                        },
                        "reviewerId": {
                            "type": "integer",
                            "description": "The ID of the user approving the expense"
                        }
                    },
                    "required": ["expenseId", "reviewerId"]
                }
                """)
        ),
        ChatTool.CreateFunctionTool(
            functionName: "reject_expense",
            functionDescription: "Rejects a submitted expense. The expense must be in 'Submitted' status.",
            functionParameters: BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "expenseId": {
                            "type": "integer",
                            "description": "The ID of the expense to reject"
                        },
                        "reviewerId": {
                            "type": "integer",
                            "description": "The ID of the user rejecting the expense"
                        }
                    },
                    "required": ["expenseId", "reviewerId"]
                }
                """)
        )
    ];

    // =====================================================================
    // Tool execution
    // =====================================================================

    private async Task<string> ExecuteToolCallAsync(string functionName, string arguments)
    {
        try
        {
            using var doc = JsonDocument.Parse(arguments);
            var root = doc.RootElement;

            switch (functionName)
            {
                case "get_expenses":
                {
                    int? userId = root.TryGetProperty("userId", out var uid) && uid.ValueKind == JsonValueKind.Number
                        ? uid.GetInt32() : null;
                    int? statusId = root.TryGetProperty("statusId", out var sid) && sid.ValueKind == JsonValueKind.Number
                        ? sid.GetInt32() : null;

                    var expenses = await _expenseService.GetExpensesAsync(userId, statusId);
                    return JsonSerializer.Serialize(expenses.Select(e => new
                    {
                        e.ExpenseId,
                        e.UserName,
                        e.CategoryName,
                        e.StatusName,
                        Amount = $"£{e.Amount:F2}",
                        e.Currency,
                        ExpenseDate = e.ExpenseDate.ToString("yyyy-MM-dd"),
                        e.Description,
                        SubmittedAt = e.SubmittedAt?.ToString("yyyy-MM-dd"),
                        e.ReviewerName
                    }));
                }

                case "get_expense_summary":
                {
                    int? userId = root.TryGetProperty("userId", out var uid) && uid.ValueKind == JsonValueKind.Number
                        ? uid.GetInt32() : null;

                    var summaries = await _expenseService.GetExpenseSummaryAsync(userId);
                    return JsonSerializer.Serialize(summaries.Select(s => new
                    {
                        s.StatusName,
                        s.Count,
                        TotalAmount = $"£{s.TotalAmount:F2}"
                    }));
                }

                case "get_categories":
                {
                    var categories = await _expenseService.GetCategoriesAsync();
                    return JsonSerializer.Serialize(categories.Select(c => new
                    {
                        c.CategoryId,
                        c.CategoryName
                    }));
                }

                case "create_expense":
                {
                    if (!root.TryGetProperty("userId", out var uidProp) ||
                        !root.TryGetProperty("categoryId", out var catProp) ||
                        !root.TryGetProperty("amountPounds", out var amtProp) ||
                        !root.TryGetProperty("expenseDate", out var dateProp))
                    {
                        return """{"error": "Missing required fields: userId, categoryId, amountPounds, expenseDate"}""";
                    }

                    var amountPounds = amtProp.GetDouble();
                    var amountMinor = (int)(amountPounds * 100);
                    var currency = root.TryGetProperty("currency", out var currProp)
                        ? currProp.GetString() ?? "GBP" : "GBP";
                    var description = root.TryGetProperty("description", out var descProp)
                        ? descProp.GetString() : null;

                    if (!DateTime.TryParse(dateProp.GetString(), out var expenseDate))
                    {
                        return """{"error": "Invalid expenseDate format. Use YYYY-MM-DD."}""";
                    }

                    var request = new CreateExpenseRequest
                    {
                        UserId = uidProp.GetInt32(),
                        CategoryId = catProp.GetInt32(),
                        AmountMinor = amountMinor,
                        Currency = currency,
                        ExpenseDate = expenseDate,
                        Description = description
                    };

                    var expenseId = await _expenseService.CreateExpenseAsync(request);
                    return JsonSerializer.Serialize(new { success = true, expenseId, amountMinor, message = $"Expense created with ID {expenseId}" });
                }

                case "approve_expense":
                {
                    if (!root.TryGetProperty("expenseId", out var expProp) ||
                        !root.TryGetProperty("reviewerId", out var revProp))
                    {
                        return """{"error": "Missing required fields: expenseId, reviewerId"}""";
                    }

                    var success = await _expenseService.ApproveExpenseAsync(expProp.GetInt32(), revProp.GetInt32());
                    return JsonSerializer.Serialize(new { success, message = success ? "Expense approved successfully." : "Expense could not be approved (may not be in Submitted status)." });
                }

                case "reject_expense":
                {
                    if (!root.TryGetProperty("expenseId", out var expProp) ||
                        !root.TryGetProperty("reviewerId", out var revProp))
                    {
                        return """{"error": "Missing required fields: expenseId, reviewerId"}""";
                    }

                    var success = await _expenseService.RejectExpenseAsync(expProp.GetInt32(), revProp.GetInt32());
                    return JsonSerializer.Serialize(new { success, message = success ? "Expense rejected successfully." : "Expense could not be rejected (may not be in Submitted status)." });
                }

                default:
                    return "{\"error\": \"Unknown function: " + functionName + "\"}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChatService: Error executing tool '{Tool}'.", functionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}

/// <summary>
/// DTO for passing chat message history between page and service.
/// </summary>
public class ChatMessageDto
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}
