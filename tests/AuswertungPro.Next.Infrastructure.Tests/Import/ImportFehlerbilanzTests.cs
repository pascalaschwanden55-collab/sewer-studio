using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Arbeitspaket 1 des Uebergabeplans vom 2026-09-05: Kein Teilschritt darf seine Fehler
/// vor dem sichtbaren Bericht verlieren.
///
/// Vorher (gemessen am Stand c1021e76e): Die Kopierfehler der name-basierten
/// Protokollverteilung standen in <c>ProtocolDistributionReport.Meldungen</c> und wurden
/// nie gelesen; die Fehler der Fotoverteilung standen nur im Meldungstext und fehlten in
/// der Gesamtzahl. Der Lauf meldete beides Mal "0 Fehler".
/// </summary>
public sealed class ImportFehlerbilanzTests
{
    // ---------------------------------------------------------------------
    // Testdoubles
    // ---------------------------------------------------------------------

    private sealed class FehlerhafterProtokollVerteiler : INameBasedProtocolDistributor
    {
        private readonly IReadOnlyList<string> _meldungen;

        public FehlerhafterProtokollVerteiler(params string[] meldungen) => _meldungen = meldungen;

        public ProtocolDistributionReport Distribute(
            Project project, string projectFolder, string sourceFolder, object? collectionLock = null)
            => new(0, 0, 0, Array.Empty<string>(), _meldungen);

        public ProtocolDistributionReport Distribute(
            Project project, string projectFolder, string sourceFolder,
            object? collectionLock, IImportFileStagingSession? fileStaging)
            => Distribute(project, projectFolder, sourceFolder, collectionLock);
    }

    private sealed class FehlerhafteFotoVerteilung : IImportMediaDistributionService
    {
        private readonly int _fehler;

        public FehlerhafteFotoVerteilung(int fehler) => _fehler = fehler;

        public ImportMediaDistributionResult Distribute(ImportMediaDistributionRequest request)
            => new(FilesCopied: 0, FilesSkipped: 0, Errors: _fehler,
                   Messages: Enumerable.Range(1, _fehler).Select(i => $"Foto {i}: Zugriff verweigert").ToList());
    }

    // ---------------------------------------------------------------------
    // Orchestrator
    // ---------------------------------------------------------------------

    [Fact]
    public void Kopierfehler_DerNamensverteilung_ErscheintMitQuelleImErgebnis()
    {
        var (quelle, projekt) = MiniIkasQuelle();
        try
        {
            var orch = new ProjectImportOrchestrator(
                new XtfImportServiceAdapter(),
                new WinCanDbImportService(),
                protocolDistributor: new FehlerhafterProtokollVerteiler(
                    "20200702_100-200.pdf: Der Prozess kann nicht auf die Datei zugreifen."));

            var ergebnis = orch.Import(quelle, projekt, new Project());

            Assert.True(ergebnis.Errors >= 1,
                "Ein Kopierfehler der Namensverteilung muss in der Gesamtfehlerzahl auftauchen.");
            Assert.Contains(ergebnis.Messages,
                m => m.Contains("20200702_100-200.pdf", StringComparison.Ordinal));
            var schritt = Assert.Single(
                ergebnis.Fehlerbilanz.Schritte.Where(s => s.Schritt == "Name-basierte Protokollverteilung"));
            Assert.Equal(1, schritt.Anzahl);
            Assert.Contains(schritt.Gruende, g => g.Contains("20200702_100-200.pdf", StringComparison.Ordinal));
        }
        finally
        {
            Aufraeumen(quelle);
        }
    }

    [Fact]
    public void Fotofehler_ZaehlenInDerGesamtzahlUndInDerBilanz()
    {
        var (quelle, projekt) = MiniIkasQuelle();
        try
        {
            var orch = new ProjectImportOrchestrator(
                new XtfImportServiceAdapter(),
                new WinCanDbImportService(),
                mediaDistributor: new FehlerhafteFotoVerteilung(2));

            var ergebnis = orch.Import(quelle, projekt, new Project());

            var schritt = Assert.Single(
                ergebnis.Fehlerbilanz.Schritte.Where(s => s.Schritt == "Fotoverteilung"));
            Assert.Equal(2, schritt.Anzahl);
            Assert.True(ergebnis.Errors >= 2,
                $"Fotofehler fehlen in der Gesamtzahl: {ergebnis.Errors}");
        }
        finally
        {
            Aufraeumen(quelle);
        }
    }

    [Fact]
    public void Bilanzsumme_EntsprichtImmerDerGesamtfehlerzahl()
    {
        var (quelle, projekt) = MiniIkasQuelle();
        try
        {
            var orch = new ProjectImportOrchestrator(
                new XtfImportServiceAdapter(),
                new WinCanDbImportService(),
                protocolDistributor: new FehlerhafterProtokollVerteiler("a.pdf: kaputt", "b.pdf: kaputt"),
                mediaDistributor: new FehlerhafteFotoVerteilung(3));

            var ergebnis = orch.Import(quelle, projekt, new Project());

            // Keine Doppelzaehlung und kein verlorener Schritt.
            Assert.Equal(ergebnis.Errors, ergebnis.Fehlerbilanz.Gesamt);
            Assert.True(ergebnis.Errors >= 5, $"Erwartet mindestens 5 Fehler, gezaehlt {ergebnis.Errors}");
        }
        finally
        {
            Aufraeumen(quelle);
        }
    }

    [Fact]
    public void FehlerfreierLauf_MeldetKeineSchrittfehler()
    {
        var (quelle, projekt) = MiniIkasQuelle();
        try
        {
            var orch = new ProjectImportOrchestrator(
                new XtfImportServiceAdapter(),
                new WinCanDbImportService());

            var ergebnis = orch.Import(quelle, projekt, new Project());

            Assert.Equal(ergebnis.Errors, ergebnis.Fehlerbilanz.Gesamt);
            Assert.Empty(ergebnis.Fehlerbilanz.Berichtszeilen());
        }
        finally
        {
            Aufraeumen(quelle);
        }
    }

    // ---------------------------------------------------------------------
    // Vollstaendigkeits-Satz
    // ---------------------------------------------------------------------

    [Fact]
    public void OhneSollzahl_WirdKeineVollstaendigkeitBehauptet()
    {
        var ergebnis = new OneClickProjectImportResult(
            OneClickProjectImportFormat.Ikas, 12, 12, 0, 0, 0, Array.Empty<string>());

        var satz = OneClickImportVollstaendigkeit.Beschreibe(ergebnis);

        Assert.Contains("nicht vollstaendig geprueft", satz, StringComparison.Ordinal);
    }

    [Fact]
    public void MitPassenderSollzahl_GiltDerLaufAlsGeprueft()
    {
        var quellen = new QuellenwahlErgebnis(
            null,
            [new QuellenVersuch("C:\\Quelle\\projekt.db3", QuellenBefund.Tauglich(12, "12 Haltungen"))]);

        var ergebnis = new OneClickProjectImportResult(
            OneClickProjectImportFormat.WinCan, 12, 12, 0, 0, 0, Array.Empty<string>())
        {
            ErwarteteHaltungen = 12,
            BearbeiteteHaltungen = 12,
            Quellenprotokoll = quellen,
            Bestand = new ImportBestandsbilanz(12, 12, 0, 12, 12, 12, 0, 0) { DateienGeprueft = true }
        };

        var satz = OneClickImportVollstaendigkeit.Beschreibe(ergebnis);

        Assert.StartsWith("geprueft", satz, StringComparison.Ordinal);
        Assert.Contains("12 von 12", satz, StringComparison.Ordinal);
    }

    [Fact]
    public void MitFehlern_GiltDerLaufNieAlsVollstaendig()
    {
        var quellen = new QuellenwahlErgebnis(
            null,
            [new QuellenVersuch("C:\\Quelle\\projekt.db3", QuellenBefund.Tauglich(12, "12 Haltungen"))]);

        var ergebnis = new OneClickProjectImportResult(
            OneClickProjectImportFormat.WinCan, 12, 12, 0, Errors: 3, Conflicts: 0, Messages: Array.Empty<string>())
        {
            ErwarteteHaltungen = 12,
            BearbeiteteHaltungen = 12,
            Quellenprotokoll = quellen,
            Fehlerbilanz = new ImportFehlerbilanz(
                [new ImportSchrittFehler("Fotoverteilung", 3, ["Foto 1: Zugriff verweigert"])])
        };

        var satz = OneClickImportVollstaendigkeit.Beschreibe(ergebnis);

        Assert.StartsWith("nicht vollstaendig", satz, StringComparison.Ordinal);
        Assert.Contains("3 Fehler", satz, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // Kleine IKAS-Quelle (kuenstlich, keine Kundendaten)
    // ---------------------------------------------------------------------

    private static (string quelle, string projekt) MiniIkasQuelle()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ap1-bilanz-{Guid.NewGuid():N}");
        var quelle = Path.Combine(root, "quelle");
        var projekt = Path.Combine(root, "projekt");
        Directory.CreateDirectory(quelle);
        Directory.CreateDirectory(projekt);

        File.WriteAllText(Path.Combine(quelle, "test.xtf"), """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1">
        <Bezeichnung>100-200</Bezeichnung>
        <vonPunktBezeichnung>100</vonPunktBezeichnung>
        <bisPunktBezeichnung>200</bisPunktBezeichnung>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="S1">
        <UntersuchungRef REF="U1" />
        <KanalSchadencode>BAB</KanalSchadencode>
        <Distanz>2.50</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""");

        return (quelle, projekt);
    }

    private static void Aufraeumen(string quelle)
    {
        try { Directory.Delete(Path.GetDirectoryName(quelle)!, recursive: true); } catch { }
    }
}
