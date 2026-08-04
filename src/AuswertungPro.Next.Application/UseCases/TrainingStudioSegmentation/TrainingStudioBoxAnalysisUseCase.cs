using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;

namespace AuswertungPro.Next.Application.UseCases.TrainingStudioSegmentation;

/// <summary>Fehlerart eines parallelen SAM-/Codevergleich-Laufs.</summary>
public enum TrainingStudioBoxAnalysisFailure
{
    None,
    SidecarUnavailable,
    Failed,
}

/// <summary>
/// Ergebnis des Box-Laufs. Ein bereits fertiges Teilergebnis bleibt erhalten, wenn
/// nur der jeweils andere KI-Zweig scheitert.
/// </summary>
public sealed record TrainingStudioBoxAnalysisResult(
    WorkbenchSegmentation? Segmentation,
    WorkbenchSuggestion? Suggestion,
    TrainingStudioBoxAnalysisFailure Failure,
    Exception? Error);

/// <summary>Fachlicher Grund, weshalb eine sichtbare SAM-Maske noch kein Gold ist.</summary>
public enum TrainingStudioSegmentationValidationFailure
{
    None,
    MissingBox,
    MissingMask,
    Degraded,
    InvalidMask,
    OutsideBox,
    AreaMismatch,
}

/// <summary>
/// Ergebnis der Maskenpruefung fuer UI und Speicherschranke. Der Klartextgrund
/// bleibt erhalten, damit eine sichtbare, aber unpassende Maske nicht als
/// "fehlend" gemeldet wird.
/// </summary>
public readonly record struct TrainingStudioSegmentationValidationResult(
    bool IsValid,
    TrainingStudioSegmentationValidationFailure Failure,
    string Reason);

/// <summary>
/// Orchestriert die zwei voneinander unabhängigen KI-Aufrufe nach einer Hand-Box.
/// Die UI entscheidet nur noch über Anzeige, Abbruchbindung und Benutzertexte.
/// </summary>
public sealed class TrainingStudioBoxAnalysisUseCase
{
    private readonly IAnnotationWorkbenchService _workbench;

    public TrainingStudioBoxAnalysisUseCase(IAnnotationWorkbenchService workbench)
        => _workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));

    public async Task<TrainingStudioBoxAnalysisResult> AnalyzeAsync(
        WorkbenchItem item,
        BoundingBox box,
        string codeHint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var segmentationTask = _workbench.SegmentAsync(
            item,
            box,
            codeHint,
            cancellationToken);
        var suggestionTask = _workbench.SuggestAsync(item, box, cancellationToken);
        try
        {
            await Task.WhenAll(segmentationTask, suggestionTask).ConfigureAwait(false);
            return BuildResult(
                segmentationTask,
                suggestionTask,
                TrainingStudioBoxAnalysisFailure.None,
                error: null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SidecarUnavailableException ex)
        {
            return BuildResult(
                segmentationTask,
                suggestionTask,
                TrainingStudioBoxAnalysisFailure.SidecarUnavailable,
                ex);
        }
        catch (Exception ex)
        {
            return BuildResult(
                segmentationTask,
                suggestionTask,
                TrainingStudioBoxAnalysisFailure.Failed,
                ex);
        }
    }

    public static bool HasValidSegmentation(
        BoundingBox? box,
        WorkbenchSegmentation? segmentation)
        => ValidateSegmentation(box, segmentation).IsValid;

    public static TrainingStudioSegmentationValidationResult ValidateSegmentation(
        BoundingBox? box,
        WorkbenchSegmentation? segmentation)
    {
        if (box is not { } handBox)
        {
            return Invalid(
                TrainingStudioSegmentationValidationFailure.MissingBox,
                "Es fehlt eine gueltige rote Box.");
        }

        if (segmentation is null)
        {
            return Invalid(
                TrainingStudioSegmentationValidationFailure.MissingMask,
                "SAM hat noch keine SAM-Maske geliefert.");
        }

        if (!SamMaskFormatValidator.IsValid(
                segmentation.MaskRle,
                segmentation.MaskImageWidth,
                segmentation.MaskImageHeight,
                out var formatReason))
        {
            return Invalid(
                TrainingStudioSegmentationValidationFailure.InvalidMask,
                formatReason);
        }

        if (segmentation.Degraded)
        {
            return Invalid(
                TrainingStudioSegmentationValidationFailure.Degraded,
                "SAM hat nur eine unvollstaendige Degraded-Teilsegmentierung geliefert.");
        }

        if (!SamMaskFormatValidator.HasForegroundPixelInsideBox(
                segmentation.MaskRle,
                segmentation.MaskImageWidth,
                segmentation.MaskImageHeight,
                handBox,
                out var boxReason))
        {
            var failure = boxReason.StartsWith(
                "Hand-Box ist ungueltig",
                StringComparison.Ordinal)
                ? TrainingStudioSegmentationValidationFailure.MissingBox
                : TrainingStudioSegmentationValidationFailure.OutsideBox;
            return Invalid(failure, boxReason);
        }

        if (!SamMaskFormatValidator.TryGetForegroundPixelCount(
                segmentation.MaskRle,
                segmentation.MaskImageWidth,
                segmentation.MaskImageHeight,
                out var foregroundPixels,
                out var areaReason))
        {
            return Invalid(
                TrainingStudioSegmentationValidationFailure.InvalidMask,
                areaReason);
        }

        if (segmentation.MaskAreaPixels.HasValue
            && segmentation.MaskAreaPixels.Value != foregroundPixels)
        {
            return Invalid(
                TrainingStudioSegmentationValidationFailure.AreaMismatch,
                "Maskenflaeche widerspricht den echten Maskenpixeln "
                + $"({segmentation.MaskAreaPixels.Value} statt {foregroundPixels}).");
        }

        return new TrainingStudioSegmentationValidationResult(
            true,
            TrainingStudioSegmentationValidationFailure.None,
            string.Empty);
    }

    private static TrainingStudioSegmentationValidationResult Invalid(
        TrainingStudioSegmentationValidationFailure failure,
        string reason)
        => new(false, failure, reason);

    private static TrainingStudioBoxAnalysisResult BuildResult(
        Task<WorkbenchSegmentation> segmentationTask,
        Task<WorkbenchSuggestion> suggestionTask,
        TrainingStudioBoxAnalysisFailure failure,
        Exception? error)
        => new(
            segmentationTask.Status == TaskStatus.RanToCompletion
                ? segmentationTask.Result
                : null,
            suggestionTask.Status == TaskStatus.RanToCompletion
                ? suggestionTask.Result
                : null,
            failure,
            error);
}
