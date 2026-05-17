namespace SagaOrchestrator.Application.DTOs;

public sealed record SagaDto(
    Guid SagaId,
    Guid OrderId,
    string Status,
    string CurrentStep,
    string? CompensationReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
