using System.Text.Json;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class MediaConflictCenterServiceTests
{
    [JunctionFact]
    public void Scan_BetrittKeinenVerknuepftenHaltungsroot()
    {
        var root = TempRoot();
        var projectRoot = Path.Combine(root, "Projekt");
        var external = Path.Combine(root, "Fremd");
        var holdingsLink = Path.Combine(projectRoot, "Haltungen");
        var externalHolding = Path.Combine(external, "H-1");
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(externalHolding);
        File.WriteAllText(
            Path.Combine(externalHolding, "20260821_H-1_VIDEO_MISSING.txt"),
            "Haltung: H-1");
        JunctionTestSupport.CreateDirectoryLink(holdingsLink, external);

        try
        {
            var conflicts = new MediaConflictCenterService().Scan(projectRoot);

            Assert.Empty(conflicts);
        }
        finally
        {
            DeleteLinkAndRoot(holdingsLink, root);
        }
    }

    [JunctionFact]
    public void ScanWithResult_unterscheidet_unsicheren_Pfad_von_keinen_Konflikten()
    {
        var root = TempRoot();
        var projectRoot = Path.Combine(root, "Projekt");
        var external = Path.Combine(root, "Fremd");
        var holdingsLink = Path.Combine(projectRoot, "Haltungen");
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(external);
        JunctionTestSupport.CreateDirectoryLink(holdingsLink, external);

        try
        {
            var result = new MediaConflictCenterService().ScanWithResult(projectRoot);

            Assert.False(result.Success);
            Assert.Empty(result.Cases);
            Assert.Contains("nicht sicher geprueft", result.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteLinkAndRoot(holdingsLink, root);
        }
    }

    [JunctionFact]
    public void ResolveConflict_SchreibtUndLoeschtNichtDurchVerknuepftenHaltungsordner()
    {
        var root = TempRoot();
        var projectRoot = Path.Combine(root, "Projekt");
        var holdingsRoot = Path.Combine(projectRoot, "Haltungen");
        var holdingLink = Path.Combine(holdingsRoot, "H-1");
        var external = Path.Combine(root, "Fremd");
        var source = Path.Combine(root, "quelle.mp4");
        Directory.CreateDirectory(holdingsRoot);
        Directory.CreateDirectory(external);
        File.WriteAllText(source, "kunden-video");
        JunctionTestSupport.CreateDirectoryLink(holdingLink, external);
        var infoPath = Path.Combine(holdingLink, "20260821_H-1_VIDEO_MISSING.txt");
        File.WriteAllText(infoPath, "Konflikt");

        try
        {
            var conflict = Conflict(infoPath, holdingLink, "H-1", "20260821");

            var result = new MediaConflictCenterService().ResolveConflict(
                new Project(),
                conflict,
                source);

            Assert.False(result.Success);
            Assert.True(File.Exists(infoPath));
            Assert.Equal(
                new[] { Path.GetFileName(infoPath) },
                Directory.GetFiles(external).Select(Path.GetFileName).ToArray());
            Assert.Equal("kunden-video", File.ReadAllText(source));
        }
        finally
        {
            DeleteLinkAndRoot(holdingLink, root);
        }
    }

    [Fact]
    public void ResolveConflict_GleichnamigerAndererInhaltErhaeltEigenesZiel()
    {
        var root = TempRoot();
        var holding = Path.Combine(root, "Projekt", "Haltungen", "H-1");
        var sourceDirectory = Path.Combine(root, "Quelle");
        Directory.CreateDirectory(holding);
        Directory.CreateDirectory(sourceDirectory);
        var fileName = "20260821_H-1.mp4";
        var existing = Path.Combine(holding, fileName);
        var source = Path.Combine(sourceDirectory, fileName);
        File.WriteAllText(existing, "AAAA");
        File.WriteAllText(source, "BBBB");
        var infoPath = Path.Combine(holding, "20260821_H-1_VIDEO_MISSING.txt");
        File.WriteAllText(infoPath, "Konflikt");

        try
        {
            var result = new MediaConflictCenterService().ResolveConflict(
                ProjectWithHolding("H-1"),
                Conflict(infoPath, holding, "H-1", "20260821"),
                source);

            Assert.True(result.Success, result.Message);
            Assert.NotEqual(existing, result.DestVideoPath);
            Assert.Equal("AAAA", File.ReadAllText(existing));
            Assert.Equal("BBBB", File.ReadAllText(result.DestVideoPath!));
        }
        finally
        {
            TryDeleteRoot(root);
        }
    }

    [Fact]
    public void ResolveConflict_GleicheGroesseUndKopfSchwanzSindKeinInhaltsbeweis()
    {
        var root = TempRoot();
        var holding = Path.Combine(root, "Projekt", "Haltungen", "H-1");
        var sourceDirectory = Path.Combine(root, "Quelle");
        Directory.CreateDirectory(holding);
        Directory.CreateDirectory(sourceDirectory);
        const int size = 3 * 1024 * 1024;
        var sourceBytes = new byte[size];
        var existingBytes = new byte[size];
        sourceBytes[size / 2] = 1;
        existingBytes[size / 2] = 2;
        var source = Path.Combine(sourceDirectory, "quelle.mp4");
        var existing = Path.Combine(holding, "anderes.mp4");
        File.WriteAllBytes(source, sourceBytes);
        File.WriteAllBytes(existing, existingBytes);
        var infoPath = Path.Combine(holding, "20260821_H-1_VIDEO_MISSING.txt");
        File.WriteAllText(infoPath, "Konflikt");

        try
        {
            var result = new MediaConflictCenterService().ResolveConflict(
                ProjectWithHolding("H-1"),
                Conflict(infoPath, holding, "H-1", "20260821"),
                source);

            Assert.True(result.Success, result.Message);
            Assert.NotEqual(existing, result.DestVideoPath);
            Assert.Equal(sourceBytes, File.ReadAllBytes(result.DestVideoPath!));
            Assert.Equal(existingBytes, File.ReadAllBytes(existing));
        }
        finally
        {
            TryDeleteRoot(root);
        }
    }

    [Fact]
    public void ResolveConflict_OhnePassendenDatensatz_BelaesstMarkerUndProjektUnveraendert()
    {
        var root = TempRoot();
        var projectRoot = Path.Combine(root, "Projekt");
        var holding = Path.Combine(projectRoot, "Haltungen", "H-1");
        Directory.CreateDirectory(holding);
        var source = Path.Combine(root, "quelle.mp4");
        File.WriteAllText(source, "kundenvideo");
        var infoPath = Path.Combine(holding, "20260821_H-1_VIDEO_MISSING.txt");
        File.WriteAllText(infoPath, "Konflikt");
        var project = new Project();

        try
        {
            var result = new MediaConflictCenterService().ResolveConflict(
                project,
                projectRoot,
                Conflict(infoPath, holding, "H-1", "20260821"),
                source);

            Assert.False(result.Success);
            Assert.Contains("Datensatz", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(infoPath));
            Assert.False(project.Dirty);
            Assert.Equal(
                new[] { Path.GetFileName(infoPath) },
                Directory.GetFiles(holding).Select(Path.GetFileName).ToArray());
        }
        finally
        {
            TryDeleteRoot(root);
        }
    }

    [JunctionFact]
    public void TryResolveLearnedSourcePath_GelernteDateiverknuepfungWirdAbgewiesen()
    {
        var root = TempRoot();
        var externalDirectory = Path.Combine(root, "Fremd");
        var sourceDirectory = Path.Combine(root, "Quelle");
        Directory.CreateDirectory(externalDirectory);
        Directory.CreateDirectory(sourceDirectory);
        var externalVideo = Path.Combine(externalDirectory, "video.mp4");
        var sourceLink = Path.Combine(sourceDirectory, "video.mp4");
        File.WriteAllText(externalVideo, "kunden-video");
        File.CreateSymbolicLink(sourceLink, externalVideo);
        var conflict = Conflict(
            Path.Combine(root, "20260821_H-1_VIDEO_MISSING.txt"),
            Path.Combine(root, "H-1"),
            "H-1",
            "20260821");
        var project = ProjectWithLearnedMapping(conflict, "video.mp4", sourceLink);

        try
        {
            var result = new MediaConflictCenterService().TryResolveLearnedSourcePath(
                project,
                conflict);

            Assert.Null(result);
            Assert.Equal("kunden-video", File.ReadAllText(externalVideo));
        }
        finally
        {
            TryDeleteFileLink(sourceLink);
            TryDeleteRoot(root);
        }
    }

    [Fact]
    public void TryResolveLearnedSourcePath_RegulaereVideodateiBleibtVerwendbar()
    {
        var root = TempRoot();
        var source = Path.Combine(root, "Quelle", "video.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.WriteAllText(source, "kunden-video");
        var conflict = Conflict(
            Path.Combine(root, "20260821_H-1_VIDEO_MISSING.txt"),
            Path.Combine(root, "H-1"),
            "H-1",
            "20260821");
        var project = ProjectWithLearnedMapping(conflict, "video.mp4", source);

        try
        {
            var result = new MediaConflictCenterService().TryResolveLearnedSourcePath(
                project,
                conflict);

            Assert.Equal(Path.GetFullPath(source), result);
        }
        finally
        {
            TryDeleteRoot(root);
        }
    }

    [JunctionFact]
    public void Verzeichnisverknuepfung_WirdWederAlsSuchrootVerwendetNochAlsAuswahlKopiert()
    {
        var root = TempRoot();
        var projectRoot = Path.Combine(root, "Projekt");
        var holding = Path.Combine(projectRoot, "Haltungen", "H-1");
        var externalDirectory = Path.Combine(root, "Fremd");
        var sourceLink = Path.Combine(root, "Quelle-Link");
        Directory.CreateDirectory(holding);
        Directory.CreateDirectory(externalDirectory);
        var externalVideo = Path.Combine(externalDirectory, "video.mp4");
        File.WriteAllText(externalVideo, "kunden-video");
        JunctionTestSupport.CreateDirectoryLink(sourceLink, externalDirectory);
        var selectedVideo = Path.Combine(sourceLink, "video.mp4");
        var infoPath = Path.Combine(holding, "20260821_H-1_VIDEO_MISSING.txt");
        File.WriteAllText(infoPath, "Konflikt");
        var conflict = Conflict(infoPath, holding, "H-1", "20260821");
        var learnedProject = ProjectWithLearnedMapping(
            conflict,
            "video.mp4",
            Path.Combine(root, "Fehlend", "video.mp4"));
        var project = ProjectWithHolding("H-1");

        try
        {
            var service = new MediaConflictCenterService();

            var learnedSource = service.TryResolveLearnedSourcePath(
                learnedProject,
                conflict,
                preferredVideoRoot: sourceLink);
            var result = service.ResolveConflict(
                project,
                projectRoot,
                conflict,
                selectedVideo);

            Assert.Null(learnedSource);
            Assert.False(result.Success);
            Assert.Contains("Verknuepfung", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(infoPath));
            Assert.False(project.Dirty);
            Assert.Equal("kunden-video", File.ReadAllText(externalVideo));
            Assert.Equal(
                new[] { Path.GetFileName(infoPath) },
                Directory.GetFiles(holding).Select(Path.GetFileName).ToArray());
        }
        finally
        {
            DeleteLinkAndRoot(sourceLink, root);
        }
    }

    [Fact]
    public void ResolveConflict_NichtErlaubteVideoendungWirdNichtKopiert()
    {
        var root = TempRoot();
        var projectRoot = Path.Combine(root, "Projekt");
        var holding = Path.Combine(projectRoot, "Haltungen", "H-1");
        var sourceDirectory = Path.Combine(root, "Quelle");
        Directory.CreateDirectory(holding);
        Directory.CreateDirectory(sourceDirectory);
        var source = Path.Combine(sourceDirectory, "video.txt");
        File.WriteAllText(source, "kein-video");
        var infoPath = Path.Combine(holding, "20260821_H-1_VIDEO_MISSING.txt");
        File.WriteAllText(infoPath, "Konflikt");
        var project = ProjectWithHolding("H-1");

        try
        {
            var result = new MediaConflictCenterService().ResolveConflict(
                project,
                projectRoot,
                Conflict(infoPath, holding, "H-1", "20260821"),
                source);

            Assert.False(result.Success);
            Assert.Contains("Video-Endung", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(infoPath));
            Assert.False(project.Dirty);
            Assert.Equal(
                new[] { Path.GetFileName(infoPath) },
                Directory.GetFiles(holding).Select(Path.GetFileName).ToArray());
        }
        finally
        {
            TryDeleteRoot(root);
        }
    }

    [Fact]
    public void ResolveConflict_UncQuelleWirdVorDemKopierenAbgewiesen()
    {
        var root = TempRoot();
        var projectRoot = Path.Combine(root, "Projekt");
        var holding = Path.Combine(projectRoot, "Haltungen", "H-1");
        Directory.CreateDirectory(holding);
        var infoPath = Path.Combine(holding, "20260821_H-1_VIDEO_MISSING.txt");
        File.WriteAllText(infoPath, "Konflikt");
        var project = ProjectWithHolding("H-1");

        try
        {
            var result = new MediaConflictCenterService().ResolveConflict(
                project,
                projectRoot,
                Conflict(infoPath, holding, "H-1", "20260821"),
                @"\\localhost\SewerStudio_DarfNichtLesen\video.mp4");

            Assert.False(result.Success);
            Assert.Contains("UNC", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(infoPath));
            Assert.False(project.Dirty);
            Assert.Equal(
                new[] { Path.GetFileName(infoPath) },
                Directory.GetFiles(holding).Select(Path.GetFileName).ToArray());
        }
        finally
        {
            TryDeleteRoot(root);
        }
    }

    private static MediaConflictCenterService.MediaConflictCase Conflict(
        string infoPath,
        string holdingFolder,
        string holdingName,
        string dateStamp)
        => new(
            infoPath,
            holdingFolder,
            holdingName,
            holdingName,
            SourcePdfPath: null,
            DateStamp: dateStamp,
            Date: null,
            ExpectedVideoName: null,
            Type: MediaConflictCenterService.ConflictType.Missing,
            Candidates: Array.Empty<string>(),
            Fingerprint: $"{dateStamp}|{holdingName}");

    private static Project ProjectWithHolding(string holdingName)
    {
        var project = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", holdingName, FieldSource.Manual, userEdited: false);
        project.AddRecord(record);
        project.Dirty = false;
        return project;
    }

    private static Project ProjectWithLearnedMapping(
        MediaConflictCenterService.MediaConflictCase conflict,
        string selectedFileName,
        string lastKnownSourcePath)
    {
        var project = new Project();
        var mapping = new MediaConflictCenterService.LearnedVideoMapping(
            conflict.Fingerprint,
            selectedFileName,
            lastKnownSourcePath,
            DateTime.UtcNow);
        project.Metadata[MediaConflictCenterService.MappingMetadataKey] = JsonSerializer.Serialize(new
        {
            ByFingerprint = new Dictionary<string, MediaConflictCenterService.LearnedVideoMapping>
            {
                [conflict.Fingerprint] = mapping
            },
            ByFilmName = new Dictionary<string, MediaConflictCenterService.LearnedVideoMapping>()
        });
        return project;
    }

    private static string TempRoot()
        => Path.Combine(Path.GetTempPath(), $"MediaConflictCenter_{Guid.NewGuid():N}");

    private static void DeleteLinkAndRoot(string link, string root)
    {
        try
        {
            if (Directory.Exists(link))
                Directory.Delete(link);
        }
        catch
        {
            // Test-Aufraeumen darf das Ergebnis nicht verdecken.
        }

        TryDeleteRoot(root);
    }

    private static void TryDeleteFileLink(string link)
    {
        try
        {
            if (File.Exists(link))
                File.Delete(link);
        }
        catch
        {
            // Test-Aufraeumen darf das Ergebnis nicht verdecken.
        }
    }

    private static void TryDeleteRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen darf das Ergebnis nicht verdecken.
        }
    }
}
