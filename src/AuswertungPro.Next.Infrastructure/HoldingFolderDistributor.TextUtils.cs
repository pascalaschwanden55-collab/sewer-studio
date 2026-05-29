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

// Text-, Datums-, Schluessel- und Pfad-Helfer.
// Teil derselben partial-Klasse - reine mechanische Auslagerung (kein Verhaltenswechsel).
public static partial class HoldingFolderDistributor
{

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? string.Empty;

        return text
            .Replace('\u00A0', ' ')
            .Replace('–', '-')
            .Replace('—', '-')
            .Replace('−', '-')
            .Replace("\t", " ");
    }


    private static bool TryParseDateString(string value, out DateTime date)
    {
        return DateTime.TryParseExact(
            value,
            new[] { "dd.MM.yyyy", "dd.MM.yy", "dd/MM/yyyy", "dd/MM/yy", "dd-MM-yyyy", "dd-MM-yy", "yyyy-MM-dd" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
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


    private static string NormalizeShaftNumberKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var digits = Regex.Replace(value, @"\D", "");
        if (string.IsNullOrWhiteSpace(digits))
            return string.Empty;

        return TrimLeadingZerosValue(digits);
    }


    private static string BuildPageRange(IReadOnlyList<int> pages)
    {
        if (pages.Count == 0) return "";
        var sorted = pages.Distinct().OrderBy(p => p).ToList();
        return sorted.Count == 1 ? $"{sorted[0]}" : $"{sorted[0]}-{sorted[^1]}";
    }


    private static bool IsContentsPage(string text)
        => text.Contains("Inhaltsverzeichnis", StringComparison.OrdinalIgnoreCase);


    private static DateTime? TryFindInspectionDate(string text)
    {
        var dateRx = InspectionDateRx;
        var lines = text.Replace("\r\n", "\n").Split('\n');

        // Priority 1: Find date in header line
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Contains("Haltungsinspektion", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Haltungsbilder", StringComparison.OrdinalIgnoreCase))
            {
                var mHeader = dateRx.Match(line);
                if (mHeader.Success && TryParseDateString(mHeader.Groups[1].Value, out var dh))
                    return dh;
            }
        }
        
        // Priority 2: Find date near Inspektionsdatum or similar
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
            if (m.Success && TryParseDateString(m.Groups[1].Value, out var d1))
                return d1;

            var prev = FindNearbyDate(lines, i - 1, -1, 3, dateRx);
            if (prev is not null) return prev;
            var next = FindNearbyDate(lines, i + 1, 1, 3, dateRx);
            if (next is not null) return next;
        }

        // Priority 3: Any date, but skip Gedruckt lines
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Contains("Gedruckt", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("erstellt", StringComparison.OrdinalIgnoreCase))
                continue;

            var any = dateRx.Match(line);
            if (any.Success && TryParseDateString(any.Groups[1].Value, out var d2))
            {
                // Validate reasonable date range (2000-2030)
                if (d2.Year >= 2000 && d2.Year <= 2030)
                    return d2;
            }
        }

        return null;
    }


    private static DateTime? TryFindSchachtDate(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var labeledDateRx = LabeledDateRx;
        foreach (var line in lines)
        {
            var m = labeledDateRx.Match(line);
            if (!m.Success)
                continue;

            if (TryParseDateString(m.Groups["date"].Value, out var d))
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

            if (TryParseDateString(m.Groups["date"].Value, out var d))
                return d;
        }

        return null;
    }


    private static DateTime? FindNearbyDate(string[] lines, int startIndex, int step, int maxLines, Regex dateRx)
    {
        if (startIndex < 0 || startIndex >= lines.Length) return null;
        var checkedLines = 0;
        for (var i = startIndex; i >= 0 && i < lines.Length && checkedLines < maxLines; i += step)
        {
            var line = lines[i];
            checkedLines++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var m = dateRx.Match(line);
            if (m.Success && TryParseDateString(m.Groups[1].Value, out var d))
                return d;
        }
        return null;
    }


    private static string? TryFindHaltungId(string text)
    {
        var idRx = HaltungIdRx;
        var generalPairRx = GeneralPairRx;
        var gluedDatePairRx = GluedDatePairRx;
        
        // Priority 1: Try extracting from Schacht oben/unten pattern first (most reliable)
        var shaftPattern = TryExtractFromShafts(text);
        if (!string.IsNullOrWhiteSpace(shaftPattern))
        {
            var normalized = NormalizeHaltungId(shaftPattern);
            if (IsValidHaltungId(normalized))
                return normalized;
        }

        // Priority 1b: Pair directly glued to a date (e.g. 23022-2159822.04.2014).
        var glued = gluedDatePairRx.Match(text);
        if (glued.Success)
        {
            var normalized = NormalizeHaltungId(glued.Groups[1].Value);
            if (IsValidHaltungId(normalized))
                return normalized;
        }

        // WinCAN compact line where "KS Nr." (non-numeric start node) is glued to node ids,
        // e.g. "... KS Nr. 221632025233 ...".
        var ksCompact = Regex.Match(text, @"KS\s*Nr\.?\s*(?<digits>\d{10,13})", RegexOptions.IgnoreCase);
        if (ksCompact.Success)
        {
            var ksCandidate = TryParseKsCompactHoldingDigits(ksCompact.Groups["digits"].Value);
            if (!string.IsNullOrWhiteSpace(ksCandidate))
                return ksCandidate;
        }

        // Priority 1c: concatenated numeric pair without dash (e.g. 2302221598 -> 23022-21598)
        var concatenated = ConcatenatedIdRx.Match(text);
        if (concatenated.Success)
        {
            var raw = concatenated.Groups["id"].Value;
            var candidate = $"{raw.Substring(0, 5)}-{raw.Substring(5, 5)}";
            var normalized = NormalizeHaltungId(candidate);
            if (IsValidHaltungId(normalized))
                return normalized;
        }
        
        // Priority 2: Jede Zeile mit "Haltung" prüfen, nach ":" oder nach dem Wort die erste passende Nummer extrahieren
        var lines = text.Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines)
        {
            if (!line.Contains("Haltung", StringComparison.OrdinalIgnoreCase))
                continue;
            // Suche nach Zahl mit Trennzeichen nach 'Haltung' oder nach ':'
            var m = idRx.Match(line);
            if (m.Success)
            {
                var id = m.Groups["id"].Value?.Trim();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    var normalized = NormalizeHaltungId(id);
                    if (IsValidHaltungId(normalized))
                        return normalized;
                }
            }
            // Fallback: Suche nach erstem Zahlenpaar im Stil 11111-2222
            var inline = generalPairRx.Match(line);
            if (inline.Success)
            {
                var normalized = NormalizeHaltungId(inline.Groups[1].Value);
                if (IsValidHaltungId(normalized))
                    return normalized;
            }
        }
        
        // Priority 4: Try "Leitung" field
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!line.Contains("Leitung", StringComparison.OrdinalIgnoreCase))
                continue;

            var inline = generalPairRx.Match(line);
            if (inline.Success)
            {
                var normalized = NormalizeHaltungId(inline.Groups[1].Value);
                if (IsValidHaltungId(normalized))
                    return normalized;
            }

            var nextId = FindNextToken(lines, i + 1, @"(?:\d{2,}\.\d{2,}|\d{4,})\s*[-/]\s*(?:\d{2,}\.\d{2,}|\d{4,})");
            if (!string.IsNullOrWhiteSpace(nextId))
            {
                var normalized = NormalizeHaltungId(nextId);
                if (IsValidHaltungId(normalized))
                    return normalized;
            }
        }

        var oberer = TryFindPoint(lines, "Oberer");
        var unterer = TryFindPoint(lines, "Unterer");
        if (!string.IsNullOrWhiteSpace(oberer) && !string.IsNullOrWhiteSpace(unterer))
        {
            var combined = NormalizeHaltungId($"{oberer}-{unterer}");
            if (IsValidHaltungId(combined))
                return combined;
        }

        var loose = generalPairRx.Match(text);
        if (loose.Success)
        {
            var normalized = NormalizeHaltungId(loose.Groups[1].Value);
            if (IsValidHaltungId(normalized) && !LooksLikeDateFragment(normalized))
                return normalized;
        }

        var anyIdLine = lines.FirstOrDefault(l => Regex.IsMatch(l, @"^\s*(?:\d{2,}\.\d{2,}|\d{4,})\s*[-/]\s*(?:\d{2,}\.\d{2,}|\d{4,})\s*$"));
        if (!string.IsNullOrWhiteSpace(anyIdLine))
        {
            var normalized = NormalizeHaltungId(anyIdLine.Trim());
            if (IsValidHaltungId(normalized))
                return normalized;
        }

        return null;
    }


    private static string? TryParseKsCompactHoldingDigits(string rawDigits)
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
                    var b = TrimLeadingZerosValue(bRaw);
                    var candidate = NormalizeHaltungId($"{a}-{b}");
                    if (IsValidHaltungId(candidate))
                        candidates.Add((2, candidate));
                }
            }

            if (remaining == 10)
            {
                var a = digits.Substring(prefixLen, 5);
                var b = digits.Substring(prefixLen + 5, 5);
                var candidate = NormalizeHaltungId($"{a}-{TrimLeadingZerosValue(b)}");
                if (IsValidHaltungId(candidate))
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


    private static string? TryFindSchachtNumber(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');

        // Preferred protocol header pattern:
        // "Zustandsaufnahme Schacht Nr: <Schachtnummer>"
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

        // Schachtfotos often contain only the shaft number as plain page text.
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (Regex.IsMatch(trimmed, @"^\d{3,8}$"))
                return trimmed;
        }

        return null;
    }

    // Schacht-Wert: numerisch (81150, 42.046) ODER alphanumerisch (S42.123, KS-0815, A1-B2)


    private static IReadOnlyList<string> Tokenize(string line)
        => line.Split(new[] { ' ', '\t', ';', ',', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim()).ToList();


    private static bool HasVideoExtension(string token)
    {
        var normalized = NormalizeVideoFileName(token);
        return MediaFileTypes.HasVideoExtension(normalized);
    }


    private static bool HasImageExtension(string token)
    {
        var normalized = NormalizeVideoFileName(token);
        return MediaFileTypes.HasImageExtension(normalized);
    }


    private static string? NormalizeVideoFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var candidate = value.Trim().Trim('"', '\'');
        candidate = candidate.TrimEnd('.', ',', ';', ':', ')', ']', '}', '>');
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        candidate = candidate.Replace('\\', '/');
        var fileName = Path.GetFileName(candidate).Trim();
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        return fileName.Trim('"', '\'');
    }


    private static string SanitizePathSegment(string value)
        => ProjectPathResolver.SanitizePathSegment(value);


    private static string NormalizeHaltungId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "UNKNOWN";

        var text = NormalizeText(value).Trim();
        // Extract pair pattern: XXXXX-XXXXX or XX.XXXX-XX.XXXX
        var pairRx = new Regex(@"((?:\d{2,}\.\d{2,}|\d{4,})\s*[-]\s*(?:\d{2,}\.\d{2,}|\d{4,}))");
        var m = pairRx.Match(text);
        if (m.Success)
        {
            var normalized = m.Groups[1].Value.Replace(" ", "").Replace("/", "-");
            // Ensure exactly one dash
            normalized = Regex.Replace(normalized, @"\s*-+\s*", "-");
            return normalized;
        }

        return text;
    }


    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }


    private static bool IsValidHaltungId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        var rx = new Regex(@"^(?:\d{2,}\.\d{2,}|\d{4,})\s*-\s*(?:\d{2,}\.\d{2,}|\d{4,})$");
        if (!rx.IsMatch(normalized))
            return false;

        var parts = normalized.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;

        // Reject common OCR glue artifacts such as "04.201423022-215987" (date fragment + id).
        foreach (var part in parts)
        {
            if (Regex.IsMatch(part, @"^\d{2}\.20\d{2}\d+$"))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Prueft ob im Haltungsordner bereits ein Video mit gleicher Dateigroesse existiert.
    /// Gibt den Pfad zurueck wenn ja, sonst null.
    /// Verhindert Duplikate beim erneuten Verteilen.
    /// </summary>


    private static string EnsureUniquePath(string path, bool overwrite)
    {
        if (overwrite || !File.Exists(path))
            return path;

        var dir = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i.ToString("00", CultureInfo.InvariantCulture)}{ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        throw new IOException($"Unable to find free filename for {path}");
    }
}
