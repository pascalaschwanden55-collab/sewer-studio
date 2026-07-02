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
}
