using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Uebernahme des Holen-Plans in die Datensaetze (23.09.2026). Ein Katasterwert wird nur ersetzt,
/// wenn er seit der Vorschau unveraendert und nicht von Hand gesetzt ist.
/// </summary>
public sealed class WebGisImportUebernahmeTests
{
    private static (Project Projekt, HaltungRecord Haltung) ProjektMit(string feld, string? wert, FieldSource quelle, bool hand)
    {
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.HoldingName, "H1", FieldSource.Manual, false);
        if (wert is not null) h.SetFieldValue(feld, wert, quelle, hand);
        var p = new Project();
        p.Data.Add(h);
        return (p, h);
    }

    private static WebGisImportPlan Plan(HaltungRecord h, string feld, string? alt, string neu)
    {
        var pos = new WebGisImportPosition { Objektart = WebGisObjektart.Haltung, Bezeichnung = "H1", RecordId = h.Id, GlobalId = "G1" };
        pos.Aenderungen.Add(new WebGisImportAenderung { Feld = feld, Alt = alt, Neu = neu, Grund = "Test" });
        var plan = new WebGisImportPlan();
        plan.Positionen.Add(pos);
        return plan;
    }

    [Fact]
    public void Leeres_feld_wird_als_katasterwert_ohne_handmarke_gefuellt()
    {
        var (p, h) = ProjektMit(FieldKeys.OperatingStatus, null, FieldSource.Manual, false);

        Assert.Equal(1, WebGisImportUseCase.Uebernimm(Plan(h, FieldKeys.OperatingStatus, null, "in_Betrieb"), p));

        Assert.Equal("In Betrieb", h.GetFieldValue(FieldKeys.OperatingStatus)); // WebGIS-Begriff auch beim Nachfuellen (23.09.2026)
        Assert.Equal(FieldSource.Kataster, h.FieldMeta[FieldKeys.OperatingStatus].Source);
        Assert.False(h.FieldMeta[FieldKeys.OperatingStatus].UserEdited);
    }

    [Fact]
    public void Katasterwert_wird_ersetzt()
    {
        // Seit 23.09.2026 stehen Datensatz und Plan in WebGIS-Begriffen (Schritt A).
        var (p, h) = ProjektMit(FieldKeys.OperatingStatus, "Ausser Betrieb", FieldSource.Kataster, false);

        WebGisImportUseCase.Uebernimm(Plan(h, FieldKeys.OperatingStatus, "Ausser Betrieb", "In Betrieb"), p);

        Assert.Equal("In Betrieb", h.GetFieldValue(FieldKeys.OperatingStatus));
    }

    [Fact]
    public void Seit_der_vorschau_von_hand_geaendert_bleibt_stehen()
    {
        var (p, h) = ProjektMit(FieldKeys.OperatingStatus, "Ausser Betrieb", FieldSource.Kataster, false);
        var plan = Plan(h, FieldKeys.OperatingStatus, "Ausser Betrieb", "In Betrieb");
        h.SetFieldValue(FieldKeys.OperatingStatus, "Tot/Aufgehoben, verfüllt", FieldSource.Manual, true);

        Assert.Equal(0, WebGisImportUseCase.Uebernimm(plan, p));

        Assert.Equal("Tot/Aufgehoben, verfüllt", h.GetFieldValue(FieldKeys.OperatingStatus));
    }

    [Fact]
    public void Globalid_wird_nicht_gespeichert_wenn_der_name_nach_der_vorschau_geaendert_wurde()
    {
        var (projekt, haltung) = ProjektMit(FieldKeys.OperatingStatus, null, FieldSource.Manual, false);
        var plan = Plan(haltung, WebGisImportPlanBuilder.FeldWebGisGlobalId, null, "G1");
        haltung.SetFieldValue(FieldKeys.HoldingName, "H2", FieldSource.Manual, true);

        Assert.Equal(0, WebGisImportUseCase.Uebernimm(plan, projekt));
        Assert.Null(haltung.WebGisGlobalId);
    }

    [Fact]
    public void Protokollwert_wird_nie_ersetzt()
    {
        var (p, h) = ProjektMit(FieldKeys.PipeMaterial, "Steinzeug", FieldSource.Protocol, false);

        WebGisImportUseCase.Uebernimm(Plan(h, FieldKeys.PipeMaterial, "Steinzeug", "Polypropylen"), p);

        Assert.Equal("Steinzeug", h.GetFieldValue(FieldKeys.PipeMaterial));
    }

    [Fact]
    public void Ersetzbar_nur_bei_katasterherkunft_ohne_handmarke()
    {
        Assert.True(WebGisImportUseCase.IstErsetzbar(new FieldMetadata { Source = FieldSource.Kataster }));
        Assert.True(WebGisImportUseCase.IstErsetzbar(new FieldMetadata { Source = FieldSource.Xtf405 }));
        Assert.False(WebGisImportUseCase.IstErsetzbar(new FieldMetadata { Source = FieldSource.Kataster, UserEdited = true }));
        Assert.False(WebGisImportUseCase.IstErsetzbar(new FieldMetadata { Source = FieldSource.Protocol }));
        Assert.False(WebGisImportUseCase.IstErsetzbar(null));
    }

    [Fact]
    public void Materialgruppe_wird_in_die_akte_der_haltung_geschrieben()
    {
        var (p, h) = ProjektMit(FieldKeys.PipeMaterial, null, FieldSource.Manual, false);

        Assert.Equal(1, WebGisImportUseCase.Uebernimm(Plan(h, "haltung.pipegroup", null, "Kunststoff"), p));

        var akte = Assert.Single(p.Objektakten, a => a.Id == h.Id && a.Art == "haltung");
        var wert = akte.Werte["haltung.pipegroup"];
        Assert.Equal("Kunststoff", wert.Text);
        Assert.Equal("3", wert.Originalcode);
        Assert.False(wert.VonHand);
    }
}
