using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Api.Requests;
using NotificationService.Application.Queries;

namespace NotificationService.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly ISender _mediator;

    public NotificationsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Sends a customer notification via Mailgun (email) or Twilio (SMS).</summary>
    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendNotificationRequest request, CancellationToken cancellationToken)
    {
        var command = new NotifyCustomerCommand(
            OrderId.From(request.OrderId),
            CustomerId.From(request.CustomerId),
            request.NotificationType,
            request.TemplateData.AsReadOnly(),
            HttpContext.Items["CorrelationId"] is Guid cid ? cid : Guid.NewGuid());

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        return Ok(new { notificationId = result.Data!.Value });
    }

    /// <summary>Returns notification details by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetNotificationByIdQuery(NotificationId.From(id)), cancellationToken);

        if (!result.IsSuccess)
            return NotFound(result.Error);

        return Ok(result.Data);
    }

    /// <summary>Returns the hub connection info for the given order so the browser client can subscribe to real-time updates.</summary>
    [HttpGet("token")]
    public IActionResult GetConnectionToken([FromQuery] Guid orderId)
    {
        if (orderId == Guid.Empty)
            return BadRequest(new { code = "INVALID_ORDER_ID", message = "orderId query parameter is required." });

        return Ok(new
        {
            hubUrl = "/hubs/order-status",
            orderId,
            method = "ReceiveOrderUpdate"
        });
    }
}
