using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>Charakterisierungs-Tests fuer DamageSymbolClassifier (IST-Verhalten).</summary>
public sealed class DamageSymbolClassifierTests
{
    // --- ResolveDamageSymbolCategory ---

    [Theory]
    [InlineData("BAA",  "deformation")]
    [InlineData("BAAB", "deformation")]
    [InlineData("BAB",  "crack")]
    [InlineData("BABB", "crack")]
    [InlineData("BAC",  "break")]
    [InlineData("BAD",  "leak")]
    [InlineData("BAE",  "offset")]
    [InlineData("BAF",  "surface")]
    [InlineData("BAH",  "offset")]
    [InlineData("BAI",  "obstacle")]
    [InlineData("BAJ",  "offset")]
    [InlineData("BAK",  "infiltration")]
    [InlineData("BAL",  "exfiltration")]
    [InlineData("BBA",  "roots")]
    [InlineData("BBB",  "incrustation")]
    [InlineData("BBC",  "deposit")]
    public void ResolveDamageSymbolCategory_bekannte_praefix_liefern_korrekte_kategorie(string code, string expectedCategory)
        => Assert.Equal(expectedCategory, DamageSymbolClassifier.ResolveDamageSymbolCategory(code));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("BCD")]   // Bestandsaufnahme, kein Schaden
    [InlineData("UNBEKANNT")]
    public void ResolveDamageSymbolCategory_unbekannte_codes_liefern_default(string? code)
        => Assert.Equal("default", DamageSymbolClassifier.ResolveDamageSymbolCategory(code));

    [Fact]
    public void ResolveDamageSymbolCategory_ist_case_insensitiv()
        => Assert.Equal("crack", DamageSymbolClassifier.ResolveDamageSymbolCategory("bab"));

    [Fact]
    public void ResolveDamageSymbolCategory_ignoriert_fuehrende_leerzeichen()
        => Assert.Equal("crack", DamageSymbolClassifier.ResolveDamageSymbolCategory("  BAB "));

    // --- GetDamageSymbolColor ---

    [Theory]
    [InlineData("crack",        "#D64541")]
    [InlineData("break",        "#D64541")]
    [InlineData("deformation",  "#E67E22")]
    [InlineData("offset",       "#E67E22")]
    [InlineData("surface",      "#E67E22")]
    [InlineData("leak",         "#2196F3")]
    [InlineData("infiltration", "#2196F3")]
    [InlineData("exfiltration", "#2196F3")]
    [InlineData("roots",        "#27AE60")]
    [InlineData("incrustation", "#8B6914")]
    [InlineData("deposit",      "#8B6914")]
    [InlineData("obstacle",     "#6B7280")]
    public void GetDamageSymbolColor_bekannte_kategorien_liefern_erwartete_farbe(string category, string expectedColor)
        => Assert.Equal(expectedColor, DamageSymbolClassifier.GetDamageSymbolColor(category));

    [Fact]
    public void GetDamageSymbolColor_default_kategorie_gibt_fallback_zurueck()
        => Assert.Equal("#006E9C", DamageSymbolClassifier.GetDamageSymbolColor("default"));

    [Fact]
    public void GetDamageSymbolColor_unbekannte_kategorie_gibt_fallback_zurueck()
        => Assert.Equal("#AABBCC", DamageSymbolClassifier.GetDamageSymbolColor("xyz", "#AABBCC"));

    [Fact]
    public void GetDamageSymbolColor_standard_fallback_ist_006E9C()
        => Assert.Equal("#006E9C", DamageSymbolClassifier.GetDamageSymbolColor("unbekannt"));
}
