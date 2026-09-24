using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Pruefung 22.09.2026: Die Doppelpruefung der Sanierungsmassnahmen verglich nur Art, Status und Verfahren.
/// Zwei Reparaturen 2020 und 2026 galten als eine — die zweite wurde weder ins WebGIS gesendet noch geholt.
/// Jetzt zaehlt das Jahr mit, aber vorsichtig: Nur zwei BEKANNTE, verschiedene Jahre machen eine neue Massnahme.
/// Fehlt ein Jahr, bleibt es «schon vorhanden» — lieber eine Massnahme zu wenig als eine doppelt im Kataster.
/// </summary>
public sealed class WebGisMassnahmenJahrTests
{
    // ---- Die Liste im WebGIS: erste Spalte «Beginn» ----

    [Fact]
    public void Liste_liefert_den_beginn_der_massnahme()
    {
        using var doc = JsonDocument.Parse(
            "{\"values\":[[\"01.01.2020\",\"Reparatur\",\"Ausgeführt\",\"Vermörtelung\",\"g1\"]," +
            "[null,\"Renovierung\",\"Ausgeführt\",\"Schlauchverfahren\",\"g2\"]]}");

        var zeilen = GeonisWebGisClient.SanierungsZeilen(doc.RootElement).ToList();

        Assert.Equal("01.01.2020", zeilen[0].Beginn);
        Assert.Equal("2020", zeilen[0].Jahr);
        Assert.Null(zeilen[1].Beginn);
        Assert.Null(zeilen[1].Jahr);
    }

    [Theory]
    [InlineData("01.01.2020", "2020")]
    [InlineData("2020-01-01T00:00:00.000Z", "2020")]
    [InlineData("1577836800000", "2020")] // Millisekunden seit 1970 (01.01.2020 UTC)
    [InlineData("", null)]
    [InlineData("Unbekannt", null)]
    public void Jahr_aus_dem_beginn(string beginn, string? jahr)
        => Assert.Equal(jahr, new WebGisSanierungZeile { Beginn = beginn }.Jahr);

    // ---- Senden: Plan ----

    private static WebGisSanierungKatalog Katalog()
    {
        var k = new WebGisSanierungKatalog();
        k.Setze(WebGisSanierungFeldkarte.ArtRef, new[] { ("2", "Reparatur"), ("4", "Renovierung") });
        k.Setze(WebGisSanierungFeldkarte.StatusRef, new[] { ("1", "Ausgeführt") });
        k.Setze(WebGisSanierungFeldkarte.VerfahrenRef, new[] { ("33", "Vermörtelung") }, "2");
        return k;
    }

    private static ObjektAkte Reparatur(string? jahr)
    {
        var a = new ObjektAkte { Art = "sanierung" };
        a.Werte[WebGisSanierungFeldkarte.AkteArt] = new ObjektFeldWert { Text = "Reparatur" };
        a.Werte[WebGisSanierungFeldkarte.AkteStatus] = new ObjektFeldWert { Text = "Ausgeführt" };
        a.Werte[WebGisSanierungFeldkarte.AkteVerfahren] = new ObjektFeldWert { Text = "Vermörtelung" };
        if (jahr is not null) a.Werte[WebGisSanierungFeldkarte.AkteJahr] = new ObjektFeldWert { Text = jahr };
        return a;
    }

    private static WebGisLesestand StandMitReparatur(string? beginn)
    {
        var s = new WebGisLesestand { GlobalId = "P1", Bezeichnung = "80478" };
        s.Sanierungen.Add(new WebGisSanierungZeile
        {
            Beginn = beginn, Art = "Reparatur", Status = "Ausgeführt", Verfahren = "Vermörtelung", GlobalId = "alt",
        });
        return s;
    }

    [Fact]
    public void Gleiche_reparatur_aus_einem_anderen_jahr_wird_angelegt()
    {
        var pos = WebGisSanierungPlanBuilder.Baue(Reparatur("2026"), WebGisObjektart.Schacht, "80478", StandMitReparatur("01.01.2020"), Katalog());

        Assert.True(pos.Schreibbar, string.Join(" | ", pos.Sperren));
        Assert.Contains(pos.Hinweise, h => h.Contains("2020") && h.Contains("2026"));
    }

    [Fact]
    public void Gleiche_reparatur_aus_demselben_jahr_ist_schon_vorhanden()
    {
        var pos = WebGisSanierungPlanBuilder.Baue(Reparatur("2020"), WebGisObjektart.Schacht, "80478", StandMitReparatur("01.01.2020"), Katalog());

        Assert.False(pos.Schreibbar);
        Assert.Contains(pos.Sperren, s => s.Contains("bereits vorhanden"));
    }

    [Theory]
    [InlineData("2026", null)]          // WebGIS-Zeile ohne Beginn
    [InlineData(null, "01.01.2020")]    // Akte ohne Jahr
    [InlineData("2026", "Unbekannt")]   // Beginn nicht lesbar
    public void Ohne_beide_jahre_bleibt_es_schon_vorhanden(string? akteJahr, string? beginn)
    {
        var pos = WebGisSanierungPlanBuilder.Baue(Reparatur(akteJahr), WebGisObjektart.Schacht, "80478", StandMitReparatur(beginn), Katalog());

        Assert.False(pos.Schreibbar);
        Assert.Contains(pos.Sperren, s => s.Contains("bereits vorhanden"));
    }

    // ---- Holen ----

    private static WebGisLesestand Massnahme(string datum)
    {
        var s = new WebGisLesestand { GlobalId = "M", Bezeichnung = "" };
        void Combo(string refId, string key, string text)
        {
            s.Felder[refId] = key;
            s.Kataloge[refId] = new List<(string, string)> { (key, text) };
        }
        Combo(WebGisSanierungFeldkarte.ArtRef, "4", "Renovierung");
        Combo(WebGisSanierungFeldkarte.StatusRef, "1", "Ausgeführt");
        Combo(WebGisSanierungFeldkarte.VerfahrenRef, "27", "Schlauchverfahren");
        s.Felder[WebGisSanierungFeldkarte.SanierungsjahrRef] = datum;
        return s;
    }

    private static (Project Projekt, HaltungRecord Haltung) ProjektMitRenovierung(string datum)
    {
        var h = new HaltungRecord();
        var p = new Project();
        p.Data.Add(h);
        Assert.True(WebGisSanierungImportRegel.LegeAn(p,
            WebGisSanierungImportRegel.Baue(WebGisObjektart.Haltung, h.Id, "H1", "M1", Massnahme(datum))));
        return (p, h);
    }

    [Theory]
    [InlineData("01.01.2020", false)]
    [InlineData("01.01.2026", true)]
    [InlineData(null, true)]
    public void Holen_vergleicht_das_jahr_der_liste(string? beginn, bool vorhanden)
    {
        var (p, h) = ProjektMitRenovierung("01.01.2026");
        var zeile = new WebGisSanierungZeile
        {
            GlobalId = "M9", Beginn = beginn, Art = "Renovierung", Status = "Ausgeführt", Verfahren = "Schlauchverfahren",
        };

        Assert.Equal(vorhanden, WebGisSanierungImportRegel.SchonVorhanden(p.Objektakten, h.Id, zeile));
    }

    [Fact]
    public void Holen_legt_die_renovierung_eines_anderen_jahres_an()
    {
        var (p, h) = ProjektMitRenovierung("01.01.2020");
        var zweite = WebGisSanierungImportRegel.Baue(WebGisObjektart.Haltung, h.Id, "H1", "M2", Massnahme("01.01.2026"));

        Assert.True(WebGisSanierungImportRegel.LegeAn(p, zweite));
        Assert.Equal(2, p.Objektakten.Count);
    }

    [Fact]
    public void Holen_legt_dieselbe_renovierung_nicht_zweimal_an()
    {
        var (p, h) = ProjektMitRenovierung("01.01.2026");
        var gleiche = WebGisSanierungImportRegel.Baue(WebGisObjektart.Haltung, h.Id, "H1", "M2", Massnahme("01.01.2026"));

        Assert.False(WebGisSanierungImportRegel.LegeAn(p, gleiche));
        Assert.Single(p.Objektakten);
    }
}
