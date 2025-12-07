using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly ExpenseService _expenseService;

    public List<Expense> Expenses { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public IndexModel(ILogger<IndexModel> logger, ExpenseService expenseService)
    {
        _logger = logger;
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        try
        {
            Expenses = await _expenseService.GetAllExpensesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading expenses");
            ErrorMessage = $"Unable to load expenses from database. Error: {ex.Message}. " +
                         "Please check that the database connection is configured correctly and the managed identity has proper permissions.";
            
            // Use dummy data as fallback
            Expenses = GetDummyExpenses();
        }
    }

    private List<Expense> GetDummyExpenses()
    {
        return new List<Expense>
        {
            new() {
                ExpenseId = 1,
                UserId = 1,
                UserName = "Alice Example",
                CategoryId = 1,
                CategoryName = "Travel",
                StatusId = 2,
                StatusName = "Submitted",
                AmountMinor = 2540,
                ExpenseDate = DateTime.Now.AddDays(-5),
                Description = "Taxi from airport to client site",
                CreatedAt = DateTime.Now.AddDays(-5)
            },
            new() {
                ExpenseId = 2,
                UserId = 1,
                UserName = "Alice Example",
                CategoryId = 2,
                CategoryName = "Meals",
                StatusId = 3,
                StatusName = "Approved",
                AmountMinor = 1425,
                ExpenseDate = DateTime.Now.AddDays(-10),
                Description = "Client lunch meeting",
                CreatedAt = DateTime.Now.AddDays(-10)
            }
        };
    }
}
