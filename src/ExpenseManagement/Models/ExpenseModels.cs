namespace ExpenseManagement.Models;

/// <summary>
/// Represents an expense record from the database.
/// Column mappings:
///   - DB alias "AmountDecimal"  → Amount property
///   - DB alias "ReviewedByName" → ReviewerName property
/// </summary>
public class Expense
{
    public int ExpenseId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public int AmountMinor { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime ExpenseDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ReceiptFile { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Represents an expense category.
/// </summary>
public class ExpenseCategory
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

/// <summary>
/// Represents an expense approval status.
/// </summary>
public class ExpenseStatus
{
    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;
}

/// <summary>
/// Represents a user in the system.
/// </summary>
public class User
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public int? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Expense summary grouped by status.
/// GetExpenseSummary SP returns EXACTLY 3 columns: StatusName, ExpenseCount, TotalAmount.
/// TotalAmountMinor is calculated in C# (not from DB).
/// </summary>
public class ExpenseSummary
{
    public string StatusName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
    public int TotalAmountMinor => (int)(TotalAmount * 100);
}

/// <summary>
/// Request model for creating a new expense.
/// </summary>
public class CreateExpenseRequest
{
    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public int AmountMinor { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Request model for updating an existing expense.
/// All fields are optional — only non-null values are updated.
/// </summary>
public class UpdateExpenseRequest
{
    public int? CategoryId { get; set; }
    public int? AmountMinor { get; set; }
    public DateTime? ExpenseDate { get; set; }
    public string? Description { get; set; }
}
