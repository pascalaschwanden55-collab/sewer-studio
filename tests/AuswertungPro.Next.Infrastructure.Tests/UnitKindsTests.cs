using AuswertungPro.Next.Application.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class UnitKindsTests
{
    [Theory]
    [InlineData("m")]
    [InlineData("M")]
    [InlineData("lfm")]
    [InlineData("Meter")]
    public void IsLength_erkennt_laengeneinheiten(string unit)
        => Assert.True(UnitKinds.IsLength(unit));

    [Theory]
    [InlineData("h")]
    [InlineData("Std")]
    [InlineData("Stunden")]
    public void IsHour_erkennt_stundeneinheiten(string unit)
        => Assert.True(UnitKinds.IsHour(unit));

    [Theory]
    [InlineData("Stk")]
    [InlineData("Stck.")]
    [InlineData("Stueck")]
    [InlineData("St\u00fcck")]
    public void IsPiece_erkennt_stueckeinheiten(string unit)
        => Assert.True(UnitKinds.IsPiece(unit));
}
