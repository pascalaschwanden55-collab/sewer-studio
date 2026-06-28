using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Pipeline.Tests;

// Charakterisierungs-Tests fuer StreckenschadenCodeClassifier (IST-Verhalten)
public sealed class StreckenschadenCodeClassifierTests
{
    // Exakte Treffer im HashSet
    [Theory]
    [InlineData("BAG")]
    [InlineData("BAGA")]
    [InlineData("BBA")]
    [InlineData("BBAA")]
    [InlineData("BBAB")]
    [InlineData("BBB")]
    [InlineData("BBBA")]
    [InlineData("BBC")]
    [InlineData("BBCA")]
    [InlineData("BBCB")]
    [InlineData("BBCC")]
    [InlineData("BBD")]
    [InlineData("BBDA")]
    [InlineData("BBDB")]
    [InlineData("BABA")]
    [InlineData("BAFA")]
    public void IsStreckenschadenCode_gibt_true_fuer_bekannte_codes(string code)
    {
        Assert.True(StreckenschadenCodeClassifier.IsStreckenschadenCode(code));
    }

    // Gross-/Kleinschreibung ist egal (OrdinalIgnoreCase)
    [Theory]
    [InlineData("bba")]
    [InlineData("bbca")]
    [InlineData("Baga")]
    public void IsStreckenschadenCode_ignoriert_grossschreibung(string code)
    {
        Assert.True(StreckenschadenCodeClassifier.IsStreckenschadenCode(code));
    }

    // Prefix-Match: laengerer Code trifft, wenn Prefix bekannt
    [Theory]
    [InlineData("BBCAX")]   // Prefix "BBCA" ist bekannt
    [InlineData("BBAAB")]   // Prefix "BBAA" ist bekannt
    public void IsStreckenschadenCode_trifft_per_praefix(string code)
    {
        Assert.True(StreckenschadenCodeClassifier.IsStreckenschadenCode(code));
    }

    // Keine Streckenschaeden
    [Theory]
    [InlineData("BCD")]
    [InlineData("BCE")]
    [InlineData("BCA")]
    [InlineData("BAC")]
    [InlineData("BAJ")]
    [InlineData("BBG")]
    public void IsStreckenschadenCode_gibt_false_fuer_punktschaeden(string code)
    {
        Assert.False(StreckenschadenCodeClassifier.IsStreckenschadenCode(code));
    }

    // Edge-Cases
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void IsStreckenschadenCode_gibt_false_fuer_leere_eingabe(string? code)
    {
        Assert.False(StreckenschadenCodeClassifier.IsStreckenschadenCode(code!));
    }

    // Praefix-Pruefung stoppt bei Laenge 3 (Min-Laenge)
    [Theory]
    [InlineData("BB")]   // zu kurz fuer Praefix-Pruefung, kein Treffer
    [InlineData("BA")]
    public void IsStreckenschadenCode_ignoriert_praefix_kuerzer_als_3(string code)
    {
        // "BB" und "BA" sind kein exakter Treffer und kuerzer als 3 Zeichen nicht geprueft
        Assert.False(StreckenschadenCodeClassifier.IsStreckenschadenCode(code));
    }
}
