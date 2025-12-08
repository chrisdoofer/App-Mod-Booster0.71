using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ApprovalsModel : PageModel
{
    private readonly IExpenseService _expenseService;

    public ApprovalsModel(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    public List<Expense> PendingExpenses { get; set; } = new();

    public async Task OnGetAsync()
    {
        var (expenses, error) = await _expenseService.GetExpensesByStatusAsync("Submitted");
        PendingExpenses = expenses;
        
        if (error != null)
        {
            ViewData["Error"] = error;
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(int id)
    {
        // Bob Manager (ID 2) is the default reviewer
        await _expenseService.ApproveExpenseAsync(id, 2);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id)
    {
        await _expenseService.RejectExpenseAsync(id, 2);
        return RedirectToPage();
    }
}
