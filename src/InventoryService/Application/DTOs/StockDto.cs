namespace InventoryService.Application.DTOs;

public sealed record StockDto(
    Guid ProductId,
    string ProductName,
    int AvailableQuantity,
    int ReservedQuantity,
    DateTimeOffset UpdatedAt);
