using ECommerceOrderProcessing.Shared.Auth;
using ECommerceOrderProcessing.Shared.Models;
using MediatR;
using PaymentService.Application.DTOs;
using PaymentService.Application.Repositories;

namespace PaymentService.Application.Queries;

public sealed class GetPaymentByIdQueryHandler : IRequestHandler<GetPaymentByIdQuery, ServiceResponse<PaymentDto>>
{
    private readonly IPaymentReadRepository _readRepository;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public GetPaymentByIdQueryHandler(IPaymentReadRepository readRepository, ICurrentUserAccessor currentUserAccessor)
    {
        _readRepository = readRepository;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<ServiceResponse<PaymentDto>> Handle(GetPaymentByIdQuery query, CancellationToken cancellationToken)
    {
        var payment = await _readRepository.GetByIdAsync(query.PaymentId, cancellationToken);

        if (payment is null)
            return ServiceResponse<PaymentDto>.Failure("PAYMENT_NOT_FOUND", $"Payment {query.PaymentId} was not found.");

        var user = _currentUserAccessor.GetCurrentUser();
        var isAdmin = user?.Roles.Contains(Roles.Admin) == true;

        if (!isAdmin && payment.CustomerId != user?.UserId.Value)
            return ServiceResponse<PaymentDto>.Failure("PAYMENT_NOT_FOUND", $"Payment {query.PaymentId} was not found.");

        return ServiceResponse<PaymentDto>.Success(payment);
    }
}
