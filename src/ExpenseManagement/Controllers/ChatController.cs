using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Services;
using OpenAI.Chat;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
    {
        try
        {
            var history = new List<ChatMessage>();
            
            foreach (var msg in request.History ?? new List<HistoryMessage>())
            {
                if (msg.Role == "user")
                {
                    history.Add(ChatMessage.CreateUserMessage(msg.Content));
                }
                else if (msg.Role == "assistant")
                {
                    history.Add(ChatMessage.CreateAssistantMessage(msg.Content));
                }
            }

            var response = await _chatService.SendMessageAsync(request.Message, history);
            
            return Ok(new ChatResponse { Response = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, new ChatResponse { Response = $"Error: {ex.Message}" });
        }
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<HistoryMessage>? History { get; set; }
}

public class HistoryMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ChatResponse
{
    public string Response { get; set; } = string.Empty;
}
