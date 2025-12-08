using ExpenseManagement.Models;
using Microsoft.Data.SqlClient;
using System.Runtime.CompilerServices;

namespace ExpenseManagement.Services;

/// <summary>
/// Interface for expense data operations
/// </summary>
public interface IExpenseService
{
    Task<(List<Expense> Expenses, ErrorInfo? Error)> GetAllExpensesAsync();
    Task<(Expense? Expense, ErrorInfo? Error)> GetExpenseByIdAsync(int id);
    Task<(List<Expense> Expenses, ErrorInfo? Error)> GetExpensesByStatusAsync(string status);
    Task<(List<Expense> Expenses, ErrorInfo? Error)> GetExpensesByUserAsync(int userId);
    Task<(List<ExpenseSummary> Summary, ErrorInfo? Error)> GetExpenseSummaryAsync();
    Task<(int? ExpenseId, ErrorInfo? Error)> CreateExpenseAsync(CreateExpenseRequest request);
    Task<(bool Success, ErrorInfo? Error)> SubmitExpenseAsync(int expenseId);
    Task<(bool Success, ErrorInfo? Error)> ApproveExpenseAsync(int expenseId, int reviewerId);
    Task<(bool Success, ErrorInfo? Error)> RejectExpenseAsync(int expenseId, int reviewerId);
    Task<(bool Success, ErrorInfo? Error)> DeleteExpenseAsync(int expenseId);
    Task<(List<Category> Categories, ErrorInfo? Error)> GetCategoriesAsync();
    Task<(List<User> Users, ErrorInfo? Error)> GetUsersAsync();
    Task<(List<ExpenseStatus> Statuses, ErrorInfo? Error)> GetStatusesAsync();
    List<Expense> GetDummyExpenses();
}

/// <summary>
/// Service for expense data operations using stored procedures
/// </summary>
public class ExpenseService : IExpenseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExpenseService> _logger;
    private readonly string _connectionString;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    private ErrorInfo CreateError(Exception ex, [CallerMemberName] string? memberName = null, [CallerLineNumber] int lineNumber = 0)
    {
        var error = new ErrorInfo
        {
            Message = ex.Message,
            Location = $"ExpenseService.{memberName} (line {lineNumber})"
        };

        // Add specific guidance for common errors
        if (ex.Message.Contains("Unable to load the proper Managed Identity") ||
            ex.Message.Contains("ManagedIdentityCredential"))
        {
            error.Guidance = "The AZURE_CLIENT_ID environment variable may not be set, or the managed identity configuration is incorrect.";
        }
        else if (ex.Message.Contains("Login failed"))
        {
            error.Guidance = "The managed identity may not have been granted database permissions. Check that the database user was created correctly.";
        }
        else if (ex.Message.Contains("connection string"))
        {
            error.Guidance = "The database connection string may be missing or incorrectly configured.";
        }

        _logger.LogError(ex, "Error in {Method}: {Message}", memberName, ex.Message);
        return error;
    }

    public async Task<(List<Expense> Expenses, ErrorInfo? Error)> GetAllExpensesAsync()
    {
        try
        {
            var expenses = new List<Expense>();
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.GetAllExpenses", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }

            return (expenses, null);
        }
        catch (Exception ex)
        {
            return (GetDummyExpenses(), CreateError(ex));
        }
    }

    public async Task<(Expense? Expense, ErrorInfo? Error)> GetExpenseByIdAsync(int id)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.GetExpenseById", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", id);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (MapExpense(reader), null);
            }

            return (null, null);
        }
        catch (Exception ex)
        {
            return (null, CreateError(ex));
        }
    }

    public async Task<(List<Expense> Expenses, ErrorInfo? Error)> GetExpensesByStatusAsync(string status)
    {
        try
        {
            var expenses = new List<Expense>();
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.GetExpensesByStatus", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@StatusName", status);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }

            return (expenses, null);
        }
        catch (Exception ex)
        {
            return (new List<Expense>(), CreateError(ex));
        }
    }

    public async Task<(List<Expense> Expenses, ErrorInfo? Error)> GetExpensesByUserAsync(int userId)
    {
        try
        {
            var expenses = new List<Expense>();
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.GetExpensesByUser", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@UserId", userId);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }

            return (expenses, null);
        }
        catch (Exception ex)
        {
            return (new List<Expense>(), CreateError(ex));
        }
    }

    public async Task<(List<ExpenseSummary> Summary, ErrorInfo? Error)> GetExpenseSummaryAsync()
    {
        try
        {
            var summary = new List<ExpenseSummary>();
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.GetExpenseSummary", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                summary.Add(new ExpenseSummary
                {
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                    Count = reader.GetInt32(reader.GetOrdinal("ExpenseCount")),
                    TotalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount"))
                });
            }

            return (summary, null);
        }
        catch (Exception ex)
        {
            return (new List<ExpenseSummary>(), CreateError(ex));
        }
    }

    public async Task<(int? ExpenseId, ErrorInfo? Error)> CreateExpenseAsync(CreateExpenseRequest request)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.CreateExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            var amountMinor = (int)Math.Round(request.Amount * 100m, MidpointRounding.AwayFromZero);
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", amountMinor);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", request.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", request.ReceiptFile ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Currency", request.Currency);

            var result = await command.ExecuteScalarAsync();
            var expenseId = Convert.ToInt32(result);

            return (expenseId, null);
        }
        catch (Exception ex)
        {
            return (null, CreateError(ex));
        }
    }

    public async Task<(bool Success, ErrorInfo? Error)> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.SubmitExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var result = await command.ExecuteScalarAsync();
            var rowsAffected = Convert.ToInt32(result);

            return (rowsAffected > 0, null);
        }
        catch (Exception ex)
        {
            return (false, CreateError(ex));
        }
    }

    public async Task<(bool Success, ErrorInfo? Error)> ApproveExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.ApproveExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            var result = await command.ExecuteScalarAsync();
            var rowsAffected = Convert.ToInt32(result);

            return (rowsAffected > 0, null);
        }
        catch (Exception ex)
        {
            return (false, CreateError(ex));
        }
    }

    public async Task<(bool Success, ErrorInfo? Error)> RejectExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.RejectExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            var result = await command.ExecuteScalarAsync();
            var rowsAffected = Convert.ToInt32(result);

            return (rowsAffected > 0, null);
        }
        catch (Exception ex)
        {
            return (false, CreateError(ex));
        }
    }

    public async Task<(bool Success, ErrorInfo? Error)> DeleteExpenseAsync(int expenseId)
    {
        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.DeleteExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var result = await command.ExecuteScalarAsync();
            var rowsAffected = Convert.ToInt32(result);

            return (rowsAffected > 0, null);
        }
        catch (Exception ex)
        {
            return (false, CreateError(ex));
        }
    }

    public async Task<(List<Category> Categories, ErrorInfo? Error)> GetCategoriesAsync()
    {
        try
        {
            var categories = new List<Category>();
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.GetCategories", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new Category
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }

            return (categories, null);
        }
        catch (Exception ex)
        {
            // Return dummy categories on error
            return (new List<Category>
            {
                new() { CategoryId = 1, CategoryName = "Travel", IsActive = true },
                new() { CategoryId = 2, CategoryName = "Meals", IsActive = true },
                new() { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
                new() { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
                new() { CategoryId = 5, CategoryName = "Other", IsActive = true }
            }, CreateError(ex));
        }
    }

    public async Task<(List<User> Users, ErrorInfo? Error)> GetUsersAsync()
    {
        try
        {
            var users = new List<User>();
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.GetUsers", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    UserName = reader.GetString(reader.GetOrdinal("UserName")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                    RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
                    ManagerId = reader.IsDBNull(reader.GetOrdinal("ManagerId")) ? null : reader.GetInt32(reader.GetOrdinal("ManagerId")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }

            return (users, null);
        }
        catch (Exception ex)
        {
            // Return dummy users on error
            return (new List<User>
            {
                new() { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleId = 1, RoleName = "Employee", IsActive = true },
                new() { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true }
            }, CreateError(ex));
        }
    }

    public async Task<(List<ExpenseStatus> Statuses, ErrorInfo? Error)> GetStatusesAsync()
    {
        try
        {
            var statuses = new List<ExpenseStatus>();
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.GetStatuses", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName"))
                });
            }

            return (statuses, null);
        }
        catch (Exception ex)
        {
            // Return dummy statuses on error
            return (new List<ExpenseStatus>
            {
                new() { StatusId = 1, StatusName = "Draft" },
                new() { StatusId = 2, StatusName = "Submitted" },
                new() { StatusId = 3, StatusName = "Approved" },
                new() { StatusId = 4, StatusName = "Rejected" }
            }, CreateError(ex));
        }
    }

    public List<Expense> GetDummyExpenses()
    {
        return new List<Expense>
        {
            new()
            {
                ExpenseId = 1,
                UserId = 1,
                UserName = "Alice Example",
                Email = "alice@example.co.uk",
                CategoryId = 1,
                CategoryName = "Travel",
                StatusId = 2,
                StatusName = "Submitted",
                AmountMinor = 12000,
                Amount = 120.00m,
                Currency = "GBP",
                ExpenseDate = new DateTime(2024, 1, 15),
                Description = "Taxi from airport to client site",
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new()
            {
                ExpenseId = 2,
                UserId = 1,
                UserName = "Alice Example",
                Email = "alice@example.co.uk",
                CategoryId = 2,
                CategoryName = "Meals",
                StatusId = 2,
                StatusName = "Submitted",
                AmountMinor = 6900,
                Amount = 69.00m,
                Currency = "GBP",
                ExpenseDate = new DateTime(2023, 10, 1),
                Description = "Client lunch meeting",
                CreatedAt = DateTime.UtcNow.AddDays(-30)
            },
            new()
            {
                ExpenseId = 3,
                UserId = 1,
                UserName = "Alice Example",
                Email = "alice@example.co.uk",
                CategoryId = 3,
                CategoryName = "Supplies",
                StatusId = 3,
                StatusName = "Approved",
                AmountMinor = 9950,
                Amount = 99.50m,
                Currency = "GBP",
                ExpenseDate = new DateTime(2023, 12, 4),
                Description = "Office supplies",
                ReviewerName = "Bob Manager",
                ReviewedAt = DateTime.UtcNow.AddDays(-5),
                CreatedAt = DateTime.UtcNow.AddDays(-20)
            },
            new()
            {
                ExpenseId = 4,
                UserId = 1,
                UserName = "Alice Example",
                Email = "alice@example.co.uk",
                CategoryId = 1,
                CategoryName = "Travel",
                StatusId = 3,
                StatusName = "Approved",
                AmountMinor = 1920,
                Amount = 19.20m,
                Currency = "GBP",
                ExpenseDate = new DateTime(2023, 12, 18),
                Description = "Transport to meeting",
                ReviewerName = "Bob Manager",
                ReviewedAt = DateTime.UtcNow.AddDays(-2),
                CreatedAt = DateTime.UtcNow.AddDays(-15)
            }
        };
    }

    private static Expense MapExpense(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
            CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
            StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
            StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
            AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
            Amount = reader.GetDecimal(reader.GetOrdinal("AmountDecimal")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
            SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
            ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
            ReviewerName = reader.IsDBNull(reader.GetOrdinal("ReviewedByName")) ? null : reader.GetString(reader.GetOrdinal("ReviewedByName")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }
}
