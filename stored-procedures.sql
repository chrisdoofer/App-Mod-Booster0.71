-- =============================================
-- Stored Procedures for Expense Management System
-- =============================================

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Get All Expenses
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[GetAllExpenses]
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountDecimal,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        reviewer.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users reviewer ON e.ReviewedBy = reviewer.UserId
    ORDER BY e.CreatedAt DESC;
END
GO

-- =============================================
-- Get Expense by ID
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[GetExpenseById]
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountDecimal,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        reviewer.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users reviewer ON e.ReviewedBy = reviewer.UserId
    WHERE e.ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- Get Expenses by Status
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[GetExpensesByStatus]
    @StatusName NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountDecimal,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        reviewer.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users reviewer ON e.ReviewedBy = reviewer.UserId
    WHERE s.StatusName = @StatusName
    ORDER BY e.CreatedAt DESC;
END
GO

-- =============================================
-- Get Expenses by User
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[GetExpensesByUser]
    @UserId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        e.ExpenseId,
        e.UserId,
        u.UserName,
        e.CategoryId,
        c.CategoryName,
        e.StatusId,
        s.StatusName,
        e.AmountMinor,
        CAST(e.AmountMinor / 100.0 AS DECIMAL(10,2)) AS AmountDecimal,
        e.Currency,
        e.ExpenseDate,
        e.Description,
        e.ReceiptFile,
        e.SubmittedAt,
        e.ReviewedBy,
        reviewer.UserName AS ReviewedByName,
        e.ReviewedAt,
        e.CreatedAt
    FROM dbo.Expenses e
    INNER JOIN dbo.Users u ON e.UserId = u.UserId
    INNER JOIN dbo.ExpenseCategories c ON e.CategoryId = c.CategoryId
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    LEFT JOIN dbo.Users reviewer ON e.ReviewedBy = reviewer.UserId
    WHERE e.UserId = @UserId
    ORDER BY e.CreatedAt DESC;
END
GO

-- =============================================
-- Create Expense
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[CreateExpense]
    @UserId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @Currency NVARCHAR(3),
    @ExpenseDate DATE,
    @Description NVARCHAR(1000),
    @ReceiptFile NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @StatusId INT;
    SELECT @StatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft';
    
    INSERT INTO dbo.Expenses (UserId, CategoryId, StatusId, AmountMinor, Currency, ExpenseDate, Description, ReceiptFile, CreatedAt)
    VALUES (@UserId, @CategoryId, @StatusId, @AmountMinor, @Currency, @ExpenseDate, @Description, @ReceiptFile, SYSUTCDATETIME());
    
    SELECT SCOPE_IDENTITY() AS ExpenseId;
END
GO

-- =============================================
-- Update Expense Status
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[UpdateExpenseStatus]
    @ExpenseId INT,
    @StatusName NVARCHAR(50),
    @ReviewedBy INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @StatusId INT;
    SELECT @StatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = @StatusName;
    
    IF @StatusId IS NULL
    BEGIN
        RAISERROR('Invalid status name', 16, 1);
        RETURN;
    END
    
    UPDATE dbo.Expenses
    SET 
        StatusId = @StatusId,
        ReviewedBy = @ReviewedBy,
        ReviewedAt = CASE WHEN @ReviewedBy IS NOT NULL THEN SYSUTCDATETIME() ELSE NULL END,
        SubmittedAt = CASE WHEN @StatusName = 'Submitted' AND SubmittedAt IS NULL THEN SYSUTCDATETIME() ELSE SubmittedAt END
    WHERE ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- Delete Expense
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[DeleteExpense]
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DELETE FROM dbo.Expenses
    WHERE ExpenseId = @ExpenseId;
END
GO

-- =============================================
-- Get All Categories
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[GetCategories]
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT CategoryId, CategoryName, IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END
GO

-- =============================================
-- Get All Users
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[GetUsers]
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
END
GO

-- =============================================
-- Get Expense Summary by Status
-- =============================================
CREATE OR ALTER PROCEDURE [dbo].[GetExpenseSummary]
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        s.StatusName,
        COUNT(*) AS ExpenseCount,
        CAST(SUM(e.AmountMinor) / 100.0 AS DECIMAL(10,2)) AS TotalAmount
    FROM dbo.Expenses e
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    GROUP BY s.StatusName, s.StatusId
    ORDER BY s.StatusId;
END
GO

PRINT 'Stored procedures created successfully';
