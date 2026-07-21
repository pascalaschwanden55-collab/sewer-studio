using System.IO;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Training;

internal enum TrainingReviewSamOutcome
{
    Completed,
    MissingSelection,
    MissingBox,
    MissingFrame
}

internal sealed record TrainingReviewSamSelection(
    string? FramePath,
    string ProtocolCode);

internal sealed record TrainingReviewSamWorkflowRequest(
    TrainingReviewSamSelection? Selection,
    BoundingBox? Box,
    Action OnStarted,
    CancellationToken CancellationToken);

internal sealed record TrainingReviewSamWorkflowResult(
    TrainingReviewSamOutcome Outcome,
    TrainingReviewSamResult? Segmentation,
    TrainingSegmentationMask? PendingMask,
    string StatusText,
    string? UserHint = null);

/// <summary>
/// Prueft eine Review-Auswahl, startet SAM und bereitet das Ergebnis fuer das Fenster auf.
/// Technische Fehler und Fenster-Abbrueche werden bewusst an das Fenster weitergegeben.
/// </summary>
internal sealed class TrainingReviewSamWorkflow
{
    private const int DefaultPipeDiameterMm = 300;

    private readonly Func<ITrainingReviewSamSegmentationService> _getSegmentationService;
    private readonly Func<int?> _resolvePipeDiameterMm;
    private readonly Func<string, bool> _fileExists;

    public TrainingReviewSamWorkflow(
        Func<ITrainingReviewSamSegmentationService> getSegmentationService,
        Func<int?> resolvePipeDiameterMm,
        Func<string, bool>? fileExists = null)
    {
        _getSegmentationService = getSegmentationService
            ?? throw new ArgumentNullException(nameof(getSegmentationService));
        _resolvePipeDiameterMm = resolvePipeDiameterMm
            ?? throw new ArgumentNullException(nameof(resolvePipeDiameterMm));
        _fileExists = fileExists ?? File.Exists;
    }

    public async Task<TrainingReviewSamWorkflowResult> ExecuteAsync(
        TrainingReviewSamWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.OnStarted);

        if (request.Selection is null)
        {
            return ExpectedFailure(
                TrainingReviewSamOutcome.MissingSelection,
                "Bitte zuerst einen Review-Kandidaten waehlen.");
        }

        if (request.Box is not { } box)
        {
            return ExpectedFailure(
                TrainingReviewSamOutcome.MissingBox,
                "Bitte zuerst eine Box um den Schaden ziehen.");
        }

        var framePath = request.Selection.FramePath;
        if (string.IsNullOrWhiteSpace(framePath) || !_fileExists(framePath))
        {
            return ExpectedFailure(
                TrainingReviewSamOutcome.MissingFrame,
                "Der Review-Frame ist nicht verfuegbar.");
        }

        // Ab hier laeuft der echte Vorgang. Das Fenster sperrt nun die Schaltflaeche
        // und entfernt eine eventuell noch angezeigte alte Maske.
        request.OnStarted();

        var pipeDiameterMm = _resolvePipeDiameterMm() ?? DefaultPipeDiameterMm;
        var segmentation = await _getSegmentationService().SegmentFrameFileAsync(
            framePath,
            box,
            request.Selection.ProtocolCode,
            pipeDiameterMm,
            request.CancellationToken).ConfigureAwait(false);

        return new TrainingReviewSamWorkflowResult(
            TrainingReviewSamOutcome.Completed,
            segmentation,
            CreateTrainingSegmentationMask(segmentation.Response),
            BuildStatus(segmentation.Response));
    }

    private static TrainingReviewSamWorkflowResult ExpectedFailure(
        TrainingReviewSamOutcome outcome,
        string userHint)
        => new(outcome, null, null, string.Empty, userHint);

    private static TrainingSegmentationMask? CreateTrainingSegmentationMask(SamResponse response)
    {
        var mask = response.Masks.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.MaskRle));
        if (mask is null)
            return null;

        return new TrainingSegmentationMask(
            mask.MaskRle,
            response.ImageWidth,
            response.ImageHeight,
            mask.MaskAreaPixels,
            mask.Confidence,
            mask.Label);
    }

    private static string BuildStatus(SamResponse response)
    {
        if (response.Masks.Count > 0)
            return $"SAM: {response.Masks.Count} Maske(n) - wird mit Akzeptieren gespeichert";

        if (!string.IsNullOrWhiteSpace(response.Error))
            return $"SAM: keine Maske ({response.Error})";

        if (response.SkippedBoxes > 0)
        {
            return $"SAM: keine Maske ({response.SkippedBoxes}/{response.RequestedBoxes} Box(en) uebersprungen)";
        }

        return "SAM: keine Maske";
    }
}
