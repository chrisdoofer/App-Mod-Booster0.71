using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ApprovalsModel : PageModel
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<ApprovalsModel> _logger;

    public ApprovalsModel(ExpenseService expenseService, ILogger<ApprovalsModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public List<Expense> PendingExpenses { get; set; } = new();
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public int ExpenseId { get; set; }
    
    [BindProperty]
    public string ReviewerName { get; set; } = "Manager";
    
    [BindProperty]
    public bool Approved { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            // Get all submitted expenses
            PendingExpenses = await _expenseService.GetExpensesAsync("Submitted");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pending expenses");
            PendingExpenses = new List<Expense>();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            var request = new ApproveExpenseRequest
            {
                ExpenseId = ExpenseId,
                ReviewerName = ReviewerName,
                Approved = Approved
            };

            await _expenseService.ApproveExpenseAsync(request);
            SuccessMessage = Approved 
                ? $"Expense {ExpenseId} has been approved." 
                : $"Expense {ExpenseId} has been rejected.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense");
            ErrorMessage = $"Failed to process approval: {ex.Message}";
        }

        // Reload the pending expenses
        await OnGetAsync();
        return Page();
    }
}
