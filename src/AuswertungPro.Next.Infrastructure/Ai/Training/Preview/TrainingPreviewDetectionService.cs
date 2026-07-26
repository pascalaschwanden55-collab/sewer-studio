using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training.Preview;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Preview;

/// <summary>
/// Liest das ausgewaehlte Foto und ruft entweder das aktive Standardmodell oder
/// den getrennten BCC-Kandidaten auf. Der Dienst schreibt keine Dateien.
/// </summary>
public sealed class TrainingPreviewDetectionService : ITrainingPreviewDetectionService
{
    private readonly IVisionPipelineClient _pipelineClient;
    private readonly Func<string, byte[]> _readAllBytes;

    public TrainingPreviewDetectionService(
        IVisionPipelineClient pipelineClient,
        Func<string, byte[]>? readAllBytes = null)
    {
        _pipelineClient = pipelineClient ?? throw new ArgumentNullException(nameof(pipelineClient));
        _readAllBytes = readAllBytes ?? File.ReadAllBytes;
    }

    public async Task<TrainingPreviewDetectionResult> DetectAsync(
        string framePath,
        TrainingPreviewModelKind modelKind,
        double confidenceThreshold = 0.25,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(framePath))
            throw new ArgumentException("Es wurde kein Bildpfad angegeben.", nameof(framePath));
        if (confidenceThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(confidenceThreshold));

        if (modelKind == TrainingPreviewModelKind.ActiveStandard)
        {
            var qualification = await GetDetectorQualificationAsync(cancellationToken)
                .ConfigureAwait(false);
            if (qualification?.Qualified != true)
            {
                return new TrainingPreviewDetectionResult(
                    Available: false,
                    Error: qualification?.Reason
                        ?? "Der Qualifikationsstatus des Standardmodells ist nicht verfuegbar.",
                    modelKind,
                    ModelName: "Aktives Standardmodell",
                    ModelSha256: string.Empty,
                    Detections: Array.Empty<TrainingPreviewDetection>(),
                    InferenceTimeMs: 0);
            }
        }

        var imageBase64 = Convert.ToBase64String(_readAllBytes(framePath));
        var request = new YoloRequest(imageBase64, confidenceThreshold);

        if (modelKind == TrainingPreviewModelKind.BccTestCandidate)
        {
            var response = await _pipelineClient
                .DetectBccTestYoloAsync(request, cancellationToken)
                .ConfigureAwait(false);
            return new TrainingPreviewDetectionResult(
                response.Available,
                response.Error,
                modelKind,
                response.ModelName,
                response.CandidateSha256,
                Map(response.Detections),
                response.InferenceTimeMs);
        }

        var standard = await _pipelineClient
            .DetectYoloAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (standard.DetectorQualified != true)
        {
            return new TrainingPreviewDetectionResult(
                Available: false,
                Error: standard.DetectorQualificationReason
                    ?? "Die YOLO-Antwort enthaelt keine positive Modellfreigabe.",
                modelKind,
                standard.ModelName ?? "Aktives Standardmodell",
                standard.DetectorArtifactSha256 ?? string.Empty,
                Detections: Array.Empty<TrainingPreviewDetection>(),
                standard.InferenceTimeMs);
        }
        return new TrainingPreviewDetectionResult(
            Available: true,
            Error: null,
            modelKind,
            standard.ModelName ?? "Aktives Standardmodell",
            ModelSha256: standard.DetectorArtifactSha256 ?? string.Empty,
            Map(standard.Detections),
            standard.InferenceTimeMs);
    }

    /// <inheritdoc />
    public async Task<TrainingDetectorQualification?> GetDetectorQualificationAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await _pipelineClient.HealthCheckAsync(cancellationToken).ConfigureAwait(false);
            var qualification = health?.DetectorQualification;
            return qualification is null
                ? new TrainingDetectorQualification(
                    Qualified: false,
                    Reason: "Der Qualifikationsstatus des Standardmodells fehlt.")
                : new TrainingDetectorQualification(qualification.Qualified, qualification.Reason);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new TrainingDetectorQualification(
                Qualified: false,
                Reason: "Der Qualifikationsstatus des Standardmodells konnte nicht gelesen werden.");
        }
    }

    private static IReadOnlyList<TrainingPreviewDetection> Map(
        IReadOnlyList<YoloDetectionDto> detections)
        => detections
            .Select(item => new TrainingPreviewDetection(
                item.X1,
                item.Y1,
                item.X2,
                item.Y2,
                item.ClassName,
                item.Confidence))
            .ToArray();
}
