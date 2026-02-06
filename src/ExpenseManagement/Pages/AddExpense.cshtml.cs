using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class AddExpenseModel : PageModel
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<AddExpenseModel> _logger;

    public AddExpenseModel(ExpenseService expenseService, ILogger<AddExpenseModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    [BindProperty]
    public string EmployeeName { get; set; } = string.Empty;
    
    [BindProperty]
    public string Description { get; set; } = string.Empty;
    
    [BindProperty]
    public decimal Amount { get; set; }
    
    [BindProperty]
    public string Category { get; set; } = string.Empty;
    
    [BindProperty]
    public DateTime ExpenseDate { get; set; } = DateTime.Now;

    public List<ExpenseCategory> Categories { get; set; } = new();
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        Categories = await _expenseService.GetCategoriesAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Categories = await _expenseService.GetCategoriesAsync();

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please fill in all required fields.";
            return Page();
        }

        try
        {
            var request = new CreateExpenseRequest
            {
                EmployeeName = EmployeeName,
                Description = Description,
                Amount = Amount,
                Category = Category,
                ExpenseDate = ExpenseDate
            };

            var expenseId = await _expenseService.CreateExpenseAsync(request);
            SuccessMessage = $"Expense created successfully! (ID: {expenseId})";
            
            // Clear form
            EmployeeName = string.Empty;
            Description = string.Empty;
            Amount = 0;
            Category = string.Empty;
            ExpenseDate = DateTime.Now;

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            ErrorMessage = $"Failed to create expense: {ex.Message}";
            return Page();
        }
    }
}
