using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Uhrlage eines Anschlusses als Winkel ab dem Auslauf (12 Uhr = 0), wie SchachtPro sie in
/// seine Tabelle schreibt (Goeschenen 8705: A1 12, E1 4, E2 6, E3 7).
/// </summary>
public sealed class SchachtUhrlageTests
{
    [Theory]
    [InlineData("12", 0)]
    [InlineData("0", 0)]
    [InlineData("4", 120)]
    [InlineData("6", 180)]
    [InlineData("7", 210)]
    [InlineData(" 07 ", 210)]
    [InlineData("4:30", 135)]
    [InlineData("4.5", 135)]
    [InlineData("4,5", 135)]
    [InlineData("4.25", 127.5)]
    [InlineData("7 Uhr", 210)]
    public void Liest_Stunden_und_Bruchteile(string uhr, double erwartet)
    {
        Assert.Equal(erwartet, SchachtUhrlage.Grad(uhr)!.Value, 3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("13")]
    [InlineData("-1")]
    [InlineData("Norden")]
    [InlineData("12:61")]
    public void Unlesbares_oder_Unmoegliches_ergibt_null(string? uhr)
    {
        Assert.Null(SchachtUhrlage.Grad(uhr));
    }
}
