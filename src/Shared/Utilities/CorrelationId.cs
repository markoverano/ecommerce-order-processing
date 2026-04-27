namespace ECommerceOrderProcessing.Shared.Utilities;

public readonly record struct CorrelationId(Guid Value)
{
    public static CorrelationId New() => new(Guid.NewGuid());
    public static CorrelationId From(Guid value) => new(value);
    public static CorrelationId From(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}
