using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IExpenseService _expenseService;

    public DashboardStats Stats { get; set; } = new();
    public List<Expense> RecentExpenses { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public IndexModel(ILogger<IndexModel> logger, IExpenseService expenseService)
    {
        _logger = logger;
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        try
        {
            Stats = await _expenseService.GetDashboardStatsAsync();
            var allExpenses = await _expenseService.GetAllExpensesAsync();
            RecentExpenses = allExpenses.Take(10).ToList();

            if (!_expenseService.IsConnected)
            {
                ErrorMessage = _expenseService.LastError ?? "Using demo data - database not connected.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard");
            ErrorMessage = $"Error loading data: {ex.Message}";
        }
    }
}
