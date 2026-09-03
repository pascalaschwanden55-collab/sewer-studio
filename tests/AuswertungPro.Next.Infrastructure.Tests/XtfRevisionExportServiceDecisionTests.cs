using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfRevisionExportServiceDecisionTests
{
    [Fact]
    public void Nur_Pruefen_meldet_offene_Entscheidungen_als_Fehler()
    {
        var temp = Path.Combine(Path.GetTempPath(), "xtf-revision-offen-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var quelle = Path.Combine(temp, "mehrdeutig.xtf");
        File.WriteAllText(quelle, MehrdeutigeQuelle);

        try
        {
            var projekt = new Project();
            projekt.Data.Add(MehrdeutigeHaltung(Path.GetFileName(quelle)));

            var ergebnis = new XtfRevisionExportService().Erzeuge(
                new XtfRevisionExportRequest(
                    projekt,
                    Path.Combine(temp, "projekt.json"),
                    Path.Combine(temp, "Ausgabe"),
                    NurPruefen: true,
                    Quelldateien: [quelle]));

            Assert.False(ergebnis.Ok);
            Assert.Contains("offene Faelle", ergebnis.Fehler, StringComparison.Ordinal);
            Assert.Contains("offen:", ergebnis.Bericht, StringComparison.Ordinal);
            Assert.Empty(ergebnis.Dateien);
            Assert.False(Directory.Exists(Path.Combine(temp, "Ausgabe")));
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { }
        }
    }

    private static HaltungRecord MehrdeutigeHaltung(string quelldatei)
    {
        var erster = Eintrag();
        var zweiter = Eintrag();
        var record = new HaltungRecord
        {
            XtfHerkunft = new XtfHerkunft
            {
                Datei = quelldatei,
                Modell = "VSA_KEK_2020_LV95",
                UntersuchungTid = "U1"
            },
            Protocol = new ProtocolDocument
            {
                HaltungId = "06-001",
                Original = new ProtocolRevision
                {
                    Entries =
                    [
                        ProtocolEntryCloner.CloneLegacyProtocolEntry(erster),
                        ProtocolEntryCloner.CloneLegacyProtocolEntry(zweiter)
                    ]
                },
                Current = new ProtocolRevision
                {
                    Entries =
                    [
                        ProtocolEntryCloner.CloneLegacyProtocolEntry(erster),
                        ProtocolEntryCloner.CloneLegacyProtocolEntry(zweiter)
                    ]
                }
            }
        };
        record.SetFieldValue(FieldKeys.HoldingName, "06-001", FieldSource.Xtf, false);
        return record;
    }

    private static ProtocolEntry Eintrag()
        => new()
        {
            EntryId = Guid.NewGuid(),
            Code = "BAB",
            MeterStart = 5.00,
            Source = ProtocolEntrySource.Imported
        };

    private const string MehrdeutigeQuelle = """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1">
        <Bezeichnung>06-001</Bezeichnung>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="A1">
        <UntersuchungRef REF="U1" />
        <KanalSchadencode>BAB</KanalSchadencode>
        <Distanz>5.00</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="A2">
        <UntersuchungRef REF="U1" />
        <KanalSchadencode>BAB</KanalSchadencode>
        <Distanz>5.00</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""";
}
