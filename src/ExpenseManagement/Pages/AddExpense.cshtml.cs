using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ExpenseManagement.Pages;

public class AddExpenseModel : PageModel
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<AddExpenseModel> _logger;

    [BindProperty]
    public CreateExpenseRequest Input { get; set; } = new();

    public List<SelectListItem> UserSelectList { get; set; } = [];
    public List<SelectListItem> CategorySelectList { get; set; } = [];
    public string? ErrorMessage { get; set; }

    public AddExpenseModel(ExpenseService expenseService, ILogger<AddExpenseModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        await LoadSelectListsAsync();
        Input.ExpenseDate = DateTime.Today;
        Input.Currency = "GBP";
    }

    public async Task<IActionResult> OnPostAsync(decimal amountPounds)
    {
        // Convert pounds to minor units (pence)
        Input.AmountMinor = (int)(amountPounds * 100);

        if (!ModelState.IsValid || Input.UserId == 0 || Input.CategoryId == 0 || Input.AmountMinor <= 0)
        {
            await LoadSelectListsAsync();
            ErrorMessage = "Please fill in all required fields with valid values.";
            return Page();
        }

        try
        {
            var expenseId = await _expenseService.CreateExpenseAsync(Input);
            TempData["SuccessMessage"] = $"Expense #{expenseId} created successfully.";
            return RedirectToPage("/Expenses");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense.");
            ErrorMessage = $"Failed to create expense: {ex.Message}";
            await LoadSelectListsAsync();
            return Page();
        }
    }

    private async Task LoadSelectListsAsync()
    {
        try
        {
            var users = await _expenseService.GetUsersAsync();
            UserSelectList = users
                .Select(u => new SelectListItem(u.UserName, u.UserId.ToString()))
                .ToList();

            var categories = await _expenseService.GetCategoriesAsync();
            CategorySelectList = categories
                .Select(c => new SelectListItem(c.CategoryName, c.CategoryId.ToString()))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading select lists.");
            ErrorMessage = $"Warning: Could not load users/categories from database. {ex.Message}";
        }
    }
}
