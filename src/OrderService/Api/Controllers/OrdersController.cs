using ECommerceOrderProcessing.Infrastructure.Middleware;
using ECommerceOrderProcessing.Shared.Auth;
using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.Utilities;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Api.Requests;
using OrderService.Application.Queries;

namespace OrderService.Api.Controllers;

/// <summary>Order lifecycle REST endpoints.</summary>
[ApiController]
[Route("api/v1/orders")]
[Authorize(Roles = Roles.Customer)]
public sealed class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;

    public OrdersController(IMediator mediator, ICorrelationIdAccessor correlationIdAccessor)
    {
        _mediator = mediator;
        _correlationIdAccessor = correlationIdAccessor;
    }

    /// <summary>Creates a new order and enqueues it for saga processing.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ServiceResponse<OrderId>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ServiceResponse<OrderId>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var correlationId = _correlationIdAccessor.GetCorrelationId(HttpContext);

        var command = new CreateOrderCommand(
            request.Items
                .Select(i => new OrderItemRequest(
                    new ProductId(i.ProductId),
                    i.Quantity,
                    Money.Create(i.UnitPrice, i.Currency)))
                .ToList()
                .AsReadOnly(),
            ShippingAddress.Create(
                request.ShippingAddress.Line1,
                request.ShippingAddress.Line2,
                request.ShippingAddress.City,
                request.ShippingAddress.State,
                request.ShippingAddress.PostalCode,
                request.ShippingAddress.CountryCode),
            idempotencyKey is not null
                ? IdempotencyKey.From(idempotencyKey)
                : IdempotencyKey.New(),
            correlationId);

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(result);

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Value }, result);
    }

    /// <summary>Returns a single order by ID. Customers may only access their own orders.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetOrderByIdQuery(OrderId.From(id), _correlationIdAccessor.GetCorrelationId(HttpContext));
        var result = await _mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return NotFound(result);

        return Ok(result);
    }

    /// <summary>Returns a paginated list of orders. Customers see only their own; admins see all.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetOrdersQuery(page, pageSize, _correlationIdAccessor.GetCorrelationId(HttpContext));
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

}
