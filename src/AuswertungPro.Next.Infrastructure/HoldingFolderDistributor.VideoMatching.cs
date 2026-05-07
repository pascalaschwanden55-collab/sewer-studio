using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// HoldingFolderDistributor — Video-Matching-Pfad (partial class).
///
/// Refactor 2026-05-07 (Etappe 3, Charge R8): die zentrale Video-Auflosungs-
/// Logik aus der Hauptdatei ausgegliedert. Enthaelt File-System-Enumeration,
/// Sidecar-Index-Aufbau (M150/MDB/XML/XTF), CDIndex-Lookup, Find-by-Filename,
/// Holding-Resolution per Reverse-Match und Photo-Hint-Voting. Mechanisch —
/// keine Verhaltensaenderung.
/// </summary>
public static partial class HoldingFolderDistributor
{
    private static IReadOnlyList<string> EnumerateVideoFiles(string root, bool recursive)
    {
        if (!Directory.Exists(root))
            return Array.Empty<string>();

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(root, "*.*", searchOption)
            .Where(MediaFileTypes.HasVideoExtension)
            .ToList();
    }

    private static List<string> EnumerateSidecarFiles(string folder)
    {
        var sidecarFiles = new List<string>();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return sidecarFiles;

        // Rekursiv suchen: M150/XML liegen in der Praxis oft in Unterordnern.
        // XML nicht mehr über Dateinamen filtern, da viele Exporte generische Namen haben.
        try { sidecarFiles.AddRange(Directory.EnumerateFiles(folder, "*.xtf", SearchOption.AllDirectories)); } catch { }
        try { sidecarFiles.AddRange(Directory.EnumerateFiles(folder, "*.m150", SearchOption.AllDirectories)); } catch { }
        try { sidecarFiles.AddRange(Directory.EnumerateFiles(folder, "*.mdb", SearchOption.AllDirectories)); } catch { }
        try { sidecarFiles.AddRange(Directory.EnumerateFiles(folder, "*.xml", SearchOption.AllDirectories)); } catch { }

        return sidecarFiles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildSidecarVideoLinkIndex(
        string? xtfSourceFolder,
        IReadOnlyList<string> pdfFiles)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var sidecarFolders = ResolveSidecarFolders(xtfSourceFolder, pdfFiles);
        if (sidecarFolders.Count == 0)
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        var sidecarPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in sidecarFolders)
        {
            foreach (var path in EnumerateSidecarFiles(folder))
                sidecarPaths.Add(path);
        }

        foreach (var sidecarPath in sidecarPaths)
        {
            try
            {
                var ext = Path.GetExtension(sidecarPath);
                List<AuswertungPro.Next.Domain.Models.HaltungRecord> records;
                if (ext.Equals(".mdb", StringComparison.OrdinalIgnoreCase))
                {
                    if (!M150MdbImportHelper.TryParseMdbFile(sidecarPath, out records, out _, out _))
                        continue;
                }
                else if (ext.Equals(".m150", StringComparison.OrdinalIgnoreCase)
                         || ext.Equals(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    records = M150MdbImportHelper.ParseM150File(sidecarPath, out _);
                }
                else
                {
                    continue;
                }

                foreach (var rec in records)
                {
                    var hRaw = rec.GetFieldValue("Haltungsname");
                    if (string.IsNullOrWhiteSpace(hRaw))
                        continue;

                    var haltung = SanitizePathSegment(NormalizeHaltungId(hRaw));
                    if (string.IsNullOrWhiteSpace(haltung) || string.Equals(haltung, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var rawLink = rec.GetFieldValue("Link");
                    var normalizedLink = NormalizeVideoFileName(rawLink);
                    if (string.IsNullOrWhiteSpace(normalizedLink))
                        continue;

                    if (!index.TryGetValue(haltung, out var links))
                    {
                        links = new List<string>();
                        index[haltung] = links;
                    }

                    if (!links.Contains(normalizedLink, StringComparer.OrdinalIgnoreCase))
                        links.Add(normalizedLink);
                }
            }
            catch
            {
                // Sidecar parsing is best-effort fallback only.
            }
        }

        return index.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildSidecarHoldingByVideoIndex(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarVideoLinksByHolding)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (sidecarVideoLinksByHolding is null || sidecarVideoLinksByHolding.Count == 0)
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in sidecarVideoLinksByHolding)
        {
            var holding = kv.Key;
            foreach (var rawLink in kv.Value)
            {
                var normalizedLink = NormalizeVideoFileName(rawLink);
                if (string.IsNullOrWhiteSpace(normalizedLink))
                    continue;

                foreach (var key in EnumerateVideoLookupKeys(normalizedLink))
                {
                    if (!index.TryGetValue(key, out var holdings))
                    {
                        holdings = new List<string>();
                        index[key] = holdings;
                    }

                    if (!holdings.Contains(holding, StringComparer.OrdinalIgnoreCase))
                        holdings.Add(holding);
                }
            }
        }

        return index.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateVideoLookupKeys(string? videoFileName)
    {
        var normalized = NormalizeVideoFileName(videoFileName);
        if (string.IsNullOrWhiteSpace(normalized))
            yield break;

        yield return normalized;

        var noExt = Path.GetFileNameWithoutExtension(normalized);
        if (!string.IsNullOrWhiteSpace(noExt))
            yield return noExt;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildCdIndexVideoLinkIndex(
        string? xtfSourceFolder,
        IReadOnlyList<string> pdfFiles)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var cdIndexFolders = ResolveCdIndexFolders(xtfSourceFolder, pdfFiles);
        if (cdIndexFolders.Count == 0)
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        var cdIndexPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in cdIndexFolders)
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(folder, "CDIndex.txt", SearchOption.AllDirectories))
                    cdIndexPaths.Add(path);
            }
            catch
            {
                // Best effort only.
            }
        }

        foreach (var cdIndexPath in cdIndexPaths)
        {
            try
            {
                AddCdIndexMappings(cdIndexPath, index);
            }
            catch
            {
                // Best effort only.
            }
        }

        return index.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ResolveCdIndexFolders(
        string? xtfSourceFolder,
        IReadOnlyList<string> pdfFiles)
    {
        var folders = new HashSet<string>(ResolveSidecarFolders(xtfSourceFolder, pdfFiles), StringComparer.OrdinalIgnoreCase);

        static void AddExisting(HashSet<string> set, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            if (Directory.Exists(path))
                set.Add(path);
        }

        foreach (var pdfPath in pdfFiles)
        {
            var pdfDir = Path.GetDirectoryName(pdfPath);
            if (string.IsNullOrWhiteSpace(pdfDir))
                continue;

            var current = new DirectoryInfo(pdfDir);
            while (current is not null)
            {
                var name = current.Name;
                if (name.Equals("Haltungen", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Schaechte", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Schächte", StringComparison.OrdinalIgnoreCase))
                {
                    AddExisting(folders, current.FullName);
                    AddExisting(folders, current.Parent?.FullName);
                    break;
                }

                current = current.Parent;
            }
        }

        return folders.ToList();
    }

    private static void AddCdIndexMappings(
        string cdIndexPath,
        Dictionary<string, List<string>> index)
    {
        string? currentVideo = null;
        foreach (var rawLine in File.ReadLines(cdIndexPath))
        {
            var line = (rawLine ?? "").Trim();
            if (string.IsNullOrWhiteSpace(line)
                || line.StartsWith("[", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Ident=", StringComparison.OrdinalIgnoreCase))
                continue;

            var entry = line.Split(';')[0].Trim();
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            var fileName = NormalizeVideoFileName(entry);
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            if (HasVideoExtension(fileName))
            {
                currentVideo = fileName;
                continue;
            }

            if (!HasImageExtension(fileName) || string.IsNullOrWhiteSpace(currentVideo))
                continue;

            foreach (var key in EnumeratePhotoLookupKeys(fileName))
            {
                if (!index.TryGetValue(key, out var videos))
                {
                    videos = new List<string>();
                    index[key] = videos;
                }

                if (!videos.Contains(currentVideo, StringComparer.OrdinalIgnoreCase))
                    videos.Add(currentVideo);
            }
        }
    }

    private static IReadOnlyList<string> ResolveSidecarFolders(string? xtfSourceFolder, IReadOnlyList<string> pdfFiles)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void AddExisting(HashSet<string> set, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            if (Directory.Exists(path))
                set.Add(path);
        }

        AddExisting(folders, xtfSourceFolder);

        foreach (var pdfPath in pdfFiles)
        {
            var pdfDir = Path.GetDirectoryName(pdfPath);
            AddExisting(folders, pdfDir);
            if (string.IsNullOrWhiteSpace(pdfDir))
                continue;

            var parent = Directory.GetParent(pdfDir)?.FullName;
            var grandParent = parent is null ? null : Directory.GetParent(parent)?.FullName;

            AddExisting(folders, parent);
            AddExisting(folders, Path.Combine(pdfDir, "XTF"));
            AddExisting(folders, Path.Combine(parent ?? "", "XTF"));
            AddExisting(folders, Path.Combine(parent ?? "", "xtf"));
            AddExisting(folders, Path.Combine(parent ?? "", "M150"));
            AddExisting(folders, Path.Combine(grandParent ?? "", "Imports", "XTF"));
        }

        return folders.ToList();
    }

    private static VideoFindResult FindVideo(
        string videoFileNameFromPdf,
        string videoSourceFolder,
        string haltung,
        string dateStamp,
        bool recursiveVideoSearch,
        IReadOnlyList<string>? videoFilesCache = null)
    {
        var files = videoFilesCache ?? EnumerateVideoFiles(videoSourceFolder, recursiveVideoSearch);
        return HoldingVideoMatching.FindVideo(videoFileNameFromPdf, haltung, dateStamp, files);
    }

    private static VideoFindResult FindVideoByHaltungDate(
        string videoSourceFolder,
        string haltung,
        string dateStamp,
        bool recursiveVideoSearch,
        IReadOnlyList<string>? videoFilesCache = null)
    {
        var files = videoFilesCache ?? EnumerateVideoFiles(videoSourceFolder, recursiveVideoSearch);
        return HoldingVideoMatching.FindVideoByHaltungDate(haltung, dateStamp, files);
    }

    private static VideoFindResult TryFindVideoFromSidecarLinks(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarVideoLinksByHolding,
        string haltung,
        string videoSourceFolder,
        string dateStamp,
        bool recursiveVideoSearch,
        IReadOnlyList<string>? videoFilesCache)
    {
        if (sidecarVideoLinksByHolding is null)
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No sidecar link hints");

        var links = new List<string>();
        foreach (var key in EnumerateHoldingLookupKeys(haltung))
        {
            if (!sidecarVideoLinksByHolding.TryGetValue(key, out var keyLinks) || keyLinks.Count == 0)
                continue;

            foreach (var link in keyLinks)
            {
                if (!links.Contains(link, StringComparer.OrdinalIgnoreCase))
                    links.Add(link);
            }
        }

        if (links.Count == 0)
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No sidecar link hints");

        var matched = new List<string>();
        var ambiguousCandidates = new List<string>();
        foreach (var link in links)
        {
            var fromLink = FindVideo(link, videoSourceFolder, haltung, dateStamp, recursiveVideoSearch, videoFilesCache);
            if (fromLink.Status == VideoMatchStatus.Matched && !string.IsNullOrWhiteSpace(fromLink.VideoPath))
            {
                if (!matched.Contains(fromLink.VideoPath, StringComparer.OrdinalIgnoreCase))
                    matched.Add(fromLink.VideoPath);
                continue;
            }

            if (fromLink.Status == VideoMatchStatus.Ambiguous)
            {
                foreach (var c in fromLink.Candidates)
                {
                    if (!ambiguousCandidates.Contains(c, StringComparer.OrdinalIgnoreCase))
                        ambiguousCandidates.Add(c);
                }
            }
        }

        if (matched.Count == 1)
            return new VideoFindResult(VideoMatchStatus.Matched, matched[0], Array.Empty<string>(), "Matched by M150/MDB sidecar link");
        if (matched.Count > 1)
            return new VideoFindResult(VideoMatchStatus.Ambiguous, null, matched, "Multiple matches from M150/MDB sidecar links");
        if (ambiguousCandidates.Count > 0)
            return new VideoFindResult(VideoMatchStatus.Ambiguous, null, ambiguousCandidates, "Ambiguous from M150/MDB sidecar links");

        return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No match from M150/MDB sidecar links");
    }

    private static VideoFindResult TryFindVideoFromCdIndexPhotoHints(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? cdIndexVideoLinksByPhoto,
        string pdfPath,
        string haltung,
        string videoSourceFolder,
        string dateStamp,
        bool recursiveVideoSearch,
        IReadOnlyList<string>? videoFilesCache)
    {
        if (cdIndexVideoLinksByPhoto is null || cdIndexVideoLinksByPhoto.Count == 0)
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No CDIndex mapping");

        var photos = ExtractPhotoHintsFromPdf(pdfPath);
        if (photos.Count == 0)
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No photo hints in PDF");

        var scoreByLink = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var links = new List<string>();
        foreach (var photo in photos)
        {
            if (!cdIndexVideoLinksByPhoto.TryGetValue(photo, out var keyLinks) || keyLinks.Count == 0)
                continue;

            foreach (var link in keyLinks)
            {
                if (!links.Contains(link, StringComparer.OrdinalIgnoreCase))
                    links.Add(link);
            }

            // Strong signal: this hint maps to exactly one CDIndex video.
            if (keyLinks.Count == 1)
            {
                var key = keyLinks[0];
                scoreByLink.TryGetValue(key, out var current);
                scoreByLink[key] = current + 1;
            }
        }

        if (links.Count == 0)
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No CDIndex match for photo hints");

        if (scoreByLink.Count > 0)
        {
            var ranked = scoreByLink.OrderByDescending(kv => kv.Value).ToList();
            var top = ranked[0];
            var secondScore = ranked.Count > 1 ? ranked[1].Value : -1;
            if (top.Value > secondScore)
            {
                var voted = FindVideo(top.Key, videoSourceFolder, haltung, dateStamp, recursiveVideoSearch, videoFilesCache);
                if (voted.Status == VideoMatchStatus.Matched && voted.VideoPath is not null)
                {
                    return new VideoFindResult(
                        VideoMatchStatus.Matched,
                        voted.VideoPath,
                        Array.Empty<string>(),
                        $"Matched by CDIndex photo hint vote ({top.Value})");
                }
            }
        }

        var matched = new List<string>();
        var ambiguousCandidates = new List<string>();
        foreach (var link in links)
        {
            var fromLink = FindVideo(link, videoSourceFolder, haltung, dateStamp, recursiveVideoSearch, videoFilesCache);
            if (fromLink.Status == VideoMatchStatus.Matched && !string.IsNullOrWhiteSpace(fromLink.VideoPath))
            {
                if (!matched.Contains(fromLink.VideoPath, StringComparer.OrdinalIgnoreCase))
                    matched.Add(fromLink.VideoPath);
                continue;
            }

            if (fromLink.Status == VideoMatchStatus.Ambiguous)
            {
                foreach (var c in fromLink.Candidates)
                {
                    if (!ambiguousCandidates.Contains(c, StringComparer.OrdinalIgnoreCase))
                        ambiguousCandidates.Add(c);
                }
            }
        }

        if (matched.Count == 1)
            return new VideoFindResult(VideoMatchStatus.Matched, matched[0], Array.Empty<string>(), "Matched by CDIndex photo hint");
        if (matched.Count > 1)
            return new VideoFindResult(VideoMatchStatus.Ambiguous, null, matched, "Multiple matches from CDIndex photo hints");
        if (ambiguousCandidates.Count > 0)
            return new VideoFindResult(VideoMatchStatus.Ambiguous, null, ambiguousCandidates, "Ambiguous from CDIndex photo hints");

        return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No usable video from CDIndex photo hints");
    }

    private static string? TryResolveHoldingFromMatchedVideo(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarHoldingsByVideoLink,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarVideoLinksByHolding,
        string? videoPath,
        string parsedHolding)
    {
        if (sidecarHoldingsByVideoLink is null || sidecarHoldingsByVideoLink.Count == 0)
            return null;
        if (string.IsNullOrWhiteSpace(videoPath))
            return null;

        var fileName = NormalizeVideoFileName(Path.GetFileName(videoPath));
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var candidates = new List<string>();
        foreach (var key in EnumerateVideoLookupKeys(fileName))
        {
            if (!sidecarHoldingsByVideoLink.TryGetValue(key, out var holdings))
                continue;

            foreach (var holding in holdings)
            {
                if (!candidates.Contains(holding, StringComparer.OrdinalIgnoreCase))
                    candidates.Add(holding);
            }
        }

        if (candidates.Count != 1)
            return null;

        var candidate = candidates[0];
        if (string.Equals(candidate, parsedHolding, StringComparison.OrdinalIgnoreCase))
            return null;

        // Keep parsed holding only if it already maps to the same matched video.
        if (HoldingHasVideoLink(sidecarVideoLinksByHolding, parsedHolding, fileName))
            return null;
        if (!HoldingHasVideoLink(sidecarVideoLinksByHolding, candidate, fileName))
            return null;

        return candidate;
    }

    private static bool HoldingHasVideoLink(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarVideoLinksByHolding,
        string holding,
        string videoFileName)
    {
        if (sidecarVideoLinksByHolding is null
            || string.IsNullOrWhiteSpace(holding)
            || string.IsNullOrWhiteSpace(videoFileName))
            return false;

        var videoKeys = EnumerateVideoLookupKeys(videoFileName)
            .ToList();

        foreach (var key in EnumerateHoldingLookupKeys(holding))
        {
            if (!sidecarVideoLinksByHolding.TryGetValue(key, out var links))
                continue;

            foreach (var link in links)
            {
                foreach (var linkKey in EnumerateVideoLookupKeys(link))
                {
                    if (videoKeys.Contains(linkKey, StringComparer.OrdinalIgnoreCase))
                        return true;
                }
            }
        }

        return false;
    }

    private static string? GetSuffixFromFirstUnderscore(string fileName)
    {
        var idx = fileName.IndexOf('_');
        if (idx < 0)
            return null;
        return fileName.Substring(idx);
    }
}
