// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Training.Models;

namespace AuswertungPro.Next.UI.Ai.Training.Services;

/// <summary>
/// Extrahiert <see cref="GroundTruthEntry"/>-Einträge aus einem Kanalinspektion-PDF.
///
/// Strategie:
/// 1. Tabellenzeilen-Muster mit Timestamp (Format: "  2.24  BCCBA  Text...  00:01:07").
/// 2. Regelbasiertes Parsing (Bereichsmuster / Einzelmeter).
/// 3. JSON-Protokolldatei als direkter Fallback.
/// </summary>
public sealed class PdfProtocolExtractor
{
    // Hauptformat: "  [meter]  [CODE]  [Text...]  [HH:MM:SS]  [rest]"
    // Lenient whitespace: works with both pdftotext-layout (multi-space) and PdfPig page.Text (single-space).
    private static readonly Regex TableRowPattern = new(
        @"^[ \t]*(?<meter>\d{1,4}[.,]\d{1,3})[ \t]+(?<code>[A-Z]{2,6})[ \t]+(?<text>[^\r\n]+?)[ \t]+(?<time>\d{2}:\d{2}:\d{2})\b",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Fallback: "12.45 BAB B Querriss..." oder "@12.45m BAB Querriss"
    private static readonly Regex EntryPattern = new(
        @"@?(?<m1>\d{1,4}[.,]\d{1,3})\s*m?\s*[-–]?\s*(?<m2>\d{1,4}[.,]\d{1,3})?\s*m?\s+(?<code>[A-Z]{2,5})(?:\s+(?<char>[ABCD]))?\s+(?<text>[^\r\n]{3,})",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Fallback: "@12.45 BAB ..."
    private static readonly Regex SingleMeterPattern = new(
        @"@?(?<m>\d{1,4}[.,]\d{1,3})\s*m?\s+(?<code>[A-Z]{2,5})(?:\s+(?<char>[ABCD]))?\s+(?<text>[^\r\n]{3,})",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Quantifizierungs-Muster: "3mm", "15%", "5 cm"
    private static readonly Regex QuantPattern = new(
        @"(?<val>\d+(?:[.,]\d+)?)\s*(?<unit>mm|cm|%|Stück|Stueck)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extrahiert Ground-Truth-Einträge aus einer PDF- oder JSON-Protokolldatei.
    /// </summary>
    public Task<IReadOnlyList<GroundTruthEntry>> ExtractAsync(
        string filePath,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return Task.FromResult<IReadOnlyList<GroundTruthEntry>>(Array.Empty<GroundTruthEntry>());

        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        return ext switch
        {
            ".json" => Task.FromResult(ExtractFromJson(filePath)),
            ".pdf"  => Task.FromResult(ExtractFromPdf(filePath)),
            _       => Task.FromResult<IReadOnlyList<GroundTruthEntry>>(Array.Empty<GroundTruthEntry>())
        };
    }

    // ── JSON (ProtocolDocument-Format) ────────────────────────────────────

    private static IReadOnlyList<GroundTruthEntry> ExtractFromJson(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            var results = new List<GroundTruthEntry>();

            // ProtocolDocument.Current.Entries
            if (doc.RootElement.TryGetProperty("Current", out var current)
                && current.TryGetProperty("Entries", out var entries))
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    var gtEntry = ParseJsonEntry(entry);
                    if (gtEntry is not null)
                        results.Add(gtEntry);
                }
            }

            return results;
        }
        catch
        {
            return Array.Empty<GroundTruthEntry>();
        }
    }

    private static GroundTruthEntry? ParseJsonEntry(System.Text.Json.JsonElement e)
    {
        if (e.TryGetProperty("IsDeleted", out var deleted) && deleted.GetBoolean())
            return null;

        var code = e.TryGetProperty("Code", out var c) ? c.GetString() : null;
        if (string.IsNullOrWhiteSpace(code)) return null;

        var text  = e.TryGetProperty("Beschreibung", out var t) ? t.GetString() ?? "" : "";
        var mStart = e.TryGetProperty("MeterStart", out var ms) && ms.ValueKind != System.Text.Json.JsonValueKind.Null
            ? ms.GetDouble() : 0.0;
        var mEnd = e.TryGetProperty("MeterEnd", out var me) && me.ValueKind != System.Text.Json.JsonValueKind.Null
            ? me.GetDouble() : mStart;
        var isStreck = e.TryGetProperty("IsStreckenschaden", out var iss) && iss.GetBoolean();

        return new GroundTruthEntry
        {
            VsaCode          = code,
            Text             = text,
            MeterStart       = mStart,
            MeterEnd         = mEnd,
            IsStreckenschaden = isStreck
        };
    }

    // ── PDF (regelbasiertes Parsing) ──────────────────────────────────────

    private static IReadOnlyList<GroundTruthEntry> ExtractFromPdf(string path)
    {
        try
        {
            var text = ExtractTextFromPdf(path);
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<GroundTruthEntry>();

            return ParseEntriesFromText(text);
        }
        catch
        {
            return Array.Empty<GroundTruthEntry>();
        }
    }

    private static string ExtractTextFromPdf(string path)
    {
        using var doc = UglyToad.PdfPig.PdfDocument.Open(path);
        var sb = new System.Text.StringBuilder();

        foreach (var page in doc.GetPages())
        {
            var letters = page.Letters;
            if (letters.Count == 0)
                continue;

            // Estimate average character width from non-whitespace glyphs
            var avgW = letters
                .Where(l => l.Width > 0 && l.Value?.Length == 1 && !char.IsWhiteSpace(l.Value[0]))
                .Select(l => l.Width)
                .DefaultIfEmpty(5.5)
                .Average();
            if (avgW < 0.5) avgW = 5.5;

            // Group letters by Y position (round to nearest 2 units) → one line per row
            // PDF Y-axis: 0 = bottom, so descending order = top-to-bottom reading order
            var lineGroups = letters
                .GroupBy(l => Math.Round(l.StartBaseLine.Y / 2.0) * 2.0)
                .OrderByDescending(g => g.Key);

            foreach (var lineGroup in lineGroups)
            {
                var sorted = lineGroup.OrderBy(l => l.StartBaseLine.X).ToList();
                if (sorted.Count == 0) continue;

                var line = new System.Text.StringBuilder();

                // Leading indent proportional to first letter's X position
                var indent = (int)(sorted[0].StartBaseLine.X / avgW);
                if (indent > 0)
                    line.Append(new string(' ', Math.Min(indent, 30)));

                double prevEndX = sorted[0].StartBaseLine.X;

                foreach (var letter in sorted)
                {
                    var gap = letter.StartBaseLine.X - prevEndX;
                    if (gap > avgW * 0.5)
                    {
                        var nSpaces = Math.Max(1, (int)Math.Round(gap / avgW));
                        line.Append(new string(' ', Math.Min(nSpaces, 80)));
                    }
                    var v = letter.Value ?? string.Empty;
                    line.Append(v);
                    prevEndX = letter.StartBaseLine.X + (letter.Width > 0 ? letter.Width : avgW);
                }

                var lineStr = line.ToString().TrimEnd();
                if (!string.IsNullOrWhiteSpace(lineStr))
                    sb.AppendLine(lineStr);
            }
        }

        return sb.ToString();
    }

    private static IReadOnlyList<GroundTruthEntry> ParseEntriesFromText(string text)
    {
        var results = new List<GroundTruthEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Versuch 1: Tabellenzeilen-Format mit Timestamp (Schweizer/Deutscher Standard)
        // Format: "  2.24  BCCBA  Beschreibung...  00:01:07"
        foreach (Match m in TableRowPattern.Matches(text))
        {
            var entry = BuildEntry(
                m.Groups["meter"].Value,
                "",
                m.Groups["code"].Value,
                "",
                m.Groups["text"].Value,
                ParseTimestamp(m.Groups["time"].Value));

            if (entry is not null && seen.Add(Sig(entry)))
                results.Add(entry);
        }

        if (results.Count > 0)
            return results;

        // Versuch 2: Bereichs-Muster (m1 – m2 CODE)
        foreach (Match m in EntryPattern.Matches(text))
        {
            var entry = BuildEntry(
                m.Groups["m1"].Value,
                m.Groups["m2"].Value,
                m.Groups["code"].Value,
                m.Groups["char"].Value,
                m.Groups["text"].Value,
                null);

            if (entry is not null && seen.Add(Sig(entry)))
                results.Add(entry);
        }

        // Versuch 3: Einzel-Meter-Muster (@m CODE)
        if (results.Count == 0)
        {
            foreach (Match m in SingleMeterPattern.Matches(text))
            {
                var entry = BuildEntry(
                    m.Groups["m"].Value,
                    "",
                    m.Groups["code"].Value,
                    m.Groups["char"].Value,
                    m.Groups["text"].Value,
                    null);

                if (entry is not null && seen.Add(Sig(entry)))
                    results.Add(entry);
            }
        }

        return results;
    }

    private static TimeSpan? ParseTimestamp(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var parts = raw.Split(':');
        if (parts.Length != 3) return null;
        if (int.TryParse(parts[0], out var h)
            && int.TryParse(parts[1], out var min)
            && int.TryParse(parts[2], out var sec))
            return new TimeSpan(h, min, sec);
        return null;
    }

    private static GroundTruthEntry? BuildEntry(
        string meterStartRaw, string meterEndRaw,
        string code, string charRaw, string text,
        TimeSpan? zeit)
    {
        if (!TryParseMeter(meterStartRaw, out var mStart)) return null;
        if (!TryParseMeter(meterEndRaw, out var mEnd)) mEnd = mStart;
        if (mEnd < mStart) mEnd = mStart;

        code = code.Trim().ToUpperInvariant();
        if (code.Length < 2 || code.Length > 6) return null;

        text = text.Trim();
        if (text.Length < 2) return null;

        var characterization = charRaw?.Trim().ToUpperInvariant() switch
        {
            "A" or "B" or "C" or "D" => charRaw.Trim().ToUpperInvariant(),
            _                         => null
        };

        var quant = TryParseQuantification(text);

        return new GroundTruthEntry
        {
            MeterStart        = mStart,
            MeterEnd          = mEnd,
            VsaCode           = code,
            Text              = text,
            Characterization  = characterization,
            Quantification    = quant,
            IsStreckenschaden = mEnd > mStart + 0.05,
            Zeit              = zeit
        };
    }

    private static QuantificationDetail? TryParseQuantification(string text)
    {
        var m = QuantPattern.Match(text);
        if (!m.Success) return null;

        if (!double.TryParse(m.Groups["val"].Value.Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            return null;

        var unit = m.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "stueck" => "Stück",
            var u    => u
        };

        var type = unit switch
        {
            "%"      => "Querschnittsverminderung",
            "mm"     => "Spaltbreite",
            "cm"     => "Spaltbreite",
            "Stück"  => "Anzahl",
            _        => "Unbekannt"
        };

        return new QuantificationDetail { Value = val, Unit = unit, Type = type };
    }

    private static bool TryParseMeter(string raw, out double value)
    {
        if (string.IsNullOrWhiteSpace(raw)) { value = 0; return false; }
        return double.TryParse(raw.Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out value);
    }

    private static string Sig(GroundTruthEntry e)
        => $"{e.VsaCode}|{e.MeterStart:F2}|{e.MeterEnd:F2}";
}
