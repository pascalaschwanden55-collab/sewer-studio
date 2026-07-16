using System;
using System.IO;
using AuswertungPro.Next.Application.Import;

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
    private static readonly IKiasExportPatternDetector Default =
        new KiasExportPatternDetectionService();

    public static IKiasExportPatternDetector Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IKiasExportPatternDetector detector)
        => throw new NotSupportedException(
            "Die globale KIAS-Erkennung kann nicht mehr ausgetauscht werden. " +
            "IKiasExportPatternDetector bitte per Konstruktor uebergeben.");

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
        var result = Current.Detect(exportRoot);
        return new DetectionResult(
            IsKias: result.IsKias,
            HasArizonaFdb: result.HasArizonaFdb,
            HasFilmFolder: result.HasFilmFolder,
            HasReportFolder: result.HasReportFolder,
            HasDatenTxt: result.HasDatenTxt,
            HoldingPdfCount: result.HoldingPdfCount,
            LateralPdfCount: result.LateralPdfCount,
            GegenrichtungVideoCount: result.GegenrichtungVideoCount,
            RepeatTakeVideoCount: result.RepeatTakeVideoCount,
            Reason: result.Reason);
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
}
