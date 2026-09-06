using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Arbeitspaket 8: Der Ein-Knopf-Import muss Schacht-Sammelprotokolle selbst anschliessen.
///
/// Vorher (Stand c1021e76e) uebersprang der Import die Schaechte ausdruecklich
/// (<c>IncludeSchacht: false</c>) und ueberliess sie dem manuellen Befehl
/// „Schacht Verteilen". Ein vollstaendiger Projektimport liess sie damit leer.
/// </summary>
public sealed class SchachtprotokollImportAnschlussTests
{
    private sealed class AufzeichnenderVerteiler : IShaftDistributionService
    {
        public ShaftDistributionRequest? LetzteAnfrage { get; private set; }
        public ShaftDistributionResult Antwort { get; set; } =
            new([], UsesPersistentProjectTransaction: false);

        public ShaftDistributionResult Distribute(ShaftDistributionRequest request)
        {
            LetzteAnfrage = request;
            return Antwort;
        }
    }

    [Fact]
    public void DerImport_RuftDieSchachtverteilungMitDemArchivOrdnerAuf()
    {
        MitQuelle((quelle, projektOrdner) =>
        {
            var verteiler = new AufzeichnenderVerteiler();
            var orch = Orchestrator(verteiler);

            orch.Import(quelle, projektOrdner, new Project());

            Assert.NotNull(verteiler.LetzteAnfrage);
            Assert.EndsWith(
                Path.Combine("Importdateien", "PDF"),
                verteiler.LetzteAnfrage!.PdfSourceFolder!,
                StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(
                ProjectStructure.SchaechteVerteilt,
                verteiler.LetzteAnfrage.DestinationFolder,
                StringComparison.Ordinal);
            // Kein zweiter Splitter und keine eigene Dateiliste — der Dienst sucht selbst.
            Assert.Null(verteiler.LetzteAnfrage.PdfFiles);
        });
    }

    [Fact]
    public void VerteilteSchachtprotokolle_WerdenVerknuepftUndGezaehlt()
    {
        MitQuelle((quelle, projektOrdner) =>
        {
            var zielPdf = Path.Combine(projektOrdner, ProjectStructure.SchaechteVerteilt, "3133", "20200703_3133.pdf");
            var schachtOrdner = Path.GetDirectoryName(zielPdf)!;
            var verteiler = new AufzeichnenderVerteiler
            {
                Antwort = new ShaftDistributionResult(
                    [new ShaftDistributionItem(true, "OK (Schachtprotokoll)", "sammel.pdf", zielPdf, zielPdf, schachtOrdner)],
                    UsesPersistentProjectTransaction: false)
            };

            var projekt = new Project();
            var schacht = new SchachtRecord();
            schacht.SetFieldValue("Schachtnummer", "3133");
            projekt.SchaechteData.Add(schacht);

            var ergebnis = Orchestrator(verteiler).Import(quelle, projektOrdner, projekt);

            // MakeRelativeIfInsideProject liefert Vorwaertsschraegstriche — dieselbe
            // Schreibweise wie beim manuellen Befehl „Schacht Verteilen".
            Assert.Equal("Schächte_Verteilt/3133/20200703_3133.pdf", schacht.GetFieldValue("PDF_Path"));
            Assert.Contains(ergebnis.Messages,
                m => m.Contains("Schachtprotokolle: 1 verteilt", StringComparison.Ordinal)
                     && m.Contains("1 mit einem Schacht verknuepft", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void UnbekannterSchacht_ErscheintImBerichtUndLegtNichtsAn()
    {
        MitQuelle((quelle, projektOrdner) =>
        {
            var zielPdf = Path.Combine(projektOrdner, ProjectStructure.SchaechteVerteilt, "9999", "20200703_9999.pdf");
            var verteiler = new AufzeichnenderVerteiler
            {
                Antwort = new ShaftDistributionResult(
                    [new ShaftDistributionItem(true, "OK", "sammel.pdf", zielPdf, zielPdf, Path.GetDirectoryName(zielPdf)!)],
                    UsesPersistentProjectTransaction: false)
            };

            var projekt = new Project();
            var ergebnis = Orchestrator(verteiler).Import(quelle, projektOrdner, projekt);

            Assert.Empty(projekt.SchaechteData);
            Assert.Contains(ergebnis.Messages,
                m => m.Contains("9999", StringComparison.Ordinal)
                     && m.Contains("nicht bekannt", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void EinFehlerBeiDenSchaechten_BrichtDenImportNichtAb()
    {
        MitQuelle((quelle, projektOrdner) =>
        {
            var verteiler = new AufzeichnenderVerteiler
            {
                Antwort = new ShaftDistributionResult(
                    [new ShaftDistributionItem(false, "Zugriff verweigert", "sammel.pdf", null, null, null)],
                    UsesPersistentProjectTransaction: false)
            };

            var ergebnis = Orchestrator(verteiler).Import(quelle, projektOrdner, new Project());

            // Die Haltung aus der XTF ist trotzdem da …
            Assert.Equal(1, ergebnis.Found);
            // … und der Schachtfehler steht im Bericht.
            Assert.Contains(ergebnis.Messages,
                m => m.Contains("Zugriff verweigert", StringComparison.Ordinal));
        });
    }

    // ---------------------------------------------------------------------

    private static ProjectImportOrchestrator Orchestrator(IShaftDistributionService verteiler)
        => new(
            new XtfImportServiceAdapter(),
            new WinCanDbImportService(),
            shaftDistribution: verteiler);

    private static void MitQuelle(Action<string, string> pruefung)
    {
        var wurzel = Path.Combine(Path.GetTempPath(), $"ap8-schacht-{Guid.NewGuid():N}");
        var quelle = Path.Combine(wurzel, "quelle");
        var projektOrdner = Path.Combine(wurzel, "projekt");
        Directory.CreateDirectory(quelle);
        Directory.CreateDirectory(projektOrdner);

        File.WriteAllText(Path.Combine(quelle, "test.xtf"), """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1">
        <Bezeichnung>1000-2000</Bezeichnung>
        <vonPunktBezeichnung>1000</vonPunktBezeichnung>
        <bisPunktBezeichnung>2000</bisPunktBezeichnung>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""");

        try { pruefung(quelle, projektOrdner); }
        finally { try { Directory.Delete(wurzel, recursive: true); } catch { } }
    }
}
