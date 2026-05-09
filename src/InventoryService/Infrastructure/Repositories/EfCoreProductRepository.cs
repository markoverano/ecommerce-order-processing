using ECommerceOrderProcessing.Shared.ValueObjects;
using InventoryService.Domain.Aggregates;
using InventoryService.Domain.Repositories;
using InventoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Infrastructure.Repositories;

public sealed class EfCoreProductRepository : IProductRepository
{
    private readonly InventoryDbContext _db;

    public EfCoreProductRepository(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task<Product?> GetByIdAsync(ProductId productId, CancellationToken cancellationToken = default)
    {
        var model = await _db.Products.FindAsync(new object[] { productId.Value }, cancellationToken);
        return model is null ? null : MapToDomain(model);
    }

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(
        IReadOnlyList<ProductId> productIds,
        CancellationToken cancellationToken = default)
    {
        var ids = productIds.Select(p => p.Value).ToList();
        var models = await _db.Products
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);

        return models.Select(MapToDomain).ToList().AsReadOnly();
    }

    private static Product MapToDomain(ProductReadModel model) =>
        Product.From(ProductId.From(model.Id), model.Name, model.AvailableQuantity, model.ReservedQuantity);
}
