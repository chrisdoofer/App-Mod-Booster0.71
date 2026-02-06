using ExpenseManagement.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddControllers();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Expense Management API",
        Version = "v1",
        Description = "API for managing expenses with Azure SQL Database and Azure OpenAI integration"
    });
});

// Add Application Insights
builder.Services.AddApplicationInsightsTelemetry();

// Register services
builder.Services.AddScoped<ExpenseService>();
builder.Services.AddScoped<ChatService>();

// Add logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Always enable Swagger for Azure deployment debugging
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Expense Management API v1");
    options.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();
