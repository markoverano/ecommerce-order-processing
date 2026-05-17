using Microsoft.EntityFrameworkCore;
using SagaOrchestrator.Application.DTOs;
using SagaOrchestrator.Application.Repositories;
using SagaOrchestrator.Infrastructure.Persistence;

namespace SagaOrchestrator.Infrastructure.Repositories;

public sealed class EfCoreSagaAdminReadRepository : ISagaAdminReadRepository
{
    private readonly SagaDbContext _db;

    public EfCoreSagaAdminReadRepository(SagaDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SagaDto>> GetByStatusAsync(
        string status,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.SagaStates
            .AsNoTracking()
            .Where(s => s.Status == status)
            .OrderBy(s => s.UpdatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows
            .Select(s => new SagaDto(s.SagaId, s.OrderId, s.Status, s.CurrentStep, s.CompensationReason, s.CreatedAt, s.UpdatedAt))
            .ToList()
            .AsReadOnly();
    }
}
