using ExpenseManagement.Models;
using Microsoft.Data.SqlClient;
using System.Data;

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

    private SqlConnection GetConnection()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        return new SqlConnection(connectionString);
    }

    public async Task<List<Expense>> GetAllExpensesAsync()
    {
        var expenses = new List<Expense>();
        
        try
        {
            using var connection = GetConnection();
            using var command = new SqlCommand("GetAllExpenses", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            
            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all expenses");
            throw;
        }
        
        return expenses;
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            using var connection = GetConnection();
            using var command = new SqlCommand("GetExpenseById", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            
            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            
            if (await reader.ReadAsync())
            {
                return MapExpenseFromReader(reader);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expense {ExpenseId}", expenseId);
            throw;
        }
        
        return null;
    }

    public async Task<List<Expense>> GetExpensesByStatusAsync(string statusName)
    {
        var expenses = new List<Expense>();
        
        try
        {
            using var connection = GetConnection();
            using var command = new SqlCommand("GetExpensesByStatus", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@StatusName", statusName);
            
            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses by status {StatusName}", statusName);
            throw;
        }
        
        return expenses;
    }

    public async Task<List<Expense>> GetExpensesByUserAsync(int userId)
    {
        var expenses = new List<Expense>();
        
        try
        {
            using var connection = GetConnection();
            using var command = new SqlCommand("GetExpensesByUser", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@UserId", userId);
            
            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpenseFromReader(reader));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving expenses for user {UserId}", userId);
            throw;
        }
        
        return expenses;
    }

    public async Task<int> CreateExpenseAsync(CreateExpenseRequest request)
    {
        try
        {
            using var connection = GetConnection();
            using var command = new SqlCommand("CreateExpense", connection)
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
            
            await connection.OpenAsync();
            var result = await command.ExecuteScalarAsync();
            
            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating expense");
            throw;
        }
    }

    public async Task UpdateExpenseStatusAsync(int expenseId, string statusName, int? reviewedBy = null)
    {
        try
        {
            using var connection = GetConnection();
            using var command = new SqlCommand("UpdateExpenseStatus", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@StatusName", statusName);
            command.Parameters.AddWithValue("@ReviewedBy", (object?)reviewedBy ?? DBNull.Value);
            
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating expense status");
            throw;
        }
    }

    public async Task DeleteExpenseAsync(int expenseId)
    {
        try
        {
            using var connection = GetConnection();
            using var command = new SqlCommand("DeleteExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            
            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting expense {ExpenseId}", expenseId);
            throw;
        }
    }

    public async Task<List<Category>> GetCategoriesAsync()
    {
        var categories = new List<Category>();
        
        try
        {
            using var connection = GetConnection();
            using var command = new SqlCommand("GetCategories", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            
            await connection.OpenAsync();
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
            _logger.LogError(ex, "Error retrieving categories");
            throw;
        }
        
        return categories;
    }

    public async Task<List<User>> GetUsersAsync()
    {
        var users = new List<User>();
        
        try
        {
            using var connection = GetConnection();
            using var command = new SqlCommand("GetUsers", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            
            await connection.OpenAsync();
            using var reader = await command.ExecuteReaderAsync();
            
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            throw;
        }
        
        return users;
    }

    public async Task<List<ExpenseSummary>> GetExpenseSummaryAsync()
    {
        var summary = new List<ExpenseSummary>();
        
        try
        {
            using var connection = GetConnection();
            using var command = new SqlCommand("GetExpenseSummary", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            
            await connection.OpenAsync();
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
            _logger.LogError(ex, "Error retrieving expense summary");
            throw;
        }
        
        return summary;
    }

    private Expense MapExpenseFromReader(SqlDataReader reader)
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
