using ExpenseManagement.Services;

var builder = WebApplication.CreateBuilder(args);

// =====================================================================
// Services
// =====================================================================

// Razor Pages + Controllers
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Application services
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<ChatService>();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Expense Management API",
        Version = "v1",
        Description = "REST API for the Expense Management System"
    });
});

// Health checks — no database dependency (per best practice: endpoint must return 200 immediately)
builder.Services.AddHealthChecks();

// Application Insights (telemetry)
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

// =====================================================================
// Middleware pipeline
// =====================================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Swagger — available in all environments so deployed app shows API docs
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Expense Management API v1");
    options.RoutePrefix = "swagger";
});

app.UseRouting();
app.UseAuthorization();

// Health check endpoint — must NOT depend on database (App Service uses this for health probes)
app.MapHealthChecks("/health");

app.MapRazorPages();
app.MapControllers();

app.Run();

// Make Program class accessible to integration tests (WebApplicationFactory)
public partial class Program { }
