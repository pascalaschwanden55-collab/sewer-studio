namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Ergebnis der Erkennung eines KIAS-/IBAK-Exportordners.
/// </summary>
public sealed record KiasExportDetectionResult(
    bool IsKias,
    bool HasArizonaFdb,
    bool HasFilmFolder,
    bool HasReportFolder,
    bool HasDatenTxt,
    int HoldingPdfCount,
    int LateralPdfCount,
    int GegenrichtungVideoCount,
    int RepeatTakeVideoCount,
    string? Reason);

/// <summary>
/// Erkennt einen KIAS-/IBAK-Export anhand seiner Ordner und Nutzdateien.
/// </summary>
public interface IKiasExportPatternDetector
{
    KiasExportDetectionResult Detect(string exportRoot);
}
