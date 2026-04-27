namespace ECommerceOrderProcessing.Shared.Utilities;

public readonly record struct IdempotencyKey(string Value)
{
    public static IdempotencyKey New() => new(Guid.NewGuid().ToString());
    public static IdempotencyKey From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new IdempotencyKey(value);
    }
    public override string ToString() => Value;
}
