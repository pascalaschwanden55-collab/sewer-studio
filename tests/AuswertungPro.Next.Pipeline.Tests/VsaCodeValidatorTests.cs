using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class VsaCodeValidatorTests
{
    [Theory]
    [InlineData("BAB")]
    [InlineData("BABBB")]
    [InlineData("BBAA")]
    [InlineData("bca.eb")]
    public void IsKnownCode_accepts_known_main_code_and_subcodes(string code)
    {
        Assert.True(VsaCodeValidator.IsKnownCode(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("BA")]
    [InlineData("BB")]
    [InlineData("ABC")]
    [InlineData("XY")]
    [InlineData("BBZ")]
    [InlineData("BA-")]
    [InlineData("B A B")]
    public void IsKnownCode_rejects_groups_unknown_codes_and_noise(string code)
    {
        Assert.False(VsaCodeValidator.IsKnownCode(code));
    }

    [Theory]
    [InlineData("BCA.F.A", "BCAFA")]   // Punkt-Trenner entfernen
    [InlineData("BCD0.00.0", "BCD")]    // Meter-Suffix abschneiden
    [InlineData("BCAFA0.00.0", "BCAFA")] // Punkte + Meter gemischt
    [InlineData("bca.eb", "BCAEB")]     // Grossschreibung
    [InlineData("  BAB  ", "BAB")]      // Trim
    public void TryNormalizeKnownCode_repairs_dot_and_meter_artifacts(string raw, string expected)
    {
        Assert.Equal(expected, VsaCodeValidator.TryNormalizeKnownCode(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0.00.0")]   // nur Meter, kein Code
    [InlineData("XYZ123")]   // unbekannte Gruppe
    [InlineData("BB")]       // zu kurz / kein Hauptcode
    public void TryNormalizeKnownCode_returns_null_for_unknown_or_noise(string? raw)
    {
        Assert.Null(VsaCodeValidator.TryNormalizeKnownCode(raw));
    }

    [Theory]
    [InlineData("BCAFAFOO")]  // gueltiger Hauptcode BCA, aber Untercode-Muell -> > 5 Zeichen
    [InlineData("BCDXYZ")]     // gueltiger Hauptcode BCD, aber > 5 Zeichen
    [InlineData("BCA.F.A.FOO")] // mit Punkten getarnter Muell -> nach Normalisierung > 5
    public void TryNormalizeKnownCode_rejects_valid_main_code_with_junk_tail(string raw)
    {
        Assert.Null(VsaCodeValidator.TryNormalizeKnownCode(raw));
    }
}
