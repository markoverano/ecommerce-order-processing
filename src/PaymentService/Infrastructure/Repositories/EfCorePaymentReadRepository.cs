using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using PaymentService.Application.DTOs;
using PaymentService.Application.Repositories;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Repositories;

public sealed class EfCorePaymentReadRepository : IPaymentReadRepository
{
    private readonly PaymentDbContext _db;

    public EfCorePaymentReadRepository(PaymentDbContext db)
    {
        _db = db;
    }

    public async Task<PaymentDto?> GetByIdAsync(PaymentId paymentId, CancellationToken cancellationToken = default)
    {
        var model = await _db.PaymentViewModels
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentId.Value, cancellationToken);

        return model is null ? null : MapToDto(model);
    }

    public async Task<PaymentId?> FindByStripeChargeIdAsync(string stripeChargeId, CancellationToken cancellationToken = default)
    {
        var model = await _db.PaymentViewModels
            .AsNoTracking()
            .Where(x => x.StripeChargeId == stripeChargeId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return model.HasValue ? PaymentId.From(model.Value) : null;
    }

    private static PaymentDto MapToDto(PaymentReadModel model) =>
        new(model.Id, model.OrderId, model.CustomerId, model.Status, model.Amount, model.Currency, model.StripeChargeId, model.CreatedAt, model.UpdatedAt);
}
