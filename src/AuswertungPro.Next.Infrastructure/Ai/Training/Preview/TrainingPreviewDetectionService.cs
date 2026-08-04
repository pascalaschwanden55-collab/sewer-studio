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
                response.Available && response.FrameUsable
                    ? Map(response.Detections)
                    : Array.Empty<TrainingPreviewDetection>(),
                response.InferenceTimeMs,
                response.FrameUsable,
                response.QualityReason);
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

    public async Task<TrainingPreviewDetectionResult> DetectBccCandidateAsync(
        string framePath,
        string candidateId,
        string candidateSha256,
        double confidenceThreshold = 0.25,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(framePath))
            throw new ArgumentException("Es wurde kein Bildpfad angegeben.", nameof(framePath));
        if (!IsSafeCandidateId(candidateId))
            throw new ArgumentException("Die BCC-Kandidaten-ID ist ungueltig.", nameof(candidateId));
        if (!IsSha256(candidateSha256))
            throw new ArgumentException("Der BCC-Kandidaten-Hash ist ungueltig.", nameof(candidateSha256));
        if (confidenceThreshold is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(confidenceThreshold));

        var request = new BccTestYoloRequest(
            Convert.ToBase64String(_readAllBytes(framePath)),
            confidenceThreshold,
            candidateId,
            candidateSha256);
        var response = await _pipelineClient
            .DetectBccTestYoloAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (response.Available
            && (!string.Equals(response.CandidateId, candidateId, StringComparison.Ordinal)
                || !string.Equals(
                    response.CandidateSha256,
                    candidateSha256,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return new TrainingPreviewDetectionResult(
                Available: false,
                Error: "Der Sidecar hat einen anderen BCC-Kandidaten geliefert. Es werden keine Boxen angezeigt.",
                TrainingPreviewModelKind.BccTestCandidate,
                ModelName: candidateId,
                ModelSha256: candidateSha256,
                Detections: Array.Empty<TrainingPreviewDetection>(),
                InferenceTimeMs: response.InferenceTimeMs);
        }

        return new TrainingPreviewDetectionResult(
            response.Available,
            response.Error,
            TrainingPreviewModelKind.BccTestCandidate,
            response.CandidateId,
            response.CandidateSha256,
            response.Available
                && response.FrameUsable
                ? Map(response.Detections)
                : Array.Empty<TrainingPreviewDetection>(),
            response.InferenceTimeMs,
            response.FrameUsable,
            response.QualityReason);
    }

    public async Task<TrainingPreviewCandidateCatalogResult> GetBccCandidatesAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await _pipelineClient
            .GetBccTestCandidatesAsync(cancellationToken)
            .ConfigureAwait(false);
        return new TrainingPreviewCandidateCatalogResult(
            response.Available,
            response.Error,
            response.Candidates
                .Select(item => new TrainingPreviewCandidateInfo(
                    item.CandidateId,
                    item.CandidateSha256,
                    item.Map50,
                    item.EpochsCompleted,
                    item.CreatedUtc))
                .ToArray());
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

    private static bool IsSha256(string? value)
        => value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static bool IsSafeCandidateId(string? value)
    {
        if (string.IsNullOrEmpty(value)
            || value.Length > 128
            || !char.IsAsciiLetterOrDigit(value[0]))
        {
            return false;
        }

        return value.All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character is '_' or '-');
    }
}
