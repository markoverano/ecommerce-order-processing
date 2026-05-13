using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace E2ETests;

/// <summary>WireMock stub that intercepts Stripe API calls routed via Stripe__BaseUrl configuration.</summary>
public sealed class StripeStubServer : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public string BaseUrl => _server.Url!;

    public void SetupChargeSuccess(string chargeId = "ch_test_123")
    {
        _server.Reset();
        _server
            .Given(Request.Create().WithPath("/v1/charges").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = chargeId,
                    @object = "charge",
                    status = "succeeded",
                    amount = 10000,
                    currency = "usd",
                    failure_message = (string?)null
                }));

        _server
            .Given(Request.Create().WithPath("/v1/refunds").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    id = "re_test_123",
                    @object = "refund",
                    status = "succeeded",
                    amount = 10000
                }));
    }

    public void SetupChargeDeclined()
    {
        _server.Reset();
        _server
            .Given(Request.Create().WithPath("/v1/charges").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(402)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    error = new
                    {
                        type = "card_error",
                        code = "card_declined",
                        message = "Your card was declined."
                    }
                }));
    }

    public void SetupChargeServerError(int count = 10)
    {
        _server.Reset();
        _server
            .Given(Request.Create().WithPath("/v1/charges").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { error = new { type = "api_error", message = "Internal server error" } }));
    }

    public void Dispose() => _server.Dispose();
}

/// <summary>WireMock stub that intercepts FedEx API calls routed via FedEx__BaseUrl configuration.</summary>
public sealed class FedExStubServer : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public string BaseUrl => _server.Url!;

    public void SetupShipmentSuccess(string trackingNumber = "1Z999AA1234567890")
    {
        _server.Reset();
        _server
            .Given(Request.Create().WithPath("/v1/ship/create").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { trackingNumber }));

        _server
            .Given(Request.Create().WithPath("/*").UsingDelete())
            .RespondWith(Response.Create().WithStatusCode(200));
    }

    public void SetupShipmentFailure()
    {
        _server.Reset();
        _server
            .Given(Request.Create().WithPath("/v1/ship/create").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { error = "Service unavailable" }));
    }

    public void Dispose() => _server.Dispose();
}

/// <summary>WireMock stub that intercepts Mailgun API calls routed via Mailgun__BaseUrl configuration.</summary>
public sealed class MailgunStubServer : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public string BaseUrl => _server.Url! + "/";

    public void SetupEmailSuccess()
    {
        _server.Reset();
        _server
            .Given(Request.Create().WithPath("/messages").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new { id = "<msg-test-123@mailgun.org>", message = "Queued. Thank you." }));
    }

    public void Dispose() => _server.Dispose();
}
