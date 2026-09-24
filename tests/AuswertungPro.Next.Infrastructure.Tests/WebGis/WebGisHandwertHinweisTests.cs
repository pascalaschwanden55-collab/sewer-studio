using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Pruefung 22.09.2026, E3: Handwerte, die nicht ins WebGIS gehen, fielen ohne Hinweis weg — der Bericht sah
/// dann aus, als sei alles uebertragen. Jetzt sagt ein Hinweis, was nicht hinausgeht und warum.
/// </summary>
public sealed class WebGisHandwertHinweisTests
{
    private const string BreiteRef = "902695a4-5f44-e910-b2da-471c17085822";

    private static WebGisLesestand HaltungStand()
    {
        var f = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [WebGisFeldkarte.HaltungZustandRef] = "103",
            [WebGisFeldkarte.HaltungSanierungsbedarfRef] = "103",
            [WebGisFeldkarte.HaltungBemerkungRef] = "",
            [BreiteRef] = "200",
        };
        var k = new Dictionary<string, List<(string Key, string Text)>>(StringComparer.Ordinal)
        {
            [BreiteRef] = new() { ("200", "200"), ("250", "250"), ("300", "300") },
        };
        return new WebGisLesestand { GlobalId = "G1", Bezeichnung = "80480-80478", Felder = f, Kataloge = k };
    }

    private static WebGisObjektEingabe Haltung(bool saniert = false, string? zustand = "3", params (string Feld, string Wert)[] handwerte)
    {
        var e = new WebGisObjektEingabe
        {
            Objektart = WebGisObjektart.Haltung, Bezeichnung = "80480-80478", RecordId = Guid.NewGuid(),
            Zustandsklasse = zustand, Saniert = saniert,
        };
        foreach (var (feld, wert) in handwerte) e.Handwerte[feld] = wert;
        return e;
    }

    // DN und lichte Breite gehen beide auf «Breite [mm]». Uebertragen wird nur einer; weichen sie voneinander ab,
    // fiel der zweite bisher still weg.
    [Fact]
    public void Zwei_verschiedene_handwerte_auf_dasselbe_webgis_feld_werden_gemeldet()
    {
        var pos = WebGisExportPlanBuilder.Baue(
            Haltung(handwerte: new[] { ("DN_mm", "300"), ("Lichte_Breite_mm", "250") }), HaltungStand());

        Assert.Single(pos.Aenderungen, a => a.RefId == BreiteRef);
        Assert.Contains(pos.Hinweise, h => h.Contains("Breite [mm]") && h.Contains("DN_mm") && h.Contains("Lichte_Breite_mm"));
    }

    [Fact]
    public void Gleiche_handwerte_auf_demselben_webgis_feld_sind_kein_hinweis()
    {
        var pos = WebGisExportPlanBuilder.Baue(
            Haltung(handwerte: new[] { ("DN_mm", "300"), ("Lichte_Breite_mm", "300.0") }), HaltungStand());

        Assert.DoesNotContain(pos.Hinweise, h => h.Contains("DN_mm"));
    }

    // Den Sanierungsbedarf setzt das Senden nur als «Saniert» aus einer ausgefuehrten Sanierungsakte.
    [Fact]
    public void Sanierungsbedarf_von_hand_ohne_akte_wird_gemeldet()
    {
        var pos = WebGisExportPlanBuilder.Baue(
            Haltung(handwerte: new[] { ("Sanierungsbedarf", "Kurzfristig") }), HaltungStand());

        Assert.DoesNotContain(pos.Aenderungen, a => a.Feld == "Sanierungsbedarf");
        Assert.Contains(pos.Hinweise, h => h.StartsWith("Sanierungsbedarf", StringComparison.Ordinal) && h.Contains("Kurzfristig"));
    }

    [Fact]
    public void Sanierungsbedarf_von_hand_gegen_die_akte_wird_gemeldet()
    {
        var pos = WebGisExportPlanBuilder.Baue(
            Haltung(saniert: true, zustand: "4", ("Sanierungsbedarf", "Kurzfristig")), HaltungStand());

        Assert.Contains(pos.Aenderungen, a => a.Feld == "Sanierungsbedarf" && a.Neu == "106");
        Assert.Contains(pos.Hinweise, h => h.StartsWith("Sanierungsbedarf", StringComparison.Ordinal) && h.Contains("Kurzfristig"));
    }

    [Fact]
    public void Sanierungsbedarf_saniert_mit_akte_ist_kein_hinweis()
    {
        var pos = WebGisExportPlanBuilder.Baue(
            Haltung(saniert: true, zustand: "4", ("Sanierungsbedarf", "Saniert")), HaltungStand());

        Assert.DoesNotContain(pos.Hinweise, h => h.StartsWith("Sanierungsbedarf", StringComparison.Ordinal));
    }

    // Nach einer Sanierung gehoert eine neue Zustandsklasse dazu. Fehlt sie ganz, blieb das bisher ohne Hinweis
    // (nur eine andere Klasse als Z4 wurde gemeldet).
    [Fact]
    public void Saniert_ohne_zustandsklasse_wird_gemeldet()
    {
        var pos = WebGisExportPlanBuilder.Baue(Haltung(saniert: true, zustand: null), HaltungStand());

        Assert.Contains(pos.Aenderungen, a => a.Feld == "Sanierungsbedarf" && a.Neu == "106");
        Assert.Contains(pos.Hinweise, h => h.Contains("Saniert") && h.Contains("ohne Zustandsklasse"));
    }
}
