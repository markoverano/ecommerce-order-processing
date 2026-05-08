using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Api.Requests;
using PaymentService.Application.Queries;

namespace PaymentService.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly ISender _mediator;

    public PaymentsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Initiates a payment charge via Stripe for the given order.</summary>
    [HttpPost("process")]
    public async Task<IActionResult> Process([FromBody] ProcessPaymentRequest request, CancellationToken cancellationToken)
    {
        var command = new ProcessPaymentCommand(
            OrderId.From(request.OrderId),
            CustomerId.From(request.CustomerId),
            Money.Create(request.Amount, request.Currency),
            request.PaymentMethodId,
            HttpContext.Items["CorrelationId"] is Guid cid ? cid : Guid.NewGuid());

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        return Ok(new { paymentId = result.Data!.Value });
    }

    /// <summary>Returns payment details by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPaymentByIdQuery(PaymentId.From(id)), cancellationToken);

        if (!result.IsSuccess)
            return NotFound(result.Error);

        return Ok(result.Data);
    }

    /// <summary>Issues a refund for a previously processed payment.</summary>
    [HttpPost("{id:guid}/refund")]
    public async Task<IActionResult> Refund(Guid id, [FromBody] RefundRequest request, CancellationToken cancellationToken)
    {
        var command = new RefundPaymentCommand(
            PaymentId.From(id),
            OrderId.From(request.OrderId),
            Money.Create(request.Amount, request.Currency),
            request.Reason,
            HttpContext.Items["CorrelationId"] is Guid cid ? cid : Guid.NewGuid());

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        return Ok();
    }
}

public sealed record RefundRequest(Guid OrderId, decimal Amount, string Currency, string Reason);
