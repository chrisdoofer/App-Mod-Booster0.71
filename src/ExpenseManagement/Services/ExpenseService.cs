using ExpenseManagement.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<(List<Expense> Expenses, ErrorInfo? Error)> GetAllExpensesAsync();
    Task<(Expense? Expense, ErrorInfo? Error)> GetExpenseByIdAsync(int expenseId);
    Task<(List<Expense> Expenses, ErrorInfo? Error)> GetExpensesByStatusAsync(string status);
    Task<(List<Expense> Expenses, ErrorInfo? Error)> GetExpensesByUserAsync(int userId);
    Task<(int ExpenseId, ErrorInfo? Error)> CreateExpenseAsync(CreateExpenseRequest request);
    Task<(bool Success, ErrorInfo? Error)> SubmitExpenseAsync(int expenseId);
    Task<(bool Success, ErrorInfo? Error)> ApproveExpenseAsync(int expenseId, int reviewerId);
    Task<(bool Success, ErrorInfo? Error)> RejectExpenseAsync(int expenseId, int reviewerId);
    Task<(bool Success, ErrorInfo? Error)> DeleteExpenseAsync(int expenseId);
    Task<(List<Category> Categories, ErrorInfo? Error)> GetCategoriesAsync();
    Task<(List<User> Users, ErrorInfo? Error)> GetUsersAsync();
    Task<(DashboardStats? Stats, ErrorInfo? Error)> GetDashboardStatsAsync();
    Task<(List<ExpenseStatus> Statuses, ErrorInfo? Error)> GetStatusesAsync();
}

public class ExpenseService : IExpenseService
{
    private readonly DatabaseConfig _dbConfig;
    private readonly ILogger<ExpenseService> _logger;
    private readonly IConfiguration _configuration;

    public ExpenseService(DatabaseConfig dbConfig, ILogger<ExpenseService> logger, IConfiguration configuration)
    {
        _dbConfig = dbConfig;
        _logger = logger;
        _configuration = configuration;
    }

    private SqlConnection CreateConnection()
    {
        return new SqlConnection(_dbConfig.ConnectionString);
    }

    private ErrorInfo CreateErrorInfo(Exception ex, string operation)
    {
        var error = new ErrorInfo
        {
            Message = $"Error during {operation}",
            Details = ex.Message,
            Location = $"{nameof(ExpenseService)}.{operation}"
        };

        // Add specific guidance for common issues
        if (ex.Message.Contains("Unable to load the proper Managed Identity") ||
            ex.Message.Contains("AZURE_CLIENT_ID"))
        {
            error.Guidance = "The AZURE_CLIENT_ID environment variable is not set or the User Id is missing from the connection string. Ensure the App Service has the managed identity client ID configured.";
        }
        else if (ex.Message.Contains("Login failed"))
        {
            error.Guidance = "The managed identity database user may not have been created or does not have proper permissions. Run the infrastructure deployment script to configure database access.";
        }
        else if (ex.Message.Contains("connection string"))
        {
            error.Guidance = "The database connection string is not configured. Ensure ConnectionStrings__DefaultConnection is set in App Service configuration.";
        }

        _logger.LogError(ex, "Error in {Operation}: {Message}", operation, ex.Message);
        return error;
    }

    public async Task<(List<Expense> Expenses, ErrorInfo? Error)> GetAllExpensesAsync()
    {
        try
        {
            var expenses = new List<Expense>();
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.usp_GetAllExpenses", connection)
            {
                CommandType = CommandType.StoredProcedure
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
            return (GetDummyExpenses(), CreateErrorInfo(ex, nameof(GetAllExpensesAsync)));
        }
    }

    public async Task<(Expense? Expense, ErrorInfo? Error)> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.usp_GetExpenseById", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (MapExpense(reader), null);
            }

            return (null, null);
        }
        catch (Exception ex)
        {
            return (null, CreateErrorInfo(ex, nameof(GetExpenseByIdAsync)));
        }
    }

    public async Task<(List<Expense> Expenses, ErrorInfo? Error)> GetExpensesByStatusAsync(string status)
    {
        try
        {
            var expenses = new List<Expense>();
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.usp_GetExpensesByStatus", connection)
            {
                CommandType = CommandType.StoredProcedure
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
            return (GetDummyExpenses().Where(e => e.StatusName == status).ToList(), CreateErrorInfo(ex, nameof(GetExpensesByStatusAsync)));
        }
    }

    public async Task<(List<Expense> Expenses, ErrorInfo? Error)> GetExpensesByUserAsync(int userId)
    {
        try
        {
            var expenses = new List<Expense>();
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.usp_GetExpensesByUser", connection)
            {
                CommandType = CommandType.StoredProcedure
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
            return (GetDummyExpenses().Where(e => e.UserId == userId).ToList(), CreateErrorInfo(ex, nameof(GetExpensesByUserAsync)));
        }
    }

    public async Task<(int ExpenseId, ErrorInfo? Error)> CreateExpenseAsync(CreateExpenseRequest request)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.usp_CreateExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(request.Amount * 100));
            command.Parameters.AddWithValue("@Currency", request.Currency);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);
            
            var outputParam = new SqlParameter("@ExpenseId", SqlDbType.Int) { Direction = ParameterDirection.Output };
            command.Parameters.Add(outputParam);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt32(0), null);
            }

            return ((int)outputParam.Value, null);
        }
        catch (Exception ex)
        {
            return (-1, CreateErrorInfo(ex, nameof(CreateExpenseAsync)));
        }
    }

    public async Task<(bool Success, ErrorInfo? Error)> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.usp_SubmitExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt32(0) > 0, null);
            }

            return (false, null);
        }
        catch (Exception ex)
        {
            return (false, CreateErrorInfo(ex, nameof(SubmitExpenseAsync)));
        }
    }

    public async Task<(bool Success, ErrorInfo? Error)> ApproveExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.usp_ApproveExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt32(0) > 0, null);
            }

            return (false, null);
        }
        catch (Exception ex)
        {
            return (false, CreateErrorInfo(ex, nameof(ApproveExpenseAsync)));
        }
    }

    public async Task<(bool Success, ErrorInfo? Error)> RejectExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.usp_RejectExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewerId", reviewerId);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt32(0) > 0, null);
            }

            return (false, null);
        }
        catch (Exception ex)
        {
            return (false, CreateErrorInfo(ex, nameof(RejectExpenseAsync)));
        }
    }

    public async Task<(bool Success, ErrorInfo? Error)> DeleteExpenseAsync(int expenseId)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.usp_DeleteExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (reader.GetInt32(0) > 0, null);
            }

            return (false, null);
        }
        catch (Exception ex)
        {
            return (false, CreateErrorInfo(ex, nameof(DeleteExpenseAsync)));
        }
    }

    public async Task<(List<Category> Categories, ErrorInfo? Error)> GetCategoriesAsync()
    {
        try
        {
            var categories = new List<Category>();
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.usp_GetCategories", connection)
            {
                CommandType = CommandType.StoredProcedure
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
            return (GetDummyCategories(), CreateErrorInfo(ex, nameof(GetCategoriesAsync)));
        }
    }

    public async Task<(List<User> Users, ErrorInfo? Error)> GetUsersAsync()
    {
        try
        {
            var users = new List<User>();
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.usp_GetUsers", connection)
            {
                CommandType = CommandType.StoredProcedure
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
                    ManagerName = reader.IsDBNull(reader.GetOrdinal("ManagerName")) ? null : reader.GetString(reader.GetOrdinal("ManagerName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
                });
            }

            return (users, null);
        }
        catch (Exception ex)
        {
            return (GetDummyUsers(), CreateErrorInfo(ex, nameof(GetUsersAsync)));
        }
    }

    public async Task<(DashboardStats? Stats, ErrorInfo? Error)> GetDashboardStatsAsync()
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.usp_GetDashboardStats", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return (new DashboardStats
                {
                    TotalExpenses = reader.GetInt32(reader.GetOrdinal("TotalExpenses")),
                    PendingApprovals = reader.GetInt32(reader.GetOrdinal("PendingApprovals")),
                    ApprovedAmountMinor = reader.GetInt32(reader.GetOrdinal("ApprovedAmountMinor")),
                    ApprovedCount = reader.GetInt32(reader.GetOrdinal("ApprovedCount"))
                }, null);
            }

            return (null, null);
        }
        catch (Exception ex)
        {
            return (GetDummyStats(), CreateErrorInfo(ex, nameof(GetDashboardStatsAsync)));
        }
    }

    public async Task<(List<ExpenseStatus> Statuses, ErrorInfo? Error)> GetStatusesAsync()
    {
        try
        {
            var statuses = new List<ExpenseStatus>();
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("dbo.usp_GetStatuses", connection)
            {
                CommandType = CommandType.StoredProcedure
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
            return (GetDummyStatuses(), CreateErrorInfo(ex, nameof(GetStatusesAsync)));
        }
    }

    private static Expense MapExpense(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            UserName = reader.GetString(reader.GetOrdinal("UserName")),
            UserEmail = reader.GetString(reader.GetOrdinal("UserEmail")),
            CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
            CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
            StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
            StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
            AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
            Amount = reader.GetDecimal(reader.GetOrdinal("Amount")),
            Currency = reader.GetString(reader.GetOrdinal("Currency")),
            ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
            Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
            ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
            SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
            ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
            ReviewedByName = reader.IsDBNull(reader.GetOrdinal("ReviewedByName")) ? null : reader.GetString(reader.GetOrdinal("ReviewedByName")),
            ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt"))
        };
    }

    // Dummy data for fallback when database is not available
    private static List<Expense> GetDummyExpenses()
    {
        return new List<Expense>
        {
            new() { ExpenseId = 1, UserId = 1, UserName = "Alice Example", CategoryId = 1, CategoryName = "Travel", StatusId = 2, StatusName = "Submitted", AmountMinor = 2540, Amount = 25.40m, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-5), Description = "Taxi to client site", CreatedAt = DateTime.Now.AddDays(-5) },
            new() { ExpenseId = 2, UserId = 1, UserName = "Alice Example", CategoryId = 2, CategoryName = "Meals", StatusId = 3, StatusName = "Approved", AmountMinor = 1425, Amount = 14.25m, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-10), Description = "Client lunch meeting", CreatedAt = DateTime.Now.AddDays(-10) },
            new() { ExpenseId = 3, UserId = 1, UserName = "Alice Example", CategoryId = 3, CategoryName = "Supplies", StatusId = 1, StatusName = "Draft", AmountMinor = 799, Amount = 7.99m, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-2), Description = "Office stationery", CreatedAt = DateTime.Now.AddDays(-2) },
            new() { ExpenseId = 4, UserId = 1, UserName = "Alice Example", CategoryId = 4, CategoryName = "Accommodation", StatusId = 3, StatusName = "Approved", AmountMinor = 12300, Amount = 123.00m, Currency = "GBP", ExpenseDate = DateTime.Now.AddDays(-20), Description = "Hotel during client visit", CreatedAt = DateTime.Now.AddDays(-20) }
        };
    }

    private static List<Category> GetDummyCategories()
    {
        return new List<Category>
        {
            new() { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new() { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new() { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new() { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new() { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };
    }

    private static List<User> GetDummyUsers()
    {
        return new List<User>
        {
            new() { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleId = 1, RoleName = "Employee", IsActive = true, CreatedAt = DateTime.Now.AddMonths(-6) },
            new() { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true, CreatedAt = DateTime.Now.AddMonths(-12) }
        };
    }

    private static DashboardStats GetDummyStats()
    {
        return new DashboardStats
        {
            TotalExpenses = 4,
            PendingApprovals = 1,
            ApprovedAmountMinor = 13725,
            ApprovedCount = 2
        };
    }

    private static List<ExpenseStatus> GetDummyStatuses()
    {
        return new List<ExpenseStatus>
        {
            new() { StatusId = 1, StatusName = "Draft" },
            new() { StatusId = 2, StatusName = "Submitted" },
            new() { StatusId = 3, StatusName = "Approved" },
            new() { StatusId = 4, StatusName = "Rejected" }
        };
    }
}
