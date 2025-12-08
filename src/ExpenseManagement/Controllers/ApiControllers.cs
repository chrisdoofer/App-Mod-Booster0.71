using Microsoft.AspNetCore.Mvc;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(ExpenseService expenseService, ILogger<ExpensesController> logger)
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
        try
        {
            var expenses = await _expenseService.GetAllExpensesAsync();
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all expenses");
            return StatusCode(500, new { error = "Failed to retrieve expenses" });
        }
    }

    /// <summary>
    /// Get expense by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Expense>> GetById(int id)
    {
        try
        {
            var expense = await _expenseService.GetExpenseByIdAsync(id);
            if (expense == null)
            {
                return NotFound(new { error = $"Expense {id} not found" });
            }
            return Ok(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to retrieve expense" });
        }
    }

    /// <summary>
    /// Get expenses by status
    /// </summary>
    [HttpGet("status/{statusName}")]
    public async Task<ActionResult<List<Expense>>> GetByStatus(string statusName)
    {
        try
        {
            var expenses = await _expenseService.GetExpensesByStatusAsync(statusName);
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses by status {StatusName}", statusName);
            return StatusCode(500, new { error = "Failed to retrieve expenses" });
        }
    }

    /// <summary>
    /// Get expenses by user ID
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<Expense>>> GetByUserId(int userId)
    {
        try
        {
            var expenses = await _expenseService.GetExpensesByUserIdAsync(userId);
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses for user {UserId}", userId);
            return StatusCode(500, new { error = "Failed to retrieve expenses" });
        }
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<int>> Create([FromBody] CreateExpenseRequest request)
    {
        try
        {
            var expenseId = await _expenseService.CreateExpenseAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = expenseId }, new { expenseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            return StatusCode(500, new { error = "Failed to create expense" });
        }
    }

    /// <summary>
    /// Update an existing expense
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateExpenseRequest request)
    {
        try
        {
            if (id != request.ExpenseId)
            {
                return BadRequest(new { error = "Expense ID mismatch" });
            }
            
            await _expenseService.UpdateExpenseAsync(request);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to update expense" });
        }
    }

    /// <summary>
    /// Submit an expense for approval
    /// </summary>
    [HttpPost("{id}/submit")]
    public async Task<ActionResult> Submit(int id)
    {
        try
        {
            await _expenseService.SubmitExpenseAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to submit expense" });
        }
    }

    /// <summary>
    /// Approve an expense
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<ActionResult> Approve(int id, [FromBody] ReviewExpenseRequest request)
    {
        try
        {
            if (id != request.ExpenseId)
            {
                return BadRequest(new { error = "Expense ID mismatch" });
            }
            
            await _expenseService.ApproveExpenseAsync(request);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to approve expense" });
        }
    }

    /// <summary>
    /// Reject an expense
    /// </summary>
    [HttpPost("{id}/reject")]
    public async Task<ActionResult> Reject(int id, [FromBody] ReviewExpenseRequest request)
    {
        try
        {
            if (id != request.ExpenseId)
            {
                return BadRequest(new { error = "Expense ID mismatch" });
            }
            
            await _expenseService.RejectExpenseAsync(request);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to reject expense" });
        }
    }

    /// <summary>
    /// Delete an expense
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            await _expenseService.DeleteExpenseAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {ExpenseId}", id);
            return StatusCode(500, new { error = "Failed to delete expense" });
        }
    }

    /// <summary>
    /// Get expense summary by status
    /// </summary>
    [HttpGet("summary")]
    public async Task<ActionResult<List<ExpenseSummary>>> GetSummary()
    {
        try
        {
            var summary = await _expenseService.GetExpenseSummaryAsync();
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense summary");
            return StatusCode(500, new { error = "Failed to retrieve summary" });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(ExpenseService expenseService, ILogger<CategoriesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all expense categories
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Category>>> GetAll()
    {
        try
        {
            var categories = await _expenseService.GetAllCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving categories");
            return StatusCode(500, new { error = "Failed to retrieve categories" });
        }
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(ExpenseService expenseService, ILogger<UsersController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<User>>> GetAll()
    {
        try
        {
            var users = await _expenseService.GetAllUsersAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, new { error = "Failed to retrieve users" });
        }
    }
}
