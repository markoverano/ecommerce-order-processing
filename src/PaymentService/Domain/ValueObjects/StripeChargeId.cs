namespace PaymentService.Domain.ValueObjects;

public readonly record struct StripeChargeId
{
    public string Value { get; init; }

    private StripeChargeId(string value) => Value = value;

    public static StripeChargeId From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Stripe charge ID cannot be empty.", nameof(value));
        return new StripeChargeId(value);
    }

    public override string ToString() => Value;
}
