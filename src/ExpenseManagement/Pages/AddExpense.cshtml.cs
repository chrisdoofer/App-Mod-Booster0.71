using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class AddExpenseModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public AddExpenseModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public List<Category> Categories { get; set; } = new();
    public List<User> Users { get; set; } = new();

    [BindProperty]
    public decimal Amount { get; set; }

    [BindProperty]
    public int CategoryId { get; set; }

    [BindProperty]
    public int UserId { get; set; }

    [BindProperty]
    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    [BindProperty]
    public string? Description { get; set; }

    public async Task OnGetAsync()
    {
        var (categories, catError) = await _expenseService.GetCategoriesAsync();
        Categories = categories;
        if (catError != null)
        {
            ViewData["Error"] = catError;
        }

        var (users, userError) = await _expenseService.GetUsersAsync();
        Users = users;
        if (userError != null && catError == null)
        {
            ViewData["Error"] = userError;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var request = new CreateExpenseRequest
        {
            UserId = UserId,
            CategoryId = CategoryId,
            Amount = Amount,
            Currency = "GBP",
            ExpenseDate = ExpenseDate,
            Description = Description
        };

        var (expenseId, error) = await _expenseService.CreateExpenseAsync(request);
        
        if (error != null)
        {
            var (categories, _) = await _expenseService.GetCategoriesAsync();
            Categories = categories;
            var (users, _) = await _expenseService.GetUsersAsync();
            Users = users;
            ViewData["Error"] = error;
            return Page();
        }

        return RedirectToPage("/Expenses");
    }
}
