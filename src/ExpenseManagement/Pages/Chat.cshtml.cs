using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ChatModel : PageModel
{
    private readonly ILogger<ChatModel> _logger;
    private readonly IChatService _chatService;

    public bool IsConfigured { get; set; }

    public ChatModel(ILogger<ChatModel> logger, IChatService chatService)
    {
        _logger = logger;
        _chatService = chatService;
    }

    public void OnGet()
    {
        IsConfigured = _chatService.IsConfigured;
    }
}
