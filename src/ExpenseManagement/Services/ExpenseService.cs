using ExpenseManagement.Models;
using Microsoft.Data.SqlClient;

namespace ExpenseManagement.Services;

public class ExpenseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private string GetConnectionString()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        }
        return connectionString;
    }

    public async Task<List<Expense>> GetExpensesAsync(string? status = null, string? employeeName = null)
    {
        var expenses = new List<Expense>();
        
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("usp_GetExpenses", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            if (!string.IsNullOrEmpty(status))
            {
                command.Parameters.AddWithValue("@Status", status);
            }
            if (!string.IsNullOrEmpty(employeeName))
            {
                command.Parameters.AddWithValue("@EmployeeName", employeeName);
            }

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(new Expense
                {
                    ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    UserName = reader.GetString(reader.GetOrdinal("UserName")),
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                    AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
                    Amount = reader.GetDecimal(reader.GetOrdinal("AmountDecimal")),
                    Currency = reader.GetString(reader.GetOrdinal("Currency")),
                    ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
                    Description = reader.IsDBNull(reader.GetOrdinal("Description")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("Description")),
                    SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) 
                        ? null 
                        : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
                    ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) 
                        ? null 
                        : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
                    ReviewerName = reader.IsDBNull(reader.GetOrdinal("ReviewedByName")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("ReviewedByName")),
                    ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) 
                        ? null 
                        : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses from database at {File}", 
                nameof(ExpenseService));
            
            // Fall back to dummy data
            expenses = GetDummyExpenses();
        }

        return expenses;
    }

    public async Task<List<ExpenseSummary>> GetExpenseSummaryAsync()
    {
        var summary = new List<ExpenseSummary>();
        
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("usp_GetExpenseSummary", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                summary.Add(new ExpenseSummary
                {
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                    Count = reader.GetInt32(reader.GetOrdinal("ExpenseCount")),
                    TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount"))
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense summary from database");
            
            // Fall back to dummy data
            summary = GetDummySummary();
        }

        return summary;
    }

    public async Task<List<ExpenseCategory>> GetCategoriesAsync()
    {
        var categories = new List<ExpenseCategory>();
        
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("usp_GetCategories", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new ExpenseCategory
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving categories from database");
            
            // Fall back to dummy data
            categories = GetDummyCategories();
        }

        return categories;
    }

    public async Task<int> CreateExpenseAsync(CreateExpenseRequest request)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("usp_CreateExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@EmployeeName", request.EmployeeName);
            command.Parameters.AddWithValue("@Description", request.Description);
            command.Parameters.AddWithValue("@AmountMinor", (int)(request.Amount * 100));
            command.Parameters.AddWithValue("@Category", request.Category);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);

            var result = await command.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense in database");
            throw;
        }
    }

    public async Task<bool> ApproveExpenseAsync(ApproveExpenseRequest request)
    {
        try
        {
            using var connection = new SqlConnection(GetConnectionString());
            await connection.OpenAsync();

            using var command = new SqlCommand("usp_ApproveExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@ExpenseId", request.ExpenseId);
            command.Parameters.AddWithValue("@ReviewerName", request.ReviewerName);
            command.Parameters.AddWithValue("@Approved", request.Approved);

            await command.ExecuteNonQueryAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving expense in database");
            throw;
        }
    }

    // Dummy data for fallback when database is unavailable
    private List<Expense> GetDummyExpenses()
    {
        return new List<Expense>
        {
            new Expense
            {
                ExpenseId = 1,
                UserId = 1,
                UserName = "John Doe",
                CategoryId = 1,
                CategoryName = "Travel",
                StatusId = 2,
                StatusName = "Submitted",
                AmountMinor = 5000,
                Amount = 50.00m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-5),
                Description = "Train ticket to London",
                SubmittedAt = DateTime.Now.AddDays(-5),
                CreatedAt = DateTime.Now.AddDays(-5)
            },
            new Expense
            {
                ExpenseId = 2,
                UserId = 2,
                UserName = "Jane Smith",
                CategoryId = 2,
                CategoryName = "Meals",
                StatusId = 3,
                StatusName = "Approved",
                AmountMinor = 2500,
                Amount = 25.00m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-3),
                Description = "Client lunch",
                SubmittedAt = DateTime.Now.AddDays(-3),
                ReviewedBy = 3,
                ReviewerName = "Manager",
                ReviewedAt = DateTime.Now.AddDays(-2),
                CreatedAt = DateTime.Now.AddDays(-3)
            }
        };
    }

    private List<ExpenseSummary> GetDummySummary()
    {
        return new List<ExpenseSummary>
        {
            new ExpenseSummary { StatusName = "Submitted", Count = 5, TotalAmount = 250.00m },
            new ExpenseSummary { StatusName = "Approved", Count = 10, TotalAmount = 500.00m },
            new ExpenseSummary { StatusName = "Rejected", Count = 2, TotalAmount = 50.00m }
        };
    }

    private List<ExpenseCategory> GetDummyCategories()
    {
        return new List<ExpenseCategory>
        {
            new ExpenseCategory { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new ExpenseCategory { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new ExpenseCategory { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new ExpenseCategory { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new ExpenseCategory { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };
    }
}
