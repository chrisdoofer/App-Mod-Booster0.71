using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(IExpenseService expenseService, ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expenses
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<Expense>>>> GetAll()
    {
        var expenses = await _expenseService.GetAllExpensesAsync();
        return Ok(ApiResponse<List<Expense>>.Ok(expenses));
    }

    /// <summary>
    /// Get expense by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<Expense>>> GetById(int id)
    {
        var expense = await _expenseService.GetExpenseByIdAsync(id);
        if (expense == null)
        {
            return NotFound(ApiResponse<Expense>.Error($"Expense with ID {id} not found"));
        }
        return Ok(ApiResponse<Expense>.Ok(expense));
    }

    /// <summary>
    /// Get expenses by status
    /// </summary>
    [HttpGet("status/{status}")]
    public async Task<ActionResult<ApiResponse<List<Expense>>>> GetByStatus(string status)
    {
        var expenses = await _expenseService.GetExpensesByStatusAsync(status);
        return Ok(ApiResponse<List<Expense>>.Ok(expenses));
    }

    /// <summary>
    /// Get expenses by user
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<ApiResponse<List<Expense>>>> GetByUser(int userId)
    {
        var expenses = await _expenseService.GetExpensesByUserAsync(userId);
        return Ok(ApiResponse<List<Expense>>.Ok(expenses));
    }

    /// <summary>
    /// Get pending approvals
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<List<Expense>>>> GetPendingApprovals()
    {
        var expenses = await _expenseService.GetPendingApprovalsAsync();
        return Ok(ApiResponse<List<Expense>>.Ok(expenses));
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<int>>> Create([FromBody] CreateExpenseRequest request)
    {
        var expenseId = await _expenseService.CreateExpenseAsync(request);
        if (expenseId <= 0)
        {
            return BadRequest(ApiResponse<int>.Error(_expenseService.LastError ?? "Failed to create expense"));
        }
        return CreatedAtAction(nameof(GetById), new { id = expenseId }, ApiResponse<int>.Ok(expenseId, "Expense created successfully"));
    }

    /// <summary>
    /// Update an expense
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Update(int id, [FromBody] UpdateExpenseRequest request)
    {
        var success = await _expenseService.UpdateExpenseAsync(id, request);
        if (!success)
        {
            return BadRequest(ApiResponse<bool>.Error(_expenseService.LastError ?? "Failed to update expense"));
        }
        return Ok(ApiResponse<bool>.Ok(true, "Expense updated successfully"));
    }

    /// <summary>
    /// Submit expense for approval
    /// </summary>
    [HttpPost("{id}/submit")]
    public async Task<ActionResult<ApiResponse<bool>>> Submit(int id)
    {
        var success = await _expenseService.SubmitExpenseAsync(id);
        if (!success)
        {
            return BadRequest(ApiResponse<bool>.Error(_expenseService.LastError ?? "Failed to submit expense"));
        }
        return Ok(ApiResponse<bool>.Ok(true, "Expense submitted for approval"));
    }

    /// <summary>
    /// Approve an expense
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<ActionResult<ApiResponse<bool>>> Approve(int id, [FromQuery] int reviewerId = 2)
    {
        var success = await _expenseService.ApproveExpenseAsync(id, reviewerId);
        if (!success)
        {
            return BadRequest(ApiResponse<bool>.Error(_expenseService.LastError ?? "Failed to approve expense"));
        }
        return Ok(ApiResponse<bool>.Ok(true, "Expense approved"));
    }

    /// <summary>
    /// Reject an expense
    /// </summary>
    [HttpPost("{id}/reject")]
    public async Task<ActionResult<ApiResponse<bool>>> Reject(int id, [FromQuery] int reviewerId = 2)
    {
        var success = await _expenseService.RejectExpenseAsync(id, reviewerId);
        if (!success)
        {
            return BadRequest(ApiResponse<bool>.Error(_expenseService.LastError ?? "Failed to reject expense"));
        }
        return Ok(ApiResponse<bool>.Ok(true, "Expense rejected"));
    }

    /// <summary>
    /// Delete an expense
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(int id)
    {
        var success = await _expenseService.DeleteExpenseAsync(id);
        if (!success)
        {
            return BadRequest(ApiResponse<bool>.Error(_expenseService.LastError ?? "Failed to delete expense"));
        }
        return Ok(ApiResponse<bool>.Ok(true, "Expense deleted"));
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public CategoriesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all expense categories
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<Category>>>> GetAll()
    {
        var categories = await _expenseService.GetCategoriesAsync();
        return Ok(ApiResponse<List<Category>>.Ok(categories));
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public UsersController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<User>>>> GetAll()
    {
        var users = await _expenseService.GetUsersAsync();
        return Ok(ApiResponse<List<User>>.Ok(users));
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public DashboardController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    /// <summary>
    /// Get dashboard statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<ApiResponse<DashboardStats>>> GetStats()
    {
        var stats = await _expenseService.GetDashboardStatsAsync();
        return Ok(ApiResponse<DashboardStats>.Ok(stats));
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Check if chat is configured
    /// </summary>
    [HttpGet("status")]
    public ActionResult<ApiResponse<bool>> GetStatus()
    {
        return Ok(ApiResponse<bool>.Ok(_chatService.IsConfigured, 
            _chatService.IsConfigured ? "Chat is available" : "Chat is not configured"));
    }

    /// <summary>
    /// Send a message to the AI chat
    /// </summary>
    [HttpPost("message")]
    public async Task<ActionResult<ApiResponse<string>>> SendMessage([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(ApiResponse<string>.Error("Message is required"));
        }

        var response = await _chatService.SendMessageAsync(request.Message, request.History);
        return Ok(ApiResponse<string>.Ok(response));
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public List<Services.ChatMessageModel>? History { get; set; }
}
