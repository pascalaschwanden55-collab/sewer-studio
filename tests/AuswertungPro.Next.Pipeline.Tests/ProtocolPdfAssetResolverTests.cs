using System;
using System.Collections.Generic;
using System.IO;

using AuswertungPro.Next.Application.Reports;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolPdfAssetResolverTests
{
    [Fact]
    public void ResolvePhotoPath_uses_preferred_folder_when_stored_path_is_dead()
    {
        var temp = Path.Combine(Path.GetTempPath(), "sewerfoto_" + Guid.NewGuid().ToString("N"));
        var haltungDir = Path.Combine(temp, "Fotos", "Haltungen", "H1");
        Directory.CreateDirectory(haltungDir);
        var real = Path.Combine(haltungDir, "L_H1_001.jpg");
        File.WriteAllBytes(real, new byte[] { 1 });
        try
        {
            // gespeicherter Pfad zeigt auf einen (nicht mehr existierenden) Import-Ort
            var deadRooted = Path.Combine(temp, "Importdateien", "XTF", "Foto", "L_H1_001.jpg");
            var cache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            var resolved = ProtocolPdfAssetResolver.ResolvePhotoPath(temp, deadRooted, cache, preferredFolder: haltungDir);

            Assert.Equal(real, resolved);
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void ResolvePhotoPath_finds_file_within_project_when_path_is_dead()
    {
        var temp = Path.Combine(Path.GetTempPath(), "sewerfoto_" + Guid.NewGuid().ToString("N"));
        var haltungDir = Path.Combine(temp, "Fotos", "Haltungen", "H1");
        Directory.CreateDirectory(haltungDir);
        var real = Path.Combine(haltungDir, "L_H1_001.jpg");
        File.WriteAllBytes(real, new byte[] { 1 });
        try
        {
            var deadRooted = Path.Combine(temp, "Importdateien", "XTF", "Foto", "L_H1_001.jpg");
            var cache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            // Ohne bevorzugten Ordner: projektweite Suche findet die Datei unter dem Root.
            var resolved = ProtocolPdfAssetResolver.ResolvePhotoPath(temp, deadRooted, cache);

            Assert.Equal(real, resolved);
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void ResolvePhotoPath_prefers_existing_absolute_path()
    {
        var temp = Path.Combine(Path.GetTempPath(), "sewerfoto_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var real = Path.Combine(temp, "direct.jpg");
        File.WriteAllBytes(real, new byte[] { 1 });
        try
        {
            var cache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            var resolved = ProtocolPdfAssetResolver.ResolvePhotoPath(temp, real, cache);

            Assert.Equal(real, resolved);
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void ResolvePhotoPaths_deduplicates_relative_and_archived_paths_resolving_to_same_photo()
    {
        var temp = Path.Combine(Path.GetTempPath(), "sewerfoto_" + Guid.NewGuid().ToString("N"));
        var haltungDir = Path.Combine(temp, "Fotos", "Haltungen", "H1");
        Directory.CreateDirectory(haltungDir);
        var real = Path.Combine(haltungDir, "L_H1_001.jpg");
        File.WriteAllBytes(real, new byte[] { 1 });
        try
        {
            var relative = Path.Combine("Fotos", "Haltungen", "H1", "L_H1_001.jpg");
            var archived = Path.Combine(temp, "Importdateien", "XTF", "Foto", "L_H1_001.jpg");
            var cache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            var resolved = ProtocolPdfAssetResolver.ResolvePhotoPaths(
                new[] { relative, archived },
                temp,
                maxPhotos: 3,
                cache,
                preferredFolder: haltungDir);

            var only = Assert.Single(resolved);
            Assert.Equal(real, only);
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void ResolvePhotoPaths_prefers_renamed_central_photo_over_existing_old_archive_copy()
    {
        var temp = Path.Combine(Path.GetTempPath(), "sewerfoto_" + Guid.NewGuid().ToString("N"));
        var haltungDir = Path.Combine(temp, "Fotos", "Haltungen", "22147-22151");
        var archiveDir = Path.Combine(temp, "Importdateien", "XTF", "Foto");
        Directory.CreateDirectory(haltungDir);
        Directory.CreateDirectory(archiveDir);

        var central = Path.Combine(haltungDir, "H_22147-22151_116.jpg");
        var archivedOld = Path.Combine(archiveDir, "H_22147-547.01_116.jpg");
        File.WriteAllBytes(central, new byte[] { 1 });
        File.WriteAllBytes(archivedOld, new byte[] { 2 });
        try
        {
            var relativeCentral = Path.Combine("Fotos", "Haltungen", "22147-22151", "H_22147-22151_116.jpg");
            var cache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            var resolved = ProtocolPdfAssetResolver.ResolvePhotoPaths(
                new[] { relativeCentral, archivedOld },
                temp,
                maxPhotos: 3,
                cache,
                preferredFolder: haltungDir);

            var only = Assert.Single(resolved);
            Assert.Equal(central, only);
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void ResolvePhotoPath_finds_uniquely_renamed_holding_photo_when_folder_was_not_synced()
    {
        var temp = Path.Combine(Path.GetTempPath(), "sewerfoto_" + Guid.NewGuid().ToString("N"));
        var oldDir = Path.Combine(temp, "Fotos", "Haltungen", "22147-547.01");
        var newDir = Path.Combine(temp, "Fotos", "Haltungen", "22147-22151");
        Directory.CreateDirectory(oldDir);
        var real = Path.Combine(oldDir, "H_22147-547.01_116.jpg");
        File.WriteAllBytes(real, new byte[] { 1 });
        try
        {
            var stored = "Fotos/Haltungen/22147-22151/H_22147-22151_116.jpg";
            var cache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            var resolved = ProtocolPdfAssetResolver.ResolvePhotoPath(temp, stored, cache, preferredFolder: newDir);

            Assert.Equal(real, resolved);
        }
        finally
        {
            try { Directory.Delete(temp, recursive: true); } catch { /* best-effort */ }
        }
    }
}
