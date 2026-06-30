using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Reine String-/Regex-Heuristiken fuer Text-, Datums- und Haltungs-Parsing.
/// Extrahiert aus HoldingFolderDistributor.TextUtils – verhaltensneutral.
/// </summary>
internal static class HoldingTextParser
{
    private const string NodeTokenPattern = @"(?:\d{1,4}\.\d{2,12}|\d{4,16})";

    // ── Regex-Felder (verbatim aus HoldingFolderDistributor.PdfParsing/TextUtils) ─────

    private static readonly Regex InspectionDateRx = new(
        @"(\d{2}\.\d{2}\.\d{2,4}|\d{4}-\d{2}-\d{2})",
        RegexOptions.Compiled);

    private static readonly Regex LabeledDateRx = new(
        @"Datum\s*[:\-]?\s*(?<date>" + SewerTextPatterns.GermanDateCore + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex GenericDateRx = new(
        @"\b(?<date>" + SewerTextPatterns.GermanDateCore + @")\b",
        RegexOptions.Compiled);

    private static readonly Regex HaltungIdRx = new(
        @"(?im)^.*Haltung.*[:\-\s]+(?<id>[\d\.\- ]{5,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex GeneralPairRx = new(
        $@"({NodeTokenPattern}\s*[-/]\s*{NodeTokenPattern})(?=[^\d.]|$)",
        RegexOptions.Compiled);

    private static readonly Regex GluedDatePairRx = new(
        @"((?:\d{2,}\.\d{2,}|\d{4,})\s*-\s*(?:\d{2,}\.\d{2,}|\d{4,}?))(?=\d{2}\.\d{2}\.\d{2,4}|\d{4}-\d{2}-\d{2})",
        RegexOptions.Compiled);

    private static readonly Regex ConcatenatedIdRx = new(
        @"(?:Haltungsname|Schacht\s*oben|Schacht\s*unten|Oberer\s*Punkt|Unterer\s*Punkt).{0,300}?(?<id>\d{10})(?!\d)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex PdfFilenamePairRegex = new(
        $@"{NodeTokenPattern}\s*[-_]\s*{NodeTokenPattern}",
        RegexOptions.Compiled);

    // ── Oeffentliche Methoden ────────────────────────────────────────────────────────

    /// <summary>
    /// Sucht eine Haltungs-ID im Text (Schacht-Felder, Haltung-Zeilen, generische Paare).
    /// </summary>
    internal static string? TryFindHaltungId(string text)
    {
        var idRx = HaltungIdRx;
        var generalPairRx = GeneralPairRx;
        var gluedDatePairRx = GluedDatePairRx;
        var lines = text.Replace("\r\n", "\n").Split('\n');

        var labeled = TryFindLabeledHoldingOrLineId(lines, idRx, generalPairRx);
        if (!string.IsNullOrWhiteSpace(labeled))
            return labeled;

        // Prioritaet 1: Schacht-Muster (zuverlaessigste Quelle)
        var shaftPattern = ShaftCandidateScanner.TryExtractFromShafts(text);
        if (!string.IsNullOrWhiteSpace(shaftPattern))
        {
            var normalized = HoldingIdNormalizer.NormalizeHaltungId(shaftPattern);
            if (HoldingIdNormalizer.IsValidHaltungId(normalized))
                return normalized;
        }

        // Prioritaet 1b: Paar direkt an ein Datum geklebt (z.B. 23022-2159822.04.2014)
        var glued = gluedDatePairRx.Match(text);
        if (glued.Success)
        {
            var normalized = HoldingIdNormalizer.NormalizeHaltungId(glued.Groups[1].Value);
            if (HoldingIdNormalizer.IsValidHaltungId(normalized))
                return normalized;
        }

        // WinCAN-Kompaktzeile mit "KS Nr." (nicht-numerischer Startknoten)
        var ksCompact = Regex.Match(text, @"KS\s*Nr\.?\s*(?<digits>\d{10,13})", RegexOptions.IgnoreCase);
        if (ksCompact.Success)
        {
            var ksCandidate = TryParseKsCompactHoldingDigits(ksCompact.Groups["digits"].Value);
            if (!string.IsNullOrWhiteSpace(ksCandidate))
                return ksCandidate;
        }

        // Prioritaet 1c: zusammengesetztes numerisches Paar ohne Bindestrich (2302221598 -> 23022-21598)
        var concatenated = ConcatenatedIdRx.Match(text);
        if (concatenated.Success)
        {
            var raw = concatenated.Groups["id"].Value;
            var candidate = $"{raw.Substring(0, 5)}-{raw.Substring(5, 5)}";
            var normalized = HoldingIdNormalizer.NormalizeHaltungId(candidate);
            if (HoldingIdNormalizer.IsValidHaltungId(normalized))
                return normalized;
        }

        // Prioritaet 2: Zeilen mit "Haltung"
        foreach (var line in lines)
        {
            if (!line.Contains("Haltung", StringComparison.OrdinalIgnoreCase))
                continue;

            var m = idRx.Match(line);
            if (m.Success)
            {
                var id = m.Groups["id"].Value?.Trim();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    var normalized = HoldingIdNormalizer.NormalizeHaltungId(id);
                    if (HoldingIdNormalizer.IsValidHaltungId(normalized))
                        return normalized;
                }
            }

            var inline = generalPairRx.Match(line);
            if (inline.Success)
            {
                var normalized = HoldingIdNormalizer.NormalizeHaltungId(inline.Groups[1].Value);
                if (HoldingIdNormalizer.IsValidHaltungId(normalized))
                    return normalized;
            }
        }

        // Prioritaet 4: "Leitung"-Feld
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!line.Contains("Leitung", StringComparison.OrdinalIgnoreCase))
                continue;

            var inline = generalPairRx.Match(line);
            if (inline.Success)
            {
                var normalized = HoldingIdNormalizer.NormalizeHaltungId(inline.Groups[1].Value);
                if (HoldingIdNormalizer.IsValidHaltungId(normalized))
                    return normalized;
            }

            var nextId = ShaftCandidateScanner.FindNextToken(lines, i + 1, @"(?:\d{2,}\.\d{2,}|\d{4,})\s*[-/]\s*(?:\d{2,}\.\d{2,}|\d{4,})");
            if (!string.IsNullOrWhiteSpace(nextId))
            {
                var normalized = HoldingIdNormalizer.NormalizeHaltungId(nextId);
                if (HoldingIdNormalizer.IsValidHaltungId(normalized))
                    return normalized;
            }
        }

        var oberer = ShaftCandidateScanner.TryFindPoint(lines, "Oberer");
        var unterer = ShaftCandidateScanner.TryFindPoint(lines, "Unterer");
        if (!string.IsNullOrWhiteSpace(oberer) && !string.IsNullOrWhiteSpace(unterer))
        {
            var combined = HoldingIdNormalizer.NormalizeHaltungId($"{oberer}-{unterer}");
            if (HoldingIdNormalizer.IsValidHaltungId(combined))
                return combined;
        }

        var loose = generalPairRx.Match(text);
        if (loose.Success)
        {
            var normalized = HoldingIdNormalizer.NormalizeHaltungId(loose.Groups[1].Value);
            if (HoldingIdNormalizer.IsValidHaltungId(normalized) && !ShaftCandidateScanner.LooksLikeDateFragment(normalized))
                return normalized;
        }

        var anyIdLine = lines.FirstOrDefault(l => Regex.IsMatch(l, @"^\s*(?:\d{2,}\.\d{2,}|\d{4,})\s*[-/]\s*(?:\d{2,}\.\d{2,}|\d{4,})\s*$"));
        if (!string.IsNullOrWhiteSpace(anyIdLine))
        {
            var normalized = HoldingIdNormalizer.NormalizeHaltungId(anyIdLine.Trim());
            if (HoldingIdNormalizer.IsValidHaltungId(normalized))
                return normalized;
        }

        return null;
    }

    private static string? TryFindLabeledHoldingOrLineId(IEnumerable<string> lines, Regex idRx, Regex generalPairRx)
    {
        foreach (var line in lines)
        {
            var isHoldingLine = line.Contains("Haltung", StringComparison.OrdinalIgnoreCase);
            var isLineLine = line.Contains("Leitung", StringComparison.OrdinalIgnoreCase);
            if (!isHoldingLine && !isLineLine)
                continue;

            var m = idRx.Match(line);
            if (m.Success)
            {
                var id = m.Groups["id"].Value?.Trim();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    var normalized = HoldingIdNormalizer.NormalizeHaltungId(id);
                    if (HoldingIdNormalizer.IsValidHaltungId(normalized))
                        return normalized;
                }
            }

            var inline = generalPairRx.Match(line);
            if (inline.Success)
            {
                var normalized = HoldingIdNormalizer.NormalizeHaltungId(inline.Groups[1].Value);
                if (HoldingIdNormalizer.IsValidHaltungId(normalized))
                    return normalized;
            }
        }

        return null;
    }

    /// <summary>
    /// Versucht, aus einer langen Ziffernfolge (WinCAN KS Nr.) eine Haltungs-ID zu lesen.
    /// </summary>
    internal static string? TryParseKsCompactHoldingDigits(string rawDigits)
    {
        if (string.IsNullOrWhiteSpace(rawDigits))
            return null;

        var digits = Regex.Replace(rawDigits, @"\D", "");
        if (digits.Length < 10)
            return null;

        var candidates = new List<(int Score, string Value)>();

        for (var prefixLen = 0; prefixLen <= 3; prefixLen++)
        {
            var remaining = digits.Length - prefixLen;
            if (remaining < 10)
                continue;

            if (remaining == 11)
            {
                var a = digits.Substring(prefixLen, 5);
                var bRaw = digits.Substring(prefixLen + 5, 6);
                if (bRaw.StartsWith("0", StringComparison.Ordinal))
                {
                    var b = PhotoTokenNormalizer.TrimLeadingZerosValue(bRaw);
                    var candidate = HoldingIdNormalizer.NormalizeHaltungId($"{a}-{b}");
                    if (HoldingIdNormalizer.IsValidHaltungId(candidate))
                        candidates.Add((2, candidate));
                }
            }

            if (remaining == 10)
            {
                var a = digits.Substring(prefixLen, 5);
                var b = digits.Substring(prefixLen + 5, 5);
                var candidate = HoldingIdNormalizer.NormalizeHaltungId($"{a}-{PhotoTokenNormalizer.TrimLeadingZerosValue(b)}");
                if (HoldingIdNormalizer.IsValidHaltungId(candidate))
                    candidates.Add((1, candidate));
            }
        }

        if (candidates.Count == 0)
            return null;

        return candidates
            .OrderByDescending(c => c.Score)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>
    /// Sucht das Inspektionsdatum im Text (Haltungsinspektion-Kopfzeile, Datums-Label, generisches Datum).
    /// </summary>
    internal static DateTime? TryFindInspectionDate(string text)
    {
        var dateRx = InspectionDateRx;
        var lines = text.Replace("\r\n", "\n").Split('\n');

        // Prioritaet 1: Datum in Kopfzeile
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Contains("Haltungsinspektion", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Haltungsbilder", StringComparison.OrdinalIgnoreCase))
            {
                var mHeader = dateRx.Match(line);
                if (mHeader.Success && HoldingTextNormalizer.TryParseDateString(mHeader.Groups[1].Value, out var dh))
                    return dh;
            }
        }

        // Prioritaet 2: Datum neben Inspektionsdatum-Label
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Contains("Gedruckt", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!line.Contains("Insp", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("Inspekt", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("Datum", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("Aufnahme", StringComparison.OrdinalIgnoreCase))
                continue;

            var m = dateRx.Match(line);
            if (m.Success && HoldingTextNormalizer.TryParseDateString(m.Groups[1].Value, out var d1))
                return d1;

            var prev = FindNearbyDate(lines, i - 1, -1, 3, dateRx);
            if (prev is not null) return prev;
            var next = FindNearbyDate(lines, i + 1, 1, 3, dateRx);
            if (next is not null) return next;
        }

        // Prioritaet 3: Beliebiges Datum (Gedruckt-Zeilen ausschliessen)
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Contains("Gedruckt", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("erstellt", StringComparison.OrdinalIgnoreCase))
                continue;

            var any = dateRx.Match(line);
            if (any.Success && HoldingTextNormalizer.TryParseDateString(any.Groups[1].Value, out var d2))
            {
                if (d2.Year >= 2000 && d2.Year <= 2030)
                    return d2;
            }
        }

        return null;
    }

    /// <summary>
    /// Sucht das Datum in einem Schachtprotokoll-Text.
    /// </summary>
    internal static DateTime? TryFindSchachtDate(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var labeledDateRx = LabeledDateRx;
        foreach (var line in lines)
        {
            var m = labeledDateRx.Match(line);
            if (!m.Success)
                continue;

            if (HoldingTextNormalizer.TryParseDateString(m.Groups["date"].Value, out var d))
                return d;
        }

        var genericDateRx = GenericDateRx;
        foreach (var line in lines)
        {
            if (line.Contains("Foto", StringComparison.OrdinalIgnoreCase))
                continue;

            var m = genericDateRx.Match(line);
            if (!m.Success)
                continue;

            if (HoldingTextNormalizer.TryParseDateString(m.Groups["date"].Value, out var d))
                return d;
        }

        return null;
    }

    /// <summary>
    /// Sucht in benachbarten Zeilen nach einem Datum.
    /// </summary>
    internal static DateTime? FindNearbyDate(string[] lines, int startIndex, int step, int maxLines, Regex dateRx)
    {
        if (startIndex < 0 || startIndex >= lines.Length) return null;
        var checkedLines = 0;
        for (var i = startIndex; i >= 0 && i < lines.Length && checkedLines < maxLines; i += step)
        {
            var line = lines[i];
            checkedLines++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var m = dateRx.Match(line);
            if (m.Success && HoldingTextNormalizer.TryParseDateString(m.Groups[1].Value, out var d))
                return d;
        }
        return null;
    }

    /// <summary>
    /// Sucht die Schachtnummer im Text eines Schachtprotokolls.
    /// </summary>
    internal static string? TryFindSchachtNumber(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');

        // Bevorzugtes Muster: "Zustandsaufnahme Schacht Nr: <Nummer>"
        var headerRx = new Regex(
            @"Zustandsaufnahme\s*Schacht\s*Nr\.?\s*[:\-]?\s*(?<nr>\d{3,10})\b",
            RegexOptions.IgnoreCase);
        var headerMatch = headerRx.Match(text);
        if (headerMatch.Success)
            return headerMatch.Groups["nr"].Value.Trim();

        var nrRx = new Regex(@"\bNr\.?\s*[:\-]?\s*(?<nr>\d{3,})\b", RegexOptions.IgnoreCase);
        foreach (var line in lines)
        {
            var m = nrRx.Match(line);
            if (m.Success)
                return m.Groups["nr"].Value.Trim();
        }

        var labelRx = new Regex(@"\bSchacht(?:nummer|nr\.?)?\s*[:\-]?\s*(?<nr>\d{3,})\b", RegexOptions.IgnoreCase);
        foreach (var line in lines)
        {
            var m = labelRx.Match(line);
            if (m.Success)
                return m.Groups["nr"].Value.Trim();
        }

        // Schachtfotos enthalten oft nur die Schachtnummer als Seitentext
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (Regex.IsMatch(trimmed, @"^\d{3,8}$"))
                return trimmed;
        }

        return null;
    }

    /// <summary>
    /// Normalisiert eine Schachtnummer auf reine Ziffern ohne fuehrende Nullen.
    /// </summary>
    internal static string NormalizeShaftNumberKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var digits = Regex.Replace(value, @"\D", "");
        if (string.IsNullOrWhiteSpace(digits))
            return string.Empty;

        return PhotoTokenNormalizer.TrimLeadingZerosValue(digits);
    }

    /// <summary>
    /// Versucht, aus dem PDF-Dateinamen eine Haltungs-ID zu lesen.
    /// </summary>
    internal static string? TryExtractHaltungFromPdfPath(string? pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            return null;

        var fileName = Path.GetFileNameWithoutExtension(pdfPath);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var match = PdfFilenamePairRegex.Match(fileName);
        if (!match.Success)
            return null;

        var normalized = HoldingIdNormalizer.NormalizeHaltungId(match.Value.Replace('_', '-'));
        return HoldingIdNormalizer.IsValidHaltungId(normalized) ? normalized : null;
    }

    /// <summary>
    /// Prueft ob ein aus Schacht-Feldern zusammengesetztes Paar gegenueber dem explizit angegebenen Paar verdaechtig ist.
    /// </summary>
    internal static bool IsSuspiciousShaftPair(string shaftPair, string explicitPair)
    {
        var shaftParts = shaftPair.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var explicitParts = explicitPair.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (shaftParts.Length != 2 || explicitParts.Length != 2)
            return false;

        if (string.Equals(shaftParts[0], explicitParts[1], StringComparison.OrdinalIgnoreCase)
            && string.Equals(shaftParts[1], explicitParts[0], StringComparison.OrdinalIgnoreCase))
            return true;

        if (shaftParts.Any(HoldingIdNormalizer.IsDateLikeNode)
            && !explicitParts.Any(HoldingIdNormalizer.IsDateLikeNode))
            return true;

        if (string.Equals(shaftParts[0], shaftParts[1], StringComparison.OrdinalIgnoreCase))
            return true;

        // Explizites Paar hat andere Endpunkte, Schacht-Paar kollabiert auf einen wiederholten Wert -> explizit bevorzugen
        if (!string.Equals(explicitParts[0], explicitParts[1], StringComparison.OrdinalIgnoreCase)
            && string.Equals(shaftParts[1], explicitParts[0], StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
