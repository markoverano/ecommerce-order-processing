using ECommerceOrderProcessing.Shared.ValueObjects;
using Xunit;

namespace OrderService.Domain.Tests;

public sealed class MoneyValueObjectTests
{
    [Fact]
    public void Create_WithPositiveAmount_Succeeds()
    {
        var money = Money.Create(10.00m, "USD");

        Assert.Equal(10.00m, money.Amount);
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void Create_NormalizesCurrencyToUpperCase()
    {
        var money = Money.Create(10.00m, "usd");

        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void Create_WithZeroAmount_Succeeds()
    {
        var money = Money.Create(0m, "USD");

        Assert.Equal(0m, money.Amount);
    }

    [Fact]
    public void Create_WithNegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.Create(-1m, "USD"));
    }

    [Fact]
    public void Create_WithEmptyCurrency_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Money.Create(10m, ""));
    }

    [Fact]
    public void Zero_ReturnsZeroAmountWithCurrency()
    {
        var zero = Money.Zero("EUR");

        Assert.Equal(0m, zero.Amount);
        Assert.Equal("EUR", zero.Currency);
    }

    [Fact]
    public void Add_SameCurrency_ReturnsSum()
    {
        var a = Money.Create(10m, "USD");
        var b = Money.Create(5m, "USD");

        var result = a.Add(b);

        Assert.Equal(Money.Create(15m, "USD"), result);
    }

    [Fact]
    public void Add_DifferentCurrencies_ThrowsInvalidOperationException()
    {
        var usd = Money.Create(10m, "USD");
        var eur = Money.Create(10m, "EUR");

        Assert.Throws<InvalidOperationException>(() => usd.Add(eur));
    }

    [Fact]
    public void Subtract_SameCurrency_ReturnsDifference()
    {
        var a = Money.Create(10m, "USD");
        var b = Money.Create(3m, "USD");

        var result = a.Subtract(b);

        Assert.Equal(Money.Create(7m, "USD"), result);
    }

    [Fact]
    public void Equality_SameAmountAndCurrency_AreEqual()
    {
        var a = Money.Create(10m, "USD");
        var b = Money.Create(10m, "USD");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentAmount_AreNotEqual()
    {
        var a = Money.Create(10m, "USD");
        var b = Money.Create(11m, "USD");

        Assert.NotEqual(a, b);
    }
}
