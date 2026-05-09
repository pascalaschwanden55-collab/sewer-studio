// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using AuswertungPro.Next.Application.Ai.Training.Models;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Services;

// PdfProtocolExtractor Text-Parsing: Erkennt verschiedene PDF-Formate
// (IKAS Leitungsgrafik, Fretz/IBAK-Tabelle, IKAS Bildbericht, Multi-Line-
// Continuation) und extrahiert GroundTruthEntries (Code, Meter, Beschreibung,
// Zeit, Quantifizierung). Aus dem Hauptdatei extrahiert (Slice 16a).
public sealed partial class PdfProtocolExtractor
{
    private static IReadOnlyList<GroundTruthEntry> ParseEntriesFromText(string text)
    {
        var results = new List<GroundTruthEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Strategie 0: Mehrzeiliges Spalten-Format
        // Erkennung: Wenn der Text "m +\n" oder "m +" gefolgt von Spaltenheadern hat
        // UND Zeilen mit nur Meter oder Meter+Code existieren
        if (text.Contains("m +") || text.Contains("m+"))
        {
            var multiLineResults = ParseMultiLineTable(text, seen);
            if (multiLineResults.Count > 0)
                return multiLineResults;
        }

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

        // Strategie 2: Fretz-Tabellenformat (Foto + Zeit VOR Meter)
        // Format: "040  00:00:16  0.00  BCD  Rohranfang"
        foreach (Match m in FretzTablePattern.Matches(text))
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

        // Fretz-PDFs haben auch Zeilen ohne Timestamp (z.B. "27.70 BCE Rohrende").
        // Diese werden weiter unten durch die Fallback-Patterns ergaenzt.
        // Deshalb hier KEIN fruehes Return — stattdessen weiter zu Strategie 5/6.
        if (results.Count > 0)
            goto fretzFallback;

        // Strategie 2b: Fretz-Klartext (Meter + Klartext, kein VSA-Code)
        // Format: "  0.00  Rohranfang  00:00:20  1"
        // Der Klartext wird via VsaCodeTree.ReverseLookup in VSA-Codes uebersetzt.
        foreach (Match m in KlartextLinePattern.Matches(text))
        {
            var langtext = m.Groups["text"].Value.Trim();
            var resolvedCode = TryResolveFromLangtext(langtext);
            if (resolvedCode is null)
                continue;

            var entry = BuildEntry(
                m.Groups["meter"].Value,
                "",
                resolvedCode,
                "",
                langtext,
                m.Groups["time"].Success ? ParseTimestamp(m.Groups["time"].Value) : null);

            if (entry is not null && seen.Add(Sig(entry)))
                results.Add(entry);
        }

        if (results.Count > 0)
            goto fretzFallback;

        // Strategie 3: Standard-Tabellenformat (Zeit NACH Beschreibung, z.B. WinCan)
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

        // Strategie 4: IKAS Bildbericht (Label-Value Blöcke)
        results = ParseBildberichtBlocks(text, seen);
        if (results.Count > 0)
            return results;

        // Strategie 5: Bereichs-Muster (m1 – m2 CODE)
        // Wird auch als Ergaenzung nach Fretz-Strategie erreicht (fretzFallback),
        // um Zeilen ohne Timestamp zu erfassen (z.B. "27.70  BCE  Rohrende").
        fretzFallback:
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

        // Strategie 6: Einzel-Meter-Muster (@m CODE)
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
    /// Strategie 0: Mehrzeiliges Spalten-Format (KIT Bauinspekt / Fretz neue PDFs).
    /// PDF-Text hat Meter+Code auf einer Zeile, Beschreibung auf der naechsten.
    /// Erkennt auch: Meter allein → Code auf naechster Zeile → Text → Zeit.
    /// </summary>
    private static List<GroundTruthEntry> ParseMultiLineTable(string text, HashSet<string> seen)
    {
        var results = new List<GroundTruthEntry>();
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var codeRegex = new Regex($@"^\s*(?<code>{CodePattern})\s*$", RegexOptions.Compiled);
        var meterRegex = new Regex(@"^\s*(?<meter>\d{1,4}[.,]\d{1,3})(?:\s+[A-Z]\d{1,3})?\s*$", RegexOptions.Compiled);
        var meterCodeRegex = new Regex($@"^\s*(?<meter>\d{1,4}[.,]\d{1,3})(?:\s+[A-Z]\d{{1,3}})?\s+(?<code>{CodePattern})\s*$", RegexOptions.Compiled);
        var timeRegex = new Regex(@"(?<time>\d{2}:\d{2}:\d{2})", RegexOptions.Compiled);

        for (int i = 0; i < lines.Length; i++)
        {
            string? meterStr = null;
            string? code = null;
            int textLineStart = -1;

            // Variante A: Meter + Code auf gleicher Zeile
            var mcMatch = meterCodeRegex.Match(lines[i]);
            if (mcMatch.Success)
            {
                meterStr = mcMatch.Groups["meter"].Value;
                code = mcMatch.Groups["code"].Value;
                textLineStart = i + 1;
            }
            else
            {
                // Variante B: Meter allein, Code auf naechster Zeile
                var mMatch = meterRegex.Match(lines[i]);
                if (mMatch.Success && i + 1 < lines.Length)
                {
                    var cMatch = codeRegex.Match(lines[i + 1]);
                    if (cMatch.Success)
                    {
                        meterStr = mMatch.Groups["meter"].Value;
                        code = cMatch.Groups["code"].Value;
                        textLineStart = i + 2;
                    }
                    else
                    {
                        // Variante C: Meter allein, Klartext auf naechster Zeile (Fretz-Format)
                        // z.B. "0.00" gefolgt von "Rohranfang" → ReverseLookup → "BCD"
                        var nextLine = lines[i + 1].Trim();
                        var resolved = TryResolveFromLangtext(nextLine);
                        if (resolved is not null)
                        {
                            meterStr = mMatch.Groups["meter"].Value;
                            code = resolved;
                            textLineStart = i + 1; // Text IST die Klartext-Zeile
                        }
                    }
                }
            }

            if (meterStr == null || code == null || textLineStart >= lines.Length)
                continue;

            // Text sammeln: alles bis zur naechsten Zeile mit Timestamp oder naechstem Meter/Code
            var textParts = new List<string>();
            TimeSpan? zeit = null;
            for (int j = textLineStart; j < Math.Min(textLineStart + 5, lines.Length); j++)
            {
                var line = lines[j].Trim();
                if (string.IsNullOrWhiteSpace(line)) break;

                // Timestamp gefunden → Zeit merken und aufhoeren
                var tMatch = timeRegex.Match(line);
                if (tMatch.Success)
                {
                    zeit = ParseTimestamp(tMatch.Groups["time"].Value);
                    break;
                }

                // Naechster Meter oder Code → aufhoeren (gehoert zum naechsten Eintrag)
                if (meterRegex.IsMatch(line) || meterCodeRegex.IsMatch(line))
                    break;

                // "Stufe" / "Seite" Zeilen ueberspringen
                if (line.StartsWith("Stufe", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Seite", StringComparison.OrdinalIgnoreCase))
                    break;

                textParts.Add(line);
            }

            var beschreibung = string.Join(" ", textParts).Trim();
            if (beschreibung.Length < 2) beschreibung = code;

            var entry = BuildEntry(meterStr, "", code, "", beschreibung, zeit);
            if (entry is not null && seen.Add(Sig(entry)))
                results.Add(entry);
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
}
