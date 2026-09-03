using AuswertungPro.Next.Application.UseCases.Xtf;
using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Die Vorschau vor dem Schreiben: eine Zusammenfassung in einer Zeile, eine Tabelle
/// Objekt / Feld / Original / Neuer Wert, hoechstens drei sichtbare Warnungen und der
/// vollstaendige Bericht als Details. Reine Darstellung des Plans, keine Dateiarbeit.
/// </summary>
public sealed class XtfExportVorschauTests
{
    [Fact]
    public void Zusammenfassung_zaehlt_Objekte_ueber_alle_Plaene()
    {
        var vorschau = XtfExportVorschau.AusRevision([Plan(
            Stammdaten("78998-79002", "Haltung", ("Material", "Steinzeug", "Zement")),
            Stammdaten("78998", "Schacht", ("BaulicherZustand", null, "Z4")),
            Befund(XtfRevisionAenderung.Neu, "BAB", 12.5, ("Code", null, "BAB")),
            Befund(XtfRevisionAenderung.Entfernt, "BBA", 3.0),
            Befund(XtfRevisionAenderung.Unveraendert, "BCA", 1.0))], "Bericht");

        Assert.Equal("2 Objekte geändert · 1 neu · 1 entfernt", vorschau.Zusammenfassung);
        Assert.Equal("Bericht", vorschau.Details);
        Assert.False(vorschau.IstFehler);
    }

    [Fact]
    public void Zeilen_zeigen_Objekt_Feld_Original_und_neuen_Wert_in_Klartext()
    {
        var vorschau = XtfExportVorschau.AusRevision([Plan(
            Stammdaten("78998-79002", "Haltung", ("Lichte_Hoehe", "150", "100"), ("Material", "Steinzeug", "Zement")),
            Stammdaten("78998", "Schacht", ("BaulicherZustand", null, "Z4")))], "");

        Assert.Collection(vorschau.Zeilen,
            z => { Assert.Equal("Haltung 78998-79002", z.Objekt); Assert.Equal("Lichte Höhe", z.Feld); Assert.Equal("150", z.Alt); Assert.Equal("100", z.Neu); },
            z => { Assert.Equal("Haltung 78998-79002", z.Objekt); Assert.Equal("Material", z.Feld); Assert.Equal("Steinzeug", z.Alt); Assert.Equal("Zement", z.Neu); },
            z => { Assert.Equal("Schacht 78998", z.Objekt); Assert.Equal("Baulicher Zustand", z.Feld); Assert.Equal("–", z.Alt); Assert.Equal("Z4", z.Neu); });
    }

    [Fact]
    public void Dimension_1_und_2_desselben_Objekts_werden_zu_einer_Zeile()
    {
        var vorschau = XtfExportVorschau.AusRevision([Plan(
            Stammdaten("78998", "Schacht", ("Dimension1", "500", "1100"), ("Dimension2", "500", "900")))], "");

        var zeile = Assert.Single(vorschau.Zeilen);
        Assert.Equal("Dimension", zeile.Feld);
        Assert.Equal("500 × 500", zeile.Alt);
        Assert.Equal("1100 × 900", zeile.Neu);
    }

    [Fact]
    public void Befunde_nennen_Code_und_Meter_und_Entfernen_ist_sichtbar()
    {
        var vorschau = XtfExportVorschau.AusRevision([Plan(
            Befund(XtfRevisionAenderung.Geaendert, "BAB", 12.5, ("Quantifizierung1", "2", "3")),
            Befund(XtfRevisionAenderung.Entfernt, "BBA", 3.0),
            Befund(XtfRevisionAenderung.Neu, "BCA", 20.0, ("Code", null, "BCA")))], "");

        Assert.Collection(vorschau.Zeilen,
            z => { Assert.Equal("Befund BAB bei 12.5 m (78998-79002)", z.Objekt); Assert.Equal("Quantifizierung 1", z.Feld); },
            z => { Assert.Equal("Befund BBA bei 3.0 m (78998-79002)", z.Objekt); Assert.Equal("Befund", z.Feld); Assert.Equal("vorhanden", z.Alt); Assert.Equal("(entfernt)", z.Neu); },
            z => { Assert.Equal("Befund BCA bei 20.0 m (78998-79002)", z.Objekt); Assert.Equal("Code", z.Feld); Assert.Equal("–", z.Alt); Assert.Equal("BCA", z.Neu); });
    }

    [Fact]
    public void Hoechstens_drei_Warnungen_sind_sichtbar_der_Rest_wird_gezaehlt()
    {
        var plan = new XtfRevisionPlan("q.xtf", [], ["eins", "zwei", "drei", "vier", "fuenf"]);
        var vorschau = XtfExportVorschau.AusRevision([plan], "");

        Assert.Equal(5, vorschau.Warnungen.Count);
        Assert.Equal(["eins", "zwei", "drei", "… und 2 weitere (siehe Details)"], vorschau.KurzeWarnungen);
    }

    [Fact]
    public void Fehler_Vorschau_hat_keine_Tabelle_und_traegt_den_Grund_kurz()
    {
        var vorschau = XtfExportVorschau.Fehler("Katasterdaten aktualisieren", "seilergasse.xtf: offene Faelle — die Pruefung ist nicht bestanden.", "voller Bericht");

        Assert.True(vorschau.IstFehler);
        Assert.Empty(vorschau.Zeilen);
        Assert.Equal("seilergasse.xtf: offene Faelle — die Pruefung ist nicht bestanden.", vorschau.Zusammenfassung);
        Assert.Equal("voller Bericht", vorschau.Details);
    }

    private static XtfRevisionPlan Plan(params XtfRevisionPosition[] positionen)
        => new("q.xtf", positionen, []);

    private static XtfRevisionPosition Stammdaten(string name, string objekt, params (string Feld, string? Alt, string? Neu)[] felder)
        => new(XtfRevisionAenderung.Geaendert, "tid", "", name, "", null,
            felder.Select(f => new XtfRevisionFeld(f.Feld, f.Alt, f.Neu)).ToList(), Objekt: objekt);

    private static XtfRevisionPosition Befund(XtfRevisionAenderung art, string code, double meter, params (string Feld, string? Alt, string? Neu)[] felder)
        => new(art, art == XtfRevisionAenderung.Neu ? null : "tid", "U1", "78998-79002", code, meter,
            felder.Select(f => new XtfRevisionFeld(f.Feld, f.Alt, f.Neu)).ToList());
}
