using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

/// <summary>
/// Sucht das passende Video zu einer Haltung in einem Export-Verzeichnis.
/// 4-stufige Suche mit absteigender Konfidenz:
///   Stufe 1 – Exakter Match: HaltungKey im Dateinamen           (0.99)
///   Stufe 2 – Regex-Match:   Knotennummern im Dateinamen        (0.90)
///   Stufe 3 – DB-Dateiname:  OMM_FileName aus SECOBSMM          (0.95)
///   Stufe 4 – Ordner-Match:  Datei liegt in Ordner mit HaltungKey (0.80)
/// </summary>
public static class VideoResolver
{
    // Unterstuetzte Video-Erweiterungen (WinCan/IKAS typisch)
    private static readonly string[] VideoExtensions =
        [".mpg", ".mpeg", ".avi", ".mp4", ".wmv"];

    /// <summary>
    /// Versucht das passende Video fuer eine Haltung zu finden.
    /// Gibt das Match mit der hoechsten Konfidenz zurueck oder null.
    /// </summary>
    /// <param name="haltungKey">Haltungsschluessel, z.B. "S1234-S5678" oder "HA_0042"</param>
    /// <param name="exportRoot">Wurzelverzeichnis des Video-Exports</param>
    /// <param name="dbFileNames">Optionale Dateinamen aus SECOBSMM (OMM_FileName)</param>
    public static VideoMatch? Resolve(
        string haltungKey,
        string exportRoot,
        List<string>? dbFileNames = null)
    {
        if (string.IsNullOrWhiteSpace(haltungKey) || !Directory.Exists(exportRoot))
            return null;

        // Alle Video-Dateien im Export-Verzeichnis rekursiv sammeln
        var allVideos = EnumerateVideos(exportRoot);
        if (allVideos.Count == 0) return null;

        var candidates = new List<VideoMatch>();

        // -----------------------------------------------------------------------
        // Stufe 3: DB-Dateinamen (zuerst, da spezifisch — hoehere Konfidenz als Stufe 2)
        // -----------------------------------------------------------------------
        if (dbFileNames != null && dbFileNames.Count > 0)
        {
            foreach (var dbFile in dbFileNames)
            {
                string dbFileNorm = Path.GetFileName(dbFile).ToUpperInvariant();
                var match = allVideos.FirstOrDefault(v =>
                    Path.GetFileName(v).ToUpperInvariant() == dbFileNorm);

                if (match != null)
                {
                    candidates.Add(new VideoMatch(
                        HaltungKey: haltungKey,
                        FilePath: match,
                        MatchType: "db_filename",
                        Confidence: 0.95));
                }
            }
        }

        // -----------------------------------------------------------------------
        // Stufe 1: Exakter Match — HaltungKey kommt im Dateinamen vor (0.99)
        // -----------------------------------------------------------------------
        string keyNorm = haltungKey.ToUpperInvariant();
        foreach (var video in allVideos)
        {
            string nameNorm = Path.GetFileNameWithoutExtension(video).ToUpperInvariant();
            if (nameNorm.Contains(keyNorm, StringComparison.Ordinal))
            {
                candidates.Add(new VideoMatch(
                    HaltungKey: haltungKey,
                    FilePath: video,
                    MatchType: "exact",
                    Confidence: 0.99));
            }
        }

        // -----------------------------------------------------------------------
        // Stufe 2: Regex — Knotennummern aus HaltungKey extrahieren (0.90)
        // -----------------------------------------------------------------------
        var knotenNummern = ExtractKnotenNummern(haltungKey);
        if (knotenNummern.Count > 0)
        {
            foreach (var video in allVideos)
            {
                string nameNorm = Path.GetFileNameWithoutExtension(video).ToUpperInvariant();
                // Beide Knotennummern muessen im Dateinamen vorkommen
                bool alleGefunden = knotenNummern.All(k =>
                    nameNorm.Contains(k, StringComparison.Ordinal));

                if (alleGefunden)
                {
                    candidates.Add(new VideoMatch(
                        HaltungKey: haltungKey,
                        FilePath: video,
                        MatchType: "knotennummer_regex",
                        Confidence: 0.90));
                }
            }
        }

        // -----------------------------------------------------------------------
        // Stufe 4: Ordner-Match — Ordnername enthaelt HaltungKey (0.80)
        // -----------------------------------------------------------------------
        foreach (var video in allVideos)
        {
            string? ordner = Path.GetDirectoryName(video);
            if (ordner == null) continue;

            string ordnerNorm = ordner.ToUpperInvariant();
            if (ordnerNorm.Contains(keyNorm, StringComparison.Ordinal))
            {
                candidates.Add(new VideoMatch(
                    HaltungKey: haltungKey,
                    FilePath: video,
                    MatchType: "folder",
                    Confidence: 0.80));
            }
        }

        if (candidates.Count == 0) return null;

        // Bestes Ergebnis: hoechste Konfidenz, bei Gleichstand kuerzester Pfad (spezifischer)
        return candidates
            .OrderByDescending(c => c.Confidence)
            .ThenBy(c => c.FilePath.Length)
            .First();
    }

    /// <summary>
    /// Listet alle Video-Dateien rekursiv im angegebenen Verzeichnis auf.
    /// </summary>
    private static List<string> EnumerateVideos(string rootDir)
    {
        try
        {
            return Directory
                .EnumerateFiles(rootDir, "*", SearchOption.AllDirectories)
                .Where(f => VideoExtensions.Contains(
                    Path.GetExtension(f).ToLowerInvariant()))
                .ToList();
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    /// <summary>
    /// Extrahiert Knotennummern aus einem Haltungsschluessel.
    /// Beispiele:
    ///   "S1234-S5678" → ["1234", "5678"]
    ///   "KS.42-KS.99" → ["42", "99"]
    ///   "HA_0042"     → ["0042"]
    /// Gibt leere Liste zurueck wenn keine Zahlen gefunden.
    /// </summary>
    private static List<string> ExtractKnotenNummern(string haltungKey)
    {
        // Zahlenfolgen (mind. 2 Stellen) extrahieren
        var matches = Regex.Matches(haltungKey, @"\d{2,}");
        return matches
            .Select(m => m.Value.ToUpperInvariant())
            .Distinct()
            .ToList();
    }
}
