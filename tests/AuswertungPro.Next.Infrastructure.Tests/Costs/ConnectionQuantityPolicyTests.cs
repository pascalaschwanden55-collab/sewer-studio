using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests.Costs;

public sealed class ConnectionQuantityPolicyTests
{
    [Fact]
    public void Evaluate_ignoriert_nicht_anschluss_position()
    {
        var result = ConnectionQuantityPolicy.Evaluate(
            "POSITION",
            "Normale Position",
            currentQuantity: 4m,
            selected: true,
            connectionCount: 0m);

        Assert.False(result.IsApplicable);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void Evaluate_deaktiviert_bei_nicht_positiver_anzahl(int connectionCount)
    {
        var result = ConnectionQuantityPolicy.Evaluate(
            "ANSCHLUSS_A",
            "Position",
            currentQuantity: 4m,
            selected: true,
            connectionCount);

        Assert.True(result.IsApplicable);
        Assert.True(result.ShouldDisable);
        Assert.False(result.ShouldReactivate);
        Assert.Equal(0m, result.SuggestedQuantity);
    }

    [Fact]
    public void Evaluate_liefert_positive_menge_ohne_manuellen_override()
    {
        var result = ConnectionQuantityPolicy.Evaluate(
            "POSITION",
            "Hausanschluss reparieren",
            currentQuantity: 1m,
            selected: true,
            connectionCount: 3m);

        Assert.True(result.IsApplicable);
        Assert.False(result.ShouldDisable);
        Assert.False(result.ShouldReactivate);
        Assert.Equal(3m, result.SuggestedQuantity);
        Assert.Equal(3m, ConnectionQuantityPolicy.ResolveSuggestedQuantity(result, isQuantityOverridden: false));
    }

    [Fact]
    public void Evaluate_bewahrt_override_und_reaktiviert_deaktivierte_null_zeile()
    {
        var result = ConnectionQuantityPolicy.Evaluate(
            "ANSCHLUSS_A",
            "Position",
            currentQuantity: 0m,
            selected: false,
            connectionCount: 3m);

        Assert.True(result.IsApplicable);
        Assert.False(result.ShouldDisable);
        Assert.True(result.ShouldReactivate);
        Assert.Equal(3m, result.SuggestedQuantity);
        Assert.Null(ConnectionQuantityPolicy.ResolveSuggestedQuantity(result, isQuantityOverridden: true));
    }
}
