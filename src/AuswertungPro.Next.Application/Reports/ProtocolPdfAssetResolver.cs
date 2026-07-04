namespace AuswertungPro.Next.Application.Reports;

internal static class ProtocolPdfAssetResolver
{
    internal static byte[]? ResolveLogoBytes(HaltungsprotokollPdfOptions options, string projectRootAbs)
    {
        foreach (var path in BuildLogoCandidates(options.LogoPathAbs, projectRootAbs))
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;
            if (!File.Exists(path))
                continue;

            try
            {
                return File.ReadAllBytes(path);
            }
            catch
            {
                // try next candidate
            }
        }

        return null;
    }

    internal static IEnumerable<string> BuildLogoCandidates(string? explicitLogo, string projectRootAbs)
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

    internal static List<string> ResolvePhotoPaths(
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

            var key = NormalizeResolvedPhotoKey(resolved);
            if (!seen.Add(key))
                continue;

            list.Add(resolved);
            if (list.Count >= maxPhotos)
                break;
        }

        return list;
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

    internal static string ResolvePhotoPath(
        string projectRootAbs,
        string raw,
        Dictionary<string, string?> resolveCache,
        string? preferredFolder = null)
    {
        var normalized = raw.Replace('/', Path.DirectorySeparatorChar);
        var fileName = Path.GetFileName(normalized);

        // 1) Bevorzugter (Verteil-)Ordner der Haltung, z.B. Fotos\Haltungen\<H>. Greift auch dann,
        //    wenn der gespeicherte Foto-Pfad auf einen veralteten Import-Ort zeigt (haeufig nach
        //    Medienverteilung: Datei liegt im Haltungsordner, nicht mehr am Original-Pfad).
        if (!string.IsNullOrWhiteSpace(preferredFolder) && !string.IsNullOrWhiteSpace(fileName))
        {
            var preferred = Path.Combine(preferredFolder, fileName);
            if (File.Exists(preferred))
                return preferred;

            var renamedPreferred = FindUniquePhotoBySuffix(preferredFolder, fileName, resolveCache);
            if (!string.IsNullOrWhiteSpace(renamedPreferred))
                return renamedPreferred;
        }

        // 2) Absoluter Pfad, falls die Datei dort noch liegt.
        if (Path.IsPathRooted(normalized) && File.Exists(normalized))
            return normalized;

        if (string.IsNullOrWhiteSpace(projectRootAbs))
            return normalized;

        // 3) Relativ zum Projekt-Root.
        if (!Path.IsPathRooted(normalized))
        {
            var direct = Path.Combine(projectRootAbs, normalized);
            if (File.Exists(direct))
                return direct;
        }

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            // 4) Bekannte Foto-Unterordner direkt unter dem Root.
            var candidates = new[] { "Fotos", "Photos", "Bilder", "Images", "Fotos_TV", "TV_Fotos", "Foto", "Photo" };
            foreach (var sub in candidates)
            {
                var candidate = Path.Combine(projectRootAbs, sub, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }

            // 5) Projektweite Suche nach Dateiname — NUR innerhalb des Projekt-Roots.
            //    KEIN Hochlaufen bis zum Laufwerk (das war ein potenzieller Voll-Scan von D:\).
            var projectMatch = FindFileByName(projectRootAbs, fileName, resolveCache);
            if (!string.IsNullOrWhiteSpace(projectMatch))
                return projectMatch;

            // Rename-Fallback fuer alte Projekte: Der Haltungsname wurde im Datagrid geaendert,
            // aber der Foto-Ordner bzw. die Foto-Dateien wurden damals noch nicht mitbenannt.
            // Nur bei eindeutiger Foto-Nummer innerhalb Fotos\Haltungen verwenden.
            var renamedHoldingPhoto = FindUniqueRenamedHoldingPhoto(projectRootAbs, normalized, fileName, resolveCache);
            if (!string.IsNullOrWhiteSpace(renamedHoldingPhoto))
                return renamedHoldingPhoto;
        }

        return Path.IsPathRooted(normalized) ? normalized : Path.Combine(projectRootAbs, normalized);
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

        string? found = null;
        try
        {
            var matches = Common.SafeFileEnumeration
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

    internal static string? FindFileByName(
        string? searchRoot,
        string fileName,
        Dictionary<string, string?> cache)
    {
        if (string.IsNullOrWhiteSpace(searchRoot) || string.IsNullOrWhiteSpace(fileName))
            return null;
        if (!Directory.Exists(searchRoot))
            return null;

        var cacheKey = $"{searchRoot}|{fileName}";
        if (cache.TryGetValue(cacheKey, out var cached))
            return cached;

        string? found = null;
        try
        {
            found = Common.SafeFileEnumeration.EnumerateFilesSafe(searchRoot, fileName, recursive: true)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
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

        var fotoRoot = Path.Combine(projectRootAbs, "Fotos", "Haltungen");
        if (!Directory.Exists(fotoRoot))
            return null;

        var cacheKey = $"renamed-holding-photo|{fotoRoot}|{suffix}";
        if (cache.TryGetValue(cacheKey, out var cached))
            return cached;

        string? found = null;
        try
        {
            var matches = Common.SafeFileEnumeration
                .EnumerateFilesSafe(fotoRoot, "*" + suffix, recursive: true)
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
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (string.Equals(parts[i], "Fotos", StringComparison.OrdinalIgnoreCase)
                && string.Equals(parts[i + 1], "Haltungen", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string? TryExtractTrailingPhotoSuffix(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(stem) || string.IsNullOrWhiteSpace(ext))
            return null;

        var idx = stem.LastIndexOf('_');
        if (idx < 0 || idx == stem.Length - 1)
            return null;

        var number = stem[(idx + 1)..];
        if (number.Length == 0 || number.Any(ch => ch < '0' || ch > '9'))
            return null;

        return "_" + number + ext;
    }

    internal static byte[]? SafeReadAllBytes(string path)
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
}
