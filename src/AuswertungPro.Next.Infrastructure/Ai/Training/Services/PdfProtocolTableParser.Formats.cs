// AuswertungPro – PDF-Protokolltabellen-Parser: Format-Erkennung
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.CodeCatalog;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Services;

// PdfProtocolTableParser Format-spezifische Parser:
// TryParseBildbericht (IKAS Bildbericht), TryParseColumnStackedReport (KIT
// Bauinspekt mit gestapelten Spalten), TryParseHaltungsbilder (Abwasser-Uri-
// Format), ParseLineContent (Fretz/IBAK Standard-Tabellenzeilen).
// Aus dem Hauptdatei extrahiert (Slice 30a).
public static partial class PdfProtocolTableParser
{
    private static List<ProtocolEntry> TryParseBildbericht(string[] lines)
    {
        var entries = new List<ProtocolEntry>();

        double? meter = null;
        string? mpeg = null;
        string? code = null;
        var desc = new System.Text.StringBuilder();
        string? lage = null;

        void Flush()
        {
            if (code is null) return;
            var entry = new ProtocolEntry
            {
                Code = code.Replace(".", "").Trim().ToUpperInvariant(),
                Beschreibung = desc.ToString().Trim(),
                MeterStart = meter,
                MeterEnd = meter,
                IsStreckenschaden = false,
                Mpeg = mpeg,
                Zeit = ParseMpeg(mpeg),
                Source = ProtocolEntrySource.Imported
            };
            if (lage is not null)
            {
                entry.CodeMeta = new ProtocolEntryCodeMeta
                {
                    Code = entry.Code,
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["ClockPos1"] = lage
                    }
                };
            }
            entries.Add(entry);

            // Reset (Position kann fuer naechsten Block uebernommen werden, wird aber meist neu gesetzt)
            code = null;
            desc.Clear();
            lage = null;
            mpeg = null;
        }

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Neue Foto: -Zeile → vorherigen Block abschliessen
            if (BildberichtFotoRegex.IsMatch(line))
            {
                Flush();
                meter = null; mpeg = null; code = null; desc.Clear(); lage = null;
                continue;
            }

            var vMatch = BildberichtVideoRegex.Match(line);
            if (vMatch.Success) { mpeg = vMatch.Groups[1].Value; continue; }

            var pMatch = BildberichtPositionRegex.Match(line);
            if (pMatch.Success) { meter = ParseMeter(pMatch.Groups[1].Value); continue; }

            var cMatch = BildberichtCodeLineRegex.Match(line);
            if (cMatch.Success && code is null)
            {
                code = cMatch.Groups[1].Value;
                var rest = cMatch.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(rest)) desc.Append(rest);
                continue;
            }

            var lMatch = BildberichtLageRegex.Match(line);
            if (lMatch.Success)
            {
                if (lMatch.Groups[1].Success) lage = lMatch.Groups[1].Value;
                // Lage: ist die letzte Zeile eines Bildbericht-Eintrags → flush
                Flush();
                meter = null; mpeg = null; code = null; desc.Clear(); lage = null;
                continue;
            }

            // Tabellen-Header (Zustandsbericht, Von-Punkt) markiert das Ende des Bildberichts
            if (line.Contains("Von-Punkt", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Zustandsbericht", StringComparison.OrdinalIgnoreCase))
            {
                Flush();
                break;
            }

            // Fortsetzungszeile der Beschreibung
            if (code is not null
                && !line.Contains("Seite", StringComparison.OrdinalIgnoreCase)
                && !line.Contains("Haltung ", StringComparison.OrdinalIgnoreCase)
                && !HeaderRegex.IsMatch(line))
            {
                if (desc.Length > 0) desc.Append(' ');
                desc.Append(line.Trim());
            }
        }

        Flush();
        return entries;
    }

    // ═══ Column-Stacked Zustandsbericht (IBAK Caesar) ════════════════════
    /// <summary>
    /// Parst das column-stacked Format wo Beschreibung, Code-mit-Restmeter und Ist-Meter
    /// auf drei aufeinanderfolgenden Zeilen stehen:
    ///   "Rohranfang"
    ///   "(24,18 m)  BCD"
    ///   "0,00 m"
    /// </summary>
    private static List<ProtocolEntry> TryParseColumnStackedReport(string[] lines)
    {
        var entries = new List<ProtocolEntry>();
        for (int i = 0; i < lines.Length; i++)
        {
            var codeMatch = StackedRemainingMeterCodeRegex.Match(lines[i].TrimEnd());
            if (!codeMatch.Success) continue;

            var code = codeMatch.Groups[1].Value;

            // Beschreibung: davor (max 3 Zeilen rueckwaerts, ueberspringt Leerzeilen)
            string desc = "";
            for (int back = 1; back <= 3 && i - back >= 0; back++)
            {
                var prev = lines[i - back].Trim();
                if (string.IsNullOrWhiteSpace(prev)) continue;
                if (HeaderRegex.IsMatch(prev)) continue;
                if (StackedRemainingMeterCodeRegex.IsMatch(prev)) break;
                if (StackedActualMeterRegex.IsMatch(prev)) break;
                if (prev.Contains("Position", StringComparison.OrdinalIgnoreCase)
                    && prev.Contains("Kürzel", StringComparison.OrdinalIgnoreCase)) break;
                desc = prev;
                break;
            }

            // Meter: danach (max 3 Zeilen vorwaerts)
            double? meter = null;
            for (int fwd = 1; fwd <= 3 && i + fwd < lines.Length; fwd++)
            {
                var next = lines[i + fwd].Trim();
                if (string.IsNullOrWhiteSpace(next)) continue;
                var meterMatch = StackedActualMeterRegex.Match(next);
                if (meterMatch.Success)
                {
                    meter = ParseMeter(meterMatch.Groups[1].Value);
                    break;
                }
                // Andere Zeilen abbrechen
                break;
            }

            entries.Add(new ProtocolEntry
            {
                Code = code.ToUpperInvariant(),
                Beschreibung = desc,
                MeterStart = meter,
                MeterEnd = meter,
                IsStreckenschaden = false,
                Source = ProtocolEntrySource.Imported
            });
        }
        return entries;
    }

    // ═══ Haltungsbilder-Format (Fretz Foto-Galerie) ═════════════════════
    /// <summary>
    /// Parst Fretz "Haltungsbilder"-Foto-Galerien.
    /// Format: "[name].jpg, HH:MM:SS, X.XXm" + naechste Zeile = Klartext.
    /// Da pro Seite mehrere Bilder nebeneinander stehen koennen, sammeln wir
    /// alle (Zeit, Meter)-Tupel pro Zeile und ordnen sie der unmittelbar
    /// folgenden Klartext-Zeile zu (oder zwei wenn auch zweispaltig).
    /// </summary>
    private static List<ProtocolEntry> TryParseHaltungsbilder(string[] lines)
    {
        var entries = new List<ProtocolEntry>();
        var pendingMeters = new List<(double Meter, string Mpeg)>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) { pendingMeters.Clear(); continue; }

            // Bilderzeilen sammeln (eine Zeile, ein oder zwei Bilder)
            var matches = HaltungsbilderImageRegex.Matches(line);
            if (matches.Count > 0)
            {
                foreach (Match m in matches)
                    pendingMeters.Add((ParseMeter(m.Groups[2].Value) ?? 0, m.Groups[1].Value));
                continue;
            }

            // Klartext-Zeile direkt nach Bilderzeile?
            if (pendingMeters.Count == 0) continue;

            var trimmed = line.Trim();
            if (HeaderRegex.IsMatch(trimmed)) { pendingMeters.Clear(); continue; }
            if (trimmed.StartsWith("Seite", StringComparison.OrdinalIgnoreCase)) { pendingMeters.Clear(); continue; }

            // Zwei Klartext-Texte koennten in einer Zeile durch viele Spaces getrennt sein.
            var parts = Regex.Split(trimmed, @"\s{4,}")
                             .Where(p => !string.IsNullOrWhiteSpace(p))
                             .ToList();

            for (int p = 0; p < pendingMeters.Count && p < parts.Count; p++)
            {
                var klartext = parts[p].Trim();
                var code = TryMapKlartext(klartext);
                if (code is null) continue;

                var (meter, mpeg) = pendingMeters[p];
                var entry = new ProtocolEntry
                {
                    Code = code,
                    Beschreibung = klartext,
                    MeterStart = meter,
                    MeterEnd = meter,
                    IsStreckenschaden = false,
                    Mpeg = mpeg,
                    Zeit = ParseMpeg(mpeg),
                    Source = ProtocolEntrySource.Imported
                };

                var clockMatch = ClockRegex.Match(klartext);
                if (clockMatch.Success)
                {
                    entry.CodeMeta = new ProtocolEntryCodeMeta
                    {
                        Code = entry.Code,
                        Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["ClockPos1"] = clockMatch.Groups[1].Value
                        }
                    };
                }

                entries.Add(entry);
            }

            pendingMeters.Clear();
        }

        return entries;
    }

    // ═══ Zeileninhalt parsen ═════════════════════════════════════════════

    private static void ParseLineContent(
        string text,
        ref string? code,
        ref string description,
        ref string? mpeg,
        ref int? stufe)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // VSA-Code suchen (nur wenn noch keiner gefunden)
        if (code is null)
        {
            var codeMatch = CodeRegex.Match(text);
            if (codeMatch.Success)
            {
                code = codeMatch.Groups[1].Value;
                // Text nach dem Code ist Beschreibung
                var afterCode = text.Substring(codeMatch.Index + codeMatch.Length).Trim();
                if (!string.IsNullOrEmpty(afterCode))
                {
                    // MPEG und Stufe aus dem Rest extrahieren
                    var mpegMatch = MpegRegex.Match(afterCode);
                    if (mpegMatch.Success)
                    {
                        mpeg = mpegMatch.Groups[1].Value;
                        afterCode = afterCode.Substring(0, mpegMatch.Index).Trim();
                    }

                    var stufeMatch = StufeRegex.Match(afterCode);
                    if (stufeMatch.Success)
                    {
                        stufe = int.Parse(stufeMatch.Groups[1].Value);
                        afterCode = afterCode.Substring(0, stufeMatch.Index).Trim();
                    }

                    if (!string.IsNullOrEmpty(afterCode))
                        description = afterCode;
                }
                return;
            }
        }

        // MPEG suchen
        if (mpeg is null)
        {
            var mpegMatch = MpegRegex.Match(text);
            if (mpegMatch.Success)
                mpeg = mpegMatch.Groups[1].Value;
        }

        // Stufe suchen
        if (!stufe.HasValue)
        {
            var stufeMatch = StufeRegex.Match(text);
            if (stufeMatch.Success)
                stufe = int.Parse(stufeMatch.Groups[1].Value);
        }

        // Rest ist Beschreibungsfortsetzung (z.B. "von 12 Uhr bis 6 Uhr")
        var trimmed = text.Trim();
        // Foto-Dateinamen und Seitenreste ignorieren
        if (trimmed.Contains(".jpg", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains(".png", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Seite", StringComparison.OrdinalIgnoreCase))
            return;

        // Beschreibung anhaengen (mehrzeilige Texte)
        if (!string.IsNullOrWhiteSpace(trimmed) && code is not null)
        {
            // MPEG-Code und Foto-Referenzen nicht in Beschreibung aufnehmen
            var clean = MpegRegex.Replace(trimmed, "").Trim();
            clean = StufeRegex.Replace(clean, "").Trim();
            if (!string.IsNullOrEmpty(clean) && clean.Length > 2)
            {
                description = string.IsNullOrEmpty(description)
                    ? clean
                    : description + " " + clean;
            }
        }
    }
}
