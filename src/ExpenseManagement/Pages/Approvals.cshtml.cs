using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ApprovalsModel : PageModel
{
    private readonly ILogger<ApprovalsModel> _logger;
    private readonly IExpenseService _expenseService;

    public List<Expense> PendingExpenses { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public ApprovalsModel(ILogger<ApprovalsModel> logger, IExpenseService expenseService)
    {
        _logger = logger;
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        try
        {
            PendingExpenses = await _expenseService.GetPendingApprovalsAsync();

            if (!_expenseService.IsConnected)
            {
                ErrorMessage = _expenseService.LastError ?? "Using demo data - database not connected.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pending approvals");
            ErrorMessage = $"Error loading data: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(int expenseId)
    {
        try
        {
            var success = await _expenseService.ApproveExpenseAsync(expenseId, reviewerId: 2);
            if (success)
            {
                SuccessMessage = $"Expense #{expenseId} approved successfully.";
            }
            else
            {
                ErrorMessage = _expenseService.LastError ?? "Failed to approve expense.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense {Id}", expenseId);
            ErrorMessage = $"Error: {ex.Message}";
        }

        await OnGetAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(int expenseId)
    {
        try
        {
            var success = await _expenseService.RejectExpenseAsync(expenseId, reviewerId: 2);
            if (success)
            {
                SuccessMessage = $"Expense #{expenseId} rejected.";
            }
            else
            {
                ErrorMessage = _expenseService.LastError ?? "Failed to reject expense.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting expense {Id}", expenseId);
            ErrorMessage = $"Error: {ex.Message}";
        }

        await OnGetAsync();
        return Page();
    }
}
