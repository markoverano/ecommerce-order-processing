using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Queries;

public sealed record GetNotificationByIdQuery(NotificationId NotificationId)
    : IRequest<ServiceResponse<NotificationDto>>;
