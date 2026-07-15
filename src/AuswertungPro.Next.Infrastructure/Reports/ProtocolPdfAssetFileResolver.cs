using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Loest Logo- und Fotodateien innerhalb eines Projekts auf und liest deren Inhalt.
/// Einzelne fehlende oder defekte Dateien werden wie bisher uebersprungen.
/// </summary>
public sealed class ProtocolPdfAssetFileResolver : IProtocolPdfAssetResolver
{
    public byte[]? ResolveLogoBytes(HaltungsprotokollPdfOptions options, string projectRootAbs)
    {
        foreach (var path in BuildLogoCandidates(options.LogoPathAbs, projectRootAbs))
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            var bytes = ReadAllBytes(path);
            if (bytes is not null)
                return bytes;
        }

        return null;
    }

    public IReadOnlyList<string> ResolvePhotoPaths(
        IReadOnlyList<string> photoPaths,
        string projectRootAbs,
        int maxPhotos,
        Dictionary<string, string?> resolveCache,
        string? preferredFolder = null)
    {
        var list = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in photoPaths)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var resolved = ResolvePhotoPath(projectRootAbs, raw, resolveCache, preferredFolder);
            if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
                continue;

            if (!seen.Add(NormalizeResolvedPhotoKey(resolved)))
                continue;

            list.Add(resolved);
            if (list.Count >= maxPhotos)
                break;
        }

        return list;
    }

    public string ResolvePhotoPath(
        string projectRootAbs,
        string raw,
        Dictionary<string, string?> resolveCache,
        string? preferredFolder = null)
    {
        var normalized = raw.Replace('/', Path.DirectorySeparatorChar);
        var fileName = Path.GetFileName(normalized);

        if (!string.IsNullOrWhiteSpace(preferredFolder) && !string.IsNullOrWhiteSpace(fileName))
        {
            var preferred = Path.Combine(preferredFolder, fileName);
            if (File.Exists(preferred))
                return preferred;

            var renamedPreferred = FindUniquePhotoBySuffix(preferredFolder, fileName, resolveCache);
            if (!string.IsNullOrWhiteSpace(renamedPreferred))
                return renamedPreferred;
        }

        if (Path.IsPathRooted(normalized) && File.Exists(normalized))
            return normalized;

        if (string.IsNullOrWhiteSpace(projectRootAbs))
            return normalized;

        if (!Path.IsPathRooted(normalized))
        {
            var direct = Path.Combine(projectRootAbs, normalized);
            if (File.Exists(direct))
                return direct;
        }

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            foreach (var subDirectory in KnownPhotoDirectories)
            {
                var candidate = Path.Combine(projectRootAbs, subDirectory, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }

            var projectMatch = FindFileByName(projectRootAbs, fileName, resolveCache);
            if (!string.IsNullOrWhiteSpace(projectMatch))
                return projectMatch;

            var renamedHoldingPhoto = FindUniqueRenamedHoldingPhoto(
                projectRootAbs,
                normalized,
                fileName,
                resolveCache);
            if (!string.IsNullOrWhiteSpace(renamedHoldingPhoto))
                return renamedHoldingPhoto;
        }

        return Path.IsPathRooted(normalized)
            ? normalized
            : Path.Combine(projectRootAbs, normalized);
    }

    public byte[]? ReadAllBytes(string path)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch
        {
            return null;
        }
    }

    private static readonly string[] KnownPhotoDirectories =
        ["Fotos", "Photos", "Bilder", "Images", "Fotos_TV", "TV_Fotos", "Foto", "Photo"];

    private static IEnumerable<string> BuildLogoCandidates(string? explicitLogo, string projectRootAbs)
    {
        if (!string.IsNullOrWhiteSpace(explicitLogo))
            yield return explicitLogo;

        if (string.IsNullOrWhiteSpace(projectRootAbs))
            yield break;

        yield return Path.Combine(projectRootAbs, "Assets", "Brand", "abwasser-uri-logo.png");
        yield return Path.Combine(projectRootAbs, "Brand", "abwasser-uri-logo.png");
        yield return Path.Combine(projectRootAbs, "Dokumente", "abwasser-uri-logo.png");
        yield return Path.Combine(projectRootAbs, "abwasser-uri-logo.png");
        yield return Path.Combine(projectRootAbs, "logo.png");
        yield return Path.Combine(projectRootAbs, "logo.jpg");
        yield return Path.Combine(projectRootAbs, "logo.jpeg");
    }

    private static string NormalizeResolvedPhotoKey(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path.Replace('/', Path.DirectorySeparatorChar);
        }
    }

    private static string? FindUniquePhotoBySuffix(
        string preferredFolder,
        string fileName,
        Dictionary<string, string?> cache)
    {
        if (string.IsNullOrWhiteSpace(preferredFolder)
            || string.IsNullOrWhiteSpace(fileName)
            || !Directory.Exists(preferredFolder))
            return null;

        var suffix = TryExtractTrailingPhotoSuffix(fileName);
        if (string.IsNullOrWhiteSpace(suffix))
            return null;

        var cacheKey = $"preferred-photo-suffix|{preferredFolder}|{suffix}";
        if (cache.TryGetValue(cacheKey, out var cached))
            return cached;

        string? found;
        try
        {
            var matches = SafeFileEnumeration
                .EnumerateFilesSafe(preferredFolder, "*" + suffix, recursive: false)
                .Where(path => Path.GetFileName(path).EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();

            found = matches.Count == 1 ? matches[0] : null;
        }
        catch
        {
            found = null;
        }

        cache[cacheKey] = found;
        return found;
    }

    private static string? FindFileByName(
        string? searchRoot,
        string fileName,
        Dictionary<string, string?> cache)
    {
        if (string.IsNullOrWhiteSpace(searchRoot)
            || string.IsNullOrWhiteSpace(fileName)
            || !Directory.Exists(searchRoot))
            return null;

        var cacheKey = $"{searchRoot}|{fileName}";
        if (cache.TryGetValue(cacheKey, out var cached))
            return cached;

        string? found;
        try
        {
            found = SafeFileEnumeration.EnumerateFilesSafe(searchRoot, fileName, recursive: true)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch
        {
            found = null;
        }

        cache[cacheKey] = found;
        return found;
    }

    private static string? FindUniqueRenamedHoldingPhoto(
        string projectRootAbs,
        string normalizedPath,
        string fileName,
        Dictionary<string, string?> cache)
    {
        if (string.IsNullOrWhiteSpace(projectRootAbs)
            || string.IsNullOrWhiteSpace(normalizedPath)
            || string.IsNullOrWhiteSpace(fileName)
            || !LooksLikeGroupedHoldingPhotoPath(normalizedPath))
            return null;

        var suffix = TryExtractTrailingPhotoSuffix(fileName);
        if (string.IsNullOrWhiteSpace(suffix))
            return null;

        var photoRoot = Path.Combine(projectRootAbs, "Fotos", "Haltungen");
        if (!Directory.Exists(photoRoot))
            return null;

        var cacheKey = $"renamed-holding-photo|{photoRoot}|{suffix}";
        if (cache.TryGetValue(cacheKey, out var cached))
            return cached;

        string? found;
        try
        {
            var matches = SafeFileEnumeration
                .EnumerateFilesSafe(photoRoot, "*" + suffix, recursive: true)
                .Where(path => Path.GetFileName(path).EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();

            found = matches.Count == 1 ? matches[0] : null;
        }
        catch
        {
            found = null;
        }

        cache[cacheKey] = found;
        return found;
    }

    private static bool LooksLikeGroupedHoldingPhotoPath(string normalizedPath)
    {
        var parts = normalizedPath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < parts.Length - 1; index++)
        {
            if (string.Equals(parts[index], "Fotos", StringComparison.OrdinalIgnoreCase)
                && string.Equals(parts[index + 1], "Haltungen", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string? TryExtractTrailingPhotoSuffix(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(stem) || string.IsNullOrWhiteSpace(extension))
            return null;

        var separatorIndex = stem.LastIndexOf('_');
        if (separatorIndex < 0 || separatorIndex == stem.Length - 1)
            return null;

        var number = stem[(separatorIndex + 1)..];
        if (number.Length == 0 || number.Any(character => character is < '0' or > '9'))
            return null;

        return "_" + number + extension;
    }
}

/// <summary>Kompatibilitaetsfassade fuer bestehende interne PDF-Renderer.</summary>
internal static class ProtocolPdfAssetResolver
{
    internal static IProtocolPdfAssetResolver CompatibilityService { get; } =
        new ProtocolPdfAssetFileResolver();

    internal static byte[]? ResolveLogoBytes(HaltungsprotokollPdfOptions options, string projectRootAbs)
        => CompatibilityService.ResolveLogoBytes(options, projectRootAbs);

    internal static string ResolvePhotoPath(
        string projectRootAbs,
        string raw,
        Dictionary<string, string?> resolveCache,
        string? preferredFolder = null)
        => CompatibilityService.ResolvePhotoPath(projectRootAbs, raw, resolveCache, preferredFolder);
}
