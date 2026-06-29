using AuswertungPro.Next.Infrastructure.Import.Common;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Charakterisierungstests fuer ContinuousDefectCodeResolver.
/// Sichert das IST-Verhalten aus WinCanDbImportService.ResolveEffectiveCode
/// und IbakExportImportService.ResolveEffectiveCode.
/// </summary>
public class ContinuousDefectCodeResolverTests
{
    // --- Marker-Regex ---

    [Theory]
    [InlineData("A01", true)]
    [InlineData("B02", true)]
    [InlineData("A99", true)]
    [InlineData("B00", true)]
    [InlineData("BBC", false)]
    [InlineData("A1", false)]
    [InlineData("AB01", false)]
    [InlineData("", false)]
    public void ContinuousDefectMarkerRegex_ErkennungKorrekt(string code, bool erwartet)
        => Assert.Equal(erwartet,
            ContinuousDefectCodeResolver.ContinuousDefectMarkerRegex.IsMatch(code));

    // --- EmbeddedVsaCode-Regex ---

    [Theory]
    [InlineData("BBC Harte Ablagerungen", "BBC")]
    [InlineData("BBCC (Harte Ablagerungen)", "BBCC")]
    [InlineData("BAB Riss", "BAB")]
    public void EmbeddedVsaCodeRegex_ExtrahiertCode(string beschreibung, string erwartetCode)
    {
        var m = ContinuousDefectCodeResolver.EmbeddedVsaCodeRegex.Match(beschreibung.Trim());
        Assert.True(m.Success);
        Assert.Equal(erwartetCode, m.Groups[1].Value);
    }

    // --- ResolveEffectiveCode ---

    [Fact]
    public void Resolve_KeinMarker_GibtCodeUnveraendert()
    {
        var result = ContinuousDefectCodeResolver.ResolveEffectiveCode("BBC", "Harte Ablagerungen", out var desc);
        Assert.Equal("BBC", result);
        Assert.Equal("Harte Ablagerungen", desc);
    }

    [Fact]
    public void Resolve_MarkerMitVsaCodeInBeschreibung_GibtVsaCode()
    {
        var result = ContinuousDefectCodeResolver.ResolveEffectiveCode("A01", "BBC Harte Ablagerungen", out var desc);
        Assert.Equal("BBC", result);
        // Beschreibung nach Code-Praefix
        Assert.Equal("Harte Ablagerungen", desc);
    }

    [Fact]
    public void Resolve_MarkerMitKlammern_KlammernEntfernt()
    {
        var result = ContinuousDefectCodeResolver.ResolveEffectiveCode("B02", "BBCC (Harte Ablagerungen)", out var desc);
        Assert.Equal("BBCC", result);
        Assert.Equal("Harte Ablagerungen", desc);
    }

    [Fact]
    public void Resolve_MarkerOhneBeschreibung_GibtMarkerZurueck()
    {
        var result = ContinuousDefectCodeResolver.ResolveEffectiveCode("A01", null, out var desc);
        Assert.Equal("A01", result);
        Assert.Null(desc);
    }

    [Fact]
    public void Resolve_MarkerMitLeererBeschreibung_GibtMarkerZurueck()
    {
        var result = ContinuousDefectCodeResolver.ResolveEffectiveCode("A01", "", out var desc);
        Assert.Equal("A01", result);
        Assert.Equal("", desc);
    }

    [Fact]
    public void Resolve_MarkerOhneVsaCodeInBeschreibung_GibtMarkerZurueck()
    {
        // Beschreibung beginnt nicht mit VSA-Code-Muster
        var result = ContinuousDefectCodeResolver.ResolveEffectiveCode("B01", "Anfang (Streckenschaden)", out var desc);
        Assert.Equal("B01", result);
    }

    [Fact]
    public void Resolve_MarkerMitNurCodeAlsBeschreibung_OriginalBeschreibungErhalten()
    {
        // Wenn nach dem Code-Entfernen die Beschreibung leer wird, Original beibehalten
        var result = ContinuousDefectCodeResolver.ResolveEffectiveCode("A01", "BBC", out var desc);
        Assert.Equal("BBC", result);
        // Beschreibung = leer nach Strip -> Original zurueckgegeben
        Assert.Equal("BBC", desc);
    }
}
