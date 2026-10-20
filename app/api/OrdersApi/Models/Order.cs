using System.Text.Json.Serialization;

namespace OrdersApi.Models;

public enum OrderStatus
{
    Pending,
    Processing,
    Completed
}

public class Order
{
    public int Id { get; set; }
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }

    // Name of the worker (container / pod hostname) that processed the order.
    public string? ProcessedBy { get; set; }

    // Set when a worker claims the order, so no other worker picks it up.
    public string? ClaimedBy { get; set; }
    public DateTime? ClaimedAt { get; set; }

    // Concurrency token: if two workers try to claim the same order at the same moment,
    // only the first save succeeds; the second gets a concurrency error and tries the next order.
    [JsonIgnore]
    public Guid Version { get; set; } = Guid.NewGuid();

    public void Claim(string workerName, DateTime now)
    {
        if (Status == OrderStatus.Completed)
            throw new InvalidOperationException($"Order {Id} is already completed.");

        if (string.IsNullOrWhiteSpace(workerName))
            throw new ArgumentException("processedBy is required.", nameof(workerName));

        Status = OrderStatus.Processing;
        ClaimedBy = workerName;
        ClaimedAt = now;
        Version = Guid.NewGuid();
    }

    public void Complete(string processedBy, DateTime processedAt)
    {
        if (Status == OrderStatus.Completed)
            throw new InvalidOperationException($"Order {Id} is already completed.");

        if (string.IsNullOrWhiteSpace(processedBy))
            throw new ArgumentException("processedBy is required.", nameof(processedBy));

        Status = OrderStatus.Completed;
        ProcessedBy = processedBy;
        ProcessedAt = processedAt;
        Version = Guid.NewGuid();
    }
}

public record CreateOrderRequest(string Product, int Quantity)
{
    // Returns validation errors per field; an empty result means the request is valid.
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(Product))
            errors[nameof(Product)] = ["Product is required."];
        else if (Product.Length > 100)
            errors[nameof(Product)] = ["Product must be 100 characters or less."];
        if (Quantity < 1 || Quantity > 1000)
            errors[nameof(Quantity)] = ["Quantity must be between 1 and 1000."];
        return errors;
    }
}

public record CompleteOrderRequest(string ProcessedBy);
