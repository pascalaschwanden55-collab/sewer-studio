using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Media;

public sealed class PhotoImportService : IPhotoImportService
{
    private static readonly string[] Ext = [".jpg", ".jpeg", ".png", ".webp"];

    public PhotoImportResult ImportFolderToProjectMedia(string folderPath, string projectMediaRootAbs, ProtocolRevision revision)
    {
        Directory.CreateDirectory(projectMediaRootAbs);

        var result = new PhotoImportResult();

        foreach (var file in Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                     .Where(f => Ext.Contains(Path.GetExtension(f).ToLowerInvariant())))
        {
            var destName = $"{Guid.NewGuid():N}{Path.GetExtension(file).ToLowerInvariant()}";
            var destAbs = Path.Combine(projectMediaRootAbs, destName);
            File.Copy(file, destAbs, overwrite: false);

            var rel = Path.Combine("media", destName).Replace('\\', '/');
            result.ImportedRelativePaths.Add(rel);

            // Auto-Zuordnung via Meter im Dateinamen (optional)
            var meter = TryParseMeterFromFileName(Path.GetFileNameWithoutExtension(file));
            if (meter is not null)
            {
                var best = revision.Entries
                    .Where(e => !e.IsDeleted && e.MeterStart is not null)
                    .OrderBy(e => Math.Abs(e.MeterStart!.Value - meter.Value))
                    .FirstOrDefault();

                if (best is not null && Math.Abs(best.MeterStart!.Value - meter.Value) <= 1.0)
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

    private static double? TryParseMeterFromFileName(string name)
    {
        var m = Regex.Match(name, @"(?<m>\d{1,3}([.,]\d{1,2})?)\s*m?$", RegexOptions.IgnoreCase);
        if (!m.Success) return null;

        var s = m.Groups["m"].Value.Replace(',', '.');
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            return val;

        return null;
    }
}
