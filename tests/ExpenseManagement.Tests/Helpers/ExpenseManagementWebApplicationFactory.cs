using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ExpenseManagement.Tests.Helpers;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Configures the application with in-memory settings so that no real Azure resources
/// are required. Empty connection string triggers the dummy-data fallback path.
/// </summary>
public class ExpenseManagementWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Override with test-specific configuration.
            // Empty connection string → ExpenseService catches InvalidOperationException
            // and falls back to dummy data, keeping all endpoints functional.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Empty connection string triggers graceful dummy-data fallback
                ["ConnectionStrings:DefaultConnection"] = "",

                // No GenAI endpoint → ChatService.IsConfigured returns false
                ["GenAISettings:OpenAIEndpoint"]   = "",
                ["GenAISettings:OpenAIModelName"]  = "",

                // No managed identity needed for tests
                ["ManagedIdentityClientId"] = "",

                // Suppress Application Insights telemetry in tests
                ["ApplicationInsights:ConnectionString"] = ""
            });
        });

        // Run in Development so Swagger middleware is active
        builder.UseEnvironment("Development");
    }
}
