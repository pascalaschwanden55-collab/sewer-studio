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
        foreach (var raw in photoPaths)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var resolved = ResolvePhotoPath(projectRootAbs, raw, resolveCache, preferredFolder);
            if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
                continue;

            list.Add(resolved);
            if (list.Count >= maxPhotos)
                break;
        }

        return list;
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
        }

        return Path.IsPathRooted(normalized) ? normalized : Path.Combine(projectRootAbs, normalized);
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
