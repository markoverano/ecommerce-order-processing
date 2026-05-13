using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace E2ETests;

/// <summary>
/// Verifies the full order processing happy path: order created → payment processed →
/// stock reserved → shipment created → customer notified → order confirmed.
/// All external calls (Stripe, FedEx, Mailgun) are intercepted by WireMock stubs.
/// </summary>
[Collection("E2E")]
public sealed class HappyPathTests
{
    private readonly ServiceFixture _fixture;
    private static readonly TimeSpan SagaTimeout = TimeSpan.FromSeconds(30);

    public HappyPathTests(ServiceFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task CreateOrder_FullSagaFlow_OrderConfirmed()
    {
        // Arrange
        _fixture.Stripe.SetupChargeSuccess("ch_happy_123");
        _fixture.FedEx.SetupShipmentSuccess("TRACK-HAPPY-001");
        _fixture.Mailgun.SetupEmailSuccess();

        var request = new
        {
            customerId = Guid.NewGuid().ToString(),
            shippingAddress = new
            {
                line1 = "123 Main St",
                city = "Springfield",
                state = "IL",
                postalCode = "62701",
                countryCode = "US"
            },
            items = new[]
            {
                new { productId = ServiceFixture.StockProductId, productName = "Test Widget", quantity = 2, unitPrice = 25.00m, currency = "USD" }
            },
            paymentMethodId = "pm_test_visa"
        };

        // Act
        var createResponse = await _fixture.OrderClient.PostAsJsonAsync("/api/v1/orders", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OrderCreatedResponse>();
        Assert.NotNull(created?.OrderId);

        // Assert: poll until the saga has driven the order to Confirmed
        await WaitHelper.WaitForConditionAsync(
            async () =>
            {
                var resp = await _fixture.OrderClient.GetAsync($"/api/v1/orders/{created.OrderId}");
                if (!resp.IsSuccessStatusCode) return false;
                var order = await resp.Content.ReadFromJsonAsync<OrderStatusResponse>();
                return order?.Status == "Confirmed";
            },
            SagaTimeout,
            description: $"order {created.OrderId} to reach Confirmed");
    }

    [Fact]
    public async Task CreateOrder_FullSagaFlow_SagaEndpointReflectsCompleted()
    {
        // Arrange
        _fixture.Stripe.SetupChargeSuccess();
        _fixture.FedEx.SetupShipmentSuccess();
        _fixture.Mailgun.SetupEmailSuccess();

        var request = new
        {
            customerId = Guid.NewGuid().ToString(),
            shippingAddress = new { line1 = "456 Oak Ave", city = "Chicago", state = "IL", postalCode = "60601", countryCode = "US" },
            items = new[] { new { productId = ServiceFixture.StockProductId, productName = "Test Widget", quantity = 1, unitPrice = 10.00m, currency = "USD" } },
            paymentMethodId = "pm_test_visa"
        };

        var createResponse = await _fixture.OrderClient.PostAsJsonAsync("/api/v1/orders", request);
        var created = await createResponse.Content.ReadFromJsonAsync<OrderCreatedResponse>();
        Assert.NotNull(created?.OrderId);

        // Wait for order to be confirmed
        await WaitHelper.WaitForConditionAsync(
            async () =>
            {
                var resp = await _fixture.OrderClient.GetAsync($"/api/v1/orders/{created.OrderId}");
                if (!resp.IsSuccessStatusCode) return false;
                var order = await resp.Content.ReadFromJsonAsync<OrderStatusResponse>();
                return order?.Status == "Confirmed";
            },
            SagaTimeout,
            description: $"order {created.OrderId} to reach Confirmed");

        // Verify saga monitoring endpoint reports Completed
        var sagaResp = await _fixture.SagaClient.GetAsync($"/api/v1/sagas/{created.OrderId}");
        Assert.Equal(HttpStatusCode.OK, sagaResp.StatusCode);

        var saga = await sagaResp.Content.ReadFromJsonAsync<SagaStatusResponse>();
        Assert.Equal("Completed", saga?.Status);
    }
}

// ── Minimal DTO shapes used for JSON deserialization in assertions ──────────
file sealed record OrderCreatedResponse(string? OrderId);
file sealed record OrderStatusResponse(string? Status);
file sealed record SagaStatusResponse(string? Status);
