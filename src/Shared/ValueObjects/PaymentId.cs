namespace ECommerceOrderProcessing.Shared.ValueObjects;

public readonly record struct PaymentId(Guid Value)
{
    public static PaymentId New() => new(Guid.NewGuid());
    public static PaymentId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
