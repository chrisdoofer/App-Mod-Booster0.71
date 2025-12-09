using Microsoft.Data.SqlClient;
using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<List<Expense>> GetAllExpensesAsync();
    Task<Expense?> GetExpenseByIdAsync(int expenseId);
    Task<List<Expense>> GetExpensesByStatusAsync(string statusName);
    Task<List<Expense>> GetExpensesByUserAsync(int userId);
    Task<List<Expense>> GetPendingApprovalsAsync();
    Task<int> CreateExpenseAsync(CreateExpenseRequest request);
    Task<bool> UpdateExpenseAsync(int expenseId, UpdateExpenseRequest request);
    Task<bool> SubmitExpenseAsync(int expenseId);
    Task<bool> ApproveExpenseAsync(int expenseId, int reviewerId);
    Task<bool> RejectExpenseAsync(int expenseId, int reviewerId);
    Task<bool> DeleteExpenseAsync(int expenseId);
    Task<List<Category>> GetCategoriesAsync();
    Task<List<User>> GetUsersAsync();
    Task<DashboardStats> GetDashboardStatsAsync();
    bool IsConnected { get; }
    string? LastError { get; }
}

public class ExpenseService : IExpenseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExpenseService> _logger;
    private string? _lastError;
    private bool _isConnected = true;

    public bool IsConnected => _isConnected;
    public string? LastError => _lastError;

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private string GetConnectionString()
    {
        return _configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    public async Task<List<Expense>> GetAllExpensesAsync()
    {
        var expenses = new List<Expense>();

        try
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                _isConnected = false;
                _lastError = "Connection string is not configured. Please configure ConnectionStrings:DefaultConnection.";
                return GetDummyExpenses();
            }

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            _isConnected = true;

            using var command = new SqlCommand("GetAllExpenses", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }
        }
        catch (Exception ex)
        {
            _isConnected = false;
            _lastError = FormatError(ex);
            _logger.LogError(ex, "Error getting all expenses");
            return GetDummyExpenses();
        }

        return expenses;
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                return GetDummyExpenses().FirstOrDefault(e => e.ExpenseId == expenseId);
            }

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("GetExpenseById", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpenseFromReader(reader);
            }
        }
        catch (Exception ex)
        {
            _lastError = FormatError(ex);
            _logger.LogError(ex, "Error getting expense {ExpenseId}", expenseId);
        }

        return null;
    }

    public async Task<List<Expense>> GetExpensesByStatusAsync(string statusName)
    {
        var expenses = new List<Expense>();

        try
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                return GetDummyExpenses().Where(e => e.StatusName == statusName).ToList();
            }

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("GetExpensesByStatus", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@StatusName", statusName);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }
        }
        catch (Exception ex)
        {
            _lastError = FormatError(ex);
            _logger.LogError(ex, "Error getting expenses by status {Status}", statusName);
            return GetDummyExpenses().Where(e => e.StatusName == statusName).ToList();
        }

        return expenses;
    }

    public async Task<List<Expense>> GetExpensesByUserAsync(int userId)
    {
        var expenses = new List<Expense>();

        try
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                return GetDummyExpenses().Where(e => e.UserId == userId).ToList();
            }

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("GetExpensesByUser", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UserId", userId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }
        }
        catch (Exception ex)
        {
            _lastError = FormatError(ex);
            _logger.LogError(ex, "Error getting expenses by user {UserId}", userId);
        }

        return expenses;
    }

    public async Task<List<Expense>> GetPendingApprovalsAsync()
    {
        return await GetExpensesByStatusAsync("Submitted");
    }

    public async Task<int> CreateExpenseAsync(CreateExpenseRequest request)
    {
        try
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                _lastError = "Database not configured";
                return -1;
            }

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("CreateExpense", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(request.Amount * 100));
            command.Parameters.AddWithValue("@Currency", request.Currency);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", request.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@StatusId", request.Submit ? 2 : 1); // 2 = Submitted, 1 = Draft

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            _lastError = FormatError(ex);
            _logger.LogError(ex, "Error creating expense");
            return -1;
        }
    }

    public async Task<bool> UpdateExpenseAsync(int expenseId, UpdateExpenseRequest request)
    {
        try
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                _lastError = "Database not configured";
                return false;
            }

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("UpdateExpense", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", (int)(request.Amount * 100));
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate);
            command.Parameters.AddWithValue("@Description", request.Description ?? (object)DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            _lastError = FormatError(ex);
            _logger.LogError(ex, "Error updating expense {ExpenseId}", expenseId);
            return false;
        }
    }

    public async Task<bool> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                _lastError = "Database not configured";
                return false;
            }

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("SubmitExpense", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            _lastError = FormatError(ex);
            _logger.LogError(ex, "Error submitting expense {ExpenseId}", expenseId);
            return false;
        }
    }

    public async Task<bool> ApproveExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                _lastError = "Database not configured";
                return false;
            }

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("ApproveExpense", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewedBy", reviewerId);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            _lastError = FormatError(ex);
            _logger.LogError(ex, "Error approving expense {ExpenseId}", expenseId);
            return false;
        }
    }

    public async Task<bool> RejectExpenseAsync(int expenseId, int reviewerId)
    {
        try
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                _lastError = "Database not configured";
                return false;
            }

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("RejectExpense", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewedBy", reviewerId);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            _lastError = FormatError(ex);
            _logger.LogError(ex, "Error rejecting expense {ExpenseId}", expenseId);
            return false;
        }
    }

    public async Task<bool> DeleteExpenseAsync(int expenseId)
    {
        try
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                _lastError = "Database not configured";
                return false;
            }

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("DeleteExpense", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            _lastError = FormatError(ex);
            _logger.LogError(ex, "Error deleting expense {ExpenseId}", expenseId);
            return false;
        }
    }

    public async Task<List<Category>> GetCategoriesAsync()
    {
        var categories = new List<Category>();

        try
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                return GetDummyCategories();
            }

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("GetAllCategories", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new Category
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }
        }
        catch (Exception ex)
        {
            _lastError = FormatError(ex);
            _logger.LogError(ex, "Error getting categories");
            return GetDummyCategories();
        }

        return categories;
    }

    public async Task<List<User>> GetUsersAsync()
    {
        var users = new List<User>();

        try
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                return GetDummyUsers();
            }

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("GetAllUsers", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    UserName = reader.GetString(reader.GetOrdinal("UserName")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive"))
                });
            }
        }
        catch (Exception ex)
        {
            _lastError = FormatError(ex);
            _logger.LogError(ex, "Error getting users");
            return GetDummyUsers();
        }

        return users;
    }

    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        var stats = new DashboardStats();

        try
        {
            var connectionString = GetConnectionString();
            if (string.IsNullOrEmpty(connectionString))
            {
                return GetDummyStats();
            }

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            using var command = new SqlCommand("GetExpenseSummary", connection);
            command.CommandType = System.Data.CommandType.StoredProcedure;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var statusName = reader.GetString(reader.GetOrdinal("StatusName"));
                var count = reader.GetInt32(reader.GetOrdinal("ExpenseCount"));
                var totalAmount = reader.GetDecimal(reader.GetOrdinal("TotalAmount"));

                stats.TotalExpenses += count;

                switch (statusName)
                {
                    case "Submitted":
                        stats.PendingApprovals = count;
                        break;
                    case "Approved":
                        stats.ApprovedAmount = totalAmount;
                        stats.ApprovedCount = count;
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            _lastError = FormatError(ex);
            _logger.LogError(ex, "Error getting dashboard stats");
            return GetDummyStats();
        }

        return stats;
    }

    private Expense MapExpenseFromReader(SqlDataReader reader)
    {
        return new Expense
        {
            ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
            UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
            EmployeeName = reader.GetString(reader.GetOrdinal("EmployeeName")),
            EmployeeEmail = reader.GetString(reader.GetOrdinal("EmployeeEmail")),
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

    private string FormatError(Exception ex)
    {
        var message = ex.Message;
        
        // Provide helpful context for common managed identity errors
        if (message.Contains("Unable to load the proper Managed Identity"))
        {
            return $"Managed Identity Error: {message}. Ensure AZURE_CLIENT_ID environment variable is set and the connection string includes 'User Id={{client-id}}'.";
        }
        
        if (message.Contains("Login failed"))
        {
            return $"Database Login Failed: {message}. Ensure the managed identity database user has been created with proper permissions.";
        }

        return message;
    }

    // Dummy data for when database is unavailable
    private List<Expense> GetDummyExpenses()
    {
        return new List<Expense>
        {
            new Expense
            {
                ExpenseId = 1,
                UserId = 1,
                EmployeeName = "Alice Example",
                EmployeeEmail = "alice@example.co.uk",
                CategoryId = 1,
                CategoryName = "Travel",
                StatusId = 2,
                StatusName = "Submitted",
                AmountMinor = 2540,
                Amount = 25.40m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-5),
                Description = "Taxi from airport to client site",
                CreatedAt = DateTime.Now.AddDays(-5)
            },
            new Expense
            {
                ExpenseId = 2,
                UserId = 1,
                EmployeeName = "Alice Example",
                EmployeeEmail = "alice@example.co.uk",
                CategoryId = 2,
                CategoryName = "Meals",
                StatusId = 3,
                StatusName = "Approved",
                AmountMinor = 1425,
                Amount = 14.25m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-10),
                Description = "Client lunch meeting",
                CreatedAt = DateTime.Now.AddDays(-10)
            },
            new Expense
            {
                ExpenseId = 3,
                UserId = 1,
                EmployeeName = "Alice Example",
                EmployeeEmail = "alice@example.co.uk",
                CategoryId = 3,
                CategoryName = "Supplies",
                StatusId = 1,
                StatusName = "Draft",
                AmountMinor = 799,
                Amount = 7.99m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-1),
                Description = "Office stationery",
                CreatedAt = DateTime.Now.AddDays(-1)
            },
            new Expense
            {
                ExpenseId = 4,
                UserId = 1,
                EmployeeName = "Alice Example",
                EmployeeEmail = "alice@example.co.uk",
                CategoryId = 4,
                CategoryName = "Accommodation",
                StatusId = 3,
                StatusName = "Approved",
                AmountMinor = 12300,
                Amount = 123.00m,
                Currency = "GBP",
                ExpenseDate = DateTime.Now.AddDays(-30),
                Description = "Hotel during client visit",
                ReviewerName = "Bob Manager",
                CreatedAt = DateTime.Now.AddDays(-30)
            }
        };
    }

    private List<Category> GetDummyCategories()
    {
        return new List<Category>
        {
            new Category { CategoryId = 1, CategoryName = "Travel", IsActive = true },
            new Category { CategoryId = 2, CategoryName = "Meals", IsActive = true },
            new Category { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
            new Category { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
            new Category { CategoryId = 5, CategoryName = "Other", IsActive = true }
        };
    }

    private List<User> GetDummyUsers()
    {
        return new List<User>
        {
            new User { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleName = "Employee", IsActive = true },
            new User { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleName = "Manager", IsActive = true }
        };
    }

    private DashboardStats GetDummyStats()
    {
        return new DashboardStats
        {
            TotalExpenses = 4,
            PendingApprovals = 1,
            ApprovedAmount = 137.25m,
            ApprovedCount = 2
        };
    }
}
