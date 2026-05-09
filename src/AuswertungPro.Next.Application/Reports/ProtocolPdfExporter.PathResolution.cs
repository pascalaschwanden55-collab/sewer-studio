using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Application.Reports;

// ProtocolPdfExporter Pfad-Aufloesung: Logo-Bytes (mit Kandidaten-Liste),
// Foto-Pfad-Resolver (mit Cache + FindFileByName-Fallback), SafeReadAllBytes.
// Aus dem Hauptdatei extrahiert (Slice 21a).
public sealed partial class ProtocolPdfExporter
{
    private static byte[]? ResolveLogoBytes(HaltungsprotokollPdfOptions options, string projectRootAbs)
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

    private static List<string> ResolvePhotoPaths(
        IReadOnlyList<string> photoPaths,
        string projectRootAbs,
        int maxPhotos,
        Dictionary<string, string?> resolveCache)
    {
        var list = new List<string>();
        foreach (var raw in photoPaths)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var resolved = ResolvePhotoPath(projectRootAbs, raw, resolveCache);
            if (string.IsNullOrWhiteSpace(resolved) || !File.Exists(resolved))
                continue;

            list.Add(resolved);
            if (list.Count >= maxPhotos)
                break;
        }

        return list;
    }

    private static string ResolvePhotoPath(
        string projectRootAbs,
        string raw,
        Dictionary<string, string?> resolveCache)
    {
        var normalized = raw.Replace('/', Path.DirectorySeparatorChar);
        var fileName = Path.GetFileName(normalized);

        if (Path.IsPathRooted(normalized))
        {
            if (File.Exists(normalized))
                return normalized;

            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var rootedSearchRoot = Path.GetDirectoryName(normalized);
                while (!string.IsNullOrWhiteSpace(rootedSearchRoot))
                {
                    if (Directory.Exists(rootedSearchRoot))
                    {
                        var rootedMatch = FindFileByName(rootedSearchRoot, fileName, resolveCache);
                        if (!string.IsNullOrWhiteSpace(rootedMatch))
                            return rootedMatch;
                    }

                    rootedSearchRoot = Path.GetDirectoryName(rootedSearchRoot);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(projectRootAbs))
            return normalized;

        var direct = Path.Combine(projectRootAbs, normalized);
        if (File.Exists(direct))
            return direct;

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var candidates = new[] { "Fotos", "Photos", "Bilder", "Images", "Fotos_TV", "TV_Fotos", "Foto", "Photo" };
            foreach (var sub in candidates)
            {
                var candidate = Path.Combine(projectRootAbs, sub, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }

            var parent = Path.GetDirectoryName(projectRootAbs);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                var parentCandidate = Path.Combine(parent, normalized);
                if (File.Exists(parentCandidate))
                    return parentCandidate;
            }

            var projectMatch = FindFileByName(projectRootAbs, fileName, resolveCache);
            if (!string.IsNullOrWhiteSpace(projectMatch))
                return projectMatch;

            if (!string.IsNullOrWhiteSpace(parent))
            {
                var parentMatch = FindFileByName(parent, fileName, resolveCache);
                if (!string.IsNullOrWhiteSpace(parentMatch))
                    return parentMatch;
            }
        }

        return direct;
    }

    private static string? FindFileByName(
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
            found = Directory.EnumerateFiles(searchRoot, fileName, SearchOption.AllDirectories).FirstOrDefault();
        }
        catch
        {
            found = null;
        }

        cache[cacheKey] = found;
        return found;
    }

    private static byte[]? SafeReadAllBytes(string path)
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
