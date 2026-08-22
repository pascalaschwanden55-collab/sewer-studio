using AuswertungPro.Next.Infrastructure.Export.Excel;

namespace AuswertungPro.Next.Infrastructure.Tests.Export;

/// <summary>
/// Im Excel-Bericht steht die ausgeschriebene VSA-Form. Die Kanal-TV-Software liefert
/// Kurzcodes (PP, STZ, Z), die gespeicherten Daten bleiben unveraendert - umgeschrieben
/// wird nur die Anzeige.
///
/// Unbekanntes bleibt stehen: lieber ein Kurzcode im Bericht als eine erfundene
/// Bezeichnung, die nach Norm aussieht.
/// </summary>
public sealed class ExcelMaterialLangformTests
{
    [Theory]
    [InlineData("PP", "Polypropylen")]
    [InlineData("PVC", "Polyvinylchlorid")]
    [InlineData("PE", "Polyethylen")]
    [InlineData("HDPE", "Hartpolyethylen")]
    [InlineData("STZ", "Steinzeug")]
    [InlineData("Z", "Zement")]
    [InlineData("GUS", "Guss")]
    [InlineData("FZ", "Faserzement")]
    public void Kurzcode_wird_ausgeschrieben(string kurz, string erwartet)
    {
        Assert.Equal(erwartet, ExcelMaterialLangform.Auflösen(kurz));
    }

    [Theory]
    [InlineData("Polypropylen")]
    [InlineData("Steinzeug")]
    [InlineData("Polyvinylchlorid")]
    public void Bereits_ausgeschriebenes_bleibt_unveraendert(string lang)
    {
        Assert.Equal(lang, ExcelMaterialLangform.Auflösen(lang));
    }

    [Fact]
    public void Beton_variante_wird_auf_beton_zurueckgefuehrt()
    {
        Assert.Equal("Beton", ExcelMaterialLangform.Auflösen("Beton_u"));
    }

    [Theory]
    [InlineData("Inliner")]
    [InlineData("Irgendwas Neues")]
    public void Unbekanntes_wird_nicht_erfunden(string wert)
    {
        Assert.Equal(wert, ExcelMaterialLangform.Auflösen(wert));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Leerwerte_bleiben_leer(string? wert)
    {
        Assert.Equal(string.Empty, ExcelMaterialLangform.Auflösen(wert));
    }

    [Fact]
    public void Gross_und_kleinschreibung_spielt_keine_rolle()
    {
        Assert.Equal("Polypropylen", ExcelMaterialLangform.Auflösen("pp"));
        Assert.Equal("Steinzeug", ExcelMaterialLangform.Auflösen("stz"));
    }
}
