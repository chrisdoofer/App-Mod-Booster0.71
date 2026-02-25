using System.Net;
using System.Net.Http.Json;
using ExpenseManagement.Tests.Helpers;
using FluentAssertions;

namespace ExpenseManagement.Tests.Integration;

/// <summary>
/// Integration tests for all REST API endpoints exposed by <c>ExpensesController</c>.
/// Uses <see cref="ExpenseManagementWebApplicationFactory"/> to host the full ASP.NET Core
/// pipeline in-process. No real Azure SQL or OpenAI connection is required — the service
/// layer falls back to dummy data automatically.
/// </summary>
public class ApiEndpointTests : IClassFixture<ExpenseManagementWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiEndpointTests(ExpenseManagementWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Expense data endpoints
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetExpenses_ReturnsOkWithJsonContent_WhenCalled()
    {
        // Act
        var response = await _client.GetAsync("/api/expenses");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the expenses endpoint must return 200 and fall back to dummy data when DB is absent");
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json",
            "the API must always return JSON");

        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotBeNullOrWhiteSpace();
        json.Should().StartWith("[", "expenses response should be a JSON array");
    }

    [Fact]
    public async Task GetExpenseSummary_ReturnsOkWithJsonContent_WhenCalled()
    {
        // Act
        var response = await _client.GetAsync("/api/expenses/summary");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the summary endpoint must return 200 with dummy data");
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotBeNullOrWhiteSpace();
        json.Should().StartWith("[", "summary response should be a JSON array");
    }

    [Fact]
    public async Task GetCategories_ReturnsOkWithJsonContent_WhenCalled()
    {
        // Act
        var response = await _client.GetAsync("/api/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotBeNullOrWhiteSpace();
        json.Should().StartWith("[", "categories response should be a JSON array");
    }

    [Fact]
    public async Task GetStatuses_ReturnsOkWithJsonContent_WhenCalled()
    {
        // Act
        var response = await _client.GetAsync("/api/statuses");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotBeNullOrWhiteSpace();
        json.Should().StartWith("[", "statuses response should be a JSON array");
    }

    [Fact]
    public async Task GetUsers_ReturnsOkWithJsonContent_WhenCalled()
    {
        // Act
        var response = await _client.GetAsync("/api/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");

        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotBeNullOrWhiteSpace();
        json.Should().StartWith("[", "users response should be a JSON array");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Health and infrastructure endpoints
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HealthEndpoint_Returns200_Always()
    {
        // The health endpoint must NEVER depend on the database.
        // App Service uses it for health probes — it must always return 200.
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the /health endpoint must return 200 regardless of database availability");
    }

    [Fact]
    public async Task SwaggerEndpoint_Returns200_WhenCalled()
    {
        // Swagger UI should be available in all environments
        var response = await _client.GetAsync("/swagger/index.html");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Swagger UI must be accessible in development/test environments");
    }

    [Fact]
    public async Task SwaggerJson_Returns200_WhenCalled()
    {
        // Swagger JSON spec must be accessible for tooling and documentation
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the OpenAPI spec must be served at /swagger/v1/swagger.json");
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Razor Pages
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IndexPage_Returns200_WhenCalled()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the Index page must load without a database connection");
    }

    [Fact]
    public async Task ChatPage_ShowsNotConfiguredMessage_WhenGenAINotDeployed()
    {
        // Act — factory sets empty GenAI endpoint, so the chat page should
        // render the "not configured" message rather than the chat UI.
        var response = await _client.GetAsync("/Chat");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the Chat page must always render a 200 even when GenAI is not deployed");

        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotBeNullOrWhiteSpace();
        // The page should contain some indication that AI is not yet configured
        html.Should().ContainAny(
            ["not available", "not configured", "redeploy", "-DeployGenAI"],
            because: "the Chat page must inform users when AI chat has not been deployed");
    }
}
