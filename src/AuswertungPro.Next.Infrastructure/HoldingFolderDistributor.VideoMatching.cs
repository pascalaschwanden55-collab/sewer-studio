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
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Import.Common;
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
        if (!ImportSourcePathGuard.TryInspectDirectory(
                root,
                out var safeRoot,
                out var rootExists,
                out _)
            || !rootExists)
        {
            return Array.Empty<string>();
        }

        return Common.SafeFileEnumeration.EnumerateFilesSafe(safeRoot, "*.*", recursive: recursive)
            .Where(MediaFileTypes.HasVideoExtension)
            .ToList();
    }


    internal static VideoFindResult FindVideo(
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


    internal static VideoFindResult FindVideoByHaltungDate(
        string videoSourceFolder,
        string haltung,
        string dateStamp,
        bool recursiveVideoSearch,
        IReadOnlyList<string>? videoFilesCache = null)
    {
        var files = videoFilesCache ?? EnumerateVideoFiles(videoSourceFolder, recursiveVideoSearch);
        // Datei-Zeitstempel als Tiebreaker, falls mehrere datumslose Videos derselben Haltung
        // existieren (im Namen steht kein Datum) - das dem Protokoll-Datum naechstgelegene gewinnt.
        return HoldingVideoMatching.FindVideoByHaltungDate(haltung, dateStamp, files, GetFileTimestampSafe);
    }

    private static System.DateTime? GetFileTimestampSafe(string path)
    {
        try
        {
            return ImportSourcePathGuard.TryInspectFile(
                       path,
                       out var safePath,
                       out var exists,
                       out _)
                   && exists
                ? File.GetLastWriteTime(safePath)
                : null;
        }
        catch { return null; }
    }


    internal static VideoFindResult TryFindVideoFromCdIndexPhotoHints(
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

        var photos = HoldingDistribution.DistributionPdfAssignmentController.ExtractPhotoHints(pdfPath);
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


    internal static string? TryResolveHoldingFromMatchedVideo(
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

    internal static string? TryResolveHoldingFromMatchedVideoName(
        AuswertungPro.Next.Domain.Models.Project? project,
        string? videoPath,
        string parsedHolding)
    {
        if (project is null || string.IsNullOrWhiteSpace(videoPath))
            return null;

        var stem = Path.GetFileNameWithoutExtension(videoPath);
        var candidate = NormalizeHaltungId(HoldingKeyNormalizer.NormalizeIbak(stem));
        if (!IsValidHaltungId(candidate))
            return null;

        if (string.Equals(candidate, parsedHolding, StringComparison.OrdinalIgnoreCase))
            return null;

        // Nur gegen einen bereits vorhandenen Record umbiegen. Ohne diese
        // Eindeutigkeit wuerde der Dateiname allein neue Haltungen erfinden.
        return FindRecordByHolding(project, candidate) is null ? null : candidate;
    }


    private static string? GetSuffixFromFirstUnderscore(string fileName)
        => HoldingDistribution.HoldingKeyUtils.GetSuffixFromFirstUnderscore(fileName);


    internal static AuswertungPro.Next.Domain.Models.HaltungRecord? FindRecordByHolding(
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



    /// <summary>
    /// Entfernt XX. Praefixe (1-2 Ziffern + Punkt) von beiden Seiten eines Haltungsnamens.
    /// Z.B. "07.7695-07.7078" → "7695-7078"
    /// </summary>


    /// <summary>
    /// Entfernt XX. Praefixe (1-2 Ziffern + Punkt) von beiden Seiten eines Haltungsnamens.
    /// Z.B. "07.7695-07.7078" → "7695-7078"
    /// </summary>
    private static string StripNodePrefixes(string holdingKey) => HoldingIdNormalizer.StripNodePrefixes(holdingKey);


    private static IEnumerable<string> EnumerateHoldingLookupKeys(string haltung) => HoldingIdNormalizer.EnumerateHoldingLookupKeys(haltung);


    private static string ReverseHoldingId(string? haltung) => HoldingIdNormalizer.ReverseHoldingId(haltung);


    internal static VideoFindResult TryFindVideoFromRecordLink(
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

        // Vor dem ersten Dateizugriff auch lokal aussehende Links sperren. Ein
        // Verzeichnis-Symlink kann sonst die syntaktische UNC-Pruefung umgehen.
        if (!ImportSourcePathGuard.TryInspectFile(
                link,
                out var safeLink,
                out var linkExists,
                out var linkError))
        {
            return new VideoFindResult(
                VideoMatchStatus.NotFound,
                null,
                Array.Empty<string>(),
                linkError ?? "Unsafe video link rejected");
        }

        if (linkExists)
            return new VideoFindResult(VideoMatchStatus.Matched, safeLink, Array.Empty<string>(), "Matched by existing Link path");

        var linkFile = Path.GetFileName(link);
        if (string.IsNullOrWhiteSpace(linkFile))
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "Link filename missing");

        return FindVideo(linkFile, videoSourceFolder, haltung, dateStamp, recursiveVideoSearch, videoFilesCache);
    }


    /// <summary>
    /// Prueft, ob im Haltungsordner bereits ein bytegleiches Video existiert.
    /// Gleicher Name oder gleiche Groesse allein reichen nicht. Bei abweichendem
    /// Inhalt liefert die Methode null; der Aufrufer erzeugt dann einen eindeutigen Zielnamen.
    /// </summary>
    internal static string? FindExistingVideo(string holdingFolder, string sourceVideoPath)
    {
        if (!ImportSourcePathGuard.TryInspectDirectory(
                holdingFolder,
                out var safeHoldingFolder,
                out var holdingFolderExists,
                out _)
            || !holdingFolderExists
            || !ImportSourcePathGuard.TryInspectFile(
                sourceVideoPath,
                out var safeSourceVideoPath,
                out var sourceExists,
                out _)
            || !sourceExists)
        {
            return null;
        }

        try
        {
            foreach (var candidate in Directory.EnumerateFiles(safeHoldingFolder))
            {
                if (!ImportSourcePathGuard.TryInspectFile(
                        candidate,
                        out var existing,
                        out var existingFileExists,
                        out _)
                    || !existingFileExists)
                {
                    continue;
                }

                if (!MediaFileTypes.HasVideoExtension(existing))
                    continue;

                try
                {
                    if (FileContentComparer.FilesEqual(safeSourceVideoPath, existing))
                        return existing;
                }
                catch (Exception ex)
                {
                    // Eine unlesbare Zieldatei ist kein Identitaetsbeweis. Andere
                    // vorhandene Videos koennen trotzdem noch bytegleich sein.
                    BestEffort.ReportWarning(
                        $"[HoldingDistribution] Vorhandenes Video nicht vergleichbar: {existing}: {ex.Message}");
                }
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
