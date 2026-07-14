using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Reports;

public sealed class DossierPhotoFileAvailabilityService : IDossierPhotoAvailabilityService
{
    public bool HasPrintablePhotos(HaltungRecord record, string projectFolder)
    {
        var entries = record.Protocol?.Current?.Entries;
        if (entries is null || entries.Count == 0)
            return false;

        foreach (var entry in entries)
        {
            if (entry.IsDeleted || entry.FotoPaths is null || entry.FotoPaths.Count == 0)
                continue;

            foreach (var raw in entry.FotoPaths)
            {
                var resolved = DossierPhotoPathResolver.Resolve(raw, projectFolder);
                if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
                    return true;
            }
        }

        return false;
    }
}
