using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ChatModel : PageModel
{
    private readonly ChatService _chatService;

    public bool IsConfigured { get; set; }

    public ChatModel(ChatService chatService)
    {
        _chatService = chatService;
    }

    public void OnGet()
    {
        IsConfigured = _chatService.IsConfigured;
    }
}
