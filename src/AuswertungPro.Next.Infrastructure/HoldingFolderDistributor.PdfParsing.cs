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

    private static readonly Regex PdfFilenamePairRegex = new(
        @"(?:\d{2,}\.\d{2,}|\d{4,})\s*[-_]\s*(?:\d{2,}\.\d{2,}|\d{4,})",
        RegexOptions.Compiled);

    // Hotpath-Regex: TryExtractDichtheitShafts
    // Schacht-ID-Pattern: numerisch (81150, 42.046) oder alphanumerisch (S42.123, KS-0815)


    // Hotpath-Regex: TryExtractDichtheitShafts
    // Schacht-ID-Pattern: numerisch (81150, 42.046) oder alphanumerisch (S42.123, KS-0815)
    private const string SchachtIdPat = @"[A-Za-z]{0,3}[\-]?\d{2,}(?:[.\-]\d{2,})?";

    private static readonly Regex DichtheitUpperRx = new(
        @"oberer\s*Schacht\s*[:\-]?\s*(?<v>" + SchachtIdPat + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DichtheitLowerRx = new(
        @"unterer\s*Schacht\s*[:\-]?\s*(?<v>" + SchachtIdPat + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SchachtObenRx = new(
        @"Schacht\s*oben\s*[:\-]?\s*(?<v>" + SchachtIdPat + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex SchachtUntenRx = new(
        @"Schacht\s*unten\s*[:\-]?\s*(?<v>" + SchachtIdPat + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Hotpath-Regex: TryFindInspectionDate / TryFindSchachtDate / TryExtractDateFromFormEntries


    // Hotpath-Regex: TryFindInspectionDate / TryFindSchachtDate / TryExtractDateFromFormEntries
    private static readonly Regex InspectionDateRx = new(
        @"(\d{2}\.\d{2}\.\d{2,4}|\d{4}-\d{2}-\d{2})",
        RegexOptions.Compiled);

    private static readonly Regex FormEntryDateRx = new(
        @"\b(?<d>\d{2}[./-]\d{2}[./-]\d{2,4}|\d{4}[./-]\d{2}[./-]\d{2})\b",
        RegexOptions.Compiled);

    private static readonly Regex LabeledDateRx = new(
        @"Datum\s*[:\-]?\s*(?<date>\d{2}[./-]\d{2}[./-]\d{2,4})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex GenericDateRx = new(
        @"\b(?<date>\d{2}[./-]\d{2}[./-]\d{2,4})\b",
        RegexOptions.Compiled);

    // Hotpath-Regex: TryFindHaltungId


    // Hotpath-Regex: TryFindHaltungId
    private static readonly Regex HaltungIdRx = new(
        @"(?im)^.*Haltung.*[:\-\s]+(?<id>[\d\.\- ]{5,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex GeneralPairRx = new(
        @"((?:\d{2,}\.\d{2,}|\d{4,})\s*[-]\s*(?:\d{2,}\.\d{2,}|\d{4,}))(?=[^\d]|$)",
        RegexOptions.Compiled);

    private static readonly Regex GluedDatePairRx = new(
        @"((?:\d{2,}\.\d{2,}|\d{4,})\s*-\s*(?:\d{2,}\.\d{2,}|\d{4,}?))(?=\d{2}\.\d{2}\.\d{2,4}|\d{4}-\d{2}-\d{2})",
        RegexOptions.Compiled);

    private static readonly Regex ConcatenatedIdRx = new(
        @"(?:Haltungsname|Schacht\s*oben|Schacht\s*unten|Oberer\s*Punkt|Unterer\s*Punkt).{0,300}?(?<id>\d{10})(?!\d)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);


    private sealed record PageInfo(int PageNumber, string Text, string SourcePath);

    private sealed record PdfPageChunk(IReadOnlyList<int> Pages, ParsedPdf Parsed);


    private static IReadOnlyList<PageInfo> ReadPdfPages(string pdfPath)
    {
        try
        {
            var extraction = PdfTextExtractor.ExtractPages(pdfPath);
            if (extraction.Pages.Count == 0)
                return ReadPdfPagesWithPdfPig(pdfPath);

            var pages = new List<PageInfo>(extraction.Pages.Count);
            for (var i = 0; i < extraction.Pages.Count; i++)
            {
                var text = (extraction.Pages[i] ?? "").Replace("\r\n", "\n").Trim();
                pages.Add(new PageInfo(i + 1, text, pdfPath));
            }
            return pages;
        }
        catch
        {
            return ReadPdfPagesWithPdfPig(pdfPath);
        }
    }


    private static IReadOnlyList<PageInfo> ReadPdfPagesWithPdfPig(string pdfPath)
    {
        // PdfTextExtractor nutzt Layout-erhaltende Extraktion (Letter-by-Letter),
        // die Zeilen/Spalten korrekt rekonstruiert. Direkt page.Text ist unbrauchbar
        // weil es keine Zeilenumbrueche oder Abstande erhaelt.
        try
        {
            var extraction = PdfTextExtractor.ExtractPages(pdfPath);
            var pages = new List<PageInfo>(extraction.Pages.Count);
            for (var i = 0; i < extraction.Pages.Count; i++)
            {
                var text = (extraction.Pages[i] ?? "").Replace("\r\n", "\n").Trim();
                pages.Add(new PageInfo(i + 1, text, pdfPath));
            }
            return pages;
        }
        catch
        {
            // Absoluter Fallback: page.Text (besser als nichts)
            var pages = new List<PageInfo>();
            using var doc = PdfDocument.Open(pdfPath);
            var pageNumber = 0;
            foreach (var page in doc.GetPages())
            {
                pageNumber++;
                var text = (page.Text ?? "").Replace("\r\n", "\n").Trim();
                pages.Add(new PageInfo(pageNumber, text, pdfPath));
            }
            return pages;
        }
    }


    private static string ReadPdfText(string pdfPath)
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(pdfPath);
        foreach (var page in doc.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

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

    private sealed record DichtheitPageResult(
        int MainPage,
        IReadOnlyList<int> PageNumbers,
        string? HaltungId,
        string DateStamp,
        bool IsSchacht);

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
    private static IReadOnlyList<DichtheitPageResult> ExtractDichtheitPerPage(
        IReadOnlyList<PageInfo> pages,
        Project? project,
        string destGemeindeFolder)
    {
        var results = new List<DichtheitPageResult>();

        foreach (var page in pages)
        {
            var text = page.Text;

            // Kontrollinformation = Folgeseite einer Pruefung → zur vorherigen anhaengen
            if (text.Contains("Kontrollinformation"))
            {
                if (results.Count > 0)
                {
                    var prev = results[^1];
                    var extPages = new List<int>(prev.PageNumbers) { page.PageNumber };
                    results[^1] = prev with { PageNumbers = extPages };
                }
                continue;
            }

            // Datum aus Seiteninhalt (YYYY/MM/DD Format, typisch fuer KIT)
            var dateMatch = Regex.Match(text, @"(\d{4})/(\d{2})/(\d{2})");
            var dateStamp = dateMatch.Success
                ? $"{dateMatch.Groups[1].Value}{dateMatch.Groups[2].Value}{dateMatch.Groups[3].Value}"
                : TryFindInspectionDate(text)?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "00000000";

            // Schachtpruefung? (Label: "Prüfgegenstand / Schacht")
            bool isSchacht = text.Contains("Prufgegenstand / Schacht", StringComparison.OrdinalIgnoreCase)
                          || text.Contains("Pruefgegenstand / Schacht", StringComparison.OrdinalIgnoreCase)
                          || text.Contains("Prüfgegenstand / Schacht", StringComparison.OrdinalIgnoreCase);

            string? haltungId = null;

            // Haltungspaar suchen: zwei 5-stellige Nummern auf einer Zeile
            // (OCR kann ^ als diverse Zeichen rendern — deshalb robust: 2 Nummern auf gleicher Zeile)
            foreach (var line in text.Split('\n'))
            {
                // Zeilen mit bekannten Nicht-Haltungs-Mustern ueberspringen
                if (line.Contains("Ebikon", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("Altdorf", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("GPS", StringComparison.OrdinalIgnoreCase))
                    continue;
                // "gepruft bei 40693,6473" — nur eine Nummer vor Komma, kein Paar
                if (Regex.IsMatch(line, @"gepr[uü]ft\s+bei", RegexOptions.IgnoreCase))
                    continue;

                var nums = Regex.Matches(line, @"\b(\d{5})\b");
                if (nums.Count >= 2)
                {
                    var a = nums[0].Groups[1].Value;
                    var b = nums[1].Groups[1].Value;
                    if (!string.Equals(a, b, StringComparison.Ordinal))
                    {
                        haltungId = ResolveDichtheitHaltungOrder(a, b, project, destGemeindeFolder)
                                    ?? $"{a}-{b}";
                        break;
                    }
                }
            }

            // Schacht: einzelne Nummer neben "Strang"
            if (haltungId == null && isSchacht)
            {
                var schachtMatch = Regex.Match(text, @":\s*(\d{5})\s*:?\s*Strang", RegexOptions.IgnoreCase);
                if (schachtMatch.Success)
                    haltungId = $"Schacht_{schachtMatch.Groups[1].Value}";
            }

            // Standard-Fallbacks
            if (haltungId == null)
            {
                var (shA, shB) = TryExtractDichtheitShafts(text);
                if (!string.IsNullOrWhiteSpace(shA) && !string.IsNullOrWhiteSpace(shB))
                    haltungId = ResolveDichtheitHaltungOrder(shA, shB, project, destGemeindeFolder)
                                ?? $"{shA}-{shB}";
            }
            if (haltungId == null)
                haltungId = TryExtractFromShafts(text);

            results.Add(new DichtheitPageResult(
                MainPage: page.PageNumber,
                PageNumbers: new List<int> { page.PageNumber },
                HaltungId: haltungId,
                DateStamp: dateStamp,
                IsSchacht: isSchacht && haltungId?.StartsWith("Schacht_") == true));
        }

        return results;
    }


    private static (string? A, string? B) TryExtractDichtheitShafts(string text)
    {
        // "oberer Schacht: XXXXX" / "unterer Schacht: XXXXX"
        var upperM = DichtheitUpperRx.Match(text);
        var lowerM = DichtheitLowerRx.Match(text);
        if (upperM.Success && lowerM.Success)
        {
            var up = upperM.Groups["v"].Value;
            var low = lowerM.Groups["v"].Value;
            if (!string.Equals(up, low, StringComparison.OrdinalIgnoreCase))
                return (up, low);
        }

        // Fallback: "Schacht oben" / "Schacht unten"
        var upperS = SchachtObenRx.Match(text);
        var lowerS = SchachtUntenRx.Match(text);
        if (upperS.Success && lowerS.Success)
        {
            var up = upperS.Groups["v"].Value;
            var low = lowerS.Groups["v"].Value;
            if (!string.Equals(up, low, StringComparison.OrdinalIgnoreCase))
                return (up, low);
        }

        return (null, null);
    }

    /// <summary>
    /// Ermittelt die korrekte Haltungs-ID-Reihenfolge fuer zwei Schachtnummern.
    /// Prueft A-B und B-A gegen Projekt-Daten und vorhandene Ordner im Zielverzeichnis.
    /// </summary>


    /// <summary>
    /// Ermittelt die korrekte Haltungs-ID-Reihenfolge fuer zwei Schachtnummern.
    /// Prueft A-B und B-A gegen Projekt-Daten und vorhandene Ordner im Zielverzeichnis.
    /// </summary>
    private static string ResolveDichtheitHaltungOrder(
        string a, string b, Project? project, string destGemeindeFolder)
    {
        var ab = $"{a}-{b}";
        var ba = $"{b}-{a}";

        // 1) Gegen Projekt-Haltungsnamen pruefen
        if (project is not null)
        {
            foreach (var rec in project.Data)
            {
                var name = rec.GetFieldValue("Haltungsname")?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;
                var normalized = NormalizeHaltungId(name);
                var stripped = StripNodePrefixes(SanitizePathSegment(normalized));

                if (string.Equals(normalized, ab, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(stripped, StripNodePrefixes(SanitizePathSegment(ab)), StringComparison.OrdinalIgnoreCase))
                    return ab;
                if (string.Equals(normalized, ba, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(stripped, StripNodePrefixes(SanitizePathSegment(ba)), StringComparison.OrdinalIgnoreCase))
                    return ba;
            }
        }

        // 2) Gegen vorhandene Ordner im Ziel pruefen
        if (Directory.Exists(destGemeindeFolder))
        {
            var abSanitized = SanitizePathSegment(NormalizeHaltungId(ab));
            var baSanitized = SanitizePathSegment(NormalizeHaltungId(ba));
            var abStripped = StripNodePrefixes(abSanitized);
            var baStripped = StripNodePrefixes(baSanitized);

            foreach (var dir in Directory.EnumerateDirectories(destGemeindeFolder))
            {
                var dirName = Path.GetFileName(dir) ?? "";
                var dirStripped = StripNodePrefixes(dirName);

                if (string.Equals(dirName, abSanitized, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(dirStripped, abStripped, StringComparison.OrdinalIgnoreCase))
                    return ab;
                if (string.Equals(dirName, baSanitized, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(dirStripped, baStripped, StringComparison.OrdinalIgnoreCase))
                    return ba;
            }
        }

        // 3) Kein Treffer – PDF-Reihenfolge beibehalten (A-B)
        return ab;
    }

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


    private static ParsedShaftPdf ParseSchachtPdfPageWithOcrFallback(PageInfo page)
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
        var syntheticText = BuildSyntheticFormText(entries);
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
        var date = TryExtractDateFromFormEntries(entries);
        var shaft = TryExtractSchachtNumberFromFormEntries(entries);
        if (string.IsNullOrWhiteSpace(shaft) || date is null)
            return null;

        return new ParsedShaftPdf(true, "aus PDF-Formular", date, shaft);
    }


    private static string BuildSyntheticFormText(IReadOnlyList<PdfFormFieldEntry> entries)
    {
        var lines = new List<string>(entries.Count * 2);
        foreach (var entry in entries)
        {
            var labels = new[] { entry.PartialName, entry.AlternateName, entry.MappingName }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (labels.Count == 0)
            {
                lines.Add(entry.Value);
                continue;
            }

            foreach (var label in labels)
                lines.Add($"{label}: {entry.Value}");
        }

        return string.Join("\n", lines);
    }


    private static DateTime? TryExtractDateFromFormEntries(IReadOnlyList<PdfFormFieldEntry> entries)
    {
        var dateRx = FormEntryDateRx;

        // Prefer labeled date fields.
        foreach (var entry in entries)
        {
            var label = BuildFormEntryLabel(entry);
            if (!ContainsDateLabel(label))
                continue;

            var m = dateRx.Match(entry.Value);
            if (m.Success && TryParseDateString(m.Groups["d"].Value, out var parsed))
                return parsed;
        }

        // Fallback: first parseable date from any value.
        foreach (var entry in entries)
        {
            var m = dateRx.Match(entry.Value);
            if (m.Success && TryParseDateString(m.Groups["d"].Value, out var parsed))
                return parsed;
        }

        return null;
    }


    private static string? TryExtractSchachtNumberFromFormEntries(IReadOnlyList<PdfFormFieldEntry> entries)
    {
        // Prefer explicit labels.
        foreach (var entry in entries)
        {
            var label = BuildFormEntryLabel(entry);
            if (!ContainsSchachtNumberLabel(label))
                continue;

            var candidate = ExtractShaftNumberToken(entry.Value);
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        // Fallback: strict numeric tokens only.
        foreach (var entry in entries)
        {
            var candidate = ExtractShaftNumberToken(entry.Value);
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return null;
    }


    private static string BuildFormEntryLabel(PdfFormFieldEntry entry)
    {
        return string.Join(" ",
            new[] { entry.PartialName, entry.AlternateName, entry.MappingName }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim()));
    }


    private static bool ContainsDateLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return false;

        return label.Contains("datum", StringComparison.OrdinalIgnoreCase)
               || label.Contains("date", StringComparison.OrdinalIgnoreCase);
    }


    private static bool ContainsSchachtNumberLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return false;

        return label.Contains("schacht", StringComparison.OrdinalIgnoreCase)
               || label.Contains("nummer", StringComparison.OrdinalIgnoreCase)
               || Regex.IsMatch(label, @"\bnr\.?\b", RegexOptions.IgnoreCase);
    }


    private static string? ExtractShaftNumberToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Prefer standalone numeric values, common for Schachtnummer forms.
        var direct = Regex.Match(value.Trim(), @"^(?<nr>\d{3,8})$");
        if (direct.Success)
            return direct.Groups["nr"].Value;

        var any = Regex.Match(value, @"\b(?<nr>\d{3,8})\b");
        if (!any.Success)
            return null;

        var token = any.Groups["nr"].Value;
        // Avoid obvious date fragments (e.g. year values).
        if (token.Length == 4 && int.TryParse(token, out var year) && year >= 1900 && year <= 2100)
            return null;

        return token;
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


    // Schacht-Wert: numerisch (81150, 42.046) ODER alphanumerisch (S42.123, KS-0815, A1-B2)
    private static readonly Regex WinCanValueRegex = new(
        @"[A-Za-z]{0,3}[\-]?\d{2,}(?:[.\-]\d{2,})?",
        RegexOptions.Compiled);

    private static readonly Regex WinCanUpperLabelRegex = new(
        @"\b(Schacht\s*oben|Knoten\s*oben|Oberer\s*(?:Punkt|Schacht)|Startschacht|Von" +
        @"|Anfangsschacht|Start\s*Schacht|Schacht\s*(?:Nr\.?\s*)?(?:A|1|Start|Anfang)" +
        @"|Pruefstrecke\s*von|Haltung\s*von|Leitung\s*von|Strecke\s*von" +
        @"|Anfangspunkt|Startpunkt)\b[:\s]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex WinCanLowerLabelRegex = new(
        @"\b(Schacht\s*unten|Knoten\s*unten|Unterer\s*(?:Punkt|Schacht)|Endschacht|Nach" +
        @"|Zielschacht|End\s*Schacht|Schacht\s*(?:Nr\.?\s*)?(?:B|2|End|Ziel)" +
        @"|Pruefstrecke\s*bis|Haltung\s*bis|Leitung\s*bis|Strecke\s*bis" +
        @"|Endpunkt|Zielpunkt)\b[:\s]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);


    private static string NormalizeLine(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        s = s.Replace('\u00A0', ' ');
        s = Regex.Replace(s, @"[ \t]+", " ");
        return s.Trim();
    }


    private static string? TryGetValueAfterLabel(IReadOnlyList<string> lines, Regex labelRegex, Regex valueRegex)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var line = NormalizeLine(lines[i]);
            if (line.Length == 0) continue;

            // 1) Label + Wert in derselben Zeile
            var m = labelRegex.Match(line);
            if (m.Success)
            {
                var tail = NormalizeLine(line.Substring(m.Index + m.Length));
                var v1 = valueRegex.Match(tail);
                if (v1.Success) return v1.Value;

                // 2) Wert steht in nächster Zeile
                if (i + 1 < lines.Count)
                {
                    var next = NormalizeLine(lines[i + 1]);
                    var v2 = valueRegex.Match(next);
                    if (v2.Success) return v2.Value;
                }

                // 3) Manchmal noch eine Zeile weiter (PDF-Layout)
                if (i + 2 < lines.Count)
                {
                    var next2 = NormalizeLine(lines[i + 2]);
                    var v3 = valueRegex.Match(next2);
                    if (v3.Success) return v3.Value;
                }
            }

            // 4) “Zerhacktes” Label über Zeilengrenze
            if (i + 1 < lines.Count)
            {
                var joined = NormalizeLine(line + " " + lines[i + 1]);
                var mj = labelRegex.Match(joined);
                if (mj.Success)
                {
                    var tail = NormalizeLine(joined.Substring(mj.Index + mj.Length));
                    var vj = valueRegex.Match(tail);
                    if (vj.Success) return vj.Value;

                    if (i + 2 < lines.Count)
                    {
                        var vNext = valueRegex.Match(NormalizeLine(lines[i + 2]));
                        if (vNext.Success) return vNext.Value;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts haltung pair from "Haltungsinspektion" or "Haltungsbilder" header lines.
    /// Both Fretz page 1 (Haltungsinspektion) and page 2 (Haltungsbilder) use this format.
    /// </summary>


    /// <summary>
    /// Extracts haltung pair from "Haltungsinspektion" or "Haltungsbilder" header lines.
    /// Both Fretz page 1 (Haltungsinspektion) and page 2 (Haltungsbilder) use this format.
    /// </summary>
    private static string? TryExtractFromHeader(string text)
    {
        var headerRx = new Regex(
            @"Haltungs(?:\s*inspektion|bilder)\s*[-–—]\s*(?:\d{2}\.\d{2}\.\d{2,4}|\d{4}-\d{2}-\d{2})\s*[-–—]\s*((?:\d{2,}\.\d{2,}|\d{4,})\s*[-/]\s*(?:\d{2,}\.\d{2,}|\d{4,}))",
            RegexOptions.IgnoreCase);
        var m = headerRx.Match(text);
        if (!m.Success) return null;
        var haltung = NormalizeHaltungId(m.Groups[1].Value);
        return IsValidHaltungId(haltung) ? haltung : null;
    }

    /// <summary>
    /// Returns true if the first part of a haltung pair looks like a date fragment (MM.YYYY).
    /// This prevents "09.2025-80638" from being treated as a valid haltung.
    /// </summary>


    /// <summary>
    /// Returns true if the first part of a haltung pair looks like a date fragment (MM.YYYY).
    /// This prevents "09.2025-80638" from being treated as a valid haltung.
    /// </summary>
    private static bool LooksLikeDateFragment(string haltungId)
    {
        if (string.IsNullOrWhiteSpace(haltungId)) return false;
        // Match patterns like "09.2025-XXXXX" where "09.2025" is actually a date fragment
        var dateFragRx = new Regex(@"^(\d{2}\.\d{4})-");
        var m = dateFragRx.Match(haltungId);
        if (!m.Success) return false;
        // Check if the first number looks like MM.YYYY (month 01-12, year 2000-2099)
        var parts = m.Groups[1].Value.Split('.');
        if (parts.Length == 2
            && int.TryParse(parts[0], out var month) && month >= 1 && month <= 12
            && int.TryParse(parts[1], out var year) && year >= 2000 && year <= 2099)
            return true;
        return false;
    }


    private static string? TryExtractFromShafts(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');

        // Frueh-Erkennung: Volles Haltungspaar direkt nach "Oberer/Unterer Schacht" oder "Oberer/Unterer Punkt"
        // Fretz-Stammdaten-Layout: "Oberer Schacht  42046-41412" → ganzes Paar extrahieren
        var fullPairAfterSchacht = Regex.Match(text,
            @"(?:Oberer|Unterer)\s*(?:Schacht|Punkt)[^\S\n]*(?<pair>(?:\d{2,}\.\d{2,}|\d{4,})\s*-\s*(?:\d{2,}\.\d{2,}|\d{4,}))",
            RegexOptions.IgnoreCase);
        if (fullPairAfterSchacht.Success)
            return fullPairAfterSchacht.Groups["pair"].Value;

        // WinCAN: robust Label->Value extraction (Schacht oben/unten, Start/End, Von/Nach, Oberer/Unterer Schacht)
        var upper = TryGetValueAfterLabel(lines, WinCanUpperLabelRegex, WinCanValueRegex);
        var lower = TryGetValueAfterLabel(lines, WinCanLowerLabelRegex, WinCanValueRegex);
        if (!string.IsNullOrWhiteSpace(upper) && !string.IsNullOrWhiteSpace(lower))
        {
            if (!string.Equals(upper, lower, StringComparison.OrdinalIgnoreCase))
                return $"{upper}-{lower}";
        }

        // Inline layouts without line breaks (common in some PdfPig extracts).
        // [^\S\n]* statt \s* um Zeilenumbrueche nicht zu ueberqueren
        var pairAfterLowerPoint = Regex.Match(
            text,
            @"Unterer\s*Punkt[^\S\n]*(?<pair>(?:\d{2,}\.\d{2,}|\d{4,})\s*-\s*(?:\d{2,}\.\d{2,}|\d{4,}))",
            RegexOptions.IgnoreCase);
        if (pairAfterLowerPoint.Success)
            return pairAfterLowerPoint.Groups["pair"].Value;

        var upperPointInline = Regex.Match(text, @"Oberer\s*Punkt[^\S\n]+(?<v>\d{2,}\.\d{2,}|\d{4,})", RegexOptions.IgnoreCase);
        var lowerPointInline = Regex.Match(text, @"Unterer\s*Punkt[^\S\n]+(?<v>\d{2,}\.\d{2,}|\d{4,})", RegexOptions.IgnoreCase);
        if (upperPointInline.Success && lowerPointInline.Success)
        {
            var up = upperPointInline.Groups["v"].Value;
            var low = lowerPointInline.Groups["v"].Value;
            if (!string.Equals(up, low, StringComparison.OrdinalIgnoreCase))
                return $"{up}-{low}";
        }

        var upperSchachtInline = Regex.Match(text, @"Schacht\s*oben\s*[:\-]?[^\S\n]*(?<v>\d{2,}\.\d{2,}|\d{4,})", RegexOptions.IgnoreCase);
        var lowerSchachtInline = Regex.Match(text, @"Schacht\s*unten\s*[:\-]?[^\S\n]*(?<v>\d{2,}\.\d{2,}|\d{4,})", RegexOptions.IgnoreCase);
        if (upperSchachtInline.Success && lowerSchachtInline.Success)
        {
            var up = upperSchachtInline.Groups["v"].Value;
            var low = lowerSchachtInline.Groups["v"].Value;
            if (!string.Equals(up, low, StringComparison.OrdinalIgnoreCase))
                return $"{up}-{low}";
        }

        // Dichtheitspruefung Format: "oberer Schacht: XXXXX" / "unterer Schacht: XXXXX"
        // [^\S\n]* statt \s* um Zeilenumbrueche nicht zu ueberqueren
        var upperObererSchacht = Regex.Match(text, @"oberer\s*Schacht\s*[:\-]?[^\S\n]*(?<v>\d{2,}\.\d{2,}|\d{4,})", RegexOptions.IgnoreCase);
        var lowerUntererSchacht = Regex.Match(text, @"unterer\s*Schacht\s*[:\-]?[^\S\n]*(?<v>\d{2,}\.\d{2,}|\d{4,})", RegexOptions.IgnoreCase);
        if (upperObererSchacht.Success && lowerUntererSchacht.Success)
        {
            var up = upperObererSchacht.Groups["v"].Value;
            var low = lowerUntererSchacht.Groups["v"].Value;
            if (!string.Equals(up, low, StringComparison.OrdinalIgnoreCase))
                return $"{up}-{low}";
        }

        string? oben = null;
        string? unten = null;

        // Schacht-Nummer: numerisch (81150, 42.046) oder alphanumerisch (S42.123, KS-0815)
        var pointRx = new Regex(@"\b([A-Za-z]{0,3}[\-]?\d{2,}(?:[.\-]\d{2,})?)\b");
        // Volles Paar auf derselben Zeile — Trennzeichen: - , – , ^ , -^ , → , ->
        // KIT-Format: "40259 ^ 40260", "41412-^40859", "40260 -^ 40261"
        var pairRx = new Regex(@"(?<a>[A-Za-z]{0,3}[\-]?\d{2,}(?:[.\-]\d{2,})?)\s*[-–\^]+[>\s]*(?<b>[A-Za-z]{0,3}[\-]?\d{2,}(?:[.\-]\d{2,})?)");

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            bool isObererPunkt = line.Contains("Oberer", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("Punkt", StringComparison.OrdinalIgnoreCase);
            bool isUntererPunkt = line.Contains("Unterer", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("Punkt", StringComparison.OrdinalIgnoreCase);
            bool isObererSchacht = line.Contains("Oberer", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("Schacht", StringComparison.OrdinalIgnoreCase);
            bool isUntererSchacht = line.Contains("Unterer", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("Schacht", StringComparison.OrdinalIgnoreCase);

            // KIT/Dichtheitspruefung: "Prüfstrecke von", "Haltung von/bis", "Leitung"
            if (!isObererPunkt && !isObererSchacht)
            {
                isObererSchacht =
                    Regex.IsMatch(line, @"\b(?:Pruefstrecke|Haltung|Leitung|Strecke|Abschnitt)\s*von\b", RegexOptions.IgnoreCase)
                    || Regex.IsMatch(line, @"\b(?:Anfangsschacht|Startschacht|Anfangspunkt|Startpunkt)\b", RegexOptions.IgnoreCase);
            }
            if (!isUntererPunkt && !isUntererSchacht)
            {
                isUntererSchacht =
                    Regex.IsMatch(line, @"\b(?:Pruefstrecke|Haltung|Leitung|Strecke|Abschnitt)\s*bis\b", RegexOptions.IgnoreCase)
                    || Regex.IsMatch(line, @"\b(?:Endschacht|Zielschacht|Endpunkt|Zielpunkt)\b", RegexOptions.IgnoreCase);
            }

            bool isOberesLabel = isObererPunkt || isObererSchacht;
            bool isUnteresLabel = isUntererPunkt || isUntererSchacht;

            if (isOberesLabel || isUnteresLabel)
            {
                // Pruefe zuerst ob ein volles Paar auf der Zeile steht (z.B. "42046-41412")
                var pairMatch = pairRx.Match(line);
                if (pairMatch.Success)
                    return $"{pairMatch.Groups["a"].Value}-{pairMatch.Groups["b"].Value}";
            }

            if (isOberesLabel)
            {
                var m = pointRx.Match(line);
                if (m.Success)
                    oben = m.Groups[1].Value;
                else if (i + 1 < lines.Length)
                {
                    var nextM = pointRx.Match(lines[i + 1]);
                    if (nextM.Success)
                        oben = nextM.Groups[1].Value;
                }
            }

            if (isUnteresLabel)
            {
                var m = pointRx.Match(line);
                if (m.Success)
                    unten = m.Groups[1].Value;
                else if (i + 1 < lines.Length)
                {
                    var nextM = pointRx.Match(lines[i + 1]);
                    if (nextM.Success)
                        unten = nextM.Groups[1].Value;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(oben) && !string.IsNullOrWhiteSpace(unten))
        {
            if (!string.Equals(oben, unten, StringComparison.OrdinalIgnoreCase))
                return $"{oben}-{unten}";
        }
        
        return null;
    }

    
    private static string? TryFindPoint(string[] lines, string label)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!line.Contains(label, StringComparison.OrdinalIgnoreCase) || !line.Contains("Punkt", StringComparison.OrdinalIgnoreCase))
                continue;

            var inline = Regex.Match(line, @"\b(\d{2,}\.\d{3,}|\d{5,})\b");
            if (inline.Success)
                return inline.Groups[1].Value.Trim();

            var next = FindNextToken(lines, i + 1, @"\d{2,}\.\d{3,}|\d{5,}");
            if (!string.IsNullOrWhiteSpace(next))
                return next.Trim();
        }
        return null;
    }


    private static string? FindNextToken(string[] lines, int startIndex, string pattern)
    {
        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            var m = Regex.Match(line, pattern);
            if (m.Success)
                return m.Value;
            break;
        }
        return null;
    }


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


    private static IReadOnlyList<string> ExtractPhotoHintsFromPdf(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            return Array.Empty<string>();

        const int maxPagesWithLabeledHints = 2;
        var labeledPhotoKeys = new List<string>();
        var genericPhotoKeys = new List<string>();
        IReadOnlyList<PageInfo> pages;
        try
        {
            pages = ReadPdfPages(pdfPath);
        }
        catch
        {
            return Array.Empty<string>();
        }

        var labeledPages = 0;
        foreach (var page in pages)
        {
            var labeledCountBefore = labeledPhotoKeys.Count;
            foreach (Match m in PhotoAfterLabelRegex.Matches(page.Text))
            {
                AddPhotoLookupKeys(m.Groups["name"].Value, labeledPhotoKeys);
            }

            if (labeledPhotoKeys.Count > labeledCountBefore)
            {
                labeledPages++;
                if (labeledPages >= maxPagesWithLabeledHints)
                    break;
            }

            foreach (Match m in PhotoTokenRegex.Matches(page.Text))
            {
                AddPhotoLookupKeys(m.Groups["name"].Value, genericPhotoKeys);
            }
        }

        if (labeledPhotoKeys.Count > 0)
            return labeledPhotoKeys;

        return genericPhotoKeys;
    }


    private static void AddPhotoLookupKeys(string? raw, List<string> keys)
    {
        foreach (var key in EnumeratePhotoLookupKeys(raw))
        {
            if (!keys.Contains(key, StringComparer.OrdinalIgnoreCase))
                keys.Add(key);
        }
    }


    private static IEnumerable<string> EnumeratePhotoLookupKeys(string? raw)
    {
        var fileName = NormalizeVideoFileName(raw);
        if (string.IsNullOrWhiteSpace(fileName))
            yield break;

        var noExt = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var hasImageExt = HasImageExtension(fileName);

        if (hasImageExt)
            yield return fileName;

        if (!string.IsNullOrWhiteSpace(noExt))
            yield return noExt;

        var normalizedNoExt = NormalizePhotoToken(noExt);
        if (string.IsNullOrWhiteSpace(normalizedNoExt))
            yield break;

        if (!string.Equals(normalizedNoExt, noExt, StringComparison.OrdinalIgnoreCase))
            yield return normalizedNoExt;

        if (hasImageExt && !string.IsNullOrWhiteSpace(ext))
            yield return $"{normalizedNoExt}{ext}";
    }


    private static string TrimLeadingZerosValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var trimmed = value.TrimStart('0');
        return string.IsNullOrEmpty(trimmed) ? "0" : trimmed;
    }


    private static string? NormalizePhotoToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var m = Regex.Match(token, @"(?<a>\d{1,5})_(?<b>\d{1,5})_(?<c>\d{1,7})_(?<d>[A-Za-z])");
        if (!m.Success)
            return null;

        static string TrimLeadingZeros(string value)
        {
            var trimmed = value.TrimStart('0');
            return string.IsNullOrEmpty(trimmed) ? "0" : trimmed;
        }

        var a = TrimLeadingZeros(m.Groups["a"].Value);
        var b = TrimLeadingZeros(m.Groups["b"].Value);
        var c = TrimLeadingZeros(m.Groups["c"].Value);
        var d = char.ToUpperInvariant(m.Groups["d"].Value[0]);
        return $"{a}_{b}_{c}_{d}";
    }


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
}
