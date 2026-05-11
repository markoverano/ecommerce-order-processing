using ECommerceOrderProcessing.Shared.Models;
using MediatR;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Repositories;

namespace NotificationService.Application.Queries;

public sealed class GetNotificationQueryHandler : IRequestHandler<GetNotificationByIdQuery, ServiceResponse<NotificationDto>>
{
    private readonly INotificationReadRepository _readRepository;
    private readonly ILogger<GetNotificationQueryHandler> _logger;

    public GetNotificationQueryHandler(
        INotificationReadRepository readRepository,
        ILogger<GetNotificationQueryHandler> logger)
    {
        _readRepository = readRepository;
        _logger = logger;
    }

    public async Task<ServiceResponse<NotificationDto>> Handle(GetNotificationByIdQuery query, CancellationToken cancellationToken)
    {
        var dto = await _readRepository.GetByIdAsync(query.NotificationId, cancellationToken);
        if (dto is null)
        {
            _logger.LogInformation("Notification {NotificationId} not found.", query.NotificationId);
            return ServiceResponse<NotificationDto>.Failure("NOT_FOUND", $"Notification {query.NotificationId.Value} was not found.");
        }

        return ServiceResponse<NotificationDto>.Success(dto);
    }
}
