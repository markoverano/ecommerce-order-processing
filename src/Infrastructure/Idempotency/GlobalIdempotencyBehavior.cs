using System.Text.Json;
using ECommerceOrderProcessing.Shared.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ECommerceOrderProcessing.Infrastructure.Idempotency;

/// <summary>
/// MediatR pipeline behavior that short-circuits duplicate command delivery.
/// Only activates for commands implementing IIdempotentCommand; all others pass through unchanged.
/// The 24-hour TTL matches the typical client retry window for transient failures.
/// </summary>
public sealed class GlobalIdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IGlobalIdempotencyStore _store;
    private readonly ILogger<GlobalIdempotencyBehavior<TRequest, TResponse>> _logger;

    public GlobalIdempotencyBehavior(
        IGlobalIdempotencyStore store,
        ILogger<GlobalIdempotencyBehavior<TRequest, TResponse>> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IIdempotentCommand idempotentCommand)
            return await next();

        var key = $"idempotency:{typeof(TRequest).Name}:{idempotentCommand.GetIdempotencyKey()}";

        var cached = await _store.TryGetAsync(key, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug(
                "Idempotency cache hit for {CommandType} key={Key}; returning cached response",
                typeof(TRequest).Name, key);
            return JsonSerializer.Deserialize<TResponse>(cached)!;
        }

        var response = await next();

        await _store.SetAsync(key, JsonSerializer.Serialize(response), Ttl, cancellationToken);

        return response;
    }
}
