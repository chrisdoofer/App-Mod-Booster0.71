using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    public async Task<ActionResult<List<Expense>>> GetAll()
    {
        var (expenses, error) = await _expenseService.GetAllExpensesAsync();
        if (error != null)
        {
            _logger.LogWarning("Error getting expenses: {Message}", error.Message);
        }
        return Ok(expenses);
    }

    /// <summary>
    /// Get expense by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Expense>> GetById(int id)
    {
        var (expense, error) = await _expenseService.GetExpenseByIdAsync(id);
        if (error != null)
        {
            return StatusCode(500, error);
        }
        if (expense == null)
        {
            return NotFound();
        }
        return Ok(expense);
    }

    /// <summary>
    /// Get expenses by status
    /// </summary>
    [HttpGet("status/{status}")]
    public async Task<ActionResult<List<Expense>>> GetByStatus(string status)
    {
        var (expenses, error) = await _expenseService.GetExpensesByStatusAsync(status);
        if (error != null)
        {
            _logger.LogWarning("Error getting expenses by status: {Message}", error.Message);
        }
        return Ok(expenses);
    }

    /// <summary>
    /// Get expenses by user ID
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<Expense>>> GetByUser(int userId)
    {
        var (expenses, error) = await _expenseService.GetExpensesByUserAsync(userId);
        if (error != null)
        {
            _logger.LogWarning("Error getting expenses by user: {Message}", error.Message);
        }
        return Ok(expenses);
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<int>> Create([FromBody] CreateExpenseRequest request)
    {
        var (expenseId, error) = await _expenseService.CreateExpenseAsync(request);
        if (error != null)
        {
            return StatusCode(500, error);
        }
        return CreatedAtAction(nameof(GetById), new { id = expenseId }, new { expenseId });
    }

    /// <summary>
    /// Submit an expense for approval
    /// </summary>
    [HttpPost("{id}/submit")]
    public async Task<ActionResult> Submit(int id)
    {
        var (success, error) = await _expenseService.SubmitExpenseAsync(id);
        if (error != null)
        {
            return StatusCode(500, error);
        }
        if (!success)
        {
            return NotFound();
        }
        return Ok(new { message = "Expense submitted for approval" });
    }

    /// <summary>
    /// Approve an expense
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<ActionResult> Approve(int id, [FromQuery] int reviewerId = 2)
    {
        var (success, error) = await _expenseService.ApproveExpenseAsync(id, reviewerId);
        if (error != null)
        {
            return StatusCode(500, error);
        }
        if (!success)
        {
            return NotFound();
        }
        return Ok(new { message = "Expense approved" });
    }

    /// <summary>
    /// Reject an expense
    /// </summary>
    [HttpPost("{id}/reject")]
    public async Task<ActionResult> Reject(int id, [FromQuery] int reviewerId = 2)
    {
        var (success, error) = await _expenseService.RejectExpenseAsync(id, reviewerId);
        if (error != null)
        {
            return StatusCode(500, error);
        }
        if (!success)
        {
            return NotFound();
        }
        return Ok(new { message = "Expense rejected" });
    }

    /// <summary>
    /// Delete an expense (only draft expenses)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var (success, error) = await _expenseService.DeleteExpenseAsync(id);
        if (error != null)
        {
            return StatusCode(500, error);
        }
        if (!success)
        {
            return BadRequest(new { message = "Only draft expenses can be deleted" });
        }
        return NoContent();
    }
}

[ApiController]
[Route("api/[controller]")]
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
    public async Task<ActionResult<List<Category>>> GetAll()
    {
        var (categories, _) = await _expenseService.GetCategoriesAsync();
        return Ok(categories);
    }
}

[ApiController]
[Route("api/[controller]")]
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
    public async Task<ActionResult<List<User>>> GetAll()
    {
        var (users, _) = await _expenseService.GetUsersAsync();
        return Ok(users);
    }
}

[ApiController]
[Route("api/[controller]")]
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
    public async Task<ActionResult<DashboardStats>> GetStats()
    {
        var (stats, _) = await _expenseService.GetDashboardStatsAsync();
        return Ok(stats);
    }
}

[ApiController]
[Route("api/[controller]")]
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
    /// Send a message to the AI chat assistant
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required" });
        }

        var response = await _chatService.ProcessMessageAsync(request.Message);
        return Ok(new ChatResponse { Message = response });
    }

    /// <summary>
    /// Check if chat is configured
    /// </summary>
    [HttpGet("status")]
    public ActionResult<ChatStatus> GetStatus()
    {
        return Ok(new ChatStatus { IsConfigured = _chatService.IsConfigured });
    }
}

public class ChatRequest
{
    public string Message { get; set; } = "";
}

public class ChatResponse
{
    public string Message { get; set; } = "";
}

public class ChatStatus
{
    public bool IsConfigured { get; set; }
}
