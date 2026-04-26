namespace ECommerceOrderProcessing.Shared.ValueObjects;

public readonly record struct ShipmentId(Guid Value)
{
    public static ShipmentId New() => new(Guid.NewGuid());
    public static ShipmentId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
