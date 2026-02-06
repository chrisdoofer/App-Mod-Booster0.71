using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ChatModel : PageModel
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatModel> _logger;

    public ChatModel(ChatService chatService, ILogger<ChatModel> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public bool IsConfigured { get; set; }

    public void OnGet()
    {
        IsConfigured = _chatService.IsConfigured;
        _logger.LogInformation("Chat page loaded. IsConfigured: {IsConfigured}", IsConfigured);
    }
}
