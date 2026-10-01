using System.Text.Json;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Lookup;

/// <summary>Eine neue gepruefte JSON-Sicherung pro Uebernahme. Keine Rotation oder Aenderung vorhandener Dateien.</summary>
public sealed class GeoShopSicherungsdatei(string ordner) : IGeoShopSicherung
{
    public string Sichere(Project projekt)
    {
        var root = Path.GetFullPath(ordner);
        var guard = new ProjectWritePathGuard(root);
        Directory.CreateDirectory(guard.EnsureSafeDirectoryTarget(root));
        var ziel = Path.Combine(root, $"{projekt.Id:N}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.json");
        var temp = ziel + ".tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(projekt, JsonProjectRepository.SerializerOptions);
        try
        {
            using (var file = new FileStream(guard.EnsureSafeFileTarget(temp), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { file.Write(bytes); file.Flush(flushToDisk: true); }
            var gelesen = File.ReadAllBytes(guard.EnsureSafeFileTarget(temp));
            if (!bytes.AsSpan().SequenceEqual(gelesen) || JsonSerializer.Deserialize<Project>(gelesen, JsonProjectRepository.SerializerOptions)?.Id != projekt.Id)
                throw new IOException("Die GeoShop-Sicherung konnte nicht geprüft werden. Es wird nichts übernommen.");
            File.Move(guard.EnsureSafeFileTarget(temp), guard.EnsureSafeFileTarget(ziel));
            return ziel;
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(guard.EnsureSafeFileTarget(temp));
        }
    }
}
