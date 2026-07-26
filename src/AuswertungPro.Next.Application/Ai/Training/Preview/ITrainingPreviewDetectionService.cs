namespace AuswertungPro.Next.Application.Ai.Training.Preview;

/// <summary>Waehlt, welches Detect-Modell ein Foto nur zur Vorschau prueft.</summary>
public enum TrainingPreviewModelKind
{
    ActiveStandard,
    BccTestCandidate,
}

/// <summary>Eine automatisch erkannte Box in echten Bildpixeln.</summary>
public sealed record TrainingPreviewDetection(
    double X1,
    double Y1,
    double X2,
    double Y2,
    string ClassName,
    double Confidence);

/// <summary>
/// Nur-lesendes Vorschauergebnis. Es ist absichtlich kein TrainingSample und kann
/// deshalb nicht versehentlich als Goldlabel gespeichert werden.
/// </summary>
public sealed record TrainingPreviewDetectionResult(
    bool Available,
    string? Error,
    TrainingPreviewModelKind ModelKind,
    string ModelName,
    string ModelSha256,
    IReadOnlyList<TrainingPreviewDetection> Detections,
    double InferenceTimeMs);

/// <summary>
/// Qualifikationsstand des aktiven Standard-Detektors (aus der Sidecar-Statusdatei).
/// Null = nicht abrufbar. Das Training Studio behandelt diesen Zustand wie
/// "nicht freigegeben" und startet keinen Standardmodell-Fototest.
/// </summary>
public sealed record TrainingDetectorQualification(bool Qualified, string? Reason);

public interface ITrainingPreviewDetectionService
{
    Task<TrainingPreviewDetectionResult> DetectAsync(
        string framePath,
        TrainingPreviewModelKind modelKind,
        double confidenceThreshold = 0.25,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Liefert die Qualifikation des aktiven Detektors. Fehlende oder unlesbare Angaben
    /// werden als nicht qualifiziert gemeldet; null bleibt nur fuer kompatible Fremdimplementierungen.
    /// </summary>
    Task<TrainingDetectorQualification?> GetDetectorQualificationAsync(
        CancellationToken cancellationToken = default);
}
