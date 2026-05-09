namespace InventoryService.Infrastructure.Persistence;

public sealed class ProductReadModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int AvailableQuantity { get; set; }
    public int ReservedQuantity { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
