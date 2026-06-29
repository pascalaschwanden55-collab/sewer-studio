using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer den gemeinsamen Helfer <see cref="CodeCatalogNormalization"/>.
/// Sichert die semantische Aequivalenz zwischen der frueheren Inline-Implementierung
/// in JsonCodeCatalogProvider und ManifestCodeCatalogProvider ab.
/// </summary>
public sealed class CodeCatalogNormalizationTests
{
    // ── NormalizeCode ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("  bab  ", "BAB")]
    [InlineData("bca", "BCA")]
    [InlineData("BAB", "BAB")]
    [InlineData("  BaB  ", "BAB")]
    public void NormalizeCode_TrimtUndUpperInvariant(string? input, string expected)
    {
        Assert.Equal(expected, CodeCatalogNormalization.NormalizeCode(input));
    }

    // ── NormalizeCodeDefinition ───────────────────────────────────────────────

    [Fact]
    public void NormalizeCodeDefinition_NormalisiertCodeUndTitle()
    {
        var def = new CodeDefinition
        {
            Code = " bab ",
            Title = "  Riss  ",
            Group = "  Risse  "
        };

        var result = CodeCatalogNormalization.NormalizeCodeDefinition(def);

        Assert.Equal("BAB", result.Code);
        Assert.Equal("Riss", result.Title);
        Assert.Equal("Risse", result.Group);
    }

    [Fact]
    public void NormalizeCodeDefinition_LeererGroup_WirdUnbekannt()
    {
        var def = new CodeDefinition { Code = "BCA", Title = "Anschluss", Group = "   " };
        var result = CodeCatalogNormalization.NormalizeCodeDefinition(def);
        Assert.Equal("Unbekannt", result.Group);
    }

    [Fact]
    public void NormalizeCodeDefinition_CanonicalCode_FaelltAufCodeZurueck_WennLeer()
    {
        var def = new CodeDefinition { Code = "BCD", Title = "Rohranfang", CanonicalCode = "" };
        var result = CodeCatalogNormalization.NormalizeCodeDefinition(def);
        Assert.Equal("BCD", result.CanonicalCode);
    }

    [Fact]
    public void NormalizeCodeDefinition_CanonicalCode_WirdNormalisiert_WennGesetzt()
    {
        var def = new CodeDefinition { Code = "BCE", Title = "Rohrende", CanonicalCode = "  bce  " };
        var result = CodeCatalogNormalization.NormalizeCodeDefinition(def);
        Assert.Equal("BCE", result.CanonicalCode);
    }

    [Fact]
    public void NormalizeCodeDefinition_NegativesRangeThresholdM_WirdNull()
    {
        var def = new CodeDefinition { Code = "BAB", Title = "Riss", RangeThresholdM = -1.0 };
        var result = CodeCatalogNormalization.NormalizeCodeDefinition(def);
        Assert.Null(result.RangeThresholdM);
    }

    [Fact]
    public void NormalizeCodeDefinition_PositivesRangeThresholdM_BleibtErhalten()
    {
        var def = new CodeDefinition { Code = "BAB", Title = "Riss", RangeThresholdM = 2.5 };
        var result = CodeCatalogNormalization.NormalizeCodeDefinition(def);
        Assert.Equal(2.5, result.RangeThresholdM);
    }

    [Fact]
    public void NormalizeCodeDefinition_CategoryPath_FiltertLeerEintraege()
    {
        var def = new CodeDefinition
        {
            Code = "BAF",
            Title = "Schaden",
            CategoryPath = new List<string> { "  ", "Strukturell", "" }
        };
        var result = CodeCatalogNormalization.NormalizeCodeDefinition(def);
        Assert.Equal(new[] { "Strukturell" }, result.CategoryPath);
    }

    // ── NormalizeCodes ────────────────────────────────────────────────────────

    [Fact]
    public void NormalizeCodes_NullEingabe_LiefertLeereListe()
    {
        var result = CodeCatalogNormalization.NormalizeCodes(null!);
        Assert.Empty(result);
    }

    [Fact]
    public void NormalizeCodes_NormalisiertAlleEintraege()
    {
        var codes = new List<CodeDefinition>
        {
            new() { Code = " bab ", Title = " Riss " },
            new() { Code = "bca", Title = "Anschluss" }
        };

        var result = CodeCatalogNormalization.NormalizeCodes(codes);

        Assert.Equal(2, result.Count);
        Assert.Equal("BAB", result[0].Code);
        Assert.Equal("BCA", result[1].Code);
    }

    // ── AllowedCodes ─────────────────────────────────────────────────────────

    [Fact]
    public void AllowedCodes_SchliestNichtSelektierbarAus()
    {
        var codes = new List<CodeDefinition>
        {
            new() { Code = "BAB", Title = "Riss", IsSelectable = true },
            new() { Code = "BCA", Title = "Anschluss", IsSelectable = false }
        };

        var result = CodeCatalogNormalization.AllowedCodes(codes);

        Assert.Contains("BAB", result);
        Assert.DoesNotContain("BCA", result);
    }

    [Fact]
    public void AllowedCodes_SchliestObservedExtensionAus()
    {
        var codes = new List<CodeDefinition>
        {
            new() { Code = "BAB", Title = "Riss", IsSelectable = true, IsObservedExtension = false },
            new() { Code = "EXT", Title = "Erweiterung", IsSelectable = true, IsObservedExtension = true }
        };

        var result = CodeCatalogNormalization.AllowedCodes(codes);

        Assert.Contains("BAB", result);
        Assert.DoesNotContain("EXT", result);
    }

    [Fact]
    public void AllowedCodes_EntferntDuplicate_UndSortiertAlphabetisch()
    {
        var codes = new List<CodeDefinition>
        {
            new() { Code = "BAB", Title = "Riss", IsSelectable = true },
            new() { Code = "bab", Title = "Riss2", IsSelectable = true },
            new() { Code = "BCA", Title = "Anschluss", IsSelectable = true }
        };

        var result = CodeCatalogNormalization.AllowedCodes(codes);

        // Duplikat entfernt, alphabetisch sortiert
        Assert.Equal(2, result.Count);
        Assert.Equal("BAB", result[0]);
        Assert.Equal("BCA", result[1]);
    }
}
