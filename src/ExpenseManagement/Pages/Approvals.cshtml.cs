using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ApprovalsModel : PageModel
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<ApprovalsModel> _logger;

    public List<Expense> SubmittedExpenses { get; set; } = [];
    public List<User> Users { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public ApprovalsModel(ExpenseService expenseService, ILogger<ApprovalsModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            // StatusId 2 = Submitted
            var expensesTask = _expenseService.GetExpensesAsync(statusId: 2);
            var usersTask = _expenseService.GetUsersAsync();
            await Task.WhenAll(expensesTask, usersTask);

            SubmittedExpenses = await expensesTask;
            Users = await usersTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading approvals page.");
            ErrorMessage = $"Unable to load data: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(int id, int reviewerId)
    {
        try
        {
            await _expenseService.ApproveExpenseAsync(id, reviewerId);
            TempData["SuccessMessage"] = $"Expense #{id} approved successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense {Id}.", id);
            TempData["ErrorMessage"] = $"Failed to approve expense: {ex.Message}";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id, int reviewerId)
    {
        try
        {
            await _expenseService.RejectExpenseAsync(id, reviewerId);
            TempData["SuccessMessage"] = $"Expense #{id} rejected.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense {Id}.", id);
            TempData["ErrorMessage"] = $"Failed to reject expense: {ex.Message}";
        }
        return RedirectToPage();
    }
}
