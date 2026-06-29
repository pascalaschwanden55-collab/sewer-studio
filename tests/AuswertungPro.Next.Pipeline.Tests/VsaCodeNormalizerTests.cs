using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungstests fuer VsaCodeNormalizer.MainCode.
/// Sichert das Verhalten, das vorher in QuantificationUnitPolicy und
/// ClockPositionResolver dupliziert war.
/// </summary>
public sealed class VsaCodeNormalizerTests
{
    [Theory]
    [InlineData("BAB",    "BAB")]  // exakter Hauptcode
    [InlineData("BAB.A",  "BAB")]  // Punkt als Trenner
    [InlineData("bab",    "BAB")]  // Kleinbuchstaben
    [InlineData("BCA.EB", "BCA")]  // laengerer Code mit Punkt
    [InlineData("BCAEB",  "BCA")]  // laengerer Code ohne Punkt
    [InlineData("  BAB ", "BAB")]  // fuehrendes/nachfolgendes Whitespace
    public void MainCode_normalisiert_auf_Hauptcode(string input, string expected)
    {
        Assert.Equal(expected, VsaCodeNormalizer.MainCode(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MainCode_liefert_null_fuer_leere_Eingabe(string? input)
    {
        Assert.Null(VsaCodeNormalizer.MainCode(input));
    }

    [Theory]
    [InlineData("BA")]  // nur 2 Zeichen (nach Normierung)
    [InlineData("B")]   // nur 1 Zeichen
    public void MainCode_liefert_null_wenn_kuerzer_als_3_Zeichen(string input)
    {
        Assert.Null(VsaCodeNormalizer.MainCode(input));
    }
}
