using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Schaechte der SIA405-XTF: In die Revision kommt ausschliesslich, was der Mensch von
/// Hand gesetzt hat — dieselbe Regel wie bei den Haltungen.
/// </summary>
public sealed class XtfSchachtPlanBuilderTests
{
    /// <summary>
    /// Aufbau wie im echten Kantonsexport: ein Normschacht mit Funktion, Material,
    /// beiden Massen und einem Verweis auf die einzige Organisation.
    /// </summary>
    private const string Kantonsexport = """
<?xml version="1.0" encoding="utf-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION VERSION="2.3" SENDER="AWU_XTF_Exporter_QGIS">
    <MODELS><MODEL NAME="SIA405_ABWASSER_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser BID="chB0000000000001">
      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Normschacht TID="ch1000a00000c3e1">
        <Letzte_Aenderung>20181219</Letzte_Aenderung>
        <Bezeichnung>80401</Bezeichnung>
        <Funktion>Kontroll_Einsteigschacht</Funktion>
        <Material>unbekannt</Material>
        <Dimension1>0</Dimension1>
        <Dimension2>0</Dimension2>
        <Status>unbekannt</Status>
        <EigentuemerRef REF="ch1000f000000001" />
      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Normschacht>
    </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser>
    <SIA405_Base_Abwasser_LV95.Administration BID="chB0000000000002">
      <SIA405_Base_Abwasser_LV95.Administration.Organisation TID="ch1000f000000001">
        <Bezeichnung>Abwasser Uri</Bezeichnung>
        <Organisationstyp>Kanton</Organisationstyp>
        <Status>aktiv</Status>
      </SIA405_Base_Abwasser_LV95.Administration.Organisation>
    </SIA405_Base_Abwasser_LV95.Administration>
  </DATASECTION>
</TRANSFER>
""";

    [Fact]
    public void Der_Leser_findet_den_Normschacht()
    {
        var element = Elemente().Single(e => e.Klasse == "Normschacht");

        Assert.Equal("ch1000a00000c3e1", element.Tid);
        Assert.Equal("80401", element.Bezeichnung);
        Assert.Equal("Kontroll_Einsteigschacht", element.Werte["Funktion"]);
        Assert.Equal("ch1000f000000001", element.Werte["EigentuemerRef"]);
    }

    [Fact]
    public void Eine_Handaenderung_an_der_Funktion_kommt_in_den_Plan()
    {
        var schacht = Schacht("80401");
        schacht.SetFieldValue("Funktion", "Schlammsammler", FieldSource.Manual, userEdited: true);

        var position = Assert.Single(Baue(schacht).Positionen);
        var feld = Assert.Single(position.Felder);

        Assert.Equal("ch1000a00000c3e1", position.KanalschadenTid);
        Assert.Equal("Funktion", feld.Name);
        Assert.Equal("Kontroll_Einsteigschacht", feld.Alt);
        Assert.Equal("Schlammsammler", feld.Neu);
    }

    // Derselbe Schutz wie bei den Haltungen: Ein importierter Wert darf nicht in die
    // Datei zurueckgeschrieben werden, aus der er stammt.
    [Fact]
    public void Ein_nur_importierter_Wert_kommt_nicht_in_den_Plan()
    {
        var schacht = Schacht("80401");
        schacht.SetFieldValue("Funktion", "Schlammsammler", FieldSource.Xtf, userEdited: false);

        Assert.Empty(Baue(schacht).Positionen);
    }

    // Der Kernbefund der Exportmatrix: "Normschacht.Material" kennt nur vier Werte.
    // Der AWU-Exporter benutzt dort die Rohrliste und schreibt "Beton_unbekannt" —
    // einen Wert, den die Klasse gar nicht hat. SewerStudio darf das nicht wiederholen.
    [Theory]
    [InlineData("Beton", "Beton")]
    [InlineData("Fertigbetonelement", "Beton")]
    [InlineData("Ortsbeton", "Beton")]
    [InlineData("Polyethylen", "Kunststoff")]
    [InlineData("Gemauert", "andere")]
    public void Das_Schachtmaterial_bleibt_in_der_kurzen_Liste(string eingabe, string erwartet)
    {
        var schacht = Schacht("80401");
        schacht.SetFieldValue("Material", eingabe, FieldSource.Manual, userEdited: true);

        var feld = Assert.Single(Assert.Single(Baue(schacht).Positionen).Felder);

        Assert.Equal("Material", feld.Name);
        Assert.Equal(erwartet, feld.Neu);
        Assert.DoesNotContain('_', feld.Neu!);
    }

    // "unbekannt" ist keine Angabe. Wuerde es geschrieben, ersetzte es in der Datei
    // moeglicherweise einen besseren Wert durch eine Leerformel.
    [Fact]
    public void Unbekannt_wird_nicht_geschrieben()
    {
        var schacht = Schacht("80401");
        schacht.SetFieldValue("Material", "unbekannt", FieldSource.Manual, userEdited: true);

        Assert.Empty(Baue(schacht).Positionen);
    }

    [Theory]
    [InlineData("600 mm", "600", "600")]
    [InlineData("1100 x 900 mm", "1100", "900")]
    [InlineData("800", "800", "800")]
    public void Die_Dimension_wird_in_beide_Masse_zerlegt(string eingabe, string eins, string zwei)
    {
        var schacht = Schacht("80401");
        schacht.SetFieldValue("Dimension", eingabe, FieldSource.Manual, userEdited: true);

        var felder = Assert.Single(Baue(schacht).Positionen).Felder;

        Assert.Equal(2, felder.Count);
        Assert.Equal(eins, felder.Single(f => f.Name == "Dimension1").Neu);
        Assert.Equal(zwei, felder.Single(f => f.Name == "Dimension2").Neu);
    }

    [Fact]
    public void Eine_unlesbare_Dimension_wird_gemeldet_statt_geraten()
    {
        var schacht = Schacht("80401");
        schacht.SetFieldValue("Dimension", "gross", FieldSource.Manual, userEdited: true);

        var plan = Baue(schacht);

        Assert.Empty(plan.Positionen);
        Assert.Contains(plan.Hinweise, h => h.Contains("nicht lesbar", StringComparison.Ordinal));
    }

    // 1803 von 2500 gesichteten Schaechten haben in AWUs Datenbank einen Z-Wert, der
    // nirgends hinausgeht. Genau diese Luecke fuellt SewerStudio.
    [Fact]
    public void Die_Zustandsklasse_geht_als_Z_Wert_hinaus()
    {
        var schacht = Schacht("80401");
        schacht.SetFieldValue(FieldKeys.ConditionClass, "2", FieldSource.Manual, userEdited: true);

        var feld = Assert.Single(Assert.Single(Baue(schacht).Positionen).Felder);

        Assert.Equal("BaulicherZustand", feld.Name);
        Assert.Equal("Z2", feld.Neu);
        Assert.Null(feld.Alt);
    }

    [Fact]
    public void Der_Eigentuemer_wird_zum_Verweis()
    {
        var schacht = Schacht("80401");
        schacht.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, userEdited: true);

        var plan = Baue(schacht);
        var feld = Assert.Single(Assert.Single(plan.Positionen).Felder);
        var organisation = Assert.Single(plan.Organisationen);

        Assert.Equal("EigentuemerRef", feld.Name);
        Assert.True(feld.IstVerweis);
        Assert.Equal("ch1000f000000001", feld.Alt);
        Assert.Equal(organisation.Tid, feld.Neu);
        Assert.Equal("Privat", organisation.Organisationstyp);
    }

    // Haltungen und Schaechte muessen sich EIN Organisationsbuch teilen. Sonst entstehen
    // fuer denselben Eigentuemer zwei Organisationen — oder schlimmer: zwei Objekte mit
    // derselben Kennung.
    [Fact]
    public void Haltung_und_Schacht_teilen_sich_eine_neue_Organisation()
    {
        var elemente = Elemente();
        var buch = new XtfOrganisationsbuch(elemente);

        var haltung = new HaltungRecord();
        haltung.SetFieldValue(FieldKeys.HoldingName, "80401-80402", FieldSource.Xtf, userEdited: false);
        haltung.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, userEdited: true);

        var schacht = Schacht("80401");
        schacht.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, userEdited: true);

        XtfStammdatenPlanBuilder.Build(new[] { haltung }, elemente, "SIA405_ABWASSER_2020_LV95", buch);
        XtfSchachtPlanBuilder.Build(new[] { schacht }, elemente, buch);

        Assert.Single(buch.Neue);
    }

    [Fact]
    public void Ein_fremder_Schacht_wird_gemeldet_statt_zugeordnet()
    {
        var schacht = Schacht("99999");
        schacht.SetFieldValue("Funktion", "Schlammsammler", FieldSource.Manual, userEdited: true);

        var plan = Baue(schacht);

        Assert.Empty(plan.Positionen);
        Assert.Contains(plan.Hinweise, h => h.Contains("nicht gefunden", StringComparison.Ordinal));
    }

    // 25 der 9739 Schachtnamen im Bestand kommen doppelt vor. Zwei Objekte mit demselben
    // Namen sind nicht eindeutig — dann wird nichts zugeordnet.
    [Fact]
    public void Eine_doppelte_Schachtnummer_wird_nicht_zugeordnet()
    {
        var doppelt = Kantonsexport.Replace(
            "</SIA405_ABWASSER_2020_LV95.SIA405_Abwasser>",
            """
              <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Normschacht TID="ch1000a00000c3e2">
                <Bezeichnung>80401</Bezeichnung>
                <Funktion>Einlaufschacht</Funktion>
              </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Normschacht>
            </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser>
            """,
            StringComparison.Ordinal);

        var schacht = Schacht("80401");
        schacht.SetFieldValue("Funktion", "Schlammsammler", FieldSource.Manual, userEdited: true);

        var plan = XtfSchachtPlanBuilder.Build(
            new[] { schacht },
            XtfStammdatenElementReader.Parse(XDocument.Parse(doppelt)));

        Assert.Empty(plan.Positionen);
        Assert.Contains(plan.Hinweise, h => h.Contains("mehrfach", StringComparison.Ordinal));
    }

    [Fact]
    public void Die_Schachtbemerkung_geht_hinaus()
    {
        var schacht = Schacht("80401");
        schacht.SetFieldValue(
            FieldKeys.Remarks, "Deckel nicht zu oeffnen", FieldSource.Manual, userEdited: true);

        var feld = Assert.Single(Assert.Single(Baue(schacht).Positionen).Felder);

        Assert.Equal("Bemerkung", feld.Name);
        Assert.Equal("Deckel nicht zu oeffnen", feld.Neu);
    }

    [Fact]
    public void Das_Wort_unbekannt_bleibt_in_einer_Bemerkung_stehen()
    {
        // Bei Funktion und Material ist "unbekannt" eine Leerformel und wird verworfen.
        // In einem Freitext ist es eine Aussage und muss bleiben.
        var schacht = Schacht("80401");
        schacht.SetFieldValue(FieldKeys.Remarks, "unbekannt", FieldSource.Manual, userEdited: true);

        var feld = Assert.Single(Assert.Single(Baue(schacht).Positionen).Felder);

        Assert.Equal("unbekannt", feld.Neu);
    }

    [Fact]
    public void Eine_zu_lange_Schachtbemerkung_wird_gemeldet_statt_gekuerzt()
    {
        var schacht = Schacht("80401");
        schacht.SetFieldValue(FieldKeys.Remarks, new string('a', 81), FieldSource.Manual, userEdited: true);

        var plan = Baue(schacht);

        Assert.Empty(plan.Positionen);
        Assert.Contains("81 Zeichen", Assert.Single(plan.Hinweise), StringComparison.Ordinal);
    }

    [Fact]
    public void Die_getrennten_Massfelder_gewinnen_gegen_das_alte_Textfeld()
    {
        // Seit 2026-09-02 gibt es "Dimension 1 mm" und "Dimension 2 mm" neben dem
        // aelteren Textfeld "Dimension". Bis 2026-09-03 las der Export nur das alte —
        // wer die getrennten Felder pflegte, bekam gar keine Masse in die Datei.
        var schacht = Schacht("80401");
        schacht.SetFieldValue(FieldKeys.ShaftDimension1Mm, "1100", FieldSource.Manual, true);
        schacht.SetFieldValue(FieldKeys.ShaftDimension2Mm, "900", FieldSource.Manual, true);

        var felder = Assert.Single(Baue(schacht).Positionen).Felder;

        Assert.Equal("1100", felder.Single(f => f.Name == "Dimension1").Neu);
        Assert.Equal("900", felder.Single(f => f.Name == "Dimension2").Neu);
    }

    [Fact]
    public void Ein_rundes_Mass_steht_in_beiden_Feldern()
    {
        var schacht = Schacht("80401");
        schacht.SetFieldValue(FieldKeys.ShaftDimension1Mm, "600", FieldSource.Manual, true);

        var felder = Assert.Single(Baue(schacht).Positionen).Felder;

        Assert.Equal("600", felder.Single(f => f.Name == "Dimension1").Neu);
        Assert.Equal("600", felder.Single(f => f.Name == "Dimension2").Neu);
    }

    [Fact]
    public void Das_alte_Textfeld_bleibt_der_Rueckfall()
    {
        var schacht = Schacht("80401");
        schacht.SetFieldValue("Dimension", "1100 x 900 mm", FieldSource.Manual, true);

        var felder = Assert.Single(Baue(schacht).Positionen).Felder;

        Assert.Equal("1100", felder.Single(f => f.Name == "Dimension1").Neu);
        Assert.Equal("900", felder.Single(f => f.Name == "Dimension2").Neu);
    }

    [Fact]
    public void Widersprechen_sich_beide_Angaben_wird_das_gemeldet()
    {
        var schacht = Schacht("80401");
        schacht.SetFieldValue("Dimension", "600 mm", FieldSource.Manual, true);
        schacht.SetFieldValue(FieldKeys.ShaftDimension1Mm, "1100", FieldSource.Manual, true);
        schacht.SetFieldValue(FieldKeys.ShaftDimension2Mm, "900", FieldSource.Manual, true);

        var plan = Baue(schacht);

        // Geschrieben werden die getrennten Felder, aber der Bericht sagt es.
        Assert.Equal("1100", Assert.Single(plan.Positionen).Felder.Single(f => f.Name == "Dimension1").Neu);
        Assert.Contains(plan.Hinweise, h => h.Contains("600", StringComparison.Ordinal));
    }

    [Fact]
    public void Eine_Form_die_nicht_zu_den_Massen_passt_wird_gemeldet()
    {
        // SIA405 kennt am Normschacht keine Form; ein ovaler Schacht ist dort einer mit
        // zwei verschiedenen Massen. Ein Widerspruch deutet auf einen Tippfehler.
        var schacht = Schacht("80401");
        schacht.SetFieldValue("Schachtform", "Rund", FieldSource.Manual, true);
        schacht.SetFieldValue(FieldKeys.ShaftDimension1Mm, "1100", FieldSource.Manual, true);
        schacht.SetFieldValue(FieldKeys.ShaftDimension2Mm, "900", FieldSource.Manual, true);

        Assert.Contains(Baue(schacht).Hinweise, h => h.Contains("Rund", StringComparison.Ordinal));
    }

    [Fact]
    public void Oval_mit_zwei_Massen_ist_kein_Widerspruch()
    {
        var schacht = Schacht("80401");
        schacht.SetFieldValue("Schachtform", "Oval", FieldSource.Manual, true);
        schacht.SetFieldValue(FieldKeys.ShaftDimension1Mm, "1100", FieldSource.Manual, true);
        schacht.SetFieldValue(FieldKeys.ShaftDimension2Mm, "900", FieldSource.Manual, true);

        Assert.DoesNotContain(Baue(schacht).Hinweise, h => h.Contains("Oval", StringComparison.Ordinal));
    }

    [Fact]
    public void Status_Sanierungsbedarf_und_Baujahr_gehen_mit()
    {
        var schacht = Schacht("80401");
        schacht.SetFieldValue(FieldKeys.OperatingStatus, "in_Betrieb", FieldSource.Manual, true);
        schacht.SetFieldValue(FieldKeys.RehabilitationNeed, "kurzfristig", FieldSource.Manual, true);
        schacht.SetFieldValue(FieldKeys.ConstructionYear, "1985", FieldSource.Manual, true);

        var felder = Assert.Single(Baue(schacht).Positionen).Felder;

        Assert.Equal("in_Betrieb", felder.Single(f => f.Name == "Status").Neu);
        Assert.Equal("kurzfristig", felder.Single(f => f.Name == "Sanierungsbedarf").Neu);
        Assert.Equal("1985", felder.Single(f => f.Name == "Baujahr").Neu);
    }

    private static IReadOnlyList<XtfStammdatenElement> Elemente()
        => XtfStammdatenElementReader.Parse(XDocument.Parse(Kantonsexport));

    private static XtfStammdatenPlan Baue(SchachtRecord schacht)
        => XtfSchachtPlanBuilder.Build(new[] { schacht }, Elemente());

    private static SchachtRecord Schacht(string nummer)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", nummer, FieldSource.Xtf, userEdited: false);
        return record;
    }
}
