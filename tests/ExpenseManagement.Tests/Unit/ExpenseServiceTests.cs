using ExpenseManagement.Services;
using ExpenseManagement.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ExpenseManagement.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ExpenseService"/>.
/// These tests verify that the service initialises correctly and falls back
/// to dummy data gracefully when the database is unavailable — no exceptions
/// should propagate to callers from the read-only data methods.
/// </summary>
public class ExpenseServiceTests
{
    private readonly Mock<ILogger<ExpenseService>> _mockLogger;
    private readonly IConfiguration _emptyConfiguration;

    public ExpenseServiceTests()
    {
        _mockLogger = new Mock<ILogger<ExpenseService>>();

        // Build an in-memory configuration with an empty/missing connection string
        // to exercise the dummy-data fallback path throughout all tests.
        _emptyConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ""
            })
            .Build();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Instantiation
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ExpenseService_Initializes_WithConfiguration()
    {
        // Arrange / Act
        var service = new ExpenseService(_emptyConfiguration, _mockLogger.Object);

        // Assert
        service.Should().NotBeNull("the service should be constructable with valid dependencies");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Graceful fallback — missing / empty connection string
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExpenseService_WithMissingConnectionString_FallsBackToDummyData()
    {
        // Arrange — configuration with no connection string at all
        var noConnConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var service = new ExpenseService(noConnConfig, _mockLogger.Object);

        // Act — should NOT throw; should return dummy data instead
        var result = await service.GetExpensesAsync();

        // Assert
        result.Should().NotBeNull("service must fall back to dummy data, not throw");
        result.Should().NotBeEmpty("the dummy data set contains at least one expense");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetExpensesAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetExpenses_ReturnsData_WithDummyData()
    {
        // Arrange
        var service = new ExpenseService(_emptyConfiguration, _mockLogger.Object);

        // Act
        var expenses = await service.GetExpensesAsync();

        // Assert
        expenses.Should().NotBeNull();
        expenses.Should().NotBeEmpty("dummy data always contains expenses");
        expenses.Should().AllSatisfy(e =>
        {
            e.ExpenseId.Should().BeGreaterThan(0);
            e.UserName.Should().NotBeNullOrWhiteSpace();
            e.Amount.Should().BeGreaterThan(0m);
        });
    }

    [Fact]
    public async Task GetExpenses_WithUserId_ReturnsDummyDataList()
    {
        // Arrange — even when filtering, fallback returns full dummy set
        var service = new ExpenseService(_emptyConfiguration, _mockLogger.Object);

        // Act
        var expenses = await service.GetExpensesAsync(userId: 1);

        // Assert
        expenses.Should().NotBeNull("service must not throw for filtered queries either");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetExpenseSummaryAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetExpenseSummary_ReturnsSummary_WithDummyData()
    {
        // Arrange
        var service = new ExpenseService(_emptyConfiguration, _mockLogger.Object);

        // Act
        var summary = await service.GetExpenseSummaryAsync();

        // Assert
        summary.Should().NotBeNull();
        summary.Should().NotBeEmpty("dummy data provides at least one status summary row");
        summary.Should().AllSatisfy(s =>
        {
            s.StatusName.Should().NotBeNullOrWhiteSpace();
            s.Count.Should().BeGreaterThan(0);
            s.TotalAmount.Should().BeGreaterThan(0m);
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetCategoriesAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCategories_ReturnsCategories_WithDummyData()
    {
        // Arrange
        var service = new ExpenseService(_emptyConfiguration, _mockLogger.Object);

        // Act
        var categories = await service.GetCategoriesAsync();

        // Assert
        categories.Should().NotBeNull();
        categories.Should().NotBeEmpty("dummy data contains standard expense categories");
        categories.Should().AllSatisfy(c =>
        {
            c.CategoryId.Should().BeGreaterThan(0);
            c.CategoryName.Should().NotBeNullOrWhiteSpace();
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetStatusesAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetStatuses_ReturnsStatuses_WithDummyData()
    {
        // Arrange
        var service = new ExpenseService(_emptyConfiguration, _mockLogger.Object);

        // Act
        var statuses = await service.GetStatusesAsync();

        // Assert
        statuses.Should().NotBeNull();
        statuses.Should().NotBeEmpty("dummy data contains the four standard statuses");
        statuses.Select(s => s.StatusName)
            .Should().Contain(["Draft", "Submitted", "Approved", "Rejected"],
                because: "dummy statuses must include all workflow states");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetUsersAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUsers_ReturnsUsers_WithDummyData()
    {
        // Arrange
        var service = new ExpenseService(_emptyConfiguration, _mockLogger.Object);

        // Act
        var users = await service.GetUsersAsync();

        // Assert
        users.Should().NotBeNull();
        users.Should().NotBeEmpty("dummy data contains at least one user");
        users.Should().AllSatisfy(u =>
        {
            u.UserId.Should().BeGreaterThan(0);
            u.UserName.Should().NotBeNullOrWhiteSpace();
            u.Email.Should().NotBeNullOrWhiteSpace();
        });
    }
}
