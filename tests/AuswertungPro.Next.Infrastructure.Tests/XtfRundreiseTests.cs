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
    public void Ohne_Abwasserknoten_kommt_der_Schacht_aus_dem_Haltungsnamen()
    {
        // Der zweite Haltungspunkt hat keinen Knoten. Sein Name "78998-79002_nach" ist
        // technisch; der Schacht steckt im Haltungsnamen: "79002". Frueher landete der
        // technische Name in "Schacht_unten".
        var record = LiesEine();

        Assert.Equal("79002", record.GetFieldValue("Schacht_unten"));
    }

    [Fact]
    public void Die_uebrigen_Felder_kommen_unveraendert_an()
    {
        var record = LiesEine();

        Assert.Equal("78998-79002", record.GetFieldValue(FieldKeys.HoldingName));
        Assert.Equal("Zement", record.GetFieldValue(FieldKeys.PipeMaterial));
        Assert.Equal("100", record.GetFieldValue(FieldKeys.NominalDiameterMm));
    }

    [Fact]
    public void Katasterkennungen_bleiben_fuer_Haltung_und_Schacht_erhalten()
    {
        // Die XTF fuehrt keine separate Objekt-ID; ihre TID ist die Identitaet. Ohne
        // dieses Merkmal wuerde der Erstexport vorhandene Katasterobjekte neu anlegen.
        var projekt = LiesProjekt();

        Assert.Equal("chSSTHALTUNG0001", Assert.Single(projekt.Data).GetFieldValue(FieldKeys.CadastreObjectId));
        Assert.Equal("chSSTSCHACHT0001", Assert.Single(projekt.SchaechteData).GetFieldValue(FieldKeys.CadastreObjectId));
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
    public void Ein_leeres_Altfeld_verdeckt_keinen_gueltigen_Organisationsverweis()
    {
        var xml = MitZustand.Replace(
            "<EigentuemerRef REF=\"chSSTORGANISAT01\" />",
            "<Eigentuemer />\n        <EigentuemerRef REF=\"chSSTORGANISAT01\" />",
            StringComparison.Ordinal);

        var projekt = LiesProjektAus(xml);

        Assert.Equal("Privat", Assert.Single(projekt.Data).GetFieldValue(FieldKeys.Owner));
        Assert.Equal("Privat", Assert.Single(projekt.SchaechteData).GetFieldValue(FieldKeys.Owner));
    }

    [Fact]
    public void Datenherr_und_Datenlieferant_kommen_in_die_eigenen_Felder_zurueck()
    {
        var xml = MitZustand
            .Replace(
                "<EigentuemerRef REF=\"chSSTORGANISAT01\" />",
                """
                <DatenherrRef REF="chSSTDATENHERR01" />
                        <DatenlieferantRef REF="chSSTDATENLIEF01" />
                        <EigentuemerRef REF="chSSTORGANISAT01" />
                """,
                StringComparison.Ordinal)
            .Replace(
                "    </SIA405_Base_Abwasser_LV95.Administration>",
                """
                      <SIA405_Base_Abwasser_LV95.Administration.Organisation TID="chSSTDATENHERR01">
                        <Bezeichnung>Abwasser Uri</Bezeichnung>
                        <Organisationstyp>Amt</Organisationstyp>
                        <Status>aktiv</Status>
                      </SIA405_Base_Abwasser_LV95.Administration.Organisation>
                      <SIA405_Base_Abwasser_LV95.Administration.Organisation TID="chSSTDATENLIEF01">
                        <Bezeichnung>Inspektor AG</Bezeichnung>
                        <Organisationstyp>Privat</Organisationstyp>
                        <Status>aktiv</Status>
                      </SIA405_Base_Abwasser_LV95.Administration.Organisation>
                    </SIA405_Base_Abwasser_LV95.Administration>
                """,
                StringComparison.Ordinal);

        var projekt = LiesProjektAus(xml);
        var haltung = Assert.Single(projekt.Data);
        var schacht = Assert.Single(projekt.SchaechteData);

        Assert.Equal("Abwasser Uri", haltung.GetFieldValue(FieldKeys.DataOwner));
        Assert.Equal("Inspektor AG", haltung.GetFieldValue(FieldKeys.DataSupplier));
        Assert.Equal("Abwasser Uri", schacht.GetFieldValue(FieldKeys.DataOwner));
        Assert.Equal("Inspektor AG", schacht.GetFieldValue(FieldKeys.DataSupplier));
    }

    [Fact]
    public void Das_Organisationswort_unbekannt_bleibt_auch_an_der_Haltung_erhalten()
    {
        var xml = MitZustand
            .Replace(
                "<EigentuemerRef REF=\"chSSTORGANISAT01\" />",
                """
                <DatenherrRef REF="chSSTUNBEKANNT01" />
                        <DatenlieferantRef REF="chSSTUNBEKANNT01" />
                        <EigentuemerRef REF="chSSTORGANISAT01" />
                """,
                StringComparison.Ordinal)
            .Replace(
                "    </SIA405_Base_Abwasser_LV95.Administration>",
                """
                      <SIA405_Base_Abwasser_LV95.Administration.Organisation TID="chSSTUNBEKANNT01">
                        <Bezeichnung>unbekannt</Bezeichnung>
                        <Organisationstyp>Privat</Organisationstyp>
                        <Status>aktiv</Status>
                      </SIA405_Base_Abwasser_LV95.Administration.Organisation>
                    </SIA405_Base_Abwasser_LV95.Administration>
                """,
                StringComparison.Ordinal);

        var haltung = Assert.Single(LiesProjektAus(xml).Data);

        Assert.Equal("unbekannt", haltung.GetFieldValue(FieldKeys.DataOwner));
        Assert.Equal("unbekannt", haltung.GetFieldValue(FieldKeys.DataSupplier));
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

    [Fact]
    public void Ein_unbekannter_Profiltyp_bleibt_beim_Import_waehlbar()
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
                        <Bezeichnung>Unbekannt</Bezeichnung>
                        <Profiltyp>unbekannt</Profiltyp>
                      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Rohrprofil>
                    </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser>
                """,
                StringComparison.Ordinal);

        var record = Lies(xml);

        Assert.Equal("Unbekannt", record.GetFieldValue(FieldKeys.ProfileType));
    }

    // Punkt 4 der Fremdanalyse vom 2026-09-03: Status, Sanierungsbedarf, beide Funktionen
    // und die Lagebestimmung verschwanden beim Rueckimport, und das Inspektionsdatum
    // 06.10.2025 wurde zum Aenderungsdatum 03.09.2026.
    [Fact]
    public void Die_Kanalfelder_kommen_zurueck_und_das_Aenderungsdatum_ist_kein_Inspektionsdatum()
    {
        var xml = MitZustand
            .Replace(
                "<Nutzungsart_Ist>Schmutzabwasser</Nutzungsart_Ist>",
                """
                <Sanierungsbedarf>kurzfristig</Sanierungsbedarf>
                        <Status>in_Betrieb</Status>
                        <FunktionHierarchisch>SAA.Liegenschaftsentwaesserung</FunktionHierarchisch>
                        <FunktionHydraulisch>Freispiegelleitung</FunktionHydraulisch>
                        <Nutzungsart_Ist>Schmutzabwasser</Nutzungsart_Ist>
                """,
                StringComparison.Ordinal)
            .Replace(
                "<Lichte_Hoehe>100</Lichte_Hoehe>",
                "<Letzte_Aenderung>20260903</Letzte_Aenderung>\n        <Lichte_Hoehe>100</Lichte_Hoehe>\n        <Lagebestimmung>genau</Lagebestimmung>",
                StringComparison.Ordinal);

        var record = Lies(xml);

        Assert.Equal("in_Betrieb", record.GetFieldValue(FieldKeys.OperatingStatus));
        Assert.Equal("kurzfristig", record.GetFieldValue(FieldKeys.RehabilitationNeed));
        Assert.Equal("SAA.Liegenschaftsentwaesserung", record.GetFieldValue(FieldKeys.HierarchicalFunction));
        Assert.Equal("Freispiegelleitung", record.GetFieldValue(FieldKeys.HydraulicFunction));
        Assert.Equal("genau", record.GetFieldValue(FieldKeys.PositionAccuracy));
        Assert.Equal("03.09.2026", record.GetFieldValue(FieldKeys.CadastreLastChange));
        Assert.True(string.IsNullOrEmpty(record.GetFieldValue("Datum_Jahr")), "Letzte_Aenderung ist kein Inspektionsdatum");
    }

    [Fact]
    public void Echter_Erstexport_und_Rueckimport_behalten_Felder_und_Masse()
    {
        var ausgang = new Project { Name = "Rundreise", Id = Guid.Parse("11111111-2222-3333-4444-555555555555") };
        var haltung = new HaltungRecord();
        haltung.SetFieldValue(FieldKeys.HoldingName, "S1-S2", FieldSource.Manual, true);
        haltung.SetFieldValue("Schacht_oben", "S1", FieldSource.Manual, true);
        haltung.SetFieldValue("Schacht_unten", "S2", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.DataOwner, "Abwasser Uri", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.DataSupplier, "Abwasser Uri", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.UsageType, "Schmutzabwasser", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.ConditionClass, "0", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.HierarchicalFunction, "SAA.Liegenschaftsentwaesserung", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.HydraulicFunction, "Freispiegelleitung", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.OperatingStatus, "in_Betrieb", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.RehabilitationNeed, "kurzfristig", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.ConstructionYear, "1999", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.GrossCost, "1250.50", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.Remarks, "Rundweg", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.PipeMaterial, "Zement", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.NominalDiameterMm, "1000", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.ClearWidthMm, "600", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.ProfileType, "Rechteckprofil", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.HoldingLengthMeters, "12.5", FieldSource.Manual, true);
        haltung.SetFieldValue(FieldKeys.PositionAccuracy, "genau", FieldSource.Manual, true);
        ausgang.Data.Add(haltung);

        ausgang.SchaechteData.Add(Schacht("S1", "1100", "900"));
        ausgang.SchaechteData.Add(Schacht("S2", "600", "600"));

        var geometrien = new Dictionary<string, XtfNeuGeometrie>(StringComparer.OrdinalIgnoreCase)
        {
            ["S1-S2"] = new("Verlauf", [new(2_690_000, 1_190_000), new(2_690_012, 1_190_005)])
        };
        var plan = XtfNeuPlanBuilder.Build(
            ausgang.Data, ausgang.SchaechteData, ausgang.Id.ToString("N"), geometrien);
        Assert.Equal(1, plan.Haltungen);
        Assert.Equal(2, plan.Schaechte);

        var ordner = Path.Combine(Path.GetTempPath(), $"xtf_echte_rundreise_{Guid.NewGuid():N}");
        Directory.CreateDirectory(ordner);
        try
        {
            var datei = Path.Combine(ordner, "erstexport.xtf");
            var geschrieben = XtfNeuWriter.Schreibe(plan, datei, new DateOnly(2026, 9, 3));
            Assert.True(geschrieben.Ok, geschrieben.Fehler);

            var rueckweg = new Project { Name = "Rueckweg" };
            var statistik = new LegacyXtfImportService().ImportXtfFiles([datei], rueckweg);
            Assert.Equal(0, statistik.Errors);

            var zurueck = Assert.Single(rueckweg.Data);
            Assert.Equal("S1-S2", zurueck.GetFieldValue(FieldKeys.HoldingName));
            Assert.Equal("Privat", zurueck.GetFieldValue(FieldKeys.Owner));
            Assert.Equal("Abwasser Uri", zurueck.GetFieldValue(FieldKeys.DataOwner));
            Assert.Equal("Abwasser Uri", zurueck.GetFieldValue(FieldKeys.DataSupplier));
            Assert.Equal("Schmutzabwasser", zurueck.GetFieldValue(FieldKeys.UsageType));
            Assert.Equal("0", zurueck.GetFieldValue(FieldKeys.ConditionClass));
            Assert.Equal("SAA.Liegenschaftsentwaesserung", zurueck.GetFieldValue(FieldKeys.HierarchicalFunction));
            Assert.Equal("Freispiegelleitung", zurueck.GetFieldValue(FieldKeys.HydraulicFunction));
            Assert.Equal("in_Betrieb", zurueck.GetFieldValue(FieldKeys.OperatingStatus));
            Assert.Equal("kurzfristig", zurueck.GetFieldValue(FieldKeys.RehabilitationNeed));
            Assert.Equal("1999", zurueck.GetFieldValue(FieldKeys.ConstructionYear));
            Assert.Equal("1250.50", zurueck.GetFieldValue(FieldKeys.GrossCost));
            Assert.Equal("Rundweg", zurueck.GetFieldValue(FieldKeys.Remarks));
            Assert.Equal("Zement", zurueck.GetFieldValue(FieldKeys.PipeMaterial));
            Assert.Equal("1000", zurueck.GetFieldValue(FieldKeys.NominalDiameterMm));
            Assert.Equal("600", zurueck.GetFieldValue(FieldKeys.ClearWidthMm));
            Assert.Equal("Rechteckprofil", zurueck.GetFieldValue(FieldKeys.ProfileType));
            Assert.Equal("12.50", zurueck.GetFieldValue(FieldKeys.HoldingLengthMeters));
            Assert.Equal("genau", zurueck.GetFieldValue(FieldKeys.PositionAccuracy));
            Assert.False(string.IsNullOrWhiteSpace(zurueck.GetFieldValue(FieldKeys.CadastreObjectId)));

            var schacht1 = Assert.Single(rueckweg.SchaechteData, s => s.GetFieldValue("Schachtnummer") == "S1");
            Assert.Equal("1100", schacht1.GetFieldValue(FieldKeys.ShaftDimension1Mm));
            Assert.Equal("900", schacht1.GetFieldValue(FieldKeys.ShaftDimension2Mm));
            Assert.Equal("Privat", schacht1.GetFieldValue(FieldKeys.Owner));
            Assert.Equal("Abwasser Uri", schacht1.GetFieldValue(FieldKeys.DataOwner));
            Assert.Equal("Abwasser Uri", schacht1.GetFieldValue(FieldKeys.DataSupplier));
            Assert.Equal("Kontrollschacht", schacht1.GetFieldValue("Funktion"));
            Assert.Equal("Beton", schacht1.GetFieldValue("Material"));
            Assert.False(string.IsNullOrWhiteSpace(schacht1.GetFieldValue(FieldKeys.CadastreObjectId)));

            var schacht2 = Assert.Single(rueckweg.SchaechteData, s => s.GetFieldValue("Schachtnummer") == "S2");
            Assert.Equal("600", schacht2.GetFieldValue(FieldKeys.ShaftDimension1Mm));
            Assert.Equal("600", schacht2.GetFieldValue(FieldKeys.ShaftDimension2Mm));
        }
        finally
        {
            if (Directory.Exists(ordner))
                Directory.Delete(ordner, recursive: true);
        }

        static SchachtRecord Schacht(string nummer, string dimension1, string dimension2)
        {
            var schacht = new SchachtRecord();
            schacht.SetFieldValue("Schachtnummer", nummer, FieldSource.Manual, true);
            schacht.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, true);
            schacht.SetFieldValue(FieldKeys.DataOwner, "Abwasser Uri", FieldSource.Manual, true);
            schacht.SetFieldValue(FieldKeys.DataSupplier, "Abwasser Uri", FieldSource.Manual, true);
            schacht.SetFieldValue("Funktion", "Kontrollschacht", FieldSource.Manual, true);
            schacht.SetFieldValue("Material", "Beton", FieldSource.Manual, true);
            schacht.SetFieldValue(FieldKeys.ShaftDimension1Mm, dimension1, FieldSource.Manual, true);
            schacht.SetFieldValue(FieldKeys.ShaftDimension2Mm, dimension2, FieldSource.Manual, true);
            return schacht;
        }
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
