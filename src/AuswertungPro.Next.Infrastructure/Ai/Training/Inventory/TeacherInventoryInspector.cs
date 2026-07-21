using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training.Inventory;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Inventory;

internal sealed class TeacherInventoryInspector
{
    private readonly TrainingInventoryPathResolver _pathResolver;
    private readonly bool _evalChecksEnabled;
    private readonly bool _evalProtectionAvailable;
    private readonly IReadOnlySet<string> _evalImageHashes;
    private readonly IReadOnlySet<string> _evalHoldingKeys;

    public TeacherInventoryInspector(
        TrainingInventoryPathResolver pathResolver,
        bool evalChecksEnabled,
        bool evalProtectionAvailable,
        IReadOnlySet<string> evalImageHashes,
        IReadOnlySet<string> evalHoldingKeys)
    {
        _pathResolver = pathResolver;
        _evalChecksEnabled = evalChecksEnabled;
        _evalProtectionAvailable = evalProtectionAvailable;
        _evalImageHashes = evalImageHashes;
        _evalHoldingKeys = evalHoldingKeys;
    }

    public async Task<TeacherInventoryRecord> InspectAsync(
        TeacherAnnotation annotation,
        int sourceIndex,
        CancellationToken cancellationToken)
    {
        var fullFrame = await _pathResolver.ResolveAsync(
            annotation.FullFramePath,
            hashContent: true,
            cancellationToken).ConfigureAwait(false);
        var croppedRegion = await _pathResolver.ResolveAsync(
            annotation.CroppedRegionPath,
            hashContent: true,
            cancellationToken).ConfigureAwait(false);
        var yoloAnnotation = await _pathResolver.ResolveAsync(
            annotation.YoloAnnotationPath,
            hashContent: true,
            cancellationToken).ConfigureAwait(false);
        var video = await _pathResolver.ResolveAsync(
            annotation.VideoPath,
            hashContent: false,
            cancellationToken).ConfigureAwait(false);

        var boxState = TeacherInventoryPolicy.ClassifyBox(annotation);
        var holding = TeacherInventoryPolicy.ClassifyHolding(annotation);
        var evalState = TeacherInventoryPolicy.ClassifyEvalState(
            fullFrame,
            annotation.HaltungName,
            _evalChecksEnabled,
            _evalProtectionAvailable,
            _evalImageHashes,
            _evalHoldingKeys);
        var disposition = TeacherInventoryPolicy.ClassifyDisposition(
            fullFrame,
            holding.State,
            boxState,
            evalState);

        return new TeacherInventoryRecord
        {
            RecordKey = string.IsNullOrWhiteSpace(annotation.AnnotationId)
                ? $"teacher-{sourceIndex:D6}"
                : annotation.AnnotationId.Trim(),
            VsaCode = annotation.VsaCode?.Trim() ?? string.Empty,
            FullFrame = fullFrame,
            CroppedRegion = croppedRegion,
            YoloAnnotation = yoloAnnotation,
            Video = video,
            BoxState = boxState,
            HoldingState = holding.State,
            SuggestedHolding = holding.SuggestedHolding,
            HoldingCandidates = holding.Candidates,
            Disposition = disposition,
            EvalState = evalState,
            ReasonCodes = TeacherInventoryPolicy.BuildReasonCodes(
                fullFrame,
                boxState,
                holding,
                evalState,
                disposition)
        };
    }
}
