namespace ECommerceOrderProcessing.Shared.ValueObjects;

public readonly record struct ShippingAddress
{
    public string Line1 { get; init; }
    public string? Line2 { get; init; }
    public string City { get; init; }
    public string State { get; init; }
    public string PostalCode { get; init; }
    public string CountryCode { get; init; }

    private ShippingAddress(string line1, string? line2, string city, string state, string postalCode, string countryCode)
    {
        Line1 = line1;
        Line2 = line2;
        City = city;
        State = state;
        PostalCode = postalCode;
        CountryCode = countryCode;
    }

    public static ShippingAddress Create(string line1, string? line2, string city, string state, string postalCode, string countryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(line1);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(postalCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        return new ShippingAddress(line1.Trim(), line2?.Trim(), city.Trim(), state.Trim(), postalCode.Trim(), countryCode.ToUpperInvariant());
    }

    public override string ToString() =>
        string.IsNullOrWhiteSpace(Line2)
            ? $"{Line1}, {City}, {State} {PostalCode}, {CountryCode}"
            : $"{Line1}, {Line2}, {City}, {State} {PostalCode}, {CountryCode}";
}
