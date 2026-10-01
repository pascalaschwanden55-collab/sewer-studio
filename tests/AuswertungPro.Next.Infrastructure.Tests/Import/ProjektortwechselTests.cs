using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Arbeitspaket 11 des Uebergabeplans vom 2026-09-05: der vollstaendige Ablauf an einem
/// kleinen kuenstlichen Projekt — importieren, speichern, neu laden, den Projektordner
/// verschieben und alles noch einmal oeffnen.
///
/// Die bestehenden Tests decken die Teilschritte ab. Was fehlte, ist der Weg als Ganzes:
/// Genau dort faellt auf, wenn ein Link zwar richtig gesetzt, aber absolut gespeichert
/// wird. In einer Kopie des fertigen Projekts ist er dann tot, und der Bericht meldet
/// trotzdem "0 Fehler".
///
/// Ausschliesslich kuenstliche Daten; keine Kundenoriginale.
/// </summary>
public sealed class ProjektortwechselTests
{
    [Fact]
    public void ImportSpeichernNeuLadenUndVerschieben_BehaeltAlleVerknuepfungen()
    {
        var wurzel = Path.Combine(Path.GetTempPath(), $"ap11-ortwechsel-{Guid.NewGuid():N}");
        var quelle = Path.Combine(wurzel, "quelle");
        var ortA = Path.Combine(wurzel, "ProjektA");
        var ortB = Path.Combine(wurzel, "ProjektB");

        try
        {
            BaueQuelle(quelle);

            // ---- 1. Importieren ------------------------------------------
            var projekt = new Project();
            var ergebnis = new ProjectImportOrchestrator(
                    new XtfImportServiceAdapter(), new WinCanDbImportService())
                .Import(quelle, ortA, projekt);

            Assert.Equal(0, ergebnis.Errors);
            Assert.Equal(2, projekt.Data.Count);
            Assert.NotNull(ergebnis.Bestand);
            Assert.Equal(2, ergebnis.Bestand!.HaltungenMitVideo);

            // Die Kundenquelle bleibt unveraendert — der Import liest sie nur.
            Assert.Equal(4, Directory.EnumerateFiles(quelle, "*", SearchOption.AllDirectories).Count());

            // ---- 2. Speichern und neu laden ------------------------------
            var projektDateiA = Path.Combine(ortA, "Projektdateien", "projekt.json");
            Directory.CreateDirectory(Path.GetDirectoryName(projektDateiA)!);
            var repo = new JsonProjectRepository();
            var gespeichert = repo.Save(projekt, projektDateiA);
            Assert.True(gespeichert.Ok, gespeichert.ErrorMessage);

            var geladenA = repo.Load(projektDateiA);
            Assert.True(geladenA.Ok, geladenA.ErrorMessage);
            PruefeVerknuepfungen(geladenA.Value!, ortA, "nach dem Neuladen am urspruenglichen Ort");

            // ---- 3. Projektort wechseln ----------------------------------
            KopiereBaum(ortA, ortB);
            var geladenB = repo.Load(Path.Combine(ortB, "Projektdateien", "projekt.json"));
            Assert.True(geladenB.Ok, geladenB.ErrorMessage);
            PruefeVerknuepfungen(geladenB.Value!, ortB, "nach dem Projektortwechsel");

            // ---- 4. Der Bestand hat sich durch den Ortwechsel nicht veraendert
            var vorher = ImportBestandszaehler.Zaehle(geladenA.Value!);
            var nachher = ImportBestandszaehler.Zaehle(geladenB.Value!);
            Assert.Equal(vorher, nachher);
        }
        finally
        {
            try { Directory.Delete(wurzel, recursive: true); } catch { }
        }
    }

    [Fact]
    public void EinZweiterIdentischerImport_LegtKeineZweitenBauwerkeUndKeineZweitenDateienAn()
    {
        var wurzel = Path.Combine(Path.GetTempPath(), $"ap11-wiederholung-{Guid.NewGuid():N}");
        var quelle = Path.Combine(wurzel, "quelle");
        var ort = Path.Combine(wurzel, "Projekt");

        try
        {
            BaueQuelle(quelle);

            var projekt = new Project();
            var orchestrator = new ProjectImportOrchestrator(
                new XtfImportServiceAdapter(), new WinCanDbImportService());

            var erster = orchestrator.Import(quelle, ort, projekt);
            var dateienNachErstem = Zaehle(ort);
            var bestandNachErstem = ImportBestandszaehler.Zaehle(projekt);

            var zweiter = orchestrator.Import(quelle, ort, projekt);
            var dateienNachZweitem = Zaehle(ort);
            var bestandNachZweitem = ImportBestandszaehler.Zaehle(projekt);

            Assert.Equal(0, erster.Errors);
            Assert.Equal(0, zweiter.Errors);
            Assert.Equal(bestandNachErstem, bestandNachZweitem);
            Assert.True(dateienNachErstem == dateienNachZweitem,
                "Der zweite Lauf hat Dateien angelegt: " + string.Join(" | ", Directory
                    .EnumerateFiles(ort, "*", SearchOption.AllDirectories)
                    .Select(f => f.Replace(ort, ""))));
        }
        finally
        {
            try { Directory.Delete(wurzel, recursive: true); } catch { }
        }
    }

    // ---------------------------------------------------------------------

    private static void PruefeVerknuepfungen(Project projekt, string projektOrdner, string wann)
    {
        Assert.Equal(2, projekt.Data.Count);

        foreach (var haltung in projekt.Data)
        {
            var name = haltung.GetFieldValue(FieldKeys.HoldingName);
            foreach (var feld in new[] { FieldKeys.Link, "Link_G" })
            {
                var link = haltung.GetFieldValue(feld);
                if (string.IsNullOrWhiteSpace(link))
                    continue;

                Assert.False(Path.IsPathRooted(link),
                    $"{wann}: {name}.{feld} ist absolut gespeichert ({link}) und ueberlebt "
                    + "einen Projektortwechsel nicht.");
                Assert.True(File.Exists(Path.Combine(projektOrdner, link)),
                    $"{wann}: {name}.{feld} zeigt auf {link}, dort liegt aber keine Datei.");
            }
        }

        Assert.Equal(3, projekt.Data.Sum(h => h.VsaFindings?.Count ?? 0));
    }

    private static int Zaehle(string ordner)
        => Directory.EnumerateFiles(ordner, "*", SearchOption.AllDirectories).Count();

    private static void KopiereBaum(string quelle, string ziel)
    {
        foreach (var verzeichnis in Directory.EnumerateDirectories(quelle, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(verzeichnis.Replace(quelle, ziel, StringComparison.Ordinal));

        Directory.CreateDirectory(ziel);
        foreach (var datei in Directory.EnumerateFiles(quelle, "*", SearchOption.AllDirectories))
            File.Copy(datei, datei.Replace(quelle, ziel, StringComparison.Ordinal), overwrite: true);
    }

    /// <summary>Kleiner kuenstlicher IKAS-Export: eine XTF und drei Videodateien.</summary>
    private static void BaueQuelle(string quelle)
    {
        Directory.CreateDirectory(quelle);

        File.WriteAllText(Path.Combine(quelle, "vsa_kek.xtf"), """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1">
        <Bezeichnung>1000-2000</Bezeichnung>
        <Zeitpunkt>2026-03-04</Zeitpunkt>
        <vonPunktBezeichnung>1000</vonPunktBezeichnung>
        <bisPunktBezeichnung>2000</bisPunktBezeichnung>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="S1">
        <UntersuchungRef REF="U1" />
        <KanalSchadencode>BAB</KanalSchadencode>
        <Distanz>2.50</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="S2">
        <UntersuchungRef REF="U1" />
        <KanalSchadencode>BBC</KanalSchadencode>
        <Distanz>7.10</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U2">
        <Bezeichnung>3000-4000</Bezeichnung>
        <Zeitpunkt>2026-03-05</Zeitpunkt>
        <vonPunktBezeichnung>3000</vonPunktBezeichnung>
        <bisPunktBezeichnung>4000</bisPunktBezeichnung>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="S3">
        <UntersuchungRef REF="U2" />
        <KanalSchadencode>BAF</KanalSchadencode>
        <Distanz>1.20</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
      <VSA_KEK_2020_LV95.KEK.Datei TID="D1">
        <Art>Video</Art>
        <Klasse>Untersuchung</Klasse>
        <Objekt>U1</Objekt>
        <Bezeichnung>1000-2000.mpg</Bezeichnung>
        <Relativpfad></Relativpfad>
      </VSA_KEK_2020_LV95.KEK.Datei>
      <VSA_KEK_2020_LV95.KEK.Datei TID="D2">
        <Art>Video</Art>
        <Klasse>Untersuchung</Klasse>
        <Objekt>U2</Objekt>
        <Bezeichnung>3000-4000.mpg</Bezeichnung>
        <Relativpfad></Relativpfad>
      </VSA_KEK_2020_LV95.KEK.Datei>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""");

        File.WriteAllText(Path.Combine(quelle, "1000-2000.mpg"), "Hinfahrt 1000-2000");
        File.WriteAllText(Path.Combine(quelle, "1000-2000_G.mpg"), "Rueckfahrt 1000-2000");
        File.WriteAllText(Path.Combine(quelle, "3000-4000.mpg"), "Hinfahrt 3000-4000");
    }
}
