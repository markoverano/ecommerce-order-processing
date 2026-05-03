using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.Utilities;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderService.Application.Commands;
using OrderService.Application.Validation;
using OrderService.Domain.Aggregates;
using OrderService.Domain.Repositories;
using Xunit;

namespace OrderService.Application.Tests;

public sealed class CreateOrderCommandHandlerTests
{
    private readonly Mock<IOrderRepository> _repositoryMock = new();
    private readonly CreateOrderCommandValidator _validator = new();
    private readonly CreateOrderCommandHandler _handler;

    public CreateOrderCommandHandlerTests()
    {
        _handler = new CreateOrderCommandHandler(
            _repositoryMock.Object,
            _validator,
            NullLogger<CreateOrderCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsSuccessWithOrderId()
    {
        var command = BuildValidCommand();
        _repositoryMock
            .Setup(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Data!.Value);
    }

    [Fact]
    public async Task Handle_WithValidCommand_SavesOrderToRepository()
    {
        var command = BuildValidCommand();

        await _handler.Handle(command, CancellationToken.None);

        _repositoryMock.Verify(
            r => r.SaveAsync(It.Is<Order>(o => o.CustomerId == command.CustomerId), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithEmptyItems_ReturnsValidationFailure()
    {
        var command = new CreateOrderCommand(
            new CustomerId(Guid.NewGuid()),
            Array.Empty<OrderItemRequest>(),
            ShippingAddress.Create("123 St", null, "City", "State", "12345", "US"),
            IdempotencyKey.New(),
            Guid.NewGuid());

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_FAILED", result.Error!.Code);
        _repositoryMock.Verify(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithZeroQuantity_ReturnsValidationFailure()
    {
        var command = new CreateOrderCommand(
            new CustomerId(Guid.NewGuid()),
            new[] { new OrderItemRequest(new ProductId(Guid.NewGuid()), 0, Money.Create(10m, "USD")) },
            ShippingAddress.Create("123 St", null, "City", "State", "12345", "US"),
            IdempotencyKey.New(),
            Guid.NewGuid());

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatedOrderHasPendingStatus()
    {
        Order? savedOrder = null;
        _repositoryMock
            .Setup(r => r.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()))
            .Callback<Order, CancellationToken>((o, _) => savedOrder = o)
            .Returns(Task.CompletedTask);

        await _handler.Handle(BuildValidCommand(), CancellationToken.None);

        Assert.NotNull(savedOrder);
        Assert.Equal(ECommerceOrderProcessing.Shared.ValueObjects.OrderId.From(savedOrder!.Id), savedOrder.OrderId);
    }

    private static CreateOrderCommand BuildValidCommand() =>
        new(
            new CustomerId(Guid.NewGuid()),
            new[]
            {
                new OrderItemRequest(new ProductId(Guid.NewGuid()), 2, Money.Create(19.99m, "USD")),
                new OrderItemRequest(new ProductId(Guid.NewGuid()), 1, Money.Create(49.99m, "USD"))
            },
            ShippingAddress.Create("456 Oak Ave", null, "Chicago", "IL", "60601", "US"),
            IdempotencyKey.New(),
            Guid.NewGuid());
}
