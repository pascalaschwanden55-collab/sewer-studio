using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingProtocolEntryPhotoPathAppender
{
    public static bool AddIfPresent(ProtocolEntry entry, string? photoPath)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (photoPath == null)
            return false;

        entry.FotoPaths.Add(photoPath);
        return true;
    }

    public static bool AddDistinctNonBlank(ProtocolEntry entry, string? photoPath)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(photoPath))
            return false;

        if (entry.FotoPaths.Contains(photoPath, StringComparer.OrdinalIgnoreCase))
            return false;

        entry.FotoPaths.Add(photoPath);
        return true;
    }
}
