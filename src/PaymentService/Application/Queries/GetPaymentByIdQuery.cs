using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;
using PaymentService.Application.DTOs;

namespace PaymentService.Application.Queries;

public sealed record GetPaymentByIdQuery(PaymentId PaymentId) : IRequest<ServiceResponse<PaymentDto>>;
