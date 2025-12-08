namespace ExpenseManagement.Models;

public class Expense
{
    public int ExpenseId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string UserEmail { get; set; } = "";
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public int StatusId { get; set; }
    public string StatusName { get; set; } = "";
    public int AmountMinor { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
    public string? ReceiptFile { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int? ReviewedBy { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    
    public string FormattedAmount => $"£{Amount:N2}";
    public string FormattedDate => ExpenseDate.ToString("dd MMM yyyy");
}

public class CreateExpenseRequest
{
    public int UserId { get; set; }
    public int CategoryId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "GBP";
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
    public string? ReceiptFile { get; set; }
}

public class Category
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public bool IsActive { get; set; }
}

public class User
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public int RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public int? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExpenseStatus
{
    public int StatusId { get; set; }
    public string StatusName { get; set; } = "";
}

public class DashboardStats
{
    public int TotalExpenses { get; set; }
    public int PendingApprovals { get; set; }
    public int ApprovedAmountMinor { get; set; }
    public int ApprovedCount { get; set; }
    
    public decimal ApprovedAmount => ApprovedAmountMinor / 100m;
    public string FormattedApprovedAmount => $"£{ApprovedAmount:N2}";
}
