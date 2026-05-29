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

// Video-Matching und Videolink-Aufloesung.
// Teil derselben partial-Klasse - reine mechanische Auslagerung (kein Verhaltenswechsel).
public static partial class HoldingFolderDistributor
{
    private static readonly string VideoExtensionPattern =
        string.Join("|", MediaFileTypes.VideoExtensions.Select(ext => Regex.Escape(ext.TrimStart('.'))));

    private static readonly Regex FilmNameRegex = new(
        $@"Film(?:name|datei)?\s*[:\-]?\s*([A-Za-z0-9_\-\. ]+?\.(?:{VideoExtensionPattern}))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);


    private static IReadOnlyList<string> EnumerateVideoFiles(string root, bool recursive)
    {
        if (!Directory.Exists(root))
            return Array.Empty<string>();

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(root, "*.*", searchOption)
            .Where(MediaFileTypes.HasVideoExtension)
            .ToList();
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


    private static string? GetSuffixFromFirstUnderscore(string fileName)
    {
        var idx = fileName.IndexOf('_');
        if (idx < 0)
            return null;
        return fileName.Substring(idx);
    }


    private static AuswertungPro.Next.Domain.Models.HaltungRecord? FindRecordByHolding(
        AuswertungPro.Next.Domain.Models.Project? project,
        string haltung)
    {
        if (project is null || string.IsNullOrWhiteSpace(haltung))
            return null;

        var keys = EnumerateHoldingLookupKeys(haltung)
            .Select(SanitizePathSegment)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 1) Exakte Suche (normalisiert)
        var exact = project.Data.FirstOrDefault(x =>
        {
            var recKey = SanitizePathSegment(NormalizeHaltungId(x.GetFieldValue("Haltungsname")?.Trim() ?? ""));
            return keys.Contains(recKey, StringComparer.OrdinalIgnoreCase);
        });
        if (exact is not null)
            return exact;

        // 2) Knoten-Prefix-tolerant (z.B. 07.7695-07.7078 == 7695-7078)
        var strippedKeys = keys
            .Select(StripNodePrefixes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return project.Data.FirstOrDefault(x =>
        {
            var recKey = SanitizePathSegment(NormalizeHaltungId(x.GetFieldValue("Haltungsname")?.Trim() ?? ""));
            var recStripped = StripNodePrefixes(recKey);
            return strippedKeys.Contains(recStripped, StringComparer.OrdinalIgnoreCase);
        });
    }


    private static readonly Regex NodePrefixRegex = new(@"^\d{1,2}\.", RegexOptions.Compiled);

    /// <summary>
    /// Entfernt XX. Praefixe (1-2 Ziffern + Punkt) von beiden Seiten eines Haltungsnamens.
    /// Z.B. "07.7695-07.7078" → "7695-7078"
    /// </summary>


    /// <summary>
    /// Entfernt XX. Praefixe (1-2 Ziffern + Punkt) von beiden Seiten eines Haltungsnamens.
    /// Z.B. "07.7695-07.7078" → "7695-7078"
    /// </summary>
    private static string StripNodePrefixes(string holdingKey)
    {
        var dashIdx = holdingKey.IndexOf('-');
        if (dashIdx < 0)
            return NodePrefixRegex.Replace(holdingKey, "");

        var left = holdingKey[..dashIdx];
        var right = holdingKey[(dashIdx + 1)..];
        left = NodePrefixRegex.Replace(left, "");
        right = NodePrefixRegex.Replace(right, "");
        return $"{left}-{right}";
    }


    private static IEnumerable<string> EnumerateHoldingLookupKeys(string haltung)
    {
        var normalized = NormalizeHaltungId(haltung);
        if (!string.IsNullOrWhiteSpace(normalized))
            yield return normalized;

        var reversed = ReverseHoldingId(normalized);
        if (!string.IsNullOrWhiteSpace(reversed)
            && !string.Equals(reversed, normalized, StringComparison.OrdinalIgnoreCase))
            yield return reversed;
    }


    private static string ReverseHoldingId(string? haltung)
    {
        if (string.IsNullOrWhiteSpace(haltung))
            return string.Empty;

        var parts = haltung.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return string.Empty;

        return $"{parts[1]}-{parts[0]}";
    }


    private static VideoFindResult TryFindVideoFromRecordLink(
        AuswertungPro.Next.Domain.Models.Project? project,
        string haltung,
        string videoSourceFolder,
        string dateStamp,
        bool recursiveVideoSearch,
        IReadOnlyList<string>? videoFilesCache)
    {
        var record = FindRecordByHolding(project, haltung);
        if (record is null)
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No matching record");

        if (record.FieldMeta.TryGetValue("Link", out var linkMeta))
        {
            var src = linkMeta.Source;
            // M150/MDB-Importe verwenden FieldSource.Xtf, INTERLIS FieldSource.Ili.
            // Alle strukturierten Import-Quellen sind vertrauenswuerdig fuer Video-Links.
            var isStructuredImport = src == AuswertungPro.Next.Domain.Models.FieldSource.Xtf
                                     || src == AuswertungPro.Next.Domain.Models.FieldSource.Xtf405
                                     || src == AuswertungPro.Next.Domain.Models.FieldSource.Ili;
            if (!isStructuredImport)
                return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "Link source is not XTF/M150/MDB/ILI");
        }

        var link = (record.GetFieldValue("Link") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(link) || !HasVideoExtension(link))
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No usable video link");

        if (File.Exists(link))
            return new VideoFindResult(VideoMatchStatus.Matched, link, Array.Empty<string>(), "Matched by existing Link path");

        var linkFile = Path.GetFileName(link);
        if (string.IsNullOrWhiteSpace(linkFile))
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "Link filename missing");

        return FindVideo(linkFile, videoSourceFolder, haltung, dateStamp, recursiveVideoSearch, videoFilesCache);
    }


    /// <summary>
    /// Prueft ob im Haltungsordner bereits ein Video mit gleicher Dateigroesse existiert.
    /// Gibt den Pfad zurueck wenn ja, sonst null.
    /// Verhindert Duplikate beim erneuten Verteilen.
    /// </summary>
    private static string? FindExistingVideo(string holdingFolder, string sourceVideoPath)
    {
        if (!Directory.Exists(holdingFolder) || !File.Exists(sourceVideoPath))
            return null;

        var srcInfo = new FileInfo(sourceVideoPath);
        var srcName = Path.GetFileName(sourceVideoPath);

        try
        {
            foreach (var existing in Directory.EnumerateFiles(holdingFolder))
            {
                if (!MediaFileTypes.HasVideoExtension(existing))
                    continue;

                // Exakter Dateiname-Match
                if (string.Equals(Path.GetFileName(existing), srcName, StringComparison.OrdinalIgnoreCase))
                    return existing;

                // Gleiche Dateigroesse = selbes Video (anderer Name)
                var existInfo = new FileInfo(existing);
                if (existInfo.Length == srcInfo.Length && existInfo.Length > 0)
                    return existing;
            }
        }
        catch
        {
            // Ordner nicht lesbar
        }

        return null;
    }

    /// <summary>
    /// Versucht, ein nicht-parsbares PDF (z.B. Dichtheitspruefungsprotokoll) anhand
    /// seines Dateinamens einem bereits verteilten Haltungsordner zuzuordnen.
    /// Sucht nach Haltungsnummern im Dateinamen und vergleicht mit dem Index.
    /// </summary>
}
