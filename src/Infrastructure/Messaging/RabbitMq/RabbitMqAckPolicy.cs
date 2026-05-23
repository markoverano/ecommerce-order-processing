using RabbitMQ.Client;

namespace ECommerceOrderProcessing.Infrastructure.Messaging.RabbitMq;

/// <summary>
/// Centralises RabbitMQ acknowledgment decisions so ACK/NACK logic is not scattered across consumer callbacks.
/// </summary>
internal static class RabbitMqAckPolicy
{
    internal static void Ack(IModel channel, ulong deliveryTag) =>
        channel.BasicAck(deliveryTag, multiple: false);

    internal static void Nack(IModel channel, ulong deliveryTag) =>
        channel.BasicNack(deliveryTag, multiple: false, requeue: false);
}
