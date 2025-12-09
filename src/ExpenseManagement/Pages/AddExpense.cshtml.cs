using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class AddExpenseModel : PageModel
{
    private readonly ILogger<AddExpenseModel> _logger;
    private readonly IExpenseService _expenseService;

    public List<Category> Categories { get; set; } = new();
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public decimal Amount { get; set; }

    [BindProperty]
    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    [BindProperty]
    public int CategoryId { get; set; } = 1;

    [BindProperty]
    public string? Description { get; set; }

    public AddExpenseModel(ILogger<AddExpenseModel> logger, IExpenseService expenseService)
    {
        _logger = logger;
        _expenseService = expenseService;
    }

    public async Task OnGetAsync()
    {
        Categories = await _expenseService.GetCategoriesAsync();
    }

    public async Task<IActionResult> OnPostSaveDraftAsync()
    {
        return await CreateExpense(submit: false);
    }

    public async Task<IActionResult> OnPostSubmitAsync()
    {
        return await CreateExpense(submit: true);
    }

    private async Task<IActionResult> CreateExpense(bool submit)
    {
        Categories = await _expenseService.GetCategoriesAsync();

        if (Amount <= 0)
        {
            ErrorMessage = "Amount must be greater than zero.";
            return Page();
        }

        try
        {
            var request = new CreateExpenseRequest
            {
                UserId = 1, // Default to first user
                CategoryId = CategoryId,
                Amount = Amount,
                ExpenseDate = ExpenseDate,
                Description = Description,
                Submit = submit
            };

            var expenseId = await _expenseService.CreateExpenseAsync(request);

            if (expenseId <= 0)
            {
                ErrorMessage = _expenseService.LastError ?? "Failed to create expense.";
                return Page();
            }

            return RedirectToPage("/Expenses");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            ErrorMessage = $"Error: {ex.Message}";
            return Page();
        }
    }
}
