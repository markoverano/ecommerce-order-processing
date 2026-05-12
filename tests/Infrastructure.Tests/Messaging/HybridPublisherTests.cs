using ECommerceOrderProcessing.Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Infrastructure.Tests.Messaging;

public class HybridPublisherTests
{
    private readonly Mock<IEventPublisher> _primary = new();
    private readonly Mock<IEventPublisher> _fallback = new();
    private readonly BrokerHealthTracker _tracker = new();

    private HybridPublisher CreatePublisher() =>
        new(_primary.Object, _fallback.Object, _tracker, NullLogger<HybridPublisher>.Instance);

    [Fact]
    public async Task PublishAsync_PrimarySucceeds_FallbackNotInvoked()
    {
        _primary.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreatePublisher().PublishAsync("OrderCreated", "{}", "order.created");

        _fallback.Verify(
            f => f.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishAsync_PrimaryThrows_FallbackInvoked()
    {
        _primary.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));
        _fallback.Setup(f => f.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreatePublisher().PublishAsync("OrderCreated", "{}", "order.created");

        _fallback.Verify(
            f => f.PublishAsync("OrderCreated", "{}", "order.created", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_PrimaryTimesOut_FallbackInvoked()
    {
        _primary.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, string _, CancellationToken ct) => await Task.Delay(TimeSpan.FromSeconds(10), ct));
        _fallback.Setup(f => f.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreatePublisher().PublishAsync("OrderCreated", "{}", "order.created");

        _fallback.Verify(
            f => f.PublishAsync("OrderCreated", "{}", "order.created", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_PrimaryFails_HealthTrackerSetToFallback()
    {
        _primary.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("connection refused"));
        _fallback.Setup(f => f.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreatePublisher().PublishAsync("OrderCreated", "{}", "order.created");

        Assert.True(_tracker.IsUsingFallback);
        Assert.NotNull(_tracker.FallbackActivatedAt);
    }

    [Fact]
    public async Task PublishAsync_PrimaryRecovers_HealthTrackerClearsFallback()
    {
        // Arrange: drive into fallback state
        _primary.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("connection refused"));
        _fallback.Setup(f => f.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        await CreatePublisher().PublishAsync("OrderCreated", "{}", "order.created");
        Assert.True(_tracker.IsUsingFallback);

        // Act: primary recovers
        _primary.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        await CreatePublisher().PublishAsync("OrderCreated", "{}", "order.created");

        Assert.False(_tracker.IsUsingFallback);
        Assert.Null(_tracker.FallbackActivatedAt);
    }

    [Fact]
    public async Task PublishAsync_CallerCancels_ExceptionPropagatesWithoutFallback()
    {
        using var cts = new CancellationTokenSource();

        _primary.Setup(p => p.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, string _, CancellationToken ct) => await Task.Delay(TimeSpan.FromSeconds(10), ct));

        cts.CancelAfter(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreatePublisher().PublishAsync("OrderCreated", "{}", "order.created", cts.Token));

        _fallback.Verify(
            f => f.PublishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
