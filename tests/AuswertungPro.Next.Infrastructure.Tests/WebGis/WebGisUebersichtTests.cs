using System;
using AuswertungPro.Next.Application.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Die Uebersicht gruppiert nach Objekt und buendelt Gleichartiges — damit die Liste
/// nachvollziehbar bleibt (Wunsch Pascal 21.09.2026: 48 Einzelwarnungen waren unlesbar).
/// </summary>
public sealed class WebGisUebersichtTests
{
    private static WebGisExportPlan Plan()
    {
        var plan = new WebGisExportPlan();

        var s1 = new WebGisExportPosition { Objektart = WebGisObjektart.Schacht, Bezeichnung = "505377", GlobalId = "G1" };
        s1.Aenderungen.Add(new WebGisFeldAenderung { RefId = "r1", Feld = "Material", Alt = "Beton, unbekannt", Neu = "104", NeuText = "Beton, Fertigteil" });
        s1.Aenderungen.Add(new WebGisFeldAenderung { RefId = "r2", Feld = "Tiefe [m]", Alt = "2.07", Neu = "1.82" });
        plan.Positionen.Add(s1);

        var s2 = new WebGisExportPosition { Objektart = WebGisObjektart.Schacht, Bezeichnung = "525145", GlobalId = "G2" };
        s2.Hinweise.Add("Bemerkung nennt 'saniert', aber es gibt keine ausgefuehrte Sanierungs-Akte — Sanierungsbedarf NICHT gesetzt.");
        plan.Positionen.Add(s2);

        var s3 = new WebGisExportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = "80462-80461" };
        s3.Sperren.Add("Im WebGIS nicht eindeutig gefunden.");
        plan.Positionen.Add(s3);

        var unveraendert = new WebGisExportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = "59435-60284", GlobalId = "G3" };
        plan.Positionen.Add(unveraendert);

        // Eine neue Massnahme und drei, die es schon gibt.
        var neu = new WebGisSanierungPosition { Objektart = WebGisObjektart.Schacht, ElternBezeichnung = "505377", ElternGlobalId = "G1", AkteId = Guid.NewGuid() };
        neu.Felder["x"] = "2"; neu.Anzeige.Add("Art: Reparatur"); neu.Anzeige.Add("Verfahren: Vermörtelung");
        plan.Sanierungen.Add(neu);
        for (var i = 0; i < 3; i++)
        {
            var alt = new WebGisSanierungPosition { Objektart = WebGisObjektart.Haltung, ElternBezeichnung = "H" + i, ElternGlobalId = "G", AkteId = Guid.NewGuid() };
            alt.Sperren.Add("Im WebGIS bereits vorhanden (Renovierung / Ausgeführt / Schlauchverfahren) — nicht doppelt angelegt.");
            plan.Sanierungen.Add(alt);
        }
        return plan;
    }

    [Fact]
    public void Gruppiert_je_objekt_und_zeigt_unveraenderte_nicht()
    {
        var u = WebGisUebersicht.Aus(Plan());

        Assert.Equal(3, u.Objekte.Count); // 505377, 525145, 80462-80461 — 59435-60284 faellt weg
        var s = u.Objekte.Find(o => o.Objekt == "Schacht 505377");
        Assert.NotNull(s);
        Assert.Equal(3, s!.Zeilen.Count); // Material, Tiefe, neue Massnahme
        Assert.Equal("3 Änderungen", s.Kurz);
        Assert.Contains(s.Zeilen, z => z.Feld == "Material" && z.Alt == "Beton, unbekannt" && z.Neu == "Beton, Fertigteil");
        Assert.Contains(s.Zeilen, z => z.Art == WebGisZeilenart.NeueMassnahme && z.Neu.Contains("Vermörtelung"));
    }

    [Fact]
    public void Gleichartige_meldungen_werden_gebuendelt_statt_je_objekt_wiederholt()
    {
        var u = WebGisUebersicht.Aus(Plan());

        Assert.DoesNotContain(u.Objekte, o => o.Objekt.StartsWith("Haltung H", StringComparison.Ordinal));
        Assert.Contains(u.Sammelmeldungen, m => m.StartsWith("3 Sanierungsmassnahmen sind im WebGIS bereits vorhanden", StringComparison.Ordinal));
        Assert.Contains(u.Sammelmeldungen, m => m.Contains("keine Sanierungs-Akte"));
    }

    [Fact]
    public void Zahlen_und_sortierung_stimmen()
    {
        var u = WebGisUebersicht.Aus(Plan());

        Assert.Equal(1, u.ObjekteMitAenderung);
        Assert.Equal(1, u.NeueMassnahmen);
        Assert.Equal(1, u.Gesperrt);
        Assert.Equal(1, u.MitHinweis);
        Assert.Equal("Schacht 505377", u.Objekte[0].Objekt);          // Änderungen zuerst
        Assert.Equal("Haltung 80462-80461", u.Objekte[1].Objekt);      // dann Sperren
        Assert.Equal("Schacht 525145", u.Objekte[2].Objekt);           // dann reine Hinweise
        Assert.Contains("1 Objekt wird geändert", u.Kopfzeile);
        Assert.False(u.NichtsZuTun);
    }

    [Fact]
    public void Ergebnisansicht_markiert_geschriebenes()
    {
        var plan = Plan();
        plan.Positionen[0].Geschrieben = true;
        plan.Sanierungen[0].Geschrieben = true;

        var u = WebGisUebersicht.Aus(plan, ergebnis: true);

        Assert.Equal("WebGIS-Übertragung — Ergebnis", u.Titel);
        Assert.Contains(u.Objekte[0].Zeilen, z => z.Art == WebGisZeilenart.Erledigt);
        Assert.Contains("1 Objekte geschrieben", u.Kopfzeile);
    }

    [Fact]
    public void Leerer_plan_sagt_nichts_zu_tun()
    {
        var u = WebGisUebersicht.Aus(new WebGisExportPlan());
        Assert.Empty(u.Objekte);
        Assert.True(u.NichtsZuTun);
    }
}
