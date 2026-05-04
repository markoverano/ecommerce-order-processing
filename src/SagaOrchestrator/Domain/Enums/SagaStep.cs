namespace SagaOrchestrator.Domain.Enums;

public enum SagaStep
{
    PaymentPending,
    InventoryPending,
    ShippingPending,
    NotificationPending,
    InventoryCompensation,
    PaymentCompensation,
    Done
}
