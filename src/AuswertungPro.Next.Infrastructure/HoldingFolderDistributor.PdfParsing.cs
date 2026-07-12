using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Map;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure;

// PDF-/Schacht-/Dichtheit-Parsing und PDF-Schreib-/Korrekturhelfer.
// Teil derselben partial-Klasse - reine mechanische Auslagerung (kein Verhaltenswechsel).
public static partial class HoldingFolderDistributor
{
    private static readonly object SchachtDateIndexSync = new();

    private static readonly Dictionary<string, IReadOnlyDictionary<string, DateTime>> SchachtDateIndexCache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex PdfHeaderRegex = new(
        @"Haltungs(?:\s*inspektion|bilder)\s*[-–—]\s*(\d{2}\.\d{2}\.\d{2,4}|\d{4}-\d{2}-\d{2})\s*[-–—]\s*((?:\d{2,}\.\d{2,}|\d{4,})\s*[-/]\s*(?:\d{2,}\.\d{2,}|\d{4,}))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private sealed record PdfPageChunk(IReadOnlyList<int> Pages, ParsedPdf Parsed);

    // Temporarily public for diagnostic purposes


    // Temporarily public for diagnostic purposes
    public sealed record ParsedPdf(bool Success, string? Message, DateTime? Date, string? Haltung, string? VideoFile);

    public sealed record ParsedShaftPdf(bool Success, string? Message, DateTime? Date, string? ShaftNumber);


    private sealed record PdfShaftChunk(IReadOnlyList<int> Pages, ParsedShaftPdf Parsed);


    public static ParsedShaftPdf ParseSchachtPdf(string text)
    {
        text = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(text))
            return new ParsedShaftPdf(false, "Empty page", null, null);

        return ParseSchachtPdfPage(text);
    }


    public static ParsedShaftPdf ParseSchachtPdfPage(string text)
    {
        text = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(text))
            return new ParsedShaftPdf(false, "Empty page", null, null);

        var shaftNumber = TryFindSchachtNumber(text);
        var date = TryFindSchachtDate(text);

        if (string.IsNullOrWhiteSpace(shaftNumber) && date is null)
            return new ParsedShaftPdf(false, "Schachtnummer und Datum nicht gefunden", null, null);
        if (string.IsNullOrWhiteSpace(shaftNumber))
            return new ParsedShaftPdf(false, "Schachtnummer nicht gefunden", date, null);
        if (date is null)
            return new ParsedShaftPdf(false, "Datum nicht gefunden", null, shaftNumber);

        return new ParsedShaftPdf(true, null, date, shaftNumber);
    }

    /// <summary>
    /// Extrahiert die beiden Schachtnummern aus einem Dichtheitspruefungsprotokoll-PDF.
    /// Gibt (schachtA, schachtB) zurueck – die Reihenfolge kann vertauscht sein.
    /// </summary>
    // ── Multi-Seiten-Dichtheitspruefung (KIT-Format u.a.) ──────────────────

    /// <summary>
    /// Extrahiert pro Seite die Haltung/Schacht-Zuordnung.
    /// Kontrollinformations-Seiten werden der vorherigen Pruefseite zugeordnet.
    /// Gibt eine Liste mit einem Eintrag pro Pruefbericht zurueck.
    /// </summary>


    /// <summary>
    /// Extrahiert pro Seite die Haltung/Schacht-Zuordnung.
    /// Kontrollinformations-Seiten werden der vorherigen Pruefseite zugeordnet.
    /// Gibt eine Liste mit einem Eintrag pro Pruefbericht zurueck.
    /// </summary>

    /// <summary>
    /// Loest eine Haltung ueber den Kataster auf: zuerst fokussiert (Zahlen auf Haltungs-/
    /// Schacht-Zeilen inkl. Nachbarzeilen wegen Spalten-Versatz von pdftotext), sonst die
    /// ganze Seite als Rueckfall. Genau ein Kataster-Treffer = sichere Zuordnung.
    /// </summary>
    /// <summary>
    /// Liefert die Ziel-Wurzel fuer eine final ermittelte Haltung: normalerweise
    /// <paramref name="destGemeindeFolder"/>, aber den Unterordner "keine_Zuordnung", wenn ein
    /// Kataster geladen ist und das Schacht-Paar dort NICHT existiert. Dadurch laeuft die normale
    /// Verteil-Logik unveraendert, nur eine Ebene tiefer.
    ///
    /// Bewusst konservativ — es wird nur umgelenkt, wenn ALLE Bedingungen erfuellt sind:
    ///  - ein Kataster ist geladen (ohne Kataster: Verhalten exakt wie bisher),
    ///  - die Haltung ist KEINE Einzelschacht-Pruefung ("Schacht_..." bleibt im eigenen Ordner),
    ///  - aus der Haltungs-ID laesst sich ein echtes Schacht-Paar ableiten,
    ///  - und genau dieses Paar fehlt im Kataster (exakter Vergleich, kein Praefix-Stripping).
    /// Eine einzelne Haltungsnummer ohne ableitbares Paar bleibt im normalen Ordner (kein Fehl-Umlenken).
    /// </summary>
    private static string ResolveDistributionRoot(
        string destGemeindeFolder,
        string? haltungId,
        IHaltungCadastreResolver? cadastre)
        => DistributionPdfAssignmentController.ResolveDistributionRoot(
            destGemeindeFolder,
            haltungId,
            cadastre);

    /// <summary>
    /// Sammelt Schachtnummern fokussiert: Zahlen auf Zeilen mit einem Haltungs-/Schacht-Label
    /// UND deren direkten Nachbarzeilen (pdftotext setzt Werte oft eine Zeile versetzt).
    /// Messwerte (mbar, DN, Datum, GPS) bleiben so weitgehend aussen vor.
    /// </summary>
    /// <summary>
    /// Ermittelt die korrekte Haltungs-ID-Reihenfolge fuer zwei Schachtnummern.
    /// Prueft A-B und B-A gegen Projekt-Daten und vorhandene Ordner im Zielverzeichnis.
    /// </summary>


    /// <summary>
    /// Ermittelt die korrekte Haltungs-ID-Reihenfolge fuer zwei Schachtnummern.
    /// Prueft A-B und B-A gegen Projekt-Daten und vorhandene Ordner im Zielverzeichnis.
    /// </summary>
    // Temporarily public for diagnostic purposes


    // Temporarily public for diagnostic purposes
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
                        xtfFiles = Common.SafeFileEnumeration.EnumerateFilesSafe(dir, "*.xtf", recursive: true).ToArray();
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


    private static IReadOnlyList<PdfPageChunk> SplitPdfIntoHoldings(IReadOnlyList<DistributionPdfPage> pages)
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


    private static ParsedPdf ParsePdfWithOcrFallback(IReadOnlyList<DistributionPdfPage> pages)
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


    private static ParsedPdf ParsePdfPageWithOcrFallback(DistributionPdfPage page)
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


    private static IReadOnlyList<PdfShaftChunk> SplitPdfIntoShafts(IReadOnlyList<DistributionPdfPage> pages)
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


    private static ParsedShaftPdf ParseSchachtPdfPageWithOcrFallback(DistributionPdfPage page)
    {
        var parsed = ParseSchachtPdfPage(page.Text);
        if (parsed.Success)
            return parsed;

        if (string.IsNullOrWhiteSpace(page.SourcePath) || !File.Exists(page.SourcePath))
            return parsed;

        var completedFromSibling = TryCompleteShaftDateFromSiblingProtocol(page.SourcePath, parsed);
        if (completedFromSibling is not null)
            return completedFromSibling;

        // Many Schachtprotokolle are interactive PDF forms where values are not in page text.
        var parsedFromForm = TryParseSchachtPdfPageFromFormFields(page.SourcePath, page.PageNumber);
        if (parsedFromForm is not null)
            return parsedFromForm;

        // OCR fallback is expensive; only try when baseline parsing has no usable result.
        var ocr = PdfOcrExtractor.TryExtractPageText(page.SourcePath, page.PageNumber);
        if (!ocr.Success || string.IsNullOrWhiteSpace(ocr.Text))
            return parsed;

        var parsedFromOcr = ParseSchachtPdfPage(ocr.Text);
        var mergedShaft = !string.IsNullOrWhiteSpace(parsedFromOcr.ShaftNumber) ? parsedFromOcr.ShaftNumber : parsed.ShaftNumber;
        var mergedDate = parsedFromOcr.Date ?? parsed.Date;
        if (string.IsNullOrWhiteSpace(mergedShaft))
            return parsed;

        if (mergedDate is null)
        {
            var resolvedDate = TryResolveDateFromSiblingProtocol(page.SourcePath, mergedShaft);
            if (resolvedDate is not null)
                mergedDate = resolvedDate;
        }

        if (mergedDate is null)
            return parsed;

        return new ParsedShaftPdf(
            true,
            MergeMessage(parsedFromOcr.Message, "aus OCR"),
            mergedDate,
            mergedShaft);
    }


    private static ParsedShaftPdf? TryParseSchachtPdfPageFromFormFields(string pdfPath, int pageNumber)
    {
        var entries = PdfFormFieldExtractor.GetPageFieldEntries(pdfPath, pageNumber);
        if (entries.Count == 0)
            return null;

        // First pass: label-preserving synthetic text for existing parser rules.
        var syntheticText = ShaftPdfFormFieldParser.BuildSyntheticText(entries);
        var parsed = ParseSchachtPdfPage(syntheticText);
        if (parsed.Success)
        {
            return new ParsedShaftPdf(
                true,
                MergeMessage(parsed.Message, "aus PDF-Formular"),
                parsed.Date,
                parsed.ShaftNumber);
        }

        // Second pass: value-only heuristics for generic field names.
        var date = ShaftPdfFormFieldParser.TryExtractDate(entries);
        var shaft = ShaftPdfFormFieldParser.TryExtractShaftNumber(entries);
        if (string.IsNullOrWhiteSpace(shaft) || date is null)
            return null;

        return new ParsedShaftPdf(true, "aus PDF-Formular", date, shaft);
    }


    private static ParsedShaftPdf? TryCompleteShaftDateFromSiblingProtocol(string sourcePdfPath, ParsedShaftPdf parsed)
    {
        if (string.IsNullOrWhiteSpace(parsed.ShaftNumber) || parsed.Date is not null)
            return null;

        var resolvedDate = TryResolveDateFromSiblingProtocol(sourcePdfPath, parsed.ShaftNumber);
        if (resolvedDate is null)
            return null;

        return new ParsedShaftPdf(
            true,
            MergeMessage(parsed.Message, "Datum aus Schachtprotokoll"),
            resolvedDate,
            parsed.ShaftNumber);
    }


    private static DateTime? TryResolveDateFromSiblingProtocol(string sourcePdfPath, string shaftNumber)
    {
        if (string.IsNullOrWhiteSpace(sourcePdfPath)
            || string.IsNullOrWhiteSpace(shaftNumber)
            || !File.Exists(sourcePdfPath))
            return null;

        var dir = Path.GetDirectoryName(sourcePdfPath);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return null;

        var normalizedShaft = NormalizeShaftNumberKey(shaftNumber);
        if (string.IsNullOrWhiteSpace(normalizedShaft))
            return null;

        var siblingProtocolPdfs = Directory.EnumerateFiles(dir, "*.pdf", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(path, sourcePdfPath, StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                return name.Contains("schachtprotokoll", StringComparison.OrdinalIgnoreCase)
                       || name.Contains("protokoll", StringComparison.OrdinalIgnoreCase);
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (siblingProtocolPdfs.Count == 0)
            return null;

        foreach (var protocolPdf in siblingProtocolPdfs)
        {
            var index = GetOrBuildSchachtDateIndex(protocolPdf);
            if (index.Count == 0)
                continue;

            if (index.TryGetValue(normalizedShaft, out var date))
                return date;
        }

        return null;
    }


    private static IReadOnlyDictionary<string, DateTime> GetOrBuildSchachtDateIndex(string protocolPdfPath)
    {
        lock (SchachtDateIndexSync)
        {
            if (SchachtDateIndexCache.TryGetValue(protocolPdfPath, out var cached))
                return cached;
        }

        var built = BuildSchachtDateIndex(protocolPdfPath);

        lock (SchachtDateIndexSync)
        {
            SchachtDateIndexCache[protocolPdfPath] = built;
        }

        return built;
    }


    private static IReadOnlyDictionary<string, DateTime> BuildSchachtDateIndex(string protocolPdfPath)
    {
        var index = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var extraction = PdfTextExtractor.ExtractPages(protocolPdfPath);
            for (var i = 0; i < extraction.Pages.Count; i++)
            {
                ParsedShaftPdf? parsed = null;

                var fromText = ParseSchachtPdfPage(extraction.Pages[i]);
                if (fromText.Success)
                {
                    parsed = fromText;
                }
                else
                {
                    parsed = TryParseSchachtPdfPageFromFormFields(protocolPdfPath, i + 1);
                }

                if (parsed is null || !parsed.Success || parsed.Date is null || string.IsNullOrWhiteSpace(parsed.ShaftNumber))
                    continue;

                var key = NormalizeShaftNumberKey(parsed.ShaftNumber);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!index.ContainsKey(key))
                    index[key] = parsed.Date.Value;
            }
        }
        catch
        {
            // Best effort date index.
        }

        return index;
    }


    // WinCan-Regex-Felder und Label-Wert-Extraktion nach ShaftCandidateScanner verschoben.


    /// <summary>
    /// Extracts haltung pair from "Haltungsinspektion" or "Haltungsbilder" header lines.
    /// Both Fretz page 1 (Haltungsinspektion) and page 2 (Haltungsbilder) use this format.
    /// </summary>


    /// <summary>
    /// Extracts haltung pair from "Haltungsinspektion" or "Haltungsbilder" header lines.
    /// Both Fretz page 1 (Haltungsinspektion) and page 2 (Haltungsbilder) use this format.
    /// </summary>
    private static string? TryExtractFromHeader(string text)
        => HoldingDistribution.ShaftCandidateScanner.TryExtractFromHeader(text);

    /// <summary>
    /// Returns true if the first part of a haltung pair looks like a date fragment (MM.YYYY).
    /// This prevents "09.2025-80638" from being treated as a valid haltung.
    /// </summary>


    /// <summary>
    /// Returns true if the first part of a haltung pair looks like a date fragment (MM.YYYY).
    /// This prevents "09.2025-80638" from being treated as a valid haltung.
    /// </summary>
    private static bool LooksLikeDateFragment(string haltungId)
        => HoldingDistribution.ShaftCandidateScanner.LooksLikeDateFragment(haltungId);


    private static string? TryExtractFromShafts(string text)
        => HoldingDistribution.ShaftCandidateScanner.TryExtractFromShafts(text);

    private static string? TryFindPoint(string[] lines, string label)
        => HoldingDistribution.ShaftCandidateScanner.TryFindPoint(lines, label);

    private static string? FindNextToken(string[] lines, int startIndex, string pattern)
        => HoldingDistribution.ShaftCandidateScanner.FindNextToken(lines, startIndex, pattern);


    private static void WritePdfPages(string sourcePdfPath, IReadOnlyList<int> pages, string destPdfPath)
    {
        PdfImportSafetyPolicy.ThrowIfFileTooLarge(sourcePdfPath);
        using var doc = PdfDocument.Open(sourcePdfPath);
        PdfImportSafetyPolicy.ThrowIfTooManyPages(doc.NumberOfPages);
        using var builder = new PdfDocumentBuilder();

        foreach (var pageNumber in pages)
            builder.AddPage(doc, pageNumber);

        var bytes = builder.Build();
        File.WriteAllBytes(destPdfPath, bytes);
    }


    internal static void AppendPdfFile(string targetPdfPath, string additionalPdfPath, bool removeAdditionalWhenMoved)
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
            PdfImportSafetyPolicy.ThrowIfFileTooLarge(targetPdfPath);
            PdfImportSafetyPolicy.ThrowIfFileTooLarge(additionalPdfPath);
            using (var targetDoc = PdfDocument.Open(targetPdfPath))
            using (var additionalDoc = PdfDocument.Open(additionalPdfPath))
            using (var builder = new PdfDocumentBuilder())
            {
                PdfImportSafetyPolicy.ThrowIfTooManyPages(targetDoc.NumberOfPages);
                PdfImportSafetyPolicy.ThrowIfTooManyPages(additionalDoc.NumberOfPages);
                foreach (var page in targetDoc.GetPages())
                    builder.AddPage(targetDoc, page.Number);

                foreach (var page in additionalDoc.GetPages())
                    builder.AddPage(additionalDoc, page.Number);

                var bytes = builder.Build();
                File.WriteAllBytes(mergedTempPath, bytes);
            }

            AtomicPdfFileReplacer.ReplaceValidated(mergedTempPath, targetPdfPath);

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


    internal static IReadOnlyList<PdfTextReplacement> BuildRenameReplacements(string oldValue, string newValue)
    {
        if (!PdfTextLayerRewriter.CanRewrite(oldValue, newValue))
            return Array.Empty<PdfTextReplacement>();

        return new[] { new PdfTextReplacement(oldValue.Trim(), newValue.Trim()) };
    }

    internal static PdfCorrectionResult TryCorrectPdfTextLayer(
        string sourcePdfPath,
        IReadOnlyList<PdfTextReplacement> replacements)
    {
        var targets = replacements
            .Select(item => new PdfTextReplacementTarget(item.SearchText, item.ReplacementText))
            .ToList();
        var rewrite = PdfTextLayerRewriter.TryRewrite(sourcePdfPath, targets);

        return new PdfCorrectionResult(
            rewrite.Success,
            rewrite.Corrected,
            rewrite.OutputPdfPath,
            rewrite.MatchCount,
            rewrite.PageCount,
            rewrite.Message);
    }

    /// <summary>
    /// Schreibt die Haltungsnummer im Text-Layer der angegebenen PDF-Dateien um (in-place).
    /// Wird beim In-App-Umbenennen einer Haltung genutzt, damit die Nummer im Protokoll-PDF
    /// sofort mitzieht (nicht erst bei der naechsten Verteilung). Reuse der Verteilungs-Korrektur:
    /// visueller Overlay (weisses Rechteck + neue Nummer); der Original-Text-Layer bleibt darunter
    /// erhalten. Best-effort: Bild-/Scan-PDFs ohne Text-Treffer bleiben unveraendert (Skipped).
    /// </summary>
    /// <returns>(Rewritten, Skipped, Failed) je PDF.</returns>
    public static (int Rewritten, int Skipped, int Failed) RewriteHoldingInPdfFiles(
        IReadOnlyList<string> pdfPaths, string oldHolding, string newHolding)
    {
        if (pdfPaths is null || pdfPaths.Count == 0)
            return (0, 0, 0);

        if (!PdfTextLayerRewriter.CanRewrite(oldHolding, newHolding))
            return (0, 0, 0);

        int rewritten = 0, skipped = 0, failed = 0;
        foreach (var pdf in pdfPaths)
        {
            if (string.IsNullOrWhiteSpace(pdf) || !File.Exists(pdf))
            {
                skipped++;
                continue;
            }

            string? temporaryPdf = null;
            try
            {
                var res = PdfTextLayerRewriter.TryRewriteHoldingNumber(pdf, oldHolding, newHolding);
                if (!res.Success)
                {
                    failed++;
                    continue;
                }

                if (!res.Corrected)
                {
                    skipped++; // kein Text-Treffer (z.B. Bild-/Scan-PDF)
                    continue;
                }

                temporaryPdf = res.OutputPdfPath;
                if (string.IsNullOrWhiteSpace(temporaryPdf)
                    || string.Equals(temporaryPdf, pdf, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(temporaryPdf))
                {
                    failed++;
                    continue;
                }

                AtomicPdfFileReplacer.ReplaceValidated(temporaryPdf, pdf);
                rewritten++;
            }
            catch
            {
                failed++;
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(temporaryPdf)
                        && !string.Equals(temporaryPdf, pdf, StringComparison.OrdinalIgnoreCase)
                        && File.Exists(temporaryPdf))
                        File.Delete(temporaryPdf);
                }
                catch
                {
                    // Best-effort: Ein Aufraeumfehler darf die Original-PDF nicht gefaehrden.
                }
            }
        }

        return (rewritten, skipped, failed);
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


    private static IEnumerable<string> EnumeratePhotoLookupKeys(string? raw)
        => HoldingDistribution.PhotoTokenNormalizer.EnumeratePhotoLookupKeys(raw);
}
