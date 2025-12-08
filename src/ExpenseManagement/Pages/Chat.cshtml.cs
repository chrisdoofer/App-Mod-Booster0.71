using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ChatModel : PageModel
{
    private readonly IChatService _chatService;

    public ChatModel(IChatService chatService)
    {
        _chatService = chatService;
    }

    public bool IsChatConfigured => _chatService.IsConfigured;

    public void OnGet()
    {
    }
}
