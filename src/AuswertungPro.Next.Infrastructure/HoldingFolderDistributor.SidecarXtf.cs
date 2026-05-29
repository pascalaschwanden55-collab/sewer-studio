using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure;

// Sidecar-/XTF-/TXT-Hilfsindizes fuer Videolinks und Importquellen.
// Teil derselben partial-Klasse - reine mechanische Auslagerung (kein Verhaltenswechsel).
public static partial class HoldingFolderDistributor
{
    private static readonly Regex KinsTxtHeaderRegex = new(
        @"^\s*(?<usage>\S+)\s+(?<from>[0-9.]+)\s*->\s*(?<to>[0-9.]+).*?@Datei=(?<video>[^\s]+)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex KinsTxtDateRegex = new(
        @"(?<d>\d{2}\.\d{2}\.\d{2,4})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly object XtfCacheSync = new();

    private static readonly Dictionary<string, string[]> XtfFilesCache =
        new(StringComparer.OrdinalIgnoreCase);


    private static IReadOnlyList<KinsTxtSection> ParseTxtSections(string txtPath)
    {
        var lines = ReadAllTextLinesBestEffort(txtPath);
        var sections = new List<KinsTxtSection>();
        var defaultDate = TryReadTxtDate(txtPath) ?? File.GetLastWriteTime(txtPath);

        string? currentHolding = null;
        string? currentVideo = null;
        var currentLines = new List<string>();

        void FlushCurrent()
        {
            if (string.IsNullOrWhiteSpace(currentHolding))
                return;

            var content = string.Join(Environment.NewLine, currentLines);
            sections.Add(new KinsTxtSection(
                SourceTxtPath: txtPath,
                HoldingRaw: currentHolding,
                VideoFileName: currentVideo ?? string.Empty,
                Date: defaultDate,
                SectionText: content));

            currentHolding = null;
            currentVideo = null;
            currentLines = new List<string>();
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine ?? string.Empty;
            if (TryParseTxtHeader(line, out var haltung, out var videoFile))
            {
                FlushCurrent();
                currentHolding = haltung;
                currentVideo = videoFile;
                currentLines.Add(line.TrimEnd());
                continue;
            }

            if (!string.IsNullOrWhiteSpace(currentHolding))
                currentLines.Add(line.TrimEnd());
        }

        FlushCurrent();
        return sections;
    }


    private static bool TryParseTxtHeader(string line, out string haltung, out string videoFile)
    {
        haltung = string.Empty;
        videoFile = string.Empty;
        var match = KinsTxtHeaderRegex.Match(line ?? string.Empty);
        if (!match.Success)
            return false;

        var from = match.Groups["from"].Value.Trim();
        var to = match.Groups["to"].Value.Trim();
        var video = match.Groups["video"].Value.Trim();
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return false;

        haltung = $"{from}-{to}";
        videoFile = video;
        return true;
    }


    private static DateTime? TryReadTxtDate(string txtPath)
    {
        var dir = Path.GetDirectoryName(txtPath);
        if (string.IsNullOrWhiteSpace(dir))
            return null;

        var current = new DirectoryInfo(dir);
        for (var depth = 0; current is not null && depth < 6; depth++, current = current.Parent)
        {
            var infoPath = Path.Combine(current.FullName, "kiDVinfo.txt");
            if (!File.Exists(infoPath))
                continue;

            var parsed = TryParseDateFromInfoFile(infoPath);
            if (parsed.HasValue)
                return parsed;
        }

        return null;
    }


    private static DateTime? TryParseDateFromInfoFile(string infoPath)
    {
        foreach (var line in ReadAllTextLinesBestEffort(infoPath))
        {
            var match = KinsTxtDateRegex.Match(line ?? string.Empty);
            if (!match.Success)
                continue;

            var raw = match.Groups["d"].Value;
            if (DateTime.TryParseExact(raw, new[] { "dd.MM.yy", "dd.MM.yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed;
        }

        return null;
    }


    private static IReadOnlyList<string> ReadAllTextLinesBestEffort(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            return File.ReadAllLines(path, Encoding.GetEncoding(1252));
        }
        catch
        {
            return File.ReadAllLines(path);
        }
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
                    || name.Equals("Sch\u00E4chte", StringComparison.OrdinalIgnoreCase))
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


    private static void PreWarmXtfCache(IReadOnlyList<PageInfo> pages)
    {
        var sourcePath = pages.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.SourcePath))?.SourcePath;
        if (string.IsNullOrWhiteSpace(sourcePath))
            return;

        var dir = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return;

        lock (XtfCacheSync)
        {
            if (!XtfFilesCache.ContainsKey(dir))
            {
                XtfFilesCache[dir] = Directory.GetFiles(dir, "*.xtf", SearchOption.AllDirectories);
            }
        }
    }
}
