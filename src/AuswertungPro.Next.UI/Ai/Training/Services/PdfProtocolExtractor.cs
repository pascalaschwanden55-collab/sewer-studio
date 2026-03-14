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
/// Strategien (in Priorität):
/// 1. IKAS Leitungsgrafik: "Entf. Kode Foto Video Beschreibung" (Zeit VOR Text)
/// 2. Tabellenzeilen mit Timestamp am Ende (Zeit NACH Text)
/// 3. IKAS Bildbericht: Label-Value Blöcke (Zustand/Entf./Video)
/// 4. Regelbasiertes Fallback (Bereichsmuster / Einzelmeter)
/// 5. JSON-Protokolldatei als direkter Fallback
/// </summary>
public sealed class PdfProtocolExtractor
{
    // ── Regex-Muster ────────────────────────────────────────────────────────

    // VSA-Code: 2-6 Buchstaben, optional mit Punkt-Suffix (.A, .C, .AB)
    private const string CodePattern = @"[A-Z]{2,6}(?:\.[A-Z]{1,2})?";

    // IKAS Leitungsgrafik: "[meter]  [CODE]  [foto?]  [HH:MM:SS]  [text]"
    // Zeit kommt VOR der Beschreibung
    private static readonly Regex IkasTablePattern = new(
        $@"^[ \t]*(?<meter>\d{{1,4}}[.,]\d{{1,3}})[ \t]+(?<code>{CodePattern})[ \t]+(?:\d{{1,5}}[ \t]+)?(?<time>\d{{2}}:\d{{2}}:\d{{2}})[ \t]+(?<text>[^\r\n]+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // IKAS Fortsetzungszeile (kein Meter): "[CODE]  [foto?]  [HH:MM:SS]  [text]"
    private static readonly Regex IkasContinuationPattern = new(
        $@"^[ \t]{{4,}}(?<code>{CodePattern})[ \t]+(?:\d{{1,5}}[ \t]+)?(?<time>\d{{2}}:\d{{2}}:\d{{2}})[ \t]+(?<text>[^\r\n]+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Standard-Format: "[meter]  [CODE]  [text...]  [HH:MM:SS]"
    // Optional: Operator-Code (z.B. A01, B01) zwischen Meter und VSA-Code (KIT Bauinspekt)
    // Zeit kommt NACH der Beschreibung
    private static readonly Regex TableRowPattern = new(
        $@"^[ \t]*(?<meter>\d{{1,4}}[.,]\d{{1,3}})[ \t]+(?:[A-Z]\d{{1,3}}[ \t]+)?(?<code>{CodePattern})[ \t]+(?<text>[^\r\n]+?)[ \t]+(?<time>\d{{2}}:\d{{2}}:\d{{2}})\b",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // IKAS Bildbericht: Label-Value Blöcke mit "Zustand" + "Entf."
    private static readonly Regex BildberichtCodePattern = new(
        $@"^\s*Zustand\s+(?<code>{CodePattern})\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);
    // Meter mit "Entf." - Flie.{1,2}r statt Flie.r wegen Multi-Byte-Encoding (z.B. IKAS ¦ → Â¦)
    private static readonly Regex BildberichtMeterPattern = new(
        @"^\s*Entf\.?\s+(?:gegen\s+Flie.{1,2}r\.?\s+)?(?<meter>\d{1,4}[.,]\d{1,3})\s*m\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex BildberichtVideoPattern = new(
        @"^\s*Video\s+(?<time>\d{2}:\d{2}:\d{2})\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Fallback: "12.45 BAB B Querriss..." oder "@12.45m BAB Querriss"
    private static readonly Regex EntryPattern = new(
        $@"@?(?<m1>\d{{1,4}}[.,]\d{{1,3}})\s*m?\s*[-–]?\s*(?<m2>\d{{1,4}}[.,]\d{{1,3}})?\s*m?\s+(?<code>{CodePattern})(?:\s+(?<char>[ABCD]))?\s+(?<text>[^\r\n]{{3,}})",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Fallback: "@12.45 BAB ..."
    private static readonly Regex SingleMeterPattern = new(
        $@"@?(?<m>\d{{1,4}}[.,]\d{{1,3}})\s*m?\s+(?<code>{CodePattern})(?:\s+(?<char>[ABCD]))?\s+(?<text>[^\r\n]{{3,}})",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Quantifizierung: "3mm", "15%", "5 cm"
    private static readonly Regex QuantPattern = new(
        @"(?<val>\d+(?:[.,]\d+)?)\s*(?<unit>mm|cm|%|Stück|Stueck)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── Öffentliche API ─────────────────────────────────────────────────────

    /// <param name="filePath">Pfad zur Protokoll-Datei (PDF oder JSON).</param>
    /// <param name="framesDir">Optionaler Ordner zum Speichern extrahierter Bildbericht-Fotos.</param>
    /// <param name="ct">Cancellation-Token.</param>
    public Task<IReadOnlyList<GroundTruthEntry>> ExtractAsync(
        string filePath,
        string? framesDir = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return Task.FromResult<IReadOnlyList<GroundTruthEntry>>(Array.Empty<GroundTruthEntry>());

        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        return ext switch
        {
            ".json" => Task.FromResult(ExtractFromJson(filePath)),
            ".pdf"  => Task.FromResult(ExtractFromPdf(filePath, framesDir)),
            _       => Task.FromResult<IReadOnlyList<GroundTruthEntry>>(Array.Empty<GroundTruthEntry>())
        };
    }

    // ── JSON ────────────────────────────────────────────────────────────────

    private static IReadOnlyList<GroundTruthEntry> ExtractFromJson(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            var results = new List<GroundTruthEntry>();

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

    // ── PDF ─────────────────────────────────────────────────────────────────

    private static IReadOnlyList<GroundTruthEntry> ExtractFromPdf(string path, string? framesDir)
    {
        try
        {
            using var doc = UglyToad.PdfPig.PdfDocument.Open(path);

            var text = ExtractTextFromPdfDoc(doc);
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<GroundTruthEntry>();

            // Diagnose: extrahierten Text speichern
            try
            {
                var diagDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AuswertungPro", "diag");
                Directory.CreateDirectory(diagDir);
                var safeName = Regex.Replace(Path.GetFileNameWithoutExtension(path), @"[^\w\-]", "_");
                File.WriteAllText(
                    Path.Combine(diagDir, $"pdf_text_{safeName}.txt"),
                    text);
            }
            catch { /* Diagnose ist optional */ }

            var entries = ParseEntriesFromText(text);

            // Fotos aus PDF-Bildbericht extrahieren und Einträgen zuordnen
            if (entries.Count > 0 && !string.IsNullOrWhiteSpace(framesDir))
            {
                entries = ExtractAndAssignPdfImages(doc, entries, path, framesDir);
            }

            return entries;
        }
        catch
        {
            return Array.Empty<GroundTruthEntry>();
        }
    }

    private static string ExtractTextFromPdfDoc(UglyToad.PdfPig.PdfDocument doc)
    {
        var sb = new System.Text.StringBuilder();

        foreach (var page in doc.GetPages())
        {
            var letters = page.Letters;
            if (letters.Count == 0)
                continue;

            var avgW = letters
                .Where(l => l.Width > 0 && l.Value?.Length == 1 && !char.IsWhiteSpace(l.Value[0]))
                .Select(l => l.Width)
                .DefaultIfEmpty(5.5)
                .Average();
            if (avgW < 0.5) avgW = 5.5;

            var lineGroups = letters
                .GroupBy(l => Math.Round(l.StartBaseLine.Y / 2.0) * 2.0)
                .OrderByDescending(g => g.Key);

            foreach (var lineGroup in lineGroups)
            {
                var sorted = lineGroup.OrderBy(l => l.StartBaseLine.X).ToList();
                if (sorted.Count == 0) continue;

                var line = new System.Text.StringBuilder();

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
                // Zeilen mit Inhalt beibehalten (auch wenn nur Steuerzeichen enthalten)
                if (lineStr.Any(c => !char.IsWhiteSpace(c)))
                    sb.AppendLine(lineStr);
            }
        }

        // Erkennung und Korrektur von Custom-Font-Encoding (z.B. IKAS-PDFs)
        var rawText = sb.ToString();
        return TryDecodeShiftedText(rawText);
    }

    // ── Font-Encoding-Korrektur ──────────────────────────────────────────────

    /// <summary>
    /// Erkennt PDFs mit verschobener Zeichencodierung (Custom Font Encoding)
    /// und korrigiert den Text automatisch. Manche PDF-Generatoren (z.B. IKAS)
    /// verwenden Schriften, bei denen alle Zeichen um einen festen Offset
    /// verschoben sind. PdfPig kann diese nicht korrekt decodieren.
    /// </summary>
    private static string TryDecodeShiftedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        string[] knownWords =
        {
            "Leitung", "Video", "Foto", "Zustand", "Material",
            "Schacht", "Kanal", "Haltung", "Inspektion", "Dimension",
            "Profil", "Rohr", "Position", "Entf", "Strasse", "Wetter"
        };

        int existingMatches = CountWordMatches(text, knownWords);
        if (existingMatches >= 3)
            return text;

        int bestShift = 0;
        int bestCount = existingMatches;

        for (int shift = 1; shift <= 60; shift++)
        {
            var decoded = ShiftAllChars(text, shift);
            int count = CountWordMatches(decoded, knownWords);
            if (count > bestCount)
            {
                bestCount = count;
                bestShift = shift;
            }
        }

        if (bestShift > 0 && bestCount >= 3)
            return ShiftAllChars(text, bestShift);

        return text;
    }

    private static int CountWordMatches(string text, string[] words)
    {
        int count = 0;
        foreach (var word in words)
        {
            if (text.Contains(word, StringComparison.OrdinalIgnoreCase))
                count++;
        }
        return count;
    }

    private static string ShiftAllChars(string text, int shift)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch == '\r' || ch == '\n' || ch == '\t' || ch == ' ')
                sb.Append(ch);
            else
                sb.Append((char)(ch + shift));
        }
        return sb.ToString();
    }

    // ── Bild-Extraktion aus PDF ─────────────────────────────────────────────

    /// <summary>
    /// Extrahiert echte Inspektionsfotos aus dem PDF-Bildbericht und ordnet sie den Einträgen zu.
    /// Unterstuetzt JPEG-kodierte Bilder (haeufig in IKAS/WinCan PDFs) und PNG.
    /// Filtert Logos, Icons, Header-Banner und Querschnitt-Zeichnungen heraus.
    /// </summary>
    private static IReadOnlyList<GroundTruthEntry> ExtractAndAssignPdfImages(
        UglyToad.PdfPig.PdfDocument doc,
        IReadOnlyList<GroundTruthEntry> entries,
        string pdfPath,
        string framesDir)
    {
        try
        {
            Directory.CreateDirectory(framesDir);
            var safeName = Regex.Replace(Path.GetFileNameWithoutExtension(pdfPath), @"[^\w\-]", "_");

            // Filter-Kriterien fuer echte Inspektionsfotos:
            //   - Min. 300x200 Pixel (echte Fotos sind mindestens SD-Aufloesung)
            //   - Seitenverhaeltnis 1.0–2.5 (Landscape-Fotos, keine Banner/Streifen)
            //   - Min. 20 KB Bilddaten (Zeichnungen/Schemata sind kleiner)
            const int MinWidth = 300;
            const int MinHeight = 200;
            const int MinBytesForPhoto = 20_000; // 20 KB — echte Fotos typisch 50KB+ (JPEG) oder 200KB+ (PNG)

            var images = new List<(byte[] data, string ext)>();
            foreach (var page in doc.GetPages())
            {
                foreach (var img in page.GetImages())
                {
                    // Groessenfilter: Logos, Icons und kleine Grafiken raus
                    if (img.WidthInSamples < MinWidth || img.HeightInSamples < MinHeight)
                        continue;

                    // Seitenverhaeltnis: Banner (z.B. 748x250 = 3.0) und schmale Streifen raus
                    // Echte Inspektionsfotos sind typisch 4:3 (1.33) oder 16:9 (1.78)
                    var aspect = (double)img.WidthInSamples / img.HeightInSamples;
                    if (aspect < 1.0 || aspect > 2.5)
                        continue;

                    // Strategie 1: PNG-Konvertierung (funktioniert fuer nicht-JPEG-Bilder)
                    if (img.TryGetPng(out var pngBytes) && pngBytes.Length >= MinBytesForPhoto)
                    {
                        images.Add((pngBytes, ".png"));
                        continue;
                    }

                    // Strategie 2: JPEG-RawBytes direkt verwenden
                    // Die meisten Inspektionsfotos in IKAS/WinCan-PDFs sind JPEG-kodiert.
                    // TryGetPng() scheitert bei diesen, aber RawBytes enthaelt das JPEG direkt.
                    // ACHTUNG: Manche PDFs haben invertierte Decode-Arrays ([1,0,1,0,1,0]),
                    // wodurch die Raw-JPEG-Daten wie ein Farbnegativ aussehen.
                    try
                    {
                        var raw = img.RawBytes;
                        if (raw.Count >= MinBytesForPhoto
                            && raw.Count >= 3
                            && raw[0] == 0xFF && raw[1] == 0xD8 && raw[2] == 0xFF) // JPEG SOI Marker
                        {
                            var jpegBytes = raw.ToArray();

                            // Pruefen ob das PDF einen invertierten Decode-Array hat
                            if (IsInvertedImage(img))
                            {
                                jpegBytes = InvertJpegColors(jpegBytes);
                            }

                            images.Add((jpegBytes, ".jpg"));
                        }
                    }
                    catch
                    {
                        // RawBytes-Zugriff kann bei manchen PDFs fehlschlagen
                    }
                }
            }

            if (images.Count == 0)
                return entries;

            // Bilder den Entries zuordnen (in Reihenfolge)
            var result = new List<GroundTruthEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string? framePath = null;

                if (i < images.Count)
                {
                    var (data, ext) = images[i];
                    var fileName = $"{safeName}_{entry.VsaCode}_{entry.MeterStart:F1}m_{i}{ext}";
                    framePath = Path.Combine(framesDir, fileName);
                    try
                    {
                        File.WriteAllBytes(framePath, data);
                    }
                    catch
                    {
                        framePath = null;
                    }
                }

                // GroundTruthEntry ist ein record → mit "with" kopieren
                result.Add(entry with { ExtractedFramePath = framePath });
            }

            return result;
        }
        catch
        {
            return entries;
        }
    }

    // ── Parsing ─────────────────────────────────────────────────────────────

    private static IReadOnlyList<GroundTruthEntry> ParseEntriesFromText(string text)
    {
        var results = new List<GroundTruthEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Strategie 1: IKAS Leitungsgrafik (Zeit VOR Beschreibung)
        // Format: "0.00  BCD  [1777]  00:00:09  Rohranfang"
        var lastMeter = 0.0;
        foreach (Match m in IkasTablePattern.Matches(text))
        {
            var entry = BuildEntry(
                m.Groups["meter"].Value,
                "",
                m.Groups["code"].Value,
                "",
                m.Groups["text"].Value,
                ParseTimestamp(m.Groups["time"].Value));

            if (entry is not null && seen.Add(Sig(entry)))
            {
                results.Add(entry);
                lastMeter = entry.MeterStart;
            }
        }

        // IKAS Fortsetzungszeilen (kein Meter → vorherigen Meter verwenden)
        if (results.Count > 0)
        {
            foreach (Match m in IkasContinuationPattern.Matches(text))
            {
                // Prüfen ob diese Zeile nicht schon als Hauptzeile gematcht wurde
                var code = m.Groups["code"].Value.Trim().ToUpperInvariant();
                var time = ParseTimestamp(m.Groups["time"].Value);
                var textVal = m.Groups["text"].Value.Trim();

                // Finde den nächsten bekannten Meter davor
                var meter = FindPrecedingMeter(text, m.Index, lastMeter);

                var entry = BuildEntryDirect(meter, meter, code, textVal, time);
                if (entry is not null && seen.Add(Sig(entry)))
                    results.Add(entry);
            }
        }

        if (results.Count > 0)
            return results;

        // Strategie 2: Standard-Tabellenformat (Zeit NACH Beschreibung)
        // Format: "2.24  BCCBA  Beschreibung...  00:01:07"
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

        // Strategie 3: IKAS Bildbericht (Label-Value Blöcke)
        results = ParseBildberichtBlocks(text, seen);
        if (results.Count > 0)
            return results;

        // Strategie 4: Bereichs-Muster (m1 – m2 CODE)
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

        // Strategie 5: Einzel-Meter-Muster (@m CODE)
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

    /// <summary>
    /// Parst IKAS Bildbericht-Seiten: Blöcke mit Zustand/Entf./Video Labels.
    /// Findet den jeweils NÄCHSTEN Entf.- und Video-Match zu jedem Zustand-Match.
    /// </summary>
    private static List<GroundTruthEntry> ParseBildberichtBlocks(string text, HashSet<string> seen)
    {
        var results = new List<GroundTruthEntry>();

        var codeMatches = BildberichtCodePattern.Matches(text);
        var meterMatches = BildberichtMeterPattern.Matches(text);
        var videoMatches = BildberichtVideoPattern.Matches(text);

        foreach (Match cm in codeMatches)
        {
            var code = cm.Groups["code"].Value.Trim().ToUpperInvariant();
            var pos = cm.Index;

            double meter = 0;
            TimeSpan? zeit = null;

            // Nächsten Meter-Match finden (minimale Distanz innerhalb 500 Zeichen)
            int bestMeterDist = int.MaxValue;
            foreach (Match mm in meterMatches)
            {
                var dist = Math.Abs(mm.Index - pos);
                if (dist < bestMeterDist && dist < 500)
                {
                    bestMeterDist = dist;
                    TryParseMeter(mm.Groups["meter"].Value, out meter);
                }
            }

            // Nächsten Video-Match finden
            int bestVideoDist = int.MaxValue;
            foreach (Match vm in videoMatches)
            {
                var dist = Math.Abs(vm.Index - pos);
                if (dist < bestVideoDist && dist < 500)
                {
                    bestVideoDist = dist;
                    zeit = ParseTimestamp(vm.Groups["time"].Value);
                }
            }

            var entry = BuildEntryDirect(meter, meter, code, code, zeit);
            if (entry is not null && seen.Add(Sig(entry)))
                results.Add(entry);
        }

        return results;
    }

    /// <summary>
    /// Findet den Meterwert der vorherigen Tabellenzeile für Fortsetzungszeilen.
    /// </summary>
    private static double FindPrecedingMeter(string text, int position, double fallback)
    {
        // Suche rückwärts nach einer Zeile mit Meterwert
        var preceding = text[..position];
        var meterMatch = Regex.Match(preceding,
            @"^[ \t]*(\d{1,4}[.,]\d{1,3})[ \t]+[A-Z]",
            RegexOptions.Multiline | RegexOptions.RightToLeft);

        if (meterMatch.Success && TryParseMeter(meterMatch.Groups[1].Value, out var m))
            return m;

        return fallback;
    }

    // ── Hilfsmethoden ───────────────────────────────────────────────────────

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
        if (code.Length < 2 || code.Length > 8) return null;

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

    /// <summary>BuildEntry ohne Rohtext-Parsing (für Bildbericht/Continuation).</summary>
    private static GroundTruthEntry? BuildEntryDirect(
        double meterStart, double meterEnd,
        string code, string text, TimeSpan? zeit)
    {
        code = code.Trim().ToUpperInvariant();
        if (code.Length < 2 || code.Length > 8) return null;

        text = text.Trim();
        if (text.Length < 2) text = code; // Fallback: Code als Beschreibung

        return new GroundTruthEntry
        {
            MeterStart        = meterStart,
            MeterEnd          = meterEnd,
            VsaCode           = code,
            Text              = text,
            Characterization  = null,
            Quantification    = TryParseQuantification(text),
            IsStreckenschaden = meterEnd > meterStart + 0.05,
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

    // ── Farbnegativ-Korrektur fuer PDF-JPEG-Bilder ──────────────────────────

    /// <summary>
    /// Prueft ob ein PDF-Bild einen invertierten Decode-Array hat.
    /// Manche PDF-Generatoren (WinCan, aeltere IKAS) speichern JPEG-Bilder
    /// mit Decode [1,0,1,0,1,0] statt [0,1,0,1,0,1], was die Farben invertiert.
    /// PdfPig exponiert den Decode-Array nicht direkt, daher pruefen wir
    /// per Reflection oder anhand einer Heuristik (mittlere Helligkeit).
    /// </summary>
    private static bool IsInvertedImage(UglyToad.PdfPig.Content.IPdfImage img)
    {
        try
        {
            // Versuch 1: Decode-Array per Reflection holen
            // PdfPig speichert intern ein Dictionary mit dem "Decode" Key
            var dictProp = img.GetType().GetProperty("ImageDictionary",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (dictProp != null)
            {
                var dict = dictProp.GetValue(img);
                if (dict != null)
                {
                    // DictionaryToken hat TryGet<T>(NameToken, out T)
                    var tryGetMethod = dict.GetType().GetMethod("TryGet",
                        new[] { typeof(UglyToad.PdfPig.Tokens.NameToken),
                                typeof(UglyToad.PdfPig.Tokens.ArrayToken).MakeByRefType() });

                    if (tryGetMethod == null)
                    {
                        // Generische TryGet<T> Methode suchen
                        var methods = dict.GetType().GetMethods()
                            .Where(m => m.Name == "TryGet" && m.IsGenericMethod);
                        foreach (var method in methods)
                        {
                            try
                            {
                                var generic = method.MakeGenericMethod(typeof(UglyToad.PdfPig.Tokens.ArrayToken));
                                var args = new object?[] {
                                    UglyToad.PdfPig.Tokens.NameToken.Create("Decode"),
                                    null };
                                var found = (bool)generic.Invoke(dict, args)!;
                                if (found && args[1] is UglyToad.PdfPig.Tokens.ArrayToken arr)
                                {
                                    // Decode [1,0,...] = invertiert, [0,1,...] = normal
                                    if (arr.Data.Count >= 2
                                        && arr.Data[0] is UglyToad.PdfPig.Tokens.NumericToken first
                                        && first.Double > 0.5)
                                    {
                                        return true;
                                    }
                                    return false;
                                }
                                break;
                            }
                            catch { }
                        }
                    }
                }
            }

            // Versuch 2: Heuristik — JPEG laden und mittlere Helligkeit pruefen
            // Invertierte Bilder haben typischerweise eine sehr hohe mittlere Helligkeit
            // (dunkles Rohr wird hell, heller Hintergrund wird dunkel)
            return IsLikelyInvertedByBrightness(img.RawBytes.ToArray());
        }
        catch
        {
            // Im Zweifel: Heuristik auf RawBytes anwenden
            try { return IsLikelyInvertedByBrightness(img.RawBytes.ToArray()); }
            catch { return false; }
        }
    }

    /// <summary>
    /// Heuristik: Prueft ob ein JPEG-Bild wahrscheinlich invertiert ist.
    /// Kanalbilder sind typischerweise dunkel (Rohrinneres). Wenn die mittlere
    /// Helligkeit sehr hoch ist (>180), sind die Farben wahrscheinlich invertiert.
    /// </summary>
    private static bool IsLikelyInvertedByBrightness(byte[] jpegBytes)
    {
        try
        {
            using var ms = new MemoryStream(jpegBytes);
            var bi = new System.Windows.Media.Imaging.BitmapImage();
            bi.BeginInit();
            bi.StreamSource = ms;
            bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bi.DecodePixelWidth = 100; // Klein laden fuer Geschwindigkeit
            bi.EndInit();
            bi.Freeze();

            // In Pixel-Array konvertieren
            var wb = new System.Windows.Media.Imaging.WriteableBitmap(bi);
            int stride = wb.PixelWidth * 4;
            byte[] pixels = new byte[stride * wb.PixelHeight];
            wb.CopyPixels(pixels, stride, 0);

            // Mittlere Helligkeit berechnen (nur Luminanz)
            long totalLum = 0;
            int count = 0;
            for (int i = 0; i < pixels.Length - 3; i += 4)
            {
                // BGRA Format
                int b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
                totalLum += (r * 299 + g * 587 + b * 114) / 1000; // ITU-R BT.601
                count++;
            }

            if (count == 0) return false;
            double avgLum = (double)totalLum / count;

            // Kanalbilder sind typischerweise dunkel (avgLum < 100)
            // Invertierte Bilder haben avgLum > 170
            return avgLum > 170;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Invertiert die Farben eines JPEG-Bildes.
    /// Decodiert JPEG → invertiert Pixeldaten → re-encodiert als JPEG.
    /// </summary>
    private static byte[] InvertJpegColors(byte[] jpegBytes)
    {
        try
        {
            // JPEG decodieren
            using var inputMs = new MemoryStream(jpegBytes);
            var decoder = new System.Windows.Media.Imaging.JpegBitmapDecoder(
                inputMs,
                System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);

            var frame = decoder.Frames[0];

            // In WriteableBitmap konvertieren
            var wb = new System.Windows.Media.Imaging.WriteableBitmap(frame);
            int stride = wb.PixelWidth * (wb.Format.BitsPerPixel / 8);
            byte[] pixels = new byte[stride * wb.PixelHeight];
            wb.CopyPixels(pixels, stride, 0);

            // Alle Farbkanaele invertieren (nicht Alpha)
            int bytesPerPixel = wb.Format.BitsPerPixel / 8;
            int alphaOffset = bytesPerPixel == 4 ? 3 : -1; // BGRA: Alpha ist Byte 3

            for (int i = 0; i < pixels.Length; i++)
            {
                // Alpha-Kanal nicht invertieren
                if (alphaOffset >= 0 && (i % bytesPerPixel) == alphaOffset)
                    continue;
                pixels[i] = (byte)(255 - pixels[i]);
            }

            // Zurueckschreiben
            wb.WritePixels(
                new System.Windows.Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight),
                pixels, stride, 0);

            // Als JPEG re-encodieren
            var encoder = new System.Windows.Media.Imaging.JpegBitmapEncoder
            {
                QualityLevel = 92
            };
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(wb));

            using var outputMs = new MemoryStream();
            encoder.Save(outputMs);
            return outputMs.ToArray();
        }
        catch
        {
            // Falls Inversion fehlschlaegt: Original zurueckgeben
            return jpegBytes;
        }
    }
}
