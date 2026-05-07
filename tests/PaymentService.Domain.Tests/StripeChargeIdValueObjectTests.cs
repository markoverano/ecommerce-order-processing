using PaymentService.Domain.ValueObjects;
using Xunit;

namespace PaymentService.Domain.Tests;

public sealed class StripeChargeIdValueObjectTests
{
    [Fact]
    public void From_WithValidValue_CreatesInstance()
    {
        var id = StripeChargeId.From("ch_3test123");

        Assert.Equal("ch_3test123", id.Value);
    }

    [Fact]
    public void From_WithNullValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StripeChargeId.From(null!));
    }

    [Fact]
    public void From_WithEmptyValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StripeChargeId.From(string.Empty));
    }

    [Fact]
    public void From_WithWhitespace_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => StripeChargeId.From("   "));
    }

    [Fact]
    public void EqualInstances_WithSameValue_AreEqual()
    {
        var a = StripeChargeId.From("ch_abc");
        var b = StripeChargeId.From("ch_abc");

        Assert.Equal(a, b);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var id = StripeChargeId.From("ch_xyz");

        Assert.Equal("ch_xyz", id.ToString());
    }
}
