using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using OrdersApi.Models;

namespace OrdersApi.Tests;

// Integration tests: start the real API in memory (with the in-memory database) and call it over HTTP.
// xUnit creates a new instance per test, so every test gets a fresh app and an empty database.
public class OrderEndpointsTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly WebApplicationFactory<Program> _factory = new();
    private readonly HttpClient _client;

    public OrderEndpointsTests() => _client = _factory.CreateClient();

    public void Dispose() => _factory.Dispose();

    private async Task<Order> CreateOrder(string product = "Laptop", int quantity = 2)
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest(product, quantity));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Order>(Json))!;
    }

    [Fact]
    public async Task Health_endpoints_return_ok()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Info_returns_service_name()
    {
        var info = await _client.GetFromJsonAsync<JsonElement>("/api/info");

        Assert.Equal("orders-api", info.GetProperty("service").GetString());
    }

    [Fact]
    public async Task Create_order_returns_201_and_pending_order()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest("Laptop", 2));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<Order>(Json);
        Assert.Equal("Laptop", order!.Product);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task Create_invalid_order_returns_400()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest("", 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_unknown_order_returns_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/orders/999")).StatusCode);
    }

    [Fact]
    public async Task Status_filter_returns_only_pending_orders()
    {
        var first = await CreateOrder("Laptop");
        await CreateOrder("Monitor");
        await _client.PostAsJsonAsync($"/api/orders/{first.Id}/complete", new CompleteOrderRequest("worker-1"));

        var pending = await _client.GetFromJsonAsync<List<Order>>("/api/orders?status=Pending", Json);

        Assert.Single(pending!);
        Assert.Equal("Monitor", pending![0].Product);
    }

    [Fact]
    public async Task Unknown_status_filter_returns_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/api/orders?status=Shipped")).StatusCode);
    }

    [Fact]
    public async Task Complete_order_marks_it_completed()
    {
        var order = await CreateOrder();

        var response = await _client.PostAsJsonAsync($"/api/orders/{order.Id}/complete", new CompleteOrderRequest("worker-1"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var completed = await response.Content.ReadFromJsonAsync<Order>(Json);
        Assert.Equal(OrderStatus.Completed, completed!.Status);
        Assert.Equal("worker-1", completed.ProcessedBy);
    }

    [Fact]
    public async Task Complete_order_twice_returns_409()
    {
        var order = await CreateOrder();
        await _client.PostAsJsonAsync($"/api/orders/{order.Id}/complete", new CompleteOrderRequest("worker-1"));

        var response = await _client.PostAsJsonAsync($"/api/orders/{order.Id}/complete", new CompleteOrderRequest("worker-2"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Claim_hands_out_the_oldest_pending_order_once()
    {
        var first = await CreateOrder("Laptop");
        var second = await CreateOrder("Monitor");

        var claimA = await (await _client.PostAsJsonAsync("/api/orders/claim", new CompleteOrderRequest("worker-a")))
            .Content.ReadFromJsonAsync<Order>(Json);
        var claimB = await (await _client.PostAsJsonAsync("/api/orders/claim", new CompleteOrderRequest("worker-b")))
            .Content.ReadFromJsonAsync<Order>(Json);

        Assert.Equal(first.Id, claimA!.Id);
        Assert.Equal(OrderStatus.Processing, claimA.Status);
        Assert.Equal("worker-a", claimA.ClaimedBy);
        Assert.Equal(second.Id, claimB!.Id);
    }

    [Fact]
    public async Task Claim_with_nothing_to_do_returns_204()
    {
        // Claim until the queue is empty (other tests may have left pending orders behind).
        HttpResponseMessage response;
        var claims = 0;
        do
        {
            response = await _client.PostAsJsonAsync("/api/orders/claim", new CompleteOrderRequest("worker-a"));
        } while (response.StatusCode == HttpStatusCode.OK && ++claims < 1000);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Concurrent_claims_never_hand_out_the_same_order_twice()
    {
        for (var i = 0; i < 10; i++)
            await CreateOrder($"Item {i}");

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            _client.PostAsJsonAsync("/api/orders/claim", new CompleteOrderRequest($"worker-{i}"))));
        var claimedIds = new List<int>();
        foreach (var response in responses.Where(r => r.StatusCode == HttpStatusCode.OK))
            claimedIds.Add((await response.Content.ReadFromJsonAsync<Order>(Json))!.Id);

        Assert.Equal(claimedIds.Count, claimedIds.Distinct().Count());
    }

    [Fact]
    public async Task Delete_order_removes_it()
    {
        var order = await CreateOrder();

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/orders/{order.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/orders/{order.Id}")).StatusCode);
    }
}
