using System.Xml.Linq;
using AuswertungPro.Next.Application.Export.Geonis;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Export.Geonis;

namespace AuswertungPro.Next.Infrastructure.Tests.Export.Geonis;

/// <summary>
/// Verhaltenstests des GEONIS-Rueckschriebs an einer kleinen, echten Katasterdatei.
/// Geprueft wird das, was im Ernstfall Schaden anrichten wuerde: falscher Schluessel,
/// erfundene Werte, unvollstaendige Objekte.
/// </summary>
public sealed class Sia405GeonisExportTests : IDisposable
{
    private const string TopicPrefix = "SIA405_Abwasser_2015_LV95.SIA405_Abwasser";

    private readonly string _ordner;
    private readonly string _katasterPfad;

    public Sia405GeonisExportTests()
    {
        _ordner = Path.Combine(Path.GetTempPath(), "geonis-export-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_ordner);
        _katasterPfad = Path.Combine(_ordner, "kataster.xtf");
        File.WriteAllText(_katasterPfad, KatasterXtf());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_ordner, recursive: true);
        }
        catch (IOException)
        {
            // Aufraeumen darf einen Test nie zum Scheitern bringen.
        }
    }

    [Fact]
    public void Katasterleser_liestIdentitaetVokabularUndModell()
    {
        var index = new Sia405KatasterIndexReader().Lies(_katasterPfad);

        Assert.Equal(TopicPrefix, index.Modell.TopicPrefix);
        Assert.Equal("xTest", index.Modell.BasketId);
        Assert.Equal("http://www.interlis.ch/INTERLIS2.3", index.Modell.TransferNamespace);
        Assert.Equal("SIA405_Abwasser_2015_LV95", Assert.Single(index.Modell.Modelle).Name);

        var haltung = index.Haltungen[Sia405NameKey.Normalize("78998-79002")];
        Assert.Equal("H1", haltung.Tid);
        Assert.Equal("ch14h00000000001", haltung.ObjId);
        Assert.Equal("K1", haltung.KanalTid);
        Assert.Equal("P1", haltung.RohrprofilTid);
        Assert.Equal("150", haltung.LichteHoehe);

        // Doppelte Bezeichnung: beide Seiten werden verworfen, nicht "erster gewinnt".
        Assert.Contains(Sia405NameKey.Normalize("1.01"), index.MehrdeutigeHaltungen);
        Assert.False(index.Haltungen.ContainsKey(Sia405NameKey.Normalize("1.01")));

        Assert.Equal("Steinzeug", index.MaterialVokabular["STEINZEUG"]);
        Assert.Equal("Zement", index.MaterialVokabular["ZEMENT"]);
        Assert.Contains("Z2", index.ZustandVokabular);
        Assert.Equal("2020-01-01", index.LetzteAenderungBeispiel);
    }

    [Fact]
    public void Planer_uebernimmtNurBeurteilteWerteUndKenntDenSchluessel()
    {
        var index = new Sia405KatasterIndexReader().Lies(_katasterPfad);
        var plan = new Sia405ExportPlanBuilder().Erstelle(
            Projekt(),
            index,
            new Sia405ExportOptionen(new DateOnly(2026, 9, 4), _katasterPfad));

        var haltung = plan.Objekte.Single(o => o.Art == Sia405ObjektArt.Haltung);
        Assert.Equal("ch14h00000000001", haltung.ObjId);
        Assert.Equal("100", Wert(haltung, "Lichte_Hoehe"));
        Assert.Equal("100", Wert(haltung, "Lichte_Breite"));
        Assert.Equal("Zement", Wert(haltung, "Material"));
        Assert.Equal("2026-09-04", Wert(haltung, "Letzte_Aenderung"));

        var kanal = plan.Objekte.Single(o => o.Art == Sia405ObjektArt.Kanal);
        Assert.Equal("ch14k00000000001", kanal.ObjId);
        Assert.Equal("Z0", Wert(kanal, "Baulicher_Zustand"));
        Assert.Equal("Das ist ein Test", Wert(kanal, "Bemerkung"));

        var schacht = plan.Objekte.Single(o => o.Art == Sia405ObjektArt.Normschacht);
        Assert.Equal("1100", Wert(schacht, "Dimension1"));
        Assert.Equal("900", Wert(schacht, "Dimension2"));
        Assert.Equal("Z4", Wert(schacht, "Baulicher_Zustand"));

        // Das Rohrprofil wird unveraendert mitgeliefert, damit GEONIS die Breite ableiten kann.
        var profil = plan.Objekte.Single(o => o.Art == Sia405ObjektArt.Rohrprofil);
        Assert.Empty(profil.Aenderungen);
    }

    [Fact]
    public void Planer_schreibtNichtsBeiMehrdeutigerBezeichnungOderUnbekanntemMaterial()
    {
        var index = new Sia405KatasterIndexReader().Lies(_katasterPfad);

        var projekt = new Project { Name = "Test" };
        projekt.Data.Add(Haltung("1.01", dn: "300", material: "Beton", zustand: "", bemerkung: ""));
        projekt.Data.Add(Haltung("78998-79002", dn: "", material: "Glasfaser", zustand: "", bemerkung: ""));

        var plan = new Sia405ExportPlanBuilder().Erstelle(
            projekt,
            index,
            new Sia405ExportOptionen(new DateOnly(2026, 9, 4), _katasterPfad));

        Assert.Empty(plan.Objekte);
        Assert.Contains(plan.Hinweise, h => h.Objekt == "1.01" && h.Grund.Contains("mehrfach", StringComparison.Ordinal));
        Assert.Contains(plan.Hinweise, h => h.Grund.Contains("Glasfaser", StringComparison.Ordinal));
    }

    [Fact]
    public void Export_schreibtVollstaendigeObjekteMitEingefuegtemZustand()
    {
        var ergebnis = GeonisXtfExportRuntime.Erzeuge().Fuehre(new GeonisXtfExportRequest(
            Projekt(),
            _katasterPfad,
            Path.Combine(_ordner, "ausgabe"),
            new DateOnly(2026, 9, 4),
            NurTrockenlauf: false));

        Assert.True(ergebnis.Erfolgreich);
        Assert.NotNull(ergebnis.XtfPfad);
        Assert.NotNull(ergebnis.ProtokollPfad);
        Assert.True(File.Exists(ergebnis.XtfPfad!));
        Assert.True(File.Exists(ergebnis.ProtokollPfad!));

        var dokument = XDocument.Load(ergebnis.XtfPfad!);
        var behaelter = dokument.Descendants().Single(e => e.Name.LocalName == TopicPrefix);
        Assert.Equal("xTest", behaelter.Attribute("BID")?.Value);

        var haltung = Objekt(dokument, "Haltung", "H1");
        Assert.Equal("100", Kind(haltung, "Lichte_Hoehe"));
        Assert.Equal("100", Kind(haltung, "Lichte_Breite"));
        Assert.Equal("Zement", Kind(haltung, "Material"));
        Assert.Equal("2026-09-04", Kind(haltung, "Letzte_Aenderung"));
        // Das ganze Objekt wird geliefert: Pflichtangaben und Verweise bleiben erhalten.
        Assert.Equal("ch14h00000000001", Kind(haltung, "OBJ_ID"));
        Assert.Equal("AWU", Kind(haltung, "Datenherr"));
        Assert.Equal("P1", haltung.Elements().Single(e => e.Name.LocalName == "rohrprofilRef").Attribute("REF")?.Value);

        // Baulicher_Zustand fehlte am Kanal und muss an der Modellstelle stehen, nicht am Schluss.
        var kanal = Objekt(dokument, "Kanal", "K1");
        var reihenfolge = kanal.Elements().Select(e => e.Name.LocalName).ToList();
        Assert.Equal(
            new[] { "OBJ_ID", "Bezeichnung", "Baulicher_Zustand", "Bemerkung", "Letzte_Aenderung", "Datenherr" },
            reihenfolge);
        Assert.Equal("Z0", Kind(kanal, "Baulicher_Zustand"));

        var schacht = Objekt(dokument, "Normschacht", "S1");
        Assert.Equal("1100", Kind(schacht, "Dimension1"));
        Assert.Equal("900", Kind(schacht, "Dimension2"));
        Assert.Equal("Z4", Kind(schacht, "Baulicher_Zustand"));

        Assert.Equal("1.0", Kind(Objekt(dokument, "Rohrprofil", "P1"), "HoehenBreitenverhaeltnis"));

        // Unberuehrte Objekte gehoeren nicht in die Datei.
        Assert.Empty(dokument.Descendants().Where(e => e.Attribute("TID")?.Value == "H2"));
        Assert.Empty(dokument.Descendants().Where(e => e.Attribute("TID")?.Value == "K2"));

        var protokoll = File.ReadAllText(ergebnis.ProtokollPfad!);
        Assert.Contains("ch14h00000000001", protokoll, StringComparison.Ordinal);
        Assert.Contains("150 -> 100", protokoll, StringComparison.Ordinal);
    }

    [Fact]
    public void Trockenlauf_schreibtNurDasProtokoll()
    {
        var ziel = Path.Combine(_ordner, "trocken");
        var ergebnis = GeonisXtfExportRuntime.Erzeuge().Fuehre(new GeonisXtfExportRequest(
            Projekt(),
            _katasterPfad,
            ziel,
            new DateOnly(2026, 9, 4),
            NurTrockenlauf: true));

        Assert.True(ergebnis.Erfolgreich);
        Assert.Null(ergebnis.XtfPfad);
        Assert.True(File.Exists(ergebnis.ProtokollPfad!));
        Assert.Empty(Directory.GetFiles(ziel, "*.xtf"));
    }

    private static Project Projekt()
    {
        var projekt = new Project { Name = "Testprojekt" };
        projekt.Data.Add(Haltung("78998-79002", dn: "100", material: "Zement", zustand: "0", bemerkung: "Das ist ein Test"));

        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "78998");
        schacht.SetFieldValue("Dimension", "1100 x 900 mm");
        schacht.SetFieldValue(FieldKeys.ConditionClass, "4");
        projekt.SchaechteData.Add(schacht);

        return projekt;
    }

    private static HaltungRecord Haltung(string name, string dn, string material, string zustand, string bemerkung)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.NominalDiameterMm, dn, FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.PipeMaterial, material, FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.ConditionClass, zustand, FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.Remarks, bemerkung, FieldSource.Manual, userEdited: true);
        return record;
    }

    private static string? Wert(Sia405ExportObjekt objekt, string attribut)
        => objekt.Aenderungen.FirstOrDefault(a => a.Attribut == attribut)?.Neu;

    private static XElement Objekt(XDocument dokument, string klasse, string tid)
        => dokument.Descendants()
            .Single(e => e.Name.LocalName == TopicPrefix + "." + klasse && e.Attribute("TID")?.Value == tid);

    private static string? Kind(XElement objekt, string name)
        => objekt.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;

    private static string KatasterXtf() =>
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
          <HEADERSECTION SENDER="Kanton" VERSION="2.3">
            <MODELS>
              <MODEL NAME="SIA405_Abwasser_2015_LV95" VERSION="2015-06-01" URI="http://www.sia.ch/405" />
            </MODELS>
          </HEADERSECTION>
          <DATASECTION>
            <SIA405_Abwasser_2015_LV95.SIA405_Abwasser BID="xTest">
              <SIA405_Abwasser_2015_LV95.SIA405_Abwasser.Rohrprofil TID="P1">
                <OBJ_ID>ch14p00000000001</OBJ_ID>
                <Bezeichnung>Kreisprofil</Bezeichnung>
                <Profiltyp>Kreisprofil</Profiltyp>
                <HoehenBreitenverhaeltnis>1.0</HoehenBreitenverhaeltnis>
              </SIA405_Abwasser_2015_LV95.SIA405_Abwasser.Rohrprofil>
              <SIA405_Abwasser_2015_LV95.SIA405_Abwasser.Kanal TID="K1">
                <OBJ_ID>ch14k00000000001</OBJ_ID>
                <Bezeichnung>78998-79002</Bezeichnung>
                <Letzte_Aenderung>2020-01-01</Letzte_Aenderung>
                <Datenherr>AWU</Datenherr>
              </SIA405_Abwasser_2015_LV95.SIA405_Abwasser.Kanal>
              <SIA405_Abwasser_2015_LV95.SIA405_Abwasser.Kanal TID="K2">
                <OBJ_ID>ch14k00000000002</OBJ_ID>
                <Bezeichnung>1.01</Bezeichnung>
                <Baulicher_Zustand>Z2</Baulicher_Zustand>
                <Bemerkung>Sammelkanal</Bemerkung>
                <Letzte_Aenderung>2020-01-01</Letzte_Aenderung>
                <Datenherr>AWU</Datenherr>
              </SIA405_Abwasser_2015_LV95.SIA405_Abwasser.Kanal>
              <SIA405_Abwasser_2015_LV95.SIA405_Abwasser.Haltung TID="H1">
                <OBJ_ID>ch14h00000000001</OBJ_ID>
                <Bezeichnung>78998-79002</Bezeichnung>
                <Lichte_Hoehe>150</Lichte_Hoehe>
                <Lichte_Breite>150</Lichte_Breite>
                <Material>Steinzeug</Material>
                <Letzte_Aenderung>2020-01-01</Letzte_Aenderung>
                <Datenherr>AWU</Datenherr>
                <AbwasserbauwerkRef REF="K1" />
                <rohrprofilRef REF="P1" />
              </SIA405_Abwasser_2015_LV95.SIA405_Abwasser.Haltung>
              <SIA405_Abwasser_2015_LV95.SIA405_Abwasser.Haltung TID="H2">
                <OBJ_ID>ch14h00000000002</OBJ_ID>
                <Bezeichnung>1.01</Bezeichnung>
                <Lichte_Hoehe>300</Lichte_Hoehe>
                <Material>Zement</Material>
                <Datenherr>AWU</Datenherr>
              </SIA405_Abwasser_2015_LV95.SIA405_Abwasser.Haltung>
              <SIA405_Abwasser_2015_LV95.SIA405_Abwasser.Haltung TID="H3">
                <OBJ_ID>ch14h00000000003</OBJ_ID>
                <Bezeichnung>1.01</Bezeichnung>
                <Lichte_Hoehe>400</Lichte_Hoehe>
                <Material>Beton</Material>
                <Datenherr>AWU</Datenherr>
              </SIA405_Abwasser_2015_LV95.SIA405_Abwasser.Haltung>
              <SIA405_Abwasser_2015_LV95.SIA405_Abwasser.Normschacht TID="S1">
                <OBJ_ID>ch14s00000000001</OBJ_ID>
                <Bezeichnung>78998</Bezeichnung>
                <Baulicher_Zustand>Z2</Baulicher_Zustand>
                <Dimension1>500</Dimension1>
                <Dimension2>500</Dimension2>
                <Letzte_Aenderung>2020-01-01</Letzte_Aenderung>
                <Datenherr>AWU</Datenherr>
              </SIA405_Abwasser_2015_LV95.SIA405_Abwasser.Normschacht>
            </SIA405_Abwasser_2015_LV95.SIA405_Abwasser>
          </DATASECTION>
        </TRANSFER>
        """;
}
