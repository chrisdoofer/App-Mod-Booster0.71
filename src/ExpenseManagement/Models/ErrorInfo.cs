namespace ExpenseManagement.Models;

public class ErrorInfo
{
    public string Message { get; set; } = "";
    public string? Details { get; set; }
    public string? Location { get; set; }
    public string? Guidance { get; set; }
}
