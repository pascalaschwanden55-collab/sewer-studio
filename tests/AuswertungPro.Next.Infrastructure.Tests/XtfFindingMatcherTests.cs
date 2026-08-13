using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Zuordnung der gespeicherten Befunde zu den Kanalschaden-Elementen der Original-XTF.
/// Mehrdeutiges wird nie geraten, sondern sichtbar gemacht.
/// </summary>
public sealed class XtfFindingMatcherTests
{
    [Fact]
    public void Neuer_Bestand_wird_ueber_die_Kennung_zugeordnet()
    {
        var record = Haltung("59220-10.1036545", anker: "U1");
        record.VsaFindings.Add(Befund("BCD", 0.00, tid: "S1"));

        var ergebnis = XtfFindingMatcher.Match(record, new[] { Element("S1", "U1", "59220-10.1036545", "BCD", 0.00) });

        var treffer = Assert.Single(ergebnis.Zugeordnet);
        Assert.Equal("S1", treffer.Element.KanalschadenTid);
        Assert.Equal(XtfZuordnungsArt.UeberHerkunft, treffer.Art);
        Assert.True(ergebnis.Vollstaendig);
    }

    [Fact]
    public void Altbestand_ohne_Kennung_wird_ueber_den_Inhalt_zugeordnet()
    {
        var record = Haltung("59220-10.1036545");
        record.VsaFindings.Add(Befund("BAF", 12.34));

        var ergebnis = XtfFindingMatcher.Match(
            record,
            new[] { Element("S9", "U9", "59220-10.1036545", "BAF", 12.34) });

        var treffer = Assert.Single(ergebnis.Zugeordnet);
        Assert.Equal("S9", treffer.Element.KanalschadenTid);
        Assert.Equal(XtfZuordnungsArt.UeberInhalt, treffer.Art);
    }

    [Fact]
    public void Der_Anker_hat_Vorrang_vor_dem_Haltungsnamen()
    {
        // Zwei Untersuchungen tragen denselben Haltungsnamen. Der Anker entscheidet.
        var record = Haltung("06-001", anker: "U2");
        record.VsaFindings.Add(Befund("BAB", 5.00));

        var ergebnis = XtfFindingMatcher.Match(
            record,
            new[]
            {
                Element("A1", "U1", "06-001", "BAB", 5.00),
                Element("B1", "U2", "06-001", "BAB", 5.00)
            });

        Assert.Equal("B1", Assert.Single(ergebnis.Zugeordnet).Element.KanalschadenTid);
    }

    [Fact]
    public void Zwei_gleiche_Befunde_am_selben_Meter_bleiben_mehrdeutig()
    {
        var record = Haltung("06-001");
        record.VsaFindings.Add(Befund("BAB", 5.00));
        record.VsaFindings.Add(Befund("BAB", 5.00));

        var ergebnis = XtfFindingMatcher.Match(
            record,
            new[]
            {
                Element("A1", "U1", "06-001", "BAB", 5.00),
                Element("A2", "U1", "06-001", "BAB", 5.00)
            });

        Assert.Empty(ergebnis.Zugeordnet);
        Assert.Equal(2, ergebnis.Mehrdeutig.Count);
        Assert.False(ergebnis.Vollstaendig);
    }

    [Fact]
    public void Der_Videozaehlerstand_trennt_sonst_gleiche_Befunde()
    {
        var record = Haltung("06-001");
        record.VsaFindings.Add(Befund("BAB", 5.00, video: "00:00:15:00"));
        record.VsaFindings.Add(Befund("BAB", 5.00, video: "00:01:20:00"));

        var ergebnis = XtfFindingMatcher.Match(
            record,
            new[]
            {
                Element("A1", "U1", "06-001", "BAB", 5.00, video: "00:00:15:00"),
                Element("A2", "U1", "06-001", "BAB", 5.00, video: "00:01:20:00")
            });

        Assert.Equal(2, ergebnis.Zugeordnet.Count);
        Assert.True(ergebnis.Vollstaendig);
    }

    [Fact]
    public void Ein_Befund_ohne_Gegenstueck_wird_gemeldet_statt_geraten()
    {
        var record = Haltung("06-001");
        record.VsaFindings.Add(Befund("BAB", 5.00));

        var ergebnis = XtfFindingMatcher.Match(
            record,
            new[] { Element("A1", "U1", "06-001", "BAB", 9.99) });

        Assert.Empty(ergebnis.Zugeordnet);
        Assert.Single(ergebnis.OhneTreffer);
        Assert.Single(ergebnis.NichtVerwendet);
    }

    [Fact]
    public void Ein_Element_ohne_Befund_erscheint_als_nicht_verwendet()
    {
        var record = Haltung("06-001");
        record.VsaFindings.Add(Befund("BAB", 5.00));

        var ergebnis = XtfFindingMatcher.Match(
            record,
            new[]
            {
                Element("A1", "U1", "06-001", "BAB", 5.00),
                Element("A2", "U1", "06-001", "BBC", 7.00)
            });

        Assert.Single(ergebnis.Zugeordnet);
        Assert.Equal("A2", Assert.Single(ergebnis.NichtVerwendet).KanalschadenTid);
    }

    // Das Ergebnis darf nicht davon abhaengen, in welcher Reihenfolge die Befunde stehen.
    [Fact]
    public void Die_Reihenfolge_der_Befunde_aendert_das_Ergebnis_nicht()
    {
        var elemente = new[]
        {
            Element("A1", "U1", "06-001", "BAB", 5.00),
            Element("A2", "U1", "06-001", "BBC", 7.00)
        };

        var vorwaerts = Haltung("06-001");
        vorwaerts.VsaFindings.Add(Befund("BAB", 5.00));
        vorwaerts.VsaFindings.Add(Befund("BBC", 7.00));

        var rueckwaerts = Haltung("06-001");
        rueckwaerts.VsaFindings.Add(Befund("BBC", 7.00));
        rueckwaerts.VsaFindings.Add(Befund("BAB", 5.00));

        var a = XtfFindingMatcher.Match(vorwaerts, elemente);
        var b = XtfFindingMatcher.Match(rueckwaerts, elemente);

        Assert.Equal(
            a.Zugeordnet.Select(z => z.Element.KanalschadenTid).OrderBy(x => x, StringComparer.Ordinal),
            b.Zugeordnet.Select(z => z.Element.KanalschadenTid).OrderBy(x => x, StringComparer.Ordinal));
        Assert.True(a.Vollstaendig && b.Vollstaendig);
    }

    [Fact]
    public void Eine_fremde_Haltung_wird_nicht_zugeordnet()
    {
        var record = Haltung("06-001");
        record.VsaFindings.Add(Befund("BAB", 5.00));

        var ergebnis = XtfFindingMatcher.Match(
            record,
            new[] { Element("A1", "U1", "99-999", "BAB", 5.00) });

        Assert.Empty(ergebnis.Zugeordnet);
        Assert.Single(ergebnis.OhneTreffer);
        Assert.Empty(ergebnis.NichtVerwendet);
    }

    [Fact]
    public void Ohne_Befunde_und_ohne_Elemente_ist_das_Ergebnis_leer_und_vollstaendig()
    {
        var ergebnis = XtfFindingMatcher.Match(Haltung("06-001"), Array.Empty<XtfKanalschadenElement>());

        Assert.Empty(ergebnis.Zugeordnet);
        Assert.True(ergebnis.Vollstaendig);
    }

    [Fact]
    public void Die_Zuordnung_veraendert_weder_Befund_noch_Haltung()
    {
        var record = Haltung("06-001");
        var befund = Befund("BAB", 5.00);
        record.VsaFindings.Add(befund);
        var vorher = record.ModifiedAtUtc;

        XtfFindingMatcher.Match(record, new[] { Element("A1", "U1", "06-001", "BAB", 5.00) });

        Assert.Null(befund.KanalschadenTid);
        Assert.Equal(vorher, record.ModifiedAtUtc);
    }

    private static HaltungRecord Haltung(string name, string? anker = null)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Xtf, userEdited: false);
        if (anker is not null)
            record.XtfHerkunft = new XtfHerkunft { Datei = "test.xtf", Modell = "VSA_KEK_2020_LV95", UntersuchungTid = anker };
        return record;
    }

    private static VsaFinding Befund(string code, double? meter, string? tid = null, string? video = null)
        => new()
        {
            KanalSchadencode = code,
            MeterStart = meter,
            KanalschadenTid = tid,
            MPEG = video
        };

    private static XtfKanalschadenElement Element(
        string tid,
        string untersuchung,
        string haltung,
        string code,
        double? distanz,
        string? video = null)
        => new(tid, untersuchung, haltung, code, distanz, video);
}
