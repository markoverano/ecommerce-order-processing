using ECommerceOrderProcessing.Shared.Auth;
using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShippingService.Application.Queries;

namespace ShippingService.Api.Controllers;

[ApiController]
[Route("api/v1/shipments")]
public sealed class ShipmentsController : ControllerBase
{
    private readonly ISender _mediator;

    public ShipmentsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Books a FedEx shipment for the given order. Sent by the Saga Orchestrator after stock is reserved.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Create([FromBody] CreateShipmentRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateShipmentCommand(
            OrderId.From(request.OrderId),
            CustomerId.From(request.CustomerId),
            ShippingAddress.Create(request.Line1, request.Line2, request.City, request.State, request.PostalCode, request.CountryCode),
            request.Items.Select(i => new ShipmentItem(ProductId.From(i.ProductId), i.Quantity, i.Description)).ToList(),
            HttpContext.Items["CorrelationId"] is Guid cid ? cid : Guid.NewGuid());

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        return Ok(new { shipmentId = result.Data!.Value });
    }

    /// <summary>Returns shipment details by ID. Customers may only access their own shipments.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetShipmentByIdQuery(ShipmentId.From(id)), cancellationToken);

        if (!result.IsSuccess)
            return NotFound(result.Error);

        return Ok(result.Data);
    }

    /// <summary>Cancels a shipment. Used by the Saga Orchestrator during compensation.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelShipmentRequest request, CancellationToken cancellationToken)
    {
        var command = new CancelShipmentCommand(
            ShipmentId.From(id),
            OrderId.From(request.OrderId),
            HttpContext.Items["CorrelationId"] is Guid cid ? cid : Guid.NewGuid());

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        return Ok();
    }
}

public sealed record CreateShipmentRequest(
    Guid OrderId,
    Guid CustomerId,
    string Line1,
    string? Line2,
    string City,
    string State,
    string PostalCode,
    string CountryCode,
    IReadOnlyList<ShipmentItemRequest> Items);

public sealed record ShipmentItemRequest(Guid ProductId, int Quantity, string Description);

public sealed record CancelShipmentRequest(Guid OrderId);
