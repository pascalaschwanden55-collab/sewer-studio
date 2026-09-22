using System;
using AuswertungPro.Next.Application.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Das Aenderungslog entsteht je Schritt, nicht erst am Ende — nach einem Abbruch muss
/// drinstehen, welche Objekte schon geschrieben sind (Pruefung 22.09.2026). Der Fensterkopf
/// zaehlt nur wirklich Geschriebenes (am 21.09. 15:57 stand «13 Objekte geschrieben», es waren 5).
/// «Jetzt schreiben» schreibt nur einen Plan, den der Bearbeiter so gesehen hat.
/// </summary>
public sealed class WebGisAblaufBerichtTests
{
    private static readonly DateTime Zeit = new(2026, 9, 22, 9, 15, 30);

    private static WebGisExportPosition Position(string bezeichnung, string gid = "G1")
    {
        var p = new WebGisExportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = bezeichnung, GlobalId = gid };
        p.Aenderungen.Add(new WebGisFeldAenderung { RefId = "r1", Feld = "Zustand", Alt = "102", AltText = "Mittlere Maengel (Z2)", Neu = "104", NeuText = "Keine Maengel (Z4)" });
        p.Aenderungen.Add(new WebGisFeldAenderung { RefId = "r2", Feld = "Bemerkung", Alt = "", Neu = "Saniert 2026" });
        return p;
    }

    private static WebGisSanierungPosition Massnahme(string eltern, string art = "Renovierung")
    {
        var s = new WebGisSanierungPosition { Objektart = WebGisObjektart.Haltung, ElternBezeichnung = eltern, ElternGlobalId = "G1", AkteId = Guid.NewGuid() };
        s.Felder["a"] = "4"; s.Anzeige.Add("Art: " + art); s.Anzeige.Add("Status: Ausgeführt");
        return s;
    }

    // ---------------- Log je Schritt ----------------

    [Fact]
    public void LogZeile_geschriebenes_objekt_hat_eine_zeile_je_feld_mit_OK()
    {
        var p = Position("525145-505377");
        p.Geschrieben = true;

        var text = WebGisExportBericht.LogZeile(p, Zeit);

        Assert.Contains("22.09.2026 09:15:30 | Haltung 525145-505377 | Zustand | 102 (Mittlere Maengel (Z2)) → 104 (Keine Maengel (Z4)) | OK", text);
        Assert.Contains("22.09.2026 09:15:30 | Haltung 525145-505377 | Bemerkung | – → Saniert 2026 | OK", text);
    }

    [Fact]
    public void LogZeile_fehlgeschlagenes_objekt_nennt_felder_und_grund()
    {
        var p = Position("525145-505377");
        p.SchreibFehler = "Schreibfehler: Verbindung unterbrochen";

        var text = WebGisExportBericht.LogZeile(p, Zeit);

        Assert.Contains("| Haltung 525145-505377 | Zustand, Bemerkung | nicht geschrieben | FEHLER: Schreibfehler: Verbindung unterbrochen", text);
    }

    [Fact]
    public void LogZeile_angelegte_massnahme_nennt_die_neue_id()
    {
        var s = Massnahme("525145-505377");
        s.Geschrieben = true; s.NeueId = "66931";

        Assert.Contains("| Haltung 525145-505377 | Sanierungsmassnahme angelegt (ID 66931) | – → Art: Renovierung, Status: Ausgeführt | OK",
            WebGisExportBericht.LogZeile(s, Zeit));
    }

    [Fact]
    public void LogStart_und_LogAbbruch_zeigen_wo_der_lauf_stand()
    {
        var plan = new WebGisExportPlan();
        var p1 = Position("H1", "G1"); p1.Geschrieben = true;
        var p2 = Position("H2", "G2"); p2.SchreibFehler = "WebGIS-Sitzung abgelaufen — nicht geschrieben (Token abgelaufen).";
        var p3 = Position("H3", "G3");
        plan.Positionen.Add(p1); plan.Positionen.Add(p2); plan.Positionen.Add(p3);

        var start = WebGisExportBericht.LogStart(Zeit, "pascal.aschwanden", plan);
        Assert.Contains("===== 22.09.2026 09:15:30 | WebGIS-Übertragung | pascal.aschwanden | gestartet: 3 Objekte, 0 Sanierungsmassnahmen", start);

        var abbruch = WebGisExportBericht.LogAbbruch(Zeit, "Token abgelaufen", plan);
        Assert.Contains("===== 22.09.2026 09:15:30 | ABGEBROCHEN | Token abgelaufen", abbruch);
        Assert.Contains("1 Objekte geschrieben", abbruch);
        Assert.Contains("1 fehlgeschlagen", abbruch);

        var ende = WebGisExportBericht.LogAbschluss(Zeit, plan);
        Assert.Contains("===== 22.09.2026 09:15:30 | abgeschlossen | ", ende);
    }

    // ---------------- Ergebniskopf ----------------

    [Fact]
    public void Ergebniskopf_zaehlt_nur_wirklich_geschriebene_objekte()
    {
        var plan = new WebGisExportPlan();
        var p1 = Position("H1", "G1"); p1.Geschrieben = true;
        var p2 = Position("H2", "G2"); p2.SchreibFehler = "HTTP 500";
        plan.Positionen.Add(p1); plan.Positionen.Add(p2);

        var u = WebGisUebersicht.Aus(plan, ergebnis: true);

        Assert.True(u.IstErgebnis);
        Assert.Equal(1, u.Geschrieben);
        Assert.Contains("1 Objekte geschrieben", u.Kopfzeile);
        Assert.Contains("1 nicht geschrieben", u.Kopfzeile);
        Assert.DoesNotContain("2 Objekte geschrieben", u.Kopfzeile);
    }

    // ---------------- Bestaetigter Plan gegen frischen Plan ----------------

    private static WebGisExportPlan PlanMit(params WebGisExportPosition[] positionen)
    {
        var plan = new WebGisExportPlan();
        foreach (var p in positionen) plan.Positionen.Add(p);
        return plan;
    }

    [Fact]
    public void Gleicher_inhalt_gilt_als_gleicher_plan()
    {
        var a = PlanMit(Position("H1"), Position("H2", "G2"));
        a.Sanierungen.Add(Massnahme("H1"));
        var b = PlanMit(Position("H1"), Position("H2", "G2"));
        b.Sanierungen.Add(Massnahme("H1"));

        Assert.True(WebGisPlanVergleich.Gleich(a, b));
    }

    [Fact]
    public void Anderer_neuer_wert_ist_ein_anderer_plan()
    {
        var a = PlanMit(Position("H1"));
        var b = PlanMit(Position("H1"));
        b.Positionen[0].Aenderungen[1] = new WebGisFeldAenderung { RefId = "r2", Feld = "Bemerkung", Alt = "", Neu = "Saniert 2025" };

        Assert.False(WebGisPlanVergleich.Gleich(a, b));
    }

    [Fact]
    public void Neu_gesperrtes_objekt_ist_ein_anderer_plan()
    {
        var a = PlanMit(Position("H1"));
        var b = PlanMit(Position("H1"));
        b.Positionen[0].Sperren.Add("Im WebGIS nicht eindeutig gefunden.");

        Assert.False(WebGisPlanVergleich.Gleich(a, b)); // der Bearbeiter hat H1 als «wird geschrieben» gesehen
    }

    [Fact]
    public void Andere_massnahme_ist_ein_anderer_plan()
    {
        var a = PlanMit(Position("H1")); a.Sanierungen.Add(Massnahme("H1", "Renovierung"));
        var b = PlanMit(Position("H1")); b.Sanierungen.Add(Massnahme("H1", "Reparatur"));

        Assert.False(WebGisPlanVergleich.Gleich(a, b));
    }

    [Fact]
    public void Nur_hinweise_aendern_den_plan_nicht()
    {
        var a = PlanMit(Position("H1"));
        var b = PlanMit(Position("H1"));
        b.Positionen[0].Hinweise.Add("Bemerkung nennt 'saniert' …");

        Assert.True(WebGisPlanVergleich.Gleich(a, b)); // geschrieben wird dasselbe
    }
}
