using System;
using AuswertungPro.Next.Application.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

public sealed class WebGisExportPlanBuilderTests
{
    private static WebGisLesestand Stand(WebGisObjektart art, string? zustand, string? sanbedarf, string? bem, string? baujahr = null)
    {
        var f = new System.Collections.Generic.Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [WebGisFeldkarte.ZustandRef(art)] = zustand,
            [WebGisFeldkarte.SanierungsbedarfRef(art)] = sanbedarf,
            [WebGisFeldkarte.BemerkungRef(art)] = bem,
        };
        if (art == WebGisObjektart.Haltung) f[WebGisFeldkarte.HaltungBaujahrRef] = baujahr;
        return new WebGisLesestand { GlobalId = "G1", Bezeichnung = "X", Felder = f };
    }

    [Fact]
    public void Ohne_stand_wird_gesperrt()
    {
        var e = new WebGisObjektEingabe { Objektart = WebGisObjektart.Haltung, Bezeichnung = "H", Zustandsklasse = "4" };
        var pos = WebGisExportPlanBuilder.Baue(e, null);
        Assert.False(pos.Schreibbar);
        Assert.Single(pos.Sperren);
    }

    [Fact]
    public void Saniert_setzt_zustand_z4_sanierungsbedarf_saniert_und_bemerkung()
    {
        var e = new WebGisObjektEingabe
        {
            Objektart = WebGisObjektart.Haltung, Bezeichnung = "525145-505377", RecordId = Guid.NewGuid(),
            Zustandsklasse = "4", Bemerkung = "Saniert mit Liner 2026", Saniert = true,
        };
        var stand = Stand(WebGisObjektart.Haltung, "102", "103", "");
        var pos = WebGisExportPlanBuilder.Baue(e, stand);

        Assert.True(pos.Schreibbar);
        Assert.Contains(pos.Aenderungen, a => a.Feld == "Zustand" && a.Neu == "104");
        Assert.Contains(pos.Aenderungen, a => a.Feld == "Sanierungsbedarf" && a.Neu == "106");
        Assert.Contains(pos.Aenderungen, a => a.Feld == "Bemerkung" && a.Neu == "Saniert mit Liner 2026");
    }

    [Fact]
    public void Bemerkung_wird_zusammengefuehrt_nicht_ueberschrieben()
    {
        var e = new WebGisObjektEingabe
        {
            Objektart = WebGisObjektart.Schacht, Bezeichnung = "525145", RecordId = Guid.NewGuid(),
            Zustandsklasse = "3", Bemerkung = "Saniert 2026", Saniert = false,
        };
        var stand = Stand(WebGisObjektart.Schacht, "103", "103", "Tiefe 1.80m");
        var pos = WebGisExportPlanBuilder.Baue(e, stand);

        var bem = pos.Aenderungen.Find(a => a.Feld == "Bemerkung");
        Assert.NotNull(bem);
        Assert.Equal("Tiefe 1.80m · Saniert 2026", bem!.Neu);
        // nicht saniert: Sanierungsbedarf NICHT gesetzt, aber Hinweis
        Assert.DoesNotContain(pos.Aenderungen, a => a.Feld == "Sanierungsbedarf");
        Assert.Contains(pos.Hinweise, h => h.Contains("keine ausgefuehrte Sanierungs-Akte"));
    }

    [Fact]
    public void Keine_aenderung_wenn_stand_schon_passt()
    {
        var e = new WebGisObjektEingabe
        {
            Objektart = WebGisObjektart.Haltung, Bezeichnung = "H", RecordId = Guid.NewGuid(),
            Zustandsklasse = "4", Bemerkung = "Saniert mit Liner 2026", Saniert = true,
        };
        var stand = Stand(WebGisObjektart.Haltung, "104", "106", "Saniert mit Liner 2026");
        var pos = WebGisExportPlanBuilder.Baue(e, stand);
        Assert.Empty(pos.Aenderungen);
        Assert.False(pos.Schreibbar);
    }

    [Fact]
    public void Baujahr_nur_wenn_webgis_leer()
    {
        var e = new WebGisObjektEingabe
        {
            Objektart = WebGisObjektart.Haltung, Bezeichnung = "H", RecordId = Guid.NewGuid(),
            Zustandsklasse = "2", Baujahr = "1963", Saniert = false,
        };
        var leer = WebGisExportPlanBuilder.Baue(e, Stand(WebGisObjektart.Haltung, "102", "103", "", baujahr: ""));
        Assert.Contains(leer.Aenderungen, a => a.Feld == "Baujahr" && a.Neu == "1963");

        var gefuellt = WebGisExportPlanBuilder.Baue(e, Stand(WebGisObjektart.Haltung, "102", "103", "", baujahr: "1970"));
        Assert.DoesNotContain(gefuellt.Aenderungen, a => a.Feld == "Baujahr");
    }

    [Fact]
    public void Laenge_wird_nie_geschrieben()
    {
        var e = new WebGisObjektEingabe
        {
            Objektart = WebGisObjektart.Haltung, Bezeichnung = "H", RecordId = Guid.NewGuid(),
            Zustandsklasse = "4", Saniert = true, Bemerkung = "Saniert mit Liner 2026",
        };
        var pos = WebGisExportPlanBuilder.Baue(e, Stand(WebGisObjektart.Haltung, "102", "103", ""));
        Assert.DoesNotContain(pos.Aenderungen, a =>
            a.RefId == WebGisFeldkarte.HaltungLaengeRohrRef || a.RefId == WebGisFeldkarte.HaltungLaengeGeomRef);
    }
}
