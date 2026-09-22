using System;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Infrastructure.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

public sealed class WebGisExportBerichtTests
{
    private static WebGisExportPlan Plan()
    {
        var plan = new WebGisExportPlan();
        var h = new WebGisExportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = "525145-505377", GlobalId = "G1" };
        h.Aenderungen.Add(new WebGisFeldAenderung { RefId = "r", Feld = "Zustand", Alt = "102", Neu = "104", NeuText = "Keine Maengel (Z4)" });
        plan.Positionen.Add(h);
        var s = new WebGisExportPosition { Objektart = WebGisObjektart.Schacht, Bezeichnung = "60122" };
        s.Sperren.Add("Im WebGIS nicht eindeutig gefunden.");
        plan.Positionen.Add(s);
        var san = new WebGisSanierungPosition { Objektart = WebGisObjektart.Haltung, ElternBezeichnung = "525145-505377", ElternGlobalId = "G1", AkteId = Guid.NewGuid() };
        san.Felder["x"] = "4"; san.Anzeige.Add("Art: Renovierung"); san.Anzeige.Add("Status: Ausgeführt");
        plan.Sanierungen.Add(san);
        return plan;
    }

    [Fact]
    public void Vorschau_zeigt_aenderungen_sperren_und_massnahmen()
    {
        var v = WebGisExportBericht.Vorschau(Plan());

        Assert.False(v.IstFehler);
        Assert.Contains(v.Zeilen, z => z.Objekt == "Haltung 525145-505377" && z.Feld == "Zustand" && z.Alt == "102" && z.Neu == "Keine Maengel (Z4)");
        Assert.Contains(v.Zeilen, z => z.Feld == "Sanierungsmassnahme (neu)" && z.Neu.Contains("Renovierung"));
        Assert.Contains(v.Warnungen, w => w.Contains("Schacht 60122") && w.Contains("GESPERRT"));
        Assert.Contains("1 Objekte mit Änderungen, 1 gesperrt", v.Zusammenfassung);
        Assert.Contains("1 Sanierungsmassnahmen anzulegen", v.Zusammenfassung);
    }

    [Fact]
    public void Leerer_plan_ist_fehler_vorschau()
    {
        var v = WebGisExportBericht.Vorschau(new WebGisExportPlan());
        Assert.True(v.IstFehler);
    }

    [Fact]
    public void Ergebnis_zaehlt_geschriebene_und_fehler()
    {
        var plan = Plan();
        plan.Positionen[0].Geschrieben = true;
        plan.Sanierungen[0].SchreibFehler = "HTTP 500";

        var text = WebGisExportBericht.Ergebnis(plan);
        Assert.Contains("1 Objekte geschrieben", text);
        Assert.Contains("0 Sanierungsmassnahmen angelegt", text);
        Assert.Contains("1 fehlgeschlagen", text);

        var details = WebGisExportBericht.Details(plan, mitErgebnis: true);
        Assert.Contains("[GESCHRIEBEN] Haltung 525145-505377", details);
        Assert.Contains("[FEHLER] Haltung 525145-505377", details);
        Assert.Contains("!! HTTP 500", details);
    }

    [Fact]
    public void Log_hat_eine_zeile_je_geschriebenem_feld_und_massnahme()
    {
        var plan = Plan();
        plan.Positionen[0].Geschrieben = true;
        plan.Sanierungen[0].Geschrieben = true; plan.Sanierungen[0].NeueId = "66931";

        var log = WebGisExportBericht.Log(plan, new DateTime(2026, 9, 21, 14, 23, 36), "pascal.aschwanden");

        Assert.Contains("===== 21.09.2026 14:23:36 | WebGIS-Übertragung | pascal.aschwanden |", log);
        Assert.Contains("21.09.2026 14:23:36 | Haltung 525145-505377 | Zustand | 102 → 104 (Keine Maengel (Z4)) | OK", log);
        Assert.Contains("| Sanierungsmassnahme angelegt (ID 66931) | – → Art: Renovierung, Status: Ausgeführt | OK", log);
        Assert.Contains("| Schacht 60122 | – | übersprungen | GESPERRT:", log);
    }

    [Fact]
    public void Syn_kontext_wird_aus_editor_url_gelesen()
    {
        var url = "https://www.geohost.ch/svc/rest/services/tn_system/gnsvc2023/MapServer/exts/GEONISserver2023/attributeeditor/getLayouts?project=awu_abw&table=AWZ_UNTERHALT&synergis_jsessionid=EA2A&X-syn-login=pascal.aschwanden&X-syn-application-roles=WebOffice%2B-%2BEditing&X-syn-groups=G_awu_rw%2CG_awu_ro%2CG_awu_rw";
        var k = PlaywrightWebGisAnmeldung.KontextAusUrl(url);

        Assert.NotNull(k);
        Assert.Equal("pascal.aschwanden", k!.Login);
        Assert.Equal("WebOffice+-+Editing", k.Roles);
        Assert.Equal("G_awu_rw,G_awu_ro,G_awu_rw", k.Groups);
        Assert.Null(PlaywrightWebGisAnmeldung.KontextAusUrl("https://www.geohost.ch/divum/synserver?project=awu_abw_edit"));
    }

    [Fact]
    public void AuthQuery_sendet_rollen_wie_der_browser()
    {
        var z = new WebGisZugang
        {
            BasisUrl = "https://www.geohost.ch", Projekt = "awu_abw_edit", Datenquelle = "awu_abw",
            JSessionId = "S", SynLogin = "pascal.aschwanden", SynGroups = "G_awu_rw,G_awu_ro,G_awu_rw",
        };
        var q = z.AuthQuery();
        Assert.Contains("X-syn-application-roles=WebOffice%2B-%2BEditing", q);
        Assert.Contains("X-syn-groups=G_awu_rw%2CG_awu_ro%2CG_awu_rw", q);
    }
}
