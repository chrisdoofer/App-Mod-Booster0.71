using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api")]
public class ExpenseApiController : ControllerBase
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<ExpenseApiController> _logger;

    public ExpenseApiController(ExpenseService expenseService, ILogger<ExpenseApiController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expenses, optionally filtered by status or employee name
    /// </summary>
    [HttpGet("expenses")]
    public async Task<ActionResult<List<Expense>>> GetExpenses(
        [FromQuery] string? status = null,
        [FromQuery] string? employeeName = null)
    {
        try
        {
            var expenses = await _expenseService.GetExpensesAsync(status, employeeName);
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses");
            return StatusCode(500, new { error = "Failed to retrieve expenses", detail = ex.Message });
        }
    }

    /// <summary>
    /// Get expense summary grouped by status
    /// </summary>
    [HttpGet("expenses/summary")]
    public async Task<ActionResult<List<ExpenseSummary>>> GetExpenseSummary()
    {
        try
        {
            var summary = await _expenseService.GetExpenseSummaryAsync();
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense summary");
            return StatusCode(500, new { error = "Failed to retrieve expense summary", detail = ex.Message });
        }
    }

    /// <summary>
    /// Get all expense categories
    /// </summary>
    [HttpGet("categories")]
    public async Task<ActionResult<List<ExpenseCategory>>> GetCategories()
    {
        try
        {
            var categories = await _expenseService.GetCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving categories");
            return StatusCode(500, new { error = "Failed to retrieve categories", detail = ex.Message });
        }
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    [HttpPost("expenses")]
    public async Task<ActionResult<int>> CreateExpense([FromBody] CreateExpenseRequest request)
    {
        try
        {
            var expenseId = await _expenseService.CreateExpenseAsync(request);
            return Ok(new { expenseId, message = "Expense created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return StatusCode(500, new { error = "Failed to create expense", detail = ex.Message });
        }
    }

    /// <summary>
    /// Approve or reject an expense
    /// </summary>
    [HttpPost("expenses/approve")]
    public async Task<ActionResult> ApproveExpense([FromBody] ApproveExpenseRequest request)
    {
        try
        {
            await _expenseService.ApproveExpenseAsync(request);
            var action = request.Approved ? "approved" : "rejected";
            return Ok(new { message = $"Expense {action} successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense");
            return StatusCode(500, new { error = "Failed to approve expense", detail = ex.Message });
        }
    }
}

[ApiController]
[Route("api")]
public class ChatApiController : ControllerBase
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatApiController> _logger;

    public ChatApiController(ChatService chatService, ILogger<ChatApiController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Send a message to the AI chat assistant
    /// </summary>
    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
    {
        try
        {
            var response = await _chatService.SendMessageAsync(request.Message, request.History);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in chat API");
            return StatusCode(500, new ChatResponse
            {
                Success = false,
                Error = $"Chat error: {ex.Message}",
                Message = ""
            });
        }
    }
}
