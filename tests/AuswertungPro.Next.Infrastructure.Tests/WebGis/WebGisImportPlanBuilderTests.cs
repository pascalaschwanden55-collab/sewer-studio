using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Holen WebGIS -> SewerStudio (23.09.2026, Entscheid Pascal): leere Felder fuellen, das WebGIS
/// geht vor GeoShop (ersetzt Katasterwerte), Handwerte bleiben immer stehen.
/// </summary>
public sealed class WebGisImportPlanBuilderTests
{
    private const string StatusRef = "7ecf9743-e8df-c35d-fdce-1a188b72bef8";
    private const string FunktionRef = "85770f0e-245b-a1a6-c768-dfcd1c460de9";
    private const string MaterialDetailRef = "e0850080-43aa-9888-1137-0d21b6f471c5";
    private const string MaterialHauptRef = "3fc1cf51-0d42-9168-df93-6ab74b15e6b2";
    private const string BreiteRef = "902695a4-5f44-e910-b2da-471c17085822";
    private const string HoeheRef = "d06f8d1f-8a09-1b22-4380-088a7ee42507";

    private static WebGisLesestand Stand(params (string RefId, string Key, string Text)[] werte)
    {
        var s = new WebGisLesestand { GlobalId = "G1", Bezeichnung = "H1" };
        foreach (var (refId, key, text) in werte)
        {
            s.Felder[refId] = key;
            if (text is not null) s.Kataloge[refId] = new List<(string, string)> { (key, text) };
        }
        return s;
    }

    private static WebGisImportEingabe Haltung(params (string Feld, string Wert, bool Ersetzbar)[] felder)
    {
        var e = new WebGisImportEingabe { Objektart = WebGisObjektart.Haltung, Bezeichnung = "H1", GespeicherteGlobalId = "G1" };
        foreach (var (f, w, ers) in felder) e.Felder[f] = new WebGisImportFeld(w, ers);
        return e;
    }

    private static WebGisImportAenderung? Aenderung(WebGisImportPosition pos, string feld)
        => pos.Aenderungen.FirstOrDefault(a => a.Feld == feld);

    [Fact]
    public void Abweichende_gespeicherte_globalid_sperrt_uebernahme()
    {
        var pos = WebGisImportPlanBuilder.Baue(
            new WebGisImportEingabe { Objektart = WebGisObjektart.Haltung, Bezeichnung = "H1", GespeicherteGlobalId = "andere-id" },
            Stand((StatusRef, "1", "In Betrieb")));

        Assert.NotEmpty(pos.Sperren);
        Assert.Empty(pos.Aenderungen);
    }

    [Fact]
    public void Globalid_wird_bei_abweichendem_namen_nicht_vorgeschlagen()
    {
        var pos = WebGisImportPlanBuilder.Baue(
            new WebGisImportEingabe { Objektart = WebGisObjektart.Haltung, Bezeichnung = "H1" },
            new WebGisLesestand { GlobalId = "G1", Bezeichnung = "H2" });

        Assert.NotEmpty(pos.Sperren);
        Assert.Empty(pos.Aenderungen);
    }

    [Fact]
    public void Leeres_feld_wird_aus_dem_webgis_gefuellt()
    {
        var pos = WebGisImportPlanBuilder.Baue(Haltung(("FunktionHierarchisch", "", false)), Stand((FunktionRef, "106", "Sammelkanal")));

        Assert.Equal("PAA.Sammelkanal", Aenderung(pos, "FunktionHierarchisch")?.Neu);
    }

    [Fact]
    public void Webgis_ersetzt_einen_katasterwert()
    {
        var pos = WebGisImportPlanBuilder.Baue(Haltung(("Status", "ausser_Betrieb", true)), Stand((StatusRef, "1", "In Betrieb")));

        var a = Aenderung(pos, "Status");
        Assert.Equal("in_Betrieb", a?.Neu);
        Assert.Equal("ausser_Betrieb", a?.Alt);
    }

    [Fact]
    public void Handwert_bleibt_stehen()
    {
        var pos = WebGisImportPlanBuilder.Baue(Haltung(("Status", "ausser_Betrieb", false)), Stand((StatusRef, "1", "In Betrieb")));

        Assert.Null(Aenderung(pos, "Status"));
    }

    [Fact]
    public void Gleicher_wert_ist_keine_aenderung()
    {
        var pos = WebGisImportPlanBuilder.Baue(Haltung(("Status", "in_Betrieb", true)), Stand((StatusRef, "1", "In Betrieb")));

        Assert.Empty(pos.Aenderungen);
    }

    [Fact]
    public void Material_kommt_aus_dem_detail_sonst_aus_der_gruppe()
    {
        var mitDetail = WebGisImportPlanBuilder.Baue(Haltung(("Rohrmaterial", "", false)),
            Stand((MaterialHauptRef, "3", "Kunststoff"), (MaterialDetailRef, "305", "Polypropylen")));
        var nurGruppe = WebGisImportPlanBuilder.Baue(Haltung(("Rohrmaterial", "", false)),
            Stand((MaterialHauptRef, "1", "Beton")));
        // SewerStudio kennt bei der Haltung kein blosses «Kunststoff» (nur «Kunststoff unbekannt»): melden.
        var nurKunststoff = WebGisImportPlanBuilder.Baue(Haltung(("Rohrmaterial", "", false)),
            Stand((MaterialHauptRef, "3", "Kunststoff")));

        Assert.Equal("Polypropylen", Aenderung(mitDetail, "Rohrmaterial")?.Neu);
        Assert.Equal("Beton", Aenderung(nurGruppe, "Rohrmaterial")?.Neu);
        Assert.Null(Aenderung(nurKunststoff, "Rohrmaterial"));
        Assert.Contains(nurKunststoff.Hinweise, h => h.Contains("Kunststoff"));
    }

    [Fact]
    public void Dn_nur_wenn_breite_und_hoehe_gleich_sind()
    {
        var rund = WebGisImportPlanBuilder.Baue(Haltung(("DN_mm", "", false)), Stand((BreiteRef, "300", "300"), (HoeheRef, "300", "300")));
        var oval = WebGisImportPlanBuilder.Baue(Haltung(("DN_mm", "", false)), Stand((BreiteRef, "600", "600"), (HoeheRef, "900", "900")));

        Assert.Equal("300", Aenderung(rund, "DN_mm")?.Neu);
        Assert.Null(Aenderung(oval, "DN_mm"));
        Assert.Contains(oval.Hinweise, h => h.Contains("DN"));
        Assert.DoesNotContain(rund.Aenderungen, a => a.Feld == "Lichte_Breite_mm");
    }

    [Fact]
    public void Nicht_zuordenbarer_webgis_wert_wird_gemeldet_nicht_uebernommen()
    {
        var pos = WebGisImportPlanBuilder.Baue(Haltung(("Status", "", false)), Stand((StatusRef, "9", "Erfundener Status")));

        Assert.Null(Aenderung(pos, "Status"));
        Assert.Contains(pos.Hinweise, h => h.Contains("Erfundener Status"));
    }

    [Fact]
    public void Combo_ohne_katalogtext_wird_nicht_als_schluessel_uebernommen()
    {
        var s = new WebGisLesestand { GlobalId = "G1", Bezeichnung = "H1" };
        s.Felder[StatusRef] = "1"; // kein Katalog gelesen -> nur der Schluessel bekannt

        var pos = WebGisImportPlanBuilder.Baue(Haltung(("Status", "", false)), s);

        Assert.Null(Aenderung(pos, "Status"));
    }

    // Materialgruppe steht nur in der Objektakte (haltung.pipegroup) — im Bild vom 23.09. leer,
    // obwohl das Detail Polypropylen war.
    [Fact]
    public void Leere_materialgruppe_der_akte_kommt_aus_der_webgis_gruppe()
    {
        var e = Haltung(("Rohrmaterial", "Polypropylen", false));
        var pos = WebGisImportPlanBuilder.Baue(e, Stand((MaterialHauptRef, "3", "Kunststoff"), (MaterialDetailRef, "305", "Polypropylen")));

        var a = Aenderung(pos, "haltung.pipegroup");
        Assert.Equal("Kunststoff", a?.Neu);
        Assert.Null(Aenderung(pos, "Rohrmaterial")); // gleicher Wert, keine Aenderung
    }

    [Fact]
    public void Von_hand_gesetzte_materialgruppe_bleibt()
    {
        var e = Haltung();
        e.Felder["haltung.pipegroup"] = new WebGisImportFeld("Beton", Ersetzbar: false);
        var pos = WebGisImportPlanBuilder.Baue(e, Stand((MaterialHauptRef, "3", "Kunststoff")));

        Assert.Null(Aenderung(pos, "haltung.pipegroup"));
    }

    // Entscheid Pascal 23.09.2026 (ersetzt «Laenge immer aus dem WebGIS» vom 21.09.): In SewerStudio gilt
    // die Laenge des Operateurs; sie wird weder geholt noch gesendet. Eigentum aendert das Programm nie.
    [Fact]
    public void Laenge_und_eigentum_werden_nie_geholt()
    {
        var s = Stand();
        s.Felder[WebGisFeldkarte.HaltungLaengeGeomRef] = "94.78";
        var e = new WebGisImportEingabe { Objektart = WebGisObjektart.Haltung, Bezeichnung = "H1", Laenge = "94.79" };
        e.Felder["Eigentuemer"] = new WebGisImportFeld("", Ersetzbar: true);

        var pos = WebGisImportPlanBuilder.Baue(e, s);

        Assert.DoesNotContain(pos.Aenderungen, a => a.Feld.Contains("laenge", System.StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(pos.Aenderungen, a => a.Feld.Contains("igent", System.StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(WebGisHandwertKarte.Felder, k => k.SewerStudioFeld.Contains("igent", System.StringComparison.OrdinalIgnoreCase)
                                                              || k.SewerStudioFeld.Contains("laenge", System.StringComparison.OrdinalIgnoreCase));
    }
}
