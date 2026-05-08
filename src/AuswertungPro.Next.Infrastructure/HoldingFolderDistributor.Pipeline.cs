using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// HoldingFolderDistributor — Pipeline-Glue (partial class).
///
/// Refactor 2026-05-08 (Etappe 6, Charge R15): zentrale Per-Result-
/// Distributoren und Lookup-Helfer fuer Project/Record/Holding.
/// Mechanisch — keine Verhaltensaenderung.
/// </summary>
public static partial class HoldingFolderDistributor
{
    private static DistributionResult HandleParsedDistribution(
        ParsedPdf parsed,
        string sourcePdfPath,
        string pdfToStorePath,
        string videoSourceFolder,
        string destGemeindeFolder,
        bool moveInsteadOfCopy,
        bool overwrite,
        bool recursiveVideoSearch,
        string unmatchedFolderName,
        string? pageRange,
        AuswertungPro.Next.Domain.Models.Project? project = null,
        IReadOnlyList<string>? videoFilesCache = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarVideoLinksByHolding = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarHoldingsByVideoLink = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? cdIndexVideoLinksByPhoto = null)
    {
        var parsedHoldingRaw = parsed.Haltung ?? "UNKNOWN";
        var haltungRaw = PdfCorrectionMetadata.ResolveHolding(project, parsedHoldingRaw);
        if (string.IsNullOrWhiteSpace(haltungRaw))
            haltungRaw = parsedHoldingRaw;

        var haltungId = NormalizeHaltungId(haltungRaw);
        var haltung = SanitizePathSegment(haltungId);
        var originalHolding = SanitizePathSegment(NormalizeHaltungId(parsedHoldingRaw));
        if (parsed.Date is null)
            return new DistributionResult(false, "Date not found", sourcePdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked);

        var date = parsed.Date.Value;
        var dateStamp = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var pdfReplacements = BuildRenameReplacements(parsedHoldingRaw, haltungRaw);
        var correctionResult = TryCorrectPdfTextLayer(pdfToStorePath, pdfReplacements);
        var pdfSourceToStorePath = correctionResult.Corrected ? correctionResult.OutputPdfPath : pdfToStorePath;
        var removeOriginalAfterStore = moveInsteadOfCopy
            && correctionResult.Corrected
            && !string.Equals(pdfToStorePath, pdfSourceToStorePath, StringComparison.OrdinalIgnoreCase);

        // Suche Standard-Video
        VideoFindResult videoFind = string.IsNullOrWhiteSpace(parsed.VideoFile)
            ? FindVideoByHaltungDate(videoSourceFolder, haltung, dateStamp, recursiveVideoSearch, videoFilesCache)
            : FindVideo(parsed.VideoFile!, videoSourceFolder, haltung, dateStamp, recursiveVideoSearch, videoFilesCache);

        // MatchedWithoutDate (Strategy 3 in FindVideoByHaltungDate, IBAK-Exporte ohne
        // Datum im Dateinamen) wird gleich behandelt wie Matched - sonst werden korrekt
        // gefundene Videos nicht kopiert.
        static bool IsVideoHit(VideoMatchStatus s)
            => s == VideoMatchStatus.Matched || s == VideoMatchStatus.MatchedWithoutDate;

        if (!IsVideoHit(videoFind.Status)
            && !string.Equals(originalHolding, haltung, StringComparison.OrdinalIgnoreCase))
        {
            var fallback = string.IsNullOrWhiteSpace(parsed.VideoFile)
                ? FindVideoByHaltungDate(videoSourceFolder, originalHolding, dateStamp, recursiveVideoSearch, videoFilesCache)
                : FindVideo(parsed.VideoFile!, videoSourceFolder, originalHolding, dateStamp, recursiveVideoSearch, videoFilesCache);

            if (IsVideoHit(fallback.Status)
                || (videoFind.Status == VideoMatchStatus.NotFound && fallback.Status == VideoMatchStatus.Ambiguous))
                videoFind = fallback;
        }

        // Conservative fallback: use imported Link (e.g. HI116 from M150/MDB) when
        // normal matching did not produce a unique hit (NotFound/Ambiguous).
        // This keeps primary behavior but allows M150 mapping to disambiguate.
        if (!IsVideoHit(videoFind.Status))
        {
            var fromLink = TryFindVideoFromRecordLink(project, haltung, videoSourceFolder, dateStamp, recursiveVideoSearch, videoFilesCache);
            if (IsVideoHit(fromLink.Status))
                videoFind = fromLink;
        }

        // Last-resort fallback for projects where videos are not named by holding, but M150/MDB carries the mapping.
        if (!IsVideoHit(videoFind.Status))
        {
            var fromSidecar = TryFindVideoFromSidecarLinks(sidecarVideoLinksByHolding, haltung, videoSourceFolder, dateStamp, recursiveVideoSearch, videoFilesCache);
            if (IsVideoHit(fromSidecar.Status)
                || (videoFind.Status == VideoMatchStatus.NotFound && fromSidecar.Status == VideoMatchStatus.Ambiguous))
                videoFind = fromSidecar;
        }

        // Last-resort fallback for WinCAN exports without MDB:
        // map photo filenames found in PDF pages via CDIndex.txt to video filenames.
        if (!IsVideoHit(videoFind.Status))
        {
            var fromCdIndex = TryFindVideoFromCdIndexPhotoHints(
                cdIndexVideoLinksByPhoto,
                pdfToStorePath,
                haltung,
                videoSourceFolder,
                dateStamp,
                recursiveVideoSearch,
                videoFilesCache);
            if (IsVideoHit(fromCdIndex.Status)
                || (videoFind.Status == VideoMatchStatus.NotFound && fromCdIndex.Status == VideoMatchStatus.Ambiguous))
                videoFind = fromCdIndex;
        }

        // Letzte Rettung: Haltungsname aus dem PDF-Dateinamen ableiten (KIAS/IBAK
        // Konvention "H_<haltung>.pdf"/"L_<haltung>.pdf"). Notwendig fuer
        //  - Multi-Anschluss-L-PDFs (Parser zieht teils Gegenrichtung oder Folge-Haltung)
        //  - Knoten-Prefix-Bugs im Parser ("7.34854-36262" -> "34854-36262")
        // Greift nur wenn die Standardsuche kein Match brachte.
        if (!IsVideoHit(videoFind.Status))
        {
            var holdingFromName = HoldingFromKiasFilename(sourcePdfPath);
            if (!string.IsNullOrWhiteSpace(holdingFromName)
                && !string.Equals(holdingFromName, haltung, StringComparison.OrdinalIgnoreCase))
            {
                var fromName = FindVideoByHaltungDate(videoSourceFolder, holdingFromName, dateStamp, recursiveVideoSearch, videoFilesCache);
                if (IsVideoHit(fromName.Status))
                {
                    videoFind = fromName;
                    // Haltung an den Dateinamen ausrichten - der Zielordner soll dann auch
                    // unter dem Dateiname-Haltungsnamen liegen, sonst landen Splits in
                    // einem nicht-existierenden "Pseudo"-Ordner.
                    haltungRaw = holdingFromName;
                    haltung = SanitizePathSegment(NormalizeHaltungId(holdingFromName));
                }
            }
        }

        var holdingLabelAdjusted = false;
        if (IsVideoHit(videoFind.Status) && videoFind.VideoPath is not null)
        {
            var mappedHolding = TryResolveHoldingFromMatchedVideo(
                sidecarHoldingsByVideoLink,
                sidecarVideoLinksByHolding,
                videoFind.VideoPath,
                haltung);
            if (!string.IsNullOrWhiteSpace(mappedHolding)
                && !string.Equals(mappedHolding, haltung, StringComparison.OrdinalIgnoreCase))
            {
                haltungRaw = mappedHolding;
                haltung = SanitizePathSegment(NormalizeHaltungId(mappedHolding));
                holdingLabelAdjusted = true;
            }
        }
        try
        {
            var holdingFolder = Path.Combine(destGemeindeFolder, haltung);
            Directory.CreateDirectory(holdingFolder);

            var destPdfName = $"{dateStamp}_{haltung}.pdf";
            var destPdfPath = EnsureUniquePath(Path.Combine(holdingFolder, destPdfName), overwrite);
            MoveOrCopy(pdfSourceToStorePath, destPdfPath, moveInsteadOfCopy);

            if (removeOriginalAfterStore
                && File.Exists(pdfToStorePath)
                && !string.Equals(pdfToStorePath, pdfSourceToStorePath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(pdfToStorePath);
                }
                catch
                {
                    // Best-effort cleanup for move semantics.
                }
            }

            // Suche Gegeninspektions-Video. Zwei Konventionen:
            //   - Altes Schema "<haltung>g.ext" (suffix-g)
            //   - KIAS/IBAK 2026 "<haltung>~G.ext"
            // Erst alt, dann KIAS-Variante als Fallback.
            VideoFindResult videoFindG = FindVideoByHaltungDate(videoSourceFolder, haltung + "g", dateStamp, recursiveVideoSearch, videoFilesCache);
            if (!IsVideoHit(videoFindG.Status))
            {
                var filesForG = videoFilesCache ?? EnumerateVideoFiles(videoSourceFolder, recursiveVideoSearch);
                var gKias = HoldingVideoMatching.FindGegenrichtungVideo(haltung, filesForG);
                // Vermeide Doppel-Kopie: wenn Gegen-Video identisch zum Standard-Video ist, ueberspringen.
                if (IsVideoHit(gKias.Status)
                    && gKias.VideoPath is not null
                    && !string.Equals(gKias.VideoPath, videoFind.VideoPath, StringComparison.OrdinalIgnoreCase))
                    videoFindG = gKias;
            }

            string? destVideoPath = null;
            string? destVideoPathG = null;
            string? infoPath = null;
            var videoPaths = new List<string>();

            // Standard-Video kopieren (Duplikat-Check: gleiche Dateigroesse = bereits vorhanden)
            // MatchedWithoutDate (Strategy 3) wird auch akzeptiert - sonst werden IBAK-Videos
            // nicht kopiert, weil deren Dateinamen kein Datum enthalten.
            if (IsVideoHit(videoFind.Status) && videoFind.VideoPath is not null)
            {
                var videoExt = Path.GetExtension(videoFind.VideoPath);
                var destVideoName = $"{dateStamp}_{haltung}{videoExt}";
                destVideoPath = Path.Combine(holdingFolder, destVideoName);
                var existingVideo = FindExistingVideo(holdingFolder, videoFind.VideoPath);
                if (existingVideo is not null)
                {
                    destVideoPath = existingVideo;
                }
                else
                {
                    destVideoPath = EnsureUniquePath(destVideoPath, overwrite);
                    MoveOrCopy(videoFind.VideoPath, destVideoPath, moveInsteadOfCopy);
                }
                videoPaths.Add(destVideoPath);

                // Automatisch Link im HaltungRecord setzen, falls Project übergeben
                if (project != null && !string.IsNullOrWhiteSpace(destVideoPath))
                {
                    var record = FindRecordByHolding(project, haltung);
                    if (record != null)
                    {
                        var meta = record.FieldMeta.TryGetValue("Link", out var m) ? m : null;
                        if (meta == null || !meta.UserEdited)
                        {
                            record.SetFieldValue("Link", destVideoPath, FieldSource.Unknown, userEdited: false);
                            project.ModifiedAtUtc = DateTime.UtcNow;
                            project.Dirty = true;
                        }
                    }
                }
            }

            // Gegeninspektions-Video kopieren (falls vorhanden und nicht identisch zum Standard-Video)
            if (IsVideoHit(videoFindG.Status) && videoFindG.VideoPath is not null && !string.Equals(videoFindG.VideoPath, videoFind.VideoPath, StringComparison.OrdinalIgnoreCase))
            {
                var existingVideoG = FindExistingVideo(holdingFolder, videoFindG.VideoPath);
                if (existingVideoG is not null)
                {
                    destVideoPathG = existingVideoG;
                }
                else
                {
                    var videoExtG = Path.GetExtension(videoFindG.VideoPath);
                    var destVideoNameG = $"{dateStamp}_{haltung}-g{videoExtG}";
                    destVideoPathG = EnsureUniquePath(Path.Combine(holdingFolder, destVideoNameG), overwrite);
                    MoveOrCopy(videoFindG.VideoPath, destVideoPathG, moveInsteadOfCopy);
                }
                videoPaths.Add(destVideoPathG);
            }

            // Fehlerbehandlung wie bisher
            if (videoPaths.Count == 0)
            {
                if (videoFind.Status == VideoMatchStatus.NotFound && videoFindG.Status == VideoMatchStatus.NotFound)
                {
                    var infoName = $"{dateStamp}_{haltung}_VIDEO_MISSING.txt";
                    infoPath = EnsureUniquePath(Path.Combine(holdingFolder, infoName), overwrite);
                    var filmName = string.IsNullOrWhiteSpace(parsed.VideoFile) ? "<nicht gefunden>" : parsed.VideoFile!;
                    File.WriteAllText(infoPath, BuildMissingInfo(sourcePdfPath, filmName, date, haltungRaw));
                }
                else if (videoFind.Status == VideoMatchStatus.Ambiguous || videoFindG.Status == VideoMatchStatus.Ambiguous)
                {
                    var infoName = $"{dateStamp}_{haltung}_VIDEO_AMBIGUOUS.txt";
                    infoPath = EnsureUniquePath(Path.Combine(holdingFolder, infoName), overwrite);
                    var candidates = videoFind.Status == VideoMatchStatus.Ambiguous ? videoFind.Candidates : videoFindG.Candidates;
                    File.WriteAllText(infoPath, BuildAmbiguousInfo(sourcePdfPath, parsed.VideoFile!, date, haltungRaw, candidates));
                    var unmatchedFolder = Path.Combine(destGemeindeFolder, unmatchedFolderName, haltung);
                    Directory.CreateDirectory(unmatchedFolder);
                    CopyCandidatesToUnmatched(unmatchedFolder, dateStamp, haltung, candidates);
                }
            }

            var message = videoPaths.Count switch
            {
                2 => "OK (Standard+Gegeninspektion)",
                1 => "OK (1 Video)",
                0 when videoFind.Status == VideoMatchStatus.Ambiguous || videoFindG.Status == VideoMatchStatus.Ambiguous => "Video ambiguous",
                0 => "Video missing",
                _ => "OK"
            };

            // Warnung anhaengen, wenn Video nur ueber Haltungsname gefunden wurde
            // (kein Datum im Dateinamen, typisch fuer IBAK-Exporte).
            if (videoPaths.Count > 0
                && (videoFind.Status == VideoMatchStatus.MatchedWithoutDate
                    || videoFindG.Status == VideoMatchStatus.MatchedWithoutDate))
                message += " [Warnung: Video ohne Datumsabgleich]";

            if (!string.IsNullOrWhiteSpace(parsed.Message))
                message += $" / Parser: {parsed.Message}";

            if (holdingLabelAdjusted)
                message += " [Haltung korrigiert via M150/MDB]";

            if (videoFind.Status == VideoMatchStatus.Matched && !string.IsNullOrWhiteSpace(videoFind.Message))
            {
                if (videoFind.Message.Contains("M150/MDB sidecar", StringComparison.OrdinalIgnoreCase))
                    message += " [Quelle: M150/MDB]";
                else if (videoFind.Message.Contains("existing Link path", StringComparison.OrdinalIgnoreCase))
                    message += " [Quelle: Datensatz-Link]";
                else if (videoFind.Message.Contains("CDIndex", StringComparison.OrdinalIgnoreCase))
                    message += " [Quelle: CDIndex-Foto]";
            }

            if (correctionResult.Corrected)
                message += $" [PDF korrigiert: {correctionResult.MatchCount} Treffer auf {correctionResult.PageCount} Seiten]";
            else if (pdfReplacements.Count > 0 && !string.IsNullOrWhiteSpace(correctionResult.Message))
                message += $" [PDF-Korrektur: {correctionResult.Message}]";

            if (!string.IsNullOrWhiteSpace(pageRange))
                message = $"Split Seiten {pageRange} - {message}";

            return new DistributionResult(
                true,
                message,
                sourcePdfPath,
                videoFind.VideoPath,
                destPdfPath,
                destVideoPath,
                infoPath,
                holdingFolder,
                videoFind.Status,
                PdfCorrected: correctionResult.Corrected,
                PdfCorrectionMessage: correctionResult.Message);
        }
        finally
        {
            if (!moveInsteadOfCopy
                && correctionResult.Corrected
                && !string.Equals(correctionResult.OutputPdfPath, pdfToStorePath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (File.Exists(correctionResult.OutputPdfPath))
                        File.Delete(correctionResult.OutputPdfPath);
                }
                catch
                {
                    // Best-effort cleanup.
                }
            }
        }
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

    private static DistributionResult HandleParsedShaftDistribution(
        ParsedShaftPdf parsed,
        string sourcePdfPath,
        string pdfToStorePath,
        string destGemeindeFolder,
        bool moveInsteadOfCopy,
        bool overwrite,
        string? pageRange,
        Dictionary<string, string> shaftOutputPathByKey,
        Project? project = null)
    {
        if (string.IsNullOrWhiteSpace(parsed.ShaftNumber))
            return new DistributionResult(false, "Schachtnummer nicht gefunden", sourcePdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked);
        if (parsed.Date is null)
            return new DistributionResult(false, "Datum nicht gefunden", sourcePdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked);

        var parsedShaftRaw = parsed.ShaftNumber.Trim();
        var shaftRaw = PdfCorrectionMetadata.ResolveShaft(project, parsedShaftRaw);
        if (string.IsNullOrWhiteSpace(shaftRaw))
            shaftRaw = parsedShaftRaw;

        var shaft = SanitizePathSegment(shaftRaw);
        var dateStamp = parsed.Date.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var pdfReplacements = BuildRenameReplacements(parsedShaftRaw, shaftRaw);
        var correctionResult = TryCorrectPdfTextLayer(pdfToStorePath, pdfReplacements);
        var pdfSourceToStorePath = correctionResult.Corrected ? correctionResult.OutputPdfPath : pdfToStorePath;
        var removeOriginalAfterStore = moveInsteadOfCopy
            && correctionResult.Corrected
            && !string.Equals(pdfToStorePath, pdfSourceToStorePath, StringComparison.OrdinalIgnoreCase);

        try
        {
            var shaftFolder = Path.Combine(destGemeindeFolder, shaft);
            Directory.CreateDirectory(shaftFolder);

            var destPdfName = $"{dateStamp}_{shaft}.pdf";
            var shaftKey = $"{dateStamp}|{shaft}";
            string destPdfPath;
            var appendedToExisting = false;

            if (shaftOutputPathByKey.TryGetValue(shaftKey, out var existingPath)
                && !string.IsNullOrWhiteSpace(existingPath)
                && File.Exists(existingPath))
            {
                try
                {
                    AppendPdfFile(existingPath, pdfSourceToStorePath, moveInsteadOfCopy);
                    destPdfPath = existingPath;
                    appendedToExisting = true;
                }
                catch (Exception ex)
                {
                    return new DistributionResult(
                        false,
                        $"Konnte PDF nicht zusammenführen: {ex.Message}",
                        sourcePdfPath,
                        null,
                        null,
                        null,
                        null,
                        shaftFolder,
                        VideoMatchStatus.NotChecked);
                }
            }
            else
            {
                destPdfPath = EnsureUniquePath(Path.Combine(shaftFolder, destPdfName), overwrite);
                MoveOrCopy(pdfSourceToStorePath, destPdfPath, moveInsteadOfCopy);
                shaftOutputPathByKey[shaftKey] = destPdfPath;
            }

            if (removeOriginalAfterStore
                && File.Exists(pdfToStorePath)
                && !string.Equals(pdfToStorePath, pdfSourceToStorePath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(pdfToStorePath);
                }
                catch
                {
                    // Best-effort cleanup for move semantics.
                }
            }

            var message = "OK (Schachtprotokoll)";
            if (appendedToExisting)
                message += " + Seite angehängt";
            if (correctionResult.Corrected)
                message += $" [PDF korrigiert: {correctionResult.MatchCount} Treffer auf {correctionResult.PageCount} Seiten]";
            else if (pdfReplacements.Count > 0 && !string.IsNullOrWhiteSpace(correctionResult.Message))
                message += $" [PDF-Korrektur: {correctionResult.Message}]";
            if (!string.IsNullOrWhiteSpace(pageRange))
                message = $"Split Seiten {pageRange} - {message}";

            return new DistributionResult(
                true,
                message,
                sourcePdfPath,
                null,
                destPdfPath,
                null,
                null,
                shaftFolder,
                VideoMatchStatus.NotChecked,
                PdfCorrected: correctionResult.Corrected,
                PdfCorrectionMessage: correctionResult.Message);
        }
        finally
        {
            if (!moveInsteadOfCopy
                && correctionResult.Corrected
                && !string.Equals(correctionResult.OutputPdfPath, pdfToStorePath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (File.Exists(correctionResult.OutputPdfPath))
                        File.Delete(correctionResult.OutputPdfPath);
                }
                catch
                {
                    // Best-effort cleanup.
                }
            }
        }
    }

    private static string? TryMatchPdfToHolding(
        string pdfPath,
        IReadOnlyDictionary<string, string> distributedHoldings)
    {
        if (distributedHoldings.Count == 0)
            return null;

        var fileName = Path.GetFileNameWithoutExtension(pdfPath) ?? "";

        // 1) Versuche Haltungsnummer aus dem Dateinamen zu extrahieren
        var pairRx = new Regex(@"((?:\d{2,}\.\d{2,}|\d{4,})\s*[-]\s*(?:\d{2,}\.\d{2,}|\d{4,}))");
        var match = pairRx.Match(fileName);
        if (match.Success)
        {
            var extracted = NormalizeHaltungId(match.Groups[1].Value);
            if (distributedHoldings.TryGetValue(extracted, out var folder))
                return folder;

            // Prefix-tolerant: z.B. Dateiname hat 7695-7078, Index hat 07.7695-07.7078
            var stripped = StripNodePrefixes(extracted);
            foreach (var kvp in distributedHoldings)
            {
                if (string.Equals(StripNodePrefixes(kvp.Key), stripped, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
        }

        // 2) Fallback: Pruefe ob der Dateiname den Ordnernamen einer verteilten Haltung enthaelt
        foreach (var kvp in distributedHoldings)
        {
            var holdingDirName = Path.GetFileName(kvp.Value) ?? "";
            if (!string.IsNullOrWhiteSpace(holdingDirName)
                && fileName.Contains(holdingDirName, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;
        }

        return null;
    }
}
