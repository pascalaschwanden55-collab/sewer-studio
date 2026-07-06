using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Protocol;

public static class ProtocolPhotoPathMerger
{
    public static int MergePhotoPaths(ProtocolEntry target, ProtocolEntry source)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);

        return MergePhotoPaths(target, source.FotoPaths);
    }

    public static int MergePhotoPaths(ProtocolEntry target, IEnumerable<string>? sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (sourcePaths is null)
            return 0;

        var paths = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();
        if (paths.Count == 0)
            return 0;

        var existing = new HashSet<string>(
            target.FotoPaths.Select(NormalizePhotoPathKey),
            StringComparer.OrdinalIgnoreCase);
        var existingFileNames = new HashSet<string>(
            target.FotoPaths.Select(PhotoFileNameKey).Where(p => !string.IsNullOrWhiteSpace(p))!,
            StringComparer.OrdinalIgnoreCase);
        var existingPhotoIdentities = new HashSet<string>(
            target.FotoPaths.Select(PhotoDuplicateIdentityKey).Where(p => !string.IsNullOrWhiteSpace(p))!,
            StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var path in paths)
        {
            var normalized = NormalizePhotoPathKey(path);
            var fileName = PhotoFileNameKey(path);
            var photoIdentity = PhotoDuplicateIdentityKey(path);
            if (existing.Contains(normalized))
                continue;
            if (!string.IsNullOrWhiteSpace(fileName) && existingFileNames.Contains(fileName))
                continue;
            if (!string.IsNullOrWhiteSpace(photoIdentity) && existingPhotoIdentities.Contains(photoIdentity))
                continue;

            target.FotoPaths.Add(path);
            existing.Add(normalized);
            if (!string.IsNullOrWhiteSpace(fileName))
                existingFileNames.Add(fileName);
            if (!string.IsNullOrWhiteSpace(photoIdentity))
                existingPhotoIdentities.Add(photoIdentity);
            added++;
        }

        return added;
    }

    private static string NormalizePhotoPathKey(string path)
        => (path ?? string.Empty).Replace('\\', '/').Trim().ToUpperInvariant();

    private static string? PhotoFileNameKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var normalized = path.Replace('\\', '/');
        var idx = normalized.LastIndexOf('/');
        return idx >= 0 ? normalized[(idx + 1)..] : normalized;
    }

    private static string? PhotoDuplicateIdentityKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var normalized = path.Replace('\\', '/');
        if (!LooksLikeKnownProjectPhotoPath(normalized))
            return null;

        var fileName = PhotoFileNameKey(normalized);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(stem) || string.IsNullOrWhiteSpace(ext))
            return null;

        var firstUnderscore = stem.IndexOf('_');
        var lastUnderscore = stem.LastIndexOf('_');
        if (firstUnderscore <= 0 || lastUnderscore <= firstUnderscore || lastUnderscore == stem.Length - 1)
            return null;

        var prefix = stem[..firstUnderscore];
        var number = stem[(lastUnderscore + 1)..];
        if (number.Length == 0 || number.Any(ch => ch < '0' || ch > '9'))
            return null;

        return prefix + "|_" + number + ext;
    }

    private static bool LooksLikeKnownProjectPhotoPath(string normalizedPath)
        => normalizedPath.Contains("Fotos/Haltungen/", StringComparison.OrdinalIgnoreCase)
           || normalizedPath.Contains("Importdateien/XTF/Foto/", StringComparison.OrdinalIgnoreCase);
}
