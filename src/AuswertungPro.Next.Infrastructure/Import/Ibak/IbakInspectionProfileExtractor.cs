using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Infrastructure.Import.WinCan;

namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

/// <summary>
/// Extrahiert Inspektionsprofile (analog InspectionProfileExtractor.ExtractFromDb3)
/// aus einem KIAS/IBAK-Export-Ordner. Quellen:
///   - Beobachtungen: Daten.txt (Zeit, Meterstand, VSA-Code, Beschreibung)
///   - Stammdaten:    StammdatenAggregator (XTF + PDF + FDB)
///   - Videos:        Film/-Ordner mit KIAS-Konvention (H_/L_/~G/~1)
/// </summary>
public static class IbakInspectionProfileExtractor
{
    // Daten.txt-Beobachtungs-Zeile: "    00:01:24    0.70 m  AED     <Beschreibung>"
    private static readonly Regex ObsRx = new(
        @"^\s*(?<t>\d{2}:\d{2}:\d{2})\s+(?<m>[\d.,]+)\s*m\s+(?<code>[A-Z0-9]+)\s+(?<desc>.*)$",
        RegexOptions.Compiled);

    // Streckenschaden-Marker A01..A99 / B01..B99 (Anfang/Ende Streckenschaden)
    private static readonly Regex RangeMarkerRx = new(@"^[AB]\d{2}$", RegexOptions.Compiled);

    // Eingebetteter VSA-Code in Beschreibung von Range-Markern: "BAF Pos: 4-8; ..."
    private static readonly Regex EmbeddedCodeRx = new(@"^([A-Z]{3,5})\b", RegexOptions.Compiled);

    /// <summary>
    /// Extrahiert pro Haltung in Daten.txt ein InspectionProfile inklusive
    /// Video-Pfad-Zuordnung (KIAS H_/L_-Konvention) und Stammdaten-Laenge.
    /// </summary>
    public static List<InspectionProfile> ExtractFromExportRoot(string exportRoot)
    {
        var result = new List<InspectionProfile>();
        if (string.IsNullOrWhiteSpace(exportRoot) || !Directory.Exists(exportRoot))
            return result;

        var datenTxt = FindDatenTxt(exportRoot);
        if (string.IsNullOrWhiteSpace(datenTxt))
            return result;

        // Stammdaten zur Laengen-Bestimmung pro Haltung.
        var (stammdaten, _) = StammdatenAggregator.Build(exportRoot, messages: null);
        var videoIndex = BuildVideoIndex(exportRoot);

        var sections = ParseDatenTxt(datenTxt);
        foreach (var sec in sections)
        {
            var key = NormalizeKey(sec.Holding);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            // Events bauen
            var events = new List<ProfileEvent>(sec.Observations.Count);
            foreach (var obs in sec.Observations)
            {
                events.Add(new ProfileEvent(
                    ZeitSek:        obs.TimeSek,
                    Meter:          obs.Meter,
                    CodeMain:       obs.MainCode,
                    CodeFull:       obs.FullCode,
                    Char1:          obs.Char1,
                    Char2:          obs.Char2,
                    Uhr1:           obs.Uhr1,
                    Uhr2:           obs.Uhr2,
                    Q1:             obs.Q1,
                    Streckenlaenge: obs.Streckenlaenge,
                    Bemerkung:      obs.Bemerkung));
            }

            // Stammdaten-Laenge
            double? laengeM = null;
            if (stammdaten.TryGetValue(key, out var sd))
                laengeM = sd.Laenge_m.Value;

            // BCE-Meter als Fallback fuer Laenge
            laengeM ??= events.Where(e => string.Equals(e.CodeMain, "BCE", StringComparison.OrdinalIgnoreCase))
                              .Max(e => e.Meter ?? 0);
            if (laengeM == 0) laengeM = null;

            var dauerSek = events.Count > 0 ? events.Max(e => e.ZeitSek) : 0;

            // Video-Pfad
            var (videoPfad, ambig) = ResolveVideo(key, videoIndex);

            // Quality-Flags
            var hasBcd = events.Any(e => string.Equals(e.CodeMain, "BCD", StringComparison.OrdinalIgnoreCase));
            var hasBce = events.Any(e => string.Equals(e.CodeMain, "BCE", StringComparison.OrdinalIgnoreCase));
            var warnings = new List<string>();
            if (videoPfad is null) warnings.Add("Kein Video gefunden");
            if (laengeM is null)   warnings.Add("Keine Haltungslaenge in Stammdaten + kein BCE");

            var qf = new QualityFlags(
                MissingVideo:           videoPfad is null,
                MissingSectionLength:   laengeM is null,
                NonMonotonicDistance:   IsNonMonotonic(events.Select(e => e.Meter)),
                NonMonotonicTime:       IsNonMonotonic(events.Select(e => (double?)e.ZeitSek)),
                DuplicateEventsSameTime: events.GroupBy(e => e.ZeitSek).Any(g => g.Count() > 1),
                MissingBcd:             !hasBcd,
                MissingBce:             !hasBce,
                AmbiguousVideoMatch:    ambig,
                FewEvents:              events.Count < 5,
                Warnings:               warnings);

            // Statistik
            double? codePerMeter = (laengeM.HasValue && laengeM.Value > 0)
                ? events.Count / laengeM.Value
                : null;

            double mittlereLueckeSek = events.Count > 1
                ? events.Zip(events.Skip(1), (a, b) => b.ZeitSek - a.ZeitSek).Average()
                : 0;
            double? mittlereLueckeM = events.Count > 1
                ? events.Zip(events.Skip(1), (a, b) => (b.Meter ?? 0) - (a.Meter ?? 0)).Where(d => d > 0).DefaultIfEmpty(0).Average()
                : null;
            double? speed = (laengeM.HasValue && laengeM.Value > 0 && dauerSek > 0)
                ? laengeM.Value / dauerSek
                : null;

            var stats = new ProfileStatistik(
                CodierungenProMeter:     codePerMeter,
                MittlereLueckeSek:       mittlereLueckeSek,
                MittlereLueckeM:         mittlereLueckeM,
                FahrgeschwindigkeitMS:   speed);

            result.Add(new InspectionProfile(
                HaltungKey:             sec.Holding,
                LaengeM:                laengeM,
                DauerSekunden:          dauerSek,
                VideoPfad:              videoPfad,
                VideoMatchConfidence:   videoPfad is null ? 0.0 : (ambig ? 0.5 : 0.9),
                Ereignisse:             events,
                Segmente:               Array.Empty<ProfileSegment>(),
                Luecken:                Array.Empty<ProfileGap>(),
                Statistik:              stats,
                QualityFlags:           qf));
        }

        return result;
    }

    // -------------------------------------------------------------------------
    // Helfer: Daten.txt-Parser (vereinfachte Variante des IbakExportImportService)
    // -------------------------------------------------------------------------

    private sealed record Observation(
        double TimeSek,
        double? Meter,
        string MainCode,
        string FullCode,
        string? Char1,
        string? Char2,
        string? Uhr1,
        string? Uhr2,
        string? Q1,
        double? Streckenlaenge,
        string? Bemerkung);

    private sealed record Section(string Holding, List<Observation> Observations);

    private static List<Section> ParseDatenTxt(string path)
    {
        var sections = new List<Section>();
        Section? current = null;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var enc = TryDetectWin1252(path) ? Encoding.GetEncoding(1252) : Encoding.UTF8;

        foreach (var raw in File.ReadLines(path, enc))
        {
            var line = raw?.TrimEnd() ?? "";
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Header: Zeile beginnt nicht mit Whitespace + ist keine Beobachtung
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && !ObsRx.IsMatch(line))
            {
                current = new Section(line.Trim(), new List<Observation>());
                sections.Add(current);
                continue;
            }

            if (current is null) continue;

            var m = ObsRx.Match(line);
            if (!m.Success) continue;

            var ts = ParseTime(m.Groups["t"].Value);
            var meter = ParseDouble(m.Groups["m"].Value);
            var code = m.Groups["code"].Value.Trim();
            var desc = m.Groups["desc"].Value.Trim();

            var (mainCode, char1, char2, uhr1, uhr2, q1, streckLen, eingebettet) = ResolveCode(code, desc);
            current.Observations.Add(new Observation(
                TimeSek:        ts,
                Meter:          meter,
                MainCode:       mainCode,
                FullCode:       BuildFullCode(mainCode, char1, char2),
                Char1:          char1,
                Char2:          char2,
                Uhr1:           uhr1,
                Uhr2:           uhr2,
                Q1:             q1,
                Streckenlaenge: streckLen,
                Bemerkung:      eingebettet ?? desc));
        }
        return sections;
    }

    private static (string Main, string? Ch1, string? Ch2, string? U1, string? U2, string? Q1, double? StrLen, string? embedDesc)
        ResolveCode(string code, string desc)
    {
        // Streckenschaden-Marker A01/B02 -> echten Code aus Beschreibung holen
        if (RangeMarkerRx.IsMatch(code) && !string.IsNullOrWhiteSpace(desc))
        {
            var em = EmbeddedCodeRx.Match(desc.TrimStart());
            if (em.Success)
                return (em.Groups[1].Value, null, null, null, null, null, null, desc);
        }

        // Char1/Char2 aus angehaengten Buchstaben (z.B. BAB.B.A -> main BAB, ch1 B, ch2 A)
        // Konservative Heuristik: nur wenn Code-Format "XXX.X" oder "XXX.X.X" ist.
        var parts = code.Split('.');
        var main = parts[0];
        var ch1 = parts.Length > 1 ? parts[1] : null;
        var ch2 = parts.Length > 2 ? parts[2] : null;

        // Uhrzeit aus Beschreibung "Uhr=10" oder ", Uhr 10"
        string? u1 = null, u2 = null;
        var uhrM = Regex.Match(desc, @"\bUhr\s*[:=]?\s*(\d{1,2})", RegexOptions.IgnoreCase);
        if (uhrM.Success) u1 = uhrM.Groups[1].Value;

        // Streckenlaenge aus Beschreibung "Anfang (1)..." / "Ende (1)..."
        double? strLen = null;
        var lengthM = Regex.Match(desc, @"L[äa]nge\s*=?\s*([\d.,]+)\s*m", RegexOptions.IgnoreCase);
        if (lengthM.Success && double.TryParse(lengthM.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var l))
            strLen = l;

        return (main, ch1, ch2, u1, u2, null, strLen, null);
    }

    private static string BuildFullCode(string main, string? ch1, string? ch2)
    {
        var sb = new StringBuilder(main);
        if (!string.IsNullOrWhiteSpace(ch1)) { sb.Append('.'); sb.Append(ch1); }
        if (!string.IsNullOrWhiteSpace(ch2)) { sb.Append('.'); sb.Append(ch2); }
        return sb.ToString();
    }

    private static double ParseTime(string hhmmss)
    {
        var p = hhmmss.Split(':');
        if (p.Length != 3) return 0;
        if (int.TryParse(p[0], out var h) && int.TryParse(p[1], out var m) && int.TryParse(p[2], out var s))
            return h * 3600 + m * 60 + s;
        return 0;
    }

    private static double? ParseDouble(string s)
    {
        if (double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return d;
        return null;
    }

    // -------------------------------------------------------------------------
    // Helfer: Video-Index + Auflösung
    // -------------------------------------------------------------------------

    private static List<string> BuildVideoIndex(string exportRoot)
    {
        var film = Path.Combine(exportRoot, "Film");
        if (!Directory.Exists(film))
            return new List<string>();
        var exts = new[] { ".mpg", ".mpeg", ".mp4", ".avi", ".mov", ".mkv" };
        return Directory.EnumerateFiles(film, "*.*", SearchOption.AllDirectories)
            .Where(p => exts.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private static (string? Path, bool Ambig) ResolveVideo(string holdingKey, IReadOnlyList<string> videos)
    {
        if (videos.Count == 0) return (null, false);
        var hKey = NormKeyAlnum(holdingKey);

        var hits = videos.Where(v =>
        {
            var nameKey = NormKeyAlnum(Path.GetFileNameWithoutExtension(v));
            return nameKey.Contains(hKey, StringComparison.OrdinalIgnoreCase);
        }).ToList();

        // Hauptaufnahme bevorzugen: ohne ~G und ohne ~N-Suffix.
        if (hits.Count > 1)
        {
            var primary = hits.Where(v =>
            {
                var n = Path.GetFileNameWithoutExtension(v);
                return !KiasExportPattern.HasTildeSuffix(n);
            }).ToList();
            if (primary.Count == 1) hits = primary;
        }

        return hits.Count switch
        {
            1 => (hits[0], false),
            > 1 => (hits[0], true),
            _ => (null, false)
        };
    }

    private static string? FindDatenTxt(string exportRoot)
    {
        try
        {
            var candidates = Directory.EnumerateFiles(exportRoot, "Daten.txt", SearchOption.AllDirectories).ToList();
            return candidates.FirstOrDefault(p => p.IndexOf(Path.DirectorySeparatorChar + "Film" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
                   ?? candidates.FirstOrDefault();
        }
        catch { return null; }
    }

    private static bool TryDetectWin1252(string path)
    {
        // Kleine Heuristik: wenn die Datei 0xFC (ü) / 0xE4 (ä) / 0xF6 (ö) als Einzelbyte enthaelt -> Win1252.
        try
        {
            using var fs = File.OpenRead(path);
            var buf = new byte[Math.Min(fs.Length, 4096)];
            var n = fs.Read(buf, 0, buf.Length);
            for (var i = 0; i < n; i++)
                if (buf[i] is 0xFC or 0xE4 or 0xF6 or 0xDF) return true;
        }
        catch { }
        return false;
    }

    private static bool IsNonMonotonic(IEnumerable<double?> seq)
    {
        double? prev = null;
        foreach (var v in seq)
        {
            if (v is null) continue;
            if (prev.HasValue && v < prev) return true;
            prev = v;
        }
        return false;
    }

    private static string NormalizeKey(string raw)
        => (raw ?? string.Empty).Trim().Replace(" ", "").Replace("/", "-").Replace("–", "-").Replace("—", "-");

    private static string NormKeyAlnum(string value)
        => new string((value ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
