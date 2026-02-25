using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Controllers;

/// <summary>
/// REST API for expense management operations.
/// All data access is via the ExpenseService (stored procedures only — no direct SQL).
/// </summary>
[ApiController]
[Route("api")]
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

    // =====================================================================
    // Expenses endpoints
    // =====================================================================

    /// <summary>Retrieves a list of expenses with optional filters.</summary>
    [HttpGet("expenses")]
    [ProducesResponseType(typeof(List<Expense>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenses([FromQuery] int? userId, [FromQuery] int? statusId)
    {
        var expenses = await _expenseService.GetExpensesAsync(userId, statusId);
        return Ok(expenses);
    }

    /// <summary>Retrieves a single expense by its ID.</summary>
    [HttpGet("expenses/{id:int}")]
    [ProducesResponseType(typeof(Expense), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExpenseById(int id)
    {
        var expense = await _expenseService.GetExpenseByIdAsync(id);
        if (expense == null)
            return NotFound(new { message = $"Expense {id} not found." });
        return Ok(expense);
    }

    /// <summary>Creates a new draft expense.</summary>
    [HttpPost("expenses")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var expenseId = await _expenseService.CreateExpenseAsync(request);
            return CreatedAtAction(nameof(GetExpenseById), new { id = expenseId }, new { expenseId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to create expense.", detail = ex.Message });
        }
    }

    /// <summary>Updates an existing draft expense.</summary>
    [HttpPut("expenses/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateExpense(int id, [FromBody] UpdateExpenseRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var success = await _expenseService.UpdateExpenseAsync(id, request);
            if (!success)
                return NotFound(new { message = $"Expense {id} not found or is not in Draft status." });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense {Id}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to update expense.", detail = ex.Message });
        }
    }

    /// <summary>Deletes a draft expense.</summary>
    [HttpDelete("expenses/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        try
        {
            var success = await _expenseService.DeleteExpenseAsync(id);
            if (!success)
                return NotFound(new { message = $"Expense {id} not found or is not in Draft status." });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {Id}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to delete expense.", detail = ex.Message });
        }
    }

    /// <summary>Submits a draft expense for approval.</summary>
    [HttpPost("expenses/{id:int}/submit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitExpense(int id)
    {
        try
        {
            var success = await _expenseService.SubmitExpenseAsync(id);
            if (!success)
                return NotFound(new { message = $"Expense {id} not found or cannot be submitted from its current status." });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense {Id}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to submit expense.", detail = ex.Message });
        }
    }

    /// <summary>Approves a submitted expense.</summary>
    [HttpPost("expenses/{id:int}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveExpense(int id, [FromQuery] int reviewerId)
    {
        try
        {
            var success = await _expenseService.ApproveExpenseAsync(id, reviewerId);
            if (!success)
                return NotFound(new { message = $"Expense {id} not found or is not in Submitted status." });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense {Id}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to approve expense.", detail = ex.Message });
        }
    }

    /// <summary>Rejects a submitted expense.</summary>
    [HttpPost("expenses/{id:int}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectExpense(int id, [FromQuery] int reviewerId)
    {
        try
        {
            var success = await _expenseService.RejectExpenseAsync(id, reviewerId);
            if (!success)
                return NotFound(new { message = $"Expense {id} not found or is not in Submitted status." });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense {Id}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "Failed to reject expense.", detail = ex.Message });
        }
    }

    /// <summary>Returns expense summary statistics grouped by status.</summary>
    [HttpGet("expenses/summary")]
    [ProducesResponseType(typeof(List<ExpenseSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenseSummary([FromQuery] int? userId)
    {
        var summary = await _expenseService.GetExpenseSummaryAsync(userId);
        return Ok(summary);
    }

    // =====================================================================
    // Supporting data endpoints
    // =====================================================================

    /// <summary>Returns all active expense categories.</summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(List<ExpenseCategory>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _expenseService.GetCategoriesAsync();
        return Ok(categories);
    }

    /// <summary>Returns all expense statuses.</summary>
    [HttpGet("statuses")]
    [ProducesResponseType(typeof(List<ExpenseStatus>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatuses()
    {
        var statuses = await _expenseService.GetStatusesAsync();
        return Ok(statuses);
    }

    /// <summary>Returns all active users.</summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(List<Models.User>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _expenseService.GetUsersAsync();
        return Ok(users);
    }
}
