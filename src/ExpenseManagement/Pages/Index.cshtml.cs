using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly ExpenseService _expenseService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ExpenseService expenseService, ILogger<IndexModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public List<ExpenseSummary> Summary { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? ErrorLocation { get; set; }
    public string? ErrorGuidance { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            Summary = await _expenseService.GetExpenseSummaryAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            ErrorLocation = $"{nameof(IndexModel)}.OnGetAsync";
            
            if (ex.Message.Contains("Managed Identity") || ex.Message.Contains("authentication"))
            {
                ErrorGuidance = "Check that AZURE_CLIENT_ID environment variable is set and the managed identity has database permissions.";
            }
            else if (ex.Message.Contains("connection"))
            {
                ErrorGuidance = "Verify the connection string in appsettings.json or App Service configuration.";
            }

            _logger.LogError(ex, "Error loading dashboard");
            
            // Summary will be populated with dummy data by the service
            Summary = await _expenseService.GetExpenseSummaryAsync();
        }
    }
}
