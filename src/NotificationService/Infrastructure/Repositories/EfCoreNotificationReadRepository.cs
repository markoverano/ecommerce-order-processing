using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using NotificationService.Application.DTOs;
using NotificationService.Application.Repositories;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Infrastructure.Repositories;

public sealed class EfCoreNotificationReadRepository : INotificationReadRepository
{
    private readonly NotificationDbContext _db;

    public EfCoreNotificationReadRepository(NotificationDbContext db)
    {
        _db = db;
    }

    public async Task<NotificationDto?> GetByIdAsync(NotificationId notificationId, CancellationToken cancellationToken = default)
    {
        var model = await _db.NotificationViewModels
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == notificationId.Value, cancellationToken);

        return model is null ? null : MapToDto(model);
    }

    public async Task<NotificationId?> FindByProviderMessageIdAsync(string providerMessageId, CancellationToken cancellationToken = default)
    {
        var model = await _db.NotificationViewModels
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProviderMessageId == providerMessageId, cancellationToken);

        return model is null ? null : NotificationId.From(model.Id);
    }

    private static NotificationDto MapToDto(NotificationReadModel model) =>
        new(model.Id, model.OrderId, model.CustomerId, model.NotificationType,
            model.Status, model.Channel, model.RecipientAddress, model.ProviderMessageId,
            model.CreatedAt, model.UpdatedAt);
}
