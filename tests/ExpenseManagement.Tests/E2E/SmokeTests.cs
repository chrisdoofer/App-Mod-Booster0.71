using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace ExpenseManagement.Tests.E2E;

/// <summary>
/// Post-deployment smoke tests that run against the live Azure App Service URL.
///
/// These tests read the deployment context written by <c>deploy-infra/deploy.ps1</c>
/// into <c>.deployment-context.json</c> at the repository root. If the context
/// file is not found (i.e. the app has not yet been deployed), ALL tests in this
/// class skip gracefully — they are not treated as failures.
/// </summary>
public class SmokeTests : IAsyncLifetime
{
    private static readonly string[] ContextSearchPaths =
    [
        Path.Combine(Directory.GetCurrentDirectory(), ".deployment-context.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", ".deployment-context.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".deployment-context.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".deployment-context.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".deployment-context.json"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "..", ".deployment-context.json")
    ];

    private bool _hasDeploymentContext;
    private string _appUrl = string.Empty;
    private HttpClient _httpClient = new();

    public Task InitializeAsync()
    {
        (_hasDeploymentContext, _appUrl) = LoadDeploymentContext();
        if (_hasDeploymentContext && !string.IsNullOrWhiteSpace(_appUrl))
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_appUrl.TrimEnd('/')),
                Timeout     = TimeSpan.FromSeconds(30)
            };
        }
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _httpClient.Dispose();
        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Live smoke tests — skipped if no deployment context
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LiveApp_IndexPage_Returns200()
    {
        if (!_hasDeploymentContext)
        {
            // No deployment context → skip gracefully
            return;
        }

        var response = await _httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"the live app index page at {_appUrl} must return HTTP 200");
    }

    [Fact]
    public async Task LiveApp_HealthEndpoint_Returns200()
    {
        if (!_hasDeploymentContext)
        {
            return;
        }

        var response = await _httpClient.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"the live app /health endpoint at {_appUrl} must always return HTTP 200");
    }

    [Fact]
    public async Task LiveApp_SwaggerEndpoint_Returns200()
    {
        if (!_hasDeploymentContext)
        {
            return;
        }

        var response = await _httpClient.GetAsync("/swagger/index.html");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"the Swagger UI at {_appUrl}/swagger/index.html must be accessible");
    }

    [Fact]
    public async Task LiveApp_ExpensesApi_Returns200()
    {
        if (!_hasDeploymentContext)
        {
            return;
        }

        var response = await _httpClient.GetAsync("/api/expenses");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"the live /api/expenses endpoint at {_appUrl} must return HTTP 200");

        var json = await response.Content.ReadAsStringAsync();
        json.Should().NotBeNullOrWhiteSpace("the expenses endpoint must return a non-empty JSON body");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static (bool Found, string AppUrl) LoadDeploymentContext()
    {
        foreach (var candidate in ContextSearchPaths)
        {
            var normalised = Path.GetFullPath(candidate);
            if (!File.Exists(normalised)) continue;

            try
            {
                var json    = File.ReadAllText(normalised);
                using var doc = JsonDocument.Parse(json);
                var root    = doc.RootElement;

                // The context file may use either "appServiceUrl" or "AppServiceUrl"
                string? appUrl = null;
                if (root.TryGetProperty("appServiceUrl", out var urlProp))
                    appUrl = urlProp.GetString();
                else if (root.TryGetProperty("AppServiceUrl", out var urlPropAlt))
                    appUrl = urlPropAlt.GetString();

                if (!string.IsNullOrWhiteSpace(appUrl))
                    return (true, appUrl);
            }
            catch (JsonException)
            {
                // Malformed context file — treat as not found
            }
        }

        return (false, string.Empty);
    }
}
