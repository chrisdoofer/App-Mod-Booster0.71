using ExpenseManagement.Models;
using Microsoft.Data.SqlClient;

namespace ExpenseManagement.Services;

/// <summary>
/// Data access service that calls stored procedures.
/// Falls back to dummy data gracefully when the database is unavailable,
/// so the application remains usable during connectivity issues.
/// </summary>
public class ExpenseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private SqlConnection CreateConnection()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        return new SqlConnection(connectionString);
    }

    // =====================================================================
    // GetExpensesAsync
    // SP: usp_GetExpenses — returns AmountDecimal, ReviewedByName aliases
    // =====================================================================
    public async Task<List<Expense>> GetExpensesAsync(int? userId = null, int? statusId = null)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("usp_GetExpenses", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);
            command.Parameters.AddWithValue("@StatusId", (object?)statusId ?? DBNull.Value);

            var expenses = new List<Expense>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            return expenses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetExpensesAsync (userId={UserId}, statusId={StatusId}). Returning dummy data.", userId, statusId);
            return GetDummyExpenses();
        }
    }

    // =====================================================================
    // GetExpenseByIdAsync
    // SP: usp_GetExpenseById — returns AmountDecimal, ReviewedByName aliases
    // =====================================================================
    public async Task<Expense?> GetExpenseByIdAsync(int id)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("usp_GetExpenseById", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", id);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpense(reader);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetExpenseByIdAsync (id={Id}). Returning null.", id);
            return null;
        }
    }

    // =====================================================================
    // CreateExpenseAsync
    // SP: usp_CreateExpense
    // =====================================================================
    public async Task<int> CreateExpenseAsync(CreateExpenseRequest request)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("usp_CreateExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", request.AmountMinor);
            command.Parameters.AddWithValue("@Currency", request.Currency);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate.Date);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return Convert.ToInt32(reader["ExpenseId"]);
            }
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in CreateExpenseAsync.");
            throw;
        }
    }

    // =====================================================================
    // UpdateExpenseAsync
    // SP: usp_UpdateExpense
    // =====================================================================
    public async Task<bool> UpdateExpenseAsync(int id, UpdateExpenseRequest request)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("usp_UpdateExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", id);
            command.Parameters.AddWithValue("@CategoryId", (object?)request.CategoryId ?? DBNull.Value);
            command.Parameters.AddWithValue("@AmountMinor", (object?)request.AmountMinor ?? DBNull.Value);
            command.Parameters.AddWithValue("@ExpenseDate", (object?)request.ExpenseDate?.Date ?? DBNull.Value);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return Convert.ToInt32(reader["RowsAffected"]) > 0;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in UpdateExpenseAsync (id={Id}).", id);
            throw;
        }
    }

    // =====================================================================
    // DeleteExpenseAsync
    // SP: usp_DeleteExpense
    // =====================================================================
    public async Task<bool> DeleteExpenseAsync(int id)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("usp_DeleteExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", id);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return Convert.ToInt32(reader["RowsAffected"]) > 0;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in DeleteExpenseAsync (id={Id}).", id);
            throw;
        }
    }

    // =====================================================================
    // SubmitExpenseAsync
    // SP: usp_SubmitExpense
    // =====================================================================
    public async Task<bool> SubmitExpenseAsync(int id)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("usp_SubmitExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", id);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return Convert.ToInt32(reader["RowsAffected"]) > 0;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in SubmitExpenseAsync (id={Id}).", id);
            throw;
        }
    }

    // =====================================================================
    // ApproveExpenseAsync
    // SP: usp_ApproveExpense
    // =====================================================================
    public async Task<bool> ApproveExpenseAsync(int id, int reviewerId)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("usp_ApproveExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", id);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return Convert.ToInt32(reader["RowsAffected"]) > 0;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in ApproveExpenseAsync (id={Id}).", id);
            throw;
        }
    }

    // =====================================================================
    // RejectExpenseAsync
    // SP: usp_RejectExpense
    // =====================================================================
    public async Task<bool> RejectExpenseAsync(int id, int reviewerId)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("usp_RejectExpense", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", id);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return Convert.ToInt32(reader["RowsAffected"]) > 0;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in RejectExpenseAsync (id={Id}).", id);
            throw;
        }
    }

    // =====================================================================
    // GetExpenseSummaryAsync
    // SP: usp_GetExpenseSummary — returns EXACTLY 3 cols: StatusName, ExpenseCount, TotalAmount
    // TotalAmountMinor is calculated in C# (not from DB)
    // =====================================================================
    public async Task<List<ExpenseSummary>> GetExpenseSummaryAsync(int? userId = null)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("usp_GetExpenseSummary", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@UserId", (object?)userId ?? DBNull.Value);

            var summaries = new List<ExpenseSummary>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var totalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount"));
                summaries.Add(new ExpenseSummary
                {
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                    Count = reader.GetInt32(reader.GetOrdinal("ExpenseCount")),
                    TotalAmount = totalAmount
                    // TotalAmountMinor is a calculated property: (int)(TotalAmount * 100)
                });
            }
            return summaries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetExpenseSummaryAsync. Returning dummy summary.");
            return GetDummySummary();
        }
    }

    // =====================================================================
    // GetCategoriesAsync
    // SP: usp_GetCategories
    // =====================================================================
    public async Task<List<ExpenseCategory>> GetCategoriesAsync()
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("usp_GetCategories", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            var categories = new List<ExpenseCategory>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new ExpenseCategory
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }
            return categories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetCategoriesAsync. Returning dummy categories.");
            return GetDummyCategories();
        }
    }

    // =====================================================================
    // GetStatusesAsync
    // SP: usp_GetStatuses
    // =====================================================================
    public async Task<List<ExpenseStatus>> GetStatusesAsync()
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("usp_GetStatuses", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            var statuses = new List<ExpenseStatus>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName"))
                });
            }
            return statuses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetStatusesAsync. Returning dummy statuses.");
            return GetDummyStatuses();
        }
    }

    // =====================================================================
    // GetUsersAsync
    // SP: usp_GetUsers
    // =====================================================================
    public async Task<List<User>> GetUsersAsync()
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = new SqlCommand("usp_GetUsers", connection)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            var users = new List<User>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(MapUser(reader));
            }
            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetUsersAsync. Returning dummy users.");
            return GetDummyUsers();
        }
    }

    // =====================================================================
    // Private helpers
    // =====================================================================

    /// <summary>
    /// Maps a SqlDataReader row to an Expense.
    /// CRITICAL: Uses DB column aliases exactly as defined in stored procedures:
    ///   - "AmountDecimal" (SP alias) → Amount property
    ///   - "ReviewedByName" (SP alias) → ReviewerName property
    /// </summary>
    private static Expense MapExpense(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
            CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
            StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
            StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
            AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
            // SP returns "AmountDecimal" alias — maps to Amount property
            Amount = reader.GetDecimal(reader.GetOrdinal("AmountDecimal")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description"))
                ? string.Empty
                : reader.GetString(reader.GetOrdinal("Description")),
            ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile"))
                ? null
                : reader.GetString(reader.GetOrdinal("ReceiptFile")),
            SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
            // SP returns "ReviewedByName" alias — maps to ReviewerName property
            ReviewerName = reader.IsDBNull(reader.GetOrdinal("ReviewedByName"))
                ? null
                : reader.GetString(reader.GetOrdinal("ReviewedByName")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt"))
                ? null
                : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    private static User MapUser(SqlDataReader reader)
    {
        return new User
        {
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            Email = reader.GetString(reader.GetOrdinal("Email")),
            RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
            RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
            ManagerId = reader.IsDBNull(reader.GetOrdinal("ManagerId"))
                ? null
                : reader.GetInt32(reader.GetOrdinal("ManagerId")),
            ManagerName = reader.IsDBNull(reader.GetOrdinal("ManagerName"))
                ? null
                : reader.GetString(reader.GetOrdinal("ManagerName")),
            IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    // =====================================================================
    // Dummy data fallbacks — used when database is unavailable
    // =====================================================================

    private static List<Expense> GetDummyExpenses() =>
    [
        new Expense
        {
            ExpenseId = 1,
            UserId = 1,
            UserName = "Alice Johnson",
            CategoryId = 1,
            CategoryName = "Travel",
            StatusId = 1,
            StatusName = "Draft",
            AmountMinor = 4500,
            Amount = 45.00m,
            Currency = "GBP",
            ExpenseDate = DateTime.UtcNow.AddDays(-3),
            Description = "Train to London (sample - database unavailable)",
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        },
        new Expense
        {
            ExpenseId = 2,
            UserId = 1,
            UserName = "Alice Johnson",
            CategoryId = 2,
            CategoryName = "Meals",
            StatusId = 2,
            StatusName = "Submitted",
            AmountMinor = 2800,
            Amount = 28.00m,
            Currency = "GBP",
            ExpenseDate = DateTime.UtcNow.AddDays(-5),
            Description = "Team lunch (sample - database unavailable)",
            SubmittedAt = DateTime.UtcNow.AddDays(-2),
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        },
        new Expense
        {
            ExpenseId = 3,
            UserId = 2,
            UserName = "Bob Smith",
            CategoryId = 3,
            CategoryName = "Accommodation",
            StatusId = 3,
            StatusName = "Approved",
            AmountMinor = 12000,
            Amount = 120.00m,
            Currency = "GBP",
            ExpenseDate = DateTime.UtcNow.AddDays(-10),
            Description = "Hotel - client visit (sample - database unavailable)",
            SubmittedAt = DateTime.UtcNow.AddDays(-8),
            ReviewerName = "Carol White",
            ReviewedAt = DateTime.UtcNow.AddDays(-7),
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        }
    ];

    private static List<ExpenseSummary> GetDummySummary() =>
    [
        new ExpenseSummary { StatusName = "Draft", Count = 1, TotalAmount = 45.00m },
        new ExpenseSummary { StatusName = "Submitted", Count = 1, TotalAmount = 28.00m },
        new ExpenseSummary { StatusName = "Approved", Count = 1, TotalAmount = 120.00m }
    ];

    private static List<ExpenseCategory> GetDummyCategories() =>
    [
        new ExpenseCategory { CategoryId = 1, CategoryName = "Travel", IsActive = true },
        new ExpenseCategory { CategoryId = 2, CategoryName = "Meals", IsActive = true },
        new ExpenseCategory { CategoryId = 3, CategoryName = "Accommodation", IsActive = true },
        new ExpenseCategory { CategoryId = 4, CategoryName = "Equipment", IsActive = true },
        new ExpenseCategory { CategoryId = 5, CategoryName = "Other", IsActive = true }
    ];

    private static List<ExpenseStatus> GetDummyStatuses() =>
    [
        new ExpenseStatus { StatusId = 1, StatusName = "Draft" },
        new ExpenseStatus { StatusId = 2, StatusName = "Submitted" },
        new ExpenseStatus { StatusId = 3, StatusName = "Approved" },
        new ExpenseStatus { StatusId = 4, StatusName = "Rejected" }
    ];

    private static List<User> GetDummyUsers() =>
    [
        new User { UserId = 1, UserName = "Alice Johnson", Email = "alice@example.com", RoleId = 1, RoleName = "Employee", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-30) },
        new User { UserId = 2, UserName = "Bob Smith", Email = "bob@example.com", RoleId = 1, RoleName = "Employee", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-30) },
        new User { UserId = 3, UserName = "Carol White", Email = "carol@example.com", RoleId = 2, RoleName = "Manager", IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(-60) }
    ];
}
