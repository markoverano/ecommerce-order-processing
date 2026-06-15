using System.Net;
using System.Net.Http.Json;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using OrderService.Integration.Tests.Infrastructure;
using Xunit;

namespace OrderService.Integration.Tests.Orders;

[Collection("OrderService")]
public sealed class CreateOrderTests : IClassFixture<OrderServiceFixture>
{
    private readonly HttpClient _client;

    public CreateOrderTests(OrderServiceFixture fixture)
    {
        _client = fixture.CreateClient();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {TestTokenFactory.CreateAdminToken()}");
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_Returns201WithOrderId()
    {
        var request = new
        {
            items = new[]
            {
                new
                {
                    productId = Guid.NewGuid(),
                    quantity = 2,
                    unitPrice = new { amount = 49.99m, currency = "USD" }
                }
            },
            shippingAddress = new
            {
                line1 = "123 Main Street",
                city = "Springfield",
                state = "IL",
                postalCode = "62701",
                countryCode = "US"
            }
        };

        var idempotencyKey = Guid.NewGuid().ToString();
        _client.DefaultRequestHeaders.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);

        var response = await _client.PostAsJsonAsync("/api/v1/orders", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ServiceResponse<OrderId>>();
        Assert.NotNull(body);
        Assert.True(body.IsSuccess);
        Assert.NotEqual(Guid.Empty, body.Data.Value);
    }

    [Fact]
    public async Task CreateOrder_EmptyItems_Returns400ValidationFailed()
    {
        var request = new
        {
            items = Array.Empty<object>(),
            shippingAddress = new
            {
                line1 = "123 Main Street",
                city = "Springfield",
                state = "IL",
                postalCode = "62701",
                countryCode = "US"
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/orders", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ServiceResponse<OrderId>>();
        Assert.False(body?.IsSuccess);
        Assert.Equal("VALIDATION_FAILED", body?.Error?.Code);
    }

    [Fact]
    public async Task CreateOrder_IdempotentRequest_ReturnsSameOrderId()
    {
        var request = new
        {
            items = new[]
            {
                new { productId = Guid.NewGuid(), quantity = 1, unitPrice = new { amount = 25.00m, currency = "USD" } }
            },
            shippingAddress = new
            {
                line1 = "456 Oak Ave",
                city = "Chicago",
                state = "IL",
                postalCode = "60601",
                countryCode = "US"
            }
        };

        var idempotencyKey = Guid.NewGuid().ToString();
        _client.DefaultRequestHeaders.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);

        var first = await _client.PostAsJsonAsync("/api/v1/orders", request);
        var second = await _client.PostAsJsonAsync("/api/v1/orders", request);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var firstBody = await first.Content.ReadFromJsonAsync<ServiceResponse<OrderId>>();
        var secondBody = await second.Content.ReadFromJsonAsync<ServiceResponse<OrderId>>();

        Assert.NotNull(firstBody);
        Assert.NotNull(secondBody);
        Assert.Equal(firstBody.Data.Value, secondBody.Data.Value);
        Assert.Equal("true", second.Headers.GetValues("X-Idempotency-Replayed").FirstOrDefault());
    }
}
