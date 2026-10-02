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

    // Entscheid Pascal 23.09.2026 abends: Daten der Kanalfirmen werden nie ueberschrieben. Xtf/Xtf405/Ili
    // vergeben auch die Kanalfirmen-Importe (VSA-KEK, M150, SIA405) — ersetzbar ist nur, was aus GeoShop
    // oder QGIS stammt (FieldSource.Kataster).
    [Fact]
    public void Ersetzbar_nur_bei_katasterherkunft_ohne_handmarke()
    {
        Assert.True(WebGisImportUseCase.IstErsetzbar(new FieldMetadata { Source = FieldSource.Kataster }));
        Assert.False(WebGisImportUseCase.IstErsetzbar(new FieldMetadata { Source = FieldSource.Xtf405 }));
        Assert.False(WebGisImportUseCase.IstErsetzbar(new FieldMetadata { Source = FieldSource.Xtf }));
        Assert.False(WebGisImportUseCase.IstErsetzbar(new FieldMetadata { Source = FieldSource.Ili }));
        Assert.False(WebGisImportUseCase.IstErsetzbar(new FieldMetadata { Source = FieldSource.Legacy }));
        Assert.False(WebGisImportUseCase.IstErsetzbar(new FieldMetadata { Source = FieldSource.Kataster, UserEdited = true }));
        Assert.False(WebGisImportUseCase.IstErsetzbar(new FieldMetadata { Source = FieldSource.Protocol }));
        Assert.False(WebGisImportUseCase.IstErsetzbar(null));
    }

    [Theory]
    [InlineData(FieldSource.Xtf405)]
    [InlineData(FieldSource.Xtf)]
    public void Wert_aus_kanalfirmen_xtf_wird_nie_ersetzt(FieldSource quelle)
    {
        var (p, h) = ProjektMit(FieldKeys.OperatingStatus, "Ausser Betrieb", quelle, false);

        Assert.Equal(0, WebGisImportUseCase.Uebernimm(Plan(h, FieldKeys.OperatingStatus, "Ausser Betrieb", "In Betrieb"), p));

        Assert.Equal("Ausser Betrieb", h.GetFieldValue(FieldKeys.OperatingStatus));
    }

    [Fact]
    public void Leere_haltungslaenge_wird_aus_dem_webgis_gefuellt()
    {
        var (p, h) = ProjektMit(FieldKeys.HoldingLengthMeters, null, FieldSource.Manual, false);

        Assert.Equal(1, WebGisImportUseCase.Uebernimm(Plan(h, FieldKeys.HoldingLengthMeters, null, "94.78"), p));

        Assert.Equal("94.78", h.GetFieldValue(FieldKeys.HoldingLengthMeters));
        Assert.False(h.FieldMeta[FieldKeys.HoldingLengthMeters].UserEdited);
    }

    [Fact]
    public void Haltungslaenge_der_kanalfirma_bleibt()
    {
        var (p, h) = ProjektMit(FieldKeys.HoldingLengthMeters, "94.10", FieldSource.Xtf, false);

        WebGisImportUseCase.Uebernimm(Plan(h, FieldKeys.HoldingLengthMeters, "94.10", "94.78"), p);

        Assert.Equal("94.10", h.GetFieldValue(FieldKeys.HoldingLengthMeters));
    }

    [Fact]
    public void Betreiber_wird_in_die_akte_der_haltung_geschrieben()
    {
        var (p, h) = ProjektMit(FieldKeys.PipeMaterial, null, FieldSource.Manual, false);

        Assert.Equal(1, WebGisImportUseCase.Uebernimm(Plan(h, "haltung.operator", null, "Kanton Uri (Kanton)"), p));

        var wert = Assert.Single(p.Objektakten, a => a.Id == h.Id && a.Art == "haltung").Werte["haltung.operator"];
        Assert.Equal("Kanton Uri (Kanton)", wert.Text);
        Assert.False(wert.VonHand);
    }

    [Fact]
    public void Eigentuemer_der_kanalfirma_wird_durch_das_webgis_ersetzt()
    {
        // Entscheid Pascal 24.09.2026: Eigentuemer fuehrt das WebGIS — auch ein Wert der Kanalfirma weicht.
        var (p, h) = ProjektMit(FieldKeys.Owner, "AWU", FieldSource.Xtf405, false);

        Assert.Equal(1, WebGisImportUseCase.Uebernimm(Plan(h, FieldKeys.Owner, "AWU", "AWU_von_privat"), p));

        Assert.Equal("AWU_von_privat", h.GetFieldValue(FieldKeys.Owner));
        Assert.Equal(FieldSource.Kataster, h.FieldMeta[FieldKeys.Owner].Source);
        Assert.False(h.FieldMeta[FieldKeys.Owner].UserEdited);
    }

    // Entscheid Pascal 24.09.2026 abends: «Eigentuemer und Betreiber duerfen vom WebGIS ueberschrieben werden».
    // Der WebGIS-Wert ersetzt auch eine Handeingabe und ist danach ein Katasterwert ohne Handmarke.
    [Fact]
    public void Eigentuemer_von_hand_wird_bei_der_uebernahme_ersetzt()
    {
        var (p, h) = ProjektMit(FieldKeys.Owner, "Privat", FieldSource.Manual, true);

        Assert.Equal(1, WebGisImportUseCase.Uebernimm(Plan(h, FieldKeys.Owner, "Privat", "AWU_von_privat (Abwasserverband)"), p));

        Assert.Equal("AWU_von_privat (Abwasserverband)", h.GetFieldValue(FieldKeys.Owner));
        Assert.Equal(FieldSource.Kataster, h.FieldMeta[FieldKeys.Owner].Source);
        Assert.False(h.FieldMeta[FieldKeys.Owner].UserEdited);
    }

    // Entscheid Pascal 02.10.2026 (E3): Ein bewusst leeres Feld fuellt auch die Uebernahme nicht,
    // selbst wenn der Plan es anbietet. Ausnahme bleibt der Eigentuemer (Fuehrungsfeld, Test unten).
    [Fact]
    public void Bewusst_leeres_feld_wird_bei_der_uebernahme_nicht_gefuellt()
    {
        var (p, h) = ProjektMit(FieldKeys.OperatingStatus, "", FieldSource.Manual, true);

        Assert.Equal(0, WebGisImportUseCase.Uebernimm(Plan(h, FieldKeys.OperatingStatus, null, "in_Betrieb"), p));

        Assert.Equal("", h.GetFieldValue(FieldKeys.OperatingStatus));
        Assert.True(h.FieldMeta[FieldKeys.OperatingStatus].UserEdited);
    }

    [Fact]
    public void Bewusst_leerer_eigentuemer_wird_bei_der_uebernahme_gefuellt()
    {
        var (p, h) = ProjektMit(FieldKeys.Owner, "", FieldSource.Manual, true);

        Assert.Equal(1, WebGisImportUseCase.Uebernimm(Plan(h, FieldKeys.Owner, null, "AWU_von_privat (Abwasserverband)"), p));

        Assert.Equal("AWU_von_privat (Abwasserverband)", h.GetFieldValue(FieldKeys.Owner));
        Assert.False(h.FieldMeta[FieldKeys.Owner].UserEdited);
    }

    [Fact]
    public void Eigentuemer_seit_der_vorschau_geaendert_bleibt()
    {
        // Konfliktschutz bleibt: Wer nach der Vorschau einen anderen Eigentuemer eintraegt, wird nicht still ueberschrieben.
        var (p, h) = ProjektMit(FieldKeys.Owner, "Privat", FieldSource.Manual, true);

        Assert.Equal(0, WebGisImportUseCase.Uebernimm(Plan(h, FieldKeys.Owner, "Kanton Uri", "AWU_von_privat (Abwasserverband)"), p));

        Assert.Equal("Privat", h.GetFieldValue(FieldKeys.Owner));
    }

    [Fact]
    public void Eigentuemer_von_hand_am_schacht_wird_ersetzt()
    {
        var s = new SchachtRecord();
        s.SetFieldValue("Schachtnummer", "S1", FieldSource.Manual, false);
        s.SetFieldValue("Eigentümer", "Privat", FieldSource.Manual, true);
        var p = new Project();
        p.SchaechteData.Add(s);
        var pos = new WebGisImportPosition { Objektart = WebGisObjektart.Schacht, Bezeichnung = "S1", RecordId = s.Id, GlobalId = "GS1" };
        pos.Aenderungen.Add(new WebGisImportAenderung { Feld = FieldKeys.Owner, Alt = "Privat", Neu = "AWU_von_privat", Grund = "Test" });
        var plan = new WebGisImportPlan();
        plan.Positionen.Add(pos);

        Assert.Equal(1, WebGisImportUseCase.Uebernimm(plan, p));

        Assert.Equal("AWU_von_privat", s.GetFieldValue("Eigentümer"));
        Assert.False(s.IsUserEdited("Eigentümer"));
    }

    [Fact]
    public void Betreiber_von_hand_in_der_akte_wird_ersetzt()
    {
        var (p, h) = ProjektMit(FieldKeys.PipeMaterial, null, FieldSource.Manual, false);
        p.Objektakten.Add(new ObjektAkte { Id = h.Id, Art = "haltung" });
        p.Objektakten[0].Werte["haltung.operator"] = new ObjektFeldWert { Text = "Privat (Privat)", VonHand = true };

        Assert.Equal(1, WebGisImportUseCase.Uebernimm(Plan(h, "haltung.operator", "Privat (Privat)", "AWU_von_privat (Abwasserverband)"), p));

        var wert = p.Objektakten[0].Werte["haltung.operator"];
        Assert.Equal("AWU_von_privat (Abwasserverband)", wert.Text);
        Assert.False(wert.VonHand);
    }

    [Fact]
    public void Kanalfirmenwert_eines_anderen_felds_bleibt_weiterhin()
    {
        // Gegenprobe: Die neue Eigentuemer-Regel gilt nur fuer Eigentuemer, nicht fuer Fachfelder der Kanalfirma.
        var (p, h) = ProjektMit(FieldKeys.OperatingStatus, "Ausser Betrieb", FieldSource.Xtf405, false);

        Assert.Equal(0, WebGisImportUseCase.Uebernimm(Plan(h, FieldKeys.OperatingStatus, "Ausser Betrieb", "In Betrieb"), p));

        Assert.Equal("Ausser Betrieb", h.GetFieldValue(FieldKeys.OperatingStatus));
    }

    [Fact]
    public void Sanierungsbedarf_fuellt_nur_einen_leeren_schacht()
    {
        // Entscheid Pascal 24.09.2026: nur wenn in SewerStudio leer. Zwischen Vorschau und Uebernahme gesetzt = bleibt.
        var leer = new SchachtRecord();
        leer.SetFieldValue("Schachtnummer", "S1", FieldSource.Manual, false);
        var gesetzt = new SchachtRecord();
        gesetzt.SetFieldValue("Schachtnummer", "S2", FieldSource.Manual, false);
        var p = new Project();
        p.SchaechteData.Add(leer);
        p.SchaechteData.Add(gesetzt);
        var plan = new WebGisImportPlan();
        foreach (var (s, name) in new[] { (leer, "S1"), (gesetzt, "S2") })
        {
            var pos = new WebGisImportPosition { Objektart = WebGisObjektart.Schacht, Bezeichnung = name, RecordId = s.Id, GlobalId = "G" + name };
            pos.Aenderungen.Add(new WebGisImportAenderung { Feld = FieldKeys.RehabilitationNeed, Neu = "Mittelfristig", Grund = "Test" });
            plan.Positionen.Add(pos);
        }
        gesetzt.SetFieldValue(FieldKeys.RehabilitationNeed, "Kurzfristig", FieldSource.Xtf405, false); // nach der Vorschau

        Assert.Equal(1, WebGisImportUseCase.Uebernimm(plan, p));

        Assert.Equal("Mittelfristig", leer.GetFieldValue(FieldKeys.RehabilitationNeed));
        Assert.Equal(FieldSource.Kataster, leer.FieldMeta[FieldKeys.RehabilitationNeed].Source);
        Assert.Equal("Kurzfristig", gesetzt.GetFieldValue(FieldKeys.RehabilitationNeed));
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
