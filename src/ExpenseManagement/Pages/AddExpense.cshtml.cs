using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ExpenseManagement.Models;
using ExpenseManagement.Services;

namespace ExpenseManagement.Pages;

public class AddExpenseModel : PageModel
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<AddExpenseModel> _logger;

    [BindProperty]
    public CreateExpenseRequest NewExpense { get; set; } = new()
    {
        ExpenseDate = DateTime.Today,
        Currency = "GBP"
    };

    public List<Category> Categories { get; set; } = new();
    public List<User> Users { get; set; } = new();
    public bool Success { get; set; }
    public int? NewExpenseId { get; set; }

    public AddExpenseModel(ExpenseService expenseService, ILogger<AddExpenseModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            Categories = await _expenseService.GetAllCategoriesAsync();
            Users = await _expenseService.GetAllUsersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load categories and users");
            ViewData["ErrorMessage"] = "Unable to load form data. Please check the database connection.";
            LoadDummyData();
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await OnGetAsync();
            return Page();
        }

        try
        {
            NewExpenseId = await _expenseService.CreateExpenseAsync(NewExpense);
            Success = true;

            // Reload form data for another entry
            Categories = await _expenseService.GetAllCategoriesAsync();
            Users = await _expenseService.GetAllUsersAsync();

            // Reset form
            NewExpense = new CreateExpenseRequest
            {
                ExpenseDate = DateTime.Today,
                Currency = "GBP"
            };

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create expense");
            ViewData["ErrorMessage"] = "Unable to save the expense. Please try again.";
            await OnGetAsync();
            return Page();
        }
    }

    private void LoadDummyData()
    {
        Categories = new List<Category>
        {
            new Category { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new Category { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new Category { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new Category { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new Category { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };

        Users = new List<User>
        {
            new User { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleName = "Employee", IsActive = true, CreatedAt = DateTime.Now },
            new User { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleName = "Manager", IsActive = true, CreatedAt = DateTime.Now }
        };
    }
}
