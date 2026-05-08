using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// HoldingFolderDistributor — Haltungs-/Schacht-Extraction aus PDF-Text
/// (partial class).
///
/// Refactor 2026-05-08 (Etappe 6, Charge R14): die textbasierten
/// Haltung-/Schacht-Erkennungs-Helfer aus der Hauptdatei ausgegliedert.
/// Mechanisch — keine Verhaltensaenderung.
/// </summary>
public static partial class HoldingFolderDistributor
{
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

}
