using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Reine Token-/String-Logik zur Erkennung von Schachtkandidaten und Haltungspaaren im PDF-Text.
/// Kein PdfPig- oder Dokumentobjekt-Zugriff. Extrahiert aus HoldingFolderDistributor.PdfParsing – verhaltensneutral.
/// </summary>
internal static class ShaftCandidateScanner
{
    // ── Regex-Felder (verbatim aus HoldingFolderDistributor.PdfParsing) ──────────

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

    // ── Haltungsbezeichner auf Schacht-/Haltungs-Zeilen ──────────────────────────

    private static readonly string[] ShaftLabelKeywords =
        { "haltung", "schacht", "prüfgegenstand", "prufgegenstand", "pruefgegenstand", "prüfobj", "prufobj", "oberer", "unterer" };

    // ── Oeffentliche Methoden ────────────────────────────────────────────────────

    /// <summary>
    /// Sammelt Schachtnummern fokussiert: Zahlen auf Zeilen mit einem Haltungs-/Schacht-Label
    /// UND deren direkten Nachbarzeilen (pdftotext setzt Werte oft eine Zeile versetzt).
    /// </summary>
    internal static IReadOnlyList<string> GatherShaftCandidates(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        var lines = text.Split('\n');
        var labeled = new bool[lines.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            var low = lines[i].ToLowerInvariant();
            foreach (var l in ShaftLabelKeywords)
                if (low.Contains(l, StringComparison.Ordinal)) { labeled[i] = true; break; }
        }

        var nums = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            var inWindow = labeled[i]
                || (i > 0 && labeled[i - 1])
                || (i < lines.Length - 1 && labeled[i + 1]);
            if (inWindow)
                AddNumberTokens(lines[i], nums);
        }
        return nums.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Rueckfall: alle Zahl-Token der ganzen Seite (Eindeutigkeit schuetzt vor Fehltreffern).
    /// </summary>
    internal static IReadOnlyList<string> GatherAllNumberCandidates(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        var nums = new List<string>();
        foreach (var line in text.Split('\n'))
            AddNumberTokens(line, nums);
        return nums.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Zieht moegliche Schachtnummern aus einer Zeile: gepunktete IDs (06.24341) und ganze Zahlen mit 2-6 Stellen.
    /// </summary>
    internal static void AddNumberTokens(string line, List<string> nums)
    {
        if (IsNoiseLine(line))
            return;

        foreach (Match mm in Regex.Matches(line, @"\b\d{2,}\.\d{2,}\b"))
            nums.Add(mm.Value);
        foreach (Match mm in Regex.Matches(line, @"(?<!\d[.,])\d{2,6}(?![.,]\d)"))
            nums.Add(mm.Value);
    }

    /// <summary>
    /// Zeilen mit typischen Nicht-Schacht-Zahlen (Telefon/Fax, Adresse, Messwerte, Datum/Zeit, GPS, Software).
    /// </summary>
    internal static bool IsNoiseLine(string line)
    {
        var low = line.ToLowerInvariant();
        return low.Contains("telefon") || low.Contains("fax") || low.Contains("www")
            || low.Contains('@') || low.Contains("mbar") || low.Contains("gps")
            || low.Contains("software") || low.Contains("sensortemp") || low.Contains('°')
            || low.Contains("strasse") || low.Contains("+41") || low.Contains("(0)41")
            || low.Contains("prufdruck") || low.Contains("prüfdruck")
            || low.Contains("prufzeit") || low.Contains("prüfzeit") || low.Contains("beruhigung");
    }

    /// <summary>
    /// Extrahiert die Haltungsnummer aus "Haltungsinspektion"- oder "Haltungsbilder"-Kopfzeilen.
    /// </summary>
    internal static string? TryExtractFromHeader(string text)
    {
        var headerRx = new Regex(
            @"Haltungs(?:\s*inspektion|bilder)\s*[-–—]\s*(?:\d{2}\.\d{2}\.\d{2,4}|\d{4}-\d{2}-\d{2})\s*[-–—]\s*((?:\d{2,}\.\d{2,}|\d{4,})\s*[-/]\s*(?:\d{2,}\.\d{2,}|\d{4,}))",
            RegexOptions.IgnoreCase);
        var m = headerRx.Match(text);
        if (!m.Success) return null;
        var haltung = HoldingIdNormalizer.NormalizeHaltungId(m.Groups[1].Value);
        return HoldingIdNormalizer.IsValidHaltungId(haltung) ? haltung : null;
    }

    /// <summary>
    /// Setzt eine Haltungs-ID aus Schacht-oben/Schacht-unten- bzw. Oberer/Unterer-Punkt-Feldern zusammen.
    /// </summary>
    internal static string? TryExtractFromShafts(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');

        // Frueherkennung: Volles Haltungspaar nach "Oberer/Unterer Schacht" oder "Oberer/Unterer Punkt"
        var fullPairAfterSchacht = Regex.Match(text,
            @"(?:Oberer|Unterer)\s*(?:Schacht|Punkt)[^\S\n]*(?<pair>(?:\d{2,}\.\d{2,}|\d{4,})\s*-\s*(?:\d{2,}\.\d{2,}|\d{4,}))",
            RegexOptions.IgnoreCase);
        if (fullPairAfterSchacht.Success)
            return fullPairAfterSchacht.Groups["pair"].Value;

        // WinCAN: robuste Label->Wert-Extraktion
        var upper = TryGetValueAfterLabel(lines, WinCanUpperLabelRegex, WinCanValueRegex);
        var lower = TryGetValueAfterLabel(lines, WinCanLowerLabelRegex, WinCanValueRegex);
        if (!string.IsNullOrWhiteSpace(upper) && !string.IsNullOrWhiteSpace(lower))
        {
            if (!string.Equals(upper, lower, StringComparison.OrdinalIgnoreCase))
                return $"{upper}-{lower}";
        }

        // Inline-Layouts ohne Zeilenumbruch
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

        // Dichtheitspruefung Format
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
        // Volles Paar auf derselben Zeile
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

            // KIT/Dichtheitspruefung: "Pruefstrecke von", "Haltung von/bis", "Leitung"
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
                // Pruefe ob ein volles Paar auf der Zeile steht
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

    /// <summary>
    /// Sucht den Wert eines "Oberer/Unterer Punkt"-Labels in den umliegenden Zeilen.
    /// </summary>
    internal static string? TryFindPoint(string[] lines, string label)
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

    /// <summary>
    /// Sucht den naechsten nicht-leeren Token in den Folgezeilen, der dem Muster entspricht.
    /// </summary>
    internal static string? FindNextToken(string[] lines, int startIndex, string pattern)
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

    /// <summary>
    /// Gibt true zurueck wenn der erste Teil einer Haltungs-ID wie ein Datumsfragment (MM.YYYY) aussieht.
    /// Verhindert "09.2025-80638" als gueltiger Haltungsname.
    /// </summary>
    internal static bool LooksLikeDateFragment(string haltungId)
    {
        if (string.IsNullOrWhiteSpace(haltungId)) return false;
        var dateFragRx = new Regex(@"^(\d{2}\.\d{4})-");
        var m = dateFragRx.Match(haltungId);
        if (!m.Success) return false;
        var parts = m.Groups[1].Value.Split('.');
        if (parts.Length == 2
            && int.TryParse(parts[0], out var month) && month >= 1 && month <= 12
            && int.TryParse(parts[1], out var year) && year >= 2000 && year <= 2099)
            return true;
        return false;
    }

    // ── Private Hilfsmethode (Label-Wert-Extraktion) ────────────────────────────

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

                // 2) Wert in naechster Zeile
                if (i + 1 < lines.Count)
                {
                    var next = NormalizeLine(lines[i + 1]);
                    var v2 = valueRegex.Match(next);
                    if (v2.Success) return v2.Value;
                }

                // 3) Noch eine Zeile weiter (PDF-Layout)
                if (i + 2 < lines.Count)
                {
                    var next2 = NormalizeLine(lines[i + 2]);
                    var v3 = valueRegex.Match(next2);
                    if (v3.Success) return v3.Value;
                }
            }

            // 4) "Zerhacktes" Label ueber Zeilengrenze
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

    private static string NormalizeLine(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        s = s.Replace(' ', ' ');
        s = Regex.Replace(s, @"[ \t]+", " ");
        return s.Trim();
    }
}
