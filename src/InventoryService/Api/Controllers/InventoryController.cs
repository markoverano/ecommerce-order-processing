using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.ValueObjects;
using InventoryService.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Api.Controllers;

[ApiController]
[Route("api/v1/inventory")]
public sealed class InventoryController : ControllerBase
{
    private readonly ISender _mediator;

    public InventoryController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Reserves stock for an order. Normally called by the Saga Orchestrator via RabbitMQ; also available over HTTP.</summary>
    [HttpPost("reserve")]
    public async Task<IActionResult> Reserve([FromBody] ReserveStockRequest request, CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items["CorrelationId"] is Guid cid ? cid : Guid.NewGuid();
        var items = request.Items
            .Select(i => new StockReservationItem(ProductId.From(i.ProductId), i.Quantity))
            .ToList();

        var command = new ReserveStockCommand(OrderId.From(request.OrderId), items, correlationId);
        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? Ok(new { reservationId = result.Data!.Value })
            : Conflict(result.Error);
    }

    /// <summary>Releases a reservation as part of saga compensation.</summary>
    [HttpPost("reservations/{id:guid}/release")]
    public async Task<IActionResult> Release(Guid id, [FromBody] ReleaseReservationRequest request, CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.Items["CorrelationId"] is Guid cid ? cid : Guid.NewGuid();
        var command = new ReleaseStockCommand(ReservationId.From(id), OrderId.From(request.OrderId), correlationId);
        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess ? Ok() : NotFound(result.Error);
    }

    /// <summary>Returns current stock levels for a product.</summary>
    [HttpGet("products/{productId:guid}")]
    public async Task<IActionResult> GetStock(Guid productId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetStockQuery(ProductId.From(productId)), cancellationToken);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result.Error);
    }
}

public sealed record ReserveStockRequest(Guid OrderId, IReadOnlyList<ReserveStockItemRequest> Items);

public sealed record ReserveStockItemRequest(Guid ProductId, int Quantity);

public sealed record ReleaseReservationRequest(Guid OrderId);
