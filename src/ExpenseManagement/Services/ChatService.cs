using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using ExpenseManagement.Models;
using OpenAI.Chat;
using System.ClientModel;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExpenseManagement.Services;

public class ChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly ExpenseService _expenseService;

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, ExpenseService expenseService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_configuration["GenAISettings:OpenAIEndpoint"]);

    public async Task<ChatResponse> SendMessageAsync(string message, List<Models.ChatMessage> history)
    {
        if (!IsConfigured)
        {
            return new ChatResponse
            {
                Success = false,
                Error = "AI Chat is not available yet. To enable it, redeploy using the -DeployGenAI switch.",
                Message = ""
            };
        }

        try
        {
            var endpoint = _configuration["GenAISettings:OpenAIEndpoint"] ?? throw new InvalidOperationException("OpenAI endpoint not configured");
            var modelName = _configuration["GenAISettings:OpenAIModelName"] ?? "gpt-4o";
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

            var client = new AzureOpenAIClient(new Uri(endpoint), credential);
            var chatClient = client.GetChatClient(modelName);

            // Build conversation history
            var messages = new List<Models.ChatMessage>
            {
                new Models.ChatMessage 
                { 
                    Role = "system", 
                    Content = @"You are a helpful AI assistant for an Expense Management System. You can help users:
- View and query expense data
- Create new expenses
- Approve or reject expenses

When users ask about expenses, use the get_expenses function to retrieve data.
When users want to add an expense, use the create_expense function.
When users want to approve or reject an expense, use the approve_expense function.

Always be helpful and provide clear, concise responses. Format lists nicely." 
                }
            };
            messages.AddRange(history);
            messages.Add(new Models.ChatMessage { Role = "user", Content = message });

            // Define function calling tools
            var tools = new List<ChatTool>
            {
                ChatTool.CreateFunctionTool(
                    functionName: "get_expenses",
                    functionDescription: "Retrieves expenses from the database. Can filter by status or employee name.",
                    functionParameters: BinaryData.FromString(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""status"": {
                                ""type"": ""string"",
                                ""description"": ""Filter by status: Submitted, Approved, Rejected, or Draft"",
                                ""enum"": [""Submitted"", ""Approved"", ""Rejected"", ""Draft""]
                            },
                            ""employeeName"": {
                                ""type"": ""string"",
                                ""description"": ""Filter by employee name""
                            }
                        }
                    }")
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "create_expense",
                    functionDescription: "Creates a new expense record in the database.",
                    functionParameters: BinaryData.FromString(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""employeeName"": {
                                ""type"": ""string"",
                                ""description"": ""Name of the employee submitting the expense""
                            },
                            ""description"": {
                                ""type"": ""string"",
                                ""description"": ""Description of the expense""
                            },
                            ""amount"": {
                                ""type"": ""number"",
                                ""description"": ""Amount in GBP (e.g., 50.00)""
                            },
                            ""category"": {
                                ""type"": ""string"",
                                ""description"": ""Category: Travel, Meals, Supplies, Accommodation, or Other"",
                                ""enum"": [""Travel"", ""Meals"", ""Supplies"", ""Accommodation"", ""Other""]
                            },
                            ""expenseDate"": {
                                ""type"": ""string"",
                                ""description"": ""Date of the expense in ISO format (YYYY-MM-DD)""
                            }
                        },
                        ""required"": [""employeeName"", ""description"", ""amount"", ""category"", ""expenseDate""]
                    }")
                ),
                ChatTool.CreateFunctionTool(
                    functionName: "approve_expense",
                    functionDescription: "Approves or rejects an expense.",
                    functionParameters: BinaryData.FromString(@"{
                        ""type"": ""object"",
                        ""properties"": {
                            ""expenseId"": {
                                ""type"": ""integer"",
                                ""description"": ""ID of the expense to approve or reject""
                            },
                            ""reviewerName"": {
                                ""type"": ""string"",
                                ""description"": ""Name of the reviewer""
                            },
                            ""approved"": {
                                ""type"": ""boolean"",
                                ""description"": ""true to approve, false to reject""
                            }
                        },
                        ""required"": [""expenseId"", ""reviewerName"", ""approved""]
                    }")
                )
            };

            var options = new ChatCompletionOptions();
            foreach (var tool in tools)
            {
                options.Tools.Add(tool);
            }

            // Convert to OpenAI chat messages
            var chatMessages = messages.Select(m => 
                m.Role.ToLower() == "user" ? (OpenAI.Chat.ChatMessage)new UserChatMessage(m.Content) :
                m.Role.ToLower() == "assistant" ? (OpenAI.Chat.ChatMessage)new AssistantChatMessage(m.Content) :
                (OpenAI.Chat.ChatMessage)new SystemChatMessage(m.Content)).ToList();

            ChatCompletion completion = await chatClient.CompleteChatAsync(chatMessages, options);

            // Handle function calls
            while (completion.FinishReason == ChatFinishReason.ToolCalls)
            {
                foreach (var toolCall in completion.ToolCalls)
                {
                    var functionResult = await ExecuteFunctionAsync(toolCall.FunctionName, toolCall.FunctionArguments.ToString());
                    
                    chatMessages.Add(new AssistantChatMessage(completion));
                    chatMessages.Add(new ToolChatMessage(toolCall.Id, functionResult));
                }

                completion = await chatClient.CompleteChatAsync(chatMessages, options);
            }

            return new ChatResponse
            {
                Success = true,
                Message = completion.Content[0].Text
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat service");
            return new ChatResponse
            {
                Success = false,
                Error = $"Error: {ex.Message}",
                Message = ""
            };
        }
    }

    private async Task<string> ExecuteFunctionAsync(string functionName, string arguments)
    {
        try
        {
            var jsonArgs = JsonNode.Parse(arguments);
            
            switch (functionName)
            {
                case "get_expenses":
                    var status = jsonArgs?["status"]?.ToString();
                    var employeeName = jsonArgs?["employeeName"]?.ToString();
                    var expenses = await _expenseService.GetExpensesAsync(status, employeeName);
                    return JsonSerializer.Serialize(expenses);

                case "create_expense":
                    var createRequest = new CreateExpenseRequest
                    {
                        EmployeeName = jsonArgs?["employeeName"]?.ToString() ?? "",
                        Description = jsonArgs?["description"]?.ToString() ?? "",
                        Amount = decimal.Parse(jsonArgs?["amount"]?.ToString() ?? "0"),
                        Category = jsonArgs?["category"]?.ToString() ?? "",
                        ExpenseDate = DateTime.Parse(jsonArgs?["expenseDate"]?.ToString() ?? DateTime.Now.ToString("yyyy-MM-dd"))
                    };
                    var expenseId = await _expenseService.CreateExpenseAsync(createRequest);
                    return JsonSerializer.Serialize(new { success = true, expenseId });

                case "approve_expense":
                    var approveRequest = new ApproveExpenseRequest
                    {
                        ExpenseId = int.Parse(jsonArgs?["expenseId"]?.ToString() ?? "0"),
                        ReviewerName = jsonArgs?["reviewerName"]?.ToString() ?? "",
                        Approved = bool.Parse(jsonArgs?["approved"]?.ToString() ?? "false")
                    };
                    var result = await _expenseService.ApproveExpenseAsync(approveRequest);
                    return JsonSerializer.Serialize(new { success = result });

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
