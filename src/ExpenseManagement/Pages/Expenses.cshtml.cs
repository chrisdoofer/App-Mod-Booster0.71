using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ExpensesModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public ExpensesModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public List<Expense> Expenses { get; set; } = new();
    
    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    public async Task OnGetAsync()
    {
        if (!string.IsNullOrEmpty(StatusFilter))
        {
            var (expenses, error) = await _expenseService.GetExpensesByStatusAsync(StatusFilter);
            Expenses = expenses;
            if (error != null)
            {
                ViewData["Error"] = error;
            }
        }
        else
        {
            var (expenses, error) = await _expenseService.GetAllExpensesAsync();
            Expenses = expenses;
            if (error != null)
            {
                ViewData["Error"] = error;
            }
        }
    }

    public async Task<IActionResult> OnPostSubmitAsync(int id)
    {
        await _expenseService.SubmitExpenseAsync(id);
        return RedirectToPage();
    }
}
