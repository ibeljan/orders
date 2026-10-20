using OrdersApi.Models;

namespace OrdersApi.Tests;

// Unit tests: plain C# objects, no web server, no database.
public class OrderTests
{
    [Fact]
    public void New_order_is_pending()
    {
        var order = new Order { Product = "Laptop", Quantity = 1 };

        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Null(order.ProcessedBy);
    }

    [Fact]
    public void Complete_sets_status_and_worker_name()
    {
        var order = new Order { Product = "Laptop", Quantity = 1 };
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        order.Complete("worker-1", now);

        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal("worker-1", order.ProcessedBy);
        Assert.Equal(now, order.ProcessedAt);
    }

    [Fact]
    public void Complete_twice_throws()
    {
        var order = new Order { Product = "Laptop", Quantity = 1 };
        order.Complete("worker-1", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => order.Complete("worker-2", DateTime.UtcNow));
    }

    [Fact]
    public void Complete_without_worker_name_throws()
    {
        var order = new Order { Product = "Laptop", Quantity = 1 };

        Assert.Throws<ArgumentException>(() => order.Complete("", DateTime.UtcNow));
    }

    [Theory]
    [InlineData("Laptop", 1)]
    [InlineData("Monitor", 1000)]
    public void Valid_request_has_no_errors(string product, int quantity)
    {
        Assert.Empty(new CreateOrderRequest(product, quantity).Validate());
    }

    [Theory]
    [InlineData("", 1, "Product")]
    [InlineData("   ", 1, "Product")]
    [InlineData("Laptop", 0, "Quantity")]
    [InlineData("Laptop", 1001, "Quantity")]
    public void Invalid_request_reports_field(string product, int quantity, string field)
    {
        var errors = new CreateOrderRequest(product, quantity).Validate();

        Assert.True(errors.ContainsKey(field));
    }

    [Fact]
    public void Claim_marks_order_processing_and_changes_version()
    {
        var order = new Order { Product = "Laptop", Quantity = 1 };
        var version = order.Version;

        order.Claim("worker-1", DateTime.UtcNow);

        Assert.Equal(OrderStatus.Processing, order.Status);
        Assert.Equal("worker-1", order.ClaimedBy);
        Assert.NotEqual(version, order.Version);
    }

    [Fact]
    public void Claiming_a_completed_order_throws()
    {
        var order = new Order { Product = "Laptop", Quantity = 1 };
        order.Complete("worker-1", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => order.Claim("worker-2", DateTime.UtcNow));
    }
}
