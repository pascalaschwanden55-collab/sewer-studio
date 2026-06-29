using AuswertungPro.Next.Infrastructure.Import.Ibak;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests für IbakStammdatenKey.Normalize —
/// sichert das gemeinsame Normalisierungsverhalten für PDF-, XTF- und FDB-Schlüssel.
/// </summary>
public sealed class IbakStammdatenKeyTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void Normalize_returns_null_for_empty_input(string? input, string? expected)
    {
        Assert.Equal(expected, IbakStammdatenKey.Normalize(input));
    }

    [Theory]
    [InlineData("36262-36275",    "36262-36275")]   // bereits normalisiert
    [InlineData("36262/36275",    "36262-36275")]   // Schrägstrich -> Bindestrich
    [InlineData("36262–36275",    "36262-36275")]   // En-Dash -> Bindestrich
    [InlineData("36262—36275",    "36262-36275")]   // Em-Dash -> Bindestrich
    [InlineData("36 262-36 275",  "36262-36275")]   // Leerzeichen im Innern entfernen
    [InlineData(" 100-200 ",      "100-200")]        // führende/nachfolgende Leerzeichen
    [InlineData("100 / 200",      "100-200")]        // Leerzeichen + Schrägstrich
    public void Normalize_applies_rules_correctly(string input, string expected)
    {
        Assert.Equal(expected, IbakStammdatenKey.Normalize(input));
    }
}
