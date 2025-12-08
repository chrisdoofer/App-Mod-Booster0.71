using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<IndexModel> _logger;

    public List<Expense> Expenses { get; set; } = new();
    public List<ExpenseSummary> ExpenseSummaries { get; set; } = new();
    public string? FilterStatus { get; set; }
    public bool UseDummyData { get; set; }

    public IndexModel(ExpenseService expenseService, ILogger<IndexModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync(string? status)
    {
        FilterStatus = status;

        try
        {
            // Try to get real data from database
            if (string.IsNullOrEmpty(status))
            {
                Expenses = await _expenseService.GetAllExpensesAsync();
            }
            else
            {
                Expenses = await _expenseService.GetExpensesByStatusAsync(status);
            }

            ExpenseSummaries = await _expenseService.GetExpenseSummaryAsync();
            UseDummyData = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve expenses from database");

            // Fall back to dummy data
            UseDummyData = true;
            LoadDummyData(status);

            // Set error information for display
            ViewData["ErrorMessage"] = "Unable to connect to the database. Showing sample data instead.";
            ViewData["ErrorLocation"] = $"{nameof(IndexModel)}.OnGetAsync";

            // Provide helpful troubleshooting hints
            if (ex.Message.Contains("managed identity", StringComparison.OrdinalIgnoreCase))
            {
                ViewData["ErrorHint"] = "The AZURE_CLIENT_ID environment variable may not be set, or the managed identity needs database permissions. Check the deployment script configuration.";
            }
            else if (ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase))
            {
                ViewData["ErrorHint"] = "Verify the connection string in App Service configuration includes 'Authentication=Active Directory Managed Identity' and 'User Id={clientId}'.";
            }
            else
            {
                ViewData["ErrorHint"] = $"Error: {ex.Message}";
            }
        }
    }

    private void LoadDummyData(string? status)
    {
        // Create sample expenses for demonstration
        var allExpenses = new List<Expense>
        {
            new Expense
            {
                ExpenseId = 1,
                UserId = 1,
                UserName = "Alice Example",
                CategoryId = 1,
                CategoryName = "Travel",
                StatusId = 2,
                StatusName = "Submitted",
                AmountMinor = 2540,
                Amount = 25.40m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-5),
                Description = "Taxi from airport to client site",
                SubmittedAt = DateTime.Now.AddDays(-5),
                CreatedAt = DateTime.Now.AddDays(-5)
            },
            new Expense
            {
                ExpenseId = 2,
                UserId = 1,
                UserName = "Alice Example",
                CategoryId = 2,
                CategoryName = "Meals",
                StatusId = 3,
                StatusName = "Approved",
                AmountMinor = 1425,
                Amount = 14.25m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-10),
                Description = "Client lunch meeting",
                SubmittedAt = DateTime.Now.AddDays(-10),
                ReviewedBy = 2,
                ReviewerName = "Bob Manager",
                ReviewedAt = DateTime.Now.AddDays(-9),
                CreatedAt = DateTime.Now.AddDays(-10)
            },
            new Expense
            {
                ExpenseId = 3,
                UserId = 1,
                UserName = "Alice Example",
                CategoryId = 3,
                CategoryName = "Supplies",
                StatusId = 1,
                StatusName = "Draft",
                AmountMinor = 799,
                Amount = 7.99m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-1),
                Description = "Office stationery",
                CreatedAt = DateTime.Now.AddDays(-1)
            },
            new Expense
            {
                ExpenseId = 4,
                UserId = 1,
                UserName = "Alice Example",
                CategoryId = 4,
                CategoryName = "Accommodation",
                StatusId = 3,
                StatusName = "Approved",
                AmountMinor = 12300,
                Amount = 123.00m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-20),
                Description = "Hotel during client visit",
                SubmittedAt = DateTime.Now.AddDays(-19),
                ReviewedBy = 2,
                ReviewerName = "Bob Manager",
                ReviewedAt = DateTime.Now.AddDays(-18),
                CreatedAt = DateTime.Now.AddDays(-20)
            }
        };

        // Filter by status if specified
        if (!string.IsNullOrEmpty(status))
        {
            Expenses = allExpenses.Where(e => e.StatusName == status).ToList();
        }
        else
        {
            Expenses = allExpenses;
        }

        // Create summary data
        ExpenseSummaries = allExpenses
            .GroupBy(e => e.StatusName)
            .Select(g => new ExpenseSummary
            {
                StatusName = g.Key,
                Count = g.Count(),
                TotalAmount = g.Sum(e => e.Amount)
            })
            .ToList();
    }
}
