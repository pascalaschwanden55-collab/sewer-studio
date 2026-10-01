using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Handwerte (in SewerStudio von Hand gesetzte Felder) gehen 1:1 ins WebGIS — Combo ueber
/// den Katalog der Maske, nie ueber geratene Codes. Anlass: Schacht 80461, wo Dimension und
/// Material geaendert waren und der Export sie nicht sah (21.09.2026).
/// </summary>
public sealed class WebGisHandwertTests
{
    private static WebGisLesestand SchachtStand(
        string? material = null, string? materialDetail = null, string? masse = null, string? form = null)
    {
        var f = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [WebGisFeldkarte.SchachtZustandRef] = "103",
            [WebGisFeldkarte.SchachtSanierungsbedarfRef] = "104",
            [WebGisFeldkarte.SchachtBemerkungRef] = "",
            ["57efe6f1-4e76-3844-d13b-4c6dc0e1301f"] = material,
            ["5eeb92cf-a23f-ed9c-9ed2-cd96fdcd7728"] = materialDetail,
            ["9cbffa1a-7b06-a884-b98f-7da25dc1158a"] = masse,
            ["76620dae-1911-3d61-5eeb-66ee9cacba20"] = masse,
            ["20888829-e165-1c4f-e997-0991e22e9be0"] = form,
        };
        var k = new Dictionary<string, List<(string Key, string Text)>>(StringComparer.Ordinal)
        {
            ["57efe6f1-4e76-3844-d13b-4c6dc0e1301f"] = new() { ("0", "Unbekannt"), ("1", "Beton"), ("3", "Kunststoff") },
            ["5eeb92cf-a23f-ed9c-9ed2-cd96fdcd7728"] = new() { ("101", "Beton, unbekannt"), ("104", "Beton, Fertigteil") },
            ["9cbffa1a-7b06-a884-b98f-7da25dc1158a"] = new() { ("800", "800"), ("1000", "1000"), ("1200", "1200") },
            ["76620dae-1911-3d61-5eeb-66ee9cacba20"] = new() { ("800", "800"), ("1000", "1000"), ("1200", "1200") },
            ["20888829-e165-1c4f-e997-0991e22e9be0"] = new() { ("0", "Unbekannt"), ("1", "Rund"), ("102", "Oval") },
        };
        return new WebGisLesestand { GlobalId = "G1", Bezeichnung = "80461", Felder = f, Kataloge = k };
    }

    private static WebGisObjektEingabe Schacht(params (string Feld, string Wert)[] handwerte)
    {
        var e = new WebGisObjektEingabe
        {
            Objektart = WebGisObjektart.Schacht, Bezeichnung = "80461", RecordId = Guid.NewGuid(),
            Zustandsklasse = "3", Saniert = false,
        };
        foreach (var (f, w) in handwerte) e.Handwerte[f] = w;
        return e;
    }

    [Fact]
    public void Dimension_und_form_gehen_als_katalogschluessel()
    {
        var pos = WebGisExportPlanBuilder.Baue(
            Schacht(("Dimension 1 mm", "1000"), ("Schachtform", "Rund")), SchachtStand());

        var masse = pos.Aenderungen.Find(a => a.Feld == "Breite/Länge [mm]");
        Assert.NotNull(masse);
        Assert.Equal("1000", masse!.Neu);
        Assert.Contains(pos.Aenderungen, a => a.Feld == "Form" && a.Neu == "1");
    }

    [Fact]
    public void Material_detail_setzt_auch_die_hauptkategorie()
    {
        var pos = WebGisExportPlanBuilder.Baue(Schacht(("Material", "Beton, Fertigteil")), SchachtStand());

        Assert.Contains(pos.Aenderungen, a => a.RefId == "5eeb92cf-a23f-ed9c-9ed2-cd96fdcd7728" && a.Neu == "104");
        Assert.Contains(pos.Aenderungen, a => a.RefId == "57efe6f1-4e76-3844-d13b-4c6dc0e1301f" && a.Neu == "1");
    }

    [Fact]
    public void Gleicher_wert_im_webgis_ergibt_keine_aenderung()
    {
        var pos = WebGisExportPlanBuilder.Baue(
            Schacht(("Dimension 1 mm", "1000")), SchachtStand(masse: "1000"));

        Assert.DoesNotContain(pos.Aenderungen, a => a.Feld == "Breite/Länge [mm]");
    }

    [Fact]
    public void Unbekannter_klartext_sperrt_nur_das_feld_und_wird_gemeldet()
    {
        var pos = WebGisExportPlanBuilder.Baue(
            Schacht(("Dimension 1 mm", "1100"), ("Schachtform", "Rund")), SchachtStand());

        Assert.DoesNotContain(pos.Aenderungen, a => a.Feld == "Breite/Länge [mm]");
        Assert.Contains(pos.Hinweise, h => h.Contains("Breite/Länge [mm] «1100»"));
        Assert.Contains(pos.Aenderungen, a => a.Feld == "Form"); // die anderen laufen weiter
        Assert.Empty(pos.Sperren);
    }

    [Fact]
    public void Feld_ohne_webgis_zuordnung_wird_genannt_nicht_stumm_verworfen()
    {
        var pos = WebGisExportPlanBuilder.Baue(Schacht(("Bauwerksart", "Normschacht")), SchachtStand());

        Assert.Contains(pos.Hinweise, h => h.Contains("«Bauwerksart = Normschacht»") && h.Contains("kein WebGIS-Feld"));
    }

    [Fact]
    public void Felder_mit_eigener_regel_laufen_nicht_ueber_die_handwerte()
    {
        // Zustandsklasse/Bemerkung/Sanierungsbedarf sind auch UserEdited — sie haben eigene Regeln
        // und duerfen nicht doppelt (und ohne Zusammenfuehrung) geschrieben werden.
        // Der WebGIS-Stand traegt Z3 (103); die Eingabe muss davon abweichen, sonst gibt es zu Recht
        // keine Aenderung (so stand der Test bis 22.09.2026 rot, ohne dass das Produkt falsch war).
        var e = new WebGisObjektEingabe
        {
            Objektart = WebGisObjektart.Schacht, Bezeichnung = "80461", RecordId = Guid.NewGuid(),
            Zustandsklasse = "4", Saniert = false,
        };
        e.Handwerte["Zustandsklasse"] = "4";
        e.Handwerte["Bemerkungen"] = "Saniert 2026";
        e.Handwerte["Sanierungsbedarf"] = "Saniert";
        var pos = WebGisExportPlanBuilder.Baue(e, SchachtStand());

        Assert.DoesNotContain(pos.Hinweise, h => h.Contains("Zustandsklasse") && h.Contains("kein WebGIS-Feld"));
        Assert.DoesNotContain(pos.Hinweise, h => h.Contains("Bemerkungen") && h.Contains("kein WebGIS-Feld"));
        Assert.Single(pos.Aenderungen, a => a.Feld == "Zustand"); // genau einmal, aus der eigenen Regel
    }

    [Theory]
    [InlineData("1.80", "1.8")]   // Buerglen 22.09.: geschrieben 1.80, WebGIS speichert 1.8
    [InlineData("1.8", "1.80")]
    [InlineData("2.00", "2")]
    [InlineData("90", "90.0")]    // Rotation
    [InlineData("503.60", "503.6")] // Sohlenhoehe
    public void Gleiche_zahl_in_anderer_schreibweise_ist_keine_aenderung(string sewerStudio, string webGis)
    {
        var stand = SchachtStand();
        stand.Felder["db8b7f6e-234c-536f-7fbd-3b6d841d8ccd"] = webGis; // Tiefe [m]
        stand.Felder["ec0e9ade-9e28-1f1b-a9a3-7e5ac9ff94af"] = webGis; // Rotation
        stand.Felder["e627d56a-9401-1ca3-acee-736e061d77ed"] = webGis; // Sohlenhoehe

        var pos = WebGisExportPlanBuilder.Baue(
            Schacht(("Tiefe", sewerStudio), ("Rotation", sewerStudio), ("Sohlenhoehe", sewerStudio)), stand);

        Assert.Empty(pos.Aenderungen);
    }

    [Fact]
    public void Andere_zahl_bleibt_eine_aenderung()
    {
        var stand = SchachtStand();
        stand.Felder["db8b7f6e-234c-536f-7fbd-3b6d841d8ccd"] = "1.80";

        var pos = WebGisExportPlanBuilder.Baue(Schacht(("Tiefe", "1.85")), stand);

        Assert.Contains(pos.Aenderungen, a => a.Feld == "Tiefe [m]" && a.Neu == "1.85");
    }

    [Fact]
    public void Text_der_keine_zahl_ist_wird_weiterhin_zeichengenau_verglichen()
    {
        var stand = SchachtStand();
        stand.Felder["db8b7f6e-234c-536f-7fbd-3b6d841d8ccd"] = "unbekannt";

        var pos = WebGisExportPlanBuilder.Baue(Schacht(("Tiefe", "Unbekannt")), stand);

        Assert.Contains(pos.Aenderungen, a => a.Feld == "Tiefe [m]");
    }

    [Fact]
    public void Beide_masse_gehen_in_ihre_eigenen_felder()
    {
        // Anlass 80461: SewerStudio 1000/1000, WebGIS 800/800.
        var pos = WebGisExportPlanBuilder.Baue(
            Schacht(("Dimension 1 mm", "1000"), ("Dimension 2 mm", "1000")), SchachtStand(masse: "800"));

        Assert.Contains(pos.Aenderungen, a => a.RefId == "9cbffa1a-7b06-a884-b98f-7da25dc1158a" && a.Neu == "1000");
        Assert.Contains(pos.Aenderungen, a => a.RefId == "76620dae-1911-3d61-5eeb-66ee9cacba20" && a.Neu == "1000");
    }

    [Fact]
    public void Zusammengesetzter_wert_wird_hinter_dem_punkt_aufgeloest()
    {
        var katalog = new List<(string, string)> { ("106", "Sammelkanal"), ("3", "Hauptsammelkanal") };
        Assert.Equal("106", WebGisHandwertKarte.Schluessel(katalog, "PAA.Sammelkanal"));
    }

    [Fact]
    public void Projektinterne_felder_werden_still_uebergangen()
    {
        var pos = WebGisExportPlanBuilder.Baue(Schacht(("PDF_Path", "x/y.pdf"), ("NR.", "7")), SchachtStand());

        Assert.Empty(pos.Aenderungen);
        Assert.Empty(pos.Hinweise);
        Assert.True(WebGisHandwertKarte.NichtFuerKataster("Link"));
    }

    [Fact]
    public void Eigentuemer_und_betreiber_bleiben_beim_webgis()
    {
        var pos = WebGisExportPlanBuilder.Baue(
            Schacht(("Eigentümer", "Abwasser Uri"), ("Betreiber", "Gemeinde")), SchachtStand());

        Assert.Empty(pos.Aenderungen);
        Assert.Contains(pos.Hinweise, h => h.Contains("Eigentümer") && h.Contains("im WebGIS führend"));
        Assert.Contains(pos.Hinweise, h => h.Contains("Betreiber") && h.Contains("im WebGIS führend"));
        Assert.True(WebGisFuehrungsfelder.NieSenden("Eigentuemer"));
    }

    [Fact]
    public void Leerer_handwert_loescht_nichts()
    {
        var pos = WebGisExportPlanBuilder.Baue(
            Schacht(("Dimension 1 mm", "   ")), SchachtStand(masse: "1000"));

        Assert.DoesNotContain(pos.Aenderungen, a => a.Feld == "Breite/Länge [mm]");
        Assert.Empty(pos.Hinweise);
    }

    [Fact]
    public void Schreibweise_egal_beim_katalogabgleich()
    {
        Assert.Equal("1", WebGisHandwertKarte.Schluessel(
            new List<(string, string)> { ("1", "In Betrieb") }, "in_Betrieb"));
        Assert.Equal("2", WebGisHandwertKarte.Schluessel(
            new List<(string, string)> { ("2", "Kreisprofil (K)") }, "Kreisprofil"));
        Assert.Null(WebGisHandwertKarte.Schluessel(
            new List<(string, string)> { ("1", "Rund") }, "Eckig"));
        Assert.Equal("Beton", WebGisHandwertKarte.Hauptteil("Beton, Fertigteil"));
        Assert.True(WebGisHandwertKarte.IstEigeneRegel("Haltungslaenge_m"));
        Assert.True(WebGisHandwertKarte.IstEigeneRegel("Bemerkung"));
        Assert.False(WebGisHandwertKarte.IstEigeneRegel("Material"));
        Assert.True(WebGisHandwertKarte.IstEigeneRegel("Baujahr"));
    }
}
