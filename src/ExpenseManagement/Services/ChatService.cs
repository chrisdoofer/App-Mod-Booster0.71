using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using ExpenseManagement.Models;
using System.Text.Json;

namespace ExpenseManagement.Services;

public class ChatService
{
    private readonly IConfiguration _configuration;
    private readonly ExpenseService _expenseService;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IConfiguration configuration, ExpenseService expenseService, ILogger<ChatService> logger)
    {
        _configuration = configuration;
        _expenseService = expenseService;
        _logger = logger;
    }

    public bool IsConfigured
    {
        get
        {
            var endpoint = _configuration["GenAISettings:OpenAIEndpoint"];
            return !string.IsNullOrEmpty(endpoint);
        }
    }

    public async Task<string> SendMessageAsync(string userMessage)
    {
        if (!IsConfigured)
        {
            return "AI Chat is not available. To enable it, redeploy the infrastructure with the -DeployGenAI switch.";
        }

        try
        {
            var endpoint = _configuration["GenAISettings:OpenAIEndpoint"];
            var modelName = _configuration["GenAISettings:OpenAIModelName"];
            
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(modelName))
            {
                return "AI Chat configuration is incomplete.";
            }

            // Create credential - prefer ManagedIdentityCredential with explicit client ID
            TokenCredential credential;
            var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
            
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

            var client = new OpenAIClient(new Uri(endpoint), credential);

            // Simple chat completion without function calling
            var chatCompletionsOptions = new ChatCompletionsOptions()
            {
                DeploymentName = modelName,
                Messages =
                {
                    new ChatRequestSystemMessage(GetSystemPrompt()),
                    new ChatRequestUserMessage(userMessage)
                },
                Temperature = 0.7f,
                MaxTokens = 800
            };

            var response = await client.GetChatCompletionsAsync(chatCompletionsOptions);
            var completion = response.Value.Choices[0].Message.Content;

            return completion ?? "I couldn't generate a response.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat service");
            return $"Error: {ex.Message}. The AI Chat feature is available but encountered an error. Please check the logs for details.";
        }
    }

    private string GetSystemPrompt()
    {
        return @"You are a helpful AI assistant for an expense management system. You can help users understand their expenses and answer questions about the system.

Available users:
- Alice Example (User ID: 1) - Employee who reports to Bob
- Bob Manager (User ID: 2) - Manager who can approve/reject expenses

Available expense categories:
- Travel (Category ID: 1)
- Meals (Category ID: 2)
- Supplies (Category ID: 3)
- Accommodation (Category ID: 4)
- Other (Category ID: 5)

Expense statuses:
- Draft: Initial state, not yet submitted
- Submitted: Waiting for manager approval
- Approved: Approved by manager
- Rejected: Rejected by manager

Be helpful and conversational. You can explain the system, answer questions about expenses, and provide guidance on how to use the expense management features.

Note: In this version, you cannot directly access the database or perform actions. You can provide information and guidance based on the system structure described above.";
    }
}
