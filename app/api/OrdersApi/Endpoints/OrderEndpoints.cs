using Microsoft.EntityFrameworkCore;
using OrdersApi.Data;
using OrdersApi.Models;

namespace OrdersApi.Endpoints;

public static class OrderEndpoints
{
    // A claimed order whose worker disappeared (crash, kill -9) is handed out again after this time.
    private static readonly TimeSpan ClaimTimeout = TimeSpan.FromMinutes(2);

    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/orders");

        // GET /api/orders            -> all orders, newest first
        // GET /api/orders?status=Pending -> only pending orders (used by the worker)
        orders.MapGet("/", async (string? status, OrdersDbContext db) =>
        {
            var query = db.Orders.AsQueryable();
            if (status is not null)
            {
                if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
                    return Results.BadRequest(new { error = $"Unknown status '{status}'." });
                query = query.Where(o => o.Status == parsed);
            }
            return Results.Ok(await query.OrderByDescending(o => o.Id).ToListAsync());
        });

        orders.MapGet("/{id:int}", async (int id, OrdersDbContext db) =>
            await db.Orders.FindAsync(id) is { } order ? Results.Ok(order) : Results.NotFound());

        orders.MapPost("/", async (CreateOrderRequest request, OrdersDbContext db) =>
        {
            var errors = request.Validate();
            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var order = new Order { Product = request.Product.Trim(), Quantity = request.Quantity };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            return Results.Created($"/api/orders/{order.Id}", order);
        });

        // Called by the worker: hands out the oldest claimable order, to exactly one worker.
        // 200 + order, or 204 when there is nothing to do.
        orders.MapPost("/claim", async (CompleteOrderRequest request, OrdersDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(request.ProcessedBy))
                return Results.BadRequest(new { error = "processedBy is required." });

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var now = DateTime.UtcNow;
                var staleBefore = now - ClaimTimeout;
                var order = await db.Orders
                    .Where(o => o.Status == OrderStatus.Pending
                             || (o.Status == OrderStatus.Processing && o.ClaimedAt < staleBefore))
                    .OrderBy(o => o.Id)
                    .FirstOrDefaultAsync();
                if (order is null)
                    return Results.NoContent();

                order.Claim(request.ProcessedBy, now);
                try
                {
                    await db.SaveChangesAsync();
                    return Results.Ok(order);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Another worker claimed it a moment earlier — try the next one.
                    db.ChangeTracker.Clear();
                }
            }
            return Results.NoContent();
        });

        // Called by the worker when it has processed an order.
        orders.MapPost("/{id:int}/complete", async (int id, CompleteOrderRequest request, OrdersDbContext db) =>
        {
            var order = await db.Orders.FindAsync(id);
            if (order is null)
                return Results.NotFound();

            try
            {
                order.Complete(request.ProcessedBy, DateTime.UtcNow);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }

            await db.SaveChangesAsync();
            return Results.Ok(order);
        });

        orders.MapDelete("/{id:int}", async (int id, OrdersDbContext db) =>
        {
            var order = await db.Orders.FindAsync(id);
            if (order is null)
                return Results.NotFound();

            db.Orders.Remove(order);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    public static void MapOpsEndpoints(this IEndpointRouteBuilder app, IConfiguration config)
    {
        // Liveness: the process is running. Used by Docker HEALTHCHECK / Kubernetes livenessProbe.
        app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));

        // Readiness: the app can reach its database. Used by Kubernetes readinessProbe.
        app.MapGet("/health/ready", async (OrdersDbContext db) =>
            await db.Database.CanConnectAsync()
                ? Results.Ok(new { status = "ready" })
                : Results.Json(new { status = "database unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable));

        // Shows which version and which container/pod answered — handy for rolling update and scaling demos.
        app.MapGet("/api/info", () => Results.Ok(new
        {
            service = "orders-api",
            version = config["APP_VERSION"] ?? "dev",
            hostname = Environment.MachineName,
            environment = config["ENVIRONMENT"] ?? "local",
        }));
    }
}
