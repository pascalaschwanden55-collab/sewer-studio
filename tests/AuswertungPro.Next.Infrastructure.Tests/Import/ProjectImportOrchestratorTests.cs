using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Ibak;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

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

        // Foto = echtes Mini-PNG (das generierte Protokoll bettet es ein -> muss dekodierbar sein).
        File.WriteAllBytes(Path.Combine(sourceDir, "Foto", "H_06-001_002.jpg"),
            System.Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M8AAAMBAQDJ/pLvAAAAAElFTkSuQmCC"));
        // Video-Inhalt beliebig (wird nur kopiert, nicht dekodiert).
        File.WriteAllText(Path.Combine(sourceDir, "Film", "H_06-001.mpg"), "dummy-video");
        File.WriteAllText(Path.Combine(sourceDir, "AWU_Mini_Plan.pdf"),
            "DW\nLeitungsende Veschlossen\nDachwasser angeschlossen");

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
            Assert.False(Path.IsPathRooted(baa.FotoPath!), $"BAA-FotoPath muss relativ sein: {baa.FotoPath}");
            Assert.Equal("Fotos/Haltungen/06-001/H_06-001_002.jpg", baa.FotoPath!.Replace('\\', '/'));
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

            // Plan-PDFs werden aus dem archivierten PDF-Bestand in den Projektordner Pläne uebernommen.
            var planPath = Path.Combine(projectDir, "Pläne", "AWU_Mini_Plan.pdf");
            Assert.True(File.Exists(planPath), $"Plan-PDF nicht importiert: {planPath}");

            // Video muss FLACH + datumsbenannt im Haltungsordner liegen (JJJJMMTT_06-001.mpg),
            // NICHT in einem Video\-Unterordner (wie "Haltung Verteilen").
            var haltungDir = ProjectStructure.HaltungVerteiltDir(projectDir, "06-001");
            var videos = Directory.Exists(haltungDir)
                ? Directory.GetFiles(haltungDir, "*.mpg") : System.Array.Empty<string>();
            Assert.True(videos.Length > 0, $"Kein flach verteiltes Video in {haltungDir}");
            Assert.EndsWith("_06-001.mpg", videos[0]);

            // BEIM IMPORT darf KEIN eigenes _E-Protokoll erzeugt werden — das passiert erst am Ende der
            // Bearbeitung via „Protokoll neu generieren" (ProtocolRegenerationService). Die Fixture hat
            // zudem kein Original-Protokoll-PDF, also wird auch kein Original verteilt.
            var eigeneProtokolle = Directory.Exists(haltungDir)
                ? Directory.GetFiles(haltungDir, "*_E.pdf") : System.Array.Empty<string>();
            Assert.True(eigeneProtokolle.Length == 0,
                $"Beim Import darf kein _E-Protokoll erzeugt werden, gefunden in {haltungDir}");
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

            // Nach der Verteilung ist der Link RELATIV auf das flach+datumsbenannte Video im Haltungsordner
            // (JJJJMMTT_06-001.mpg) und die Datei existiert im Projekt.
            var linkNachImport = rec!.GetFieldValue("Link");
            Assert.False(string.IsNullOrWhiteSpace(linkNachImport),
                "Link-Feld muss nach IKAS-Import gesetzt + verteilt sein");
            Assert.False(Path.IsPathRooted(linkNachImport), $"Link sollte relativ sein: {linkNachImport}");
            Assert.Contains("Haltungen_Verteilt", linkNachImport!.Replace('\\', '/'));
            Assert.EndsWith("_06-001.mpg", linkNachImport);
            Assert.True(File.Exists(Path.Combine(projectDir, linkNachImport)),
                $"Verteiltes Video sollte existieren: {linkNachImport}");
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

    // -----------------------------------------------------------------------
    // Test 3: „Protokoll neu generieren" erzeugt das eigene _E-Protokoll (PDF_Eigen),
    //         ohne das Original (PDF_Path) anzufassen.
    // -----------------------------------------------------------------------

    [Fact]
    public void ProtokollNeuGenerieren_ErzeugtEigenesProtokoll_UndLaesstOriginalUnberuehrt()
    {
        var (sourceDir, projectDir) = ErstelleMiniIkasFixture();
        try
        {
            var project = new Project();
            var orch = new ProjectImportOrchestrator(
                new XtfImportServiceAdapter(),
                new WinCanDbImportService());

            // Import (verteilt Fotos, kein _E) — Ausgangslage fuer die Regenerierung
            orch.Import(sourceDir, projectDir, project);

            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "06-001", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);

            // Vor der Regenerierung: kein eigenes Protokoll verlinkt
            Assert.True(string.IsNullOrWhiteSpace(rec!.GetFieldValue("PDF_Eigen")));
            var pdfPathVorher = rec.GetFieldValue("PDF_Path");

            // Am Ende der Bearbeitung: eigenes Protokoll erzeugen
            var result = ProtocolRegenerationService.RegenerateAll(project, projectDir);
            Assert.True(result.Generated > 0, "Es muss mindestens ein eigenes Protokoll erzeugt werden");

            // _E-Protokoll liegt flach im Haltungsordner
            var haltungDir = ProjectStructure.HaltungVerteiltDir(projectDir, "06-001");
            var eigene = Directory.Exists(haltungDir)
                ? Directory.GetFiles(haltungDir, "*_E.pdf") : System.Array.Empty<string>();
            Assert.True(eigene.Length > 0, $"Kein generiertes _E-Protokoll in {haltungDir}");

            // PDF_Eigen ist relativ gesetzt und die Datei existiert
            var pdfEigen = rec.GetFieldValue("PDF_Eigen");
            Assert.False(string.IsNullOrWhiteSpace(pdfEigen), "PDF_Eigen muss gesetzt sein");
            Assert.False(Path.IsPathRooted(pdfEigen), $"PDF_Eigen soll relativ sein: {pdfEigen}");
            Assert.EndsWith("_E.pdf", pdfEigen);
            Assert.True(File.Exists(Path.Combine(projectDir, pdfEigen!)),
                $"Verlinktes eigenes Protokoll sollte existieren: {pdfEigen}");

            // Das Original (PDF_Path) wurde NICHT angefasst
            Assert.Equal(pdfPathVorher, rec.GetFieldValue("PDF_Path"));
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(sourceDir)!, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Import_IkasOhneXtf_LegtHaltungenAusVerteiltemOriginalPdfAn()
    {
        var root = Path.Combine(Path.GetTempPath(), $"orch-ikas-pdf-only-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(root, "source");
        var projectDir = Path.Combine(root, "projekt");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(sourceDir, "Film"));
        File.WriteAllText(Path.Combine(sourceDir, "Arizona.fdb"), "fdb");
        File.WriteAllText(Path.Combine(sourceDir, "Film", "Daten.txt"), "daten");
        WritePdf(
            Path.Combine(sourceDir, "Gesamtprotokoll.pdf"),
            "Haltungsinspektion - 22.06.2026 - 10081-8993",
            "Film H_10081-8993.mpg",
            "Leitungsbericht",
            "0.00 BCD Rohranfang");

        try
        {
            var project = new Project();
            var orch = new ProjectImportOrchestrator(
                new XtfImportServiceAdapter(),
                new WinCanDbImportService());

            var result = orch.Import(sourceDir, projectDir, project);

            Assert.Equal(KanalExportFormat.Ibak, result.Format);
            Assert.Equal(1, result.Found);
            Assert.Equal(1, result.Created);
            var rec = Assert.Single(project.Data);
            Assert.Equal("10081-8993", rec.GetFieldValue("Haltungsname"));
            var pdfPath = rec.GetFieldValue("PDF_Path");
            Assert.False(string.IsNullOrWhiteSpace(pdfPath));
            Assert.False(Path.IsPathRooted(pdfPath), $"PDF_Path muss relativ sein: {pdfPath}");
            Assert.True(File.Exists(Path.Combine(projectDir, pdfPath!)));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Import_IkasOhneXtf_NutztIbakDatenTxtAlsHerstellerQuelle()
    {
        var root = Path.Combine(Path.GetTempPath(), $"orch-ikas-datentxt-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(root, "source");
        var projectDir = Path.Combine(root, "projekt");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(sourceDir, "Film"));
        File.WriteAllText(Path.Combine(sourceDir, "Arizona.fdb"), "fdb");
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        File.WriteAllText(
            Path.Combine(sourceDir, "Film", "Daten.txt"),
            "SS 10081-SS 8993\n" +
            "\t00:00:05    0.00 m  BCD     Rohranfang@!$ibak$!SS 10081-SS 8993$H\n" +
            "\t00:01:47    8.20 m  BCE     Rohrende@!$ibak$!SS 10081-SS 8993$H\n",
            System.Text.Encoding.GetEncoding(1252));

        try
        {
            var project = new Project();
            var orch = new ProjectImportOrchestrator(
                new XtfImportServiceAdapter(),
                new WinCanDbImportService(),
                kins: null,
                ibak: new IbakExportImportService());

            var result = orch.Import(sourceDir, projectDir, project);

            Assert.Equal(KanalExportFormat.Ibak, result.Format);
            Assert.Equal(1, result.Found);
            Assert.Equal(1, result.Created);
            var rec = Assert.Single(project.Data);
            Assert.Equal("10081-8993", rec.GetFieldValue("Haltungsname"));
            Assert.Equal("8.2", rec.GetFieldValue("Haltungslaenge_m"));
            Assert.Contains(result.Messages, m => m.Contains("IBAK", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Import_IbakVerteiltHUnterstrichSchachtSchachtFotosZentral()
    {
        var root = Path.Combine(Path.GetTempPath(), $"orch-ibak-foto-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(root, "source");
        var projectDir = Path.Combine(root, "projekt");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(sourceDir, "Film"));
        Directory.CreateDirectory(Path.Combine(sourceDir, "Foto"));
        File.WriteAllText(Path.Combine(sourceDir, "Arizona.fdb"), "fdb");
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        File.WriteAllText(
            Path.Combine(sourceDir, "Film", "Daten.txt"),
            "SS 10081-SS 8993\n" +
            "\t00:00:05    0.00 m  BCD     Rohranfang Foto 1@!$ibak$!SS 10081-SS 8993$H\n",
            System.Text.Encoding.GetEncoding(1252));
        var sourceFoto = Path.Combine(sourceDir, "Foto", "H_SS 10081-SS 8993_001.jpg");
        File.WriteAllText(sourceFoto, "bild");

        try
        {
            var project = new Project();
            var orch = new ProjectImportOrchestrator(
                new XtfImportServiceAdapter(),
                new WinCanDbImportService(),
                kins: null,
                ibak: new IbakExportImportService());

            var result = orch.Import(sourceDir, projectDir, project);

            Assert.Equal(KanalExportFormat.Ibak, result.Format);
            var record = Assert.Single(project.Data);
            var entry = Assert.Single(record.Protocol!.Current.Entries);
            var relFoto = Assert.Single(entry.FotoPaths);
            Assert.False(Path.IsPathRooted(relFoto), $"FotoPath muss relativ sein: {relFoto}");
            Assert.Equal("Fotos/Haltungen/10081-8993/H_SS 10081-SS 8993_001.jpg", relFoto.Replace('\\', '/'));
            Assert.True(File.Exists(Path.Combine(projectDir, relFoto)), $"Foto nicht verteilt: {relFoto}");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Import_IbakVerteiltHUnterstrichFotosAuchOhneFotoMarker()
    {
        var root = Path.Combine(Path.GetTempPath(), $"orch-ibak-foto-nomarker-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(root, "source");
        var projectDir = Path.Combine(root, "projekt");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(sourceDir, "Film"));
        Directory.CreateDirectory(Path.Combine(sourceDir, "Foto"));
        File.WriteAllText(Path.Combine(sourceDir, "Arizona.fdb"), "fdb");
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        File.WriteAllText(
            Path.Combine(sourceDir, "Film", "Daten.txt"),
            "SS 10081-SS 8993\n" +
            "\t00:00:05    0.00 m  BCD     Rohranfang@!$ibak$!SS 10081-SS 8993$H\n" +
            "\t00:00:55    2.90 m  BCC     Pos: 6; Bogen nach unten, Winkel = 10°@!$ibak$!SS 10081-SS 8993$H\n",
            System.Text.Encoding.GetEncoding(1252));
        File.WriteAllText(Path.Combine(sourceDir, "Foto", "H_SS 10081-SS 8993_001.jpg"), "bild1");
        File.WriteAllText(Path.Combine(sourceDir, "Foto", "H_SS 10081-SS 8993_002.jpg"), "bild2");

        try
        {
            var project = new Project();
            var orch = new ProjectImportOrchestrator(
                new XtfImportServiceAdapter(),
                new WinCanDbImportService(),
                kins: null,
                ibak: new IbakExportImportService());

            var result = orch.Import(sourceDir, projectDir, project);

            Assert.Equal(KanalExportFormat.Ibak, result.Format);
            Assert.DoesNotContain(result.Messages, m => m.Contains("Nicht zugeordnete Fotos", StringComparison.OrdinalIgnoreCase));
            var record = Assert.Single(project.Data);
            var fotos = record.Protocol!.Current.Entries
                .SelectMany(e => e.FotoPaths)
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            Assert.Equal(
                new[]
                {
                    "Fotos/Haltungen/10081-8993/H_SS 10081-SS 8993_001.jpg",
                    "Fotos/Haltungen/10081-8993/H_SS 10081-SS 8993_002.jpg"
                },
                fotos);
            foreach (var relFoto in fotos)
                Assert.True(File.Exists(Path.Combine(projectDir, relFoto)), $"Foto nicht verteilt: {relFoto}");
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Import_IbakFotoFallback_VerteiltFotosAufEchteBefundeStattRohranfang()
    {
        var root = Path.Combine(Path.GetTempPath(), $"orch-ibak-foto-real-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(root, "source");
        var projectDir = Path.Combine(root, "projekt");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.Combine(sourceDir, "Film"));
        Directory.CreateDirectory(Path.Combine(sourceDir, "Foto"));
        File.WriteAllText(Path.Combine(sourceDir, "Arizona.fdb"), "fdb");
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        File.WriteAllText(
            Path.Combine(sourceDir, "Film", "Daten.txt"),
            "SS 10081-SS 8993\n" +
            "\t00:00:05    0.00 m  BCD     Rohranfang@!$ibak$!SS 10081-SS 8993$H\n" +
            "\t00:00:07    0.00 m  BCB     Anfang Inliner@!$ibak$!SS 10081-SS 8993$H\n" +
            "\t00:00:55    2.90 m  BCC     Bogen nach unten@!$ibak$!SS 10081-SS 8993$H\n" +
            "\t00:01:44    8.20 m  BCB     Ende Inliner@!$ibak$!SS 10081-SS 8993$H\n" +
            "\t00:01:47    8.20 m  BCE     Rohrende@!$ibak$!SS 10081-SS 8993$H\n",
            System.Text.Encoding.GetEncoding(1252));
        File.WriteAllText(Path.Combine(sourceDir, "Foto", "H_SS 10081-SS 8993_001.jpg"), "bild1");
        File.WriteAllText(Path.Combine(sourceDir, "Foto", "H_SS 10081-SS 8993_002.jpg"), "bild2");
        File.WriteAllText(Path.Combine(sourceDir, "Foto", "H_SS 10081-SS 8993_003.jpg"), "bild3");

        try
        {
            var project = new Project();
            var orch = new ProjectImportOrchestrator(
                new XtfImportServiceAdapter(),
                new WinCanDbImportService(),
                kins: null,
                ibak: new IbakExportImportService());

            var result = orch.Import(sourceDir, projectDir, project);

            Assert.Equal(KanalExportFormat.Ibak, result.Format);
            Assert.DoesNotContain(result.Messages, m => m.Contains("Nicht zugeordnete Fotos", StringComparison.OrdinalIgnoreCase));
            var entries = Assert.Single(project.Data).Protocol!.Current.Entries;

            Assert.Empty(entries.Single(e => e.Code == "BCD").FotoPaths);
            Assert.Empty(entries.Single(e => e.Code == "BCE").FotoPaths);

            var fotos = entries
                .Where(e => e.Code == "BCB" || e.Code == "BCC")
                .SelectMany(e => e.FotoPaths)
                .Select(p => p.Replace('\\', '/'))
                .ToArray();

            Assert.Equal(
                new[]
                {
                    "Fotos/Haltungen/10081-8993/H_SS 10081-SS 8993_001.jpg",
                    "Fotos/Haltungen/10081-8993/H_SS 10081-SS 8993_002.jpg",
                    "Fotos/Haltungen/10081-8993/H_SS 10081-SS 8993_003.jpg"
                },
                fotos);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Import_MeldetWarnungWennDatenquelleAberNullHaltungen()
    {
        var root = Path.Combine(Path.GetTempPath(), $"orch-zero-warning-{Guid.NewGuid():N}");
        var sourceDir = Path.Combine(root, "source");
        var projectDir = Path.Combine(root, "projekt");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(sourceDir, "leer.xtf"), """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION />
</TRANSFER>
""");

        try
        {
            var project = new Project();
            var orch = new ProjectImportOrchestrator(
                new XtfImportServiceAdapter(),
                new WinCanDbImportService());

            var result = orch.Import(sourceDir, projectDir, project);

            Assert.Equal(KanalExportFormat.Ikas, result.Format);
            Assert.Equal(0, result.Found);
            Assert.Contains(result.Messages, m => m.StartsWith("Erkanntes Format:", StringComparison.Ordinal));
            Assert.Contains(result.Messages, m => m.StartsWith("Hauptquelle:", StringComparison.Ordinal));
            Assert.Contains(result.Messages, m => m.StartsWith("WARNUNG: 0 Haltungen", StringComparison.Ordinal));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static void WritePdf(string path, params string[] lines)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        var y = 780m;
        foreach (var line in lines)
        {
            page.AddText(line, 12, new PdfPoint(40, y), font);
            y -= 18;
        }

        File.WriteAllBytes(path, builder.Build());
    }
}
