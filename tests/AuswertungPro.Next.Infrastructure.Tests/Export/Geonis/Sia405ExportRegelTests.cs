using AuswertungPro.Next.Application.Export.Geonis;

namespace AuswertungPro.Next.Infrastructure.Tests.Export.Geonis;

/// <summary>Reine Regeln des Rueckschriebs: Schluesselvergleich, Masse und Attributreihenfolge.</summary>
public sealed class Sia405ExportRegelTests
{
    [Theory]
    [InlineData("78998-79002", "78998-79002")]
    [InlineData(" 78998 - 79002 ", "78998-79002")]
    [InlineData("78998/79002", "78998-79002")]
    [InlineData("78998–79002", "78998-79002")]
    [InlineData("abc-def", "ABC-DEF")]
    public void NameKey_vergleichtProjektUndKatasterGleich(string eingabe, string erwartet)
        => Assert.Equal(erwartet, Sia405NameKey.Normalize(eingabe));

    [Theory]
    [InlineData("300", 300)]
    [InlineData("300 mm", 300)]
    [InlineData("30 cm", 300)]
    [InlineData("0.30 m", 300)]
    public void Massparser_liestEindeutigeEinzelmasse(string eingabe, int erwartet)
        => Assert.Equal(erwartet, Sia405MassParser.LiesMillimeter(eingabe));

    [Theory]
    [InlineData("")]
    [InlineData("unbekannt")]
    [InlineData("300 bis 400")]
    [InlineData("0")]
    public void Massparser_raetNieBeiUnklaremText(string eingabe)
        => Assert.Null(Sia405MassParser.LiesMillimeter(eingabe));

    [Theory]
    [InlineData("1100 x 900 mm", 1100, 900)]
    [InlineData("900 x 1100 mm", 1100, 900)]
    [InlineData("1000 mm", 1000, 1000)]
    public void Massparser_liestSchachtmassMitGroesseremWertZuerst(string eingabe, int d1, int d2)
    {
        var mass = Sia405MassParser.LiesSchachtmass(eingabe);

        Assert.True(mass.HasValue);
        Assert.Equal(d1, mass!.Value.Dimension1);
        Assert.Equal(d2, mass.Value.Dimension2);
    }

    [Fact]
    public void Massparser_liefertKeinSchachtmassBeiUnklaremText()
        => Assert.False(Sia405MassParser.LiesSchachtmass("rund, gross").HasValue);

    [Fact]
    public void Attributreihenfolge_fuegtFehlendesElementAnDieModellstelleEin()
    {
        var reihenfolge = new Sia405AttributReihenfolge();
        reihenfolge.Beobachte("Kanal", new[] { "OBJ_ID", "Bezeichnung", "Letzte_Aenderung" });
        reihenfolge.Beobachte("Kanal", new[] { "OBJ_ID", "Bezeichnung", "Baulicher_Zustand", "Letzte_Aenderung" });

        var vorhanden = new[] { "OBJ_ID", "Bezeichnung", "Letzte_Aenderung" };
        Assert.Equal(2, reihenfolge.IndexFuerEinfuegen("Kanal", vorhanden, "Baulicher_Zustand"));
    }

    [Fact]
    public void Attributreihenfolge_haengtUnbekanntesAnsEnde()
    {
        var reihenfolge = new Sia405AttributReihenfolge();
        reihenfolge.Beobachte("Kanal", new[] { "OBJ_ID", "Bezeichnung" });

        var vorhanden = new[] { "OBJ_ID", "Bezeichnung" };
        Assert.Equal(2, reihenfolge.IndexFuerEinfuegen("Kanal", vorhanden, "Bemerkung"));
        Assert.Equal(0, reihenfolge.IndexFuerEinfuegen("Unbekannt", Array.Empty<string>(), "Bemerkung"));
    }

    [Fact]
    public void Datum_folgtDerSchreibweiseDerQuelldatei()
    {
        var datum = new DateOnly(2026, 9, 4);
        Assert.Equal("2026-09-04", Sia405ExportPlanBuilder.FormatiereDatum(datum, "2020-01-01"));
        Assert.Equal("20260904", Sia405ExportPlanBuilder.FormatiereDatum(datum, "20200101"));
        Assert.Equal("2026-09-04", Sia405ExportPlanBuilder.FormatiereDatum(datum, null));
    }

    [Fact]
    public void Bemerkung_wirdEinzeilig()
        => Assert.Equal("Zeile eins / Zeile zwei", Sia405ExportPlanBuilder.Einzeilig("Zeile eins\r\nZeile zwei"));
}
