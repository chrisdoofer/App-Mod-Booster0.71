namespace ExpenseManagement.Models;

/// <summary>
/// Represents an expense record
/// </summary>
public class Expense
{
    public int ExpenseId { get; set; }
    public int UserId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeEmail { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int AmountMinor { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
    public string? ReceiptFile { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? ReviewedBy { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Formatted amount with currency symbol
    /// </summary>
    public string FormattedAmount => $"£{Amount:N2}";

    /// <summary>
    /// CSS class for status badge
    /// </summary>
    public string StatusBadgeClass => StatusName switch
    {
        "Approved" => "badge-approved",
        "Submitted" => "badge-submitted",
        "Draft" => "badge-draft",
        "Rejected" => "badge-rejected",
        _ => "badge-default"
    };
}

/// <summary>
/// Model for creating a new expense
/// </summary>
public class CreateExpenseRequest
{
    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
    public bool Submit { get; set; } = false;
}

/// <summary>
/// Model for updating an expense
/// </summary>
public class UpdateExpenseRequest
{
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Represents an expense category
/// </summary>
public class Category
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>
/// Represents a user
/// </summary>
public class User
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>
/// Expense summary statistics
/// </summary>
public class ExpenseSummary
{
    public string StatusName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }

    public int TotalAmountMinor => (int)(TotalAmount * 100);
}

/// <summary>
/// Dashboard statistics
/// </summary>
public class DashboardStats
{
    public int TotalExpenses { get; set; }
    public int PendingApprovals { get; set; }
    public decimal ApprovedAmount { get; set; }
    public int ApprovedCount { get; set; }
    public string FormattedApprovedAmount => $"£{ApprovedAmount:N2}";
}

/// <summary>
/// API response wrapper
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message
    };

    public static ApiResponse<T> Error(string message) => new()
    {
        Success = false,
        Message = message
    };
}
