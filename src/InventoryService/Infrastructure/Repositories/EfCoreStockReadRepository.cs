using ECommerceOrderProcessing.Shared.ValueObjects;
using InventoryService.Application.DTOs;
using InventoryService.Application.Repositories;
using InventoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Infrastructure.Repositories;

public sealed class EfCoreStockReadRepository : IStockReadRepository
{
    private readonly InventoryDbContext _db;

    public EfCoreStockReadRepository(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task<StockDto?> GetByProductIdAsync(ProductId productId, CancellationToken cancellationToken = default)
    {
        var model = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId.Value, cancellationToken);

        return model is null ? null : new StockDto(model.Id, model.Name, model.AvailableQuantity, model.ReservedQuantity, model.UpdatedAt);
    }
}
