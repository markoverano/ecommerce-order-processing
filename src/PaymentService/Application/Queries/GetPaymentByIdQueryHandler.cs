using ECommerceOrderProcessing.Shared.Models;
using MediatR;
using PaymentService.Application.DTOs;
using PaymentService.Application.Repositories;

namespace PaymentService.Application.Queries;

public sealed class GetPaymentByIdQueryHandler : IRequestHandler<GetPaymentByIdQuery, ServiceResponse<PaymentDto>>
{
    private readonly IPaymentReadRepository _readRepository;

    public GetPaymentByIdQueryHandler(IPaymentReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<ServiceResponse<PaymentDto>> Handle(GetPaymentByIdQuery query, CancellationToken cancellationToken)
    {
        var payment = await _readRepository.GetByIdAsync(query.PaymentId, cancellationToken);

        if (payment is null)
            return ServiceResponse<PaymentDto>.Failure("PAYMENT_NOT_FOUND", $"Payment {query.PaymentId} was not found.");

        return ServiceResponse<PaymentDto>.Success(payment);
    }
}
