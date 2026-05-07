using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using PaymentService.Application.ExternalClients;
using PaymentService.Application.Validation;
using PaymentService.Domain.Aggregates;
using PaymentService.Domain.Exceptions;
using PaymentService.Domain.Repositories;
using PaymentService.Domain.ValueObjects;

namespace PaymentService.Application.Commands;

public sealed class ProcessPaymentCommandHandler : IRequestHandler<ProcessPaymentCommand, ServiceResponse<PaymentId>>
{
    private readonly IPaymentRepository _repository;
    private readonly IStripePaymentGateway _stripe;
    private readonly ProcessPaymentCommandValidator _validator;
    private readonly ILogger<ProcessPaymentCommandHandler> _logger;

    public ProcessPaymentCommandHandler(
        IPaymentRepository repository,
        IStripePaymentGateway stripe,
        ProcessPaymentCommandValidator validator,
        ILogger<ProcessPaymentCommandHandler> logger)
    {
        _repository = repository;
        _stripe = stripe;
        _validator = validator;
        _logger = logger;
    }

    public async Task<ServiceResponse<PaymentId>> Handle(ProcessPaymentCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return ServiceResponse<PaymentId>.Failure("VALIDATION_FAILED", errors);
        }

        var payment = Payment.Create(command.OrderId, command.CustomerId, command.Amount, command.PaymentMethodId, command.CorrelationId);

        try
        {
            var result = await _stripe.ChargeAsync(command.PaymentMethodId, command.Amount, command.CorrelationId, cancellationToken);

            if (result.IsSuccess)
                payment.MarkAsProcessed(StripeChargeId.From(result.ChargeId!), command.CorrelationId);
            else
                payment.MarkAsFailed(result.ErrorMessage ?? "Payment declined.", command.CorrelationId);
        }
        catch (PaymentProcessingException ex)
        {
            _logger.LogWarning(ex, "Stripe charge rejected for order {OrderId}: {Reason}", command.OrderId, ex.Message);
            payment.MarkAsFailed(ex.Message, command.CorrelationId);
        }

        await _repository.SaveAsync(payment, cancellationToken);

        _logger.LogInformation(
            "Payment {PaymentId} for order {OrderId} completed with status {Status}. CorrelationId={CorrelationId}",
            payment.PaymentId, command.OrderId, payment.Status, command.CorrelationId);

        return ServiceResponse<PaymentId>.Success(payment.PaymentId);
    }
}
