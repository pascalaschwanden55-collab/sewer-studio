using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Was SewerStudio exportiert, muss beim Zurueckimportieren wieder dasselbe ergeben.
///
/// Am 2026-09-03 tat es das nicht: Aus einer exportierten Zustandsklasse Z0 wurde beim
/// Import eine 4, und aus dem Schacht "78998" der technische Name "78998-79002_von".
/// Beide Fehler lagen im Import und trafen auch die Kantonsdateien.
/// </summary>
public sealed class XtfRundreiseTests
{
    private const string MitZustand = """
<?xml version="1.0" encoding="utf-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION VERSION="2.3" SENDER="SewerStudio">
    <MODELS><MODEL NAME="SIA405_ABWASSER_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser BID="chB0000000000001">
      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Normschacht TID="chSSTSCHACHT0001">
        <BaulicherZustand>Z2</BaulicherZustand>
        <Bezeichnung>78998</Bezeichnung>
        <Dimension1>800</Dimension1>
        <Dimension2>800</Dimension2>
        <Funktion>Kontrollschacht</Funktion>
        <Material>Beton</Material>
        <EigentuemerRef REF="chSSTORGANISAT01" />
      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Normschacht>
      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Abwasserknoten TID="chSSTKNOTEN00001">
        <Bezeichnung>78998</Bezeichnung>
        <AbwasserbauwerkRef REF="chSSTSCHACHT0001" />
      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Abwasserknoten>
      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltungspunkt TID="chSSTPUNKT000001">
        <Bezeichnung>78998-79002_von</Bezeichnung>
        <AbwassernetzelementRef REF="chSSTKNOTEN00001" />
      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltungspunkt>
      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltungspunkt TID="chSSTPUNKT000002">
        <Bezeichnung>78998-79002_nach</Bezeichnung>
      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltungspunkt>
      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Kanal TID="chSSTKANAL000001">
        <BaulicherZustand>Z0</BaulicherZustand>
        <Bezeichnung>78998-79002</Bezeichnung>
        <Nutzungsart_Ist>Schmutzabwasser</Nutzungsart_Ist>
        <EigentuemerRef REF="chSSTORGANISAT01" />
      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Kanal>
      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung TID="chSSTHALTUNG0001">
        <Bezeichnung>78998-79002</Bezeichnung>
        <Material>Zement</Material>
        <Lichte_Hoehe>100</Lichte_Hoehe>
        <vonHaltungspunktRef REF="chSSTPUNKT000001" />
        <nachHaltungspunktRef REF="chSSTPUNKT000002" />
        <AbwasserbauwerkRef REF="chSSTKANAL000001" />
      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Haltung>
    </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser>
    <SIA405_Base_Abwasser_LV95.Administration BID="chB0000000000002">
      <SIA405_Base_Abwasser_LV95.Administration.Organisation TID="chSSTORGANISAT01">
        <Bezeichnung>Privat</Bezeichnung>
        <Organisationstyp>Privat</Organisationstyp>
        <Status>aktiv</Status>
      </SIA405_Base_Abwasser_LV95.Administration.Organisation>
    </SIA405_Base_Abwasser_LV95.Administration>
  </DATASECTION>
</TRANSFER>
""";

    [Fact]
    public void Die_Zustandsklasse_aus_der_Datei_kommt_im_Projekt_an()
    {
        // Frueher wurde BaulicherZustand gar nicht gelesen: Aus Z0 wurde eine 4.
        var record = LiesEine();

        Assert.Equal("0", record.GetFieldValue("Zustandsklasse"));
        Assert.Equal(FieldSource.Xtf405, record.FieldMeta["Zustandsklasse"].Source);
    }

    [Fact]
    public void Der_Schachtname_kommt_vom_Abwasserknoten_nicht_vom_Haltungspunkt()
    {
        // "78998-79002_von" ist ein technischer Name. Der Schacht heisst "78998".
        var record = LiesEine();

        Assert.Equal("78998", record.GetFieldValue("Schacht_oben"));
    }

    [Fact]
    public void Ohne_Abwasserknoten_bleibt_die_Bezeichnung_der_Rueckfall()
    {
        // Der zweite Haltungspunkt hat keinen Knoten — dann ist sein Name das Beste,
        // was die Datei hergibt. Besser als gar nichts, aber erkennbar technisch.
        var record = LiesEine();

        Assert.Equal("78998-79002_nach", record.GetFieldValue("Schacht_unten"));
    }

    [Fact]
    public void Die_uebrigen_Felder_kommen_unveraendert_an()
    {
        var record = LiesEine();

        Assert.Equal("78998-79002", record.GetFieldValue(FieldKeys.HoldingName));
        Assert.Equal("Zement", record.GetFieldValue(FieldKeys.PipeMaterial));
        Assert.Equal("100", record.GetFieldValue(FieldKeys.NominalDiameterMm));
    }

    [Theory]
    [InlineData("Z0", "0")]
    [InlineData("Z4", "4")]
    [InlineData("unbekannt", "")]
    [InlineData("", "")]
    [InlineData("Z9", "")]
    [InlineData("Quatsch", "")]
    public void Nur_gueltige_Zustandswerte_werden_uebernommen(string ausDerDatei, string erwartet)
    {
        var xml = MitZustand.Replace("<BaulicherZustand>Z0</BaulicherZustand>",
            ausDerDatei.Length == 0 ? "" : $"<BaulicherZustand>{ausDerDatei}</BaulicherZustand>");

        var record = Lies(xml);

        Assert.Equal(erwartet, record.GetFieldValue("Zustandsklasse"));
    }

    [Fact]
    public void Der_Eigentuemer_kommt_aus_dem_Verweis_auf_die_Organisation()
    {
        // In SIA405 ist der Eigentuemer kein Text, sondern ein Verweis. Wer nur nach
        // einem Element "Eigentuemer" sucht, findet nichts — und beim naechsten Export
        // fehlt genau die Angabe, die dort Pflicht ist.
        var record = LiesEine();

        Assert.Equal("Privat", record.GetFieldValue(FieldKeys.Owner));
    }

    [Fact]
    public void Auch_der_Schacht_kommt_vollstaendig_zurueck()
    {
        var schacht = Assert.Single(LiesProjekt().SchaechteData);

        Assert.Equal("78998", schacht.GetFieldValue("Schachtnummer"));
        Assert.Equal("Kontrollschacht", schacht.GetFieldValue("Funktion"));
        Assert.Equal("Beton", schacht.GetFieldValue("Material"));
        Assert.Equal("800", schacht.GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.Equal("800", schacht.GetFieldValue(FieldKeys.ShaftDimension2Mm));
    }

    [Fact]
    public void Der_Schacht_behaelt_Zustand_und_Eigentuemer()
    {
        // Beide fehlten bis 2026-09-03: BaulicherZustand wurde am Normschacht nicht
        // gelesen, und der Eigentuemer stand als Verweis da, nicht als Text.
        var schacht = Assert.Single(LiesProjekt().SchaechteData);

        Assert.Equal("2", schacht.GetFieldValue(FieldKeys.ConditionClass));
        Assert.Equal("Privat", schacht.GetFieldValue(FieldKeys.Owner));
    }

    // Die Breite eines Rechteck- oder Eiprofils steht in SIA405 als Hoehen-Breiten-
    // Verhaeltnis am Rohrprofil. Auf dem Rueckweg muss daraus wieder die Breite werden.
    [Fact]
    public void Profiltyp_und_Breite_kommen_ueber_das_Rohrprofil_zurueck()
    {
        var xml = MitZustand
            .Replace(
                "<Lichte_Hoehe>100</Lichte_Hoehe>",
                "<Lichte_Hoehe>1000</Lichte_Hoehe>\n        <RohrprofilRef REF=\"chSSTPROFIL00001\" />",
                StringComparison.Ordinal)
            .Replace(
                "    </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser>",
                """
                      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Rohrprofil TID="chSSTPROFIL00001">
                        <Bezeichnung>Rechteckprofil 1.66667</Bezeichnung>
                        <HoehenBreitenverhaeltnis>1.66667</HoehenBreitenverhaeltnis>
                        <Profiltyp>Rechteckprofil</Profiltyp>
                      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Rohrprofil>
                    </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser>
                """,
                StringComparison.Ordinal);

        var record = Lies(xml);

        Assert.Equal("1000", record.GetFieldValue(FieldKeys.NominalDiameterMm));
        Assert.Equal("600", record.GetFieldValue(FieldKeys.ClearWidthMm));
        Assert.Equal("Rechteckprofil", record.GetFieldValue(FieldKeys.ProfileType));
        Assert.Equal(FieldSource.Xtf405, record.FieldMeta[FieldKeys.ClearWidthMm].Source);
    }

    [Fact]
    public void Ein_Kreisprofil_setzt_die_Breite_gleich_der_Hoehe()
    {
        var xml = MitZustand
            .Replace(
                "<Lichte_Hoehe>100</Lichte_Hoehe>",
                "<Lichte_Hoehe>300</Lichte_Hoehe>\n        <RohrprofilRef REF=\"chSSTPROFIL00001\" />",
                StringComparison.Ordinal)
            .Replace(
                "    </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser>",
                """
                      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Rohrprofil TID="chSSTPROFIL00001">
                        <Bezeichnung>Kreisprofil</Bezeichnung>
                        <Profiltyp>Kreisprofil</Profiltyp>
                      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Rohrprofil>
                    </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser>
                """,
                StringComparison.Ordinal);

        var record = Lies(xml);

        Assert.Equal("300", record.GetFieldValue(FieldKeys.ClearWidthMm));
        Assert.Equal("Kreisprofil", record.GetFieldValue(FieldKeys.ProfileType));
    }

    private static HaltungRecord LiesEine() => Assert.Single(LiesProjekt().Data);

    private static Project LiesProjekt() => LiesProjektAus(MitZustand);

    private static HaltungRecord Lies(string xml) => Assert.Single(LiesProjektAus(xml).Data);

    private static Project LiesProjektAus(string xml)
    {
        var ordner = Path.Combine(Path.GetTempPath(), $"rundreise_{Guid.NewGuid():N}");
        Directory.CreateDirectory(ordner);
        var datei = Path.Combine(ordner, "export.xtf");
        try
        {
            File.WriteAllText(datei, xml);
            var projekt = new Project { Name = "Rundreise" };
            var stats = new LegacyXtfImportService().ImportXtfFiles([datei], projekt);

            Assert.True(stats.Errors == 0, string.Join(" | ", stats.Messages));
            return projekt;
        }
        finally
        {
            if (Directory.Exists(ordner)) Directory.Delete(ordner, recursive: true);
        }
    }
}
