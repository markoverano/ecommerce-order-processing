using ECommerceOrderProcessing.Shared.Auth;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Moq;
using OrderService.Application.DTOs;
using OrderService.Application.Queries;
using OrderService.Application.Repositories;
using Xunit;

namespace OrderService.Application.Tests;

public sealed class GetOrderByIdQueryHandlerTests
{
    private readonly Mock<IOrderReadRepository> _readRepositoryMock = new();
    private readonly Mock<ICurrentUserAccessor> _accessorMock = new();
    private readonly GetOrderByIdQueryHandler _handler;

    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid OtherId = Guid.NewGuid();
    private static readonly OrderId TestOrderId = OrderId.From(Guid.NewGuid());

    public GetOrderByIdQueryHandlerTests()
    {
        _handler = new GetOrderByIdQueryHandler(_readRepositoryMock.Object, _accessorMock.Object);
    }

    [Fact]
    public async Task Handle_WhenOrderBelongsToCallingCustomer_ReturnsSuccess()
    {
        SetupCustomer(OwnerId);
        SetupOrder(OwnerId);

        var result = await _handler.Handle(new GetOrderByIdQuery(TestOrderId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_WhenOrderBelongsToDifferentCustomer_ReturnsNotFound()
    {
        SetupCustomer(OwnerId);
        SetupOrder(OtherId);

        var result = await _handler.Handle(new GetOrderByIdQuery(TestOrderId, Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ORDER_NOT_FOUND", result.Error!.Code);
    }

    [Fact]
    public async Task Handle_WhenCallerIsAdmin_ReturnsOrderRegardlessOfOwnership()
    {
        _accessorMock
            .Setup(a => a.GetCurrentUser())
            .Returns(new CurrentUserContext(new CustomerId(OwnerId), "admin@example.com", new[] { Roles.Admin }));
        SetupOrder(OtherId);

        var result = await _handler.Handle(new GetOrderByIdQuery(TestOrderId, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_WhenOrderDoesNotExist_ReturnsNotFound()
    {
        SetupCustomer(OwnerId);
        _readRepositoryMock
            .Setup(r => r.GetByIdAsync(TestOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderDto?)null);

        var result = await _handler.Handle(new GetOrderByIdQuery(TestOrderId, Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("ORDER_NOT_FOUND", result.Error!.Code);
    }

    private void SetupCustomer(Guid customerId) =>
        _accessorMock
            .Setup(a => a.GetCurrentUser())
            .Returns(new CurrentUserContext(new CustomerId(customerId), "customer@example.com", new[] { Roles.Customer }));

    private void SetupOrder(Guid customerId) =>
        _readRepositoryMock
            .Setup(r => r.GetByIdAsync(TestOrderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderDto(
                TestOrderId.Value,
                customerId,
                "Pending",
                100m,
                "USD",
                Array.Empty<OrderItemDto>(),
                new ShippingAddressDto("123 St", null, "City", "ST", "12345", "US"),
                DateTimeOffset.UtcNow,
                null));
}
