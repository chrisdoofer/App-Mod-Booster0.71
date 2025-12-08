using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public IndexModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public DashboardStats? Stats { get; set; }
    public List<Expense> RecentExpenses { get; set; } = new();

    public async Task OnGetAsync()
    {
        var (stats, statsError) = await _expenseService.GetDashboardStatsAsync();
        Stats = stats;
        
        if (statsError != null)
        {
            ViewData["Error"] = statsError;
        }

        var (expenses, expensesError) = await _expenseService.GetAllExpensesAsync();
        RecentExpenses = expenses.Take(10).ToList();
        
        if (expensesError != null && statsError == null)
        {
            ViewData["Error"] = expensesError;
        }
    }
}
