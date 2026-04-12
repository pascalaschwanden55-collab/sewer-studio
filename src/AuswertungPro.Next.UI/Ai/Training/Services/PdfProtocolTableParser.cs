// AuswertungPro – Video-Selbsttraining: PDF-Protokolltabellen-Parser
// Liest Inspektionsprotokolle aus WinCan/IBAK-PDF-Exporten.
// Erkennt zwei Formate:
//   Format 1 (Fretz/IBAK):  "Meter  Code  Beschreibung  MPEG  Foto  Stufe"
//   Format 2 (Abwasser Uri): "POSITION [m] SK CODE  BEOBACHTUNG  VIDEO  FOTO"
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Shared;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Ai.Training.Services;

/// <summary>
/// Parst die Beobachtungstabelle aus Inspektions-PDFs.
/// Gibt eine Liste von ProtocolEntry-Objekten zurueck (Meter, Code, Beschreibung, MPEG, Zustandsstufe).
/// Nutzt pdftotext (muss im PATH sein oder via FfmpegLocator auffindbar).
/// </summary>
public static class PdfProtocolTableParser
{
    // ═══ Regex-Patterns ═════════════════════════════════════════════════

    // Meter am Zeilenanfang: "  0.00", "  28.40", "  142.49", "    250.61"
    private static readonly Regex MeterRegex = new(
        @"^\s{2,}(\d{1,4}[.,]\d{1,2})\s",
        RegexOptions.Compiled);

    // VSA-Code: B-Codes (BAB, BBFA, BCAEA etc.) und AE-Codes (AEF, AECXC, AEDXP etc.)
    private static readonly Regex CodeRegex = new(
        @"\b((?:B[A-Z]{2,5}[A-Z]?)|(?:AE[A-Z]{1,4}))\b",
        RegexOptions.Compiled);

    // MPEG-Zeitcode: HH:MM:SS oder H:MM:SS
    private static readonly Regex MpegRegex = new(
        @"\b(\d{1,2}:\d{2}:\d{2})\b",
        RegexOptions.Compiled);

    // Zustandsstufe am Zeilenende: einzelne Ziffer 1-4
    private static readonly Regex StufeRegex = new(
        @"\s([1-4])\s*$",
        RegexOptions.Compiled);

    // Uhrlage: "bei 3 Uhr", "von 12 Uhr bis 6 Uhr", "bei 9 Uhr"
    private static readonly Regex ClockRegex = new(
        @"(?:bei|von)\s+(\d{1,2})\s*Uhr",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Stammdaten-Header die wir ueberspringen
    private static readonly Regex HeaderRegex = new(
        @"Haltungsinspektion|Inspektionsbericht|Haltungsbilder|Fotos|Seite\s*:|POSITION \[m\]|m\+\s+.*Zustand|m\+\s+OP",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Schachtnummer (am Anfang einer Beobachtungsgruppe)
    private static readonly Regex SchachtRegex = new(
        @"^\s{2,}(\d{2,}\.\d+|\d{5,})\s*$",
        RegexOptions.Compiled);

    // ═══ Hauptmethode ═══════════════════════════════════════════════════

    /// <summary>
    /// Parst ein Inspektions-PDF und extrahiert alle Protokolleintraege.
    /// </summary>
    /// <summary>Statisch gesetzter pdftotext-Pfad (aus AppSettings/DiagnosticsOptions).</summary>
    public static string? PdfToTextExePath { get; set; }

    /// <param name="pdfPath">Pfad zur PDF-Datei.</param>
    /// <param name="log">Optionaler Logger.</param>
    /// <returns>Stammdaten + Liste von ProtocolEntry-Objekten.</returns>
    public static PdfProtocolParseResult Parse(string pdfPath, ILogger? log = null)
    {
        if (!File.Exists(pdfPath))
            return PdfProtocolParseResult.Empty($"PDF nicht gefunden: {pdfPath}");

        var text = ExtractText(pdfPath);
        if (string.IsNullOrWhiteSpace(text))
            return PdfProtocolParseResult.Empty($"PDF-Text leer — pdftotext nicht gefunden oder fehlgeschlagen. Pfad: {ResolvePdfToTextPath()}");

        var lines = text.Split('\n');
        var entries = new List<ProtocolEntry>();
        var stammdaten = ParseStammdaten(lines);

        // Zustandsvariablen fuer mehrzeilige Eintraege
        double? currentMeter = null;
        string? currentCode = null;
        string? currentMpeg = null;
        string descAccumulator = "";
        int? currentStufe = null;
        bool inObservationTable = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            // Header/Seitenumbruch ueberspringen
            if (HeaderRegex.IsMatch(line) || string.IsNullOrWhiteSpace(line))
            {
                // Wenn wir in der Tabelle waren und einen Header treffen → Seitenumbruch
                if (inObservationTable && HeaderRegex.IsMatch(line))
                    continue;
                continue;
            }

            // Schachtnummer (z.B. "  35625" oder "  06.242583") — markiert Tabellenanfang
            if (SchachtRegex.IsMatch(line))
            {
                inObservationTable = true;
                continue;
            }

            // Skala-Zeile (z.B. "Skala 1:31") ueberspringen
            if (line.Contains("Skala ", StringComparison.OrdinalIgnoreCase))
            {
                inObservationTable = true;
                continue;
            }

            // Versuche Meter am Zeilenanfang zu finden
            var meterMatch = MeterRegex.Match(line);
            if (meterMatch.Success)
            {
                inObservationTable = true;

                var rest = line.Substring(meterMatch.Index + meterMatch.Length);
                var newMeter = ParseMeter(meterMatch.Groups[1].Value);

                // FIX: Pruefe ob die Zeile auch einen Code hat.
                // Zeilen mit NUR Meter (kein Code) sind Folgezeilen — kein neuer Eintrag starten.
                var hasCodeInRest = CodeRegex.IsMatch(rest);

                if (hasCodeInRest)
                {
                    // Vorherigen Eintrag abschliessen
                    if (currentCode is not null)
                    {
                        entries.Add(BuildEntry(currentMeter, currentCode, descAccumulator, currentMpeg, currentStufe));
                    }

                    // Neuen Eintrag starten
                    currentMeter = newMeter;
                    currentCode = null;
                    currentMpeg = null;
                    descAccumulator = "";
                    currentStufe = null;

                    ParseLineContent(rest, ref currentCode, ref descAccumulator, ref currentMpeg, ref currentStufe);
                }
                else
                {
                    // Nur-Meter-Zeile: Meter merken (fuer naechsten Eintrag), aber als Fortsetzung behandeln
                    // Typisch: Mehrzeilige Beschreibung wo der Meter auf einer eigenen Zeile steht
                    if (currentCode is null)
                    {
                        // Noch kein aktiver Eintrag — Meter fuer den naechsten speichern
                        currentMeter = newMeter;
                    }
                    // Sonst: Rest als Beschreibungs-Fortsetzung parsen
                    ParseLineContent(rest, ref currentCode, ref descAccumulator, ref currentMpeg, ref currentStufe);
                }
            }
            else if (inObservationTable)
            {
                // Fortsetzungszeile (kein Meter am Anfang)
                // Koennte Code, Beschreibung oder MPEG enthalten
                ParseLineContent(line, ref currentCode, ref descAccumulator, ref currentMpeg, ref currentStufe);
            }
        }

        // Letzten Eintrag abschliessen
        if (currentCode is not null)
        {
            entries.Add(BuildEntry(currentMeter, currentCode, descAccumulator, currentMpeg, currentStufe));
        }

        log?.LogInformation("PDF-Protokoll geparst: {Count} Eintraege aus {Path}",
            entries.Count, Path.GetFileName(pdfPath));

        return new PdfProtocolParseResult
        {
            Entries = entries,
            Stammdaten = stammdaten,
            SourcePath = pdfPath
        };
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

    // ═══ ProtocolEntry bauen ═════════════════════════════════════════════

    private static ProtocolEntry BuildEntry(
        double? meter, string code, string beschreibung, string? mpeg, int? stufe)
    {
        var entry = new ProtocolEntry
        {
            Code = code.Replace(".", "").Trim().ToUpperInvariant(),
            Beschreibung = beschreibung.Trim(),
            MeterStart = meter,
            MeterEnd = meter,
            IsStreckenschaden = false,
            Mpeg = mpeg,
            Zeit = ParseMpeg(mpeg),
            Source = ProtocolEntrySource.Imported
        };

        // Uhrlage aus Beschreibung extrahieren
        if (!string.IsNullOrEmpty(beschreibung))
        {
            var clockMatch = ClockRegex.Match(beschreibung);
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

                // Zustandsstufe in CodeMeta speichern
                if (stufe.HasValue)
                    entry.CodeMeta.Severity = stufe.Value.ToString();
            }
        }

        if (entry.CodeMeta is null && stufe.HasValue)
        {
            entry.CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = entry.Code,
                Severity = stufe.Value.ToString()
            };
        }

        return entry;
    }

    // ═══ Stammdaten extrahieren ══════════════════════════════════════════

    private static PdfStammdaten ParseStammdaten(string[] lines)
    {
        var result = new PdfStammdaten();
        var fullText = string.Join("\n", lines.Take(40)); // Stammdaten sind im Header

        // Haltungsname
        var haltungMatch = Regex.Match(fullText,
            @"Haltung\s*[\n\r]*\s*([\d.]+\s*-\s*[\d.]+)", RegexOptions.IgnoreCase);
        if (haltungMatch.Success)
            result.Haltungsname = haltungMatch.Groups[1].Value.Trim();

        // Material
        var matMatch = Regex.Match(fullText,
            @"Material\s+([A-Za-zäöüÄÖÜ]+)", RegexOptions.IgnoreCase);
        if (matMatch.Success)
            result.Rohrmaterial = matMatch.Groups[1].Value.Trim();

        // Profil/DN
        var profilMatch = Regex.Match(fullText,
            @"(?:Kreisprofil|Profil[a-z]*)\s+(\d{2,4})\s*mm", RegexOptions.IgnoreCase);
        if (profilMatch.Success && int.TryParse(profilMatch.Groups[1].Value, out var dn))
            result.NennweiteMm = dn;

        // Alternativ: Profilhoehe
        if (!result.NennweiteMm.HasValue)
        {
            var phMatch = Regex.Match(fullText,
                @"Profilh[öo]he.*?(\d{2,4})\s*mm", RegexOptions.IgnoreCase);
            if (phMatch.Success && int.TryParse(phMatch.Groups[1].Value, out var ph))
                result.NennweiteMm = ph;
        }

        // Haltungslaenge
        var hlMatch = Regex.Match(fullText,
            @"(?:Rohrl[äa]nge|HL)\s*\[m\]\s*(\d+[.,]?\d*)", RegexOptions.IgnoreCase);
        if (hlMatch.Success)
            result.HaltungslaengeMeter = ParseMeter(hlMatch.Groups[1].Value);

        // Nutzungsart
        var nutzMatch = Regex.Match(fullText,
            @"Nutzungsart\s+([A-Za-zäöüÄÖÜ]+)", RegexOptions.IgnoreCase);
        if (nutzMatch.Success)
            result.Nutzungsart = nutzMatch.Groups[1].Value.Trim();

        // Videoname
        var filmMatch = Regex.Match(fullText,
            @"(?:Film|Videoname)\s+.*?([\w\-]+\.mp[g4])", RegexOptions.IgnoreCase);
        if (filmMatch.Success)
            result.VideoFileName = filmMatch.Groups[1].Value;

        return result;
    }

    // ═══ Hilfsmethoden ═══════════════════════════════════════════════════

    private static double? ParseMeter(string raw)
    {
        var cleaned = raw.Replace(',', '.').Trim();
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static TimeSpan? ParseMpeg(string? mpeg)
    {
        if (string.IsNullOrWhiteSpace(mpeg)) return null;
        return TimeSpan.TryParse(mpeg, out var ts) ? ts : null;
    }

    /// <summary>Loest den Pfad zu pdftotext.exe auf.</summary>
    private static string ResolvePdfToTextPath()
    {
        // 1. Explizit gesetzter Pfad (aus AppSettings)
        if (!string.IsNullOrWhiteSpace(PdfToTextExePath) && File.Exists(PdfToTextExePath))
            return PdfToTextExePath;

        // 2. Im tools-Ordner der App
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var toolsPath = Path.Combine(appDir, "tools", "pdftotext.exe");
        if (File.Exists(toolsPath))
            return toolsPath;

        // 3. Im PATH
        return "pdftotext";
    }

    /// <summary>Extrahiert Text aus PDF via pdftotext (layout-Modus).</summary>
    private static string ExtractText(string pdfPath)
    {
        try
        {
            var exePath = ResolvePdfToTextPath();
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"-layout \"{pdfPath}\" -",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc is null) return "";

            // stderr parallel lesen — verhindert Deadlock wenn stderr-Buffer voll
            var stderrTask = proc.StandardError.ReadToEndAsync();
            var text = proc.StandardOutput.ReadToEnd();

            if (!proc.WaitForExit(30000))
            {
                try { proc.Kill(); } catch { }
                Debug.WriteLine($"[PdfProtocolTableParser] pdftotext Timeout nach 30s fuer {Path.GetFileName(pdfPath)}");
                return "";
            }

            return text;
        }
        catch
        {
            return "";
        }
    }
}

/// <summary>Ergebnis des PDF-Protokoll-Parsings.</summary>
public sealed class PdfProtocolParseResult
{
    public List<ProtocolEntry> Entries { get; init; } = [];
    public PdfStammdaten Stammdaten { get; init; } = new();
    public string? SourcePath { get; init; }
    public string? Error { get; init; }

    public bool HasEntries => Entries.Count > 0;

    public static PdfProtocolParseResult Empty(string? error = null) => new() { Error = error };
}

/// <summary>Aus dem PDF extrahierte Stammdaten der Haltung.</summary>
public sealed class PdfStammdaten
{
    public string? Haltungsname { get; set; }
    public string? Rohrmaterial { get; set; }
    public int? NennweiteMm { get; set; }
    public double? HaltungslaengeMeter { get; set; }
    public string? Nutzungsart { get; set; }
    public string? VideoFileName { get; set; }
}
