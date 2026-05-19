using ECommerceOrderProcessing.Shared.Auth;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagaOrchestrator.Domain.Repositories;

namespace SagaOrchestrator.Api.Controllers;

[ApiController]
[Route("api/v1/sagas")]
[Authorize(Roles = Roles.Customer)]
public sealed class SagasController : ControllerBase
{
    private readonly ISagaRepository _repository;

    public SagasController(ISagaRepository repository) => _repository = repository;

    /// <summary>Returns the current saga state for the given order, for monitoring purposes.</summary>
    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(typeof(SagaViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByOrderId(Guid orderId, CancellationToken cancellationToken)
    {
        var saga = await _repository.GetByOrderIdAsync(OrderId.From(orderId), cancellationToken);

        if (saga is null)
            return NotFound();

        return Ok(new SagaViewModel(
            saga.Id,
            saga.OrderId.Value,
            saga.Status.ToString(),
            saga.CurrentStep.ToString(),
            saga.CompensationReason));
    }
}

public sealed record SagaViewModel(
    Guid SagaId,
    Guid OrderId,
    string Status,
    string CurrentStep,
    string? CompensationReason);
