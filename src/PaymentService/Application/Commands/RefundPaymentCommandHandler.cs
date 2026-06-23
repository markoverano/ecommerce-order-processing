using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using PaymentService.Application.ExternalClients;
using PaymentService.Application.Metrics;
using PaymentService.Domain.Exceptions;
using PaymentService.Domain.Repositories;

namespace PaymentService.Application.Commands;

public sealed class RefundPaymentCommandHandler : IRequestHandler<RefundPaymentCommand, ServiceResponse<bool>>
{
    private readonly IPaymentRepository _repository;
    private readonly IStripePaymentClient _stripe;
    private readonly ILogger<RefundPaymentCommandHandler> _logger;

    public RefundPaymentCommandHandler(
        IPaymentRepository repository,
        IStripePaymentClient stripe,
        ILogger<RefundPaymentCommandHandler> logger)
    {
        _repository = repository;
        _stripe = stripe;
        _logger = logger;
    }

    public async Task<ServiceResponse<bool>> Handle(RefundPaymentCommand command, CancellationToken cancellationToken)
    {
        var payment = await _repository.GetByIdAsync(command.PaymentId, cancellationToken);

        if (payment is null)
            return ServiceResponse<bool>.Failure("PAYMENT_NOT_FOUND", $"Payment {command.PaymentId} was not found.");

        if (payment.ChargeId is null)
            return ServiceResponse<bool>.Failure("NO_CHARGE_ID", "Cannot refund a payment that was never charged.");

        try
        {
            var result = await _stripe.RefundAsync(payment.ChargeId.Value.Value, command.Amount, cancellationToken);
            if (!result.IsSuccess)
                return ServiceResponse<bool>.Failure("REFUND_FAILED", result.ErrorMessage ?? "Refund could not be processed.");
        }
        catch (PaymentProcessingException ex)
        {
            _logger.LogError(ex, "Stripe refund failed for payment {PaymentId}: {Reason}", command.PaymentId, ex.Message);
            return ServiceResponse<bool>.Failure("REFUND_FAILED", ex.Message);
        }

        payment.MarkAsRefunded(command.Amount, command.CorrelationId);
        await _repository.SaveAsync(payment, cancellationToken);

        PaymentMetrics.PaymentsRefunded.Inc();

        _logger.LogInformation(
            "Payment {PaymentId} for order {OrderId} refunded. CorrelationId={CorrelationId}",
            command.PaymentId, command.OrderId, command.CorrelationId);

        return ServiceResponse<bool>.Success(true);
    }
}
