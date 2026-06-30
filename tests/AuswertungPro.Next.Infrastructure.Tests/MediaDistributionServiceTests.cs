using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Tests fuer die Medien-Verteilung beim Import (Audit 2026-06-09, Testluecke "hoechstes
/// Datenverlust-Potenzial"): Kopieren statt Verschieben, Pfad-Reparatur, Namenskollisionen,
/// DryRun und fehlende Quellen duerfen nie Daten verlieren oder crashen.
/// </summary>
public sealed class MediaDistributionServiceTests
{
    [Fact]
    public void DistributeImportedMedia_AbsoluterVideoPfad_KopiertUndSetztRelativenPfad()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var quelle = temp.CreateSubdir("quelle");
        var videoQuelle = Path.Combine(quelle, "inspektion.mpg");
        File.WriteAllText(videoQuelle, "videodaten");

        var project = NewProject("06.123-456", "Link", videoQuelle);

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project);

        Assert.Equal(1, result.FilesCopied);
        Assert.Equal(0, result.Errors);

        // Quelle bleibt erhalten: Kopieren, niemals Verschieben.
        Assert.True(File.Exists(videoQuelle));

        // Ziel liegt im Video-Unterordner der Haltung (Haltungen_Verteilt), Feld ist relativ (Forward-Slashes).
        var neuerLink = project.Data[0].GetFieldValue("Link");
        Assert.False(Path.IsPathRooted(neuerLink));
        Assert.Equal("Haltungen_Verteilt/06.123-456/Video/inspektion.mpg", neuerLink);
        Assert.True(File.Exists(Path.Combine(projectFolder, "Haltungen_Verteilt", "06.123-456", "Video", "inspektion.mpg")));
        Assert.True(project.Dirty);
    }

    [Fact]
    public void DistributeImportedMedia_VerteiltSchachtDokument_NachSchaechteVerteilt()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var quelle = temp.CreateSubdir("quelle");
        var pdfQuelle = Path.Combine(quelle, "schacht.pdf");
        File.WriteAllText(pdfQuelle, "pdfdaten");

        var project = new Project();
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "S-100");
        schacht.SetFieldValue("Link", pdfQuelle);
        project.SchaechteData.Add(schacht);

        var result = new MediaDistributionService().DistributeImportedMedia(projectFolder, project);

        Assert.Equal(0, result.Errors);
        Assert.True(File.Exists(pdfQuelle)); // Quelle bleibt erhalten (Kopieren, nicht Verschieben)

        var rel = schacht.GetFieldValue("Link").Replace('\\', '/');
        Assert.False(Path.IsPathRooted(rel));
        Assert.StartsWith("Schächte_Verteilt/S-100/", rel);
        Assert.True(File.Exists(Path.Combine(projectFolder, rel)));
    }

    [Fact]
    public void DistributeImportedMedia_RelativerPfadVorhanden_BleibtUnveraendert()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var zielDir = Path.Combine(projectFolder, "Haltungen_Verteilt", "06.123-456", "Video");
        Directory.CreateDirectory(zielDir);
        File.WriteAllText(Path.Combine(zielDir, "inspektion.mpg"), "videodaten");

        var relativ = "Haltungen_Verteilt/06.123-456/Video/inspektion.mpg";
        var project = NewProject("06.123-456", "Link", relativ);

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project);

        Assert.Equal(0, result.FilesCopied);
        Assert.Equal(0, result.Errors);
        Assert.Equal(relativ, project.Data[0].GetFieldValue("Link"));
    }

    [Fact]
    public void DistributeImportedMedia_RelativerTraversalPfad_WirdNichtAlsVorhandenAkzeptiert()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var outside = temp.CreateSubdir("outside");
        File.WriteAllText(Path.Combine(outside, "secret.mpg"), "video");

        var traversal = Path.Combine("..", "outside", "secret.mpg");
        var project = NewProject("06.123-456", "Link", traversal);

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project);

        Assert.Equal(0, result.FilesCopied);
        Assert.Equal(traversal, project.Data[0].GetFieldValue("Link"));
        Assert.Contains(result.Messages, m => m.Contains("Unsicherer relativer Pfad", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DistributeImportedMedia_RelativerPfadFehlt_WirdUeberDateinamenRepariert()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");

        // Datei liegt real woanders im Haltungen_Verteilt-Baum als das Feld behauptet.
        var echtesDir = Path.Combine(projectFolder, "Haltungen_Verteilt", "06.123-456", "Video");
        Directory.CreateDirectory(echtesDir);
        File.WriteAllText(Path.Combine(echtesDir, "inspektion.mpg"), "videodaten");

        var project = NewProject("06.123-456", "Link", "Haltungen_Verteilt/FALSCHER-ORDNER/Video/inspektion.mpg");

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project);

        Assert.Equal(1, result.FilesCopied); // Reparatur zaehlt als Aenderung
        Assert.Equal("Haltungen_Verteilt/06.123-456/Video/inspektion.mpg", project.Data[0].GetFieldValue("Link"));
        Assert.Contains(result.Messages, m => m.Contains("Repariert", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DistributeImportedMedia_RelativerPfadFehlt_MehrereGlobaleTreffer_RepariertNicht()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");

        var fremdA = Path.Combine(projectFolder, "Haltungen_Verteilt", "11-22", "Video");
        var fremdB = Path.Combine(projectFolder, "Haltungen_Verteilt", "33-44", "Video");
        Directory.CreateDirectory(fremdA);
        Directory.CreateDirectory(fremdB);
        File.WriteAllText(Path.Combine(fremdA, "inspektion.mpg"), "a");
        File.WriteAllText(Path.Combine(fremdB, "inspektion.mpg"), "b");

        var original = "Haltungen_Verteilt/06.123-456/Video/inspektion.mpg";
        var project = NewProject("06.123-456", "Link", original);

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project);

        Assert.Equal(0, result.FilesCopied);
        Assert.Equal(original, project.Data[0].GetFieldValue("Link"));
        Assert.Contains(result.Messages, m => m.Contains("Mehrere globale Treffer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DistributeImportedMedia_DryRun_RelativeReparatur_AendertFeldNicht()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var echtesDir = Path.Combine(projectFolder, "Haltungen_Verteilt", "06.123-456", "Video");
        Directory.CreateDirectory(echtesDir);
        File.WriteAllText(Path.Combine(echtesDir, "inspektion.mpg"), "videodaten");

        var original = "Haltungen_Verteilt/FALSCHER-ORDNER/Video/inspektion.mpg";
        var project = NewProject("06.123-456", "Link", original);

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project, dryRun: true);

        Assert.Equal(1, result.FilesCopied);
        Assert.Equal(original, project.Data[0].GetFieldValue("Link"));
        Assert.False(project.Dirty);
        Assert.Contains(result.Messages, m => m.Contains("Wuerde reparieren", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DistributeImportedMedia_QuelleFehlt_MeldungOhneAbsturz()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var fehlend = Path.Combine(temp.Path, "gibt-es-nicht", "video.mpg");

        var project = NewProject("06.123-456", "Link", fehlend);

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project);

        Assert.Equal(0, result.FilesCopied);
        Assert.Equal(0, result.Errors); // fehlende Quelle ist Meldung, kein Fehler
        Assert.Contains(result.Messages, m => m.Contains("nicht gefunden", StringComparison.OrdinalIgnoreCase));
        // Feld bleibt unveraendert, damit der Anwender den Original-Verweis noch sieht.
        Assert.Equal(fehlend, project.Data[0].GetFieldValue("Link"));
    }

    [Fact]
    public void DistributeImportedMedia_Namenskollision_GleicheGroesse_Wiederverwendet()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var quelle = temp.CreateSubdir("quelle");
        var videoQuelle = Path.Combine(quelle, "inspektion.mpg");
        File.WriteAllText(videoQuelle, "gleicher-inhalt");

        // Ziel existiert bereits mit identischer Groesse.
        var zielDir = Path.Combine(projectFolder, "Haltungen_Verteilt", "06.123-456", "Video");
        Directory.CreateDirectory(zielDir);
        File.WriteAllText(Path.Combine(zielDir, "inspektion.mpg"), "gleicher-inhalt");

        var project = NewProject("06.123-456", "Link", videoQuelle);

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project);

        Assert.Equal(1, result.FilesCopied);
        Assert.Equal(0, result.Errors);
        // Keine zweite Datei: bestehende wird wiederverwendet.
        Assert.Single(Directory.GetFiles(zielDir));
        Assert.Equal("Haltungen_Verteilt/06.123-456/Video/inspektion.mpg", project.Data[0].GetFieldValue("Link"));
    }

    [Fact]
    public void DistributeImportedMedia_Namenskollision_AndereGroesse_TimestampSuffix()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var quelle = temp.CreateSubdir("quelle");
        var videoQuelle = Path.Combine(quelle, "inspektion.mpg");
        File.WriteAllText(videoQuelle, "neuer-deutlich-laengerer-inhalt");

        var zielDir = Path.Combine(projectFolder, "Haltungen_Verteilt", "06.123-456", "Video");
        Directory.CreateDirectory(zielDir);
        var bestehend = Path.Combine(zielDir, "inspektion.mpg");
        File.WriteAllText(bestehend, "alt");

        var project = NewProject("06.123-456", "Link", videoQuelle);

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project);

        Assert.Equal(1, result.FilesCopied);
        Assert.Equal(0, result.Errors);
        // Bestehende Datei darf NICHT ueberschrieben werden; neue bekommt Suffix.
        Assert.Equal("alt", File.ReadAllText(bestehend));
        var dateien = Directory.GetFiles(zielDir);
        Assert.Equal(2, dateien.Length);
        var neueDatei = dateien.Single(f => !string.Equals(f, bestehend, StringComparison.OrdinalIgnoreCase));
        Assert.StartsWith("inspektion_", Path.GetFileName(neueDatei));
        Assert.Equal("neuer-deutlich-laengerer-inhalt", File.ReadAllText(neueDatei));
    }

    [Fact]
    public void DistributeImportedMedia_DryRun_KopiertNichtsUndSetztDirtyNicht()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var quelle = temp.CreateSubdir("quelle");
        var videoQuelle = Path.Combine(quelle, "inspektion.mpg");
        File.WriteAllText(videoQuelle, "videodaten");

        var project = NewProject("06.123-456", "Link", videoQuelle);

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project, dryRun: true);

        // DryRun zaehlt was kopiert wuerde, schreibt aber nichts.
        Assert.Equal(1, result.FilesCopied);
        Assert.False(Directory.Exists(Path.Combine(projectFolder, "Haltungen_Verteilt")));
        Assert.Equal(videoQuelle, project.Data[0].GetFieldValue("Link"));
        Assert.False(project.Dirty);
    }

    [Fact]
    public void DistributeImportedMedia_HaltungsnameMitUngueltigenZeichen_OrdnerWirdSaniert()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var quelle = temp.CreateSubdir("quelle");
        var videoQuelle = Path.Combine(quelle, "inspektion.mpg");
        File.WriteAllText(videoQuelle, "videodaten");

        // Haltungsname mit unter Windows verbotenen Zeichen (kommt aus Fremd-PDFs vor).
        var project = NewProject("06.1<2>3:45-67", "Link", videoQuelle);

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project);

        Assert.Equal(1, result.FilesCopied);
        Assert.Equal(0, result.Errors);

        var haltungen = Directory.GetDirectories(Path.Combine(projectFolder, "Haltungen_Verteilt"));
        var ordnerName = Path.GetFileName(Assert.Single(haltungen));
        Assert.Equal(MediaDistributionService.SanitizePathSegment("06.1<2>3:45-67"), ordnerName);
        Assert.DoesNotContain(ordnerName, c => Path.GetInvalidFileNameChars().Contains(c));
    }

    [Fact]
    public void DistributeImportedMedia_PdfAllListe_KopiertAbsoluteUndLaesstRelative()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var quelle = temp.CreateSubdir("quelle");

        // Ein relativer Eintrag der existiert + ein absoluter der kopiert werden muss.
        var pdfDir = Path.Combine(projectFolder, "Haltungen_Verteilt", "06.123-456", "PDF");
        Directory.CreateDirectory(pdfDir);
        File.WriteAllText(Path.Combine(pdfDir, "vorhanden.pdf"), "pdf1");
        var absolutePdf = Path.Combine(quelle, "neu.pdf");
        File.WriteAllText(absolutePdf, "pdf2");

        var project = NewProject("06.123-456",
            "PDF_All", $"Haltungen_Verteilt/06.123-456/PDF/vorhanden.pdf;{absolutePdf}");

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project);

        Assert.Equal(1, result.FilesCopied);
        Assert.Equal(0, result.Errors);
        Assert.Equal(
            "Haltungen_Verteilt/06.123-456/PDF/vorhanden.pdf;Haltungen_Verteilt/06.123-456/PDF/neu.pdf",
            project.Data[0].GetFieldValue("PDF_All"));
        Assert.True(File.Exists(Path.Combine(pdfDir, "neu.pdf")));
        Assert.True(File.Exists(absolutePdf)); // Quelle bleibt erhalten
    }

    [Fact]
    public void DistributeImportedMedia_OhneHaltungsname_WirdUebersprungen()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");

        var project = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue("Link", Path.Combine(temp.Path, "video.mpg"), FieldSource.Manual, userEdited: false);
        project.Data.Add(record);

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project);

        Assert.Equal(1, result.FilesSkipped);
        Assert.Equal(0, result.FilesCopied);
        Assert.Equal(0, result.Errors);
    }

    [Theory]
    [InlineData(".mpg", "Video")]
    [InlineData(".mp4", "Video")]
    [InlineData(".jpg", "Fotos")]
    [InlineData(".png", "Fotos")]
    [InlineData(".pdf", "PDF")]
    [InlineData(".txt", "PDF")] // Unbekannte Endungen landen im PDF-Sammelordner
    public void GetSubfolder_Erweiterung_RichtigerUnterordner(string ext, string erwartet)
    {
        Assert.Equal(erwartet, MediaDistributionService.GetSubfolder(ext));
    }

    [Fact]
    public void CopyProtocolFotos_PutsPhotosUnderFotosHaltungen()
    {
        // Beleg: Protokoll-Foto landet unter Fotos\Haltungen\<Haltung>\ und
        // entry.FotoPaths[0] ist der relative Pfad dorthin.
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var quelle = temp.CreateSubdir("quelle");
        var fotoPfad = Path.Combine(quelle, "bild.jpg");
        File.WriteAllText(fotoPfad, "bilddaten");

        var project = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "06-001", AuswertungPro.Next.Domain.Models.FieldSource.Manual, userEdited: false);
        var revision = new AuswertungPro.Next.Domain.Protocol.ProtocolRevision();
        var entry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry { Code = "BAB" };
        entry.FotoPaths.Add(fotoPfad);
        revision.Entries.Add(entry);
        var protokoll = new AuswertungPro.Next.Domain.Protocol.ProtocolDocument();
        protokoll.Current = revision;
        record.Protocol = protokoll;
        project.Data.Add(record);
        project.Dirty = false;

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project);

        Assert.Equal(1, result.FilesCopied);
        Assert.Equal(0, result.Errors);

        // Foto liegt unter Fotos\Haltungen\06-001\
        var erwartetesZielDir = Path.Combine(projectFolder, "Fotos", "Haltungen", "06-001");
        Assert.True(File.Exists(Path.Combine(erwartetesZielDir, "bild.jpg")),
            $"Foto muss unter Fotos\\Haltungen\\06-001\\ liegen.");

        // Relativer Pfad korrekt gesetzt
        var relPath = entry.FotoPaths[0];
        Assert.False(Path.IsPathRooted(relPath), "FotoPath muss relativ sein.");
        Assert.Equal("Fotos/Haltungen/06-001/bild.jpg", relPath);
        Assert.True(project.Dirty);
    }

    private static Project NewProject(string haltungsname, string fieldName, string fieldValue)
    {
        var project = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", haltungsname, FieldSource.Manual, userEdited: false);
        record.SetFieldValue(fieldName, fieldValue, FieldSource.Manual, userEdited: false);
        project.Data.Add(record);
        project.Dirty = false; // Add setzt Dirty nicht, aber sicherheitshalber definierter Ausgangszustand
        return project;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "media_dist_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string CreateSubdir(string name)
        {
            var dir = System.IO.Path.Combine(Path, name);
            Directory.CreateDirectory(dir);
            return dir;
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Cleanup-Fehler ignorieren
            }
        }
    }
}
