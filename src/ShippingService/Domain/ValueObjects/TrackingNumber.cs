namespace ShippingService.Domain.ValueObjects;

public readonly record struct TrackingNumber
{
    public string Value { get; }

    private TrackingNumber(string value) => Value = value;

    public static TrackingNumber From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Tracking number cannot be empty.", nameof(value));

        return new TrackingNumber(value.Trim().ToUpperInvariant());
    }

    public override string ToString() => Value;
}
