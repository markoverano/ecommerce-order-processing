using SagaOrchestrator.Application.DTOs;

namespace SagaOrchestrator.Application.Repositories;

/// <summary>Read-side repository for admin monitoring queries against saga state.</summary>
public interface ISagaAdminReadRepository
{
    Task<IReadOnlyList<SagaDto>> GetByStatusAsync(string status, int limit, CancellationToken cancellationToken = default);
}
