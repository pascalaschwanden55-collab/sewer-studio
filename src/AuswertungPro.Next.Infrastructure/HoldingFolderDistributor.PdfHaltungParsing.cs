using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// HoldingFolderDistributor — PDF-Parsing fuer Haltungsinspektions-PDFs
/// (partial class).
///
/// Refactor 2026-05-07 (Etappe 2, Charge R6): Public ParsePdf/ParsePdfPage
/// + ihre direkten Helfer (Header-Match, Schacht-Felder, OCR-Fallback,
/// Multi-Page-Splitting) ausgegliedert. Mechanisch — keine
/// Verhaltensaenderung.
/// </summary>
public static partial class HoldingFolderDistributor
{
    public static ParsedPdf ParsePdf(string text)
    {
        text = NormalizeText(text);
        // Match both "Haltungsinspektion" and "Haltungsbilder" headers (Fretz PDF page 1 vs page 2)
        var headerMatch = PdfHeaderRegex.Match(text);
        if (!headerMatch.Success)
            return ParsePdfPage(text, null);

        if (!TryParseDateString(headerMatch.Groups[1].Value, out var date))
            return new ParsedPdf(false, "Date parse failed", null, null, null);

        var haltung = NormalizeHaltungId(headerMatch.Groups[2].Value);
        if (!IsValidHaltungId(haltung))
            return ParsePdfPage(text);

        var videoFile = TryFindFilmName(text, FilmNameRegex);
        return new ParsedPdf(true, videoFile is null ? "Film name not found" : null, date, haltung, videoFile);
    }

    // Temporarily public for diagnostic purposes
    public static ParsedPdf ParsePdfPage(string text, string? pdfPath = null)
    {
        text = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(text))
            return new ParsedPdf(false, "Empty page", null, null, null);

        var isWinCan = text.Contains("wincan", StringComparison.OrdinalIgnoreCase);
        var filenameHaltung = isWinCan ? TryExtractHaltungFromPdfPath(pdfPath) : null;

        // Try header extraction first (Haltungsinspektion / Haltungsbilder headers)
        // This is the most reliable source for Fretz/IBAK PDFs (pages 1 + 2)
        var headerHaltung = TryExtractFromHeader(text);
        if (headerHaltung is not null)
        {
            var headerDate = TryFindInspectionDate(text);
            var videoFileH = TryFindFilmName(text, FilmNameRegex);
            var baseMessageH = videoFileH is null ? "Film name not found" : null;
            return new ParsedPdf(true, baseMessageH, headerDate, headerHaltung, videoFileH);
        }

        // Fallback: extract from reliable sources:
        // 1. Haltung from Schacht/Punkt fields (Schacht oben/unten, Oberer/Unterer Punkt)
        // 2. Date from separate date field (Datum, Insp.datum, etc.)

        // Immer Haltungsnummer aus Schacht/Punkt-Feldern zusammensetzen
        var shaftHaltung = TryExtractFromShafts(text);
        var date = TryFindInspectionDate(text);
        var videoFile = TryFindFilmName(text, FilmNameRegex);
        var baseMessage = videoFile is null ? "Film name not found" : null;

        // Extrahiere explizites Haltung-Feld (falls vorhanden)
        var explicitHaltung = TryFindHaltungId(text);

        if (!string.IsNullOrWhiteSpace(shaftHaltung) && date is not null)
        {
            var shaftNormalized = NormalizeHaltungId(shaftHaltung);
            if (!IsValidHaltungId(shaftNormalized))
            {
                // Continue with explicit/fallback extraction instead of hard-failing.
                shaftHaltung = null;
            }
        }

        if (!string.IsNullOrWhiteSpace(shaftHaltung) && date is not null)
        {
            var shaftNormalized = NormalizeHaltungId(shaftHaltung);
            var normalized = shaftNormalized;

            // Verifiziere: explizites Haltung-Feld muss mit zusammengesetzter Nummer übereinstimmen (falls vorhanden)
            if (!string.IsNullOrWhiteSpace(explicitHaltung))
            {
                var explicitNorm = NormalizeHaltungId(explicitHaltung);
                if (IsValidHaltungId(explicitNorm) &&
                    !string.Equals(explicitNorm, shaftNormalized, StringComparison.OrdinalIgnoreCase))
                {
                    if (IsSuspiciousShaftPair(shaftNormalized, explicitNorm))
                    {
                        return new ParsedPdf(true, MergeMessage(baseMessage, $"Explizite Haltung bevorzugt ({explicitNorm})"), date, explicitNorm, videoFile);
                    }

                    if (!string.IsNullOrWhiteSpace(filenameHaltung) &&
                        (string.Equals(filenameHaltung, shaftNormalized, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(filenameHaltung, explicitNorm, StringComparison.OrdinalIgnoreCase)))
                    {
                        return new ParsedPdf(true, MergeMessage(baseMessage, "Haltung mit Dateiname validiert"), date, filenameHaltung, videoFile);
                    }
                    return new ParsedPdf(false, $"Haltungsnummer stimmt nicht überein: Schacht={normalized}, Feld={explicitNorm}", date, normalized, videoFile);
                }
            }

            return new ParsedPdf(true, baseMessage, date, normalized, videoFile);
        }

        // Fallback: Wenn keine Schacht-Felder gefunden, versuche explizites Haltung-Feld
        if (!string.IsNullOrWhiteSpace(explicitHaltung) && date is not null)
        {
            var explicitNorm = NormalizeHaltungId(explicitHaltung);
            if (!IsValidHaltungId(explicitNorm))
                return new ParsedPdf(false, "Haltung invalid (aus Feld)", date, explicitNorm, videoFile);

            if (!string.IsNullOrWhiteSpace(filenameHaltung) &&
                !string.Equals(filenameHaltung, explicitNorm, StringComparison.OrdinalIgnoreCase))
            {
                return new ParsedPdf(
                    true,
                    MergeMessage(baseMessage, $"Dateiname bevorzugt ({filenameHaltung})"),
                    date,
                    filenameHaltung,
                    videoFile);
            }

            return new ParsedPdf(true, baseMessage, date, explicitNorm, videoFile);
        }

        if (date is not null && !string.IsNullOrWhiteSpace(filenameHaltung))
        {
            return new ParsedPdf(
                true,
                MergeMessage(baseMessage, "Haltung aus Dateiname"),
                date,
                filenameHaltung,
                videoFile);
        }

        // XTF-Fallback: Wenn WinCAN (erkennbar an typischem Text) und keine Haltung gefunden, versuche XTF
        if (isWinCan && !string.IsNullOrWhiteSpace(pdfPath))
        {
            var dir = Path.GetDirectoryName(pdfPath);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                string[] xtfFiles;
                lock (XtfCacheSync)
                {
                    if (!XtfFilesCache.TryGetValue(dir, out xtfFiles!))
                    {
                        xtfFiles = Directory.GetFiles(dir, "*.xtf", SearchOption.AllDirectories);
                        XtfFilesCache[dir] = xtfFiles;
                    }
                }
                var xtfPath = XtfHelper.FindMatchingXtf(pdfPath, xtfFiles);
                if (!string.IsNullOrWhiteSpace(xtfPath))
                {
                    var holdings = XtfHelper.ParseHoldingsFromXtf(xtfPath);
                    var holding = holdings.FirstOrDefault();
                    if (holding != null && !string.IsNullOrWhiteSpace(holding.HaltungId))
                    {
                        return new ParsedPdf(true, "(aus XTF uebernommen)", date, holding.HaltungId, videoFile);
                    }
                }
            }
        }

        // Haltung trotzdem zurueckgeben (auch ohne Datum), damit SplitPdfIntoHoldings
        // Stammdaten-Seiten dem richtigen Chunk zuordnen kann.
        var bestHaltung = !string.IsNullOrWhiteSpace(shaftHaltung) ? NormalizeHaltungId(shaftHaltung)
            : !string.IsNullOrWhiteSpace(explicitHaltung) ? NormalizeHaltungId(explicitHaltung)
            : null;
        if (!string.IsNullOrWhiteSpace(bestHaltung) && !IsValidHaltungId(bestHaltung))
            bestHaltung = null;

        return new ParsedPdf(false, "Schacht-Felder und Haltung nicht gefunden", date, bestHaltung, videoFile);
    }

    private static bool IsSuspiciousShaftPair(string shaftPair, string explicitPair)
    {
        var shaftParts = shaftPair.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var explicitParts = explicitPair.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (shaftParts.Length != 2 || explicitParts.Length != 2)
            return false;

        if (string.Equals(shaftParts[0], shaftParts[1], StringComparison.OrdinalIgnoreCase))
            return true;

        // If explicit pair has different endpoints but shaft pair collapsed to a repeated value, prefer explicit.
        if (!string.Equals(explicitParts[0], explicitParts[1], StringComparison.OrdinalIgnoreCase)
            && string.Equals(shaftParts[1], explicitParts[0], StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static string? MergeMessage(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a))
            return string.IsNullOrWhiteSpace(b) ? null : b;
        if (string.IsNullOrWhiteSpace(b))
            return a;
        return $"{a}; {b}";
    }

    private static string? TryExtractHaltungFromPdfPath(string? pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            return null;

        var fileName = Path.GetFileNameWithoutExtension(pdfPath);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var match = PdfFilenamePairRegex.Match(fileName);
        if (!match.Success)
            return null;

        var normalized = NormalizeHaltungId(match.Value.Replace('_', '-'));
        return IsValidHaltungId(normalized) ? normalized : null;
    }

    private static IReadOnlyList<PdfPageChunk> SplitPdfIntoHoldings(IReadOnlyList<PageInfo> pages)
    {
        var chunks = new List<PdfPageChunk>();
        if (pages.Count == 0) return chunks;

        // Pre-warm XTF cache for the PDF directory to avoid AllDirectories scan per page
        PreWarmXtfCache(pages);

        List<int>? currentPages = null;
        ParsedPdf? currentParsed = null;

        foreach (var page in pages)
        {
            var parsed = ParsePdfPageWithOcrFallback(page);
            if (!parsed.Success)
            {
                if (IsContentsPage(page.Text))
                    continue;

                // Stammdaten-Seiten haben oft eine gueltige Haltung aber kein Datum.
                // Wenn die Haltung NICHT zum aktuellen Chunk passt, nicht blind anhaengen
                // sondern fuer den naechsten Chunk aufheben (Seite wird dann dort angehaengt).
                if (currentPages is not null && currentParsed is not null)
                {
                    var failedHaltung = parsed.Haltung;
                    if (!string.IsNullOrWhiteSpace(failedHaltung)
                        && !string.Equals(failedHaltung, currentParsed.Haltung, StringComparison.OrdinalIgnoreCase))
                    {
                        // Diese Seite gehoert zur naechsten Haltung → Chunk abschliessen
                        chunks.Add(new PdfPageChunk(currentPages, currentParsed));
                        currentPages = new List<int> { page.PageNumber };
                        currentParsed = null; // Warte auf die naechste erfolgreiche Seite
                    }
                    else
                    {
                        currentPages.Add(page.PageNumber);
                    }
                }
                else if (currentPages is not null)
                {
                    // currentParsed ist null (wartend nach Stammdaten-Seite)
                    currentPages.Add(page.PageNumber);
                }
                continue;
            }

            if (currentPages is not null
                && currentParsed is not null
                && string.Equals(parsed.Haltung, currentParsed.Haltung, StringComparison.OrdinalIgnoreCase)
                && parsed.Date == currentParsed.Date)
            {
                currentPages.Add(page.PageNumber);
                continue;
            }

            // Stammdaten-Seite hatte Chunk abgeschlossen, jetzt kommt die passende Haltungsseite
            if (currentPages is not null && currentParsed is null)
            {
                currentPages.Add(page.PageNumber);
                currentParsed = parsed;
                continue;
            }

            if (currentPages is not null && currentParsed is not null)
                chunks.Add(new PdfPageChunk(currentPages, currentParsed));

            currentPages = new List<int> { page.PageNumber };
            currentParsed = parsed;
        }

        if (currentPages is not null && currentParsed is not null)
            chunks.Add(new PdfPageChunk(currentPages, currentParsed));

        return chunks;
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
                // Robustheits-Fix 2026-05-10 (Deep-Dive #1): SafeFileEnumeration
                // statt Directory.GetFiles — gesperrte Unterordner brechen den
                // XTF-Cache-Aufbau nicht mehr ab.
                XtfFilesCache[dir] = Common.SafeFileEnumeration
                    .EnumerateFilesSafe(dir, "*.xtf", recursive: true)
                    .ToArray();
            }
        }
    }

    private static ParsedPdf ParsePdfWithOcrFallback(IReadOnlyList<PageInfo> pages)
    {
        var pdfText = string.Join("\n\n", pages.Select(p => p.Text));
        var parsed = ParsePdf(pdfText);
        if (parsed.Success)
            return parsed;

        var ocrTexts = new List<string>(pages.Count);
        string? firstOcrError = null;
        var ocrAttempted = false;

        foreach (var page in pages)
        {
            if (!string.IsNullOrWhiteSpace(page.Text))
            {
                ocrTexts.Add(page.Text);
                continue;
            }

            if (string.IsNullOrWhiteSpace(page.SourcePath) || !File.Exists(page.SourcePath))
                continue;

            ocrAttempted = true;
            var ocr = PdfOcrExtractor.TryExtractPageText(page.SourcePath, page.PageNumber);
            if (ocr.Success && !string.IsNullOrWhiteSpace(ocr.Text))
            {
                ocrTexts.Add(ocr.Text);
            }
            else if (string.IsNullOrWhiteSpace(firstOcrError) && !string.IsNullOrWhiteSpace(ocr.Message))
            {
                firstOcrError = ocr.Message;
            }
        }

        if (ocrTexts.Count == 0)
        {
            if (!ocrAttempted)
                return parsed;

            var ocrMessage = string.IsNullOrWhiteSpace(firstOcrError)
                ? "OCR lieferte keinen Text"
                : $"OCR: {firstOcrError}";
            return new ParsedPdf(false, MergeMessage(parsed.Message, ocrMessage), parsed.Date, parsed.Haltung, parsed.VideoFile);
        }

        var parsedFromOcr = ParsePdf(string.Join("\n\n", ocrTexts));
        if (parsedFromOcr.Success)
            return parsedFromOcr;

        var mergedMessage = string.IsNullOrWhiteSpace(firstOcrError)
            ? MergeMessage(parsed.Message, parsedFromOcr.Message)
            : MergeMessage(MergeMessage(parsed.Message, parsedFromOcr.Message), $"OCR: {firstOcrError}");
        var mergedDate = parsedFromOcr.Date ?? parsed.Date;
        var mergedHaltung = !string.IsNullOrWhiteSpace(parsedFromOcr.Haltung) ? parsedFromOcr.Haltung : parsed.Haltung;
        var mergedVideo = !string.IsNullOrWhiteSpace(parsedFromOcr.VideoFile) ? parsedFromOcr.VideoFile : parsed.VideoFile;
        return new ParsedPdf(false, mergedMessage, mergedDate, mergedHaltung, mergedVideo);
    }

    private static ParsedPdf ParsePdfPageWithOcrFallback(PageInfo page)
    {
        var parsed = ParsePdfPage(page.Text, page.SourcePath);
        if (parsed.Success)
            return parsed;

        if (string.IsNullOrWhiteSpace(page.SourcePath) || !File.Exists(page.SourcePath))
            return parsed;

        // OCR fallback is expensive; only run when direct extraction failed.
        var ocr = PdfOcrExtractor.TryExtractPageText(page.SourcePath, page.PageNumber);
        if (!ocr.Success || string.IsNullOrWhiteSpace(ocr.Text))
        {
            var ocrMessage = string.IsNullOrWhiteSpace(ocr.Message)
                ? "OCR lieferte keinen Text"
                : $"OCR: {ocr.Message}";
            return new ParsedPdf(false, MergeMessage(parsed.Message, ocrMessage), parsed.Date, parsed.Haltung, parsed.VideoFile);
        }

        var parsedFromOcr = ParsePdfPage(ocr.Text, page.SourcePath);
        if (!parsedFromOcr.Success)
        {
            var mergedDateFallback = parsedFromOcr.Date ?? parsed.Date;
            var mergedHaltungFallback = !string.IsNullOrWhiteSpace(parsedFromOcr.Haltung) ? parsedFromOcr.Haltung : parsed.Haltung;
            var mergedVideoFallback = !string.IsNullOrWhiteSpace(parsedFromOcr.VideoFile) ? parsedFromOcr.VideoFile : parsed.VideoFile;
            return new ParsedPdf(false, MergeMessage(parsed.Message, parsedFromOcr.Message), mergedDateFallback, mergedHaltungFallback, mergedVideoFallback);
        }

        var mergedDate = parsedFromOcr.Date ?? parsed.Date;
        var mergedHaltung = !string.IsNullOrWhiteSpace(parsedFromOcr.Haltung) ? parsedFromOcr.Haltung : parsed.Haltung;
        var mergedVideo = !string.IsNullOrWhiteSpace(parsedFromOcr.VideoFile) ? parsedFromOcr.VideoFile : parsed.VideoFile;
        var mergedMessage = MergeMessage(parsed.Message, parsedFromOcr.Message);
        return new ParsedPdf(true, mergedMessage, mergedDate, mergedHaltung, mergedVideo);
    }
}
