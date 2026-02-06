/*
  stored-procedures.sql
  All stored procedures for the Expense Management System
  - Uses CREATE OR ALTER for idempotency
  - Uses GO batch separators between procedures
  - Column aliases match C# model property names exactly
  - Monetary amounts: INT in storage, DECIMAL(10,2) in output
  Generated: 2025-11-04
*/

SET NOCOUNT ON;
GO

-- ============================================================
-- Procedure: usp_GetExpenses
-- Description: Retrieves a list of expenses with optional filters
-- Parameters: @UserId (optional), @StatusId (optional)
-- Returns: List of expenses with calculated decimal amounts
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_GetExpenses]
    @UserId INT = NULL,
    @StatusId INT = NULL
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
    WHERE (@UserId IS NULL OR e.UserId = @UserId)
      AND (@StatusId IS NULL OR e.StatusId = @StatusId)
    ORDER BY e.CreatedAt DESC;
END
GO

-- ============================================================
-- Procedure: usp_GetExpenseById
-- Description: Retrieves a single expense by ID
-- Parameters: @ExpenseId
-- Returns: Single expense record with calculated decimal amount
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_GetExpenseById]
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

-- ============================================================
-- Procedure: usp_CreateExpense
-- Description: Creates a new expense record
-- Parameters: Expense details (AmountMinor in minor units)
-- Returns: ExpenseId of created record
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_CreateExpense]
    @UserId INT,
    @CategoryId INT,
    @AmountMinor INT,
    @Currency NVARCHAR(3) = 'GBP',
    @ExpenseDate DATE,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @StatusId INT;
    SELECT @StatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft';
    
    INSERT INTO dbo.Expenses (
        UserId, 
        CategoryId, 
        StatusId, 
        AmountMinor, 
        Currency, 
        ExpenseDate, 
        Description, 
        ReceiptFile,
        CreatedAt
    )
    VALUES (
        @UserId,
        @CategoryId,
        @StatusId,
        @AmountMinor,
        @Currency,
        @ExpenseDate,
        @Description,
        @ReceiptFile,
        SYSUTCDATETIME()
    );
    
    SELECT SCOPE_IDENTITY() AS ExpenseId;
END
GO

-- ============================================================
-- Procedure: usp_UpdateExpense
-- Description: Updates an existing expense record
-- Parameters: ExpenseId and fields to update
-- Returns: Number of rows affected
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_UpdateExpense]
    @ExpenseId INT,
    @CategoryId INT = NULL,
    @AmountMinor INT = NULL,
    @ExpenseDate DATE = NULL,
    @Description NVARCHAR(1000) = NULL,
    @ReceiptFile NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    UPDATE dbo.Expenses
    SET 
        CategoryId = ISNULL(@CategoryId, CategoryId),
        AmountMinor = ISNULL(@AmountMinor, AmountMinor),
        ExpenseDate = ISNULL(@ExpenseDate, ExpenseDate),
        Description = ISNULL(@Description, Description),
        ReceiptFile = ISNULL(@ReceiptFile, ReceiptFile)
    WHERE ExpenseId = @ExpenseId
      AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft');
    
    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- ============================================================
-- Procedure: usp_DeleteExpense
-- Description: Deletes a draft expense record
-- Parameters: @ExpenseId
-- Returns: Number of rows affected
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_DeleteExpense]
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DELETE FROM dbo.Expenses
    WHERE ExpenseId = @ExpenseId
      AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft');
    
    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- ============================================================
-- Procedure: usp_SubmitExpense
-- Description: Submits an expense for approval
-- Parameters: @ExpenseId
-- Returns: Success indicator
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_SubmitExpense]
    @ExpenseId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @SubmittedStatusId INT;
    SELECT @SubmittedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted';
    
    UPDATE dbo.Expenses
    SET 
        StatusId = @SubmittedStatusId,
        SubmittedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId
      AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Draft');
    
    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- ============================================================
-- Procedure: usp_ApproveExpense
-- Description: Approves a submitted expense
-- Parameters: @ExpenseId, @ReviewerId
-- Returns: Success indicator
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_ApproveExpense]
    @ExpenseId INT,
    @ReviewerId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ApprovedStatusId INT;
    SELECT @ApprovedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Approved';
    
    UPDATE dbo.Expenses
    SET 
        StatusId = @ApprovedStatusId,
        ReviewedBy = @ReviewerId,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId
      AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted');
    
    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- ============================================================
-- Procedure: usp_RejectExpense
-- Description: Rejects a submitted expense
-- Parameters: @ExpenseId, @ReviewerId
-- Returns: Success indicator
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_RejectExpense]
    @ExpenseId INT,
    @ReviewerId INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @RejectedStatusId INT;
    SELECT @RejectedStatusId = StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Rejected';
    
    UPDATE dbo.Expenses
    SET 
        StatusId = @RejectedStatusId,
        ReviewedBy = @ReviewerId,
        ReviewedAt = SYSUTCDATETIME()
    WHERE ExpenseId = @ExpenseId
      AND StatusId = (SELECT StatusId FROM dbo.ExpenseStatus WHERE StatusName = 'Submitted');
    
    SELECT @@ROWCOUNT AS RowsAffected;
END
GO

-- ============================================================
-- Procedure: usp_GetExpenseSummary
-- Description: Returns summary statistics grouped by status
-- Parameters: @UserId (optional)
-- Returns: EXACTLY 3 columns: StatusName, ExpenseCount, TotalAmount
-- Note: Returns DECIMAL for TotalAmount (calculated from INT minor units)
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_GetExpenseSummary]
    @UserId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        s.StatusName,
        COUNT(*) AS ExpenseCount,
        CAST(SUM(e.AmountMinor) / 100.0 AS DECIMAL(10,2)) AS TotalAmount
    FROM dbo.Expenses e
    INNER JOIN dbo.ExpenseStatus s ON e.StatusId = s.StatusId
    WHERE (@UserId IS NULL OR e.UserId = @UserId)
    GROUP BY s.StatusName
    ORDER BY s.StatusName;
END
GO

-- ============================================================
-- Procedure: usp_GetCategories
-- Description: Retrieves all active expense categories
-- Parameters: None
-- Returns: List of active categories
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_GetCategories]
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        CategoryId,
        CategoryName,
        IsActive
    FROM dbo.ExpenseCategories
    WHERE IsActive = 1
    ORDER BY CategoryName;
END
GO

-- ============================================================
-- Procedure: usp_GetStatuses
-- Description: Retrieves all expense statuses
-- Parameters: None
-- Returns: List of all statuses
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_GetStatuses]
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        StatusId,
        StatusName
    FROM dbo.ExpenseStatus
    ORDER BY StatusId;
END
GO

-- ============================================================
-- Procedure: usp_GetUsers
-- Description: Retrieves all active users
-- Parameters: None
-- Returns: List of active users with their roles
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_GetUsers]
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
        manager.UserName AS ManagerName,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    LEFT JOIN dbo.Users manager ON u.ManagerId = manager.UserId
    WHERE u.IsActive = 1
    ORDER BY u.UserName;
END
GO

-- ============================================================
-- Procedure: usp_GetUserById
-- Description: Retrieves a single user by ID
-- Parameters: @UserId
-- Returns: Single user record with role information
-- ============================================================
CREATE OR ALTER PROCEDURE [dbo].[usp_GetUserById]
    @UserId INT
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
        manager.UserName AS ManagerName,
        u.IsActive,
        u.CreatedAt
    FROM dbo.Users u
    INNER JOIN dbo.Roles r ON u.RoleId = r.RoleId
    LEFT JOIN dbo.Users manager ON u.ManagerId = manager.UserId
    WHERE u.UserId = @UserId;
END
GO

-- ============================================================
-- End of stored procedures
-- ============================================================
