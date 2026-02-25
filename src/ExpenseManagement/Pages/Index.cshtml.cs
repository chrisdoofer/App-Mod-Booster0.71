using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<IndexModel> _logger;

    public List<ExpenseSummary> Summary { get; set; } = [];
    public List<Expense> RecentExpenses { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public IndexModel(ExpenseService expenseService, ILogger<IndexModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            var summaryTask = _expenseService.GetExpenseSummaryAsync();
            var expensesTask = _expenseService.GetExpensesAsync();
            await Task.WhenAll(summaryTask, expensesTask);

            Summary = await summaryTask;
            RecentExpenses = (await expensesTask).Take(10).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard data.");
            ErrorMessage = $"Unable to load data from the database: {ex.Message}";
            Summary = [];
            RecentExpenses = [];
        }
    }
}
