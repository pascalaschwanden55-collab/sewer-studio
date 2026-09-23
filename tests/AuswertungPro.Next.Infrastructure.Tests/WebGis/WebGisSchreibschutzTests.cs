using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Pruefung 23.09.2026 (zweite Runde): fremde Aenderungen im WebGIS, nicht exakte Begriffe beim Senden,
/// und Handarbeit in SewerStudio beim Holen. Entscheid Pascal: beim Holen gilt «bewusst leer = geschuetzt»
/// wie beim GeoShop-Abgleich.
/// </summary>
public sealed class WebGisSchreibschutzTests
{
    private const string StatusRef = "7ecf9743-e8df-c35d-fdce-1a188b72bef8";      // Haltung Status
    private const string SchachtStatusRef = "576d3dff-e216-c25f-3e55-16d93027ca3d";

    // ---------------- Punkt 1: fremde Aenderung waehrend der offenen Vorschau ----------------

    private static WebGisExportPlan PlanMit(string alt, string neu)
    {
        var plan = new WebGisExportPlan();
        var pos = new WebGisExportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = "H1", GlobalId = "G1" };
        pos.Aenderungen.Add(new WebGisFeldAenderung { RefId = StatusRef, Feld = "Status", Alt = alt, Neu = neu });
        plan.Positionen.Add(pos);
        return plan;
    }

    [Fact]
    public void Fremde_aenderung_im_webgis_seit_der_vorschau_verlangt_eine_neue_vorschau()
        => Assert.False(WebGisPlanVergleich.Gleich(PlanMit(alt: "2", neu: "1"), PlanMit(alt: "5", neu: "1")));

    [Fact]
    public void Unveraenderter_plan_bleibt_gleich()
        => Assert.True(WebGisPlanVergleich.Gleich(PlanMit(alt: "2", neu: "1"), PlanMit(alt: "2", neu: "1")));

    // ---------------- Punkt 4: nur exakte WebGIS-Begriffe gehen hinaus ----------------

    private static WebGisLesestand HaltungsStand(params (string Key, string Text)[] statusListe)
    {
        var s = new WebGisLesestand { GlobalId = "G1", Bezeichnung = "H1" };
        s.Felder[StatusRef] = "0";
        s.Kataloge[StatusRef] = new List<(string, string)> { ("0", "Unbekannt") };
        s.Kataloge[StatusRef].AddRange(statusListe);
        return s;
    }

    private static WebGisObjektEingabe HaltungMitStatus(string status) => new()
    {
        Objektart = WebGisObjektart.Haltung, Bezeichnung = "H1", Handwerte = { [FieldKeys.OperatingStatus] = status },
    };

    [Fact]
    public void Wert_ohne_webgis_begriff_wird_nicht_gesendet_auch_wenn_er_gefaltet_passen_wuerde()
    {
        var pos = WebGisExportPlanBuilder.Baue(HaltungMitStatus("in_Betrieb"), HaltungsStand(("1", "In Betrieb")));

        Assert.DoesNotContain(pos.Aenderungen, a => a.RefId == StatusRef);
        Assert.Contains(pos.Hinweise, h => h.Contains("kein WebGIS-Begriff"));
    }

    [Fact]
    public void Doppelter_eintrag_in_der_webgis_liste_sperrt_das_feld()
    {
        var pos = WebGisExportPlanBuilder.Baue(HaltungMitStatus("In Betrieb"), HaltungsStand(("1", "In Betrieb"), ("7", "In Betrieb")));

        Assert.DoesNotContain(pos.Aenderungen, a => a.RefId == StatusRef);
        Assert.Contains(pos.Hinweise, h => h.Contains("mehrfach"));
    }

    [Fact]
    public void Exakter_webgis_begriff_wird_gesendet()
    {
        var pos = WebGisExportPlanBuilder.Baue(HaltungMitStatus("In Betrieb"), HaltungsStand(("1", "In Betrieb")));

        Assert.Contains(pos.Aenderungen, a => a.RefId == StatusRef && a.Neu == "1");
    }

    // ---------------- Punkt 2: bewusst leer = geschuetzt (Entscheid Pascal) ----------------

    private static WebGisLesestand SchachtStand()
    {
        var s = new WebGisLesestand { GlobalId = "G1", Bezeichnung = "80409" };
        s.Felder[SchachtStatusRef] = "1";
        s.Kataloge[SchachtStatusRef] = new List<(string, string)> { ("1", "In Betrieb") };
        s.Felder[WebGisFeldkarte.SchachtBaujahrRef] = "1969";
        return s;
    }

    [Fact]
    public void Holen_schlaegt_ein_bewusst_geleertes_feld_nicht_zum_fuellen_vor()
    {
        var e = new WebGisImportEingabe
        {
            Objektart = WebGisObjektart.Schacht, Bezeichnung = "80409", GespeicherteGlobalId = "G1",
            BaujahrHandwert = true,
            Felder = { [FieldKeys.OperatingStatus] = new WebGisImportFeld("", Ersetzbar: false, Handwert: true) },
        };

        var pos = WebGisImportPlanBuilder.Baue(e, SchachtStand());

        Assert.DoesNotContain(pos.Aenderungen, a => a.Feld == FieldKeys.OperatingStatus);
        Assert.DoesNotContain(pos.Aenderungen, a => a.Feld == WebGisImportPlanBuilder.FeldBaujahr);
    }

    private static WebGisImportPlan ImportPlan(Guid id, WebGisObjektart art, string name, string feld, string? alt, string neu)
    {
        var plan = new WebGisImportPlan();
        var pos = new WebGisImportPosition { Objektart = art, Bezeichnung = name, RecordId = id, GlobalId = "G1" };
        pos.Aenderungen.Add(new WebGisImportAenderung { Feld = feld, Alt = alt, Neu = neu, Grund = "Test" });
        plan.Positionen.Add(pos);
        return plan;
    }

    [Fact]
    public void Nach_der_vorschau_von_hand_geleertes_feld_bleibt_leer_und_handmarkiert()
    {
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.HoldingName, "H1", FieldSource.Manual, false);
        h.SetFieldValue(FieldKeys.OperatingStatus, "Ausser Betrieb", FieldSource.Kataster, false);
        var projekt = new Project();
        projekt.Data.Add(h);
        var plan = ImportPlan(h.Id, WebGisObjektart.Haltung, "H1", FieldKeys.OperatingStatus, "Ausser Betrieb", "In Betrieb");
        h.SetFieldValue(FieldKeys.OperatingStatus, "", FieldSource.Manual, true); // bewusst geleert

        WebGisImportUseCase.Uebernimm(plan, projekt);

        Assert.Equal("", h.GetFieldValue(FieldKeys.OperatingStatus));
        Assert.True(h.FieldMeta[FieldKeys.OperatingStatus].UserEdited);
    }

    // ---------------- Punkt 3: Bauwerksart nach der Vorschau geaendert ----------------

    [Fact]
    public void Spezialbauwerk_bekommt_beim_uebernehmen_keine_normschacht_funktion()
    {
        var s = new SchachtRecord();
        s.SetFieldValue("Schachtnummer", "80409", FieldSource.Manual, false);
        var projekt = new Project();
        projekt.SchaechteData.Add(s);
        var plan = ImportPlan(s.Id, WebGisObjektart.Schacht, "80409", WebGisBegriffe.SchachtFunktion, null, "Pumpenschacht");
        s.SetFieldValue(FieldKeys.ShaftStructureType, "Spezialbauwerk", FieldSource.Manual, true); // nach der Vorschau

        WebGisImportUseCase.Uebernimm(plan, projekt);

        Assert.Equal("", s.GetFieldValue(WebGisBegriffe.SchachtFunktion));
    }

    // ---------------- Punkt 5: Nachfuellen der Haltung speichert den WebGIS-Begriff ----------------

    [Fact]
    public void Nachfuellen_einer_haltung_speichert_den_webgis_begriff()
    {
        var h = new HaltungRecord();

        Assert.True(h.FuelleLeeresFeld(FieldKeys.OperatingStatus, "in_Betrieb", FieldSource.Kataster));

        Assert.Equal("In Betrieb", h.GetFieldValue(FieldKeys.OperatingStatus));
    }
}
