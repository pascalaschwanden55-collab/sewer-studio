using System;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Security-Tests zu S2-1..S2-3 (XTF-Medienpfad-Aufloesung und Kopierpfade):
/// - Nur bekannte Medientypen werden aufgeloest/kopiert (keine Exfiltration beliebiger Dateien).
/// - Relativpfade duerfen nicht aus dem XTF-Verzeichnis ausbrechen (Traversal).
/// - UNC-Pfade werden verworfen (NTLM-Hash-Leak via SMB).
/// Gleichzeitig muessen legitime Workflows (Medien relativ zur XTF, absolute Medienpfade
/// auf anderen Laufwerken) unveraendert funktionieren.
/// </summary>
public sealed class XtfMediaPathSecurityTests
{
    // ---------------- Resolver: Extension-Allowlist (S2-1) ----------------

    [Fact]
    public void ResolvePhoto_RootedNichtMedienDatei_WirdVerworfen()
    {
        using var temp = new TempDir();
        var xlsx = Path.Combine(temp.Path, "geheim.xlsx");
        File.WriteAllText(xlsx, "dokument");

        var result = new VsaMediaPathFileResolver().ResolvePhoto("export.xtf", null, xlsx);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ResolveVideo_RootedExe_WirdVerworfen()
    {
        using var temp = new TempDir();
        var exe = Path.Combine(temp.Path, "tool.exe");
        File.WriteAllText(exe, "MZ");

        var result = new VsaMediaPathFileResolver().ResolveVideo("export.xtf", null, exe);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ResolveVideo_RootedMp4AufAnderemLaufwerk_BleibtErlaubt()
    {
        // Fachliche Randbedingung: Videos liegen oft absolut auf anderen Laufwerken
        // (z. B. externe Platte mit WinCan-Exporten) und muessen weiter aufloesen.
        using var temp = new TempDir();
        var mp4 = Path.Combine(temp.Path, "H_06-001.mp4");
        File.WriteAllText(mp4, "video");

        var result = new VsaMediaPathFileResolver().ResolveVideo("export.xtf", null, mp4);

        Assert.Equal(mp4, result);
    }

    [Fact]
    public void ResolvePhoto_RelativeNichtMedienDatei_WirdVerworfen()
    {
        using var temp = new TempDir();
        var documents = temp.CreateSubdir("Dokumente");
        File.WriteAllText(Path.Combine(documents, "protokoll.xlsx"), "dokument");

        var result = new VsaMediaPathFileResolver().ResolvePhoto(
            Path.Combine(documents, "export.xtf"), null, "protokoll.xlsx");

        Assert.Equal(string.Empty, result);
    }

    // ---------------- Resolver: Traversal-Containment (S2-2) ----------------

    [Fact]
    public void ResolvePhoto_RelativpfadMitVerzeichnisbruch_WirdVerworfen()
    {
        using var temp = new TempDir();
        var basis = temp.CreateSubdir("export");
        var documents = Path.Combine(basis, "Dokumente");
        Directory.CreateDirectory(documents);
        // "Ausbruchs"-Datei ausserhalb des XTF-Verzeichnisses.
        var outside = temp.CreateSubdir("outside");
        var ausbruch = Path.Combine(outside, "foto.jpg");
        File.WriteAllText(ausbruch, "foto");

        var result = new VsaMediaPathFileResolver().ResolvePhoto(
            Path.Combine(documents, "export.xtf"),
            Path.Combine("..", "..", "outside"),
            "foto.jpg");

        // Der Traversal-Kandidat wird verworfen; es bleibt der normale Fallback-Kandidat
        // im XTF-Verzeichnis (Datei existiert dort nicht -> wie "nicht gefunden").
        Assert.NotEqual(Path.GetFullPath(ausbruch), Path.GetFullPath(result));
        Assert.Equal(
            Path.GetFullPath(Path.Combine(documents, "foto.jpg")),
            Path.GetFullPath(result));
    }

    [Fact]
    public void ResolvePhoto_DateinameMitVerzeichnisbruch_WirdVerworfen()
    {
        using var temp = new TempDir();
        var documents = temp.CreateSubdir("export/Dokumente");
        var outside = temp.CreateSubdir("outside");
        var ausbruch = Path.Combine(outside, "foto.jpg");
        File.WriteAllText(ausbruch, "foto");

        var result = new VsaMediaPathFileResolver().ResolvePhoto(
            Path.Combine(documents, "export.xtf"),
            relativeFolder: null,
            fileName: Path.Combine("Medien", "..", "..", "..", "outside", "foto.jpg"));

        Assert.Equal(string.Empty, result);
        Assert.True(File.Exists(ausbruch));
    }

    [Fact]
    public void ResolvePhoto_RelativpfadInnerhalbDesXtfVerzeichnisses_BleibtErlaubt()
    {
        using var temp = new TempDir();
        var documents = temp.CreateSubdir("Dokumente");
        var medien = Path.Combine(documents, "Medien");
        Directory.CreateDirectory(medien);
        var foto = Path.Combine(medien, "foto.jpg");
        File.WriteAllText(foto, "foto");

        var result = new VsaMediaPathFileResolver().ResolvePhoto(
            Path.Combine(documents, "export.xtf"), "Medien", "foto.jpg");

        Assert.Equal(foto, result, ignoreCase: true);
    }

    [JunctionFact]
    public void ResolveVideo_VerknuepfterKandidatenordner_WirdNichtBetreten()
    {
        using var temp = new TempDir();
        var documents = temp.CreateSubdir("export/Dokumente");
        var external = temp.CreateSubdir("fremd");
        var externalVideo = Path.Combine(external, "film.mp4");
        File.WriteAllText(externalVideo, "fremdes-video");
        var videoLink = Path.Combine(documents, "Video");
        JunctionTestSupport.CreateDirectoryLink(videoLink, external);

        try
        {
            var result = new VsaMediaPathFileResolver().ResolveVideo(
                Path.Combine(documents, "export.xtf"),
                relativeFolder: null,
                fileName: "film.mp4");

            var expectedFallback = Path.Combine(documents, "film.mp4")
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            var normalizedResult = result
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            Assert.Equal(expectedFallback, normalizedResult, ignoreCase: true);
            Assert.False(string.Equals(
                Path.Combine(videoLink, "film.mp4")
                    .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar),
                normalizedResult,
                StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { Directory.Delete(videoLink); } catch { }
        }
    }

    [JunctionFact]
    public void ResolvePhoto_Dateiverknuepfung_WirdNichtAlsQuelleAufgeloest()
    {
        using var temp = new TempDir();
        var documents = temp.CreateSubdir("export/Dokumente");
        var external = temp.CreateSubdir("fremd");
        var externalPhoto = Path.Combine(external, "foto.jpg");
        File.WriteAllText(externalPhoto, "fremdes-foto");
        var photoLink = Path.Combine(documents, "foto.jpg");
        File.CreateSymbolicLink(photoLink, externalPhoto);

        try
        {
            var result = new VsaMediaPathFileResolver().ResolvePhoto(
                Path.Combine(documents, "export.xtf"),
                relativeFolder: null,
                fileName: "foto.jpg");

            Assert.Equal(
                Path.GetFullPath(Path.Combine(documents, "Foto", "foto.jpg")),
                Path.GetFullPath(result),
                ignoreCase: true);
        }
        finally
        {
            try { File.Delete(photoLink); } catch { }
        }
    }

    // ---------------- Resolver: UNC-Sperre (S2-3) ----------------

    [Fact]
    public void ResolvePhoto_UncPfad_WirdVerworfen()
    {
        var result = new VsaMediaPathFileResolver().ResolvePhoto(
            "export.xtf", null, @"\\fremder-host\share\foto.jpg");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ResolveVideo_UncPfad_WirdVerworfen()
    {
        var result = new VsaMediaPathFileResolver().ResolveVideo(
            "export.xtf", null, @"\\fremder-host\share\film.mp4");

        Assert.Equal(string.Empty, result);
    }

    // ---------------- Kopierpfade: MediaDistributionService (S2-1/S2-3) ----------------

    [Fact]
    public void DistributeImportedMedia_AbsoluterMp4Pfad_WirdKopiert()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var quelle = temp.CreateSubdir("quelle");
        var videoQuelle = Path.Combine(quelle, "inspektion.mp4");
        File.WriteAllText(videoQuelle, "videodaten");

        var project = NewProject("06.123-456", "Link", videoQuelle);

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project);

        Assert.Equal(1, result.FilesCopied);
        Assert.Equal(0, result.Errors);
        Assert.True(File.Exists(videoQuelle)); // Quelle bleibt erhalten
        var neuerLink = project.Data[0].GetFieldValue("Link");
        Assert.False(Path.IsPathRooted(neuerLink));
        Assert.True(File.Exists(Path.Combine(projectFolder, neuerLink.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void DistributeImportedMedia_AbsoluterXlsxPfad_WirdNichtKopiert()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var quelle = temp.CreateSubdir("quelle");
        var xlsx = Path.Combine(quelle, "geheim.xlsx");
        File.WriteAllText(xlsx, "dokument");

        var project = NewProject("06.123-456", "Link", xlsx);

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project);

        Assert.Equal(0, result.FilesCopied);
        Assert.Equal(xlsx, project.Data[0].GetFieldValue("Link")); // Feld bleibt unveraendert
        Assert.Contains(result.Messages, m => m.Contains("nicht erlaubt", StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(Path.Combine(projectFolder, "Haltungen_Verteilt", "06.123-456", "PDF", "geheim.xlsx")));
        Assert.False(File.Exists(Path.Combine(projectFolder, "Haltungen_Verteilt", "06.123-456", "Video", "geheim.xlsx")));
    }

    [Fact]
    public void DistributeImportedMedia_ProtokollFotoXlsx_WirdNichtKopiert()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var quelle = temp.CreateSubdir("quelle");
        var xlsx = Path.Combine(quelle, "geheim.xlsx");
        File.WriteAllText(xlsx, "dokument");

        var project = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "06-001", FieldSource.Manual, userEdited: false);
        var revision = new ProtocolRevision();
        var entry = new ProtocolEntry { Code = "BAB" };
        entry.FotoPaths.Add(xlsx);
        revision.Entries.Add(entry);
        record.Protocol = new ProtocolDocument { Current = revision };
        project.Data.Add(record);

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project);

        Assert.Equal(0, result.FilesCopied);
        Assert.Equal(xlsx, entry.FotoPaths[0]);
        Assert.Contains(result.Messages, m => m.Contains("nicht erlaubt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DistributeImportedMedia_UncPfad_WirdNichtKopiert()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var unc = @"\\fremder-host\share\video.mp4";

        var project = NewProject("06.123-456", "Link", unc);

        var result = new MediaDistributionService()
            .DistributeImportedMedia(projectFolder, project);

        Assert.Equal(0, result.FilesCopied);
        Assert.Equal(unc, project.Data[0].GetFieldValue("Link"));
        Assert.Contains(result.Messages, m => m.Contains("UNC", StringComparison.OrdinalIgnoreCase));
    }

    // ---------------- Kopierpfade: KanalImportDistributionService (S2-1) ----------------

    [Fact]
    public void KanalImport_FallbackVideoMp4_WirdKopiert()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var quelle = temp.CreateSubdir("quelle");
        var videoQuelle = Path.Combine(quelle, "inspektion.mp4");
        File.WriteAllText(videoQuelle, "videodaten");

        var project = NewProject("06.123-456", "Link", videoQuelle);

        var result = new KanalImportDistributionService().Distribute(
            project, projectFolder,
            archivedPdfDir: Path.Combine(projectFolder, "Importdateien", "PDF"), // existiert nicht -> kein Split
            sourceVideoDir: quelle);

        Assert.Equal(1, result.VideosDistributed);
        Assert.Equal(0, result.Errors);
        var neuerLink = project.Data[0].GetFieldValue("Link");
        Assert.False(Path.IsPathRooted(neuerLink));
        Assert.EndsWith(".mp4", neuerLink, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(projectFolder, neuerLink.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void KanalImport_FallbackVideoXlsx_WirdNichtKopiert()
    {
        using var temp = new TempDir();
        var projectFolder = temp.CreateSubdir("projekt");
        var quelle = temp.CreateSubdir("quelle");
        var xlsx = Path.Combine(quelle, "geheim.xlsx");
        File.WriteAllText(xlsx, "dokument");

        var project = NewProject("06.123-456", "Link", xlsx);

        var result = new KanalImportDistributionService().Distribute(
            project, projectFolder,
            archivedPdfDir: Path.Combine(projectFolder, "Importdateien", "PDF"),
            sourceVideoDir: quelle);

        Assert.Equal(0, result.VideosDistributed);
        Assert.Equal(xlsx, project.Data[0].GetFieldValue("Link"));
        Assert.Contains(result.Messages, m => m.Contains("nicht erlaubt", StringComparison.OrdinalIgnoreCase));
    }

    // ---------------- Kopierpfade: ProjectPortabilityService (S2-1/S2-3) ----------------

    [Fact]
    public void MakePortable_ExternesFotoJpg_WirdKopiert()
    {
        using var temp = new TempDir();
        var root = temp.CreateSubdir("projekt");
        var ext = temp.CreateSubdir("quelle");
        var holding = "06-001";
        Directory.CreateDirectory(Path.Combine(root, "Verteilung", holding));
        var srcFoto = Path.Combine(ext, "H_06-001_001.jpg");
        File.WriteAllText(srcFoto, "img");

        var project = new Project();
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", holding, FieldSource.Xtf, userEdited: false);
        rec.VsaFindings.Add(new VsaFinding { FotoPath = srcFoto });
        project.Data.Add(rec);

        var result = new ProjectPortabilityService().MakePortable(root, project);

        Assert.True(result.FotosCopied >= 1);
        var foto = rec.VsaFindings[0].FotoPath ?? "";
        Assert.False(Path.IsPathRooted(foto));
        Assert.True(File.Exists(Path.Combine(root, foto.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void MakePortable_ExternesFotoXlsx_WirdNichtKopiert()
    {
        using var temp = new TempDir();
        var root = temp.CreateSubdir("projekt");
        var ext = temp.CreateSubdir("quelle");
        var holding = "06-001";
        Directory.CreateDirectory(Path.Combine(root, "Verteilung", holding));
        var xlsx = Path.Combine(ext, "geheim.xlsx");
        File.WriteAllText(xlsx, "dokument");

        var project = new Project();
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", holding, FieldSource.Xtf, userEdited: false);
        rec.VsaFindings.Add(new VsaFinding { FotoPath = xlsx });
        project.Data.Add(rec);

        var result = new ProjectPortabilityService().MakePortable(root, project);

        Assert.Equal(0, result.FotosCopied);
        Assert.True(result.Unresolved >= 1);
        Assert.Equal(xlsx, rec.VsaFindings[0].FotoPath); // Feld bleibt unveraendert
    }

    [Fact]
    public void MakePortable_UncFoto_WirdNichtKopiert()
    {
        using var temp = new TempDir();
        var root = temp.CreateSubdir("projekt");
        var holding = "06-001";
        Directory.CreateDirectory(Path.Combine(root, "Verteilung", holding));
        var unc = @"\\fremder-host\share\foto.jpg";

        var project = new Project();
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", holding, FieldSource.Xtf, userEdited: false);
        rec.VsaFindings.Add(new VsaFinding { FotoPath = unc });
        project.Data.Add(rec);

        var result = new ProjectPortabilityService().MakePortable(root, project);

        Assert.Equal(0, result.FotosCopied);
        Assert.True(result.Unresolved >= 1);
        Assert.Equal(unc, rec.VsaFindings[0].FotoPath);
    }

    private static Project NewProject(string haltungsname, string fieldName, string fieldValue)
    {
        var project = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", haltungsname, FieldSource.Manual, userEdited: false);
        record.SetFieldValue(fieldName, fieldValue, FieldSource.Manual, userEdited: false);
        project.Data.Add(record);
        project.Dirty = false;
        return project;
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xtf_media_sec_" + Guid.NewGuid().ToString("N"));
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
