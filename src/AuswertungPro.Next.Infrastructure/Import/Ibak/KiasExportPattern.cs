using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

/// <summary>
/// Zentraler Erkennungs- und Pattern-Service fuer KIAS/IBAK-Exporte
/// (so wie die Erstfeld_Jagdmatt_38454_0426-Struktur):
///
///   &lt;Projekt&gt;_Export/
///     Arizona.fdb              (Firebird 2.5, Topologie)
///     Data/Arizona.fdb         (alternativer Pfad)
///     Film/
///       Daten.txt              (IBAK-Beobachtungen)
///       H_&lt;haltung&gt;.mpg     (Haltung)
///       H_&lt;haltung&gt;~G.mpg   (Gegeninspektion)
///       H_&lt;haltung&gt;~1.mpg   (Wiederholungs-Aufnahme)
///       L_&lt;haltung&gt;.mpg     (Anschluss/Lateral)
///     Report/
///       H_&lt;haltung&gt;.pdf     (Haltungsbericht mit Stammdatenblock)
///       L_&lt;haltung&gt;.pdf
///     Foto/...
///     Bin/Bin.7z (KIAS-Viewer + fbembed.dll)
///
/// Wird verwendet von:
///   - KinsImportService.DetectFormats (Format-Erkennung)
///   - HoldingFolderDistributor (Dateiname-Fallback fuer Video-Match)
///   - HoldingVideoMatching (~G/~1-Suffix-Behandlung)
///   - IbakExportImportService (PDF-Stammdaten + FDB-Topologie laden)
/// </summary>
public static class KiasExportPattern
{
    public sealed record DetectionResult(
        bool IsKias,
        bool HasArizonaFdb,
        bool HasFilmFolder,
        bool HasReportFolder,
        bool HasDatenTxt,
        int  HoldingPdfCount,
        int  LateralPdfCount,
        int  GegenrichtungVideoCount,
        int  RepeatTakeVideoCount,
        string? Reason);

    /// <summary>
    /// Prueft ob der Ordner einem KIAS/IBAK-Export entspricht.
    /// Liefert IsKias=true wenn mindestens Arizona.fdb + Film/ + (Daten.txt ODER Report/H_*.pdf|L_*.pdf) vorhanden sind.
    /// </summary>
    public static DetectionResult Detect(string exportRoot)
    {
        if (string.IsNullOrWhiteSpace(exportRoot) || !Directory.Exists(exportRoot))
            return new DetectionResult(false, false, false, false, false, 0, 0, 0, 0, "Pfad nicht vorhanden");

        var hasFdb = HasFile(exportRoot, "Arizona.fdb")
                     || (Directory.Exists(Path.Combine(exportRoot, "Data"))
                         && HasFile(Path.Combine(exportRoot, "Data"), "*.fdb"));

        var filmDir = Path.Combine(exportRoot, "Film");
        var hasFilm = Directory.Exists(filmDir);
        var reportDir = Path.Combine(exportRoot, "Report");
        var hasReport = Directory.Exists(reportDir);
        var hasDatenTxt = hasFilm && File.Exists(Path.Combine(filmDir, "Daten.txt"));

        var holdingPdfs = 0;
        var lateralPdfs = 0;
        if (hasReport)
        {
            try
            {
                holdingPdfs = Directory.EnumerateFiles(reportDir, "H_*.pdf").Count();
                lateralPdfs = Directory.EnumerateFiles(reportDir, "L_*.pdf").Count();
            }
            catch { /* ignore */ }
        }

        var gegenrichtung = 0;
        var wiederholung = 0;
        if (hasFilm)
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(filmDir, "*.*"))
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (IsGegenrichtungName(name)) gegenrichtung++;
                    else if (HasTildeSuffix(name)) wiederholung++;
                }
            }
            catch { /* ignore */ }
        }

        var hasReportPdfs = (holdingPdfs + lateralPdfs) > 0;
        var isKias = hasFdb && hasFilm && (hasDatenTxt || hasReportPdfs);

        var reason = isKias
            ? $"KIAS erkannt: Arizona.fdb + Film/ + {(hasDatenTxt ? "Daten.txt" : "Report-PDFs")}"
            : $"Kein KIAS: hasFdb={hasFdb}, hasFilm={hasFilm}, hasDatenTxt={hasDatenTxt}, reportPdfs={holdingPdfs + lateralPdfs}";

        return new DetectionResult(
            IsKias: isKias,
            HasArizonaFdb: hasFdb,
            HasFilmFolder: hasFilm,
            HasReportFolder: hasReport,
            HasDatenTxt: hasDatenTxt,
            HoldingPdfCount: holdingPdfs,
            LateralPdfCount: lateralPdfs,
            GegenrichtungVideoCount: gegenrichtung,
            RepeatTakeVideoCount: wiederholung,
            Reason: reason);
    }

    /// <summary>
    /// Liest den Haltungsnamen aus dem KIAS/IBAK-Dateinamen ("H_&lt;haltung&gt;.pdf",
    /// "L_&lt;haltung&gt;.pdf", "H__&lt;haltung&gt;.pdf", "L__&lt;haltung&gt;.pdf",
    /// auch ".mpg"). Liefert null wenn kein KIAS-Schema.
    /// </summary>
    public static string? HoldingFromKiasFilename(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var name = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(name)) return null;

        if      (name.StartsWith("L__", StringComparison.OrdinalIgnoreCase)) name = name[3..];
        else if (name.StartsWith("L_",  StringComparison.OrdinalIgnoreCase)) name = name[2..];
        else if (name.StartsWith("H__", StringComparison.OrdinalIgnoreCase)) name = name[3..];
        else if (name.StartsWith("H_",  StringComparison.OrdinalIgnoreCase)) name = name[2..];
        else return null;

        // Suffixe abstreifen ("~G" Gegenrichtung, "~1"/"~2" Wiederholung).
        var tildeIdx = name.IndexOf('~');
        if (tildeIdx > 0)
            name = name[..tildeIdx];

        return name.Contains('-') ? name : null;
    }

    /// <summary>"&lt;haltung&gt;~G" am Ende.</summary>
    public static bool IsGegenrichtungName(string fileNameWithoutExt)
        => !string.IsNullOrWhiteSpace(fileNameWithoutExt)
           && (fileNameWithoutExt.EndsWith("~G", StringComparison.OrdinalIgnoreCase));

    /// <summary>Tilde-Suffix vorhanden ("~G", "~1", ...) - nicht Hauptaufnahme.</summary>
    public static bool HasTildeSuffix(string fileNameWithoutExt)
    {
        if (string.IsNullOrWhiteSpace(fileNameWithoutExt)) return false;
        var idx = fileNameWithoutExt.LastIndexOf('~');
        return idx > 0 && idx < fileNameWithoutExt.Length - 1;
    }

    private static bool HasFile(string dir, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly).Any();
        }
        catch { return false; }
    }
}
