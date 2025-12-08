/*
    stored-procedures.sql
    Stored procedures for the Expense Management System
    All application data access should go through these procedures
*/

SET NOCOUNT ON;
GO

-- =============================================
-- Get all expenses with related data
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetAllExpenses
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email as UserEmail,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS Amount,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        r.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users r ON e.ReviewedBy = r.UserId
    ORDER BY e.CreatedAt DESC;
END;
GO

-- =============================================
-- Get expense by ID
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpenseById
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email as UserEmail,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS Amount,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        r.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users r ON e.ReviewedBy = r.UserId
    WHERE e.ExpenseId = @ExpenseId;
END;
GO

-- =============================================
-- Get expenses by status
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpensesByStatus
    @StatusName NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email as UserEmail,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS Amount,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        r.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users r ON e.ReviewedBy = r.UserId
    WHERE s.StatusName = @StatusName
    ORDER BY e.CreatedAt DESC;
END;
GO

-- =============================================
-- Get expenses by user
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetExpensesByUser
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        u.Email as UserEmail,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS Amount,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        r.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users r ON e.ReviewedBy = r.UserId
    WHERE e.UserId = @UserId
    ORDER BY e.CreatedAt DESC;
END;
GO

-- =============================================
-- Create a new expense
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_CreateExpense
    @UserId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @Currency NVARCHAR(3) = 'GBP',
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500) = NULL,
    @ExpenseId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Default to Draft status
    DECLARE @StatusId INT = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft');
    
    INSERT INTO dbo.Expenses (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, ReceiptFile, CreatedAt)
    VALUES (@UserId, @CategoryId, @StatusId, @AmountMinor, @Currency, @ExpenseDate, @Description, @ReceiptFile, SYSUTCDATETIME());
    
    SET @ExpenseId = SCOPE_IDENTITY();
    
    SELECT @ExpenseId AS ExpenseId;
END;
GO

-- =============================================
-- Submit an expense for approval
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_SubmitExpense
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @SubmittedStatusId INT = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted');
    
    UPDATE dbo.Expenses
    SET StatusId = @SubmittedStatusId,
        SubmittedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
    
    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- =============================================
-- Approve an expense
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_ApproveExpense
    @ExpenseId INT,
    @ReviewerId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ApprovedStatusId INT = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved');
    
    UPDATE dbo.Expenses
    SET StatusId = @ApprovedStatusId,
        ReviewedBy = @ReviewerId,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
    
    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- =============================================
-- Reject an expense
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_RejectExpense
    @ExpenseId INT,
    @ReviewerId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @RejectedStatusId INT = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Rejected');
    
    UPDATE dbo.Expenses
    SET StatusId = @RejectedStatusId,
        ReviewedBy = @ReviewerId,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId;
    
    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- =============================================
-- Delete an expense (only if draft)
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_DeleteExpense
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @DraftStatusId INT = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft');
    
    DELETE FROM dbo.Expenses
    WHERE ExpenseId = @ExpenseId AND StatusId = @DraftStatusId;
    
    SELECT @@ROWCOUNT AS RowsAffected;
END;
GO

-- =============================================
-- Get all expense categories
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetCategories
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT CategoryId, CategoryName, IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END;
GO

-- =============================================
-- Get all users
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetUsers
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        u.UserId,
        u.UserName,
        u.Email,
        u.RoleId,
        r.RoleName,
        u.ManagerId,
        m.UserName AS ManagerName,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    LEFT JOIN dbo.Users m ON u.ManagerId = m.UserId
    WHERE u.IsActive = 1
    ORDER BY u.UserName;
END;
GO

-- =============================================
-- Get dashboard statistics
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetDashboardStats
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        (SELECT COUNT(*) FROM dbo.Expenses) AS TotalExpenses,
        (SELECT COUNT(*) FROM dbo.Expenses e INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId WHERE s.StatusName = 'Submitted') AS PendingApprovals,
        (SELECT ISNULL(SUM(AmountMinor), 0) FROM dbo.Expenses e INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId WHERE s.StatusName = 'Approved') AS ApprovedAmountMinor,
        (SELECT COUNT(*) FROM dbo.Expenses e INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId WHERE s.StatusName = 'Approved') AS ApprovedCount;
END;
GO

-- =============================================
-- Get expense statuses
-- =============================================
CREATE OR ALTER PROCEDURE dbo.usp_GetStatuses
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT StatusId, StatusName
    FROM dbo.ExpenseStatus
    ORDER BY StatusId;
END;
GO

PRINT 'All stored procedures created successfully.';
GO
