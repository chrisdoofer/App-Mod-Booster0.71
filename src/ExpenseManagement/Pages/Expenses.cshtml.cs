using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class ExpensesModel : PageModel
{
    private readonly ILogger<ExpensesModel> _logger;
    private readonly IExpenseService _expenseService;

    public List<Expense> Expenses { get; set; } = new();
    public string? ErrorMessage { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? Filter { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    public ExpensesModel(ILogger<ExpensesModel> logger, IExpenseService expenseService)
    {
        _logger = logger;
        _expenseService = expenseService;
    }

    public async Task OnGetAsync(string? status)
    {
        try
        {
            StatusFilter = status;
            
            if (!string.IsNullOrEmpty(StatusFilter))
            {
                Expenses = await _expenseService.GetExpensesByStatusAsync(StatusFilter);
            }
            else
            {
                Expenses = await _expenseService.GetAllExpensesAsync();
            }

            // Apply text filter
            if (!string.IsNullOrEmpty(Filter))
            {
                Expenses = Expenses.Where(e => 
                    (e.Description?.Contains(Filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    e.CategoryName.Contains(Filter, StringComparison.OrdinalIgnoreCase) ||
                    e.EmployeeName.Contains(Filter, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            if (!_expenseService.IsConnected)
            {
                ErrorMessage = _expenseService.LastError ?? "Using demo data - database not connected.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading expenses");
            ErrorMessage = $"Error loading data: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostSubmitAsync(int id)
    {
        try
        {
            await _expenseService.SubmitExpenseAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense {Id}", id);
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        try
        {
            await _expenseService.DeleteExpenseAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {Id}", id);
        }
        return RedirectToPage();
    }
}
