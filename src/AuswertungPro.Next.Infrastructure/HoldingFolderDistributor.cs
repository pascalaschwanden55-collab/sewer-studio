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

/// <summary>
/// Hoch-Level-Orchestrator fuer das Verteilen von Inspektions-Sidecar-Dateien
/// (PDF, TXT, Video, XTF) in Haltungs-Ordner.
///
/// Audit-Fix 2026-04: Die Klasse war 4616 Zeilen mit 24 public + 146 private Methoden.
/// Ueber 'partial class' jetzt physisch auf mehrere Dateien verteilt:
///   - HoldingFolderDistributor.cs              -> Public API + Distribute/DistributeCore
///   - HoldingFolderDistributor.DateParsing.cs  -> Date-Regex + Parse-Helpers
///
/// Verhalten unveraendert. Tests laufen ohne Anpassung.
/// </summary>
public static partial class HoldingFolderDistributor
{
    // SchachtDateIndexSync + SchachtDateIndexCache ausgegliedert nach
    // HoldingFolderDistributor.SchachtPdfParsing.cs
    // (Refactor 2026-05-07, Charge R7).

    // Regex-Patterns ausgegliedert nach HoldingFolderDistributor.Regex.cs
    // (Refactor 2026-05-07, Charge R1).

    private static readonly object XtfCacheSync = new();
    private static readonly Dictionary<string, string[]> XtfFilesCache =
        new(StringComparer.OrdinalIgnoreCase);
    // Public types ausgegliedert nach HoldingFolderDistributor.Types.cs
    // (Refactor 2026-05-07, Charge R2).

    private sealed record KinsTxtSection(
        string SourceTxtPath,
        string HoldingRaw,
        string VideoFileName,
        DateTime Date,
        string SectionText);

    private sealed record PdfTextReplacement(string SearchText, string ReplacementText);
    private sealed record PdfTextReplacementMatch(
        PdfTextReplacement Replacement,
        int StartLetterIndex,
        int EndLetterIndex,
        double Left,
        double Bottom,
        double Right,
        double Top,
        PdfPoint StartBaseLine,
        double FontSize);
    private sealed record PdfCorrectionResult(
        bool Success,
        bool Corrected,
        string OutputPdfPath,
        int MatchCount,
        int PageCount,
        string Message);

    // Distribute, DistributeFiles, DistributeTxt, DistributeTxtFiles,
    // DistributeTxtCore, DistributeCore ausgegliedert nach
    // HoldingFolderDistributor.Distribute.cs
    // (Refactor 2026-05-07, Charge R9).



    // I/O-Helpers (CopyCandidatesToUnmatched, BuildMissingInfo,
    // BuildAmbiguousInfo, MoveOrCopy) ausgegliedert nach
    // HoldingFolderDistributor.IO.cs (Refactor 2026-05-07, Charge R3).

    // TXT-Parsing-Methoden wurden 2026-05-07 in HoldingFolderDistributor.TxtParsing.cs ausgelagert.

    // EnumerateVideoFiles, EnumerateSidecarFiles, BuildSidecarVideoLinkIndex,
    // BuildSidecarHoldingByVideoIndex, EnumerateVideoLookupKeys,
    // BuildCdIndexVideoLinkIndex, ResolveCdIndexFolders, AddCdIndexMappings,
    // ResolveSidecarFolders, FindVideo, FindVideoByHaltungDate,
    // TryFindVideoFromSidecarLinks, TryFindVideoFromCdIndexPhotoHints,
    // TryResolveHoldingFromMatchedVideo, HoldingHasVideoLink,
    // GetSuffixFromFirstUnderscore ausgegliedert nach
    // HoldingFolderDistributor.VideoMatching.cs
    // (Refactor 2026-05-07, Charge R8).

    // PageInfo, PdfPageChunk, ReadPdfPages, ReadPdfPagesWithPdfPig,
    // ReadPdfText, NormalizeText ausgegliedert nach
    // HoldingFolderDistributor.PdfReading.cs
    // (Refactor 2026-05-08, Charge R13).

    // ParsedPdf / ParsedShaftPdf ausgegliedert nach HoldingFolderDistributor.Types.cs
    // (Refactor 2026-05-07, Charge R2).

    // TryParseDateString ausgegliedert nach HoldingFolderDistributor.DateParsing.cs
    // (Refactor 2026-05-07, Charge R5).

    private sealed record PdfShaftChunk(IReadOnlyList<int> Pages, ParsedShaftPdf Parsed);

    // ParseSchachtPdf + ParseSchachtPdfPage ausgegliedert nach
    // HoldingFolderDistributor.SchachtPdfParsing.cs
    // (Refactor 2026-05-07, Charge R7).


    // DistributeShafts, DistributeShaftFiles, DistributeShaftCore,
    // ExpandSelectedShaftPdfFiles ausgegliedert nach
    // HoldingFolderDistributor.DistributeShafts.cs
    // (Refactor 2026-05-07, Charge R10).

    // --- Dichtheitspruefungsprotokoll Distribution ---

    // DistributeDichtheit, DistributeDichtheitFiles, DistributeDichtheitCore,
    // ExtractDichtheitPerPage, TryExtractDichtheitShafts,
    // ResolveDichtheitHaltungOrder ausgegliedert nach
    // HoldingFolderDistributor.DistributeDichtheit.cs
    // (Refactor 2026-05-07, Charge R11).


    private static IReadOnlyList<PdfShaftChunk> SplitPdfIntoShafts(IReadOnlyList<PageInfo> pages)
    {
        var chunks = new List<PdfShaftChunk>();
        if (pages.Count == 0) return chunks;

        List<int>? currentPages = null;
        ParsedShaftPdf? currentParsed = null;

        foreach (var page in pages)
        {
            var parsed = ParseSchachtPdfPageWithOcrFallback(page);
            if (!parsed.Success)
            {
                if (currentPages is not null && currentParsed is not null)
                    currentPages.Add(page.PageNumber);
                continue;
            }

            if (currentPages is not null
                && currentParsed is not null
                && string.Equals(parsed.ShaftNumber, currentParsed.ShaftNumber, StringComparison.OrdinalIgnoreCase)
                && parsed.Date == currentParsed.Date)
            {
                currentPages.Add(page.PageNumber);
                continue;
            }

            if (currentPages is not null && currentParsed is not null)
                chunks.Add(new PdfShaftChunk(currentPages, currentParsed));

            currentPages = new List<int> { page.PageNumber };
            currentParsed = parsed;
        }

        if (currentPages is not null && currentParsed is not null)
            chunks.Add(new PdfShaftChunk(currentPages, currentParsed));

        return chunks;
    }

    // ParseSchachtPdfPageWithOcrFallback, TryParseSchachtPdfPageFromFormFields,
    // BuildSyntheticFormText, TryExtractSchachtNumberFromFormEntries,
    // BuildFormEntryLabel, ContainsDateLabel, ContainsSchachtNumberLabel,
    // ExtractShaftNumberToken, TryCompleteShaftDateFromSiblingProtocol,
    // TryResolveDateFromSiblingProtocol, GetOrBuildSchachtDateIndex,
    // BuildSchachtDateIndex, NormalizeShaftNumberKey
    // ausgegliedert nach HoldingFolderDistributor.SchachtPdfParsing.cs
    // (Refactor 2026-05-07, Charge R7).

    // BuildPageRange + IsContentsPage ausgegliedert nach
    // HoldingFolderDistributor.PdfReading.cs
    // (Refactor 2026-05-08, Charge R13).

    // TryFindInspectionDate, TryFindSchachtDate, FindNearbyDate
    // ausgegliedert nach HoldingFolderDistributor.DateParsing.cs
    // (Refactor 2026-05-07, Charge R5).

    // TryFindHaltungId, TryParseKsCompactHoldingDigits, TryFindSchachtNumber,
    // WinCanValueRegex, WinCanUpperLabelRegex, WinCanLowerLabelRegex,
    // NormalizeLine, TryGetValueAfterLabel, TryExtractFromHeader,
    // LooksLikeDateFragment, TryExtractFromShafts, TryFindPoint,
    // FindNextToken ausgegliedert nach
    // HoldingFolderDistributor.HaltungExtraction.cs
    // (Refactor 2026-05-08, Charge R14).

    private static void WritePdfPages(string sourcePdfPath, IReadOnlyList<int> pages, string destPdfPath)
    {
        using var doc = PdfDocument.Open(sourcePdfPath);
        using var builder = new PdfDocumentBuilder();

        foreach (var pageNumber in pages)
            builder.AddPage(doc, pageNumber);

        var bytes = builder.Build();
        File.WriteAllBytes(destPdfPath, bytes);
    }

    private static void AppendPdfFile(string targetPdfPath, string additionalPdfPath, bool removeAdditionalWhenMoved)
    {
        if (string.IsNullOrWhiteSpace(targetPdfPath)
            || string.IsNullOrWhiteSpace(additionalPdfPath)
            || !File.Exists(targetPdfPath)
            || !File.Exists(additionalPdfPath))
            throw new FileNotFoundException("PDF for append not found.");

        if (string.Equals(targetPdfPath, additionalPdfPath, StringComparison.OrdinalIgnoreCase))
            return;

        var mergedTempPath = Path.Combine(Path.GetTempPath(), $"merge_{Guid.NewGuid():N}.pdf");
        try
        {
            using (var targetDoc = PdfDocument.Open(targetPdfPath))
            using (var additionalDoc = PdfDocument.Open(additionalPdfPath))
            using (var builder = new PdfDocumentBuilder())
            {
                foreach (var page in targetDoc.GetPages())
                    builder.AddPage(targetDoc, page.Number);

                foreach (var page in additionalDoc.GetPages())
                    builder.AddPage(additionalDoc, page.Number);

                var bytes = builder.Build();
                File.WriteAllBytes(mergedTempPath, bytes);
            }

            File.Copy(mergedTempPath, targetPdfPath, overwrite: true);

            if (removeAdditionalWhenMoved)
            {
                try
                {
                    if (File.Exists(additionalPdfPath))
                        File.Delete(additionalPdfPath);
                }
                catch
                {
                    // Best-effort cleanup for move semantics.
                }
            }
        }
        finally
        {
            try
            {
                if (File.Exists(mergedTempPath))
                    File.Delete(mergedTempPath);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static IReadOnlyList<PdfTextReplacement> BuildRenameReplacements(string oldValue, string newValue)
    {
        var oldToken = (oldValue ?? string.Empty).Trim();
        var newToken = (newValue ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(oldToken)
            || string.IsNullOrWhiteSpace(newToken)
            || string.Equals(oldToken, newToken, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<PdfTextReplacement>();

        return new[] { new PdfTextReplacement(oldToken, newToken) };
    }

    private static PdfCorrectionResult TryCorrectPdfTextLayer(string sourcePdfPath, IReadOnlyList<PdfTextReplacement> replacements)
    {
        if (string.IsNullOrWhiteSpace(sourcePdfPath) || !File.Exists(sourcePdfPath))
            return new PdfCorrectionResult(false, false, sourcePdfPath, 0, 0, "PDF nicht gefunden.");

        if (replacements.Count == 0)
            return new PdfCorrectionResult(true, false, sourcePdfPath, 0, 0, string.Empty);

        var effectiveReplacements = replacements
            .Where(r => !string.IsNullOrWhiteSpace(r.SearchText)
                        && !string.IsNullOrWhiteSpace(r.ReplacementText)
                        && !string.Equals(r.SearchText, r.ReplacementText, StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => r.SearchText.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (effectiveReplacements.Count == 0)
            return new PdfCorrectionResult(true, false, sourcePdfPath, 0, 0, string.Empty);

        var correctedTempPath = Path.Combine(Path.GetTempPath(), $"pdfcorr_{Guid.NewGuid():N}.pdf");
        try
        {
            using var sourceDocument = PdfDocument.Open(sourcePdfPath);
            using var builder = new PdfDocumentBuilder();
            var overlayFont = builder.AddStandard14Font(Standard14Font.Helvetica);

            var totalMatches = 0;
            var pageCount = 0;

            foreach (var page in sourceDocument.GetPages())
            {
                var pageBuilder = builder.AddPage(sourceDocument, page.Number);
                var matches = FindPdfTextReplacementMatches(page, effectiveReplacements);
                if (matches.Count == 0)
                    continue;

                pageCount++;
                totalMatches += matches.Count;

                pageBuilder.NewContentStreamAfter();
                foreach (var match in matches.OrderBy(m => m.StartLetterIndex))
                {
                    var width = Math.Max(1d, match.Right - match.Left);
                    var height = Math.Max(1d, match.Top - match.Bottom);
                    var left = Math.Max(0d, match.Left - 0.40d);
                    var bottom = Math.Max(0d, match.Bottom - 0.25d);
                    var drawWidth = width + 0.80d;
                    var drawHeight = height + 0.50d;

                    pageBuilder.SetTextAndFillColor(255, 255, 255);
                    pageBuilder.DrawRectangle(
                        new PdfPoint((decimal)left, (decimal)bottom),
                        (decimal)drawWidth,
                        (decimal)drawHeight,
                        0.1m,
                        fill: true);

                    var fontSize = Math.Max(1d, match.FontSize);
                    var textPosition = new PdfPoint(match.StartBaseLine.X, match.StartBaseLine.Y);
                    var measuredLetters = pageBuilder.MeasureText(match.Replacement.ReplacementText, (decimal)fontSize, textPosition, overlayFont);
                    var measuredWidth = MeasureLettersWidth(measuredLetters);
                    if (measuredWidth > width && measuredWidth > 0d)
                    {
                        var scale = width / measuredWidth;
                        fontSize = Math.Max(1d, fontSize * scale);
                    }

                    pageBuilder.SetTextAndFillColor(0, 0, 0);
                    pageBuilder.AddText(match.Replacement.ReplacementText, (decimal)fontSize, textPosition, overlayFont);
                    pageBuilder.ResetColor();
                }
            }

            if (totalMatches == 0)
                return new PdfCorrectionResult(true, false, sourcePdfPath, 0, 0, "Keine Treffer im Text-Layer gefunden.");

            var bytes = builder.Build();
            File.WriteAllBytes(correctedTempPath, bytes);
            return new PdfCorrectionResult(
                true,
                true,
                correctedTempPath,
                totalMatches,
                pageCount,
                $"Text-Layer aktualisiert ({totalMatches} Treffer auf {pageCount} Seiten).");
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(correctedTempPath))
                    File.Delete(correctedTempPath);
            }
            catch
            {
                // Best-effort cleanup.
            }

            return new PdfCorrectionResult(false, false, sourcePdfPath, 0, 0, $"PDF-Korrektur fehlgeschlagen: {ex.Message}");
        }
    }

    private static IReadOnlyList<PdfTextReplacementMatch> FindPdfTextReplacementMatches(
        Page page,
        IReadOnlyList<PdfTextReplacement> replacements)
    {
        var letters = page.Letters?.ToList() ?? new List<Letter>();
        if (letters.Count == 0 || replacements.Count == 0)
            return Array.Empty<PdfTextReplacementMatch>();

        var flatTextBuilder = new StringBuilder();
        var charToLetterIndex = new List<int>(letters.Count * 2);
        for (var i = 0; i < letters.Count; i++)
        {
            var value = letters[i].Value;
            if (string.IsNullOrEmpty(value))
                continue;

            flatTextBuilder.Append(value);
            for (var c = 0; c < value.Length; c++)
                charToLetterIndex.Add(i);
        }

        var flatText = flatTextBuilder.ToString();
        if (flatText.Length == 0 || charToLetterIndex.Count == 0)
            return Array.Empty<PdfTextReplacementMatch>();

        var matches = new List<PdfTextReplacementMatch>();
        foreach (var replacement in replacements)
        {
            var search = replacement.SearchText.Trim();
            if (string.IsNullOrWhiteSpace(search) || search.Length > flatText.Length)
                continue;

            var searchStart = 0;
            while (searchStart <= flatText.Length - search.Length)
            {
                var foundIndex = flatText.IndexOf(search, searchStart, StringComparison.OrdinalIgnoreCase);
                if (foundIndex < 0)
                    break;

                if (IsReplacementBoundary(flatText, foundIndex, search.Length))
                {
                    var foundEnd = foundIndex + search.Length - 1;
                    if (foundEnd >= 0 && foundEnd < charToLetterIndex.Count)
                    {
                        var startLetterIndex = charToLetterIndex[foundIndex];
                        var endLetterIndex = charToLetterIndex[foundEnd];
                        var match = TryBuildPdfTextReplacementMatch(letters, replacement, startLetterIndex, endLetterIndex);
                        if (match is not null)
                            matches.Add(match);
                    }
                }

                searchStart = foundIndex + search.Length;
            }
        }

        if (matches.Count <= 1)
            return matches;

        return FilterOverlappingReplacementMatches(matches);
    }

    private static PdfTextReplacementMatch? TryBuildPdfTextReplacementMatch(
        IReadOnlyList<Letter> letters,
        PdfTextReplacement replacement,
        int startLetterIndex,
        int endLetterIndex)
    {
        if (startLetterIndex < 0 || endLetterIndex < startLetterIndex || endLetterIndex >= letters.Count)
            return null;

        var left = double.MaxValue;
        var right = double.MinValue;
        var bottom = double.MaxValue;
        var top = double.MinValue;
        double fontSizeSum = 0;
        var fontSizeCount = 0;
        var startBaseline = letters[startLetterIndex].StartBaseLine;

        for (var i = startLetterIndex; i <= endLetterIndex; i++)
        {
            var glyph = letters[i].GlyphRectangle;
            left = Math.Min(left, glyph.Left);
            right = Math.Max(right, glyph.Right);
            bottom = Math.Min(bottom, glyph.Bottom);
            top = Math.Max(top, glyph.Top);

            if (letters[i].FontSize > 0)
            {
                fontSizeSum += letters[i].FontSize;
                fontSizeCount++;
            }
        }

        if (left == double.MaxValue || right == double.MinValue || bottom == double.MaxValue || top == double.MinValue)
            return null;

        var fontSize = fontSizeCount > 0 ? fontSizeSum / fontSizeCount : 9d;
        return new PdfTextReplacementMatch(
            replacement,
            startLetterIndex,
            endLetterIndex,
            left,
            bottom,
            right,
            top,
            startBaseline,
            fontSize);
    }

    private static IReadOnlyList<PdfTextReplacementMatch> FilterOverlappingReplacementMatches(
        IReadOnlyList<PdfTextReplacementMatch> matches)
    {
        var accepted = new List<PdfTextReplacementMatch>();
        foreach (var candidate in matches
                     .OrderBy(m => m.StartLetterIndex)
                     .ThenByDescending(m => m.EndLetterIndex - m.StartLetterIndex))
        {
            var overlaps = accepted.Any(existing =>
                !(candidate.EndLetterIndex < existing.StartLetterIndex
                  || candidate.StartLetterIndex > existing.EndLetterIndex));
            if (!overlaps)
                accepted.Add(candidate);
        }

        return accepted;
    }

    private static bool IsReplacementBoundary(string text, int startIndex, int length)
    {
        if (startIndex < 0 || length <= 0 || startIndex + length > text.Length)
            return false;

        var before = startIndex > 0 ? text[startIndex - 1] : '\0';
        var afterIndex = startIndex + length;
        var after = afterIndex < text.Length ? text[afterIndex] : '\0';
        return !IsIdentifierCharacter(before) && !IsIdentifierCharacter(after);
    }

    private static bool IsIdentifierCharacter(char ch)
    {
        if (ch == '\0')
            return false;

        return char.IsLetterOrDigit(ch)
               || ch == '-'
               || ch == '_'
               || ch == '.';
    }

    private static double MeasureLettersWidth(IReadOnlyList<Letter> letters)
    {
        if (letters.Count == 0)
            return 0d;

        var left = double.MaxValue;
        var right = double.MinValue;
        foreach (var letter in letters)
        {
            var glyph = letter.GlyphRectangle;
            left = Math.Min(left, glyph.Left);
            right = Math.Max(right, glyph.Right);
        }

        if (left == double.MaxValue || right == double.MinValue)
            return 0d;

        return Math.Max(0d, right - left);
    }

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

    // NodePrefixRegex, StripNodePrefixes, EnumerateHoldingLookupKeys,
    // ReverseHoldingId ausgegliedert nach HoldingFolderDistributor.Util.cs
    // (Refactor 2026-05-07, Charge R4).

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

    private static string? TryFindFilmName(string text, Regex filmRx)
    {
        var filmMatch = filmRx.Match(text);
        if (filmMatch.Success)
            return NormalizeVideoFileName(filmMatch.Groups[1].Value);

        // Fallback: any token with common video extension
        var extRx = new Regex($@"\b([A-Za-z0-9_\-\.]+?\.(?:{VideoExtensionPattern}))\b", RegexOptions.IgnoreCase);
        var extMatch = extRx.Match(text);
        if (extMatch.Success)
            return NormalizeVideoFileName(extMatch.Groups[1].Value);

        // Fallback: line with "Film" or "Video" -> take next non-empty token
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!line.Contains("Film", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("Video", StringComparison.OrdinalIgnoreCase))
                continue;

            var tokens = Tokenize(line);
            var candidate = tokens.FirstOrDefault(t => HasVideoExtension(t));
            if (!string.IsNullOrWhiteSpace(candidate))
                return NormalizeVideoFileName(candidate);

            if (i + 1 < lines.Length)
            {
                var nextTokens = Tokenize(lines[i + 1]);
                var nextCandidate = nextTokens.FirstOrDefault(t => HasVideoExtension(t));
                if (!string.IsNullOrWhiteSpace(nextCandidate))
                    return NormalizeVideoFileName(nextCandidate);
            }
        }

        return null;
    }

    // Photo-Hint-Extraction (PhotoAfterLabelRegex, PhotoTokenRegex,
    // ExtractPhotoHintsFromPdf, AddPhotoLookupKeys, EnumeratePhotoLookupKeys)
    // ausgegliedert nach HoldingFolderDistributor.PhotoHints.cs
    // (Refactor 2026-05-07, Charge R12).

    private static string TrimLeadingZerosValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var trimmed = value.TrimStart('0');
        return string.IsNullOrEmpty(trimmed) ? "0" : trimmed;
    }

    // String-/ID-Normalisierungs-Helfer (NormalizePhotoToken, Tokenize,
    // HasVideoExtension, HasImageExtension, NormalizeVideoFileName,
    // SanitizePathSegment, HoldingFromKiasFilename, NormalizeHaltungId,
    // NormalizeKey, IsValidHaltungId) ausgegliedert nach
    // HoldingFolderDistributor.Util.cs (Refactor 2026-05-07, Charge R4).

    /// <summary>
    /// Prueft ob im Haltungsordner bereits ein Video mit gleicher Dateigroesse existiert.
    /// Gibt den Pfad zurueck wenn ja, sonst null.
    /// Verhindert Duplikate beim erneuten Verteilen.
    /// </summary>
    // FindExistingVideo ausgegliedert nach HoldingFolderDistributor.IO.cs
    // (Refactor 2026-05-07, Charge R3).

    /// <summary>
    /// Versucht, ein nicht-parsbares PDF (z.B. Dichtheitspruefungsprotokoll) anhand
    /// seines Dateinamens einem bereits verteilten Haltungsordner zuzuordnen.
    /// Sucht nach Haltungsnummern im Dateinamen und vergleicht mit dem Index.
    /// </summary>
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

    // EnsureUniquePath ausgegliedert nach HoldingFolderDistributor.IO.cs
    // (Refactor 2026-05-07, Charge R3).

#if DEMO
    public static class DemoProgram
    {
        public static void Main()
        {
            var results = Distribute(
                pdfSourceFolder: @"D:\Input\PDFs",
                videoSourceFolder: @"D:\Input\Videos",
                destGemeindeFolder: @"D:\Bauwerke\Haltungen\Buerglen",
                moveInsteadOfCopy: false,
                overwrite: false,
                recursiveVideoSearch: true,
                unmatchedFolderName: "__UNMATCHED",
                project: null,
                progress: null);

            foreach (var r in results)
                Console.WriteLine($"{(r.Success ? "OK" : "FAIL")} - {r.Message} - {r.SourcePdfPath}");
        }
    }
#endif
}
