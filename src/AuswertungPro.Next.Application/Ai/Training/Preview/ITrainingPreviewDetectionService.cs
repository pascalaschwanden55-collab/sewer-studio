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
    double InferenceTimeMs,
    bool FrameUsable = true,
    string? QualityReason = null);

/// <summary>
/// Qualifikationsstand des aktiven Standard-Detektors (aus der Sidecar-Statusdatei).
/// Null = nicht abrufbar. Das Training Studio behandelt diesen Zustand wie
/// "nicht freigegeben" und startet keinen Standardmodell-Fototest.
/// </summary>
public sealed record TrainingDetectorQualification(bool Qualified, string? Reason);

/// <summary>Pfadfreie Auswahlmetadaten eines manifest- und hashgeprueften Kandidaten.</summary>
public sealed record TrainingPreviewCandidateInfo(
    string CandidateId,
    string CandidateSha256,
    double Map50,
    int EpochsCompleted,
    string CreatedUtc);

public sealed record TrainingPreviewCandidateCatalogResult(
    bool Available,
    string? Error,
    IReadOnlyList<TrainingPreviewCandidateInfo> Candidates);

public interface ITrainingPreviewDetectionService
{
    Task<TrainingPreviewDetectionResult> DetectAsync(
        string framePath,
        TrainingPreviewModelKind modelKind,
        double confidenceThreshold = 0.25,
        CancellationToken cancellationToken = default);

    Task<TrainingPreviewDetectionResult> DetectBccCandidateAsync(
        string framePath,
        string candidateId,
        string candidateSha256,
        double confidenceThreshold = 0.25,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new TrainingPreviewDetectionResult(
            Available: false,
            Error: "Dieser Dienst unterstuetzt die exakte Anheftung eines BCC-Kandidaten nicht.",
            TrainingPreviewModelKind.BccTestCandidate,
            ModelName: candidateId,
            ModelSha256: candidateSha256,
            Detections: [],
            InferenceTimeMs: 0));

    Task<TrainingPreviewCandidateCatalogResult> GetBccCandidatesAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(new TrainingPreviewCandidateCatalogResult(
            Available: false,
            Error: "Die BCC-Kandidatenliste wird von diesem Dienst nicht unterstuetzt.",
            Candidates: []));

    /// <summary>
    /// Liefert die Qualifikation des aktiven Detektors. Fehlende oder unlesbare Angaben
    /// werden als nicht qualifiziert gemeldet; null bleibt nur fuer kompatible Fremdimplementierungen.
    /// </summary>
    Task<TrainingDetectorQualification?> GetDetectorQualificationAsync(
        CancellationToken cancellationToken = default);
}
