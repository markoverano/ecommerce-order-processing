using AnalyticsService.Domain.Repositories;
using AnalyticsService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AnalyticsService.Infrastructure.Repositories;

public abstract class EfCoreAnalyticsRepository<T> : IAnalyticsRepository<T> where T : class
{
    protected readonly AnalyticsDbContext Context;
    protected readonly DbSet<T> DbSet;

    protected EfCoreAnalyticsRepository(AnalyticsDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await DbSet.AddAsync(entity, cancellationToken);
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbSet.Update(entity);
        await Task.CompletedTask;
    }

    public virtual async Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FindAsync(new object[] { id }, cancellationToken: cancellationToken);
    }

    public virtual async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await Context.SaveChangesAsync(cancellationToken);
    }
}
