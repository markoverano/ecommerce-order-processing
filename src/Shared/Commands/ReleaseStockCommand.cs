using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;

namespace ECommerceOrderProcessing.Shared.Commands;

/// <summary>Instructs Inventory Service to release a reservation during saga compensation.</summary>
public sealed record ReleaseStockCommand : IRequest<ServiceResponse<bool>>, IIdempotentCommand
{
    public ReservationId ReservationId { get; init; }
    public OrderId OrderId { get; init; }
    public Guid CorrelationId { get; init; }

    public ReleaseStockCommand(ReservationId reservationId, OrderId orderId, Guid correlationId)
    {
        ReservationId = reservationId;
        OrderId = orderId;
        CorrelationId = correlationId;
    }

    string IIdempotentCommand.GetIdempotencyKey() => CorrelationId.ToString();
}
