using ECommerceOrderProcessing.Shared.SignalR;
using Microsoft.AspNetCore.SignalR;
using Moq;
using NotificationService.Api.Hubs;
using NotificationService.Api.Notifications;
using Xunit;

namespace NotificationService.Application.Tests.Notifications;

public sealed class OrderStatusNotifierTests
{
    private static OrderStatusUpdate BuildUpdate(Guid orderId) =>
        new(orderId, "NotificationPending", "Sent", DateTimeOffset.UtcNow, Guid.NewGuid());

    [Fact]
    public async Task NotifyAsync_SendsToGroupKeyedOnOrderId()
    {
        var orderId = Guid.NewGuid();
        var update = BuildUpdate(orderId);

        var clientProxyMock = new Mock<IClientProxy>();
        var clientsMock = new Mock<IHubClients>();
        clientsMock.Setup(c => c.Group($"order-{orderId}")).Returns(clientProxyMock.Object);

        var contextMock = new Mock<IHubContext<OrderStatusHub>>();
        contextMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        var notifier = new OrderStatusNotifier(contextMock.Object);
        await notifier.NotifyAsync(update);

        clientsMock.Verify(c => c.Group($"order-{orderId}"), Times.Once);
        clientProxyMock.Verify(
            c => c.SendCoreAsync("ReceiveOrderUpdate", It.Is<object[]>(a => a.Length == 1 && a[0].Equals(update)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_DifferentOrders_TargetDifferentGroups()
    {
        var orderId1 = Guid.NewGuid();
        var orderId2 = Guid.NewGuid();

        string? capturedGroup1 = null;
        string? capturedGroup2 = null;

        var proxy1 = new Mock<IClientProxy>();
        var proxy2 = new Mock<IClientProxy>();
        var clientsMock = new Mock<IHubClients>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns<string>(group =>
        {
            if (group == $"order-{orderId1}") { capturedGroup1 = group; return proxy1.Object; }
            capturedGroup2 = group; return proxy2.Object;
        });

        var contextMock = new Mock<IHubContext<OrderStatusHub>>();
        contextMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        var notifier = new OrderStatusNotifier(contextMock.Object);
        await notifier.NotifyAsync(BuildUpdate(orderId1));
        await notifier.NotifyAsync(BuildUpdate(orderId2));

        Assert.NotEqual(capturedGroup1, capturedGroup2);
        proxy1.Verify(c => c.SendCoreAsync("ReceiveOrderUpdate", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
        proxy2.Verify(c => c.SendCoreAsync("ReceiveOrderUpdate", It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyAsync_PassesCancellationTokenToSendCore()
    {
        var orderId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        CancellationToken? capturedToken = null;
        var clientProxyMock = new Mock<IClientProxy>();
        clientProxyMock
            .Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, object[], CancellationToken>((_, _, ct) => capturedToken = ct)
            .Returns(Task.CompletedTask);

        var clientsMock = new Mock<IHubClients>();
        clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxyMock.Object);

        var contextMock = new Mock<IHubContext<OrderStatusHub>>();
        contextMock.Setup(h => h.Clients).Returns(clientsMock.Object);

        var notifier = new OrderStatusNotifier(contextMock.Object);
        await notifier.NotifyAsync(BuildUpdate(orderId), token);

        Assert.Equal(token, capturedToken);
    }
}
