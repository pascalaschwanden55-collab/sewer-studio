using System.Reflection;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class HaltungsDossierPathSafetyTests
{
    [Fact]
    public void ResolveMediaPath_RejectsTraversalOutsideProjectRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dossier_root_{Guid.NewGuid():N}");
        var outside = Path.Combine(Path.GetTempPath(), $"dossier_outside_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(outside);
            var outsideFile = Path.Combine(outside, "film.mp4");
            File.WriteAllText(outsideFile, "x");

            var raw = Path.Combine("..", Path.GetFileName(outside), "film.mp4");
            var resolved = InvokeResolveMediaPath(raw, root);

            Assert.Null(resolved);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
            try { Directory.Delete(outside, recursive: true); } catch { }
        }
    }

    [Fact]
    public void ResolveMediaPath_AllowsExistingProjectRelativeFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dossier_root_{Guid.NewGuid():N}");
        try
        {
            var mediaDir = Path.Combine(root, "Haltungen", "A");
            Directory.CreateDirectory(mediaDir);
            var mediaFile = Path.Combine(mediaDir, "film.mp4");
            File.WriteAllText(mediaFile, "x");

            var resolved = InvokeResolveMediaPath(Path.Combine("Haltungen", "A", "film.mp4"), root);

            Assert.Equal(Path.GetFullPath(mediaFile), resolved);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Fotoaufloesung_bevorzugt_die_zentrale_Kopie_und_entdoppelt()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dossier_root_{Guid.NewGuid():N}");
        try
        {
            var centralDir = Path.Combine(root, "Fotos", "Haltungen", "22147-22151");
            var archiveDir = Path.Combine(root, "Importdateien", "XTF", "Foto");
            Directory.CreateDirectory(centralDir);
            Directory.CreateDirectory(archiveDir);

            var central = Path.Combine(centralDir, "H_22147-22151_116.jpg");
            var archive = Path.Combine(archiveDir, "H_22147-22151_116.jpg");
            File.WriteAllText(central, "central");
            File.WriteAllText(archive, "archive");

            var record = new HaltungRecord();
            record.SetFieldValue("Haltungsname", "22147-22151", FieldSource.Xtf, userEdited: false);
            var doc = new ProtocolDocument();
            doc.Current.Entries.Add(new ProtocolEntry
            {
                Code = "BAA",
                FotoPaths =
                {
                    archive,
                    "Fotos/Haltungen/22147-22151/H_22147-22151_116.jpg"
                }
            });

            var photos = ResolveDossierPhotos(record, doc, root);

            var photo = Assert.Single(photos);
            Assert.Equal(Path.GetFullPath(central), photo.Path);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static string? InvokeResolveMediaPath(string raw, string projectRoot)
    {
        var method = typeof(HaltungsDossierPdfBuilder).GetMethod(
            "ResolveMediaPath",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return (string?)method!.Invoke(null, [raw, projectRoot]);
    }

    /// <summary>
    /// Genau der Weg, den das Dossier heute geht: eigener Verteil-Fotoordner der Haltung
    /// plus die gemeinsame Fotoseiten-Logik des Haltungsprotokolls.
    /// </summary>
    private static List<ProtocolPdfPhotoSection.PhotoItem> ResolveDossierPhotos(
        HaltungRecord record,
        ProtocolDocument doc,
        string projectRoot)
    {
        var holdingLabel = record.GetFieldValue("Haltungsname") ?? string.Empty;
        var folderMethod = typeof(HaltungsDossierPdfBuilder).GetMethod(
            "ResolveHoldingPhotoFolder",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(folderMethod);
        var preferredFolder = (string?)folderMethod!.Invoke(null, [holdingLabel, projectRoot]);

        var entries = doc.Current.Entries.Where(e => !e.IsDeleted).ToList();
        return ProtocolPdfPhotoSection.BuildItems(
            ProtocolPdfAssetResolver.CompatibilityService,
            entries,
            projectRoot,
            int.MaxValue,
            preferredFolder);
    }
}
