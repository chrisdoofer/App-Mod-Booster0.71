using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class ExpensesModel : PageModel
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<ExpensesModel> _logger;

    public List<Expense> Expenses { get; set; } = [];
    public List<ExpenseStatus> Statuses { get; set; } = [];
    public List<User> Users { get; set; } = [];
    public int? SelectedStatusId { get; set; }
    public int? SelectedUserId { get; set; }
    public string? ErrorMessage { get; set; }

    public ExpensesModel(ExpenseService expenseService, ILogger<ExpensesModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync(int? statusId, int? userId)
    {
        SelectedStatusId = statusId;
        SelectedUserId = userId;

        try
        {
            var expensesTask = _expenseService.GetExpensesAsync(userId, statusId);
            var statusesTask = _expenseService.GetStatusesAsync();
            var usersTask = _expenseService.GetUsersAsync();
            await Task.WhenAll(expensesTask, statusesTask, usersTask);

            Expenses = await expensesTask;
            Statuses = await statusesTask;
            Users = await usersTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading expenses page.");
            ErrorMessage = $"Unable to load data: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostSubmitAsync(int id)
    {
        try
        {
            await _expenseService.SubmitExpenseAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting expense {Id}.", id);
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        try
        {
            await _expenseService.DeleteExpenseAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {Id}.", id);
        }
        return RedirectToPage();
    }
}
