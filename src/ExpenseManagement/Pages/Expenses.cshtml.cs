using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ExpensesModel : PageModel
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<ExpensesModel> _logger;

    public ExpensesModel(ExpenseService expenseService, ILogger<ExpensesModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public List<Expense> Expenses { get; set; } = new();
    
    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public string? EmployeeFilter { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            Expenses = await _expenseService.GetExpensesAsync(StatusFilter, EmployeeFilter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading expenses");
            Expenses = new List<Expense>();
        }
    }
}
