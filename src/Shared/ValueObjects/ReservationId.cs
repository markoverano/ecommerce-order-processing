namespace ECommerceOrderProcessing.Shared.ValueObjects;

public readonly record struct ReservationId(Guid Value)
{
    public static ReservationId New() => new(Guid.NewGuid());
    public static ReservationId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
