using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using OrdersApi.Data;
using OrdersApi.Endpoints;

// Container health check without curl/wget in the image:
//   HEALTHCHECK CMD ["dotnet", "OrdersApi.dll", "--healthcheck"]
// Calls the liveness endpoint of the running app and exits 0 (healthy) or 1 (unhealthy).
if (args.Contains("--healthcheck"))
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    try
    {
        var response = await http.GetAsync("http://localhost:8080/health/live");
        return response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (Exception)
    {
        return 1;
    }
}

var builder = WebApplication.CreateBuilder(args);

// PostgreSQL when a connection string is configured (Docker, Kubernetes),
// otherwise an in-memory database (quick local start and tests).
var connectionString = builder.Configuration.GetConnectionString("OrdersDb");
var useInMemory = string.IsNullOrWhiteSpace(connectionString);
builder.Services.AddDbContext<OrdersDbContext>(options =>
{
    if (useInMemory)
        options.UseInMemoryDatabase("orders");
    else
        options.UseNpgsql(connectionString);
});

// Serialize enums as text ("Pending") instead of numbers (0).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.Logger.LogInformation("Using {Database} database", useInMemory ? "in-memory" : "PostgreSQL");
await EnsureDatabaseCreatedAsync(app);

app.MapOrderEndpoints();
app.MapOpsEndpoints(app.Configuration);

await app.RunAsync();
return 0;

// Creates the schema if it does not exist (no migrations — keeps the demo simple).
// Retries because the database container may start after the API.
static async Task EnsureDatabaseCreatedAsync(WebApplication app)
{
    const int maxAttempts = 10;
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            await db.Database.EnsureCreatedAsync();
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            app.Logger.LogWarning("Database not reachable (attempt {Attempt}/{Max}): {Message}", attempt, maxAttempts, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

// Makes Program visible to the integration tests (WebApplicationFactory<Program>).
public partial class Program;
