using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Import.WinCan;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Integrationstests fuer ProjectImportOrchestrator (Task 7, Ein-Knopf-Import).
/// </summary>
public sealed class ProjectImportOrchestratorTests
{
    // -----------------------------------------------------------------------
    // Hilfsmethode: Mini-IKAS-Fixture anlegen
    // -----------------------------------------------------------------------

    /// <summary>
    /// Legt einen temporaeren IKAS-Quellordner an:
    ///   test.xtf       – VSA_KEK-XTF mit 1 Untersuchung, 2 Kanalschaeden, 1 Foto-Datei, 1 Video-Datei
    ///   Foto\H_06-001_002.jpg
    ///   Film\H_06-001.mpg
    /// Gibt Quellpfad und Projektpfad zurueck.
    /// </summary>
    private static (string sourceDir, string projectDir) ErstelleMiniIkasFixture()
    {
        var root = Path.Combine(Path.GetTempPath(), $"orch-ikas-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(root, "source");
        var projectDir = Path.Combine(root, "projekt");

        // XTF liegt direkt im sourceDir-Root, Foto- und Film-Ordner ebenfalls,
        // damit ResolveVsaPhotoPath/ResolveVsaVideoPath (baseDir=sourceDir) greift.
        Directory.CreateDirectory(Path.Combine(sourceDir, "Foto"));
        Directory.CreateDirectory(Path.Combine(sourceDir, "Film"));
        Directory.CreateDirectory(projectDir);

        // VSA_KEK-XTF mit Foto- UND Video-Datei-Element
        var xtfContent = """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1">
        <Bezeichnung>06-001</Bezeichnung>
        <Zeitpunkt>2026-06-26</Zeitpunkt>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="S_BCD">
        <UntersuchungRef REF="U1" />
        <KanalSchadencode>BCD</KanalSchadencode>
        <Distanz>0.00</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="S_BAA">
        <UntersuchungRef REF="U1" />
        <KanalSchadencode>BAA</KanalSchadencode>
        <Distanz>1.90</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
      <VSA_KEK_2020_LV95.KEK.Datei TID="D1">
        <Art>Foto</Art>
        <Klasse>Kanalschaden</Klasse>
        <Objekt>S_BAA</Objekt>
        <Bezeichnung>H_06-001_002.jpg</Bezeichnung>
        <Relativpfad>Foto</Relativpfad>
      </VSA_KEK_2020_LV95.KEK.Datei>
      <VSA_KEK_2020_LV95.KEK.Datei TID="DV1">
        <Art>Film</Art>
        <Klasse>Untersuchung</Klasse>
        <Objekt>U1</Objekt>
        <Bezeichnung>H_06-001.mpg</Bezeichnung>
        <Relativpfad>Film</Relativpfad>
      </VSA_KEK_2020_LV95.KEK.Datei>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""";
        // XTF direkt im sourceDir (nicht in Unterordner), damit Pfad-Aufloesung klappt
        File.WriteAllText(Path.Combine(sourceDir, "test.xtf"), xtfContent);

        // Foto-Datei und Video-Datei (Inhalt beliebig)
        File.WriteAllText(Path.Combine(sourceDir, "Foto", "H_06-001_002.jpg"), "dummy-bild");
        File.WriteAllText(Path.Combine(sourceDir, "Film", "H_06-001.mpg"), "dummy-video");

        return (sourceDir, projectDir);
    }

    // -----------------------------------------------------------------------
    // Test 1: IKAS-Import verknuepft Fotos und archiviert XTF
    // -----------------------------------------------------------------------

    [Fact]
    public void Import_Ikas_LinksPhotosPerObservation_AndArchives()
    {
        var (sourceDir, projectDir) = ErstelleMiniIkasFixture();
        try
        {
            var project = new Project();
            var orch = new ProjectImportOrchestrator(
                new XtfImportServiceAdapter(),
                new WinCanDbImportService());

            var result = orch.Import(sourceDir, projectDir, project);

            // Format muss IKAS sein
            Assert.Equal(KanalExportFormat.Ikas, result.Format);

            // Record "06-001" muss existieren
            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "06-001", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);

            // BAA muss ein FotoPath haben, BCD nicht
            var baa = rec!.VsaFindings?.FirstOrDefault(f =>
                string.Equals(f.KanalSchadencode, "BAA", StringComparison.OrdinalIgnoreCase));
            var bcd = rec.VsaFindings?.FirstOrDefault(f =>
                string.Equals(f.KanalSchadencode, "BCD", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(baa);
            Assert.False(string.IsNullOrWhiteSpace(baa!.FotoPath),
                "BAA-Befund muss FotoPath haben");
            Assert.Contains("H_06-001_002.jpg", baa.FotoPath!);
            Assert.True(string.IsNullOrWhiteSpace(bcd?.FotoPath),
                "BCD-Befund darf kein Foto haben");

            // XTF muss archiviert worden sein
            var archivedXtf = Path.Combine(
                projectDir, ProjectStructure.Importdateien, ProjectStructure.XtfDir, "test.xtf");
            Assert.True(File.Exists(archivedXtf),
                $"Archivierte XTF nicht gefunden: {archivedXtf}");

            // Foto muss im Projekt-Ordner Fotos\Haltungen\06-001\ liegen
            // (nach MediaDistributionService)
            var fotoDir = ProjectStructure.FotosHaltungDir(projectDir, "06-001");
            Assert.True(
                Directory.Exists(fotoDir) && Directory.GetFiles(fotoDir).Length > 0,
                $"Kein Foto unter {fotoDir} nach Verteilung");

            // Video muss im Projekt-Ordner Haltungen_Verteilt\06-001\Video\ liegen
            // (MediaDistributionService.CopyFieldFile legt GetSubfolder(".mpg")="Video" an)
            var videoDir = Path.Combine(
                ProjectStructure.HaltungVerteiltDir(projectDir, "06-001"), "Video");
            Assert.True(
                Directory.Exists(videoDir) && Directory.GetFiles(videoDir).Length > 0,
                $"Kein Inspektionsvideo unter {videoDir} nach Verteilung (IKAS-Link-Gap)");
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(sourceDir)!, recursive: true); } catch { }
        }
    }

    // -----------------------------------------------------------------------
    // Test 1b: IKAS-Import setzt Link-Feld und verteilt Inspektionsfilm
    // -----------------------------------------------------------------------

    [Fact]
    public void Import_Ikas_SetztLinkFeld_UndVerteiltInspektionsfilm()
    {
        var (sourceDir, projectDir) = ErstelleMiniIkasFixture();
        try
        {
            var project = new Project();
            var orch = new ProjectImportOrchestrator(
                new XtfImportServiceAdapter(),
                new WinCanDbImportService());

            var result = orch.Import(sourceDir, projectDir, project);

            // Format muss IKAS sein
            Assert.Equal(KanalExportFormat.Ikas, result.Format);

            // Record "06-001" muss existieren
            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "06-001", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);

            // Link-Feld muss nach dem XTF-Parse gesetzt sein (vor Medienverteilung)
            // Nach Medienverteilung ist der Pfad relativ
            var linkNachImport = rec!.GetFieldValue("Link");
            Assert.False(string.IsNullOrWhiteSpace(linkNachImport),
                "Link-Feld muss nach IKAS-Import gesetzt sein (KEK.Datei Klasse=Untersuchung)");
            Assert.Contains("H_06-001.mpg", linkNachImport!, StringComparison.OrdinalIgnoreCase);

            // Video muss im Haltungen_Verteilt\06-001\Video\-Ordner liegen
            var videoDir = Path.Combine(
                ProjectStructure.HaltungVerteiltDir(projectDir, "06-001"), "Video");
            Assert.True(
                Directory.Exists(videoDir) && Directory.GetFiles(videoDir).Length > 0,
                $"Inspektionsfilm wurde nicht nach {videoDir} verteilt");
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(sourceDir)!, recursive: true); } catch { }
        }
    }

    // -----------------------------------------------------------------------
    // Test 2: Zweimaliger Import ist idempotent
    // -----------------------------------------------------------------------

    [Fact]
    public void Import_TwiceSameSource_NoDuplicates()
    {
        var (sourceDir, projectDir) = ErstelleMiniIkasFixture();
        try
        {
            var project = new Project();
            var orch = new ProjectImportOrchestrator(
                new XtfImportServiceAdapter(),
                new WinCanDbImportService());

            // Erster Import
            orch.Import(sourceDir, projectDir, project);
            var countAfterFirst = project.Data.Count;

            // Zweiter Import auf dieselbe Quelle
            orch.Import(sourceDir, projectDir, project);
            var countAfterSecond = project.Data.Count;

            // Record-Anzahl darf sich nicht erhoehen
            Assert.Equal(countAfterFirst, countAfterSecond);

            // Kein VsaFinding darf doppelten FotoPath haben (kein Semikolon-Mehrfachwert)
            foreach (var rec in project.Data)
            {
                if (rec.VsaFindings is null) continue;
                foreach (var finding in rec.VsaFindings)
                {
                    // Ein Finding hat genau einen FotoPath – kein Semikolon-separierter Mehrfachwert
                    if (!string.IsNullOrWhiteSpace(finding.FotoPath))
                    {
                        Assert.False(finding.FotoPath!.Contains(';'),
                            $"Haltung {rec.GetFieldValue("Haltungsname")}: Doppelter FotoPath nach zweitem Import: {finding.FotoPath}");
                    }
                }
            }
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(sourceDir)!, recursive: true); } catch { }
        }
    }
}
