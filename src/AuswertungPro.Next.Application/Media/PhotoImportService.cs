using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Media;

public sealed class PhotoImportService : IPhotoImportService
{
    private static readonly string[] Ext = [".jpg", ".jpeg", ".png", ".webp"];

    public PhotoImportResult ImportFolderToProjectMedia(string folderPath, string projectMediaRootAbs, ProtocolRevision revision)
    {
        Directory.CreateDirectory(projectMediaRootAbs);

        var result = new PhotoImportResult();

        foreach (var file in SafeFileEnumeration.EnumerateFilesSafe(folderPath, "*.*", recursive: true)
                     .Where(f => Ext.Contains(Path.GetExtension(f).ToLowerInvariant())))
        {
            var destName = $"{Guid.NewGuid():N}{Path.GetExtension(file).ToLowerInvariant()}";
            var destAbs = Path.Combine(projectMediaRootAbs, destName);
            File.Copy(file, destAbs, overwrite: false);

            var rel = Path.Combine("media", destName).Replace('\\', '/');
            result.ImportedRelativePaths.Add(rel);

            // Auto-Zuordnung via Meter im Dateinamen (optional)
            var meter = PhotoFileMeterParser.TryParseFromPath(file);
            if (meter is not null)
            {
                var best = PhotoProtocolEntryMatcher.FindNearestActiveEntry(revision.Entries, meter.Value);
                if (best is not null)
                {
                    best.FotoPaths.Add(rel);
                    revision.Changes.Add(new ProtocolChange
                    {
                        Kind = ProtocolChangeKind.AttachPhoto,
                        EntryId = best.EntryId,
                        After = rel
                    });
                    continue;
                }
            }

            result.UnassignedRelativePaths.Add(rel);
        }

        return result;
    }
}
