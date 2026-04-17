// AuswertungPro – Video-Selbsttraining: PDF-Protokolltabellen-Parser
// Liest Inspektionsprotokolle aus WinCan/IBAK-PDF-Exporten.
// Erkennt drei Formate:
//   Format 1 (Fretz/IBAK):       "Meter  Code  Beschreibung  MPEG  Foto  Stufe"
//   Format 2 (Abwasser Uri):     "POSITION [m] SK CODE  BEOBACHTUNG  VIDEO  FOTO"
//   Format 3 (IBAK direkt):      "Station  Zeit  Code  Strecke  Langtext  Uhr  Foto"
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
    // Auch: "Fliessrichtung  4.7 ..." (IBAK direkt: Text vor Meter)
    // V4.2: Meter mit oder ohne Einrueckung — Fretz-PDFs (ab 2023) haben keine Einrueckung.
    // Negative lookahead (?!\d|\.\d) verhindert Match auf Datums-Strings wie "23.04.2023".
    private static readonly Regex MeterRegex = new(
        @"^\s*(\d{1,4}[.,]\d{1,2})(?!\d|\.\d)(?:\s|$)",
        RegexOptions.Compiled);

    // V4.2: Zeile mit NUR einem VSA-Code (column-stacked Layout).
    private static readonly Regex CodeOnlyLineRegex = new(
        @"^\s*((?:B[A-Z]{2,5}[A-Z]?)|(?:AE[A-Z]{1,4}))\s*$",
        RegexOptions.Compiled);

    // IBAK direkt: "Fliessrichtung  4.7 00:03:24 BAAA" — Text vor Meter
    private static readonly Regex IbakMeterRegex = new(
        @"^[A-Za-z\u00C0-\u00FF]+\s+(\d{1,4}[.,]\d{1,2})\s",
        RegexOptions.Compiled);

    // IBAK direkt: Zeile ohne Meter aber mit Zeitcode + Code: "  00:00:00 BDB"
    private static readonly Regex TimeThenCodeRegex = new(
        @"^\s+(\d{1,2}:\d{2}:\d{2})\s+((?:B[A-Z]{2,5}[A-Z]?)|(?:AE[A-Z]{1,4}))\b",
        RegexOptions.Compiled);

    // VSA-Code: B-Codes (BAB, BBFA, BCAEA etc.) und AE-Codes (AEF, AECXC, AEDXP etc.)
    private static readonly Regex CodeRegex = new(
        @"\b((?:B[A-Z]{2,5}[A-Z]?)|(?:AE[A-Z]{1,4}))\b",
        RegexOptions.Compiled);

    // MPEG-Zeitcode: HH:MM:SS oder H:MM:SS
    private static readonly Regex MpegRegex = new(
        @"\b(\d{1,2}:\d{2}:\d{2})\b",
        RegexOptions.Compiled);

    // Zustandsstufe am Zeilenende: einzelne Ziffer 1-5 (Stufe 5 = Sofortmassnahme, Audit T2)
    private static readonly Regex StufeRegex = new(
        @"\s([1-5])\s*$",
        RegexOptions.Compiled);

    // Uhrlage: "bei 3 Uhr", "von 12 Uhr bis 6 Uhr", "bei 9 Uhr"
    private static readonly Regex ClockRegex = new(
        @"(?:bei|von)\s+(\d{1,2})\s*Uhr",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Stammdaten-Header die wir ueberspringen (und Tabellen-Start erkennen)
    private static readonly Regex HeaderRegex = new(
        @"Haltungsinspektion|Inspektionsbericht|Haltungsbilder|Fotos|Seite\s*:|POSITION \[m\]|m\+\s+.*Zustand|m\+\s+OP|Station\s+Zeit\s+Code",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Schachtnummer (am Anfang einer Beobachtungsgruppe)
    private static readonly Regex SchachtRegex = new(
        @"^\s{2,}(\d{2,}\.\d+|\d{5,})\s*$",
        RegexOptions.Compiled);

    // Fretz/Klartext → VSA-Code Mapping (fuer PDFs die Klartext statt Codes verwenden)
    private static readonly Dictionary<string, string> KlartextToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        // BC Bestandsaufnahme
        { "Rohranfang", "BCD" },
        { "Rohrende", "BCE" },
        { "Bogen", "BCC" },
        { "Kurve", "BCC" },
        { "Bogen nach links", "BCCA" },
        { "Bogen links", "BCCA" },
        { "Bogen nach rechts", "BCCB" },
        { "Bogen rechts", "BCCB" },
        { "Anschluss", "BCA" },
        { "Seitlicher Anschluss", "BCA" },
        { "Formst\u00FCck", "BCAAA" },
        { "Reparatur", "BCB" },
        { "Innenauskleidung", "BCBB" },

        // BA Bauliche Schaeden
        { "Riss", "BAB" },
        { "L\u00E4ngsriss", "BABA" },
        { "Querriss", "BABB" },
        { "Rissbildung", "BAB" },
        { "Bruch", "BAC" },
        { "Scherbe", "BACB" },
        { "Einsturz", "BACC" },
        { "Deformation", "BAA" },
        { "Verformung", "BAA" },
        { "Versatz", "BAJ" },
        { "Rohrverbindung", "BAJ" },
        { "Dichtring", "BAIA" },
        { "Oberfl\u00E4chenschaden", "BAF" },
        { "Korrosion", "BAFJ" },
        { "korrodiert", "BAFJ" },
        { "Abplatzung", "BAFB" },
        { "einragend", "BAG" },
        { "Anschluss einragend", "BAG" },

        // BB Betriebliche Stoerungen
        { "Wurzel", "BBA" },
        { "Wurzeleinwuchs", "BBA" },
        { "Inkrustation", "BBBA" },
        { "Kalk", "BBBA" },
        { "Fett", "BBBB" },
        { "Ablagerung", "BBC" },
        { "Ablagerungen", "BBC" },
        { "Sand", "BBCA" },
        { "Infiltration", "BBF" },
        { "Wasser fliesst", "BBFC" },
        { "Wasser tropft", "BBFB" },
        { "Hindernis", "BBE" },

        // AE Aenderungen
        { "Rohrmaterialwechsel", "AED" },
        { "Neue L\u00E4nge", "AEF" },
        { "Rohrprofilwechsel", "AECXC" },

        // BD Weitere
        { "Wasserspiegel", "BDD" },
        { "Allgemeinzustand", "BDA" },
    };

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

        // V4.2 Nachbesserung: Caesar-Decoder fuer IKAS-PDFs mit Custom-Font-Encoding.
        // Wirkt nur wenn Text verschoben ist (Check auf bekannte Wortmuster, sonst Identity).
        text = PdfProtocolExtractor.TryDecodeShiftedText(text);

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

            // IBAK direkt: "Fliessrichtung  4.7 00:03:24 BAAA" — Text vor Meter
            if (!meterMatch.Success)
                meterMatch = IbakMeterRegex.Match(line);

            // IBAK direkt: Zeile ohne Meter aber mit "00:00:00 BDB" — nutze letzten Meter
            if (!meterMatch.Success && inObservationTable)
            {
                var timeThenCode = TimeThenCodeRegex.Match(line);
                if (timeThenCode.Success)
                {
                    // Vorherigen Eintrag abschliessen
                    if (currentCode is not null)
                        entries.Add(BuildEntry(currentMeter, currentCode, descAccumulator, currentMpeg, currentStufe));

                    // Neuen Eintrag mit dem letzten Meter (oder null)
                    currentCode = timeThenCode.Groups[2].Value;
                    currentMpeg = timeThenCode.Groups[1].Value;
                    descAccumulator = line.Substring(timeThenCode.Index + timeThenCode.Length).Trim();
                    currentStufe = null;

                    var stufeMatch = StufeRegex.Match(line);
                    if (stufeMatch.Success && int.TryParse(stufeMatch.Groups[1].Value, out var s))
                        currentStufe = s;

                    continue;
                }
            }

            if (meterMatch.Success)
            {
                inObservationTable = true;

                var rest = line.Substring(meterMatch.Index + meterMatch.Length);
                var newMeter = ParseMeter(meterMatch.Groups[1].Value);

                // Pruefe ob die Zeile einen Code hat (VSA-Code oder Klartext-Mapping).
                var hasCodeInRest = CodeRegex.IsMatch(rest);

                // Klartext-Fallback: Fretz AG u.a. schreiben Klartext statt VSA-Codes
                if (!hasCodeInRest)
                {
                    var mappedCode = TryMapKlartext(rest);
                    if (mappedCode is not null)
                    {
                        hasCodeInRest = true;
                        // Klartext-Code in die Zeile injizieren (fuer ParseLineContent)
                        rest = mappedCode + " " + rest;
                    }
                }

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
                // V4.2: Column-stacked Layout — Code steht alleine auf eigener Zeile
                // nach einer Meter-Zeile. Startet neuen Eintrag.
                var codeOnly = CodeOnlyLineRegex.Match(line);
                if (codeOnly.Success && currentMeter.HasValue && currentCode is null)
                {
                    currentCode = codeOnly.Groups[1].Value;
                    continue;
                }

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

    /// <summary>Versucht Klartext (z.B. "Rohranfang") in einen VSA-Code zu uebersetzen.</summary>
    private static string? TryMapKlartext(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var trimmed = text.Trim();

        // Exakter Match zuerst
        if (KlartextToCode.TryGetValue(trimmed, out var code))
            return code;

        // Teilstring-Match: "Infiltration, Wasser fliesst" → "Infiltration" matcht
        foreach (var (key, val) in KlartextToCode)
        {
            if (trimmed.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                return val;
            if (trimmed.Contains(key, StringComparison.OrdinalIgnoreCase))
                return val;
        }

        return null;
    }

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

    /// <summary>Loest den Pfad zu pdftotext.exe auf. V4.2: Erweitert um bekannte Installationspfade.</summary>
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

        // 3. V4.2: Bekannte Installationspfade auf Windows durchsuchen.
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            Path.Combine(programFiles, "Git", "mingw64", "bin", "pdftotext.exe"),
            Path.Combine(programFilesX86, "Git", "mingw64", "bin", "pdftotext.exe"),
            Path.Combine(programFiles, "poppler", "bin", "pdftotext.exe"),
            Path.Combine(programFiles, "poppler", "Library", "bin", "pdftotext.exe"),
            Path.Combine(programFilesX86, "poppler", "bin", "pdftotext.exe"),
            Path.Combine(localAppData, "Programs", "poppler", "bin", "pdftotext.exe"),
            // Chocolatey
            @"C:\ProgramData\chocolatey\bin\pdftotext.exe",
        };
        foreach (var c in candidates)
        {
            if (!string.IsNullOrEmpty(c) && File.Exists(c))
            {
                // Cachen damit nicht jeder Parse-Call das Filesystem scanned.
                PdfToTextExePath = c;
                return c;
            }
        }

        // 4. Im PATH (Process.Start sucht dort).
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
