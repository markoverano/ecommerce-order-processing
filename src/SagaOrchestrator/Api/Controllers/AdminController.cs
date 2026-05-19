using ECommerceOrderProcessing.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SagaOrchestrator.Application.Services;
using SagaOrchestrator.Domain.Exceptions;

namespace SagaOrchestrator.Api.Controllers;

[ApiController]
[Route("api/v1/admin/sagas")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminController : ControllerBase
{
    private readonly SagaAdminService _adminService;

    public AdminController(SagaAdminService adminService) => _adminService = adminService;

    /// <summary>
    /// Lists sagas filtered by status.
    /// Useful for finding compensating or long-running sagas that may be stuck.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SagaOrchestrator.Application.DTOs.SagaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetByStatus(
        [FromQuery] string status = "Compensating",
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(status))
            return BadRequest("status query parameter is required.");

        if (limit is <= 0 or > 500)
            return BadRequest("limit must be between 1 and 500.");

        var sagas = await _adminService.GetSagasByStatusAsync(status, limit, cancellationToken);
        return Ok(sagas);
    }

    /// <summary>
    /// Re-issues the pending command for the saga's current step.
    /// Use this to unblock a saga that is stuck due to transient infrastructure failure.
    /// </summary>
    [HttpPost("{orderId:guid}/retry")]
    [ProducesResponseType(typeof(RetryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retry(Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            var message = await _adminService.RetryCurrentStepAsync(orderId, cancellationToken);
            return Ok(new RetryResponse(orderId, message));
        }
        catch (SagaNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }
}

public sealed record RetryResponse(Guid OrderId, string Message);
