using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Holen, 24.09.2026 (Wunsch Pascal «importiere das was im WebGIS ist»): Die Masken haben ein eigenes Feld
/// «Typ AA» (PAA/SAA). Vorher las das Holen es nicht — am Schacht blieb «Liegenschaftsentwässerung» deshalb
/// mehrdeutig (22 Schächte in Zone 1.15), und Typ AA, Funktion hierarchisch und hydraulisch der Schachtakte
/// blieben leer.
/// </summary>
public sealed class WebGisImportTypAaTests
{
    private const string SchachtTypAaRef = "8d17a0bc-4472-d776-82f7-58ba57adf676";
    private const string HaltungTypAaRef = "65e83cb4-0e0f-ee6c-47c6-6efad172c3b1";
    private const string SchachtFunktionHierRef = "2bb1a598-71a2-7cc4-b659-61301a68de69";
    private const string HaltungFunktionHierRef = "85770f0e-245b-a1a6-c768-dfcd1c460de9";
    private const string SchachtFunktionHydrRef = "0e5eab11-5f00-4a00-9b00-000000000001"; // Praefix aus der Inventur

    private static readonly List<(string, string)> TypAaListe = new() { ("0", "Unbekannt"), ("1", "PAA"), ("2", "SAA") };
    private static readonly List<(string, string)> HierListe = new()
    {
        ("0", "Unbekannt"), ("5", "Liegenschaftsentwässerung"), ("106", "Sammelkanal"), ("108", "Strassenentwässerung"),
    };
    private static readonly List<(string, string)> HydrListe = new()
    {
        ("0", "Unbekannt"), ("4", "Freispiegelleitung"), ("5", "Pumpendruckleitung"),
    };

    private static WebGisLesestand Stand(string bezeichnung, params (string RefId, string Key, List<(string, string)> Liste)[] werte)
    {
        var s = new WebGisLesestand { GlobalId = "G1", Bezeichnung = bezeichnung };
        foreach (var (refId, key, liste) in werte)
        {
            s.Felder[refId] = key;
            s.Kataloge[refId] = new List<(string, string)>(liste);
        }
        return s;
    }

    private static WebGisImportEingabe Schacht(params (string Feld, WebGisImportFeld Wert)[] felder)
    {
        var e = new WebGisImportEingabe { Objektart = WebGisObjektart.Schacht, Bezeichnung = "80659", GespeicherteGlobalId = "G1" };
        foreach (var (f, w) in felder) e.Felder[f] = w;
        return e;
    }

    private static WebGisImportAenderung? Aenderung(WebGisImportPosition pos, string feld)
        => pos.Aenderungen.FirstOrDefault(a => a.Feld == feld);

    [Fact]
    public void Schacht_funktion_kommt_wie_im_webgis_in_die_akte_ohne_mehrdeutigkeit()
    {
        var pos = WebGisImportPlanBuilder.Baue(Schacht(),
            Stand("80659", (SchachtTypAaRef, "2", TypAaListe), (SchachtFunktionHierRef, "5", HierListe)));

        Assert.Equal("Liegenschaftsentwässerung", Aenderung(pos, "schacht.funktion_hierarchisch")?.Neu);
        Assert.Equal("SAA", Aenderung(pos, "schacht.typ_aa")?.Neu);
        // Nicht mehr ins Tabellenfeld: Die Schachttabelle fuehrt es nicht, sichtbar ist nur die Akte.
        Assert.Null(Aenderung(pos, "FunktionHierarchisch"));
        Assert.DoesNotContain(pos.Hinweise, h => h.Contains("passt zu mehreren"));
    }

    [Fact]
    public void Haltung_typ_aa_aus_dem_webgis_macht_liegenschaftsentwaesserung_eindeutig()
    {
        var e = new WebGisImportEingabe { Objektart = WebGisObjektart.Haltung, Bezeichnung = "H1", GespeicherteGlobalId = "G1" };
        var pos = WebGisImportPlanBuilder.Baue(e,
            Stand("H1", (HaltungTypAaRef, "2", TypAaListe), (HaltungFunktionHierRef, "5", HierListe)));

        Assert.Equal("SAA.Liegenschaftsentwaesserung", Aenderung(pos, "FunktionHierarchisch")?.Neu);
        Assert.DoesNotContain(pos.Hinweise, h => h.Contains("passt zu mehreren"));
    }

    [Fact]
    public void Schacht_akte_bekommt_typ_aa_und_beide_funktionen_wie_im_webgis()
    {
        var pos = WebGisImportPlanBuilder.Baue(Schacht(),
            Stand("80659", (SchachtTypAaRef, "2", TypAaListe), (SchachtFunktionHierRef, "5", HierListe),
                (SchachtFunktionHydrRef, "4", HydrListe)));

        Assert.Equal("SAA", Aenderung(pos, "schacht.typ_aa")?.Neu);
        Assert.Equal("Liegenschaftsentwässerung", Aenderung(pos, "schacht.funktion_hierarchisch")?.Neu);
        Assert.Equal("Freispiegelleitung", Aenderung(pos, "schacht.funktion_hydraulisch")?.Neu);
    }

    [Fact]
    public void Haltung_typ_aa_kommt_aus_dem_webgis_und_in_die_akte()
    {
        var e = new WebGisImportEingabe { Objektart = WebGisObjektart.Haltung, Bezeichnung = "H1", GespeicherteGlobalId = "G1" };
        // SewerStudio fuehrt (aus GeoShop, ohne Handmarke) SAA — das WebGIS sagt PAA: das WebGIS gilt.
        e.Felder[WebGisImportPlanBuilder.TypAaFeld] = new WebGisImportFeld("SAA", Ersetzbar: true);

        var pos = WebGisImportPlanBuilder.Baue(e,
            Stand("H1", (HaltungTypAaRef, "1", TypAaListe), (HaltungFunktionHierRef, "5", HierListe)));

        Assert.Equal("PAA.Liegenschaftsentwaesserung", Aenderung(pos, "FunktionHierarchisch")?.Neu);
        var typ = Aenderung(pos, WebGisImportPlanBuilder.TypAaFeld);
        Assert.Equal("PAA", typ?.Neu);
        Assert.Equal("SAA", typ?.Alt);
    }

    [Fact]
    public void Ein_feld_ohne_paa_saa_liste_gilt_nicht_als_typ_aa()
    {
        // Die refId stammt aus der Inventur, nicht aus einer Live-Pruefung: Traegt die Komponente keine
        // PAA/SAA-Liste, ist es nicht Typ AA — dann nichts daraus ableiten.
        var pos = WebGisImportPlanBuilder.Baue(Schacht(),
            Stand("80659", (SchachtTypAaRef, "2", HierListe), (SchachtFunktionHierRef, "5", HierListe)));

        Assert.Null(Aenderung(pos, "FunktionHierarchisch"));
        Assert.Null(Aenderung(pos, "schacht.typ_aa"));
        Assert.Contains(pos.Hinweise, h => h.Contains("Typ AA") && h.Contains("nicht sicher erkannt"));
    }

    [Fact]
    public void Unbekannt_im_webgis_fuellt_nichts()
    {
        var pos = WebGisImportPlanBuilder.Baue(Schacht(),
            Stand("80659", (SchachtTypAaRef, "0", TypAaListe), (SchachtFunktionHydrRef, "0", HydrListe)));

        Assert.Null(Aenderung(pos, "schacht.typ_aa"));
        Assert.Null(Aenderung(pos, "schacht.funktion_hydraulisch"));
    }

    [Fact]
    public void Bewusst_leerer_handwert_der_akte_bleibt_leer()
    {
        var pos = WebGisImportPlanBuilder.Baue(Schacht(("schacht.typ_aa", new WebGisImportFeld("", Ersetzbar: false, Handwert: true))),
            Stand("80659", (SchachtTypAaRef, "2", TypAaListe)));

        Assert.Null(Aenderung(pos, "schacht.typ_aa"));
        Assert.Contains(pos.Hinweise, h => h.Contains("Typ AA") && h.Contains("bewusst leer"));
    }

    [Fact]
    public void Typ_aa_gegen_die_funktion_der_kanalfirma_wird_gemeldet_statt_geschrieben()
    {
        // Die Kanalfirma fuehrt PAA.… (Ist-Zustand, nie ersetzt); das WebGIS sagt SAA. Typ AA nicht dagegen
        // setzen, sonst widerspraechen sich Typ AA und Funktion im selben Datensatz.
        var e = new WebGisImportEingabe { Objektart = WebGisObjektart.Haltung, Bezeichnung = "H1", GespeicherteGlobalId = "G1" };
        e.Felder["FunktionHierarchisch"] = new WebGisImportFeld("PAA.Liegenschaftsentwaesserung", Ersetzbar: false);
        var pos = WebGisImportPlanBuilder.Baue(e,
            Stand("H1", (HaltungTypAaRef, "2", TypAaListe), (HaltungFunktionHierRef, "5", HierListe)));

        Assert.Null(Aenderung(pos, "FunktionHierarchisch"));
        Assert.Null(Aenderung(pos, WebGisImportPlanBuilder.TypAaFeld));
        Assert.Contains(pos.Hinweise, h => h.Contains("Typ AA") && h.Contains("PAA.Liegenschaftsentwaesserung"));
    }

    [Fact]
    public void Hydraulische_funktion_nur_mit_passender_liste()
    {
        var pos = WebGisImportPlanBuilder.Baue(Schacht(),
            Stand("80659", (SchachtFunktionHydrRef, "4", TypAaListe)));

        Assert.Null(Aenderung(pos, "schacht.funktion_hydraulisch"));
    }

    [Fact]
    public void Uebernahme_schreibt_die_schachtakte_mit_webgis_schluessel()
    {
        var s = new SchachtRecord();
        s.SetFieldValue("Schachtnummer", "80659", FieldSource.Manual, false);
        var p = new Project();
        p.SchaechteData.Add(s);
        var pos = new WebGisImportPosition { Objektart = WebGisObjektart.Schacht, Bezeichnung = "80659", RecordId = s.Id, GlobalId = "G1" };
        pos.Aenderungen.Add(new WebGisImportAenderung { Feld = "schacht.typ_aa", Neu = "SAA", Grund = "Test" });
        pos.Aenderungen.Add(new WebGisImportAenderung { Feld = "schacht.funktion_hierarchisch", Neu = "Liegenschaftsentwässerung", Grund = "Test" });
        var plan = new WebGisImportPlan();
        plan.Positionen.Add(pos);

        Assert.Equal(1, WebGisImportUseCase.Uebernimm(plan, p));

        var akte = Assert.Single(p.Objektakten, a => a.Id == s.Id && a.Art == "schacht");
        Assert.Equal("SAA", akte.Werte["schacht.typ_aa"].Text);
        Assert.Equal("2", akte.Werte["schacht.typ_aa"].Originalcode);
        Assert.Equal("5", akte.Werte["schacht.funktion_hierarchisch"].Originalcode);
        Assert.False(akte.Werte["schacht.typ_aa"].VonHand);
    }

    [Theory]
    [InlineData("schacht.typ_aa", "Typ AA")]
    [InlineData("haltung.aatype", "Typ AA")]
    [InlineData("schacht.funktion_hierarchisch", "Funktion hierarchisch")]
    [InlineData("schacht.funktion_hydraulisch", "Funktion hydraulisch")]
    public void Bericht_nennt_die_aktenfelder_lesbar(string feld, string anzeige)
    {
        var plan = new WebGisImportPlan();
        var pos = new WebGisImportPosition { Objektart = WebGisObjektart.Schacht, Bezeichnung = "80659", GlobalId = "G1" };
        pos.Aenderungen.Add(new WebGisImportAenderung { Feld = feld, Neu = "SAA", Grund = "Test" });
        plan.Positionen.Add(pos);

        Assert.Contains(WebGisImportBericht.Vorschau(plan).Zeilen, z => z.Feld == anzeige);
    }

    // ---------------- Alle fehlenden Schachtfelder (Wunsch Pascal 24.09.2026) ----------------

    private const string SohlenRef = "e627d56a-9401-1ca3-acee-736e061d77ed";
    private const string ZugangRef = "67e362fb-387e-f80f-19d4-8f2a006b1f9b";
    private const string BueroRef = "64fda99b-3191-9fcb-8db2-e1d4a700653f";
    private const string ErhebungsjahrRef = "52f1902f-6759-e421-0873-a897111dbebb";
    private const string SachbearbeiterRef = "d7e6bee7-1c03-e0f9-23ba-542c8209dc9c";

    private static WebGisLesestand MitText(WebGisLesestand s, string refId, string wert)
    {
        s.Felder[refId] = wert;
        return s;
    }

    [Fact]
    public void Sohlenhoehe_kommt_in_die_akte_die_die_hoehenrechnung_liest()
    {
        var pos = WebGisImportPlanBuilder.Baue(Schacht(), MitText(Stand("80659"), SohlenRef, "438.120"));

        Assert.Equal("438.12", Aenderung(pos, "schacht.sohlenhoehe")?.Neu);
        Assert.Null(Aenderung(pos, "Sohlenhoehe")); // kein unsichtbares Tabellenfeld mehr
    }

    [Fact]
    public void Gleiche_zahl_in_anderer_schreibweise_ist_keine_aenderung()
    {
        var pos = WebGisImportPlanBuilder.Baue(Schacht(("schacht.sohlenhoehe", new WebGisImportFeld("438.12", Ersetzbar: true))),
            MitText(Stand("80659"), SohlenRef, "438.1200"));

        Assert.Null(Aenderung(pos, "schacht.sohlenhoehe"));
    }

    [Fact]
    public void Keine_zahl_wird_gemeldet_statt_uebernommen()
    {
        var pos = WebGisImportPlanBuilder.Baue(Schacht(), MitText(Stand("80659"), SohlenRef, "ca. 438"));

        Assert.Null(Aenderung(pos, "schacht.sohlenhoehe"));
        Assert.Contains(pos.Hinweise, h => h.Contains("Sohlenhöhe") && h.Contains("kein gültiger Wert"));
    }

    [Fact]
    public void Auswahlfeld_ueber_den_webgis_schluessel()
    {
        var liste = new List<(string, string)> { ("0", "Unbekannt"), ("101", "Überdeckt"), ("102", "Unzugänglich"), ("103", "Zugänglich") };
        var pos = WebGisImportPlanBuilder.Baue(Schacht(), Stand("80659", (ZugangRef, "103", liste)));

        Assert.Equal("Zugänglich", Aenderung(pos, "schacht.zugaenglichkeit")?.Neu);
    }

    [Fact]
    public void Auswahlfeld_mit_fremder_liste_wird_nicht_uebernommen()
    {
        // Die refId stammt aus der Inventur: Traegt die Komponente eine andere Liste, ist es ein anderes Feld.
        var pos = WebGisImportPlanBuilder.Baue(Schacht(), Stand("80659", (ZugangRef, "5", HierListe)));

        Assert.Null(Aenderung(pos, "schacht.zugaenglichkeit"));
        Assert.Contains(pos.Hinweise, h => h.Contains("Zugänglichkeit") && h.Contains("nicht sicher erkannt"));
    }

    [Fact]
    public void Lagegenauigkeit_ohne_refid_ueber_die_eindeutige_liste()
    {
        var liste = new List<(string, string)> { ("0", "Unbekannt"), ("101", "> 50 cm"), ("102", "+/- 10 cm"), ("103", "+/- 3 cm"), ("104", "+/- 50 cm") };
        var pos = WebGisImportPlanBuilder.Baue(Schacht(), Stand("80659", ("50d16671-0000-0000-0000-000000000001", "102", liste)));

        Assert.Equal("+/- 10 cm", Aenderung(pos, "schacht.lagegenauigkeit")?.Neu);
        Assert.Null(Aenderung(pos, "schacht.hoehengenauigkeit"));
    }

    [Fact]
    public void Buero_ueber_die_organisationsliste()
    {
        var liste = new List<(string, string)>
        {
            ("df1f763b-7f01-4d4d-a22c-14476c7a3a9b", "Bund"), ("58d1c876-3d16-47da-8b38-b509bbc0cbca", "Kanton Uri"),
        };
        var pos = WebGisImportPlanBuilder.Baue(Schacht(), Stand("80659", (BueroRef, "58d1c876-3d16-47da-8b38-b509bbc0cbca", liste)));

        Assert.StartsWith("Kanton Uri", Aenderung(pos, "schacht.buero")?.Neu);
    }

    [Fact]
    public void Jahr_und_text_werden_uebernommen()
    {
        var s = MitText(MitText(Stand("80659"), ErhebungsjahrRef, "2019"), SachbearbeiterRef, "M. Muster");
        var pos = WebGisImportPlanBuilder.Baue(Schacht(), s);

        Assert.Equal("2019", Aenderung(pos, "schacht.erhebungsjahr_zustand")?.Neu);
        Assert.Equal("M. Muster", Aenderung(pos, "schacht.sachbearbeiter")?.Neu);
    }

    [Fact]
    public void Vorhandener_handwert_der_akte_bleibt()
    {
        var pos = WebGisImportPlanBuilder.Baue(
            Schacht(("schacht.sachbearbeiter", new WebGisImportFeld("P. Aschwanden", Ersetzbar: false, Handwert: true))),
            MitText(Stand("80659"), SachbearbeiterRef, "M. Muster"));

        Assert.Null(Aenderung(pos, "schacht.sachbearbeiter"));
    }

    [Fact]
    public void Uebernahme_schreibt_zahlfelder_woertlich_in_die_akte()
    {
        var s = new SchachtRecord();
        s.SetFieldValue("Schachtnummer", "80659", FieldSource.Manual, false);
        var p = new Project();
        p.SchaechteData.Add(s);
        var pos = new WebGisImportPosition { Objektart = WebGisObjektart.Schacht, Bezeichnung = "80659", RecordId = s.Id, GlobalId = "G1" };
        pos.Aenderungen.Add(new WebGisImportAenderung { Feld = "schacht.sohlenhoehe", Neu = "438.12", Grund = "Test" });
        var plan = new WebGisImportPlan();
        plan.Positionen.Add(pos);

        Assert.Equal(1, WebGisImportUseCase.Uebernimm(plan, p));

        var wert = Assert.Single(p.Objektakten, a => a.Id == s.Id && a.Art == "schacht").Werte["schacht.sohlenhoehe"];
        Assert.Equal("438.12", wert.Text);
        Assert.Null(wert.KatalogId);
        Assert.False(wert.VonHand);
    }
}
