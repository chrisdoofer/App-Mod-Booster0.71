using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace ExpenseManagement.Pages;

public class ChatModel : PageModel
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatModel> _logger;

    public bool IsConfigured { get; private set; }
    public List<ChatMessageDto> MessageHistory { get; set; } = [];
    public string HistoryJson { get; set; } = "[]";

    public ChatModel(ChatService chatService, ILogger<ChatModel> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public void OnGet()
    {
        // Check configuration on page load — not just when sending a message
        IsConfigured = _chatService.IsConfigured;
    }

    public async Task<IActionResult> OnPostAsync(string userMessage, string? historyJson)
    {
        IsConfigured = _chatService.IsConfigured;

        if (!IsConfigured)
        {
            return Page();
        }

        // Deserialise history
        List<ChatMessageDto> history = [];
        if (!string.IsNullOrEmpty(historyJson))
        {
            try
            {
                history = JsonSerializer.Deserialize<List<ChatMessageDto>>(historyJson)
                    ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialise chat history.");
            }
        }

        // Add user message to history
        history.Add(new ChatMessageDto { Role = "user", Content = userMessage });

        // Get AI response
        string response;
        try
        {
            response = await _chatService.SendMessageAsync(userMessage, history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chat response.");
            response = $"Sorry, an error occurred: {ex.Message}";
        }

        // Add assistant response to history
        history.Add(new ChatMessageDto { Role = "assistant", Content = response });

        // Keep history manageable (last 20 messages = 10 turns)
        if (history.Count > 20)
        {
            history = history.Skip(history.Count - 20).ToList();
        }

        MessageHistory = history;
        HistoryJson = JsonSerializer.Serialize(history);

        return Page();
    }
}
